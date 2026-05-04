using UnityEngine;
using UnityEngine.InputSystem; // Added for New Input System support

namespace Core
{
    public class CanonController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("How fast the canon follows the finger/mouse")]
        public float moveSpeed = 5f;
        [Tooltip("Maximum distance from the center on the X axis")]
        public float limitX = 3f;

        private Camera mainCamera;
        private bool isDragging = false;

        private void Start()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("Main Camera not found! Make sure your camera is tagged 'MainCamera'.");
            }
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            // Check if pointer exists (mouse or touch)
            if (Pointer.current == null) return;

            // Start/End drag based on pointer press state
            isDragging = Pointer.current.press.isPressed;

            if (isDragging)
            {
                MoveCanon();
            }
        }

        private void MoveCanon()
        {
            // Get screen position from the pointer
            Vector2 screenPos = Pointer.current.position.ReadValue();
            
            // Create a mathematical plane at the canon's Y height, facing upwards
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
            
            // Cast a ray from the camera through the pointer position
            Ray ray = mainCamera.ScreenPointToRay(screenPos);

            // Find where the ray hits the plane
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);

                // We only care about the X position, keep the canon's original Y and Z
                Vector3 targetPosition = new Vector3(hitPoint.x, transform.position.y, transform.position.z);

                // Clamp the X position so it doesn't fall off the platform
                targetPosition.x = Mathf.Clamp(targetPosition.x, -limitX, limitX);

                // Smoothly interpolate towards the target position
                transform.position = Vector3.Lerp(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            }
        }
    }
}
