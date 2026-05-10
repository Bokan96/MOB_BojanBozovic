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

        [Header("Gradient (Red to Green)")]
        [Tooltip("Gradient for the fill meter color based on how full it is.")]
        public Gradient fillGradient;

        [Header("Offscreen Clamping")]
        [Tooltip("Cannon X at which UI starts shifting right")]
        public float cannonXStartClamp = -1.4f;
        [Tooltip("Cannon X at which UI reaches max shift")]
        public float cannonXEndClamp = -2.3f;
        [Tooltip("Maximum pixels to shift the UI to the right")]
        public float maxUiShift = 37f;

        private float _targetFill;
        private float _currentFill;
        private bool _isFull;
        private Vector3 _barBaseScale;
        private bool _isVisible;

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

        private Vector3 _barOriginalLocalPos;
        private Vector3 _textOriginalLocalPos;
        private Core.CanonController _cannon;

        private void Start()
        {
            _targetFill = 0f;
            _currentFill = 0f;
            _isFull = false;
            _isVisible = false;

            _cannon = FindObjectOfType<Core.CanonController>();

            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
                if (fillGradient != null) fillImage.color = fillGradient.Evaluate(0f);
            }

            if (releaseTextObject != null)
            {
                _textOriginalLocalPos = releaseTextObject.transform.localPosition;
                releaseTextObject.SetActive(false);
            }

            if (barVisualTransform != null)
            {
                _barOriginalLocalPos = barVisualTransform.localPosition;
                _barBaseScale = barVisualTransform.localScale;
                barVisualTransform.gameObject.SetActive(false); // Hide during intro
            }

            // Auto-calculate fillPerShot from shotsToFill for convenience
            if (shotsToFill > 0)
            {
                fillPerShot = 1f / shotsToFill;
            }
        }

        private void Update()
        {
            // 1. Check if we should reveal the fever bar (after intro completes)
            if (!_isVisible && Core.GameManager.Instance != null && Core.GameManager.Instance.hasStarted)
            {
                _isVisible = true;
                if (barVisualTransform != null) barVisualTransform.gameObject.SetActive(true);
            }

            // 2. Smoothly lerp the fill amount (duration ~0.05s)
            if (_currentFill != _targetFill)
            {
                // To finish in roughly 0.05s, lerp very fast
                _currentFill = Mathf.MoveTowards(_currentFill, _targetFill, Time.deltaTime / 0.1f);
                if (fillImage != null)
                {
                    fillImage.fillAmount = _currentFill;
                    if (fillGradient != null)
                    {
                        fillImage.color = fillGradient.Evaluate(_currentFill);
                    }
                }
            }

            // 3. UI Offscreen clamping logic (counteracts cannon movement leftwards)
            if (_cannon != null)
            {
                float cannonX = _cannon.transform.position.x;
                // t=0 when cannon >= start, t=1 when cannon <= end
                float t = Mathf.InverseLerp(cannonXStartClamp, cannonXEndClamp, cannonX);
                // Shift UI right by up to maxUiShift pixels
                float shiftX = Mathf.Lerp(0f, maxUiShift, t);

                if (barVisualTransform != null)
                {
                    Vector3 barPos = _barOriginalLocalPos;
                    barPos.x += shiftX;
                    barVisualTransform.localPosition = barPos;
                }

                if (releaseTextObject != null)
                {
                    Vector3 textPos = _textOriginalLocalPos;
                    textPos.x += shiftX;
                    releaseTextObject.transform.localPosition = textPos;
                }
            }

            // 4. Juicy pulse animation when the bar is full
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

            _targetFill = Mathf.Clamp01(_targetFill + fillPerShot);

            if (_targetFill >= 1f)
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
            _targetFill = 0f;
            _currentFill = 0f;
            _isFull = false;

            if (fillImage != null)
            {
                fillImage.fillAmount = 0f;
                if (fillGradient != null) fillImage.color = fillGradient.Evaluate(0f);
            }

            if (releaseTextObject != null)
            {
                releaseTextObject.SetActive(false);
            }
        }
    }
}
