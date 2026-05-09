using System.Collections;
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

        [Header("Hook Animation")]
        [Tooltip("The frame rig transform that gets rotated 90° on Y at start and lerps back during the hook.")]
        public Transform cannonFrameRig;
        [Tooltip("How far back on the Z-axis the cannon starts during the hook.")]
        public float hookZDistance = 8f;
        [Tooltip("Duration of the hook entrance animation in seconds.")]
        public float hookDuration = 2f;
        [Tooltip("If true, the main camera is parented to the cannon during the hook so it follows the movement.")]
        public bool hookAttachCamera = true;
        [Tooltip("Degrees per second the wheels rotate on Z during the hook slide.")]
        public float hookWheelRotationSpeed = 70f;
        [Tooltip("Duration of the camera smoothly transitioning back to its original position.")]
        public float hookCameraTransitionDuration = 0.8f;
        [Tooltip("Optional: Assign a camera or transform here to define where the main camera starts during the hook. If null, uses the values below.")]
        public Transform hookCameraStartMarker;
        [Tooltip("World position the camera starts at during the hook (used if marker is null).")]
        public Vector3 hookCameraStartPos = new Vector3(0.0f, 7.17f, -3.69f);
        [Tooltip("World rotation the camera starts at during the hook (used if marker is null).")]
        public Quaternion hookCameraStartRot = new Quaternion(0.3267505f, 0.0f, 0.0f, 0.9451107f);

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

        // Hook animation state
        /// <summary>True once the hook intro animation has finished and the player can interact.</summary>
        [HideInInspector] public bool hookComplete = false;
        private Vector3 _hookTargetPos;
        private Quaternion _hookTargetFrameRot;
        private Vector3 _hookStartPos;
        private Quaternion _hookStartFrameRot;
        private Transform _cameraOriginalParent;
        private Vector3 _cameraOriginalWorldPos;
        private Quaternion _cameraOriginalWorldRot;

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

            // ── Hook Animation Setup ──
            // Cache the cannon's designed scene position as the target
            _hookTargetPos = transform.position;

            // Parent the camera to the cannon so it follows the hook movement
            // This happens BEFORE we move the cannon to its start offset
            if (hookAttachCamera && cam != null)
            {
                _cameraOriginalParent = cam.transform.parent;
                _cameraOriginalWorldPos = cam.transform.position;
                _cameraOriginalWorldRot = cam.transform.rotation;
                
                if (hookCameraStartMarker != null)
                {
                    cam.transform.position = hookCameraStartMarker.position;
                    cam.transform.rotation = hookCameraStartMarker.rotation;
                }
                else
                {
                    cam.transform.position = hookCameraStartPos;
                    cam.transform.rotation = hookCameraStartRot;
                }
                cam.transform.SetParent(transform, true);
            }

            // Cache the frame rig's designed rotation as the target
            if (cannonFrameRig != null)
            {
                _hookTargetFrameRot = cannonFrameRig.localRotation;

                // Start the frame rig rotated 90° on Y
                Vector3 startEuler = cannonFrameRig.localEulerAngles;
                startEuler.y += 90f;
                _hookStartFrameRot = Quaternion.Euler(startEuler);
                cannonFrameRig.localRotation = _hookStartFrameRot;
            }

            // Move the cannon backward along Z
            _hookStartPos = _hookTargetPos - new Vector3(0f, 0f, hookZDistance);
            transform.position = _hookStartPos;
            _prevX = transform.position.x;

            // Start the hook animation coroutine
            StartCoroutine(HookAnimationRoutine());
        }

        private void Update()
        {
            // Block all player input during the hook intro animation
            if (!hookComplete) return;

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

                        if (AudioManager.Instance != null)
                        {
                            AudioManager.Instance.PlayCannonFire();
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

                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayFeverActivate();
                    }

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

            // Randomize Z rotation for variation on every shot
            Vector3 euler = muzzleFlare.transform.localEulerAngles;
            euler.z = Random.Range(0f, 360f);
            muzzleFlare.transform.localEulerAngles = euler;
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

        // ==================== HOOK ANIMATION ====================

        /// <summary>
        /// Intro hook: cannon slides forward along Z while the frame rig
        /// rotates from 90° back to its original Y rotation. Camera follows.
        /// On completion the camera is detached and the tutorial hand appears.
        /// </summary>
        private IEnumerator HookAnimationRoutine()
        {
            float elapsed = 0f;

            // Phase 1: Slide forward along Z
            while (elapsed < hookDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hookDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                // Lerp cannon position forward along Z
                transform.position = Vector3.Lerp(_hookStartPos, _hookTargetPos, eased);

                // Spin wheels during the slide
                if (wheels != null)
                {
                    float rotAmount = hookWheelRotationSpeed * Time.deltaTime;
                    foreach (Transform wheel in wheels)
                    {
                        if (wheel != null)
                            wheel.Rotate(0f, 0f, -rotAmount, Space.Self);
                    }
                }

                yield return null;
            }

            // Snap to final position
            transform.position = _hookTargetPos;
            _prevX = _hookTargetPos.x;

            // Phase 2: Rotate the frame rig back (Sequential, only after slide)
            if (cannonFrameRig != null)
            {
                elapsed = 0f;
                float rotDuration = 0.5f;
                while (elapsed < rotDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / rotDuration);
                    float eased = Mathf.SmoothStep(0f, 1f, t);

                    cannonFrameRig.localRotation = Quaternion.Slerp(
                        _hookStartFrameRot, _hookTargetFrameRot, eased);

                    yield return null;
                }
                cannonFrameRig.localRotation = _hookTargetFrameRot;
            }

            // Phase 3: Smoothly transition camera back to its original position/rotation
            if (hookAttachCamera && cam != null)
            {
                cam.transform.SetParent(_cameraOriginalParent, true);
                Vector3 currentCamPos = cam.transform.position;
                Quaternion currentCamRot = cam.transform.rotation;
                
                elapsed = 0f;
                while (elapsed < hookCameraTransitionDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / hookCameraTransitionDuration);
                    float eased = Mathf.SmoothStep(0f, 1f, t);
                    
                    cam.transform.position = Vector3.Lerp(currentCamPos, _cameraOriginalWorldPos, eased);
                    cam.transform.rotation = Quaternion.Slerp(currentCamRot, _cameraOriginalWorldRot, eased);
                    
                    yield return null;
                }
                cam.transform.position = _cameraOriginalWorldPos;
                cam.transform.rotation = _cameraOriginalWorldRot;
            }

            // Mark hook as complete — unlocks Update() input processing
            hookComplete = true;

            // Show the tutorial hand now that the cannon has arrived
            if (UI.UIManager.Instance != null)
            {
                UI.UIManager.Instance.ShowTutorial();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (hookCameraStartMarker != null)
            {
                hookCameraStartPos = hookCameraStartMarker.position;
                hookCameraStartRot = hookCameraStartMarker.rotation;
            }
        }
#endif
    }
}
