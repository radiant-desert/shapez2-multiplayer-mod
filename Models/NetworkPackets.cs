using System;
using System.Collections.Generic;

namespace Shapez2MultiplayerMod.Models
{
    /// <summary>
    /// Base class for all network packets
    /// </summary>
    [Serializable]
    public abstract class NetworkPacket
    {
        public PacketType Type { get; set; }
        public long Timestamp { get; set; }
        
        protected NetworkPacket(PacketType type)
        {
            Type = type;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Packet types for network communication
    /// </summary>
    public enum PacketType
    {
        // Connection packets
        ConnectionRequest = 0,
        ConnectionAccepted = 1,
        ConnectionRejected = 2,
        Disconnect = 3,

        // Player packets
        PlayerJoined = 10,
        PlayerLeft = 11,
        PlayerPosition = 12,
        PlayerAction = 13,

        // Building packets
        BuildingPlaced = 20,
        BuildingRemoved = 21,
        BuildingUpgraded = 22,
        BuildingRotated = 23,

        // Sync packets
        FullStateSync = 30,
        DeltaSync = 31,
        WorldState = 32,

        // Chat packets
        ChatMessage = 40,

        // Heartbeat
        Ping = 50,
        Pong = 51,

        // Savegame packets
        SavegameRequest = 60,
        SavegameData = 61,
        SavegameComplete = 62,

        // World Modification packets
        RequestModifyWorld = 70,
        IslandPlaced = 71,
        IslandRemoved = 72,

        // Progression Sync packets
        GameState = 80,
        RequestUnlockUpgrade = 81, 
        ConfirmUnlockUpgrade = 82, 
        RequestLevelUpUpgrade = 83, 
        ConfirmLevelUpUpgrade = 84, 
        UpdateCurrency = 85,
        RequestTogglePause = 90,
        ConfirmPauseState = 91,
        PlayerGhostPlacement = 100,
        PlayerIslandGhostPlacement = 101,
        RequestWorldReload = 102
    }

    // ==================== Connection Packets ====================

    [Serializable]
    public class ConnectionRequestPacket : NetworkPacket
    {
        public string PlayerName { get; set; }
        public string ModVersion { get; set; }

        public ConnectionRequestPacket() : base(PacketType.ConnectionRequest) { }
    }

    [Serializable]
    public class RequestWorldReloadPacket : NetworkPacket
    {
        public WorldStatePacket WorldState { get; set; }
        public RequestWorldReloadPacket() : base(PacketType.RequestWorldReload) { }
    }

    [Serializable]
    public class GhostBuildingData
    {
        public string BuildingType { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int Rotation { get; set; }
        public bool IsValid { get; set; }
    }

    [Serializable]
    public class GhostIslandData
    {
        public string IslandType { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int Rotation { get; set; }
        public bool IsValid { get; set; }
    }

    [Serializable]
    public class PlayerIslandGhostPlacementPacket : NetworkPacket
    {
        public int PlayerId { get; set; }
        public List<GhostIslandData> IslandGhosts { get; set; }

        public PlayerIslandGhostPlacementPacket() : base(PacketType.PlayerIslandGhostPlacement) 
        {
            IslandGhosts = new List<GhostIslandData>();
        }
    }

    [Serializable]
    public class PlayerGhostPlacementPacket : NetworkPacket
    {
        public int PlayerId { get; set; }
        public List<GhostBuildingData> GhostBuildings { get; set; }
        public PlayerGhostPlacementPacket() : base(PacketType.PlayerGhostPlacement) 
        {
            GhostBuildings = new List<GhostBuildingData>();
        }
    }

    [Serializable]
    public class RequestTogglePausePacket : NetworkPacket
    {
        public RequestTogglePausePacket() : base(PacketType.RequestTogglePause) { }
    }

    [Serializable]
    public class ConfirmPauseStatePacket : NetworkPacket
    {
        public bool IsPaused { get; set; }
        public ConfirmPauseStatePacket() : base(PacketType.ConfirmPauseState) { }
    }

    [Serializable]
    public class UpdateCurrencyPacket : NetworkPacket
    {
        public int NewResearchPoints { get; set; }
        public UpdateCurrencyPacket() : base(PacketType.UpdateCurrency) { }
    }

    [Serializable]
    public class RequestLevelUpUpgradePacket : NetworkPacket
    {
        public string UpgradeId { get; set; }
        public RequestLevelUpUpgradePacket() : base(PacketType.RequestLevelUpUpgrade) { }
    }

    [Serializable]
    public class ConfirmLevelUpUpgradePacket : NetworkPacket
    {
    public string UpgradeId { get; set; }
    public int NewLevel { get; set; }
    public ConfirmLevelUpUpgradePacket() : base(PacketType.ConfirmLevelUpUpgrade) { }
    }

    [Serializable]
    public class GameStatePacket : NetworkPacket
    {
        public int ResearchPoints { get; set; }
        public List<string> UnlockedMilestoneIds { get; set; }
        public Dictionary<string, int> LinearUpgradeLevels { get; set; }

        public GameStatePacket() : base(PacketType.GameState) 
        {
            UnlockedMilestoneIds = new List<string>();
            LinearUpgradeLevels = new Dictionary<string, int>();
        }
    }
    
    [Serializable]
    public class ConnectionAcceptedPacket : NetworkPacket
    {
        public int AssignedPlayerId { get; set; }
        public List<PlayerInfo> ConnectedPlayers { get; set; }
        
        public ConnectionAcceptedPacket() : base(PacketType.ConnectionAccepted) 
        {
            ConnectedPlayers = new List<PlayerInfo>();
        }
    }

    [Serializable]
    public class ConnectionRejectedPacket : NetworkPacket
    {
        public string Reason { get; set; }
        
        public ConnectionRejectedPacket() : base(PacketType.ConnectionRejected) { }
    }

    [Serializable]
    public class DisconnectPacket : NetworkPacket
    {
        public int PlayerId { get; set; }
        public string Reason { get; set; }
        
        public DisconnectPacket() : base(PacketType.Disconnect) { }
    }

    // ==================== Player Packets ====================
    
    [Serializable]
    public class PlayerJoinedPacket : NetworkPacket
    {
        public PlayerInfo Player { get; set; }
        public List<PlayerInfo> ExistingPlayers { get; set; }
        
        public PlayerJoinedPacket() : base(PacketType.PlayerJoined)
        {
            ExistingPlayers = new List<PlayerInfo>();
        }
    }

    [Serializable]
    public class PlayerLeftPacket : NetworkPacket
    {
        public int PlayerId { get; set; }
        
        public PlayerLeftPacket() : base(PacketType.PlayerLeft) { }
    }

    [Serializable]
    public class PlayerPositionPacket : NetworkPacket
    {
        public int PlayerId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        
        public PlayerPositionPacket() : base(PacketType.PlayerPosition) { }
    }

    // ==================== Building Packets ====================
    
    [Serializable]
    public class BuildingPlacedPacket : NetworkPacket
    {
        public int PlayerId { get; set; }
        public string BuildingType { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int Rotation { get; set; }
        public int Variant { get; set; }
        
        public BuildingPlacedPacket() : base(PacketType.BuildingPlaced) { }
    }

    [Serializable]
    public class BuildingRemovedPacket : NetworkPacket
    {
        public int PlayerId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public BuildingRemovedPacket() : base(PacketType.BuildingRemoved) { }
    }
    
    [Serializable]
    public class IslandData
    {
        public string IslandType { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int Rotation { get; set; }
    }

    // ==================== Sync Packets ====================
    
    [Serializable]
    public class FullStateSyncPacket : NetworkPacket
    {
        public List<BuildingData> Buildings { get; set; }

        public FullStateSyncPacket() : base(PacketType.FullStateSync)
        {
            Buildings = new List<BuildingData>();
        }
    }

    [Serializable]
    public class WorldStatePacket : NetworkPacket
    {
        public int AssignedPlayerId { get; set; } // The ID for the receiving client
        public List<PlayerInfo> AllPlayers { get; set; } // The list of all currently connected players
        public int WorldSeed { get; set; }
        public string WorldName { get; set; }
        public string SaveData { get; set; }
        public float GameTime { get; set; }
        public List<BuildingData> Buildings { get; set; }
        public List<IslandData> Islands { get; set; }
        public Dictionary<string, string> WorldMetadata { get; set; }
        
        public WorldStatePacket() : base(PacketType.WorldState)
        {
            AllPlayers = new List<PlayerInfo>(); // Initialize the new list
            Islands = new List<IslandData>();
            Buildings = new List<BuildingData>();
            WorldMetadata = new Dictionary<string, string>();
        }
    }

    // ==================== Chat Packets ====================
    
    [Serializable]
    public class ChatMessagePacket : NetworkPacket
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
        public string Message { get; set; }
        
        public ChatMessagePacket() : base(PacketType.ChatMessage) { }
    }

    // ==================== Heartbeat Packets ====================
    
    [Serializable]
    public class PingPacket : NetworkPacket
    {
        public PingPacket() : base(PacketType.Ping) { }
    }

    [Serializable]
    public class PongPacket : NetworkPacket
    {
        public PongPacket() : base(PacketType.Pong) { }
    }

    // ==================== Data Models ====================
    
    [Serializable]
    public class PlayerInfo
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
        public bool IsHost { get; set; }
        
        [NonSerialized]
        public UnityEngine.Vector3 Position;
        
        [NonSerialized]
        public float LastUpdateTime;
    }

    [Serializable]
    public class BuildingData
    {
        public string BuildingType { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int Rotation { get; set; }
        public int Variant { get; set; }
        public string AdditionalData { get; set; }
    }

    [Serializable]
    public class SerializableVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    [Serializable]
    public class IslandPlacedPacket : NetworkPacket
    {
        public int PlayerId { get; set; }
        public string IslandType { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int Rotation { get; set; }
        public IslandPlacedPacket() : base(PacketType.IslandPlaced) { }
    }

    [Serializable]
    public class IslandRemovedPacket : NetworkPacket
    {
        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public IslandRemovedPacket() : base(PacketType.IslandRemoved) { }
    }
    
    [Serializable]
    public class RequestModifyWorldPacket : NetworkPacket
    {
        public List<BuildingData> BuildingsToPlace { get; set; }
        public List<SerializableVector3> PositionsToRemove { get; set; }
        public List<IslandData> IslandsToPlace { get; set; }
        public List<SerializableVector3> IslandPositionsToRemove { get; set; }

        public RequestModifyWorldPacket() : base(PacketType.RequestModifyWorld) 
        {
            BuildingsToPlace = new List<BuildingData>();
            PositionsToRemove = new List<SerializableVector3>();
            IslandsToPlace = new List<IslandData>();
            IslandPositionsToRemove = new List<SerializableVector3>();
        }
    }

    // ==================== Progression Sync Packets (NEW) ====================
    
    [Serializable]
    public class RequestUnlockUpgradePacket : NetworkPacket
    {
        public string UpgradeId { get; set; }
        
        public RequestUnlockUpgradePacket() : base(PacketType.RequestUnlockUpgrade) { }
    }

    [Serializable]
    public class ConfirmUnlockUpgradePacket : NetworkPacket
    {
        public string UpgradeId { get; set; }
        
        public ConfirmUnlockUpgradePacket() : base(PacketType.ConfirmUnlockUpgrade) { }
    }
}