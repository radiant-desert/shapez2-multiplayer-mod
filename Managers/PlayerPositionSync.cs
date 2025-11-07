using UnityEngine;
using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod.Networking;

namespace Shapez2MultiplayerMod.Managers
{
    public class PlayerPositionSync
    {
        private readonly NetworkManager networkManager;
        private readonly PlayerManager playerManager;
        private readonly CameraPositionTracker cameraTracker;
        
        private const float POSITION_SEND_INTERVAL = 0.05f; // 20 updates per second
        private float lastSendTime = 0f;
        
        private bool isRunning = false;
        
        public PlayerPositionSync(NetworkManager netManager, PlayerManager plyrManager)
        {
            networkManager = netManager;
            playerManager = plyrManager;
            cameraTracker = new CameraPositionTracker();
            MultiplayerMod.Log.LogInfo("[PlayerPositionSync] Initialized");
        }
        
        public void StartSyncing()
        {
            if (isRunning) return;
            isRunning = true;
            MultiplayerMod.Log.LogInfo("[PlayerPositionSync] Started position syncing.");
        }
        
        public void StopSyncing()
        {
            isRunning = false;
            MultiplayerMod.Log.LogInfo("[PlayerPositionSync] Stopped position syncing.");
        }
        
        public void Update()
        {
            if (!isRunning || !networkManager.IsConnected) return;
            
            cameraTracker.Update();
            
            if (Time.time - lastSendTime >= POSITION_SEND_INTERVAL)
            {
                if (cameraTracker.HasMoved)
                {
                    SendPositionUpdate();
                    cameraTracker.UpdateLastPosition();
                }
                lastSendTime = Time.time;
            }
        }
        
        private void SendPositionUpdate()
        {
            if (playerManager.LocalPlayerId < 0)
            {
                // This is a key debug message. If you see this, the client's ID was never set.
                MultiplayerMod.Log.LogWarning("[PlayerPositionSync] Cannot send position, LocalPlayerId is not set!");
                return;
            }

            Vector3 currentPos = cameraTracker.CurrentPosition;
            var packet = new PlayerPositionPacket
            {
                PlayerId = playerManager.LocalPlayerId,
                X = currentPos.x, Y = currentPos.y, Z = currentPos.z
            };

            if (networkManager.IsClient)
            {
                //MultiplayerMod.Log.LogInfo($"[DEBUG] Client sending position: {currentPos}");
                networkManager.SendToServer(packet);
            }
            else if (networkManager.IsServer)
            {
                //MultiplayerMod.Log.LogInfo($"[DEBUG] Host broadcasting position: {currentPos}");
                networkManager.BroadcastToClients(packet);
            }
        }
        
        public void Shutdown()
        {
            StopSyncing();
            MultiplayerMod.Log.LogInfo("[PlayerPositionSync] Shutdown complete.");
        }
    }
}