using HarmonyLib;
using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod.Networking;
using System.Collections.Generic;
using System.Linq;
//using Game.Rendering;

namespace Shapez2MultiplayerMod.Patches
{
    [HarmonyPatch(typeof(PlacementBuildingsDrawer))]
    public static class GhostPlacementHook
    {
        public static bool WasActiveThisFrame = false;
        public static FrameDrawOptions LastFrameDrawOptions = null;
        private static PlayerGhostPlacementPacket lastSentPacket = null;
        
        [HarmonyPatch("Draw")]
        [HarmonyPostfix]
        public static void Postfix(PlacementData placementData, FrameDrawOptions drawOptions) 
        {
            WasActiveThisFrame = true;
            LastFrameDrawOptions = drawOptions;

            var networkManager = MultiplayerMod.Instance.NetworkManager;
            if (networkManager == null || !networkManager.IsConnected) return;

            // ... rest of the logic is correct and unchanged ...
            var ghostsToDraw = placementData.Buildings;
            var packet = new PlayerGhostPlacementPacket
            {
                PlayerId = MultiplayerMod.Instance.PlayerManager.LocalPlayerId,
                GhostBuildings = new List<GhostBuildingData>()
            };

            if (ghostsToDraw == null || !ghostsToDraw.Any())
            {
                if (lastSentPacket != null && lastSentPacket.GhostBuildings.Count > 0)
                {
                    lastSentPacket = packet;
                    if (networkManager.IsClient) networkManager.SendToServer(lastSentPacket);
                    if (networkManager.IsServer) networkManager.BroadcastToClients(lastSentPacket);
                }
                return;
            }

            foreach (var ghost in ghostsToDraw)
            {
                var transform = ghost.BuildingDescriptor.Transform_G;
                var definition = ghost.BuildingDescriptor.Definition;
                bool isValid = ghost.PlacementAllowability.WillBePlaced();
                packet.GhostBuildings.Add(new GhostBuildingData
                {
                    BuildingType = definition.Id.ToString(),
                    X = transform.Position.x,
                    Y = transform.Position.y,
                    Z = transform.Position.z,
                    Rotation = (int)transform.Rotation.Value,
                    IsValid = isValid
                });
            }

            if (!AreEqual(lastSentPacket, packet))
            {
                lastSentPacket = packet;
                if (networkManager.IsClient) networkManager.SendToServer(packet);
                else if (networkManager.IsServer) networkManager.BroadcastToClients(packet);
            }
        }

        private static bool AreEqual(PlayerGhostPlacementPacket a, PlayerGhostPlacementPacket b)
        {
            if (a == null || b == null) return a == b;
            if (a.GhostBuildings.Count != b.GhostBuildings.Count) return false;
            for (int i = 0; i < a.GhostBuildings.Count; i++)
            {
                var itemA = a.GhostBuildings[i];
                var itemB = b.GhostBuildings[i];
                if (itemA.X != itemB.X || itemA.Y != itemB.Y || itemA.Z != itemB.Z ||
                    itemA.Rotation != itemB.Rotation || itemA.IsValid != itemB.IsValid ||
                    itemA.BuildingType != itemB.BuildingType)
                {
                    return false;
                }
            }
            return true;
        }
    }
}