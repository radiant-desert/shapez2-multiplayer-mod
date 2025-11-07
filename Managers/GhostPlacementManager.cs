extern alias GameMapSim;

using Shapez2MultiplayerMod.Models;
using System.Collections.Generic;
using System.Linq; // <-- Make sure to add this

// Add aliases to access game types

using Game.Core.Coordinates; // For GlobalTileTransform

namespace Shapez2MultiplayerMod.Managers
{
    public class GhostPlacementManager
    {
        // We now store the exact data the game's renderer needs
        private readonly Dictionary<int, List<SmartBuildingBlueprintRenderer.DrawData>> remotePlayerDrawData = 
            new Dictionary<int, List<SmartBuildingBlueprintRenderer.DrawData>>();

        // NO MORE MATERIALS OR INITIALIZE METHOD NEEDED!

        public void UpdateGhostsForPlayer(PlayerGhostPlacementPacket packet)
        {
            int playerId = packet.PlayerId;
            ClearGhosts(playerId); // Clears the old data for this player

            if (!remotePlayerDrawData.ContainsKey(playerId))
            {
                remotePlayerDrawData[playerId] = new List<SmartBuildingBlueprintRenderer.DrawData>();
            }

            // Get the game's building manager to convert string IDs back to definitions
            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore == null) return;
            var buildingManager = gameCore.Mode.Buildings;

            foreach (var ghostData in packet.GhostBuildings)
            {
                var defId = new GameMapSim::BuildingDefinitionId(ghostData.BuildingType);
                if (buildingManager.TryGetDefinition(defId, out GameMapSim::IBuildingDefinition definition))
                {
                    // Reconstruct the same data structures the game uses
                    var coord = new GlobalTileCoordinate((int)ghostData.X, (int)ghostData.Y, (short)ghostData.Z);
                    var rotation = new GridRotation((GridRotation.Serializable)ghostData.Rotation);
                    var transform = new GlobalTileTransform(coord, rotation);

                    // Convert our boolean 'IsValid' back to the game's enum
                    var allowability = ghostData.IsValid ? PlacementAllowability.ValidPlacement : PlacementAllowability.InvalidPlacement;

                    // Create the DrawData object and add it to our list for this player
                    var drawData = new SmartBuildingBlueprintRenderer.DrawData(definition, ref transform, allowability);
                    remotePlayerDrawData[playerId].Add(drawData);
                }
            }
        }

        // A new method to get all ghost data ready for rendering
        public List<SmartBuildingBlueprintRenderer.DrawData> GetAllDrawData()
        {
            // Flatten the dictionary of lists into a single list
            return remotePlayerDrawData.Values.SelectMany(list => list).ToList();
        }

        public void ClearGhosts(int playerId)
        {
            if (remotePlayerDrawData.ContainsKey(playerId))
            {
                remotePlayerDrawData[playerId].Clear();
            }
        }

        public void ClearAllGhosts()
        {
            remotePlayerDrawData.Clear();
        }

        public void Shutdown()
        {
            ClearAllGhosts();
        }
    }
}