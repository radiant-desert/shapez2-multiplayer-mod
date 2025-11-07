using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Shapez2MultiplayerMod.Networking;
using Shapez2MultiplayerMod.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

// Required for the save/load logic
using Game.Core;
using Game.Core.Serialization;
using Unity.Core.Logging; // The correct namespace for UnityLogger

namespace Shapez2MultiplayerMod.Managers
{
    public class SavegameTransfer
    {
        private NetworkManager networkManager;
        private MonoBehaviour coroutineHost;
        private const int CHUNK_SIZE = 64000;
        private string savegamePath;

        private Dictionary<int, byte[]> receivedChunks = new Dictionary<int, byte[]>();
        private int expectedChunks = 0;
        
        public SavegameTransfer(NetworkManager netManager)
        {
            networkManager = netManager;
            savegamePath = Path.Combine(Application.persistentDataPath, "savegames");
        }
        
        public void Initialize(MonoBehaviour host)
        {
            coroutineHost = host;
            networkManager.OnPacketReceived += OnPacketReceived;
            MultiplayerMod.Log.LogInfo("[SavegameTransfer] Initialized and subscribed to OnPacketReceived.");
        }
        
        // --- HOST-SIDE LOGIC ---

        public void SendCurrentSavegame(int clientId)
        {
            if (!networkManager.IsServer) return;
            
            var gameCore = Singleton<StaticGameCoreAccessor>.G;
            if (gameCore?.Savegame == null) return;

            coroutineHost.StartCoroutine(SendSavegameRoutine(gameCore.Savegame.InternalUuid, clientId));
        }

        private IEnumerator SendSavegameRoutine(string savegameUid, int clientId)
        {
            byte[] fileData;
            try
            {
                string savegameFile = FindSavegameFileByUid(savegameUid);
                if (string.IsNullOrEmpty(savegameFile))
                {
                    MultiplayerMod.Log.LogError($"[SavegameTransfer] CRITICAL: Could not find file for savegame UID: {savegameUid}");
                    yield break;
                }
                
                fileData = File.ReadAllBytes(savegameFile);
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[SavegameTransfer] Error reading savegame file: {ex.Message}\n{ex.StackTrace}");
                yield break;
            }

            string saveName = "mp_world"; 
            int totalChunks = (int)Math.Ceiling((double)fileData.Length / CHUNK_SIZE);
            
            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * CHUNK_SIZE;
                int chunkSize = Math.Min(CHUNK_SIZE, fileData.Length - offset);
                byte[] chunk = new byte[chunkSize];
                Array.Copy(fileData, offset, chunk, 0, chunkSize);
                
                var packet = new SavegameDataPacket
                {
                    SavegameName = saveName,
                    ChunkIndex = i,
                    TotalChunks = totalChunks,
                    Data = chunk
                };
                networkManager.SendToClient(clientId, packet);
            }
            
            yield return null; 
            
            var completePacket = new SavegameCompletePacket { SavegameName = saveName, TotalSize = fileData.Length };
            networkManager.SendToClient(clientId, completePacket);
            MultiplayerMod.Log.LogInfo($"[SavegameTransfer] Savegame transfer to client {clientId} complete.");
        }

        private string FindSavegameFileByUid(string savegameUid)
        {
            if (!Directory.Exists(savegamePath)) return null;
            var files = Directory.GetFiles(savegamePath, "*.spz2", SearchOption.AllDirectories);
            return files.FirstOrDefault(f => Path.GetFileName(f).Contains(savegameUid));
        }

        // --- CLIENT-SIDE LOGIC ---
        
        private void OnPacketReceived(int senderId, NetworkPacket packet)
        {
            if (!networkManager.IsClient) return;
            if (packet is SavegameDataPacket dataPacket) { OnSavegameDataReceived(dataPacket); }
            else if (packet is SavegameCompletePacket completePacket) { OnSavegameComplete(completePacket); }
        }

        private void OnSavegameDataReceived(SavegameDataPacket packet)
        {
            if (packet.ChunkIndex == 0)
            {
                receivedChunks.Clear();
                expectedChunks = packet.TotalChunks;
                MultiplayerMod.Log.LogInfo($"[SavegameTransfer] Receiving new savegame '{packet.SavegameName}' in {expectedChunks} chunks.");
            }
            receivedChunks[packet.ChunkIndex] = packet.Data;
            MultiplayerMod.Log.LogInfo($"[SavegameTransfer] Received chunk {receivedChunks.Count}/{expectedChunks}.");
        }

        private void OnSavegameComplete(SavegameCompletePacket packet)
        {
            if (receivedChunks.Count != expectedChunks) return;

            try
            {
                using (var ms = new MemoryStream())
                {
                    for (int i = 0; i < expectedChunks; i++)
                    {
                        ms.Write(receivedChunks[i], 0, receivedChunks[i].Length);
                    }
                    
                    string tempSaveName = $"mp_session_{Guid.NewGuid().ToString().Substring(0, 8)}.spz2";
                    string tempSavePath = Path.Combine(savegamePath, tempSaveName);
                    File.WriteAllBytes(tempSavePath, ms.ToArray());
                    coroutineHost.StartCoroutine(LoadNewSavegame(tempSavePath));
                }
            }
            catch (Exception ex) { MultiplayerMod.Log.LogError($"[SavegameTransfer] Error saving/loading savegame: {ex.Message}\n{ex.StackTrace}"); }
            finally
            {
                receivedChunks.Clear();
                expectedChunks = 0;
            }
        }

        private IEnumerator LoadNewSavegame(string savePath)
        {
            networkManager.Shutdown();
            yield return new WaitForSeconds(0.5f);

            try
            {
                // FIX: Use the correct namespace for UnityLogger
                var savegameReader = new SaveFileAccessor(new UnityLogger(null)).Read(savePath, new Dictionary<Type, IDataSerializer>());
                
                // FIX: Use the correct property for the Metadata's UID.
                string saveUid = savegameReader.Metadata.InternalUuid;

                Globals.CrossSceneGameOptions.Provide(new GameStartOptionsContinueExisting(savegameReader, false, saveUid));
                SceneManager.LoadSceneAsync("GameLoading", LoadSceneMode.Single);
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogError($"[SavegameTransfer] Failed to initiate savegame load: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}