/*extern alias GameMapSim;

using HarmonyLib;
using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod;
using Shapez2MultiplayerMod.Patches;
using System.Collections.Generic;
using Game.Core.Coordinates;
using UnityEngine; // For throttling
using System.Linq;

// We are patching the main drawing class for island previews
[HarmonyPatch(typeof(IslandsPreviewDrawer), "Draw")] 
public static class IslandBlueprintPlacementHook
{
    public static bool WasActiveThisFrame = false;
    
    // Throttling variables to prevent lag
    private static float lastSendTime = 0f;
    private const float sendInterval = 0.025f; // Send updates 10 times per second

    // The method signature MUST exactly match the one in IslandsPreviewDrawer
    [HarmonyPostfix]
    public static void Postfix(FrameDrawOptions options, IEnumerable<IslandPreviewRenderData> islands)
    {
        WasActiveThisFrame = true;
        // Also update the FrameDrawOptions for the LateUpdate renderer
        GhostPlacementHook.LastFrameDrawOptions = options;

        var networkManager = MultiplayerMod.Instance.NetworkManager;
        if (networkManager == null || !networkManager.IsConnected) return;

        // Immediately stop if there's nothing to draw
        // We use .Any() which is more efficient for IEnumerables than checking Count
        if (!islands.Any()) return; 

        // --- LAG FIX: Throttling ---
        if (Time.time - lastSendTime < sendInterval)
        {
            return;
        }
        lastSendTime = Time.time;
        
        var islandGhostPacket = new PlayerIslandGhostPlacementPacket
        {
            PlayerId = MultiplayerMod.Instance.PlayerManager.LocalPlayerId
        };

        foreach (var data in islands)
        {
            islandGhostPacket.IslandGhosts.Add(new GhostIslandData
            {
                IslandType = data.Definition.Id.ToString(),
                X = data.Transform.Position.x,
                Y = data.Transform.Position.y,
                Z = data.Transform.Position.z,
                Rotation = (int)data.Transform.Rotation.Value,
                IsValid = data.PlacementAllowability.WillBePlaced()
            });
        }

        if (islandGhostPacket.IslandGhosts.Count > 0)
        {
            if (networkManager.IsClient) networkManager.SendToServer(islandGhostPacket);
            else if (networkManager.IsServer) networkManager.BroadcastToClients(islandGhostPacket);
        }
    }
}*/