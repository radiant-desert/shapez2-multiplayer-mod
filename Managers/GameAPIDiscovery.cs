using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Shapez2MultiplayerMod.Managers
{
    /// <summary>
    /// Discovers and logs Shapez 2's internal APIs for world loading and building management
    /// </summary>
    public class GameAPIDiscovery
    {
        private static bool hasRunDiscovery = false;
        
        /// <summary>
        /// Run comprehensive API discovery
        /// </summary>
        public static void DiscoverAPIs()
        {
            if (hasRunDiscovery) return;
            hasRunDiscovery = true;
            
            //MultiplayerMod.Log.LogInfo("=".PadRight(80, '='));
            //MultiplayerMod.Log.LogInfo("GAME API DISCOVERY - Starting comprehensive scan...");
            //MultiplayerMod.Log.LogInfo("=".PadRight(80, '='));
            
            DiscoverSaveGameAPIs();
            DiscoverBuildingAPIs();
            DiscoverGameStateAPIs();
            DiscoverSceneManagementAPIs();
            
            //MultiplayerMod.Log.LogInfo("=".PadRight(80, '='));
            //MultiplayerMod.Log.LogInfo("GAME API DISCOVERY - Scan complete!");
            //MultiplayerMod.Log.LogInfo("=".PadRight(80, '='));
        }
        
        /// <summary>
        /// Discover save game and world loading APIs
        /// </summary>
        private static void DiscoverSaveGameAPIs()
        {
            //MultiplayerMod.Log.LogInfo("\n### SAVE GAME APIs ###");
            
            string[] saveKeywords = new[] { "Savegame", "SaveFile", "WorldLoader", "LoadGame", "SaveManager" };
            
            foreach (string keyword in saveKeywords)
            {
                var types = FindTypesByKeyword(keyword);
                foreach (var type in types)
                {
                    LogTypeDetails(type, "SAVE");
                }
            }
        }
        
        /// <summary>
        /// Discover building placement and management APIs
        /// </summary>
        private static void DiscoverBuildingAPIs()
        {
            //MultiplayerMod.Log.LogInfo("\n### BUILDING APIs ###");
            
            string[] buildingKeywords = new[] { "Building", "Placement", "Construction", "Destroy", "Demolish", "PlaceBuilding" };
            
            foreach (string keyword in buildingKeywords)
            {
                var types = FindTypesByKeyword(keyword);
                foreach (var type in types)
                {
                    LogTypeDetails(type, "BUILDING");
                }
            }
        }
        
        /// <summary>
        /// Discover game state and core management APIs
        /// </summary>
        private static void DiscoverGameStateAPIs()
        {
            //MultiplayerMod.Log.LogInfo("\n### GAME STATE APIs ###");
            
            string[] stateKeywords = new[] { "GameCore", "GameState", "GameManager", "GameController", "GameData" };
            
            foreach (string keyword in stateKeywords)
            {
                var types = FindTypesByKeyword(keyword);
                foreach (var type in types)
                {
                    LogTypeDetails(type, "STATE");
                }
            }
        }
        
        /// <summary>
        /// Discover scene management and loading APIs
        /// </summary>
        private static void DiscoverSceneManagementAPIs()
        {
            //MultiplayerMod.Log.LogInfo("\n### SCENE MANAGEMENT APIs ###");
            
            string[] sceneKeywords = new[] { "SceneLoader", "SceneManager", "LoadScene", "GameLoading" };
            
            foreach (string keyword in sceneKeywords)
            {
                var types = FindTypesByKeyword(keyword);
                foreach (var type in types)
                {
                    LogTypeDetails(type, "SCENE");
                }
            }
        }
        
        /// <summary>
        /// Find all types containing a keyword
        /// </summary>
        private static List<Type> FindTypesByKeyword(string keyword)
        {
            List<Type> results = new List<Type>();
            
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // Skip system assemblies
                        string assemblyName = assembly.GetName().Name;
                        if (assemblyName.StartsWith("System") || 
                            assemblyName.StartsWith("Unity") ||
                            assemblyName.StartsWith("mscorlib") ||
                            assemblyName.StartsWith("netstandard"))
                            continue;
                        
                        var types = assembly.GetTypes()
                            .Where(t => t.Name.Contains(keyword) && 
                                       !t.Name.Contains("Shapez2MultiplayerMod"))
                            .Take(5); // Limit to prevent spam
                        
                        results.AddRange(types);
                    }
                    catch { }
                }
            }
            catch { }
            
            return results;
        }
        
        /// <summary>
        /// Log detailed information about a type
        /// </summary>
        private static void LogTypeDetails(Type type, string category)
        {
            try
            {
                //MultiplayerMod.Log.LogInfo($"\n[{category}] Type: {type.FullName}");
                
                // Log interesting static methods
                var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                    .Take(10);
                
                if (staticMethods.Any())
                {
                    //MultiplayerMod.Log.LogInfo("  Static Methods:");
                    foreach (var method in staticMethods)
                    {
                        string parameters = string.Join(", ", method.GetParameters()
                            .Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        //MultiplayerMod.Log.LogInfo($"    {method.ReturnType.Name} {method.Name}({parameters})");
                    }
                }
                
                // Log interesting instance methods
                var instanceMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.Name.StartsWith("get_") && 
                               !m.Name.StartsWith("set_") &&
                               !m.Name.StartsWith("add_") &&
                               !m.Name.StartsWith("remove_") &&
                               m.DeclaringType == type)
                    .Take(10);
                
                if (instanceMethods.Any())
                {
                    //MultiplayerMod.Log.LogInfo("  Instance Methods:");
                    foreach (var method in instanceMethods)
                    {
                        string parameters = string.Join(", ", method.GetParameters()
                            .Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        //MultiplayerMod.Log.LogInfo($"    {method.ReturnType.Name} {method.Name}({parameters})");
                    }
                }
                
                // Log static properties
                var staticProps = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                    .Take(5);
                
                if (staticProps.Any())
                {
                    //MultiplayerMod.Log.LogInfo("  Static Properties:");
                    foreach (var prop in staticProps)
                    {
                        //MultiplayerMod.Log.LogInfo($"    {prop.PropertyType.Name} {prop.Name}");
                    }
                }
                
                // Try to find instance
                TryFindInstance(type);
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogWarning($"  Error logging type details: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Try to find an instance of the type
        /// </summary>
        private static void TryFindInstance(Type type)
        {
            try
            {
                // Try FindObjectOfType for Unity objects
                if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[] { })
                        .MakeGenericMethod(type);
                    var instance = findMethod.Invoke(null, null);
                    
                    if (instance != null)
                    {
                        //MultiplayerMod.Log.LogInfo($"  ✓ Found instance via FindObjectOfType!");
                    }
                }
                
                // Try static Instance property
                var instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null)
                {
                    var instance = instanceProp.GetValue(null);
                    if (instance != null)
                    {
                        //MultiplayerMod.Log.LogInfo($"  ✓ Found instance via Instance property!");
                    }
                }
            }
            catch { }
        }
    }
}

