using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod.Networking;

namespace Shapez2MultiplayerMod.Managers
{
    /// <summary>
    /// Detects building changes by monitoring islands
    /// Shapez 2 uses islands, not a single map
    /// </summary>
    public class BuildingChangeDetectorV2
    {
        private NetworkManager networkManager;
        private object islandManager = null;
        private Dictionary<string, BuildingSnapshot> lastKnownBuildings = new Dictionary<string, BuildingSnapshot>();
        private bool isInitialized = false;
        private float updateInterval = 0.5f; // Check 2 times per second
        private float lastUpdateTime = 0f;
        
        public BuildingChangeDetectorV2(NetworkManager netManager)
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
        /// Try to find islands and building data
        /// </summary>
        private void TryInitialize()
        {
            try
            {
                //MultiplayerMod.Log.LogInfo("[BuildingDetectorV2] Searching for island types...");
                
                // Find IslandInstance type
                var islandInstanceType = FindTypeByName("IslandInstance");
                if (islandInstanceType == null)
                {
                    //MultiplayerMod.Log.LogWarning("[BuildingDetectorV2] IslandInstance type not found");
                    return;
                }
                
                //MultiplayerMod.Log.LogInfo($"[BuildingDetectorV2] Found IslandInstance type: {islandInstanceType.FullName}");
                //MultiplayerMod.Log.LogInfo($"[BuildingDetectorV2] IslandInstance is UnityEngine.Object: {typeof(UnityEngine.Object).IsAssignableFrom(islandInstanceType)}");
                
                // Try to find all island instances in the scene
                if (typeof(UnityEngine.Object).IsAssignableFrom(islandInstanceType))
                {
                    //MultiplayerMod.Log.LogInfo("[BuildingDetectorV2] Searching for island instances using FindObjectsOfType...");
                    var findAllMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new Type[] { })
                        .MakeGenericMethod(islandInstanceType);
                    var islands = findAllMethod.Invoke(null, null) as Array;
                    
                    if (islands != null && islands.Length > 0)
                    {
                        //MultiplayerMod.Log.LogInfo($"[BuildingDetectorV2] Found {islands.Length} islands!");
                        isInitialized = true;
                        SnapshotCurrentBuildings();
                        return;
                    }
                    else
                    {
                        //MultiplayerMod.Log.LogWarning("[BuildingDetectorV2] No islands found in scene yet - will retry");
                    }
                }
                else
                {
                    //MultiplayerMod.Log.LogWarning("[BuildingDetectorV2] IslandInstance is not a UnityEngine.Object - trying alternative approach");
                    
                    // Try to find through GameCore
                    var gameCoreType = FindTypeByName("GameCore");
                    if (gameCoreType != null && typeof(UnityEngine.Object).IsAssignableFrom(gameCoreType))
                    {
                        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[] { })
                            .MakeGenericMethod(gameCoreType);
                        var gameCore = findMethod.Invoke(null, null);
                        
                        if (gameCore != null)
                        {
                            //MultiplayerMod.Log.LogInfo("[BuildingDetectorV2] Found GameCore, searching for islands...");
                            
                            // Look for properties that might contain islands
                            var props = gameCoreType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            //MultiplayerMod.Log.LogInfo($"[BuildingDetectorV2] Checking {props.Length} GameCore properties for islands...");
                            
                            foreach (var prop in props)
                            {
                                try
                                {
                                    var value = prop.GetValue(gameCore);
                                    if (value != null)
                                    {
                                        // Check if this property has an Islands or GetIslands method
                                        var islandsProp = value.GetType().GetProperty("Islands");
                                        var getIslandsMethod = value.GetType().GetMethod("GetIslands");
                                        var getAllIslandsMethod = value.GetType().GetMethod("GetAllIslands");
                                        
                                        if (islandsProp != null)
                                        {
                                            //MultiplayerMod.Log.LogInfo($"[BuildingDetectorV2] Found Islands property in GameCore.{prop.Name}");
                                            islandManager = value;
                                            isInitialized = true;
                                            SnapshotCurrentBuildings();
                                            return;
                                        }
                                        
                                        if (getIslandsMethod != null)
                                        {
                                            //MultiplayerMod.Log.LogInfo($"[BuildingDetectorV2] Found GetIslands method in GameCore.{prop.Name}");
                                            islandManager = value;
                                            isInitialized = true;
                                            SnapshotCurrentBuildings();
                                            return;
                                        }
                                        
                                        if (getAllIslandsMethod != null)
                                        {
                                            //MultiplayerMod.Log.LogInfo($"[BuildingDetectorV2] Found GetAllIslands method in GameCore.{prop.Name}");
                                            islandManager = value;
                                            isInitialized = true;
                                            SnapshotCurrentBuildings();
                                            return;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MultiplayerMod.Log.LogWarning($"[BuildingDetectorV2] Error checking property {prop.Name}: {ex.Message}");
                                }
                            }
                            
                            //MultiplayerMod.Log.LogWarning("[BuildingDetectorV2] No islands container found in GameCore properties");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[BuildingDetectorV2] Error during initialization: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Take a snapshot of all current buildings across all islands
        /// </summary>
        private void SnapshotCurrentBuildings()
        {
            try
            {
                lastKnownBuildings.Clear();
                
                var islandInstanceType = FindTypeByName("IslandInstance");
                if (islandInstanceType == null) return;
                
                var findAllMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new Type[] { })
                    .MakeGenericMethod(islandInstanceType);
                var islands = findAllMethod.Invoke(null, null) as Array;
                
                if (islands == null) return;
                
                int totalBuildings = 0;
                foreach (var island in islands)
                {
                    // Get buildings from this island
                    var buildingsProp = island.GetType().GetProperty("Buildings");
                    if (buildingsProp != null)
                    {
                        var buildings = buildingsProp.GetValue(island);
                        if (buildings != null)
                        {
                            // Try to enumerate buildings
                            var getEnumeratorMethod = buildings.GetType().GetMethod("GetEnumerator");
                            if (getEnumeratorMethod != null)
                            {
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
                                        totalBuildings++;
                                    }
                                }
                            }
                        }
                    }
                }
                
                MultiplayerMod.Log.LogInfo($"[BuildingDetectorV2] Snapshot complete: {totalBuildings} buildings found");
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[BuildingDetectorV2] Error snapshotting buildings: {ex.Message}");
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
                
                var islandInstanceType = FindTypeByName("IslandInstance");
                if (islandInstanceType == null) return;
                
                var findAllMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new Type[] { })
                    .MakeGenericMethod(islandInstanceType);
                var islands = findAllMethod.Invoke(null, null) as Array;
                
                if (islands == null) return;
                
                foreach (var island in islands)
                {
                    var buildingsProp = island.GetType().GetProperty("Buildings");
                    if (buildingsProp != null)
                    {
                        var buildings = buildingsProp.GetValue(island);
                        if (buildings != null)
                        {
                            var getEnumeratorMethod = buildings.GetType().GetMethod("GetEnumerator");
                            if (getEnumeratorMethod != null)
                            {
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
                            }
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
                MultiplayerMod.Log.LogError($"[BuildingDetectorV2] Error checking for changes: {ex.Message}");
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
            MultiplayerMod.Log.LogInfo($"[BuildingDetectorV2] Building placed: {snapshot.BuildingType} at ({snapshot.X},{snapshot.Y},{snapshot.Z})");
            
            var packet = new BuildingPlacedPacket
            {
                PlayerId = 0,
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
            MultiplayerMod.Log.LogInfo($"[BuildingDetectorV2] Building removed at ({snapshot.X},{snapshot.Y},{snapshot.Z})");
            
            var packet = new BuildingRemovedPacket
            {
                PlayerId = 0,
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

