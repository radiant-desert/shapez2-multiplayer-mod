using System.Collections.Generic;
using Shapez2MultiplayerMod.Models;
using Shapez2MultiplayerMod.Networking;

namespace Shapez2MultiplayerMod.Managers
{
    // The history object now includes islands
    public class UndoableAction
    {
        public List<BuildingData> PlacedBuildings { get; } = new List<BuildingData>();
        public List<BuildingData> RemovedBuildings { get; } = new List<BuildingData>();
        public List<IslandData> PlacedIslands { get; } = new List<IslandData>();
        public List<IslandData> RemovedIslands { get; } = new List<IslandData>();
    }

    /// <summary>
    /// Manages a client-side "shadow" undo/redo stack.
    /// </summary>
    public class ClientActionManager
    {
        private Stack<UndoableAction> undoStack = new Stack<UndoableAction>();
        private Stack<UndoableAction> redoStack = new Stack<UndoableAction>();
        private NetworkManager networkManager;

        public ClientActionManager(NetworkManager netManager)
        {
            networkManager = netManager;
        }

        public bool CanUndo() => undoStack.Count > 0;
        public bool CanRedo() => redoStack.Count > 0;

        public void RecordAction(UndoableAction action)
        {
            undoStack.Push(action);
            redoStack.Clear();
            MultiplayerMod.Log.LogInfo($"[ClientActionManager] Action recorded. Undo stack: {undoStack.Count}, Redo stack: {redoStack.Count}");
        }

        public void PerformUndo()
        {
            if (!CanUndo()) return;

            var actionToUndo = undoStack.Pop();
            redoStack.Push(actionToUndo);

            MultiplayerMod.Log.LogInfo("[ClientActionManager] Performing UNDO. Sending reversed action to server.");
            
            // Use the correct, unified packet
            var reverseRequest = new RequestModifyWorldPacket();

            // Re-place what was removed
            reverseRequest.BuildingsToPlace.AddRange(actionToUndo.RemovedBuildings);
            reverseRequest.IslandsToPlace.AddRange(actionToUndo.RemovedIslands);

            // Remove what was placed
            foreach (var building in actionToUndo.PlacedBuildings)
            {
                reverseRequest.PositionsToRemove.Add(new SerializableVector3 { X = building.X, Y = building.Y, Z = building.Z });
            }
            foreach (var island in actionToUndo.PlacedIslands)
            {
                reverseRequest.IslandPositionsToRemove.Add(new SerializableVector3 { X = island.X, Y = island.Y, Z = 0 });
            }

            networkManager.SendToServer(reverseRequest);
        }

        public void PerformRedo()
        {
            if (!CanRedo()) return;

            var actionToRedo = redoStack.Pop();
            undoStack.Push(actionToRedo);
            
            MultiplayerMod.Log.LogInfo("[ClientActionManager] Performing REDO. Sending original action to server.");

            // Use the correct, unified packet
            var originalRequest = new RequestModifyWorldPacket();

            // Re-apply the original placements
            originalRequest.BuildingsToPlace.AddRange(actionToRedo.PlacedBuildings);
            originalRequest.IslandsToPlace.AddRange(actionToRedo.PlacedIslands);

            // Re-apply the original removals
            foreach (var building in actionToRedo.RemovedBuildings)
            {
                originalRequest.PositionsToRemove.Add(new SerializableVector3 { X = building.X, Y = building.Y, Z = building.Z });
            }
            foreach (var island in actionToRedo.RemovedIslands)
            {
                originalRequest.IslandPositionsToRemove.Add(new SerializableVector3 { X = island.X, Y = island.Y, Z = 0 });
            }

            networkManager.SendToServer(originalRequest);
        }
    }
}