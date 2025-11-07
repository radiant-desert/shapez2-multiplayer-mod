using System;

namespace Shapez2MultiplayerMod.Models
{
    // SavegameRequestPacket has been removed as it is no longer used.
    
    /// <summary>
    /// A chunk of savegame data.
    /// </summary>
    [Serializable]
    public class SavegameDataPacket : NetworkPacket
    {
        public string SavegameName { get; set; }
        public int ChunkIndex { get; set; }
        public int TotalChunks { get; set; }
        public byte[] Data { get; set; }
        
        public SavegameDataPacket() : base(PacketType.SavegameData) { }
    }
    
    /// <summary>
    /// Signals that the savegame transfer is complete.
    /// </summary>
    [Serializable]
    public class SavegameCompletePacket : NetworkPacket
    {
        public string SavegameName { get; set; }
        public int TotalSize { get; set; }
        
        public SavegameCompletePacket() : base(PacketType.SavegameComplete) { }
    }
}