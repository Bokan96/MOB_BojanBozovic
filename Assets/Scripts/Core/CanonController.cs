using UnityEngine;
using UnityEngine.InputSystem;

namespace Core
{
    /// <summary>
    /// Handles the player's canon movement and provides input state for shooting.
    /// This is the foundation of the player's interaction.
    /// </summary>
    public class CanonController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("How fast the canon slides to follow the pointer")]
        public float moveSpeed = 15f;
        [Tooltip("Horizontal clamp limits")]
        public float limitX = 4f;

        [Header("Input State (Read-only)")]
        [SerializeField] private bool _isPressing = false;
        
        private Camera _mainCamera;

        /// <summary>
        /// Public property to check if the player is currently holding down.
        /// Other scripts (like Spawner) will check this.
        /// </summary>
        public bool IsPressing => _isPressing;

        private void Start()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("[CanonController] No Main Camera found in scene!");
            }
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            // Support for New Input System Pointer (Mouse/Touch)
            if (Pointer.current == null) return;

            _isPressing = Pointer.current.press.isPressed;

            if (_isPressing)
            {
                MoveToPosition(Pointer.current.position.ReadValue());
            }
        }

        private void MoveToPosition(Vector2 screenPosition)
        {
            // Cast a ray to the ground plane (Y=transform.position.y)
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);

                // Smoothly follow the X position
                float targetX = Mathf.Clamp(worldPoint.x, -limitX, limitX);
                Vector3 targetPos = new Vector3(targetX, transform.position.y, transform.position.z);
                
                transform.position = Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.deltaTime);
            }
        }
    }
}
