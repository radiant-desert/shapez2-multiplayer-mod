extern alias GameMapModel;
extern alias GameMap;
extern alias GameMapSim;

using HarmonyLib;
using Shapez2MultiplayerMod.Models;
using Game.Core;
using Game.Core.Coordinates;
using Shapez2MultiplayerMod.Managers; // Required to access UndoableAction

namespace Shapez2MultiplayerMod.Patches
{
    [HarmonyPatch]
    public class WorldModificationHook
    {
        public static RequestModifyWorldPacket pendingModifications = new RequestModifyWorldPacket();
        public static bool hasPendingModifications = false;
        
        // Accumulator for the undo action, which will be recorded when the packet is sent.
        private static UndoableAction pendingUndoAction = new UndoableAction();
        
        [HarmonyPatch(typeof(ActionModifyBuildings), "ExecuteInternal")]
        [HarmonyPrefix]
        public static bool OnBuildingsModified_Prefix(ActionModifyBuildings __instance)
        {
            var networkManager = MultiplayerMod.Instance.NetworkManager;
            if (networkManager == null || !networkManager.IsConnected) return true;

            var map = Singleton<StaticGameCoreAccessor>.G?.LocalPlayer?.CurrentMap;
            if (map == null) return false;

            // Accumulate all building changes for this action
            foreach (var deleted in __instance.Data.Delete)
            {
                if (map.TryGetBuilding(deleted.BuildingId, out var building))
                {
                    pendingModifications.PositionsToRemove.Add(new SerializableVector3 { X = building.Tile_G.x, Y = building.Tile_G.y, Z = building.Tile_G.z });
                    
                    // Also record it for the undo action
                    pendingUndoAction.RemovedBuildings.Add(new BuildingData {
                        BuildingType = building.Definition.Id.ToString(),
                        X = building.Tile_G.x, Y = building.Tile_G.y, Z = building.Tile_G.z,
                        Rotation = (int)building.Rotation_G.Value
                    });
                    hasPendingModifications = true;
                }
            }
            foreach (var placed in __instance.Data.Place)
            {
                if (map.TryGetIsland(placed.IslandId, out var island))
                {
                    GlobalTileTransform globalTransform = placed.Transform_I.To_G(island.Position);
                    var buildingData = new BuildingData {
                        BuildingType = placed.Definition.Id.ToString(),
                        X = globalTransform.Position.x, Y = globalTransform.Position.y, Z = globalTransform.Position.z,
                        Rotation = (int)globalTransform.Rotation.Value
                    };
                    pendingModifications.BuildingsToPlace.Add(buildingData);
                    pendingUndoAction.PlacedBuildings.Add(buildingData); // Record for undo
                    hasPendingModifications = true;
                }
            }

            if (networkManager.IsClient) return false;
            return true;
        }

        [HarmonyPatch(typeof(ActionModifyIsland), "ExecuteInternal")]
        [HarmonyPrefix]
        public static bool OnIslandsModified_Prefix(ActionModifyIsland __instance)
        {
            var networkManager = MultiplayerMod.Instance.NetworkManager;
            if (networkManager == null || !networkManager.IsConnected) return true;
            
            var map = Singleton<StaticGameCoreAccessor>.G?.LocalPlayer?.CurrentMap;
            if (map == null) return false;

            // Handle island deletion (this part is unchanged and correct)
            foreach (var deleted in __instance.Data.Delete)
            {
                if (map.TryGetIsland(deleted.IslandId, out var island))
                {
                    pendingModifications.IslandPositionsToRemove.Add(new SerializableVector3 { X = island.Position.x, Y = island.Position.y, Z = island.Position.z });
                    pendingUndoAction.RemovedIslands.Add(new IslandData {
                        IslandType = island.Definition.Id.ToString(),
                        X = island.Position.x, Y = island.Position.y, Z = island.Position.z,
                        Rotation = (int)island.Rotation.Value
                    });
                    hasPendingModifications = true;
                }
            }

            // Handle island placement
            foreach (var placed in __instance.Data.Place)
            {
                // 1. Add the island itself (this part is unchanged and correct)
                var islandData = new IslandData {
                    IslandType = placed.Definition.Id.ToString(),
                    X = placed.Origin_GC.x, Y = placed.Origin_GC.y, Z = placed.Origin_GC.z,
                    Rotation = (int)placed.Rotation.Value
                };
                pendingModifications.IslandsToPlace.Add(islandData);
                pendingUndoAction.PlacedIslands.Add(islandData);
                hasPendingModifications = true;

                // --- CORRECTED FIX START ---
                // 2. Check if this island placement includes buildings by checking the 'PlaceBuildings' collection.
                if (placed.PlaceBuildings != null && placed.PlaceBuildings.Count > 0)
                {
                    MultiplayerMod.Log.LogInfo($"Detected {placed.PlaceBuildings.Count} buildings on placed island.");
                    var islandOrigin = placed.Origin_GC;

                    // 3. Loop through all 'PlaceBuildingPayload' objects in the collection
                    foreach (var buildingPayload in placed.PlaceBuildings)
                    {
                        // The building's transform is local to the island ('Transform_I'). We must convert it to a global world position.
                        var globalTransform = buildingPayload.Transform_I.To_G(islandOrigin);

                        var buildingData = new BuildingData
                        {
                            BuildingType = buildingPayload.Definition.Id.ToString(),
                            X = globalTransform.Position.x,
                            Y = globalTransform.Position.y,
                            Z = globalTransform.Position.z,
                            Rotation = (int)globalTransform.Rotation.Value
                        };
                        
                        pendingModifications.BuildingsToPlace.Add(buildingData);
                        pendingUndoAction.PlacedBuildings.Add(buildingData); // Also add to undo action
                    }
                }
                // --- CORRECTED FIX END ---
            }

            if (networkManager.IsClient) return false;
            return true;
        }

        // This will be called from LateUpdate right after sending the packet.
        public static void RecordAndReset()
        {
            // Record the accumulated undo action
            if (MultiplayerMod.Instance.NetworkManager.IsClient)
            {
                MultiplayerMod.Instance.ClientActionManager.RecordAction(pendingUndoAction);
            }

            // Reset both accumulators for the next frame
            pendingModifications = new RequestModifyWorldPacket();
            pendingUndoAction = new UndoableAction();
            hasPendingModifications = false;
        }
    }
}