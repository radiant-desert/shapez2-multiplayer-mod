extern alias GameMapSim;

using Shapez2MultiplayerMod.Models;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Coordinates;
using Game.Placement.Utils; // Required for IslandPreviewRenderData

namespace Shapez2MultiplayerMod.Managers
{
    // THIS CLASS IS A 1:1 REPLICATION OF GhostPlacementManager
    public class IslandPlacementManager
    {
        // We store the exact data the game's island renderer needs
        private readonly Dictionary<int, List<IslandPreviewRenderData>> remotePlayerRenderData =
            new Dictionary<int, List<IslandPreviewRenderData>>();

        public void UpdateGhostsForPlayer(PlayerIslandGhostPlacementPacket packet)
        {
            int playerId = packet.PlayerId;
            ClearGhosts(playerId);

            if (!remotePlayerRenderData.ContainsKey(playerId))
            {
                remotePlayerRenderData[playerId] = new List<IslandPreviewRenderData>();
            }

            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore?.Mode?.Islands == null) return;
            var islandManager = gameCore.Mode.Islands;

            foreach (var ghostData in packet.IslandGhosts)
            {
                var defId = new GameMapSim::IslandDefinitionId(ghostData.IslandType);
                if (islandManager.TryGetDefinition(defId, out var definition))
                {
                    // Reconstruct the same data structures the game uses
                    var coord = new GlobalChunkCoordinate((int)ghostData.X, (int)ghostData.Y, (short)ghostData.Z);
                    var rotation = new GridRotation((GridRotation.Serializable)ghostData.Rotation);
                    var transform = new GlobalChunkTransform(coord, rotation);
                    var allowability = ghostData.IsValid ? PlacementAllowability.ValidPlacement : PlacementAllowability.InvalidPlacement;
                    
                    // Create the RenderData object (equivalent to DrawData for buildings)
                    var renderData = new IslandPreviewRenderData(definition, transform, null, allowability);
                    remotePlayerRenderData[playerId].Add(renderData);
                }
            }
        }

        public List<IslandPreviewRenderData> GetAllRenderData()
        {
            return remotePlayerRenderData.Values.SelectMany(list => list).ToList();
        }

        public void ClearGhosts(int playerId)
        {
            if (remotePlayerRenderData.ContainsKey(playerId))
            {
                remotePlayerRenderData[playerId].Clear();
            }
        }

        public void ClearAllGhosts()
        {
            remotePlayerRenderData.Clear();
        }

        public void Shutdown()
        {
            ClearAllGhosts();
        }
    }
}