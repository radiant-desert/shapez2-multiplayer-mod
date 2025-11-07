using HarmonyLib;
using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod.Networking;
using System.Collections.Generic;
using System.Linq;
using Game.Core; 
using Game.Placement.Utils;
using System.Reflection; // <-- NEW: Required for Reflection

namespace Shapez2MultiplayerMod.Patches
{
    [HarmonyPatch(typeof(PlacementIslandsDrawer))]
    public static class IslandPlacementHook
    {
        public static bool WasActiveThisFrame = false;
        public static IIslandPreviewDrawer IslandDrawerInstance = null;
        public static IMapLayout LastMapLayout = null;
        private static PlayerIslandGhostPlacementPacket lastSentPacket = null;
        
        [HarmonyPatch("Draw")]
        [HarmonyPostfix]
        public static void Postfix(PlacementIslandsDrawer __instance, PlacementData placementData, IMapLayout mapLayout) 
        {
            WasActiveThisFrame = true;
            LastMapLayout = mapLayout; 

            // --- THE FIX: Use Reflection to access the private field ---
            if (IslandDrawerInstance == null) // Only get it once to be efficient
            {
                // 1. Get the information about the private field
                FieldInfo drawerField = typeof(PlacementIslandsDrawer).GetField("IslandPreviewDrawer", BindingFlags.NonPublic | BindingFlags.Instance);
                
                // 2. Use that info to get the actual drawer object from the instance
                IslandDrawerInstance = drawerField.GetValue(__instance) as IIslandPreviewDrawer;
            }

            var networkManager = MultiplayerMod.Instance.NetworkManager;
            if (networkManager == null || !networkManager.IsConnected) return;

            // The rest of the packet sending logic is correct and unchanged.
            var ghostsToDraw = placementData.Islands;
            var packet = new PlayerIslandGhostPlacementPacket
            {
                PlayerId = MultiplayerMod.Instance.PlayerManager.LocalPlayerId,
                IslandGhosts = new List<GhostIslandData>()
            };

            if (ghostsToDraw == null || !ghostsToDraw.Any())
            {
                if (lastSentPacket != null && lastSentPacket.IslandGhosts.Count > 0)
                {
                    lastSentPacket = packet;
                    if (networkManager.IsClient) networkManager.SendToServer(lastSentPacket);
                    if (networkManager.IsServer) networkManager.BroadcastToClients(lastSentPacket);
                }
                return;
            }

            foreach (var ghost in ghostsToDraw)
            {
                var transform = ghost.IslandDescriptor.Transform;
                var definition = ghost.IslandDescriptor.Definition;
                bool isValid = ghost.PlacementAllowability.WillBePlaced();
                packet.IslandGhosts.Add(new GhostIslandData
                {
                    IslandType = definition.Id.ToString(),
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

        private static bool AreEqual(PlayerIslandGhostPlacementPacket a, PlayerIslandGhostPlacementPacket b)
        {
            if (a == null || b == null) return a == b;
            if (a.IslandGhosts.Count != b.IslandGhosts.Count) return false;
            for (int i = 0; i < a.IslandGhosts.Count; i++)
            {
                var itemA = a.IslandGhosts[i]; var itemB = b.IslandGhosts[i];
                if (itemA.X != itemB.X || itemA.Y != itemB.Y || itemA.Z != itemB.Z ||
                    itemA.Rotation != itemB.Rotation || itemA.IsValid != itemB.IsValid ||
                    itemA.IslandType != itemB.IslandType)
                    return false;
            }
            return true;
        }
    }
}