using System;
using System.Linq;
using System.Reflection;

namespace Shapez2MultiplayerMod.Managers
{
    /// <summary>
    /// Discovers building-related methods in the game code
    /// </summary>
    public static class BuildingMethodDiscovery
    {
        public static void DiscoverBuildingMethods()
        {
            MultiplayerMod.Log.LogInfo("[MethodDiscovery] Searching for building-related methods...");
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!assembly.FullName.Contains("GameCore") && !assembly.FullName.Contains("Assembly-CSharp"))
                        continue;
                        
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        // Look for types related to buildings
                        if (type.Name.Contains("Building") || type.Name.Contains("Island") || 
                            type.Name.Contains("Placement") || type.Name.Contains("Construction"))
                        {
                            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                            
                            foreach (var method in methods)
                            {
                                // Look for methods that might place or remove buildings
                                if (method.Name.Contains("Place") || method.Name.Contains("Remove") || 
                                    method.Name.Contains("Delete") || method.Name.Contains("Add") ||
                                    method.Name.Contains("Build") || method.Name.Contains("Destroy"))
                                {
                                    var parameters = method.GetParameters();
                                    var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                    
                                    MultiplayerMod.Log.LogInfo($"[MethodDiscovery] {type.Name}.{method.Name}({paramStr}) : {method.ReturnType.Name}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Skip assemblies that can't be loaded
                }
            }
            
            MultiplayerMod.Log.LogInfo("[MethodDiscovery] Method discovery complete");
        }
    }
}

