using UnityEngine;

namespace Core
{
    /// <summary>
    /// Controls the player's cannon. Handles horizontal movement.
    /// Hold touch/mouse to move. Release to stop.
    /// </summary>
    public class CanonController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float limitX = 3f;

        [Header("Shooting")]
        // public MobSpawner mobSpawner; // We'll add this back in the next step
        public Transform shootPoint;

        private Camera cam;

        private void Start()
        {
            cam = Camera.main;
        }

        private void Update()
        {
            // Use legacy Input because Luna's web compiler supports it perfectly.
            // (The New Input System package causes CS0234 errors in Luna builds).
            bool held = Input.GetMouseButton(0);

            if (!held) return;

            // --- MOVE ---
            Vector3 screenPos = Input.mousePosition;
            Plane plane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
            Ray ray = cam.ScreenPointToRay(screenPos);

            if (plane.Raycast(ray, out float dist))
            {
                Vector3 hit = ray.GetPoint(dist);
                float targetX = Mathf.Clamp(hit.x, -limitX, limitX);
                Vector3 pos = transform.position;
                pos.x = Mathf.Lerp(pos.x, targetX, moveSpeed * Time.deltaTime);
                transform.position = pos;
            }

            // --- SHOOT ---
            // if (mobSpawner != null && shootPoint != null)
            // {
            //     mobSpawner.TryShoot(shootPoint.position);
            // }
        }
    }
}
