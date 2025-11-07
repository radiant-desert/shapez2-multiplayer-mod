using HarmonyLib;
using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod;

[HarmonyPatch(typeof(HUDPauseMenu))]
public static class PauseMenuHook
{
    // This PREFIX runs BEFORE the Hide() method
    [HarmonyPatch("Hide")]
    [HarmonyPrefix]
    public static void OnMenuClose_Prefix()
    {
        var networkManager = MultiplayerMod.Instance.NetworkManager;

        // If a CLIENT closes the menu, they send a request to the server.
        // The original Hide() method is allowed to run to close the UI visually.
        if (networkManager != null && networkManager.IsClient)
        {
            MultiplayerMod.Log.LogInfo("[PauseMenuHook PRE] Client closing menu, requesting unpause.");
            networkManager.SendToServer(new RequestTogglePausePacket());
        }
    }

    // This POSTFIX runs AFTER the Hide() method has finished
    [HarmonyPatch("Hide")]
    [HarmonyPostfix]
    public static void OnMenuClose_Postfix()
    {
        var networkManager = MultiplayerMod.Instance.NetworkManager;

        // If the SERVER (host) has just closed the menu, the game is now unpaused locally.
        // We need to broadcast this new authoritative state to all clients.
        if (networkManager != null && networkManager.IsServer)
        {
            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore != null)
            {
                bool isNowPaused = gameCore.SimulationSpeed.IsPaused;
                MultiplayerMod.Log.LogInfo($"[PauseMenuHook POST] Host closed menu. New pause state is '{isNowPaused}'. Broadcasting.");
                
                var packet = new ConfirmPauseStatePacket { IsPaused = isNowPaused };
                networkManager.BroadcastToClients(packet);
            }
        }
    }
}