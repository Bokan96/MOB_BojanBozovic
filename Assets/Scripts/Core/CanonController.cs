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

        [Header("Juice - Wheels")]
        [Tooltip("Drag all 4 wheel GameObjects here. They will rotate on Z proportional to cannon movement.")]
        public Transform[] wheels;
        [Tooltip("Degrees per unit of horizontal movement speed.")]
        public float wheelRotationSpeed = 120f;

        [Header("Juice - Movement Smoke")]
        [Tooltip("Single Particle System placed between the wheels. Emission scales with movement speed.")]
        public ParticleSystem movementSmoke;
        [Tooltip("Maximum particles-per-second emitted at full speed.")]
        public float smokeMaxEmission = 40f;
        [Tooltip("Minimum movement speed (units/sec) before smoke starts emitting.")]
        public float smokeMinSpeed = 1f;

        [Header("Debug (Read Only)")]
        [Tooltip("Current cannon movement speed in units/sec. Watch this at runtime to tune smokeMinSpeed.")]
        public float debugCurrentSpeed;

        private readonly Vector3 _baseScale = Vector3.one;
        private readonly Vector3 _recoilScale = new Vector3(1.1f, 1.1f, 1.1f);
        private readonly Vector3 _bigRecoilScale = new Vector3(1.3f, 0.7f, 1.3f); // Exaggerated squash for Big Mob

        private Camera cam;
        private float _prevX;

        private void Start()
        {
            cam = Camera.main;
            _prevX = transform.position.x;
        }

        private void Update()
        {
            bool held = Input.GetMouseButton(0);
            bool released = Input.GetMouseButtonUp(0);

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

            // --- BIG MOB ON RELEASE ---
            if (released && UI.FeverBar.Instance != null && UI.FeverBar.Instance.IsFull)
            {
                if (mobSpawner != null && shootPoint != null)
                {
                    mobSpawner.SpawnBigMob(shootPoint.position);

                    // Heavy recoil for the big shot
                    if (cannonHead != null)
                    {
                        cannonHead.localScale = _bigRecoilScale;
                    }

                    // Reset the fever bar so it can fill again
                    UI.FeverBar.Instance.ResetBar();
                }
            }

            // --- ANIMATION RECOVERY ---
            if (cannonHead != null)
            {
                cannonHead.localScale = Vector3.Lerp(cannonHead.localScale, _baseScale, Time.deltaTime * springSpeed);
            }

            // --- WHEEL ROTATION ---
            float deltaX = transform.position.x - _prevX;
            _prevX = transform.position.x;

            if (wheels != null && Mathf.Abs(deltaX) > 0.0001f)
            {
                float rotAmount = -deltaX * wheelRotationSpeed;
                foreach (Transform wheel in wheels)
                {
                    if (wheel != null)
                        wheel.Rotate(0f, 0f, rotAmount, Space.Self);
                }
            }

            // --- MOVEMENT SMOKE ---
            float speed = Mathf.Abs(deltaX) / Time.deltaTime; // units per second
            debugCurrentSpeed = speed;

            if (movementSmoke != null)
            {
                var emission = movementSmoke.emission;

                if (speed >= smokeMinSpeed)
                {
                    emission.rateOverTime = Mathf.Lerp(0f, smokeMaxEmission, Mathf.Clamp01(speed / moveSpeed));

                    if (!movementSmoke.isPlaying)
                        movementSmoke.Play();
                }
                else
                {
                    // Below threshold — cut emission, let existing particles die naturally
                    emission.rateOverTime = 0f;
                }
            }
        }
    }
}
