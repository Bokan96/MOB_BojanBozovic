using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Tracks and displays the Fever Bar. Fills up each time the player shoots.
    /// When full, shows the "Release" text prompting the player to release their finger.
    /// 
    /// Setup in Unity:
    /// - Create a Canvas (Screen Space - Overlay).
    /// - Create 3 child Images stacked back-to-front:
    ///   1. Background (cannon_progressbar_background 1.png) — Image Type: Simple
    ///   2. Fill (cannon_progressbar_fill.png) — Image Type: Filled, Fill Method: Vertical, Fill Origin: Bottom
    ///   3. Frame (cannon_progressbar_frame.png) — Image Type: Simple
    /// - Create a child Image for the "Release" text (release_text.png).
    /// - Assign all 4 references in the Inspector.
    /// </summary>
    public class FeverBar : MonoBehaviour
    {
        public static FeverBar Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("The fill Image (Filled type, Vertical, Bottom origin)")]
        public Image fillImage;
        
        [Tooltip("The 'Release' text image — shown when bar is full")]
        public GameObject releaseTextObject;

        [Header("Settings")]
        [Tooltip("How much the bar fills per shot burst (0-1 range)")]
        public float fillPerShot = 0.05f;

        [Tooltip("Number of shots needed to fill the bar (auto-calculated from fillPerShot)")]
        public int shotsToFill = 20; // Informational — derived from fillPerShot

        [Header("Juice")]
        [Tooltip("The entire bar visual container to animate")]
        public Transform barVisualTransform;
        public float pulseScale = 1.08f;
        public float pulseSpeed = 12f;

        private float _currentFill;
        private bool _isFull;
        private Vector3 _barBaseScale;

        /// <summary>
        /// Returns true when the bar is completely full and the player can release for Big Mob.
        /// </summary>
        public bool IsFull => _isFull;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Mobs.MobSpawner.OnPlayerMobShot += HandlePlayerShot;
        }

        private void OnDisable()
        {
            Mobs.MobSpawner.OnPlayerMobShot -= HandlePlayerShot;
        }

        private void Start()
        {
            _currentFill = 0f;
            _isFull = false;

            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
            }

            if (releaseTextObject != null)
            {
                releaseTextObject.SetActive(false);
            }

            if (barVisualTransform != null)
            {
                _barBaseScale = barVisualTransform.localScale;
            }

            // Auto-calculate fillPerShot from shotsToFill for convenience
            if (shotsToFill > 0)
            {
                fillPerShot = 1f / shotsToFill;
            }
        }

        private void Update()
        {
            // Juicy pulse animation when the bar is full
            if (_isFull && barVisualTransform != null)
            {
                // Gentle breathing pulse using a sine wave
                float pulse = 1f + (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f) * (pulseScale - 1f);
                barVisualTransform.localScale = _barBaseScale * pulse;
            }
            else if (barVisualTransform != null)
            {
                // Smoothly recover to base scale when not full
                barVisualTransform.localScale = Vector3.Lerp(
                    barVisualTransform.localScale, _barBaseScale, Time.deltaTime * 10f);
            }
        }

        private void HandlePlayerShot()
        {
            if (_isFull) return; // Already full, waiting for release

            _currentFill = Mathf.Clamp01(_currentFill + fillPerShot);

            if (fillImage != null)
            {
                fillImage.fillAmount = _currentFill;
            }

            if (_currentFill >= 1f)
            {
                _isFull = true;

                if (releaseTextObject != null)
                {
                    releaseTextObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// Called externally (by CanonController) after the Big Mob has been fired.
        /// Resets the bar so it can fill up again.
        /// </summary>
        public void ResetBar()
        {
            _currentFill = 0f;
            _isFull = false;

            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
            }

            if (releaseTextObject != null)
            {
                releaseTextObject.SetActive(false);
            }
        }
    }
}
