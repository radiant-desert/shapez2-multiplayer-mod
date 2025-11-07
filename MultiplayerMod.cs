extern alias GameMapModel;

using HarmonyLib;
using BepInEx;
using BepInEx.Logging;
using Shapez2MultiplayerMod.Networking;
using Shapez2MultiplayerMod.Managers;
using Shapez2MultiplayerMod.UI;
using Shapez2MultiplayerMod.Patches;
using UnityEngine;
using System.Collections;
using Shapez2MultiplayerMod.Models;
using System.Reflection;
using System;
using System.Linq;
using Game.Core;
using Game.Core.Research;
using Global.Core;
using System.Collections.Generic;
//using Game.Rendering; // Added for SmartBuildingBlueprintRenderer
using Game.Placement.Utils;

namespace Shapez2MultiplayerMod
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class MultiplayerMod : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.shapez2.multiplayer";
        public const string PLUGIN_NAME = "Shapez2 Multiplayer";
        public const string PLUGIN_VERSION = "0.1.0";
        public const int DEFAULT_PORT = 7777;

        public static MultiplayerMod Instance { get; private set; }
        public ClientActionManager ClientActionManager { get; private set; }
        public static ManualLogSource Log { get; private set; }

        public NetworkManager NetworkManager { get; private set; }
        public PlayerManager PlayerManager { get; private set; }
        public WorldStateManager WorldStateManager { get; private set; }
        public BuildingSyncManager BuildingSyncManager { get; private set; }
        public PlayerPositionSync PositionSync { get; private set; }
        public PlayerMarkerManager MarkerManager { get; private set; }
        public SavegameTransfer SavegameTransfer { get; private set; }
        public GhostPlacementManager GhostPlacementManager { get; private set; }
        public IslandPlacementManager IslandPlacementManager { get; private set; }
        
        private GameObject uiObject;
        private bool networkUpdateRunning = false;
        private bool lateGameManagersInitialized = false;

        private bool wasPlacingBuildingsLastFrame = false;
        private bool wasPlacingIslandsLastFrame = false;
        private bool wasPlacingLastFrame = false;
        private bool wasPlacingBlueprintLastFrame = false;

        void Awake()
        {
            try
            {
                Instance = this;
                Log = Logger;
                var harmony = new Harmony(PLUGIN_GUID);
                harmony.PatchAll();
                Log.LogInfo("Harmony patches applied successfully.");
                
                Log.LogInfo("========================================");
                Log.LogInfo("Initializing Shapez 2 Multiplayer Mod");
                
                NetworkManager = new NetworkManager();
                ClientActionManager = new ClientActionManager(NetworkManager);
                PlayerManager = new PlayerManager();
                WorldStateManager = new WorldStateManager(NetworkManager);
                BuildingSyncManager = new BuildingSyncManager(NetworkManager);
                PositionSync = new PlayerPositionSync(NetworkManager, PlayerManager);
                MarkerManager = new PlayerMarkerManager(PlayerManager);
                SavegameTransfer = new SavegameTransfer(NetworkManager);
                GhostPlacementManager = new GhostPlacementManager();
                IslandPlacementManager = new IslandPlacementManager();
                
                NetworkManager.OnServerStarted += () => PositionSync?.StartSyncing();
                NetworkManager.OnConnectedToServer += () => PositionSync?.StartSyncing();
                NetworkManager.OnClientConnected += (clientId) => {
                    if (NetworkManager.IsServer) { Log.LogInfo($"Client {clientId} connected."); }
                };
                
                PlayerManager.OnPlayerAdded += (player) => {
                    if (player.PlayerId != PlayerManager.LocalPlayerId) MarkerManager.CreateMarker(player);
                };
                PlayerManager.OnPlayerRemoved += (playerId) => MarkerManager.RemoveMarker(playerId.ToString());

                Log.LogInfo("Managers constructed. Initializing UI and Network Loop after delay.");
                StartCoroutine(InitializeAfterDelayCoroutine());
            }
            catch (System.Exception ex) { Log.LogError($"FATAL ERROR during initialization: {ex}"); }
        }
        
        private IEnumerator InitializeAfterDelayCoroutine()
        {
            yield return new WaitForSeconds(1.5f);
            
            try 
            {
                Log.LogInfo("Delayed initialization started.");
                
                uiObject = new GameObject("MultiplayerUI");
                DontDestroyOnLoad(uiObject);
                var ui = uiObject.AddComponent<MultiplayerUI>();
                ui.Initialize(NetworkManager);
                
                WorldStateManager.SetCoroutineHost(this);
                WorldStateManager.Initialize();
                BuildingSyncManager.Initialize();
                
                StartCoroutine(NetworkUpdateLoop());
                Log.LogInfo("Delayed initialization complete.");
            } 
            catch (System.Exception ex) { Log.LogError($"ERROR in delayed initialization: {ex}"); }
        }

        public void InitializeLateGameManagers()
        {
            if (lateGameManagersInitialized) return;
            Log.LogInfo("InitializeLateGameManagers: Initializing managers that require game assets...");
            lateGameManagersInitialized = true;
        }

        private void LateUpdate()
        {
            if (NetworkManager == null || !NetworkManager.IsConnected) return;

            // --- Part 1: World Modification Sending (Unchanged) ---
            if (WorldModificationHook.hasPendingModifications)
            {
                MultiplayerMod.Log.LogInfo($"[LateUpdate] Detected pending modifications. Sending packet.");
                if (NetworkManager.IsServer)
                {
                    NetworkManager.BroadcastToClients(WorldModificationHook.pendingModifications);
                }
                else if (NetworkManager.IsClient)
                {
                    NetworkManager.SendToServer(WorldModificationHook.pendingModifications);
                }
                WorldModificationHook.RecordAndReset();
            }
            
            // --- Part 2: Ghost Rendering (Unchanged) ---
            var buildingDrawOptions = GhostPlacementHook.LastFrameDrawOptions;
            
            // -- Buildings --
            if (buildingDrawOptions != null && GhostPlacementManager != null)
            {
                var buildingsToDraw = GhostPlacementManager.GetAllDrawData();
                if (buildingsToDraw.Count > 0)
                {
                    SmartBuildingBlueprintRenderer.Draw(buildingDrawOptions, buildingsToDraw);
                }
            }
            
            // -- Islands --
            var islandDrawer = IslandPlacementHook.IslandDrawerInstance;
            var mapLayout = IslandPlacementHook.LastMapLayout;
            if (islandDrawer != null && mapLayout != null && buildingDrawOptions != null && IslandPlacementManager != null)
            {
                var islandsToDraw = IslandPlacementManager.GetAllRenderData();
                if (islandsToDraw.Count > 0)
                {
                    islandDrawer.Draw(mapLayout.ToModel(), buildingDrawOptions, islandsToDraw, true, true, true);
                }
            }
            
            // --- Part 3: LINGERING GHOST FIX (Reverted to single placement only) ---

            // -- BLUEPRINT LOGIC DISABLED ---
            /*
            bool isPlacingBlueprintThisFrame = BlueprintPlacementHook.WasActiveThisFrame || 
                                            IslandBlueprintPlacementHook.WasActiveThisFrame;
            */

            // Check for exiting SINGLE BUILDING placement mode
            if (!GhostPlacementHook.WasActiveThisFrame && wasPlacingBuildingsLastFrame)
            {
                MultiplayerMod.Log.LogInfo("[LateUpdate] Player exited building placement mode. Sending clear packet.");
                var clearPacket = new PlayerGhostPlacementPacket { PlayerId = PlayerManager.LocalPlayerId };
                if (NetworkManager.IsClient) NetworkManager.SendToServer(clearPacket);
                else if (NetworkManager.IsServer) NetworkManager.BroadcastToClients(clearPacket);
            }

            // Check for exiting SINGLE ISLAND placement mode
            if (!IslandPlacementHook.WasActiveThisFrame && wasPlacingIslandsLastFrame)
            {
                MultiplayerMod.Log.LogInfo("[LateUpdate] Player exited island placement mode. Sending clear packet.");
                var clearPacket = new PlayerIslandGhostPlacementPacket { PlayerId = PlayerManager.LocalPlayerId };
                if (NetworkManager.IsClient) NetworkManager.SendToServer(clearPacket);
                else if (NetworkManager.IsServer) NetworkManager.BroadcastToClients(clearPacket);
            }
            
            // -- BLUEPRINT LOGIC DISABLED ---
            /*
            if (!isPlacingBlueprintThisFrame && wasPlacingBlueprintLastFrame)
            {
                MultiplayerMod.Log.LogInfo("[LateUpdate] Player exited blueprint placement mode. Sending clear packets.");
                var clearBuildings = new PlayerGhostPlacementPacket { PlayerId = PlayerManager.LocalPlayerId };
                if (NetworkManager.IsClient) NetworkManager.SendToServer(clearBuildings);
                else if (NetworkManager.IsServer) NetworkManager.BroadcastToClients(clearBuildings);

                var clearIslands = new PlayerIslandGhostPlacementPacket { PlayerId = PlayerManager.LocalPlayerId };
                if (NetworkManager.IsClient) NetworkManager.SendToServer(clearIslands);
                else if (NetworkManager.IsServer) networkManager.BroadcastToClients(clearIslands);
            }
            */

            // Update states for the next frame
            wasPlacingBuildingsLastFrame = GhostPlacementHook.WasActiveThisFrame;
            wasPlacingIslandsLastFrame = IslandPlacementHook.WasActiveThisFrame;
            // wasPlacingBlueprintLastFrame = isPlacingBlueprintThisFrame; // -- BLUEPRINT LOGIC DISABLED

            // Reset all flags for the next frame's check
            GhostPlacementHook.WasActiveThisFrame = false;
            IslandPlacementHook.WasActiveThisFrame = false;
            // BlueprintPlacementHook.WasActiveThisFrame = false;       // -- BLUEPRINT LOGIC DISABLED
            // IslandBlueprintPlacementHook.WasActiveThisFrame = false; // -- BLUEPRINT LOGIC DISABLED
        }
        
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) { DumpAllUpgradeIds(); }
            if (Input.GetKeyDown(KeyCode.F10)) { RunSoloUnlockTest(); }
            if (Input.GetKeyDown(KeyCode.F11)) { RunSoloLinearUpgradeTest(); }
        }

        public void ForceHudRefresh()
        {
            StartCoroutine(RefreshHudCoroutine());
        }
        
        private IEnumerator RefreshHudCoroutine()
        {
            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore == null) { yield break; }
            Transform rootTransform = null;
            try {
                var hudField = gameCore.GetType().GetField("HUD", BindingFlags.NonPublic | BindingFlags.Instance);
                if (hudField == null) { yield break; }
                var hudInstance = hudField.GetValue(gameCore);
                if (hudInstance == null) { yield break; }
                var rootField = hudInstance.GetType().GetField("Root", BindingFlags.NonPublic | BindingFlags.Instance);
                if (rootField == null) { yield break; }
                rootTransform = rootField.GetValue(hudInstance) as Transform;
            } catch {}
            if (rootTransform == null) { yield break; }
            rootTransform.gameObject.SetActive(false);
            yield return null;
            rootTransform.gameObject.SetActive(true);
        }
        
        private void DumpAllUpgradeIds()
        {
            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore?.Research == null) { return; }
            var allMilestones = gameCore.Research.Layout.AllUpgrades;
            if (allMilestones != null && allMilestones.Any()) { Log.LogInfo($"--- {allMilestones.Count} Milestones ---"); foreach (var u in allMilestones) { Log.LogInfo($"[Milestone] ID: \"{u.Id.ToString()}\""); } }
            var linearManager = gameCore.Research.LinearUpgradeManager;
            if (linearManager?.Levels != null && linearManager.Levels.Any()) { Log.LogInfo($"--- {linearManager.Levels.Count} Shop Upgrades ---"); foreach (var kvp in linearManager.Levels) { Log.LogInfo($"[Shop] ID: \"{kvp.Key.ToString()}\""); } }
        }
        
        private void RunSoloUnlockTest()
        {
            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore?.Research == null) { return; }
            string testUpgradeId = "RNBlueprints"; 
            var upgradeToTest = gameCore.Research.Layout.AllUpgrades.FirstOrDefault(u => u.Id.ToString() == testUpgradeId);
            if (upgradeToTest == null) { return; }
            gameCore.Research.UnlockManager.TryUnlock(upgradeToTest, forced: true);
        }

        private void RunSoloLinearUpgradeTest()
        {
            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore?.Research == null) { return; }
            string testUpgradeId = "LRUBeltSpeed";
            var linearManager = gameCore.Research.LinearUpgradeManager;
            var upgradeId = new ResearchLinearUpgradeId(testUpgradeId);
            if (!linearManager.Levels.ContainsKey(upgradeId)) { return; }
            linearManager.TryLevelUp(upgradeId);
        }

        private IEnumerator NetworkUpdateLoop()
        {
            networkUpdateRunning = true;
            while (networkUpdateRunning)
            {
                try { NetworkManager?.Update(); PositionSync?.Update(); MarkerManager?.Update(); }
                catch (System.Exception ex) { Log.LogError($"Error in network update loop: {ex.Message}"); }
                yield return null;
            }
        }

        void OnDestroy()
        {
            try {
                networkUpdateRunning = false;
                PositionSync?.Shutdown();
                MarkerManager?.Shutdown();
                WorldStateManager?.Shutdown();
                BuildingSyncManager?.Shutdown();
                NetworkManager?.Shutdown();
                GhostPlacementManager?.Shutdown();
                IslandPlacementManager?.Shutdown();
                if (uiObject != null) { Destroy(uiObject); }
                Instance = null;
            } catch (System.Exception ex) { Log.LogError($"Error during shutdown: {ex.Message}"); }
        }
    }
}