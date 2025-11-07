using System;
using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod.Networking;
using UnityEngine;

namespace Shapez2MultiplayerMod.UI
{
    /// <summary>
    /// Simple UI for multiplayer functionality
    /// Provides host/join interface and player list
    /// </summary>
    public class MultiplayerUI : MonoBehaviour
    {
        private NetworkManager networkManager;
        private bool showUI = false;
        private Rect windowRect = new Rect(20, 20, 400, 500);
        
        // UI state
        private string serverAddress = "127.0.0.1";
        private string serverPort = "7777";
        private string playerName = "Player";
        private string statusMessage = "";
        
        // UI mode
        private enum UIMode
        {
            MainMenu,
            HostSettings,
            JoinSettings,
            InGame
        }
        
        private UIMode currentMode = UIMode.MainMenu;
        
        // Debug
        private bool hasLoggedStart = false;

        void Start()
        {
            MultiplayerMod.Log.LogInfo("MultiplayerUI Start() called - UI component is active!");
            hasLoggedStart = true;
        }

        public void Initialize(NetworkManager netManager)
        {
            networkManager = netManager;
            
            // --- SIMPLIFIED ---
            // We now just use a simple default name. The user can change it in the UI.
            playerName = "Player_" + UnityEngine.Random.Range(1000, 9999);
            
            // Subscribe to network events
            networkManager.OnServerStarted += OnServerStarted;
            networkManager.OnConnectedToServer += OnConnectedToServer;
            networkManager.OnDisconnectedFromServer += OnDisconnectedFromServer;
            networkManager.OnClientConnected += OnClientConnected;
            networkManager.OnClientDisconnected += OnClientDisconnected;
            networkManager.OnConnectionFailed += OnConnectionFailed;
            
            MultiplayerMod.Log.LogInfo($"MultiplayerUI initialized with player name: {playerName}");
        }
        
        // --- REMOVED ---
        // The entire GetSteamName() method has been removed.
        
        void OnDestroy()
        {
            // Unsubscribe from events
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= OnServerStarted;
                networkManager.OnConnectedToServer -= OnConnectedToServer;
                networkManager.OnDisconnectedFromServer -= OnDisconnectedFromServer;
                networkManager.OnClientConnected -= OnClientConnected;
                networkManager.OnClientDisconnected -= OnClientDisconnected;
                networkManager.OnConnectionFailed -= OnConnectionFailed;
            }
        }
        
        // Network event handlers
        private void OnServerStarted()
        {
            statusMessage = "Server started! Waiting for players...";
            MultiplayerMod.Log.LogInfo("[UI] Server started event received");
            
            if (MultiplayerMod.Instance?.PositionSync != null)
            {
                MultiplayerMod.Instance.PositionSync.StartSyncing();
            }
        }
        
        private void OnConnectedToServer()
        {
            statusMessage = "Connected to server!";
            currentMode = UIMode.InGame;
            MultiplayerMod.Log.LogInfo("[UI] Connected to server event received");
            
            if (MultiplayerMod.Instance?.PositionSync != null)
            {
                MultiplayerMod.Instance.PositionSync.StartSyncing();
            }
        }
        
        private void OnDisconnectedFromServer()
        {
            statusMessage = "Disconnected from server";
            currentMode = UIMode.MainMenu;
            MultiplayerMod.Log.LogInfo("[UI] Disconnected from server event received");
        }
        
        private void OnClientConnected(int clientId)
        {
            statusMessage = $"Player {clientId} connected";
            MultiplayerMod.Log.LogInfo($"[UI] Client {clientId} connected event received");
        }
        
        private void OnClientDisconnected(int clientId)
        {
            statusMessage = $"Player {clientId} disconnected";
            MultiplayerMod.Log.LogInfo($"[UI] Client {clientId} disconnected event received");
        }
        
        private void OnConnectionFailed(string reason)
        {
            statusMessage = $"Connection failed: {reason}";
            currentMode = UIMode.JoinSettings; // Go back to join settings on failure
            MultiplayerMod.Log.LogError($"[UI] Connection failed: {reason}");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                showUI = !showUI;
                MultiplayerMod.Log.LogInfo($"F8 pressed! UI is now: {(showUI ? "VISIBLE" : "HIDDEN")}");
            }
        }

        void OnGUI()
        {
            if (!showUI) return;
            
            GUI.skin.window.fontSize = 14;
            GUI.skin.button.fontSize = 12;
            GUI.skin.label.fontSize = 12;
            
            windowRect = GUI.Window(12345, windowRect, DrawWindow, "Shapez 2 Multiplayer");
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();
            
            switch (currentMode)
            {
                case UIMode.MainMenu:
                    DrawMainMenu();
                    break;
                    
                case UIMode.HostSettings:
                    DrawHostSettings();
                    break;
                    
                case UIMode.JoinSettings:
                    DrawJoinSettings();
                    break;
                    
                case UIMode.InGame:
                    DrawInGame();
                    break;
            }
            
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(10);
                GUILayout.Label(statusMessage);
            }
            
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawMainMenu()
        {
            GUILayout.Label("Welcome to Shapez 2 Multiplayer!", GUI.skin.box);
            GUILayout.Space(10);
            
            GUILayout.Label("Player Name:");
            playerName = GUILayout.TextField(playerName, 25); // Limit name length
            GUILayout.Space(20);
            
            if (GUILayout.Button("Host Game", GUILayout.Height(40)))
            {
                currentMode = UIMode.HostSettings;
                statusMessage = "";
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Join Game", GUILayout.Height(40)))
            {
                currentMode = UIMode.JoinSettings;
                statusMessage = "";
            }
            
            GUILayout.Space(20);
            GUILayout.Label("Press F8 to toggle this menu", GUI.skin.box);
        }

        private void DrawHostSettings()
        {
            GUILayout.Label("Host Multiplayer Game", GUI.skin.box);
            GUILayout.Space(10);
            
            GUILayout.Label("Server Port:");
            serverPort = GUILayout.TextField(serverPort);
            GUILayout.Space(10);
            
            GUILayout.Label("Other players can connect to your IP address on this port.");
            GUILayout.Label($"Your local IP: {GetLocalIPAddress()}");
            GUILayout.Space(20);
            
            if (GUILayout.Button("Start Server", GUILayout.Height(40)))
            {
                StartHost();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Back", GUILayout.Height(30)))
            {
                currentMode = UIMode.MainMenu;
                statusMessage = "";
            }
        }

        private void DrawJoinSettings()
        {
            GUILayout.Label("Join Multiplayer Game", GUI.skin.box);
            GUILayout.Space(10);
            
            GUILayout.Label("Server Address:");
            serverAddress = GUILayout.TextField(serverAddress);
            GUILayout.Space(10);
            
            GUILayout.Label("Server Port:");
            serverPort = GUILayout.TextField(serverPort);
            GUILayout.Space(20);
            
            if (GUILayout.Button("Connect", GUILayout.Height(40)))
            {
                JoinServer();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Back", GUILayout.Height(30)))
            {
                currentMode = UIMode.MainMenu;
                statusMessage = "";
            }
        }

        private void DrawInGame()
        {
            GUILayout.Label("Multiplayer Session", GUI.skin.box);
            GUILayout.Space(10);
            
            string status = networkManager.GetStatusString();
            GUILayout.Label($"Status: {status}");
            GUILayout.Space(10);
            
            if (networkManager.IsServer)
            {
                GUILayout.Label($"Your IP: {GetLocalIPAddress()}");
                GUILayout.Label($"Port: {serverPort}");
            }
            else if (networkManager.IsClient)
            {
                GUILayout.Label($"Server: {serverAddress}:{serverPort}");
            }
            
            GUILayout.Space(10);
            
            GUILayout.Label("Connected Players:", GUI.skin.box);
            
            var playerManager = MultiplayerMod.Instance?.PlayerManager;
            if (playerManager != null)
            {
                foreach (var player in playerManager.GetAllPlayers())
                {
                    string hostTag = player.IsHost ? " [HOST]" : "";
                    string youTag = player.PlayerId == playerManager.LocalPlayerId ? " (You)" : "";
                    GUILayout.Label($"• {player.PlayerName}{hostTag}{youTag}");
                }
            }
            
            GUILayout.Space(20);
            
            if (GUILayout.Button("Disconnect", GUILayout.Height(40)))
            {
                Disconnect();
            }
        }

        private void StartHost()
        {
            if (!int.TryParse(serverPort, out int port))
            {
                statusMessage = "Invalid port number!";
                return;
            }
            
            if (networkManager.StartServer(port))
            {
                statusMessage = "Server started successfully!";
                currentMode = UIMode.InGame;
                
                var playerManager = MultiplayerMod.Instance?.PlayerManager;
                if (playerManager != null)
                {
                    // --- THIS IS THE FIX ---
                    // Set the host's ID to 0, which is the reserved ID for the server.
                    playerManager.LocalPlayerId = 0; 
                    playerManager.LocalPlayerName = playerName;
                    
                    // The host adds ITSELF to the manager. The server logic will handle clients.
                    playerManager.AddPlayer(new PlayerInfo
                    {
                        PlayerId = 0,
                        PlayerName = playerName,
                        IsHost = true
                    });
                }
            }
            else
            {
                statusMessage = "Failed to start server!";
            }
        }

        private void JoinServer()
        {
            if (!int.TryParse(serverPort, out int port))
            {
                statusMessage = "Invalid port number!";
                return;
            }

            statusMessage = "Connecting...";
            
            // THE FIX:
            // We now pass the 'playerName' from the UI directly into the ConnectToServer method.
            // The if/else block is removed because the connection status (success/fail)
            // will be handled by the OnConnectedToServer and OnConnectionFailed event handlers automatically.
            networkManager.ConnectToServer(serverAddress, port, playerName);
        }

        private void Disconnect()
        {
            networkManager.Shutdown();
            MultiplayerMod.Instance?.PlayerManager?.Clear();
            currentMode = UIMode.MainMenu;
            statusMessage = "Disconnected";
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "Not found";
            }
            catch { return "Error"; }
        }
    }
}