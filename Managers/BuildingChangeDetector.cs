using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod.Networking;

namespace Shapez2MultiplayerMod.Managers
{
    /// <summary>
    /// Detects building changes by polling the game state
    /// Much simpler than trying to hook into placement events
    /// </summary>
    public class BuildingChangeDetector
    {
        private NetworkManager networkManager;
        private object buildingLayoutModel = null;
        private Dictionary<string, BuildingSnapshot> lastKnownBuildings = new Dictionary<string, BuildingSnapshot>();
        private bool isInitialized = false;
        private float updateInterval = 0.1f; // Check 10 times per second
        private float lastUpdateTime = 0f;
        
        public BuildingChangeDetector(NetworkManager netManager)
        {
            networkManager = netManager;
        }
        
        /// <summary>
        /// Called every frame to check for building changes
        /// </summary>
        public void Update()
        {
            if (!networkManager.IsConnected)
                return;
                
            if (Time.time - lastUpdateTime < updateInterval)
                return;
                
            lastUpdateTime = Time.time;
            
            if (!isInitialized)
            {
                TryInitialize();
                return;
            }
            
            CheckForBuildingChanges();
        }
        
        /// <summary>
        /// Try to find the building layout model by searching for MapModel type
        /// </summary>
        private void TryInitialize()
        {
            try
            {
                MultiplayerMod.Log.LogInfo("[BuildingChangeDetector] Searching for GameContext type...");
                
                // Search for GameContext type which should contain the Map
                Type gameContextType = FindTypeByName("GameContext");
                if (gameContextType == null)
                {
                    gameContextType = FindTypeByName("IGameContext");
                }
                
                if (gameContextType == null)
                {
                    MultiplayerMod.Log.LogWarning("[BuildingChangeDetector] GameContext type not found");
                    return;
                }
                
                MultiplayerMod.Log.LogInfo($"[BuildingChangeDetector] Found GameContext type: {gameContextType.FullName}");
                
                // Also find MapModel type for later
                Type mapModelType = FindTypeByName("MapModel");
                if (mapModelType == null)
                {
                    mapModelType = FindTypeByName("IMapModel");
                }
                
                if (mapModelType == null)
                {
                    MultiplayerMod.Log.LogWarning("[BuildingChangeDetector] MapModel type not found");
                    return;
                }
                
                MultiplayerMod.Log.LogInfo($"[BuildingChangeDetector] Found MapModel type: {mapModelType.FullName}");
                
                // Try to find an instance using FindObjectOfType
                object mapModel = null;
                if (typeof(UnityEngine.Object).IsAssignableFrom(mapModelType))
                {
                    var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[] { })
                        .MakeGenericMethod(mapModelType);
                    mapModel = findMethod.Invoke(null, null);
                }
                else
                {
                    // If it's not a MonoBehaviour, try to find it through GameCore
                    var gameCoreType = FindTypeByName("GameCore");
                    if (gameCoreType != null && typeof(UnityEngine.Object).IsAssignableFrom(gameCoreType))
                    {
                        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[] { })
                            .MakeGenericMethod(gameCoreType);
                        var gameCore = findMethod.Invoke(null, null);
                        
                        if (gameCore != null)
                        {
                            MultiplayerMod.Log.LogInfo("[BuildingChangeDetector] Found GameCore, searching for Map property...");
                            
                            // Search all properties and fields for something that returns MapModel
                            var allProps = gameCoreType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            MultiplayerMod.Log.LogInfo($"[BuildingChangeDetector] Checking {allProps.Length} GameCore properties...");
                            
                            foreach (var prop in allProps)
                            {
                                // Check if property directly returns MapModel
                                if (mapModelType.IsAssignableFrom(prop.PropertyType))
                                {
                                    MultiplayerMod.Log.LogInfo($"[BuildingChangeDetector] Property {prop.Name} has MapModel type, getting value...");
                                    mapModel = prop.GetValue(gameCore);
                                    if (mapModel != null)
                                    {
                                        MultiplayerMod.Log.LogInfo($"[BuildingChangeDetector] Found MapModel through GameCore.{prop.Name}");
                                        break;
                                    }
                                    else
                                    {
                                        MultiplayerMod.Log.LogWarning($"[BuildingChangeDetector] GameCore.{prop.Name} is null");
                                    }
                                }
                                
                                // Also check if property has a Map property
                                try
                                {
                                    var propValue = prop.GetValue(gameCore);
                                    if (propValue != null)
                                    {
                                        var mapProp = propValue.GetType().GetProperty("Map");
                                        if (mapProp != null)
                                        {
                                            if (mapModelType.IsAssignableFrom(mapProp.PropertyType))
                                            {
                                                MultiplayerMod.Log.LogInfo($"[BuildingChangeDetector] Property {prop.Name}.Map has MapModel type, getting value...");
                                                mapModel = mapProp.GetValue(propValue);
                                                if (mapModel != null)
                                                {
                                                    MultiplayerMod.Log.LogInfo($"[BuildingChangeDetector] Found MapModel through GameCore.{prop.Name}.Map");
                                                    break;
                                                }
                                                else
                                                {
                                                    MultiplayerMod.Log.LogWarning($"[BuildingChangeDetector] GameCore.{prop.Name}.Map is null");
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Silently skip properties that throw exceptions
                                }
                            }
                        }
                    }
                }
                
                if (mapModel == null)
                {
                    MultiplayerMod.Log.LogWarning("[BuildingChangeDetector] MapModel instance not found");
                    return;
                }
                
                MultiplayerMod.Log.LogInfo("[BuildingChangeDetector] Found MapModel instance!");
                
                // Get Buildings property from MapModel
                var buildingsProp = mapModel.GetType().GetProperty("Buildings");
                if (buildingsProp == null)
                {
                    MultiplayerMod.Log.LogWarning("[BuildingChangeDetector] MapModel.Buildings property not found");
                    return;
                }
                
                buildingLayoutModel = buildingsProp.GetValue(mapModel);
                if (buildingLayoutModel != null)
                {
                    isInitialized = true;
                    MultiplayerMod.Log.LogInfo("[BuildingChangeDetector] Successfully initialized!");
                    
                    // Take initial snapshot
                    SnapshotCurrentBuildings();
                }
                else
                {
                    MultiplayerMod.Log.LogWarning("[BuildingChangeDetector] Map.Buildings is null");
                }
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[BuildingChangeDetector] Error during initialization: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Take a snapshot of all current buildings
        /// </summary>
        private void SnapshotCurrentBuildings()
        {
            try
            {
                lastKnownBuildings.Clear();
                
                // Get all buildings
                var getAllMethod = buildingLayoutModel.GetType().GetMethod("GetAll");
                if (getAllMethod == null)
                    return;
                    
                var buildings = getAllMethod.Invoke(buildingLayoutModel, null);
                if (buildings == null)
                    return;
                    
                // Iterate through buildings
                var enumerableType = buildings.GetType();
                var getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator");
                if (getEnumeratorMethod == null)
                    return;
                    
                var enumerator = getEnumeratorMethod.Invoke(buildings, null);
                var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                var currentProp = enumerator.GetType().GetProperty("Current");
                
                while ((bool)moveNextMethod.Invoke(enumerator, null))
                {
                    var building = currentProp.GetValue(enumerator);
                    var snapshot = CreateSnapshot(building);
                    if (snapshot != null)
                    {
                        string key = $"{snapshot.X},{snapshot.Y},{snapshot.Z}";
                        lastKnownBuildings[key] = snapshot;
                    }
                }
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[BuildingChangeDetector] Error snapshotting buildings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check for changes since last snapshot
        /// </summary>
        private void CheckForBuildingChanges()
        {
            try
            {
                var currentBuildings = new Dictionary<string, BuildingSnapshot>();
                
                // Get all current buildings
                var getAllMethod = buildingLayoutModel.GetType().GetMethod("GetAll");
                if (getAllMethod == null)
                    return;
                    
                var buildings = getAllMethod.Invoke(buildingLayoutModel, null);
                if (buildings == null)
                    return;
                    
                // Iterate through buildings
                var enumerableType = buildings.GetType();
                var getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator");
                if (getEnumeratorMethod == null)
                    return;
                    
                var enumerator = getEnumeratorMethod.Invoke(buildings, null);
                var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                var currentProp = enumerator.GetType().GetProperty("Current");
                
                while ((bool)moveNextMethod.Invoke(enumerator, null))
                {
                    var building = currentProp.GetValue(enumerator);
                    var snapshot = CreateSnapshot(building);
                    if (snapshot != null)
                    {
                        string key = $"{snapshot.X},{snapshot.Y},{snapshot.Z}";
                        currentBuildings[key] = snapshot;
                        
                        // Check if this is a new building
                        if (!lastKnownBuildings.ContainsKey(key))
                        {
                            OnBuildingPlaced(snapshot);
                        }
                    }
                }
                
                // Check for removed buildings
                foreach (var kvp in lastKnownBuildings)
                {
                    if (!currentBuildings.ContainsKey(kvp.Key))
                    {
                        OnBuildingRemoved(kvp.Value);
                    }
                }
                
                // Update snapshot
                lastKnownBuildings = currentBuildings;
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[BuildingChangeDetector] Error checking for changes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Create a snapshot of a building
        /// </summary>
        private BuildingSnapshot CreateSnapshot(object building)
        {
            try
            {
                var buildingType = building.GetType();
                
                // Get position
                var positionProp = buildingType.GetProperty("Position");
                if (positionProp == null)
                    return null;
                    
                var position = positionProp.GetValue(building);
                var posType = position.GetType();
                
                var xProp = posType.GetProperty("X");
                var xField = posType.GetField("X");
                var yProp = posType.GetProperty("Y");
                var yField = posType.GetField("Y");
                var zProp = posType.GetProperty("Z");
                var zField = posType.GetField("Z");
                
                float x = xProp != null ? Convert.ToSingle(xProp.GetValue(position)) : (xField != null ? Convert.ToSingle(xField.GetValue(position)) : 0);
                float y = yProp != null ? Convert.ToSingle(yProp.GetValue(position)) : (yField != null ? Convert.ToSingle(yField.GetValue(position)) : 0);
                float z = zProp != null ? Convert.ToSingle(zProp.GetValue(position)) : (zField != null ? Convert.ToSingle(zField.GetValue(position)) : 0);
                
                // Get definition/type
                var definitionProp = buildingType.GetProperty("Definition");
                string buildingTypeName = "Unknown";
                int rotation = 0;
                
                if (definitionProp != null)
                {
                    var definition = definitionProp.GetValue(building);
                    if (definition != null)
                    {
                        var idProp = definition.GetType().GetProperty("Id");
                        if (idProp != null)
                        {
                            var id = idProp.GetValue(definition);
                            if (id != null)
                            {
                                buildingTypeName = id.ToString();
                            }
                        }
                    }
                }
                
                // Get rotation
                var rotationProp = buildingType.GetProperty("Rotation");
                if (rotationProp != null)
                {
                    var rotValue = rotationProp.GetValue(building);
                    if (rotValue != null)
                    {
                        rotation = Convert.ToInt32(rotValue);
                    }
                }
                
                return new BuildingSnapshot
                {
                    BuildingType = buildingTypeName,
                    X = x,
                    Y = y,
                    Z = z,
                    Rotation = rotation
                };
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        
        /// <summary>
        /// Called when a building is placed
        /// </summary>
        private void OnBuildingPlaced(BuildingSnapshot snapshot)
        {
            MultiplayerMod.Log.LogInfo($"[BuildingChangeDetector] Building placed: {snapshot.BuildingType} at ({snapshot.X},{snapshot.Y},{snapshot.Z})");
            
            var packet = new BuildingPlacedPacket
            {
                PlayerId = 0, // Will be set by NetworkManager
                BuildingType = snapshot.BuildingType,
                X = snapshot.X,
                Y = snapshot.Y,
                Z = snapshot.Z,
                Rotation = snapshot.Rotation,
                Variant = 0
            };
            
            if (networkManager.IsServer)
            {
                networkManager.BroadcastToClients(packet);
            }
            else if (networkManager.IsClient)
            {
                networkManager.SendToServer(packet);
            }
        }
        
        /// <summary>
        /// Called when a building is removed
        /// </summary>
        private void OnBuildingRemoved(BuildingSnapshot snapshot)
        {
            MultiplayerMod.Log.LogInfo($"[BuildingChangeDetector] Building removed at ({snapshot.X},{snapshot.Y},{snapshot.Z})");
            
            var packet = new BuildingRemovedPacket
            {
                PlayerId = 0, // Will be set by NetworkManager
                X = snapshot.X,
                Y = snapshot.Y,
                Z = snapshot.Z
            };
            
            if (networkManager.IsServer)
            {
                networkManager.BroadcastToClients(packet);
            }
            else if (networkManager.IsClient)
            {
                networkManager.SendToServer(packet);
            }
        }
        
        /// <summary>
        /// Find a type by name across all assemblies
        /// </summary>
        private Type FindTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.Name == typeName || type.FullName == typeName)
                        {
                            return type;
                        }
                    }
                }
                catch { }
            }
            return null;
        }
        
        private class BuildingSnapshot
        {
            public string BuildingType { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public int Rotation { get; set; }
        }
    }
}

