using HarmonyLib;
using Shapez2MultiplayerMod;
using Shapez2MultiplayerMod.Models;
// These are still needed for the method signature
//using Game.Research;

// DO NOT wrap this class in a namespace.
// This ensures Harmony can find the global `ResearchUnlockManager` class.
[HarmonyPatch(typeof(ResearchUnlockManager))]
public static class ProgressionSyncHook
{
    [HarmonyPatch("TryUnlock")]
    [HarmonyPrefix]
    public static bool TryUnlock_Prefix(IResearchUpgrade upgrade, bool forced, ref bool __result)
    {
        if (MultiplayerMod.Instance?.NetworkManager == null) return true;

        if (MultiplayerMod.Instance.NetworkManager.IsClient && !forced)
        {
            MultiplayerMod.Log.LogInfo($"[HOOK-PREFIX] Client intercepted unlock request for '{upgrade.Id.Id}'.");
            MultiplayerMod.Log.LogInfo($"[HOOK-PREFIX] Cancelling original method and sending packet to server...");
            
            var packet = new RequestUnlockUpgradePacket() { UpgradeId = upgrade.Id.Id };
            MultiplayerMod.Instance.NetworkManager.SendToServer(packet);

            __result = false;
            return false; // Skip original method on client
        }
        
        return true;
    }

    [HarmonyPatch("TryUnlock")]
    [HarmonyPostfix]
    public static void TryUnlock_Postfix(IResearchUpgrade upgrade, bool __result)
    {
        if (MultiplayerMod.Instance?.NetworkManager == null) return;

        if (MultiplayerMod.Instance.NetworkManager.IsServer)
        {
            if (__result)
            {
                MultiplayerMod.Log.LogInfo($"[HOOK-POSTFIX] Server successfully unlocked '{upgrade.Id.Id}'.");
                MultiplayerMod.Log.LogInfo($"[HOOK-POSTFIX] Broadcasting 'ConfirmUnlockUpgradePacket' to all clients...");
                
                var packet = new ConfirmUnlockUpgradePacket() { UpgradeId = upgrade.Id.Id };
                MultiplayerMod.Instance.NetworkManager.BroadcastToClients(packet);
            }
            else
            {
                MultiplayerMod.Log.LogInfo($"[HOOK-POSTFIX] Server tried to unlock '{upgrade.Id.Id}' but it failed (e.g., not enough resources). No broadcast sent.");
            }
        }
    }
}