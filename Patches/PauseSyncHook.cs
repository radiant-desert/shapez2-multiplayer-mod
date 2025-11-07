using HarmonyLib;
using Shapez2MultiplayerMod;
using Shapez2MultiplayerMod.Models;

// The SimulationSpeedManager is in the global namespace
[HarmonyPatch(typeof(SimulationSpeedManager))]
public static class PauseSyncHook
{
    // This flag is crucial to prevent an infinite loop on the client
    public static bool IsApplyingFromServer = false;

    // Target the 'set' method of the 'IsPaused' property
    [HarmonyPatch(nameof(SimulationSpeedManager.IsPaused), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool SetIsPaused_Prefix(bool value) // 'value' is the boolean being set
    {
        var networkManager = MultiplayerMod.Instance.NetworkManager;
        
        // Allow the original method to run if:
        // - We are not in multiplayer
        // - We are the server
        // - We are a client applying a command FROM the server (IsApplyingFromServer is true)
        if (networkManager == null || !networkManager.IsConnected || networkManager.IsServer || IsApplyingFromServer)
        {
            return true;
        }

        // If we are a client and this is a user action, block it and send a request
        if (networkManager.IsClient)
        {
            MultiplayerMod.Log.LogInfo("Client intercepted pause/unpause request. Sending to server.");
            networkManager.SendToServer(new RequestTogglePausePacket());
            return false; // Cancel the original method
        }

        return true;
    }

    [HarmonyPatch(nameof(SimulationSpeedManager.IsPaused), MethodType.Setter)]
    [HarmonyPostfix]
    public static void SetIsPaused_Postfix(SimulationSpeedManager __instance)
    {
        var networkManager = MultiplayerMod.Instance.NetworkManager;
        
        // If we are the server, after the pause state has changed, broadcast the new state
        if (networkManager != null && networkManager.IsServer)
        {
            MultiplayerMod.Log.LogInfo($"Server pause state changed to: {__instance.IsPaused}. Broadcasting to clients.");
            var packet = new ConfirmPauseStatePacket { IsPaused = __instance.IsPaused };
            networkManager.BroadcastToClients(packet);
        }
    }
}