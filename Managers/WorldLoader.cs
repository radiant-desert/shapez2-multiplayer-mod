using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shapez2MultiplayerMod.Managers
{
    /// <summary>
    /// Handles actual world/scene loading for multiplayer
    /// This class bridges the gap between network packets and game systems
    /// </summary>
    public class WorldLoader
    {
        private MonoBehaviour coroutineHost;
        
        public WorldLoader(MonoBehaviour host)
        {
            coroutineHost = host;
            MultiplayerMod.Log.LogInfo("[WorldLoader] Initialized");
        }
        
        /// <summary>
        /// Get the currently loaded save/world name from the host
        /// </summary>
        public string GetCurrentWorldName()
        {
            try
            {
                // Try to find the active scene name
                Scene activeScene = SceneManager.GetActiveScene();
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Active scene: {activeScene.name}");
                
                // TODO: Find the actual save name from game's save system
                // For now, return the scene name
                return activeScene.name;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Failed to get world name: {ex.Message}");
                return "Unknown";
            }
        }
        
        /// <summary>
        /// Load a world/save on the client
        /// This is called when client receives world state from host
        /// </summary>
        public void LoadWorld(string worldName, string saveData = null)
        {
            try
            {
                MultiplayerMod.Log.LogInfo("========================================");
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Attempting to load world: {worldName}");
                MultiplayerMod.Log.LogInfo("========================================");
                
                // Start the loading coroutine
                if (coroutineHost != null)
                {
                    coroutineHost.StartCoroutine(LoadWorldCoroutine(worldName, saveData));
                }
                else
                {
                    MultiplayerMod.Log.LogError("[WorldLoader] No coroutine host available!");
                }
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Failed to start world loading: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Coroutine to load world asynchronously
        /// </summary>
        private IEnumerator LoadWorldCoroutine(string worldName, string saveData)
        {
            MultiplayerMod.Log.LogInfo("[WorldLoader] Starting world load coroutine...");
            
            // Step 1: Try to find and use game's save system
            bool loadedViaGameSystem = TryLoadViaGameSystem(worldName, saveData);
            
            if (loadedViaGameSystem)
            {
                MultiplayerMod.Log.LogInfo("[WorldLoader] ✓ World loaded via game system");
                yield break;
            }
            
            // Step 2: Try to load via scene management
            MultiplayerMod.Log.LogInfo("[WorldLoader] Attempting scene-based loading...");
            bool loadedViaScene = TryLoadViaScene(worldName);
            
            if (loadedViaScene)
            {
                MultiplayerMod.Log.LogInfo("[WorldLoader] ✓ World loaded via scene management");
                yield break;
            }
            
            // Step 3: Try to find gameplay scene
            MultiplayerMod.Log.LogInfo("[WorldLoader] Attempting to find gameplay scene...");
            bool foundGameplayScene = TryFindAndLoadGameplayScene();
            
            if (foundGameplayScene)
            {
                MultiplayerMod.Log.LogInfo("[WorldLoader] ✓ Loaded gameplay scene");
                yield break;
            }
            
            // If all methods fail, log detailed information
            MultiplayerMod.Log.LogWarning("========================================");
            MultiplayerMod.Log.LogWarning("[WorldLoader] ⚠ Could not automatically load world");
            MultiplayerMod.Log.LogWarning("[WorldLoader] This requires game API integration");
            MultiplayerMod.Log.LogWarning("[WorldLoader] Network sync is working, but world loading needs research");
            MultiplayerMod.Log.LogWarning("========================================");
            
            // Log available scenes for debugging
            LogAvailableScenes();
        }
        
        /// <summary>
        /// Try to load world using game's save system
        /// </summary>
        private bool TryLoadViaGameSystem(string worldName, string saveData)
        {
            try
            {
                MultiplayerMod.Log.LogInfo("[WorldLoader] Searching for game's loading system...");
                
                // Look for CrossSceneGameOptionsTransfer
                var optionsTransferType = FindTypeByName("CrossSceneGameOptionsTransfer");
                if (optionsTransferType != null)
                {
                    MultiplayerMod.Log.LogInfo("[WorldLoader] Found CrossSceneGameOptionsTransfer!");
                    
                    // Log all properties and methods to understand the structure
                    LogTypeStructure(optionsTransferType);
                    
                    // Try multiple ways to get/create an instance
                    object instance = null;
                    
                    // Method 1: Try static Instance property
                    var instanceProperty = optionsTransferType.GetProperty("Instance", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (instanceProperty != null)
                    {
                        instance = instanceProperty.GetValue(null);
                        if (instance != null)
                        {
                            MultiplayerMod.Log.LogInfo("[WorldLoader] Got instance via Instance property");
                        }
                    }
                    
                    // Method 2: Look for static field or method that returns instance
                    if (instance == null)
                    {
                        MultiplayerMod.Log.LogInfo("[WorldLoader] Searching for static instance accessor...");
                        
                        // Try common static field names
                        var fields = optionsTransferType.GetFields(System.Reflection.BindingFlags.Public | 
                                                                   System.Reflection.BindingFlags.Static);
                        foreach (var field in fields)
                        {
                            MultiplayerMod.Log.LogInfo($"[WorldLoader] Found static field: {field.Name}");
                            if (field.FieldType == optionsTransferType)
                            {
                                instance = field.GetValue(null);
                                if (instance != null)
                                {
                                    MultiplayerMod.Log.LogInfo($"[WorldLoader] Got instance via static field: {field.Name}");
                                    break;
                                }
                            }
                        }
                        
                        // Try static methods that might return instance
                        if (instance == null)
                        {
                            var methods = optionsTransferType.GetMethods(System.Reflection.BindingFlags.Public | 
                                                                        System.Reflection.BindingFlags.Static);
                            foreach (var method in methods)
                            {
                                if (method.ReturnType == optionsTransferType && method.GetParameters().Length == 0)
                                {
                                    MultiplayerMod.Log.LogInfo($"[WorldLoader] Trying static method: {method.Name}");
                                    try
                                    {
                                        instance = method.Invoke(null, null);
                                        if (instance != null)
                                        {
                                            MultiplayerMod.Log.LogInfo($"[WorldLoader] Got instance via static method: {method.Name}");
                                            break;
                                        }
                                    }
                                    catch (Exception methodEx)
                                    {
                                        MultiplayerMod.Log.LogWarning($"[WorldLoader] Method {method.Name} failed: {methodEx.Message}");
                                    }
                                }
                            }
                        }
                    }
                    
                    // Method 3: Try to create a new instance
                    if (instance == null)
                    {
                        MultiplayerMod.Log.LogInfo("[WorldLoader] Trying to create new instance...");
                        try
                        {
                            instance = System.Activator.CreateInstance(optionsTransferType);
                            if (instance != null)
                            {
                                MultiplayerMod.Log.LogInfo("[WorldLoader] Created new instance!");
                            }
                        }
                        catch (Exception createEx)
                        {
                            MultiplayerMod.Log.LogWarning($"[WorldLoader] Could not create instance: {createEx.Message}");
                        }
                    }
                    
                    // If we have an instance, try to use it
                    if (instance != null)
                    {
                        return TryProvideGameOptions(instance, optionsTransferType);
                    }
                    else
                    {
                        MultiplayerMod.Log.LogWarning("[WorldLoader] Could not get or create CrossSceneGameOptionsTransfer instance");
                        // Try alternative approach - simulate the Play button
                        return TrySimulatePlayButton();
                    }
                }
                else
                {
                    MultiplayerMod.Log.LogWarning("[WorldLoader] Could not find CrossSceneGameOptionsTransfer type");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error in TryLoadViaGameSystem: {ex.Message}");
                MultiplayerMod.Log.LogError($"[WorldLoader] Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Log the structure of a type for debugging
        /// </summary>
        private void LogTypeStructure(System.Type type)
        {
            try
            {
                MultiplayerMod.Log.LogInfo($"[WorldLoader] === Type Structure: {type.Name} ===");
                
                // Log properties
                var properties = type.GetProperties(System.Reflection.BindingFlags.Public | 
                                                   System.Reflection.BindingFlags.Static | 
                                                   System.Reflection.BindingFlags.Instance);
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Properties ({properties.Length}):");
                foreach (var prop in properties)
                {
                    string scope = prop.GetMethod?.IsStatic == true ? "static" : "instance";
                    MultiplayerMod.Log.LogInfo($"[WorldLoader]   {scope} {prop.PropertyType.Name} {prop.Name}");
                }
                
                // Log methods
                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | 
                                             System.Reflection.BindingFlags.Static | 
                                             System.Reflection.BindingFlags.Instance | 
                                             System.Reflection.BindingFlags.DeclaredOnly);
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Methods ({methods.Length}):");
                foreach (var method in methods)
                {
                    string scope = method.IsStatic ? "static" : "instance";
                    var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    MultiplayerMod.Log.LogInfo($"[WorldLoader]   {scope} {method.ReturnType.Name} {method.Name}({paramStr})");
                }
                
                MultiplayerMod.Log.LogInfo($"[WorldLoader] === End Type Structure ===");
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error logging type structure: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Try to simulate clicking the Play button
        /// </summary>
        private bool TrySimulatePlayButton()
        {
            try
            {
                MultiplayerMod.Log.LogInfo("[WorldLoader] Attempting to simulate Play button...");
                
                // Look for MainMenu or similar UI controller
                var mainMenuType = FindTypeByName("MainMenu");
                if (mainMenuType != null)
                {
                    MultiplayerMod.Log.LogInfo("[WorldLoader] Found MainMenu type");
                    
                    // Try to find instance
                    var mainMenuInstance = GameObject.FindObjectOfType(mainMenuType);
                    if (mainMenuInstance != null)
                    {
                        MultiplayerMod.Log.LogInfo("[WorldLoader] Found MainMenu instance");
                        LogTypeStructure(mainMenuType);
                        
                        // Look for methods that might start the game
                        var methods = mainMenuType.GetMethods(System.Reflection.BindingFlags.Public | 
                                                             System.Reflection.BindingFlags.Instance);
                        
                        foreach (var method in methods)
                        {
                            if (method.Name.Contains("Play") || method.Name.Contains("Start") || 
                                method.Name.Contains("Load") || method.Name.Contains("Begin"))
                            {
                                MultiplayerMod.Log.LogInfo($"[WorldLoader] Found potential method: {method.Name}");
                                // Could try to invoke it here
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error simulating play button: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Try to provide game options and load via GameLoading scene
        /// </summary>
        private bool TryProvideGameOptions(object optionsTransferInstance, System.Type optionsTransferType)
        {
            try
            {
                MultiplayerMod.Log.LogInfo("[WorldLoader] Attempting to provide game options...");
                
                // Look for the Provide method - we know it's: Provide(IGameStartOptions options)
                var provideMethod = optionsTransferType.GetMethod("Provide", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (provideMethod != null)
                {
                    var parameters = provideMethod.GetParameters();
                    MultiplayerMod.Log.LogInfo($"[WorldLoader] Found Provide method with {parameters.Length} parameters");
                    
                    if (parameters.Length > 0)
                    {
                        // The parameter type is IGameStartOptions
                        var optionsInterfaceType = parameters[0].ParameterType;
                        MultiplayerMod.Log.LogInfo($"[WorldLoader] Options interface type: {optionsInterfaceType.Name}");
                        
                        // Find a concrete implementation of IGameStartOptions
                        var optionsImpl = FindGameStartOptionsImplementation(optionsInterfaceType);
                        
                        if (optionsImpl != null)
                        {
                            MultiplayerMod.Log.LogInfo("[WorldLoader] Created game options, invoking Provide...");
                            provideMethod.Invoke(optionsTransferInstance, new object[] { optionsImpl });
                            
                            // Now load the GameLoading scene (not Ingame directly!)
                            MultiplayerMod.Log.LogInfo("[WorldLoader] Loading GameLoading scene...");
                            SceneManager.LoadScene("GameLoading");
                            
                            return true;
                        }
                        else
                        {
                            MultiplayerMod.Log.LogWarning("[WorldLoader] Could not create IGameStartOptions implementation");
                        }
                    }
                }
                else
                {
                    MultiplayerMod.Log.LogWarning("[WorldLoader] Could not find Provide method");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error providing game options: {ex.Message}");
                MultiplayerMod.Log.LogError($"[WorldLoader] Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// Find and create an implementation of IGameStartOptions
        /// </summary>
        private object FindGameStartOptionsImplementation(System.Type interfaceType)
        {
            try
            {
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Searching for implementations of {interfaceType.Name}...");
                
                // Search all loaded assemblies for classes that implement this interface
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (interfaceType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                            {
                                MultiplayerMod.Log.LogInfo($"[WorldLoader] Found implementation: {type.Name}");
                                
                                // Log the structure of this implementation
                                LogTypeStructure(type);
                                
                                // Try to create an instance
                                var instance = TryCreateGameOptions(type);
                                if (instance != null)
                                {
                                    return instance;
                                }
                            }
                        }
                    }
                    catch (Exception assemblyEx)
                    {
                        // Some assemblies might not be accessible, skip them
                        continue;
                    }
                }
                
                MultiplayerMod.Log.LogWarning($"[WorldLoader] No implementation found for {interfaceType.Name}");
                return null;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error finding implementation: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Try to create a game options object
        /// </summary>
        private object TryCreateGameOptions(System.Type optionsType)
        {
            try
            {
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Creating instance of {optionsType.Name}...");
                
                // Log all constructors
                var constructors = optionsType.GetConstructors(System.Reflection.BindingFlags.Public | 
                                                               System.Reflection.BindingFlags.Instance);
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Found {constructors.Length} constructors");
                
                foreach (var ctor in constructors)
                {
                    var parameters = ctor.GetParameters();
                    var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    MultiplayerMod.Log.LogInfo($"[WorldLoader] Constructor: {optionsType.Name}({paramStr})");
                    
                    // Try to create parameters for this constructor
                    var paramValues = new object[parameters.Length];
                    bool canCreate = true;
                    
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var param = parameters[i];
                        MultiplayerMod.Log.LogInfo($"[WorldLoader] Need parameter: {param.ParameterType.Name} {param.Name}");
                        
                        // Try to create or find this parameter
                        object paramValue = TryCreateParameter(param.ParameterType, param.Name);
                        
                        if (paramValue != null)
                        {
                            paramValues[i] = paramValue;
                            MultiplayerMod.Log.LogInfo($"[WorldLoader] Created parameter {param.Name}");
                        }
                        else
                        {
                            MultiplayerMod.Log.LogWarning($"[WorldLoader] Could not create parameter {param.Name}");
                            canCreate = false;
                            break;
                        }
                    }
                    
                    if (canCreate)
                    {
                        try
                        {
                            var instance = ctor.Invoke(paramValues);
                            if (instance != null)
                            {
                                MultiplayerMod.Log.LogInfo($"[WorldLoader] Successfully created {optionsType.Name}!");
                                return instance;
                            }
                        }
                        catch (Exception ctorEx)
                        {
                            MultiplayerMod.Log.LogWarning($"[WorldLoader] Constructor invocation failed: {ctorEx.Message}");
                        }
                    }
                }
                
                // Try default constructor as fallback
                try
                {
                    var options = System.Activator.CreateInstance(optionsType);
                    if (options != null)
                    {
                        MultiplayerMod.Log.LogInfo("[WorldLoader] Created via default constructor");
                        return options;
                    }
                }
                catch (Exception ex)
                {
                    MultiplayerMod.Log.LogError($"[WorldLoader] Error creating game options: {ex.Message}");
                }
                
                return null;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error in TryCreateGameOptions: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Try to create a parameter value for a constructor
        /// </summary>
        private object TryCreateParameter(System.Type paramType, string paramName)
        {
            try
            {
                // Handle common types
                if (paramType == typeof(string))
                {
                    if (paramName.ToLower().Contains("uid") || paramName.ToLower().Contains("id"))
                    {
                        return "multiplayer_session";
                    }
                    return "default";
                }
                
                if (paramType == typeof(bool))
                {
                    if (paramName.ToLower().Contains("menu"))
                    {
                        return false; // Not in menu mode
                    }
                    return false;
                }
                
                if (paramType == typeof(int))
                {
                    return 0;
                }
                
                // For SavegameBlobReader or similar, try to find or create it
                if (paramType.Name.Contains("Savegame") || paramType.Name.Contains("Reader"))
                {
                    MultiplayerMod.Log.LogInfo($"[WorldLoader] Need to create {paramType.Name}");
                    LogTypeStructure(paramType);
                    
                    // Try to create it
                    try
                    {
                        var instance = System.Activator.CreateInstance(paramType);
                        return instance;
                    }
                    catch
                    {
                        // Try to find it
                        var foundInstance = GameObject.FindObjectOfType(paramType);
                        if (foundInstance != null)
                        {
                            return foundInstance;
                        }
                    }
                }
                
                // For GameParameters or similar config types
                if (paramType.Name.Contains("Parameter") || paramType.Name.Contains("Config"))
                {
                    MultiplayerMod.Log.LogInfo($"[WorldLoader] Need to create {paramType.Name}");
                    LogTypeStructure(paramType);
                    
                    // Special handling for GameParameters
                    if (paramType.Name == "GameParameters")
                    {
                        // Use GameParameters.DefaultForMode(modeDefinition)
                        var defaultForModeMethod = paramType.GetMethod("DefaultForMode", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        
                        if (defaultForModeMethod != null)
                        {
                            MultiplayerMod.Log.LogInfo("[WorldLoader] Found DefaultForMode method!");
                            
                            // Get the GameModeDefinition parameter type
                            var modeDefType = defaultForModeMethod.GetParameters()[0].ParameterType;
                            MultiplayerMod.Log.LogInfo($"[WorldLoader] Need {modeDefType.Name} for DefaultForMode");
                            
                            // Try to find a default mode definition
                            var modeDefinition = FindDefaultGameMode(modeDefType);
                            
                            if (modeDefinition != null)
                            {
                                MultiplayerMod.Log.LogInfo("[WorldLoader] Got game mode definition, calling DefaultForMode...");
                                var gameParams = defaultForModeMethod.Invoke(null, new object[] { modeDefinition });
                                
                                if (gameParams != null)
                                {
                                    MultiplayerMod.Log.LogInfo("[WorldLoader] Successfully created GameParameters!");
                                    return gameParams;
                                }
                            }
                        }
                    }
                    
                    // Try default constructor
                    try
                    {
                        var instance = System.Activator.CreateInstance(paramType);
                        return instance;
                    }
                    catch
                    {
                        // Try to find a default instance
                        var defaultField = paramType.GetField("Default", System.Reflection.BindingFlags.Public | 
                                                                         System.Reflection.BindingFlags.Static);
                        if (defaultField != null)
                        {
                            return defaultField.GetValue(null);
                        }
                    }
                }
                
                // Try to create any type with default constructor
                try
                {
                    return System.Activator.CreateInstance(paramType);
                }
                catch
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error creating parameter {paramName}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Find a default game mode definition
        /// </summary>
        private object FindDefaultGameMode(System.Type modeDefType)
        {
            try
            {
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Searching for {modeDefType.Name}...");
                LogTypeStructure(modeDefType);
                
                // Try to find a static instance or default
                var fields = modeDefType.GetFields(System.Reflection.BindingFlags.Public | 
                                                   System.Reflection.BindingFlags.Static);
                
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Found {fields.Length} static fields");
                foreach (var field in fields)
                {
                    MultiplayerMod.Log.LogInfo($"[WorldLoader] Static field: {field.FieldType.Name} {field.Name}");
                    
                    // Look for fields like "Regular", "Normal", "Default", etc.
                    if (field.FieldType == modeDefType)
                    {
                        var value = field.GetValue(null);
                        if (value != null)
                        {
                            MultiplayerMod.Log.LogInfo($"[WorldLoader] Using game mode from field: {field.Name}");
                            return value;
                        }
                    }
                }
                
                // Try to find via properties
                var properties = modeDefType.GetProperties(System.Reflection.BindingFlags.Public | 
                                                          System.Reflection.BindingFlags.Static);
                
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Found {properties.Length} static properties");
                foreach (var prop in properties)
                {
                    MultiplayerMod.Log.LogInfo($"[WorldLoader] Static property: {prop.PropertyType.Name} {prop.Name}");
                    
                    if (prop.PropertyType == modeDefType)
                    {
                        var value = prop.GetValue(null);
                        if (value != null)
                        {
                            MultiplayerMod.Log.LogInfo($"[WorldLoader] Using game mode from property: {prop.Name}");
                            return value;
                        }
                    }
                }
                
                // Try to create a default instance
                try
                {
                    var instance = System.Activator.CreateInstance(modeDefType);
                    if (instance != null)
                    {
                        MultiplayerMod.Log.LogInfo("[WorldLoader] Created default game mode instance");
                        return instance;
                    }
                }
                catch (Exception ex)
                {
                    MultiplayerMod.Log.LogWarning($"[WorldLoader] Could not create default instance: {ex.Message}");
                }
                
                MultiplayerMod.Log.LogWarning($"[WorldLoader] Could not find {modeDefType.Name}");
                return null;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error finding game mode: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Find a type by name across all loaded assemblies
        /// </summary>
        private System.Type FindTypeByName(string typeName)
        {
            try
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == typeName || type.FullName.Contains(typeName))
                        {
                            return type;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error finding type {typeName}: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Try to load world by switching scenes
        /// </summary>
        private bool TryLoadViaScene(string worldName)
        {
            try
            {
                // IMPORTANT: Don't load the scene directly
                // Shapez 2 requires game options to be set first
                // This causes the "No options provided to consume" error
                
                MultiplayerMod.Log.LogWarning("[WorldLoader] Direct scene loading causes game options error");
                MultiplayerMod.Log.LogWarning("[WorldLoader] Need to set CrossSceneGameOptionsTransfer before loading");
                
                return false; // Disabled for now
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error in TryLoadViaScene: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Try to find and load the gameplay scene
        /// </summary>
        private bool TryFindAndLoadGameplayScene()
        {
            try
            {
                // IMPORTANT: Direct scene loading doesn't work for Shapez 2
                // The game requires CrossSceneGameOptionsTransfer to be set
                // We need to find and use the game's proper loading mechanism
                
                MultiplayerMod.Log.LogWarning("[WorldLoader] Direct scene loading not supported");
                MultiplayerMod.Log.LogWarning("[WorldLoader] Shapez 2 requires game options transfer");
                
                return false; // Disabled for now
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error in TryFindAndLoadGameplayScene: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Log all available scenes for debugging
        /// </summary>
        private void LogAvailableScenes()
        {
            try
            {
                MultiplayerMod.Log.LogInfo("========================================");
                MultiplayerMod.Log.LogInfo("[WorldLoader] Available Scenes:");
                
                int sceneCount = SceneManager.sceneCountInBuildSettings;
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Total scenes in build: {sceneCount}");
                
                for (int i = 0; i < sceneCount; i++)
                {
                    string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                    MultiplayerMod.Log.LogInfo($"[WorldLoader]   [{i}] {sceneName} ({scenePath})");
                }
                
                MultiplayerMod.Log.LogInfo($"[WorldLoader] Current active scene: {SceneManager.GetActiveScene().name}");
                MultiplayerMod.Log.LogInfo("========================================");
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[WorldLoader] Error logging scenes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if we're currently in a gameplay scene
        /// </summary>
        public bool IsInGameplay()
        {
            try
            {
                Scene activeScene = SceneManager.GetActiveScene();
                string sceneName = activeScene.name.ToLower();
                
                // Check if scene name suggests gameplay
                return sceneName.Contains("game") || 
                       sceneName.Contains("play") || 
                       sceneName.Contains("world") ||
                       sceneName.Contains("level");
            }
            catch
            {
                return false;
            }
        }
    }
}

