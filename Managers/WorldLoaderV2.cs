using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shapez2MultiplayerMod.Managers
{
    /// <summary>
    /// Version 2: Uses game's existing systems instead of recreating them
    /// </summary>
    public class WorldLoaderV2
    {
        /// <summary>
        /// Attempt to load world by finding and using game's existing systems
        /// </summary>
        public IEnumerator LoadWorld(string worldName, string saveData)
        {
            MultiplayerMod.Log.LogInfo("[WorldLoaderV2] ========================================");
            MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Attempting to load world: {worldName}");
            MultiplayerMod.Log.LogInfo("[WorldLoaderV2] ========================================");
            
            // Try multiple approaches
            bool success = false;
            
            // Approach 1: Find SavegameManager
            if (!success)
            {
                MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Approach 1: Searching for SavegameManager...");
                success = TryUseSavegameManager();
                yield return null;
            }
            
            // Approach 2: Find IGameData
            if (!success)
            {
                MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Approach 2: Searching for IGameData...");
                success = TryUseGameData();
                yield return null;
            }
            
            // Approach 3: Find GameCore
            if (!success)
            {
                MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Approach 3: Searching for GameCore...");
                success = TryUseGameCore();
                yield return null;
            }
            
            // Approach 4: Search for any manager with Load methods
            if (!success)
            {
                MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Approach 4: Searching for any loading systems...");
                success = SearchForLoadingSystems();
                yield return null;
            }
            
            if (!success)
            {
                MultiplayerMod.Log.LogWarning("[WorldLoaderV2] ========================================");
                MultiplayerMod.Log.LogWarning("[WorldLoaderV2] Could not find game loading systems");
                MultiplayerMod.Log.LogWarning("[WorldLoaderV2] Manual workaround: Client should click Play and load a save");
                MultiplayerMod.Log.LogWarning("[WorldLoaderV2] ========================================");
            }
        }
        
        /// <summary>
        /// Try to find and use SavegameManager
        /// </summary>
        private bool TryUseSavegameManager()
        {
            try
            {
                // Search for SavegameManager type
                var managerType = FindTypeByName("SavegameManager");
                if (managerType != null)
                {
                    MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Found SavegameManager type!");
                    LogTypeStructure(managerType);
                    
                    // Try to find instance
                    var instance = FindInstance(managerType);
                    if (instance != null)
                    {
                        MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Found SavegameManager instance!");
                        
                        // Look for Load methods
                        var methods = managerType.GetMethods(System.Reflection.BindingFlags.Public | 
                                                            System.Reflection.BindingFlags.Instance);
                        
                        foreach (var method in methods)
                        {
                            if (method.Name.Contains("Load") || method.Name.Contains("Continue") || 
                                method.Name.Contains("Start") || method.Name.Contains("Open"))
                            {
                                MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Found potential method: {method.Name}");
                                var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                MultiplayerMod.Log.LogInfo($"[WorldLoaderV2]   Parameters: ({paramStr})");
                                
                                // Try to call methods with simple parameters
                                if (TryInvokeLoadMethod(instance, method))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error in TryUseSavegameManager: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Try to find and use IGameData
        /// </summary>
        private bool TryUseGameData()
        {
            try
            {
                var gameDataType = FindTypeByName("IGameData");
                if (gameDataType != null)
                {
                    MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Found IGameData type!");
                    LogTypeStructure(gameDataType);
                    
                    var instance = FindInstance(gameDataType);
                    if (instance != null)
                    {
                        MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Found IGameData instance!");
                        return TryUseInstanceMethods(instance, gameDataType);
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error in TryUseGameData: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Try to find and use GameCore
        /// </summary>
        private bool TryUseGameCore()
        {
            try
            {
                var gameCoreType = FindTypeByName("GameCore");
                if (gameCoreType != null)
                {
                    MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Found GameCore type!");
                    LogTypeStructure(gameCoreType);
                    
                    var instance = FindInstance(gameCoreType);
                    if (instance != null)
                    {
                        MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Found GameCore instance!");
                        
                        // IMPORTANT: We need to provide options BEFORE calling Start()
                        MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Setting up CrossSceneGameOptionsTransfer first...");
                        if (SetupCrossSceneOptions())
                        {
                            MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Options provided, now calling GameCore methods...");
                            return TryUseInstanceMethods(instance, gameCoreType);
                        }
                        else
                        {
                            MultiplayerMod.Log.LogWarning("[WorldLoaderV2] Could not setup options, skipping GameCore");
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error in TryUseGameCore: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Setup CrossSceneGameOptionsTransfer with minimal options
        /// </summary>
        private bool SetupCrossSceneOptions()
        {
            try
            {
                MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Looking for CrossSceneGameOptionsTransfer...");
                
                var optionsTransferType = FindTypeByName("CrossSceneGameOptionsTransfer");
                if (optionsTransferType == null)
                {
                    MultiplayerMod.Log.LogWarning("[WorldLoaderV2] Could not find CrossSceneGameOptionsTransfer");
                    return false;
                }
                
                MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Found CrossSceneGameOptionsTransfer!");
                
                // Create an instance
                var optionsTransfer = System.Activator.CreateInstance(optionsTransferType);
                if (optionsTransfer == null)
                {
                    MultiplayerMod.Log.LogWarning("[WorldLoaderV2] Could not create CrossSceneGameOptionsTransfer instance");
                    return false;
                }
                
                MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Created CrossSceneGameOptionsTransfer instance");
                
                // Find the Provide method
                var provideMethod = optionsTransferType.GetMethod("Provide", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (provideMethod == null)
                {
                    MultiplayerMod.Log.LogWarning("[WorldLoaderV2] Could not find Provide method");
                    return false;
                }
                
                // Get the parameter type (IGameStartOptions)
                var parameters = provideMethod.GetParameters();
                if (parameters.Length == 0)
                {
                    MultiplayerMod.Log.LogWarning("[WorldLoaderV2] Provide method has no parameters");
                    return false;
                }
                
                var optionsInterfaceType = parameters[0].ParameterType;
                MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Need to create: {optionsInterfaceType.Name}");
                
                // Try to create a simple mock options object
                var mockOptions = CreateMockGameStartOptions(optionsInterfaceType);
                
                if (mockOptions != null)
                {
                    MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Created mock options, calling Provide...");
                    provideMethod.Invoke(optionsTransfer, new object[] { mockOptions });
                    MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Successfully provided options!");
                    return true;
                }
                else
                {
                    MultiplayerMod.Log.LogWarning("[WorldLoaderV2] Could not create mock options");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error setting up options: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Create a mock IGameStartOptions using a dynamic proxy or simple implementation
        /// </summary>
        private object CreateMockGameStartOptions(System.Type interfaceType)
        {
            try
            {
                MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Creating mock for {interfaceType.Name}...");
                
                // Find GameStartOptionsStartNew - we know it exists
                var startNewType = FindTypeByName("GameStartOptionsStartNew");
                if (startNewType != null)
                {
                    MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Found GameStartOptionsStartNew");
                    
                    // Try to create with null parameters as a last resort
                    var constructors = startNewType.GetConstructors();
                    if (constructors.Length > 0)
                    {
                        var ctor = constructors[0];
                        var paramCount = ctor.GetParameters().Length;
                        MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Constructor needs {paramCount} parameters");
                        
                        // Create null array - risky but might work
                        var nullParams = new object[paramCount];
                        
                        try
                        {
                            var instance = ctor.Invoke(nullParams);
                            if (instance != null)
                            {
                                MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Created instance with null parameters!");
                                return instance;
                            }
                        }
                        catch (Exception ctorEx)
                        {
                            MultiplayerMod.Log.LogWarning($"[WorldLoaderV2] Null parameters failed: {ctorEx.Message}");
                        }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error creating mock: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Search for any types that might handle loading
        /// </summary>
        private bool SearchForLoadingSystems()
        {
            try
            {
                MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Searching all types for loading systems...");
                
                // PRIORITY: Look for WorldLoader with LoadWorld method first!
                var worldLoaderTypes = FindAllTypesByName("WorldLoader");
                MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Found {worldLoaderTypes.Length} WorldLoader types");
                
                foreach (var worldLoaderType in worldLoaderTypes)
                {
                    MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Checking WorldLoader: {worldLoaderType.FullName}");
                    LogTypeStructure(worldLoaderType);
                    
                    // Look for LoadWorld method
                    var loadWorldMethod = worldLoaderType.GetMethod("LoadWorld", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                    if (loadWorldMethod != null)
                    {
                        MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Found LoadWorld method!");
                        var parameters = loadWorldMethod.GetParameters();
                        var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] LoadWorld({paramStr})");
                        
                        // Try to find instance
                        var instance = FindInstance(worldLoaderType);
                        if (instance != null)
                        {
                            MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Found WorldLoader instance!");
                            
                            // Try to invoke LoadWorld
                            if (parameters.Length == 2 && 
                                parameters[0].ParameterType == typeof(string) && 
                                parameters[1].ParameterType == typeof(string))
                            {
                                MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Perfect match! Invoking LoadWorld(worldName, saveData)...");
                                try
                                {
                                    loadWorldMethod.Invoke(instance, new object[] { "multiplayer_world", "" });
                                    MultiplayerMod.Log.LogInfo("[WorldLoaderV2] Successfully invoked LoadWorld!");
                                    return true;
                                }
                                catch (Exception ex)
                                {
                                    MultiplayerMod.Log.LogWarning($"[WorldLoaderV2] LoadWorld invocation failed: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            MultiplayerMod.Log.LogWarning("[WorldLoaderV2] Could not find WorldLoader instance");
                        }
                    }
                }
                
                // Try other systems
                string[] searchTerms = { "Savegame", "GameLoader", "GameManager", 
                                        "SaveManager", "GameController", "LoadingManager" };
                
                foreach (var term in searchTerms)
                {
                    var type = FindTypeByName(term);
                    if (type != null)
                    {
                        MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Found type: {type.Name}");
                        LogTypeStructure(type);
                        
                        var instance = FindInstance(type);
                        if (instance != null)
                        {
                            MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Found instance of {type.Name}!");
                            if (TryUseInstanceMethods(instance, type))
                            {
                                return true;
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error in SearchForLoadingSystems: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Try to use methods on an instance
        /// </summary>
        private bool TryUseInstanceMethods(object instance, System.Type type)
        {
            try
            {
                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | 
                                             System.Reflection.BindingFlags.Instance);
                
                foreach (var method in methods)
                {
                    if (method.Name.Contains("Load") || method.Name.Contains("Start") || 
                        method.Name.Contains("Continue") || method.Name.Contains("Open"))
                    {
                        MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Found method: {method.Name}");
                        
                        if (TryInvokeLoadMethod(instance, method))
                        {
                            return true;
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error in TryUseInstanceMethods: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Try to invoke a load method
        /// </summary>
        private bool TryInvokeLoadMethod(object instance, System.Reflection.MethodInfo method)
        {
            try
            {
                var parameters = method.GetParameters();
                var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Trying to invoke: {method.Name}({paramStr})");
                
                // Only try methods with 0-2 simple parameters
                if (parameters.Length == 0)
                {
                    MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Invoking {method.Name}()...");
                    method.Invoke(instance, null);
                    MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Successfully invoked {method.Name}!");
                    return true;
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Invoking {method.Name}(\"multiplayer_session\")...");
                    method.Invoke(instance, new object[] { "multiplayer_session" });
                    MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Successfully invoked {method.Name}!");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogWarning($"[WorldLoaderV2] Could not invoke {method.Name}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Find an instance of a type
        /// </summary>
        private object FindInstance(System.Type type)
        {
            try
            {
                // Try static Instance property
                var instanceProp = type.GetProperty("Instance", System.Reflection.BindingFlags.Public | 
                                                               System.Reflection.BindingFlags.Static);
                if (instanceProp != null)
                {
                    var instance = instanceProp.GetValue(null);
                    if (instance != null)
                    {
                        MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Found via Instance property");
                        return instance;
                    }
                }
                
                // Try static fields
                var fields = type.GetFields(System.Reflection.BindingFlags.Public | 
                                           System.Reflection.BindingFlags.Static);
                foreach (var field in fields)
                {
                    if (field.FieldType == type)
                    {
                        var instance = field.GetValue(null);
                        if (instance != null)
                        {
                            MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Found via static field: {field.Name}");
                            return instance;
                        }
                    }
                }
                
                // Try FindObjectOfType if it's a MonoBehaviour
                if (typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    var instance = GameObject.FindObjectOfType(type);
                    if (instance != null)
                    {
                        MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Found via FindObjectOfType");
                        return instance;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error finding instance: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Log type structure for debugging
        /// </summary>
        private void LogTypeStructure(System.Type type)
        {
            try
            {
                MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] === Type: {type.Name} ===");
                
                // Log static fields
                var staticFields = type.GetFields(System.Reflection.BindingFlags.Public | 
                                                 System.Reflection.BindingFlags.Static);
                if (staticFields.Length > 0)
                {
                    MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Static Fields ({staticFields.Length}):");
                    foreach (var field in staticFields)
                    {
                        MultiplayerMod.Log.LogInfo($"[WorldLoaderV2]   {field.FieldType.Name} {field.Name}");
                    }
                }
                
                // Log static properties
                var staticProps = type.GetProperties(System.Reflection.BindingFlags.Public | 
                                                    System.Reflection.BindingFlags.Static);
                if (staticProps.Length > 0)
                {
                    MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Static Properties ({staticProps.Length}):");
                    foreach (var prop in staticProps)
                    {
                        MultiplayerMod.Log.LogInfo($"[WorldLoaderV2]   {prop.PropertyType.Name} {prop.Name}");
                    }
                }
                
                // Log interesting methods
                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | 
                                             System.Reflection.BindingFlags.Instance);
                var interestingMethods = methods.Where(m => 
                    m.Name.Contains("Load") || m.Name.Contains("Start") || 
                    m.Name.Contains("Continue") || m.Name.Contains("Open") ||
                    m.Name.Contains("Begin") || m.Name.Contains("Init")).ToArray();
                
                if (interestingMethods.Length > 0)
                {
                    MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] Interesting Methods ({interestingMethods.Length}):");
                    foreach (var method in interestingMethods)
                    {
                        var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        MultiplayerMod.Log.LogInfo($"[WorldLoaderV2]   {method.ReturnType.Name} {method.Name}({paramStr})");
                    }
                }
                
                MultiplayerMod.Log.LogInfo($"[WorldLoaderV2] === End Type ===");
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error logging type: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Find a type by name (excluding mod types)
        /// </summary>
        private System.Type FindTypeByName(string typeName)
        {
            try
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.Contains(typeName))
                            {
                                // Skip our own mod types
                                if (type.Namespace != null && type.Namespace.Contains("Shapez2MultiplayerMod"))
                                {
                                    continue;
                                }
                                
                                return type;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error finding type: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Find all types matching a name
        /// </summary>
        private System.Type[] FindAllTypesByName(string typeName)
        {
            try
            {
                var types = new System.Collections.Generic.List<System.Type>();
                
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name.Contains(typeName))
                            {
                                // Skip our own mod types
                                if (type.Namespace != null && type.Namespace.Contains("Shapez2MultiplayerMod"))
                                {
                                    continue;
                                }
                                
                                types.Add(type);
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                return types.ToArray();
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoaderV2] Error finding types: {ex.Message}");
                return new System.Type[0];
            }
        }
    }
}

