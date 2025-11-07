using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod.Networking;

namespace Shapez2MultiplayerMod.Managers
{
    /// <summary>
    /// This class is now obsolete and only provides placeholder methods
    /// for the WorldModificationHook. All logic has been moved to NetworkManager.
    /// </summary>
    public class BuildingSyncManager
    {
        private readonly NetworkManager networkManager;

        public BuildingSyncManager(NetworkManager netManager)
        {
            networkManager = netManager;
        }
        
        // These methods do nothing now. The hook will be updated to handle this logic.
        public void Initialize() { }
        public void Shutdown() { }
        public void BroadcastBuildingPlaced(string buildingType, float x, float y, float z, int rotation, int variant, int playerId) { }
        public void BroadcastBuildingRemoved(float x, float y, float z, int playerId) { }
        public void BroadcastIslandPlaced(string islandType, int x, int y, int z, int rotation, int playerId) { }
        public void BroadcastIslandRemoved(int x, int y, int z, int playerId) { }
    }
}