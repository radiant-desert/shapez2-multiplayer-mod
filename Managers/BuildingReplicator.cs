extern alias GameMap;
extern alias GameMapModel;
extern alias GameMapSim;

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shapez2MultiplayerMod.Models;

using Game.Core;
using Game.Core.Coordinates;
using Game.Core.Simulation;
using Game.Buildings;
using System.Linq;

namespace Shapez2MultiplayerMod.Managers
{
    public class BuildingReplicator
    {
        private MonoBehaviour coroutineHost;

        public BuildingReplicator(MonoBehaviour host)
        {
            coroutineHost = host;
        }

        public void ApplyWorldState(WorldStatePacket packet, Action onComplete)
        {
            coroutineHost.StartCoroutine(ApplyWorldStateRoutine(packet, onComplete));
        }

        public void ApplyModificationPacket(RequestModifyWorldPacket packet)
        {
            coroutineHost.StartCoroutine(ApplyModificationRoutine(packet));
        }

        private IEnumerator ApplyModificationRoutine(RequestModifyWorldPacket packet)
        {
            MultiplayerMod.Log.LogInfo($"[BuildingReplicator] Applying modification packet. Removing {packet.IslandPositionsToRemove.Count + packet.PositionsToRemove.Count}, Placing {packet.IslandsToPlace.Count} islands and {packet.BuildingsToPlace.Count} buildings.");
            
            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore == null) yield break;
            var mapModel = gameCore.LocalPlayer.CurrentMap as GameMapModel.MapModel;
            if (mapModel == null) yield break;

            var buildingManager = gameCore.Mode.Buildings;
            var islandManager = gameCore.Mode.Islands;

            // PHASE 1: DEMOLITION
            var demolitionScope = mapModel.StartBunchEdit();
            if (packet.IslandPositionsToRemove.Any())
            {
                foreach (var posData in packet.IslandPositionsToRemove)
                {
                    var coord = new GlobalChunkCoordinate((int)posData.X, (int)posData.Y, (short)posData.Z);
                    if(mapModel.TryGetIsland(coord, out var islandModel)) { mapModel.DeleteIsland(islandModel.Id); }
                }
            }
            if (packet.PositionsToRemove.Any())
            {
                foreach (var posData in packet.PositionsToRemove)
                {
                    var coord = new GlobalTileCoordinate((int)posData.X, (int)posData.Y, (short)posData.Z);
                    if (mapModel.TryGetBuilding(coord, out var buildingModel)) { mapModel.DeleteBuilding(buildingModel.Id); }
                }
            }
            mapModel.FinishBunchEdit(demolitionScope);
            
            yield return null;

            // PHASE 2: FOUNDATION (Islands)
            var foundationScope = mapModel.StartBunchEdit();
            if (packet.IslandsToPlace.Any())
            {
                foreach (var islandData in packet.IslandsToPlace)
                {
                    var defId = new GameMapSim.IslandDefinitionId(islandData.IslandType);
                    if (islandManager.TryGetDefinition(defId, out var definition))
                    {
                        var pos = new GlobalChunkCoordinate(islandData.X, islandData.Y, 0);
                        var rot = new GridRotation((GridRotation.Serializable)islandData.Rotation);
                        var trans = new GlobalChunkTransform(pos, rot);
                        mapModel.CreateIsland(definition, trans, null);
                    }
                }
            }
            mapModel.FinishBunchEdit(foundationScope);

            // PHASE 3: ACTIVE WAITING
            if (packet.IslandsToPlace.Any())
            {
                MultiplayerMod.Log.LogInfo("[BuildingReplicator] Waiting for islands to be created...");
                int timeout = 0;
                bool allIslandsReady = false;
                while (!allIslandsReady && timeout < 60)
                {
                    yield return null;
                    timeout++;
                    allIslandsReady = true;
                    foreach (var islandData in packet.IslandsToPlace)
                    {
                        var coord = new GlobalChunkCoordinate(islandData.X, islandData.Y, 0);
                        if (!mapModel.TryGetIsland(coord, out _))
                        {
                            allIslandsReady = false;
                            break;
                        }
                    }
                }

                if (!allIslandsReady)
                {
                    MultiplayerMod.Log.LogError("[BuildingReplicator] Timed out waiting for islands to be created! Aborting building placement.");
                    yield break;
                }
                MultiplayerMod.Log.LogInfo($"[BuildingReplicator] All islands confirmed after {timeout} frames.");
            }
            
            // PHASE 4: CONSTRUCTION (Buildings)
            var constructionScope = mapModel.StartBunchEdit();
            if (packet.BuildingsToPlace.Any())
            {
                foreach (var buildingData in packet.BuildingsToPlace)
                {
                    var defId = new GameMapSim.BuildingDefinitionId(buildingData.BuildingType);
                    if (buildingManager.TryGetDefinition(defId, out var definition))
                    {
                        var pos = new GlobalTileCoordinate((int)buildingData.X, (int)buildingData.Y, (short)buildingData.Z);
                        var rot = new GridRotation((GridRotation.Serializable)buildingData.Rotation);
                        var trans = new GlobalTileTransform(pos, rot);
                        mapModel.CreateBuilding(definition, trans, null);
                    }
                }
            }
            mapModel.FinishBunchEdit(constructionScope);
            
            MultiplayerMod.Log.LogInfo("[BuildingReplicator] Modification packet applied successfully.");
        }

        private IEnumerator ApplyWorldStateRoutine(WorldStatePacket packet, Action onComplete)
        {
            while (Singleton<StaticGameCoreAccessor>.G == null || !Singleton<StaticGameCoreAccessor>.G.Initialized)
            {
                yield return new WaitForSeconds(0.2f);
            }

            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            var gameMapModel = gameCore.LocalPlayer.CurrentMap as GameMapModel.MapModel;
            var buildingManager = gameCore.Mode.Buildings;
            var islandManager = gameCore.Mode.Islands;

            if (gameMapModel == null || buildingManager == null || islandManager == null) yield break;

            MultiplayerMod.Log.LogInfo("[BuildingReplicator] Phase 1: Clearing client's map...");
            var demolitionScope = gameMapModel.StartBunchEdit();
            
            var buildingsToDestroy = new List<GameMap.BuildingId>();
            foreach (var b in gameMapModel.Buildings) { buildingsToDestroy.Add(b.Id); }
            foreach (var id in buildingsToDestroy) { gameMapModel.DeleteBuilding(id); }
            
            var hubDefinitionId = gameCore.Mode.Islands.Hub.Id;
            var islandsToDestroy = new List<GameMap.IslandId>();
            foreach (var i in gameMapModel.Islands) 
            {
                if (i.Definition.Id != hubDefinitionId)
                {
                    islandsToDestroy.Add(i.Id);
                }
            }
            foreach (var id in islandsToDestroy) { gameMapModel.DeleteIsland(id); }
            
            gameMapModel.FinishBunchEdit(demolitionScope);
            MultiplayerMod.Log.LogInfo($"[BuildingReplicator] Cleared {buildingsToDestroy.Count} buildings and {islandsToDestroy.Count} non-hub islands.");

            yield return null;

            var islandScope = gameMapModel.StartBunchEdit();
            foreach (var islandData in packet.Islands)
            {
                try
                {
                    if (islandData.IslandType == hubDefinitionId.ToString()) continue;
                    
                    var defId = new GameMapSim.IslandDefinitionId(islandData.IslandType);
                    if(islandManager.TryGetDefinition(defId, out var definition))
                    {
                        var pos = new GlobalChunkCoordinate(islandData.X, islandData.Y, 0);
                        var rot = new GridRotation((GridRotation.Serializable)islandData.Rotation);
                        var trans = new GlobalChunkTransform(pos, rot);
                        gameMapModel.CreateIsland(definition, trans, null);
                    }
                }
                catch (Exception ex) { MultiplayerMod.Log.LogError($"Failed to create island '{islandData.IslandType}': {ex.Message}"); }
            }
            gameMapModel.FinishBunchEdit(islandScope);

            if (packet.Islands.Any())
            {
                MultiplayerMod.Log.LogInfo("[BuildingReplicator] Waiting for initial islands to be created...");
                int timeout = 0;
                bool allIslandsReady = false;
                while (!allIslandsReady && timeout < 60)
                {
                    yield return null;
                    timeout++;
                    allIslandsReady = true;
                    foreach (var islandData in packet.Islands)
                    {
                        if (islandData.IslandType == hubDefinitionId.ToString()) continue;
                        var coord = new GlobalChunkCoordinate(islandData.X, islandData.Y, 0);
                        if (!gameMapModel.TryGetIsland(coord, out _))
                        {
                            allIslandsReady = false;
                            break;
                        }
                    }
                }
                if (!allIslandsReady)
                {
                     MultiplayerMod.Log.LogError("[BuildingReplicator] Timed out waiting for initial islands!");
                     yield break;
                }
            }

            int buildingsPlaced = 0;
            int buildingsFailed = 0;
            var buildingScope = gameMapModel.StartBunchEdit();
            foreach (var buildingData in packet.Buildings)
            {
                try
                {
                    var defId = new GameMapSim.BuildingDefinitionId(buildingData.BuildingType);
                    if (buildingManager.TryGetDefinition(defId, out GameMapSim.IBuildingDefinition definition))
                    {
                        var pos = new GlobalTileCoordinate((int)buildingData.X, (int)buildingData.Y, (short)buildingData.Z);
                        var rot = new GridRotation((GridRotation.Serializable)buildingData.Rotation);
                        var trans = new GlobalTileTransform(pos, rot);
                        gameMapModel.CreateBuilding(definition, trans, null);
                        buildingsPlaced++;
                    }
                    else { buildingsFailed++; }
                }
                catch (Exception)
                {
                    buildingsFailed++;
                }
            }
            gameMapModel.FinishBunchEdit(buildingScope);

            MultiplayerMod.Log.LogInfo($"[BuildingReplicator] World replication complete! Placed: {buildingsPlaced}, Failed: {buildingsFailed}.");

            onComplete?.Invoke();
        }
    }
}