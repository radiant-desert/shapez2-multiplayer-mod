using HarmonyLib;

// No 'using' statement is needed because SystemButtonsModel is in the global namespace.

namespace Shapez2MultiplayerMod.Patches
{
    [HarmonyPatch]
    public class SystemButtonsHook
    {
        // This is the method the game calls when you press the Undo button or Ctrl+Z.
        [HarmonyPatch(typeof(SystemButtonsModel), "TryUndo")]
        [HarmonyPrefix]
        public static bool OnTryUndo()
        {
            var networkManager = MultiplayerMod.Instance.NetworkManager;
            if (networkManager != null && networkManager.IsClient)
            {
                // We are a client. Instead of running the game's logic,
                // we call our own custom undo manager.
                MultiplayerMod.Log.LogInfo("[SystemButtonsHook] Client pressed Undo. Performing custom undo action.");
                MultiplayerMod.Instance.ClientActionManager.PerformUndo();
                
                // Return FALSE to completely cancel the original TryUndo() method.
                return false;
            }
            
            // We are the server or in single-player. Let the original method run.
            return true;
        }

        // This is the method the game calls for the Redo action.
        [HarmonyPatch(typeof(SystemButtonsModel), "TryRedo")]
        [HarmonyPrefix]
        public static bool OnTryRedo()
        {
            var networkManager = MultiplayerMod.Instance.NetworkManager;
            if (networkManager != null && networkManager.IsClient)
            {
                // We are a client. Call our custom redo manager.
                MultiplayerMod.Log.LogInfo("[SystemButtonsHook] Client pressed Redo. Performing custom redo action.");
                MultiplayerMod.Instance.ClientActionManager.PerformRedo();
                
                // Return FALSE to cancel the original TryRedo() method.
                return false;
            }
            
            // We are the server or in single-player. Let the original method run.
            return true;
        }
    }
}