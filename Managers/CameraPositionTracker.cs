using UnityEngine;
using Shapez2MultiplayerMod.Models;

namespace Shapez2MultiplayerMod.Managers
{
    /// <summary>
    /// Tracks the cursor position on the ground to represent the player's location
    /// Uses raycasting to find where the mouse cursor points in the world
    /// </summary>
    public class CameraPositionTracker
    {
        private Camera mainCamera;
        private Vector3 lastPosition;
        private Vector3 currentCursorPosition;
        private float movementThreshold = 0.5f; // Minimum movement to trigger update
        private float groundY = 0f; // Y level of the ground plane
        
        public Vector3 CurrentPosition => currentCursorPosition;
        public bool HasMoved => Vector3.Distance(CurrentPosition, lastPosition) > movementThreshold;
        
        public CameraPositionTracker()
        {
            FindMainCamera();
            currentCursorPosition = Vector3.zero;
            lastPosition = Vector3.zero;
        }
        
        /// <summary>
        /// Find the main camera in the scene
        /// </summary>
        private void FindMainCamera()
        {
            mainCamera = Camera.main;
            
            if (mainCamera == null)
            {
                // Fallback: find any camera with tag "MainCamera"
                GameObject cameraObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (cameraObj != null)
                {
                    mainCamera = cameraObj.GetComponent<Camera>();
                }
            }
            
            if (mainCamera != null)
            {
                MultiplayerMod.Log.LogInfo("[CameraPositionTracker] Main camera found");
            }
            else
            {
                /// MultiplayerMod.Log.LogWarning("[CameraPositionTracker] Main camera not found!");
            }
        }
        
        /// <summary>
        /// Update the tracker - call this every frame
        /// </summary>
        public void Update()
        {
            // Re-find camera if lost
            if (mainCamera == null)
            {
                FindMainCamera();
                return;
            }
            
            // Get cursor position in world space
            UpdateCursorPosition();
        }
        
        /// <summary>
        /// Update cursor position using raycast from camera to ground
        /// </summary>
        private void UpdateCursorPosition()
        {
            if (mainCamera == null) return;
            
            // Get mouse position in screen space
            Vector3 mousePos = Input.mousePosition;
            
            // Create a ray from camera through mouse position
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            
            // Try to hit the ground plane (Y = groundY)
            // Calculate intersection with horizontal plane at groundY
            // Ray equation: P = origin + t * direction
            // Plane equation: Y = groundY
            // Solve for t: origin.y + t * direction.y = groundY
            
            if (Mathf.Abs(ray.direction.y) > 0.001f) // Avoid division by zero
            {
                float t = (groundY - ray.origin.y) / ray.direction.y;
                
                if (t > 0) // Ray hits plane in front of camera
                {
                    Vector3 hitPoint = ray.origin + ray.direction * t;
                    currentCursorPosition = hitPoint;
                }
            }
            else
            {
                // Ray is parallel to ground, use camera position projected to ground
                currentCursorPosition = new Vector3(
                    mainCamera.transform.position.x,
                    groundY,
                    mainCamera.transform.position.z
                );
            }
        }
        
        /// <summary>
        /// Mark current position as the last known position
        /// </summary>
        public void UpdateLastPosition()
        {
            lastPosition = CurrentPosition;
        }
        
        /// <summary>
        /// Get position as a serializable struct
        /// </summary>
        public SerializableVector3 GetSerializablePosition()
        {
            Vector3 pos = CurrentPosition;
            return new SerializableVector3
            {
                X = pos.x,
                Y = pos.y,
                Z = pos.z
            };
        }
    }
    
    /// <summary>
    /// Serializable version of Vector3 for network transmission
    /// </summary>
    /// 
}

