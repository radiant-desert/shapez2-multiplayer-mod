using System;
using System.Collections.Generic;
using UnityEngine;
using Shapez2MultiplayerMod.Models;
using System.Reflection;

namespace Shapez2MultiplayerMod.Managers
{
    public class PlayerMarkerManager
    {
        private Dictionary<string, PlayerMarker> playerMarkers = new Dictionary<string, PlayerMarker>();
        private PlayerManager playerManager;
        
        private static readonly Color[] PLAYER_COLORS = new Color[]
        {
            new Color(1f, 0.2f, 0.2f),    // Red
            new Color(0.2f, 0.2f, 1f),    // Blue
            new Color(0.2f, 1f, 0.2f),    // Green
            new Color(1f, 1f, 0.2f),      // Yellow
            new Color(1f, 0.2f, 1f),      // Magenta
            new Color(0.2f, 1f, 1f),      // Cyan
            new Color(1f, 0.6f, 0.2f),    // Orange
            new Color(0.6f, 0.2f, 1f),    // Purple
        };
        
        public PlayerMarkerManager(PlayerManager plyrManager)
        {
            playerManager = plyrManager;
            MultiplayerMod.Log.LogInfo("[PlayerMarkerManager] Initialized");
        }
        
        public void CreateMarker(PlayerInfo player)
        {
            if (player == null || player.PlayerId == playerManager.LocalPlayerId) return;
            
            string playerId = player.PlayerId.ToString();
            
            if (playerMarkers.ContainsKey(playerId))
            {
                MultiplayerMod.Log.LogWarning($"[PlayerMarkerManager] Marker already exists for player {playerId}");
                return;
            }
            
            GameObject markerObj = new GameObject($"PlayerMarker_{player.PlayerName}");
            UnityEngine.Object.DontDestroyOnLoad(markerObj);
            
            GameObject visualsParent = new GameObject("Visuals");
            visualsParent.transform.SetParent(markerObj.transform);

            Color playerColor = PLAYER_COLORS[player.PlayerId % PLAYER_COLORS.Length];
            
            // --- NEW: Create a custom mesh for the cursor arrow ---
            GameObject pointer = new GameObject("PointerVisual");
            pointer.transform.SetParent(visualsParent.transform);
            
            Mesh pointerMesh = new Mesh();
            pointerMesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0),        // Tip of the cursor
                new Vector3(-0.5f, 0.8f, 0), // Top-left wing
                new Vector3(0, 0.6f, 0),     // Inner crook
                new Vector3(0.5f, 0.8f, 0)   // Top-right wing
            };
            pointerMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 }; // Define the two triangles that make the shape
            pointerMesh.RecalculateNormals();

            MeshFilter filter = pointer.AddComponent<MeshFilter>();
            filter.mesh = pointerMesh;

            MeshRenderer renderer = pointer.AddComponent<MeshRenderer>();
            // Use an Unlit shader so the cursor color is always bright and unaffected by lighting
            renderer.material.shader = Shader.Find("Universal RenderPipeline/Unlit"); 
            renderer.material.color = playerColor;
            
            GameObject labelObj = new GameObject($"Label_{player.PlayerName}");
            labelObj.transform.SetParent(visualsParent.transform);
            // Adjust text position to be nicely above the new, smaller cursor
            labelObj.transform.localPosition = new Vector3(0, 1.1f, 0); 
            
            float outlineOffset = 0.03f;
            CreateTextMesh(labelObj, player.PlayerName, Color.black, new Vector3(outlineOffset, -outlineOffset, 0.1f));
            CreateTextMesh(labelObj, player.PlayerName, Color.black, new Vector3(-outlineOffset, outlineOffset, 0.1f));
            CreateTextMesh(labelObj, player.PlayerName, Color.white, Vector3.zero);
            
            PlayerMarker marker = new PlayerMarker
            {
                GameObject = markerObj,
                VisualsObject = visualsParent,
                LabelObject = labelObj,
                PlayerId = playerId,
                PlayerName = player.PlayerName,
                Color = playerColor,
                TargetPosition = player.Position,
                CurrentPosition = player.Position
            };
            
            playerMarkers[playerId] = marker;
            
            MultiplayerMod.Log.LogInfo($"[PlayerMarkerManager] Created marker for player {player.PlayerName} (ID: {playerId})");
        }

        private void CreateTextMesh(GameObject parent, string text, Color color, Vector3 localPosition)
        {
            try
            {
                var textObj = new GameObject("TextMesh_" + color);
                textObj.transform.SetParent(parent.transform, false); // Use false to maintain local orientation
                textObj.transform.localPosition = localPosition;

                var textMeshType = Type.GetType("UnityEngine.TextMesh, UnityEngine.TextRenderingModule");
                if (textMeshType != null)
                {
                    var textMesh = textObj.AddComponent(textMeshType);
                    textMeshType.GetProperty("text")?.SetValue(textMesh, text);
                    textMeshType.GetProperty("fontSize")?.SetValue(textMesh, 80); // Slightly smaller font size
                    textMeshType.GetProperty("characterSize")?.SetValue(textMesh, 0.1f);
                    textMeshType.GetProperty("color")?.SetValue(textMesh, color);
                    
                    var anchorProp = textMeshType.GetProperty("anchor");
                    var textAnchorType = Type.GetType("UnityEngine.TextAnchor, UnityEngine.TextRenderingModule");
                    if (anchorProp != null && textAnchorType != null)
                    {
                        // Anchor at the bottom-center so it sits nicely above the pointer's origin
                        anchorProp.SetValue(textMesh, Enum.Parse(textAnchorType, "LowerCenter"));
                    }
                }
            }
            catch (Exception ex)
            {
                MultiplayerMod.Log.LogWarning($"[PlayerMarkerManager] Could not add TextMesh: {ex.Message}");
            }
        }
        
        public void RemoveMarker(string playerId)
        {
            if (playerMarkers.TryGetValue(playerId, out PlayerMarker marker))
            {
                if (marker.GameObject != null)
                {
                    UnityEngine.Object.Destroy(marker.GameObject);
                }
                
                playerMarkers.Remove(playerId);
                MultiplayerMod.Log.LogInfo($"[PlayerMarkerManager] Removed marker for player {playerId}");
            }
        }
        
        public void UpdateMarkerPosition(string playerId, Vector3 position)
        {
            if (playerMarkers.TryGetValue(playerId, out PlayerMarker marker))
            {
                marker.TargetPosition = position;
            }
        }
        
        public void Update()
        {
            foreach (var marker in playerMarkers.Values)
            {
                UpdateMarker(marker);
            }
        }
        
        private void UpdateMarker(PlayerMarker marker)
        {
            if (marker.GameObject == null) return;
            
            marker.CurrentPosition = Vector3.Lerp(marker.CurrentPosition, marker.TargetPosition, 15f * Time.deltaTime);
            marker.GameObject.transform.position = marker.CurrentPosition;
            
            Camera mainCamera = Camera.main;
            if (mainCamera != null && marker.VisualsObject != null)
            {
                float distance = Vector3.Distance(mainCamera.transform.position, marker.CurrentPosition);
                // --- CHANGE: Reduced scale factor to make the cursor smaller ---
                float constantScaleFactor = 0.035f; 
                float newScale = distance * constantScaleFactor;
                marker.VisualsObject.transform.localScale = Vector3.one * newScale;
                
                if (marker.LabelObject != null)
                {
                    marker.LabelObject.transform.rotation = mainCamera.transform.rotation;
                }
            }
        }
        
        public void Shutdown()
        {
            var keys = new List<string>(playerMarkers.Keys);
            foreach (var playerId in keys)
            {
                RemoveMarker(playerId);
            }
            playerMarkers.Clear();
            MultiplayerMod.Log.LogInfo("[PlayerMarkerManager] Shutdown complete");
        }
    }
    
    public class PlayerMarker
    {
        public GameObject GameObject { get; set; }
        public GameObject VisualsObject { get; set; }
        public GameObject LabelObject { get; set; }
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public Color Color { get; set; }
        public Vector3 TargetPosition { get; set; }
        public Vector3 CurrentPosition { get; set; }
    }
}