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
        [Tooltip("One ParticleSystem per wheel pair (e.g. front and rear). All emit together based on speed.")]
        public ParticleSystem[] movementSmokes;
        [Tooltip("Maximum particles-per-second emitted at full speed.")]
        public float smokeMaxEmission = 40f;
        [Tooltip("Minimum movement speed (units/sec) before smoke starts emitting.")]
        public float smokeMinSpeed = 1f;

        [Header("Juice - Muzzle Flare")]
        [Tooltip("A SpriteRenderer child at the shoot point. Use a soft white circle sprite with Additive shader.")]
        public SpriteRenderer muzzleFlare;
        [Tooltip("Maximum uniform scale of the flare sprite.")]
        public float flareMaxScale = 2f;
        [Tooltip("How long the flare is visible (seconds).")]
        public float flareDuration = 0.12f;

        [Header("Juice - Fever Ready Particles")]
        [Tooltip("ParticleSystem on the cannon that sparkles when fever bar is full.")]
        public ParticleSystem feverReadyParticles;

        [Header("Juice - Big Shot Recoil")]
        [Tooltip("The root transform of the cannon to push backward on local Z.")]
        public Transform cannonBody;
        [Tooltip("How far backward (local -Z) the cannon recoils.")]
        public float recoilDistance = 0.6f;
        [Tooltip("Time to reach full recoil position (seconds).")]
        public float recoilOutDuration = 0.08f;
        [Tooltip("Time to spring back to original position (seconds).")]
        public float recoilReturnDuration = 0.3f;

        [Header("Juice - Big Shot Smoke")]
        [Tooltip("ParticleSystem for the big smoke burst. Reuses the existing Smoke material.")]
        public ParticleSystem bigShotSmoke;

        [Header("Debug (Read Only)")]
        [Tooltip("Current cannon movement speed in units/sec. Watch this at runtime to tune smokeMinSpeed.")]
        public float debugCurrentSpeed;

        private readonly Vector3 _baseScale = Vector3.one;
        private readonly Vector3 _recoilScale = new Vector3(1.1f, 1.1f, 1.1f);
        private readonly Vector3 _bigRecoilScale = new Vector3(1.3f, 0.7f, 1.3f); // Exaggerated squash for Big Mob

        private Camera cam;
        private float _prevX;

        // Muzzle flare state
        private float _flareTimer;
        private bool _flareActive;
        private Color _flareBaseColor;

        // Fever tracking
        private bool _wasFeverFull;

        // Big shot Z recoil state
        private bool _isRecoiling;
        private float _recoilTimer;
        private Vector3 _cannonBodyBaseLocalPos;
        private bool _recoilReturning; // false = pushing back, true = returning

        private void Start()
        {
            cam = Camera.main;
            _prevX = transform.position.x;

            // Initialize muzzle flare to invisible
            if (muzzleFlare != null)
            {
                _flareBaseColor = muzzleFlare.color;
                muzzleFlare.transform.localScale = Vector3.zero;
                muzzleFlare.enabled = false;
            }

            // Cache cannon body base position for recoil
            if (cannonBody != null)
            {
                _cannonBodyBaseLocalPos = cannonBody.localPosition;
            }

            // Ensure fever particles start stopped
            if (feverReadyParticles != null)
            {
                feverReadyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
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

                        // Trigger muzzle flare on normal shots
                        TriggerMuzzleFlare();
                    }
                }
            }

            // --- BIG MOB ON RELEASE ---
            if (released && UI.FeverBar.Instance != null && UI.FeverBar.Instance.IsFull)
            {
                if (mobSpawner != null && shootPoint != null)
                {
                    mobSpawner.SpawnBigMob(shootPoint.position);

                    // Heavy recoil for the big shot (scale squash)
                    if (cannonHead != null)
                    {
                        cannonHead.localScale = _bigRecoilScale;
                    }

                    // Z-axis recoil push-back
                    TriggerBigShotRecoil();

                    // Big smoke burst instead of normal flare
                    TriggerBigShotSmoke();

                    // Reset the fever bar so it can fill again
                    UI.FeverBar.Instance.ResetBar();
                }
            }

            // --- ANIMATION RECOVERY ---
            if (cannonHead != null)
            {
                cannonHead.localScale = Vector3.Lerp(cannonHead.localScale, _baseScale, Time.deltaTime * springSpeed);
            }

            // --- MUZZLE FLARE ANIMATION ---
            UpdateMuzzleFlare();

            // --- FEVER READY PARTICLES ---
            UpdateFeverParticles();

            // --- BIG SHOT RECOIL ANIMATION ---
            UpdateBigShotRecoil();

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
            float speed = Mathf.Abs(deltaX) / Time.deltaTime;
            debugCurrentSpeed = speed;

            if (movementSmokes != null)
            {
                bool shouldEmit = speed >= smokeMinSpeed;
                float rate = shouldEmit
                    ? Mathf.Lerp(0f, smokeMaxEmission, Mathf.Clamp01(speed / moveSpeed))
                    : 0f;

                foreach (var smoke in movementSmokes)
                {
                    if (smoke == null) continue;
                    var emission = smoke.emission;
                    emission.rateOverTime = rate;

                    if (shouldEmit && !smoke.isPlaying)
                        smoke.Play();
                }
            }
        }

        // ==================== MUZZLE FLARE ====================

        private void TriggerMuzzleFlare()
        {
            if (muzzleFlare == null) return;
            _flareActive = true;
            _flareTimer = 0f;
            muzzleFlare.enabled = true;
        }

        private void UpdateMuzzleFlare()
        {
            if (!_flareActive || muzzleFlare == null) return;

            _flareTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_flareTimer / flareDuration);

            // Quick scale-up (first 30%) then shrink back to 0
            float scaleCurve = t < 0.3f
                ? Mathf.Lerp(0f, 1f, t / 0.3f)
                : Mathf.Lerp(1f, 0f, (t - 0.3f) / 0.7f);
            float scale = scaleCurve * flareMaxScale;
            muzzleFlare.transform.localScale = new Vector3(scale, scale, scale);

            // Alpha fade out
            Color c = _flareBaseColor;
            c.a = 1f - t;
            muzzleFlare.color = c;

            if (t >= 1f)
            {
                _flareActive = false;
                muzzleFlare.enabled = false;
                muzzleFlare.transform.localScale = Vector3.zero;
            }
        }

        // ==================== FEVER READY PARTICLES ====================

        private void UpdateFeverParticles()
        {
            if (feverReadyParticles == null) return;

            bool feverFull = UI.FeverBar.Instance != null && UI.FeverBar.Instance.IsFull;

            if (feverFull && !_wasFeverFull)
            {
                // Just became full — start particles
                feverReadyParticles.Play();
            }
            else if (!feverFull && _wasFeverFull)
            {
                // No longer full — stop particles
                feverReadyParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            _wasFeverFull = feverFull;
        }

        // ==================== BIG SHOT RECOIL ====================

        private void TriggerBigShotRecoil()
        {
            if (cannonBody == null) return;
            _isRecoiling = true;
            _recoilTimer = 0f;
            _recoilReturning = false;
        }

        private void UpdateBigShotRecoil()
        {
            if (!_isRecoiling || cannonBody == null) return;

            _recoilTimer += Time.deltaTime;

            if (!_recoilReturning)
            {
                // Phase 1: Push backward (fast, punchy)
                float t = Mathf.Clamp01(_recoilTimer / recoilOutDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f); // Cubic ease-out

                Vector3 pos = _cannonBodyBaseLocalPos;
                pos.z -= recoilDistance * eased;
                cannonBody.localPosition = pos;

                if (t >= 1f)
                {
                    _recoilReturning = true;
                    _recoilTimer = 0f;
                }
            }
            else
            {
                // Phase 2: Spring back with elastic ease-out
                float t = Mathf.Clamp01(_recoilTimer / recoilReturnDuration);
                float eased = ElasticEaseOut(t);

                Vector3 pos = _cannonBodyBaseLocalPos;
                pos.z -= recoilDistance * (1f - eased);
                cannonBody.localPosition = pos;

                if (t >= 1f)
                {
                    _isRecoiling = false;
                    cannonBody.localPosition = _cannonBodyBaseLocalPos;
                }
            }
        }

        /// <summary>
        /// Elastic ease-out: springs past the target and settles. Gives a satisfying "boing" feel.
        /// </summary>
        private static float ElasticEaseOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }

        // ==================== BIG SHOT SMOKE ====================

        private void TriggerBigShotSmoke()
        {
            if (bigShotSmoke == null) return;
            bigShotSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            bigShotSmoke.Play();
        }
    }
}
