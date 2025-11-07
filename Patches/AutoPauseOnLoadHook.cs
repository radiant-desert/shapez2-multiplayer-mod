extern alias Orchestration;

using HarmonyLib;
using Shapez2MultiplayerMod;
using Global.Core; 
using Game.Core;
using System.Reflection; // <-- ADD THIS for reflection

namespace SHAPEZ2MULTIPLAYERMOD.Patches
{
    [HarmonyPatch(typeof(Orchestration.GameCore), "Start")] 
    public class AutoPauseOnLoadHook
    {
        [HarmonyPostfix]
        public static void Postfix(Orchestration.GameCore __instance)
        {
            if (__instance == null || __instance.Mode == null)
            {
                return;
            }

            // --- USE REFLECTION TO GET THE PRIVATE FIELD ---
            // 1. Get the FieldInfo for the private 'SavegameOptionsManager' field.
            FieldInfo somField = typeof(Orchestration.GameCore).GetField("SavegameOptionsManager", BindingFlags.NonPublic | BindingFlags.Instance);
            if (somField == null)
            {
                MultiplayerMod.Log.LogError("AutoPauseOnLoadHook: Could not find private field 'SavegameOptionsManager' via reflection.");
                return;
            }

            // 2. Get the actual manager object from the GameCore instance.
            var savegameOptionsManager = somField.GetValue(__instance) as SavegameOptionsManager;
            if (savegameOptionsManager == null)
            {
                MultiplayerMod.Log.LogError("AutoPauseOnLoadHook: 'SavegameOptionsManager' field value is null.");
                return;
            }
            // ---------------------------------------------


            // This is our definitive filter, now using the reflected value.
            if (!savegameOptionsManager.Options.MenuMode)
            {
                // Action 1: Pause the game.
                if (__instance.SimulationSpeed != null && !__instance.SimulationSpeed.IsPaused)
                {
                    __instance.SimulationSpeed.IsPaused = true;
                    MultiplayerMod.Log.LogInfo("AutoPauseOnLoadHook: Playable game instance detected. Pausing game.");
                }
                
                // Action 2: Initialize our late-game managers.
                MultiplayerMod.Instance?.InitializeLateGameManagers();
            }
        }
    }
}