using System.Collections.Generic;
using Shapez2MultiplayerMod.Models;
using UnityEngine;

namespace Shapez2MultiplayerMod.Managers
{
    public class PlayerManager
    {
        private Dictionary<int, PlayerInfo> players = new Dictionary<int, PlayerInfo>();
        
        public int LocalPlayerId { get; set; } = -1;
        public string LocalPlayerName { get; set; } = "Player";
        
        public event System.Action<PlayerInfo> OnPlayerAdded;
        public event System.Action<int> OnPlayerRemoved;
        
        public PlayerManager()
        {
            MultiplayerMod.Log.LogInfo("[PlayerManager] Initialized");
        }

        public void AddPlayer(PlayerInfo player)
        {
            if (player == null) return;
            if (!players.ContainsKey(player.PlayerId))
            {
                players[player.PlayerId] = player;
                MultiplayerMod.Log.LogInfo($"[PlayerManager] Player {player.PlayerName} (ID: {player.PlayerId}) added.");
                OnPlayerAdded?.Invoke(player);
            }
        }

        public void RemovePlayer(int playerId)
        {
            if (players.TryGetValue(playerId, out var player))
            {
                players.Remove(playerId);
                MultiplayerMod.Log.LogInfo($"[PlayerManager] Player {player.PlayerName} (ID: {playerId}) removed.");
                OnPlayerRemoved?.Invoke(playerId);
            }
        }

        public PlayerInfo GetPlayer(int playerId)
        {
            players.TryGetValue(playerId, out var player);
            return player;
        }
        
        public List<PlayerInfo> GetAllPlayers()
        {
            return new List<PlayerInfo>(players.Values);
        }

        public void Clear()
        {
            var playerIds = new List<int>(players.Keys);
            foreach (var id in playerIds)
            {
                RemovePlayer(id);
            }
            players.Clear();
            LocalPlayerId = -1;
            MultiplayerMod.Log.LogInfo("[PlayerManager] Cleared all players.");
        }
    }
}