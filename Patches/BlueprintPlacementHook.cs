/*extern alias GameMapSim;

using HarmonyLib;
using Shapez2MultiplayerMod.Models;
using System.Collections.Generic;
using Game.Core.Coordinates;
using Game.Core.Coordinates.Extensions;
using UnityEngine; // Required for Time.time

namespace Shapez2MultiplayerMod.Patches
{
    [HarmonyPatch(typeof(SmartBuildingBlueprintRenderer), "Draw")]
    public static class BlueprintPlacementHook
    {
        public static bool WasActiveThisFrame = false;
        
        // --- Throttling variables ---
        private static float lastSendTime = 0f;
        private const float SendInterval = 0.025f; // Send updates 10 times per second

        [HarmonyPostfix]
        public static void Postfix(FrameDrawOptions options, IReadOnlyList<SmartBuildingBlueprintRenderer.DrawData> drawData)
        {
            WasActiveThisFrame = true;
            GhostPlacementHook.LastFrameDrawOptions = options;

            var networkManager = MultiplayerMod.Instance.NetworkManager;
            if (networkManager == null || !networkManager.IsConnected) return;

            // --- Throttling Logic ---
            // Only send an update if enough time has passed since the last one.
            if (Time.time - lastSendTime < SendInterval)
            {
                return;
            }
            lastSendTime = Time.time;

            // --- Packet Creation (Largely the same) ---
            var buildingGhostPacket = new PlayerGhostPlacementPacket
            {
                PlayerId = MultiplayerMod.Instance.PlayerManager.LocalPlayerId
            };
            
            var islandGhostPacket = new PlayerIslandGhostPlacementPacket
            {
                PlayerId = MultiplayerMod.Instance.PlayerManager.LocalPlayerId
            };

            foreach (var data in drawData)
            {
                // This logic is optimized to only process what's needed.
                if (data.Definition is GameMapSim::IBuildingDefinition buildingDef)
                {
                    buildingGhostPacket.GhostBuildings.Add(new GhostBuildingData
                    {
                        BuildingType = buildingDef.Id.ToString(),
                        X = data.Transform_G.Position.x,
                        Y = data.Transform_G.Position.y,
                        Z = data.Transform_G.Position.z,
                        Rotation = (int)data.Transform_G.Rotation.Value,
                        IsValid = data.Allowability.WillBePlaced()
                    });
                }
                else if (data.Definition is GameMapSim::IIslandDefinition islandDef)
                {
                    GlobalChunkCoordinate chunkPos = data.Transform_G.Position.To_GC();
                    islandGhostPacket.IslandGhosts.Add(new GhostIslandData
                    {
                        IslandType = islandDef.Id.ToString(),
                        X = chunkPos.x,
                        Y = chunkPos.y,
                        Z = chunkPos.z,
                        Rotation = (int)data.Transform_G.Rotation.Value,
                        IsValid = data.Allowability.WillBePlaced()
                    });
                }
            }
            
            // --- Packet Sending (Unchanged) ---
            if (buildingGhostPacket.GhostBuildings.Count > 0)
            {
                if (networkManager.IsClient) networkManager.SendToServer(buildingGhostPacket);
                else if (networkManager.IsServer) networkManager.BroadcastToClients(buildingGhostPacket);
            }
            if (islandGhostPacket.IslandGhosts.Count > 0)
            {
                if (networkManager.IsClient) networkManager.SendToServer(islandGhostPacket);
                else if (networkManager.IsServer) networkManager.BroadcastToClients(islandGhostPacket);
            }
        }
    }
}*/