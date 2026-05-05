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
        public Mobs.MobSpawner mobSpawner;
        public Transform shootPoint;

        [Header("Juice - Cannon Head")]
        public Transform cannonHead;
        public float springSpeed = 25f;

        private readonly Vector3 _baseScale = Vector3.one;
        private readonly Vector3 _recoilScale = new Vector3(1.1f, 1.1f, 1.1f);

        private Camera cam;

        private void Start()
        {
            cam = Camera.main;
        }

        private void Update()
        {
            bool held = Input.GetMouseButton(0);

            if (held)
            {
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
                if (mobSpawner != null && shootPoint != null)
                {
                    if (mobSpawner.TryShoot())
                    {
                        if (cannonHead != null)
                        {
                            cannonHead.localScale = _recoilScale;
                        }
                    }
                }
            }

            // --- ANIMATION RECOVERY ---
            if (cannonHead != null)
            {
                cannonHead.localScale = Vector3.Lerp(cannonHead.localScale, _baseScale, Time.deltaTime * springSpeed);
            }
        }
    }
}
