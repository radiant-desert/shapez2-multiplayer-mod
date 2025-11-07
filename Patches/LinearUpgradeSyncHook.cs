using Game.Core.Research;
using HarmonyLib;
using Shapez2MultiplayerMod;
using Shapez2MultiplayerMod.Models;

[HarmonyPatch(typeof(ResearchLinearUpgradeManager))]
public static class LinearUpgradeSyncHook
{
    [HarmonyPatch("TryLevelUp")]
    [HarmonyPrefix]
    public static bool TryLevelUp_Prefix(ResearchLinearUpgradeId upgrade, ref bool __result)
    {
        if (MultiplayerMod.Instance?.NetworkManager == null) return true;

        if (MultiplayerMod.Instance.NetworkManager.IsClient)
        {
            MultiplayerMod.Log.LogInfo($"[LINEAR-HOOK-PREFIX] Client intercepted shop upgrade for '{upgrade.Id}'. Sending to server.");
            var packet = new RequestLevelUpUpgradePacket() { UpgradeId = upgrade.Id };
            MultiplayerMod.Instance.NetworkManager.SendToServer(packet);
            __result = false;
            return false;
        }
        
        return true;
    }

    [HarmonyPatch("TryLevelUp")]
    [HarmonyPostfix]
    public static void TryLevelUp_Postfix(ResearchLinearUpgradeManager __instance, ResearchLinearUpgradeId upgrade, bool __result)
    {
        if (MultiplayerMod.Instance?.NetworkManager == null) return;

        if (MultiplayerMod.Instance.NetworkManager.IsServer)
        {
            if (__result) // If the purchase was successful on the server
            {
                // --- Broadcast the Level Up Confirmation ---
                int newLevel = __instance.GetCurrentLevel(upgrade).Level;
                MultiplayerMod.Log.LogInfo($"[LINEAR-HOOK-POSTFIX] Server leveled up '{upgrade.Id}' to {newLevel}. Broadcasting...");
                var levelUpPacket = new ConfirmLevelUpUpgradePacket() { UpgradeId = upgrade.Id, NewLevel = newLevel };
                MultiplayerMod.Instance.NetworkManager.BroadcastToClients(levelUpPacket);

                // --- NEW: Broadcast the Currency Update ---
                var gameCore = Singleton<StaticGameCoreAccessor>.G;
                if (gameCore != null)
                {
                    int newPointTotal = gameCore.Research.PointStorage.Points.Amount;
                    MultiplayerMod.Log.LogInfo($"[LINEAR-HOOK-POSTFIX] Server currency is now {newPointTotal}. Broadcasting currency update...");
                    var currencyPacket = new UpdateCurrencyPacket() { NewResearchPoints = newPointTotal };
                    MultiplayerMod.Instance.NetworkManager.BroadcastToClients(currencyPacket);
                }
            }
        }
    }
}