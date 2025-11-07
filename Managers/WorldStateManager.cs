extern alias GameMapModel;

using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using Game.Core;
using Game.Core.Coordinates;
using Game.Core.Research;

using MapModel = GameMapModel.MapModel;

namespace Shapez2MultiplayerMod.Managers
{
    public class WorldStateManager
    {
        private readonly NetworkManager networkManager;
        private MonoBehaviour coroutineHost;
        public BuildingReplicator BuildingReplicator { get; private set; }

        public WorldStateManager(NetworkManager manager)
        {
            networkManager = manager;
        }

        public void Initialize()
        {
            networkManager.OnClientConnected += OnClientConnected;
            networkManager.OnPacketReceived += OnPacketReceived;
        }

        public void SetCoroutineHost(MonoBehaviour host)
        {
            coroutineHost = host;
            BuildingReplicator = new BuildingReplicator(host);
        }

        private void OnClientConnected(int clientId)
        {
            if (networkManager.IsServer)
            {
                MultiplayerMod.Log.LogInfo($"New client ({clientId}) connected. Scheduling full state send.");
                coroutineHost.StartCoroutine(SendFullStateAfterDelay(clientId));
            }
        }

        private IEnumerator SendFullStateAfterDelay(int clientId)
        {
            yield return new WaitForSeconds(2.0f);
            SendWorldStateToClient(clientId);
            yield return new WaitForSeconds(0.5f);
            SendGameStateToClient(clientId);
        }

        public WorldStatePacket CaptureWorldState()
        {
            try
            {
                var gameCore = Singleton<StaticGameCoreAccessor>.G;
                var mapModel = gameCore?.LocalPlayer?.CurrentMap as MapModel;
                if (mapModel == null)
                {
                    MultiplayerMod.Log.LogError("[WorldState] Could not get MapModel to capture state.");
                    return null;
                }

                var worldStatePacket = new WorldStatePacket
                {
                    WorldSeed = gameCore.Savegame.Parameters.Seed,
                    Islands = new List<IslandData>(),
                    Buildings = new List<BuildingData>()
                };

                foreach (var island in mapModel.Islands)
                {
                    worldStatePacket.Islands.Add(new IslandData
                    {
                        IslandType = island.Definition.Id.ToString(),
                        X = island.Position.x,
                        Y = island.Position.y,
                        Rotation = (int)island.Rotation.Value
                    });
                }

                foreach (var building in mapModel.Buildings)
                {
                    worldStatePacket.Buildings.Add(new BuildingData
                    {
                        BuildingType = building.Definition.Id.ToString(),
                        X = building.Tile_G.x,
                        Y = building.Tile_G.y,
                        Z = building.Tile_G.z,
                        Rotation = (int)building.Rotation_G.Value
                    });
                }

                return worldStatePacket;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldState] CRITICAL error during world capture: {ex.Message}");
                return null;
            }
        }

        public void SendWorldStateToClient(int clientId)
        {
            if (!networkManager.IsServer) return;
            MultiplayerMod.Log.LogInfo($"[WorldState] Capturing and sending initial world state to client {clientId}...");
            
            var worldStatePacket = CaptureWorldState();
            if(worldStatePacket != null)
            {
                networkManager.SendToClient(clientId, worldStatePacket);
                MultiplayerMod.Log.LogInfo($"[WorldState] Sent world state with {worldStatePacket.Islands.Count} islands and {worldStatePacket.Buildings.Count} buildings.");
            }
        }

        public void SendGameStateToClient(int clientId)
        {
            if (!networkManager.IsServer) return;
            MultiplayerMod.Log.LogInfo($"[GameState] Capturing and sending game state to client {clientId}...");

            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore != null && gameCore.Research != null)
            {
                var gameStatePacket = new GameStatePacket
                {
                    ResearchPoints = gameCore.Research.PointStorage.Points.Amount,
                    UnlockedMilestoneIds = gameCore.Research.Progress.UnlockedUpgrades.Select(id => id.Id).ToList(),
                    LinearUpgradeLevels = gameCore.Research.LinearUpgradeManager.Levels.ToDictionary(kvp => kvp.Key.Id, kvp => kvp.Value)
                };
                networkManager.SendToClient(clientId, gameStatePacket);
                MultiplayerMod.Log.LogInfo($"[GameState] Sent game state packet.");
            }
        }

        private void OnPacketReceived(int senderId, NetworkPacket packet)
        {
            if (packet is WorldStatePacket worldPacket)
            {
                // This is for the INITIAL load for a new client
                ApplyWorldState(worldPacket, () => {
                    if (networkManager.IsClient)
                    {
                        MultiplayerMod.Log.LogInfo("[WorldState] Client finished initial load, sending reload request to server.");
                        networkManager.SendToServer(new RequestWorldReloadPacket());
                    }
                });
            }
            else if (packet is GameStatePacket gamePacket)
            {
                ApplyGameState(gamePacket);
            }
            // This handles the BROADCASTED reload request from the server
            else if (packet is RequestWorldReloadPacket reloadPacket)
            {
                if (reloadPacket.WorldState != null)
                {
                    MultiplayerMod.Log.LogInfo("[WorldState] Received world reload command from server. Applying...");
                    ApplyWorldState(reloadPacket.WorldState, null); 
                }
            }
        }

        public void ApplyWorldState(WorldStatePacket packet, Action onComplete)
        {
            if (packet == null)
            {
                MultiplayerMod.Log.LogError("[WorldState] Received null world state packet. Aborting apply.");
                return;
            }
            BuildingReplicator.ApplyWorldState(packet, onComplete);
        }

        public void ApplyGameState(GameStatePacket packet)
        {
            MultiplayerMod.Log.LogInfo("[GameState] Received game state from host. Applying non-destructively...");
            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore?.Research == null) { MultiplayerMod.Log.LogError("[GameState] GameCore not ready, cannot apply state."); return; }

            foreach (var milestoneId in packet.UnlockedMilestoneIds)
            {
                var milestone = gameCore.Research.Layout.AllUpgrades.FirstOrDefault(u => u.Id.Id == milestoneId);
                if (milestone != null && !gameCore.Research.Progress.IsUnlocked(milestone))
                {
                    gameCore.Research.UnlockManager.TryUnlock(milestone, forced: true);
                }
            }
            var linearManager = gameCore.Research.LinearUpgradeManager;
            var setLevelMethod = typeof(ResearchLinearUpgradeManager).GetMethod("SetLevel", BindingFlags.NonPublic | BindingFlags.Instance);
            if (setLevelMethod != null)
            {
                foreach (var kvp in packet.LinearUpgradeLevels)
                {
                    setLevelMethod.Invoke(linearManager, new object[] { new ResearchLinearUpgradeId(kvp.Key), kvp.Value });
                }
            }
            gameCore.Research.PointStorage.Set(new ResearchPointCurrency(packet.ResearchPoints));

            MultiplayerMod.Log.LogInfo("[GameState] All data synced. Forcing a final HUD refresh.");
            MultiplayerMod.Instance.ForceHudRefresh();
        }

        public void Shutdown()
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnected -= OnClientConnected;
                networkManager.OnPacketReceived -= OnPacketReceived;
            }
        }
    }
}