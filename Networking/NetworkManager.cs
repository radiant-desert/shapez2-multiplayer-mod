extern alias GameMap;
extern alias GameMapSim;
extern alias GameMapModel;

using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using Shapez2MultiplayerMod.Models;
using UnityEngine;
using Newtonsoft.Json;
using System.Text;
using System.Linq;
using Game.Core.Research;
using System.Reflection;
using Game.Core.Coordinates;
using Shapez2MultiplayerMod.Patches;

namespace Shapez2MultiplayerMod.Networking
{
    public class NetworkManager
    {
        // Network state
        public bool IsServer { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsConnected { get; private set; }
        public int ConnectedPlayersCount => peers.Count;
        
        // LiteNetLib components
        private NetManager netManager;
        private EventBasedNetListener listener;
        private NetPeer serverPeer; // For clients: connection to server
        private Dictionary<int, NetPeer> peers = new Dictionary<int, NetPeer>(); // For server: connected clients
        
        // Connection key for security
        private const string CONNECTION_KEY = "Shapez2Multiplayer";
        
        // JSON serialization settings with type information
        private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            Formatting = Formatting.None
        };
        
        // Events
        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;
        public event Action<int, NetworkPacket> OnPacketReceived;
        public event Action<string> OnConnectionFailed;
        public event Action OnServerStarted;
        public event Action OnConnectedToServer;
        public event Action OnDisconnectedFromServer;

        private int nextClientId = 1;

        public NetworkManager()
        {
            MultiplayerMod.Log.LogInfo("[NetworkManager] Initialized with LiteNetLib");
        }
        
        public bool StartServer(int port = MultiplayerMod.DEFAULT_PORT)
        {
            if (IsServer || IsClient) { MultiplayerMod.Log.LogWarning("[NetworkManager] Already running as server or client"); return false; }
            try {
                listener = new EventBasedNetListener();
                netManager = new NetManager(listener);
                listener.ConnectionRequestEvent += OnConnectionRequest;
                listener.PeerConnectedEvent += OnPeerConnected;
                listener.PeerDisconnectedEvent += OnPeerDisconnected;
                listener.NetworkReceiveEvent += OnNetworkReceive;
                listener.NetworkErrorEvent += OnNetworkError;
                if (netManager.Start(port)) {
                    IsServer = true;
                    IsConnected = true;
                    MultiplayerMod.Log.LogInfo($"[NetworkManager] Server started on port {port}");
                    OnServerStarted?.Invoke();
                    return true;
                } else { MultiplayerMod.Log.LogError("[NetworkManager] Failed to start server"); return false; }
            } catch (Exception ex) { MultiplayerMod.Log.LogError($"[NetworkManager] Failed to start server: {ex.Message}"); return false; }
        }

        private void OnConnectionRequest(ConnectionRequest request)
        {
            if (!IsServer) return;
            string data = request.Data.GetString();
            string[] parts = data.Split(':');
            if (parts.Length == 2 && parts[0] == CONNECTION_KEY)
            {
                string clientName = parts[1];
                MultiplayerMod.Log.LogInfo($"[NetworkManager] Connection request from '{clientName}'. Accepting.");
                NetPeer peer = request.Accept();
                peer.Tag = clientName; 
            }
            else
            {
                MultiplayerMod.Log.LogWarning($"[NetworkManager] Connection request rejected. Invalid key or format.");
                request.Reject();
            }
        }

        private void OnPeerConnected(NetPeer peer)
        {
            if (IsServer)
            {
                string clientName = peer.Tag as string;
                int newClientId = nextClientId++;
                if (string.IsNullOrEmpty(clientName))
                {
                    clientName = $"Player_{newClientId}";
                }
                MultiplayerMod.Log.LogInfo($"[NetworkManager] Peer connected. Assigning ClientId: {newClientId} to '{clientName}'.");
                peers[newClientId] = peer;

                var existingPlayers = MultiplayerMod.Instance.PlayerManager.GetAllPlayers();
                var newPlayer = new PlayerInfo { PlayerId = newClientId, PlayerName = clientName };

                var welcomePacket = new PlayerJoinedPacket
                {
                    Player = newPlayer,
                    ExistingPlayers = existingPlayers
                };
                SendToClient(newClientId, welcomePacket);

                MultiplayerMod.Instance.PlayerManager.AddPlayer(newPlayer);
                
                var broadcastPacket = new PlayerJoinedPacket { Player = newPlayer };
                BroadcastToClients(broadcastPacket, newClientId);
                
                OnClientConnected?.Invoke(newClientId);
            }
            else if (IsClient)
            {
                serverPeer = peer;
                IsConnected = true;
                MultiplayerMod.Log.LogInfo("========================================");
                MultiplayerMod.Log.LogInfo($"[NetworkManager] ✓ Successfully connected to server!");
                MultiplayerMod.Log.LogInfo($"[NetworkManager] Server address: {peer.Address}");
                MultiplayerMod.Log.LogInfo($"[NetworkManager] Your peer ID from server's perspective: {peer.Id}");
                MultiplayerMod.Log.LogInfo("========================================");
                OnConnectedToServer?.Invoke();
            }
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (IsServer)
            {
                int disconnectedClientId = -1;
                foreach (var entry in peers)
                {
                    if (entry.Value == peer)
                    {
                        disconnectedClientId = entry.Key;
                        break;
                    }
                }
                if (disconnectedClientId != -1)
                {
                    MultiplayerMod.Log.LogInfo($"[NetworkManager] Client {disconnectedClientId} disconnected.");
                    peers.Remove(disconnectedClientId);
                    MultiplayerMod.Instance.PlayerManager.RemovePlayer(disconnectedClientId);
                    BroadcastToClients(new PlayerLeftPacket { PlayerId = disconnectedClientId });
                    OnClientDisconnected?.Invoke(disconnectedClientId);
                }
            }
            else if (IsClient)
            {
                serverPeer = null;
                IsConnected = false;
                OnDisconnectedFromServer?.Invoke();
            }
        }
        
        public void SendToClient(int clientId, NetworkPacket packet)
        {
            if (!IsServer) return;
            if (peers.TryGetValue(clientId, out NetPeer peer)) {
                try { peer.Send(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet, jsonSettings)), DeliveryMethod.ReliableOrdered);
                } catch (Exception ex) { MultiplayerMod.Log.LogError($"[NetworkManager] Failed to send to client {clientId}: {ex.Message}"); }
            }
        }
        
        public void BroadcastToClients(NetworkPacket packet, int excludeClientId = -1)
        {
            if (!IsServer) return;
            try {
                byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet, jsonSettings));
                foreach (var kvp in peers) {
                    if (kvp.Key != excludeClientId) {
                        kvp.Value.Send(data, DeliveryMethod.ReliableOrdered);
                    }
                }
            } catch (Exception ex) { MultiplayerMod.Log.LogError($"[NetworkManager] Failed to broadcast: {ex.Message}"); }
        }
        
        public bool ConnectToServer(string host, int port, string playerName)
        {
            if (IsServer || IsClient) { MultiplayerMod.Log.LogWarning("[NetworkManager] Already running"); return false; }
            try {
                listener = new EventBasedNetListener();
                netManager = new NetManager(listener);
                listener.PeerConnectedEvent += OnPeerConnected;
                listener.PeerDisconnectedEvent += OnPeerDisconnected;
                listener.NetworkReceiveEvent += OnNetworkReceive;
                listener.NetworkErrorEvent += OnNetworkError;
                netManager.Start();
                NetDataWriter writer = new NetDataWriter();
                writer.Put($"{CONNECTION_KEY}:{playerName}"); 
                netManager.Connect(host, port, writer);
                IsClient = true;
                return true;
            } catch (Exception ex) { MultiplayerMod.Log.LogError($"[NetworkManager] Failed to connect: {ex.Message}"); OnConnectionFailed?.Invoke(ex.Message); return false; }
        }
        
        public void SendToServer(NetworkPacket packet)
        {
            if (!IsClient || serverPeer == null) return;
            try { serverPeer.Send(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet, jsonSettings)), DeliveryMethod.ReliableOrdered);
            } catch (Exception ex) { MultiplayerMod.Log.LogError($"[NetworkManager] Failed to send to server: {ex.Message}"); }
        }
        
        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            try
            {
                byte[] rawData = reader.GetRemainingBytes();
                string json = Encoding.UTF8.GetString(rawData);
                NetworkPacket packet = JsonConvert.DeserializeObject<NetworkPacket>(json, jsonSettings);

                if (packet != null)
                {
                    if (packet is RequestModifyWorldPacket request)
                    {
                        if (IsServer)
                        {
                            MultiplayerMod.Log.LogInfo("[Server] Received world modification, broadcasting to ALL clients.");
                            // --- THE FIX ---
                            // Broadcast to ALL clients, including the original sender to confirm the action.
                            // Do NOT exclude the sender.
                            BroadcastToClients(request);
                        }

                        MultiplayerMod.Log.LogInfo("[NetworkManager] Applying world modification packet.");
                        MultiplayerMod.Instance.WorldStateManager.BuildingReplicator.ApplyModificationPacket(request);
                        return;
                    }
                        
                    if (packet is PlayerJoinedPacket joinedPacket)
                    {
                        if (IsClient)
                        {
                            if (joinedPacket.Player.PlayerId != 0 && (joinedPacket.ExistingPlayers != null && joinedPacket.ExistingPlayers.Any()))
                            {
                                MultiplayerMod.Log.LogInfo($"[NetworkManager] Received my welcome packet! My new ID is {joinedPacket.Player.PlayerId}.");
                                MultiplayerMod.Instance.PlayerManager.LocalPlayerId = joinedPacket.Player.PlayerId;
                                MultiplayerMod.Instance.PlayerManager.AddPlayer(joinedPacket.Player);
                                
                                foreach (var p in joinedPacket.ExistingPlayers) { MultiplayerMod.Instance.PlayerManager.AddPlayer(p); }
                            }
                            else
                            {
                                MultiplayerMod.Log.LogInfo($"[NetworkManager] Another player joined: {joinedPacket.Player.PlayerName}");
                                MultiplayerMod.Instance.PlayerManager.AddPlayer(joinedPacket.Player);
                            }
                        }
                        return;
                    }

                    if (packet is PlayerLeftPacket leftPacket)
                    {
                        if (IsClient) { MultiplayerMod.Instance.PlayerManager.RemovePlayer(leftPacket.PlayerId); }
                        return;
                    }
                    
                    if (packet is PlayerPositionPacket positionPacket)
                    {
                        if (IsServer)
                        {
                            int senderId = -1;
                            foreach(var entry in peers) { if (entry.Value == peer) { senderId = entry.Key; break; } }
                            BroadcastToClients(positionPacket, senderId);
                        }
                        if (positionPacket.PlayerId != MultiplayerMod.Instance.PlayerManager.LocalPlayerId)
                        {
                            Vector3 newPos = new Vector3(positionPacket.X, positionPacket.Y, positionPacket.Z);
                            MultiplayerMod.Instance.MarkerManager.UpdateMarkerPosition(positionPacket.PlayerId.ToString(), newPos);
                        }
                        return;
                    }
                    if (packet is PlayerGhostPlacementPacket ghostPacket)
                    {
                        if (ghostPacket.PlayerId != MultiplayerMod.Instance.PlayerManager.LocalPlayerId)
                        {
                            MultiplayerMod.Instance.GhostPlacementManager.UpdateGhostsForPlayer(ghostPacket);
                        }
                        if (IsServer)
                        {
                             int senderId = -1;
                            foreach(var entry in peers) { if (entry.Value == peer) { senderId = entry.Key; break; } }
                            BroadcastToClients(ghostPacket, senderId);
                        }
                        return;
                    }
                    if (packet is PlayerIslandGhostPlacementPacket islandGhostPacket)
                    {
                        if (islandGhostPacket.PlayerId != MultiplayerMod.Instance.PlayerManager.LocalPlayerId)
                        {
                            MultiplayerMod.Instance.IslandPlacementManager.UpdateGhostsForPlayer(islandGhostPacket);
                        }
                        if (IsServer)
                        {
                            int senderId = -1;
                            foreach(var entry in peers) { if (entry.Value == peer) { senderId = entry.Key; break; } }
                            BroadcastToClients(islandGhostPacket, senderId);
                        }
                        return;
                    }
                    if (packet is RequestWorldReloadPacket)
                    {
                        if (IsServer)
                        {
                            var definitiveWorldState = MultiplayerMod.Instance.WorldStateManager.CaptureWorldState();
                            if (definitiveWorldState != null)
                            {
                                var broadcastPacket = new RequestWorldReloadPacket { WorldState = definitiveWorldState };
                                BroadcastToClients(broadcastPacket);
                                MultiplayerMod.Instance.WorldStateManager.ApplyWorldState(definitiveWorldState, null);
                            }
                        }
                        return;
                    }
                    
                    if (packet is WorldStatePacket || packet is GameStatePacket || (packet is RequestWorldReloadPacket && IsClient))
                    {
                        OnPacketReceived?.Invoke(peer.Id, packet);
                        return;
                    }

                    var gameCore = Singleton<StaticGameCoreAccessor>.G;
                    if (gameCore != null)
                    {

                        if (IsServer)
                        {
                            switch (packet.Type)
                            {
                                case PacketType.RequestTogglePause:
                                    gameCore.SimulationSpeed.IsPaused = !gameCore.SimulationSpeed.IsPaused;
                                    break;
                                case PacketType.RequestUnlockUpgrade:
                                    var requestUnlockPacket = packet as RequestUnlockUpgradePacket;
                                    var upgradeToUnlock = gameCore.Research.Layout.AllUpgrades.FirstOrDefault(u => u.Id.Id == requestUnlockPacket.UpgradeId);
                                    if (upgradeToUnlock != null) { gameCore.Research.UnlockManager.TryUnlock(upgradeToUnlock); }
                                    break;
                                case PacketType.RequestLevelUpUpgrade:
                                    var requestLevelUpPacket = packet as RequestLevelUpUpgradePacket;
                                    var upgradeId = new ResearchLinearUpgradeId(requestLevelUpPacket.UpgradeId);
                                    gameCore.Research.LinearUpgradeManager.TryLevelUp(upgradeId);
                                    break;
                            }   
                        }
                        else if (IsClient)
                        {
                           switch (packet.Type)
                            {
                                case PacketType.ConfirmPauseState:
                                    var pausePacket = packet as ConfirmPauseStatePacket;
                                    PauseSyncHook.IsApplyingFromServer = true;
                                    gameCore.SimulationSpeed.IsPaused = pausePacket.IsPaused;
                                    PauseSyncHook.IsApplyingFromServer = false;
                                    break;
                                case PacketType.ConfirmUnlockUpgrade:
                                    var confirmUnlockPacket = packet as ConfirmUnlockUpgradePacket;
                                    var upgradeToApply = gameCore.Research.Layout.AllUpgrades.FirstOrDefault(u => u.Id.Id == confirmUnlockPacket.UpgradeId);
                                    if (upgradeToApply != null)
                                    {
                                        gameCore.Research.UnlockManager.TryUnlock(upgradeToApply, forced: true);
                                        MultiplayerMod.Instance.ForceHudRefresh();
                                    }
                                    break;
                                case PacketType.ConfirmLevelUpUpgrade:
                                    var confirmLevelUpPacket = packet as ConfirmLevelUpUpgradePacket;
                                    var upgradeIdToApply = new ResearchLinearUpgradeId(confirmLevelUpPacket.UpgradeId);
                                    var linearManager = gameCore.Research.LinearUpgradeManager;
                                    try {
                                        var setLevelMethod = typeof(ResearchLinearUpgradeManager).GetMethod("SetLevel", BindingFlags.NonPublic | BindingFlags.Instance);
                                        var onChangedField = typeof(ResearchLinearUpgradeManager).GetField("_OnChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (setLevelMethod != null && onChangedField != null) {
                                            setLevelMethod.Invoke(linearManager, new object[] { upgradeIdToApply, confirmLevelUpPacket.NewLevel });
                                            var onChangedEvent = onChangedField.GetValue(linearManager);
                                            onChangedEvent.GetType().GetMethod("Invoke").Invoke(onChangedEvent, new object[] { upgradeIdToApply, confirmLevelUpPacket.NewLevel });
                                            MultiplayerMod.Instance.ForceHudRefresh();
                                        }
                                    } catch (Exception ex) {
                                        MultiplayerMod.Log.LogError($"[CLIENT-RECEIVE] > CRITICAL: Error with reflection: {ex.Message}");
                                    }
                                    break;
                                case PacketType.UpdateCurrency:
                                    var currencyPacket = packet as UpdateCurrencyPacket;
                                    gameCore.Research.PointStorage.Set(new ResearchPointCurrency(currencyPacket.NewResearchPoints));
                                    break;
                            }
                        }
                    }
                }
            } 
            catch (Exception ex) 
            {
                MultiplayerMod.Log.LogError($"[NetworkManager] Failed to process received packet: {ex.Message}");
            } 
            finally 
            {
                reader.Recycle();
            }
        }

        private void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            MultiplayerMod.Log.LogError($"[NetworkManager] Network error from {endPoint}: {socketError}");
        }

        public void Update()
        {
            netManager?.PollEvents();
        }

        public void Shutdown()
        {
            MultiplayerMod.Log.LogInfo("[NetworkManager] Shutting down...");
            nextClientId = 1;
            if (netManager != null) { netManager.Stop(); netManager = null; }
            listener = null; serverPeer = null; peers.Clear();
            IsServer = false; IsClient = false; IsConnected = false;
            MultiplayerMod.Log.LogInfo("[NetworkManager] Shutdown complete");
        }

        public string GetStatusString()
        {
            if (IsServer) return $"Server: {ConnectedPlayersCount} player(s) connected";
            if (IsClient && IsConnected) return "Connected to server";
            if (IsClient && !IsConnected) return "Connecting...";
            return "Not connected";
        }
    }
}