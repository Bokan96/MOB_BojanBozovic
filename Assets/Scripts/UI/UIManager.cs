using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Tutorial Background")]
        [Tooltip("The Image used as the tutorial text background. Its alpha will fade in first.")]
        public Image tutorialTextBG;

        [Header("Tutorial Hand")]
        [Tooltip("The tutorial hand GameObject (must be inside a Canvas with an RectTransform).")]
        public RectTransform tutorialHand;

        [Header("Tutorial Text & Arrows")]
        [Tooltip("The tutorial text GameObject.")]
        public GameObject tutorialText;
        public RectTransform tutorialArrowLeft;
        public RectTransform tutorialArrowRight;

        [Tooltip("Distance the arrows move inward toward the text.")]
        public float arrowMoveDistance = 20f;
        public float arrowMoveSpeed = 4f;

        [Tooltip("Where the hand should settle and ping-pong from. Set X to the right-side position (e.g. 200). Y is vertical offset from screen center (e.g. -300 for bottom-center).")]
        public Vector2 pingPongCenter = new Vector2(200f, -300f);

        [Tooltip("How long it takes for the hand to slide from center to its starting ping-pong position.")]
        public float introSlideDuration = 0.6f;

        [Tooltip("Time for one full ping-pong sweep (right to left). Lower = faster swipe.")]
        public float pingPongDuration = 1.2f;

        [Tooltip("Z-axis rotation angle when sweeping left or right.")]
        public float pingPongRotationZ = 25f;

        [Tooltip("Easing strength of the ping-pong swing. SineInOut gives a natural feel.")]
        public AnimationCurve pingPongCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("End Screens")]
        public GameObject winCTA;
        public GameObject loseCTA;
        public RectTransform loseDownloadButton;
        public GameObject persistentCTA;

        private Coroutine _handRoutine;
        private Coroutine _loseAnimationRoutine;

        private Vector2 _originalArrowLeftPos;
        private Vector2 _originalArrowRightPos;
        private bool _tutorialActive;
        private bool _arrowsAnimating; // true during the intro pop, suppresses Update ping-pong movement

        // Cached original values captured in Awake
        private Color _bgOriginalColor;
        private Vector3 _textOriginalScale;
        private Vector3 _arrowLeftOriginalScale;
        private Vector3 _arrowRightOriginalScale;
        private Vector3 _handOriginalScale;
        private Vector2 _handOriginalPos;
        private Vector3 _downloadButtonOriginalScale;

        private void Awake()
        {
            Instance = this;

            if (winCTA != null) winCTA.SetActive(false);
            if (loseCTA != null) loseCTA.SetActive(false);
            if (persistentCTA != null) persistentCTA.SetActive(true);

            if (tutorialArrowLeft != null) _originalArrowLeftPos = tutorialArrowLeft.anchoredPosition;
            if (tutorialArrowRight != null) _originalArrowRightPos = tutorialArrowRight.anchoredPosition;

            // Cache original colours and scales from scene values
            if (tutorialTextBG != null)
            {
                _bgOriginalColor = tutorialTextBG.color;
            }
            if (tutorialText != null)
            {
                _textOriginalScale = tutorialText.transform.localScale;
            }
            if (tutorialArrowLeft != null)
            {
                _arrowLeftOriginalScale = tutorialArrowLeft.localScale;
            }
            if (tutorialArrowRight != null)
            {
                _arrowRightOriginalScale = tutorialArrowRight.localScale;
            }
            if (tutorialHand != null)
            {
                _handOriginalScale = tutorialHand.localScale;
                _handOriginalPos = tutorialHand.anchoredPosition;
            }

            if (loseDownloadButton != null)
            {
                _downloadButtonOriginalScale = loseDownloadButton.localScale;
            }

            // Tutorial starts hidden — it will be shown by CanonController
            // after the hook entrance animation completes.
            HideAllTutorialImmediate();
        }

        private void Update()
        {
            if (_tutorialActive && !_arrowsAnimating)
            {
                // Ping-pong Arrows position
                if (tutorialArrowLeft != null || tutorialArrowRight != null)
                {
                    float arrowT = Mathf.PingPong(Time.time * arrowMoveSpeed, 1f);
                    float easedArrowT = arrowT < 0.5f ? 4f * arrowT * arrowT * arrowT : 1f - Mathf.Pow(-2f * arrowT + 2f, 3f) / 2f;

                    if (tutorialArrowLeft != null)
                        tutorialArrowLeft.anchoredPosition = _originalArrowLeftPos + new Vector2(arrowMoveDistance * easedArrowT, 0f);

                    if (tutorialArrowRight != null)
                        tutorialArrowRight.anchoredPosition = _originalArrowRightPos + new Vector2(-arrowMoveDistance * easedArrowT, 0f);
                }
            }
        }

        // ──────────────── PUBLIC API ────────────────

        public void ShowTutorial()
        {
            if (_handRoutine != null) StopCoroutine(_handRoutine);
            _handRoutine = StartCoroutine(TutorialIntroSequence());
        }

        public void HideTutorial()
        {
            if (_handRoutine != null)
            {
                StopCoroutine(_handRoutine);
                _handRoutine = null;
            }

            HideAllTutorialImmediate();
            _tutorialActive = false;
        }

        public void ShowWinCTA()
        {
            if (winCTA != null)
            {
                StartCoroutine(ScalePopInRoutine(winCTA, 1.0f));
            }
        }

        private IEnumerator ScalePopInRoutine(GameObject ctaObject, float targetScale)
        {
            ctaObject.SetActive(true);
            ctaObject.transform.localScale = Vector3.zero;
            float elapsed = 0f;
            float duration = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                // Pop in with slight overshoot
                float c = t - 1f;
                float eased = c * c * ((1.70158f + 1f) * c + 1.70158f) + 1f;

                ctaObject.transform.localScale = Vector3.one * (eased * targetScale);
                yield return null;
            }
            ctaObject.transform.localScale = Vector3.one * targetScale;

            // If this is the Lose CTA, start the button pulse
            if (ctaObject == loseCTA && loseDownloadButton != null)
            {
                _loseAnimationRoutine = StartCoroutine(PulseDownloadButton());
            }
        }

        private IEnumerator PulseDownloadButton()
        {
            if (loseDownloadButton == null) yield break;

            float pulseSpeed = 4f;
            float pulseScale = 1.05f;
            Vector3 baseScale = loseDownloadButton.localScale;

            while (true)
            {
                float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
                float easedT = Mathf.SmoothStep(0f, 1f, t);
                loseDownloadButton.localScale = Vector3.Lerp(baseScale, baseScale * pulseScale, easedT);
                yield return null;
            }
        }

        public void ShowLoseCTA()
        {
            if (loseCTA != null)
            {
                StartCoroutine(ScalePopInRoutine(loseCTA, 0.9f));
                
                if (persistentCTA != null)
                {
                    StartCoroutine(FadeOutPersistentCTA());
                }
            }
        }

        private IEnumerator FadeOutPersistentCTA()
        {
            if (persistentCTA == null) yield break;

            CanvasGroup group = persistentCTA.GetComponent<CanvasGroup>();
            if (group == null) group = persistentCTA.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            float duration = 0.5f;
            float startAlpha = group.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }

            group.alpha = 0f;
            persistentCTA.SetActive(false);
        }

        public void OnCTAClicked()
        {
            if (Core.GameManager.Instance != null)
                Core.GameManager.Instance.InstallGame();
        }

        // ──────────────── HELPERS ────────────────

        private void HideAllTutorialImmediate()
        {
            if (tutorialTextBG != null)
            {
                Color c = _bgOriginalColor;
                c.a = 0f;
                tutorialTextBG.color = c;
                tutorialTextBG.gameObject.SetActive(false);
            }
            if (tutorialText != null)
            {
                tutorialText.transform.localScale = Vector3.zero;
                tutorialText.SetActive(false);
            }
            if (tutorialArrowLeft != null)
            {
                tutorialArrowLeft.localScale = Vector3.zero;
                tutorialArrowLeft.gameObject.SetActive(false);
            }
            if (tutorialArrowRight != null)
            {
                tutorialArrowRight.localScale = Vector3.zero;
                tutorialArrowRight.gameObject.SetActive(false);
            }
            if (tutorialHand != null)
            {
                tutorialHand.localScale = Vector3.zero;
                tutorialHand.gameObject.SetActive(false);
            }
        }

        // ──────────────── TUTORIAL INTRO SEQUENCE ────────────────

        /// <summary>
        /// Full sequential tutorial intro:
        /// 1. BG fades in (0.3s)
        /// 2. Text + Arrows scale in simultaneously (0.3s)
        /// 3. Hand pops in (scale 0 → overshoot → target) and then immediately starts ping-pong
        /// </summary>
        private IEnumerator TutorialIntroSequence()
        {
            _tutorialActive = true;
            _arrowsAnimating = true;

            // ── Step 1: Fade in BG ──
            if (tutorialTextBG != null)
            {
                tutorialTextBG.gameObject.SetActive(true);
                Color start = _bgOriginalColor; start.a = 0f;
                tutorialTextBG.color = start;

                float elapsed = 0f;
                float dur = 0.3f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / dur);
                    Color c = _bgOriginalColor;
                    c.a = Mathf.Lerp(0f, _bgOriginalColor.a, t);
                    tutorialTextBG.color = c;
                    yield return null;
                }
                tutorialTextBG.color = _bgOriginalColor;
            }

            // ── Step 2: Scale in Text + Arrows simultaneously (0.3s) ──
            if (tutorialText != null)
            {
                tutorialText.SetActive(true);
                tutorialText.transform.localScale = Vector3.zero;
            }
            if (tutorialArrowLeft != null)
            {
                tutorialArrowLeft.gameObject.SetActive(true);
                tutorialArrowLeft.localScale = Vector3.zero;
            }
            if (tutorialArrowRight != null)
            {
                tutorialArrowRight.gameObject.SetActive(true);
                tutorialArrowRight.localScale = Vector3.zero;
            }

            {
                float elapsed = 0f;
                float dur = 0.3f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / dur);
                    // Cubic ease-out gives a snappy pop feel
                    float eased = 1f - Mathf.Pow(1f - t, 3f);

                    if (tutorialText != null)
                        tutorialText.transform.localScale = Vector3.Lerp(Vector3.zero, _textOriginalScale, eased);
                    if (tutorialArrowLeft != null)
                        tutorialArrowLeft.localScale = Vector3.Lerp(Vector3.zero, _arrowLeftOriginalScale, eased);
                    if (tutorialArrowRight != null)
                        tutorialArrowRight.localScale = Vector3.Lerp(Vector3.zero, _arrowRightOriginalScale, eased);

                    yield return null;
                }
                // Snap to exact final scales
                if (tutorialText != null) tutorialText.transform.localScale = _textOriginalScale;
                if (tutorialArrowLeft != null) tutorialArrowLeft.localScale = _arrowLeftOriginalScale;
                if (tutorialArrowRight != null) tutorialArrowRight.localScale = _arrowRightOriginalScale;
            }

            // ── Step 3: Hand pop-in and slide ──
            // Arrow ping-pong can now run freely in Update
            _arrowsAnimating = false;
            
            // This coroutine now handles both scaling up and sliding to the center
            yield return StartCoroutine(HandPingPongLoop());
        }

        // ──────────────── HAND ANIMATION (PING-PONG) ────────────────

        private IEnumerator HandPingPongLoop()
        {
            if (tutorialHand == null) yield break;

            // Phase 1: Slide from current editor pos → pingPongCenter
            // AND scale up from 0 → overshoot → target (halved duration)
            Vector2 startPos = _handOriginalPos;
            float elapsed = 0f;
            Vector3 overshoot = _handOriginalScale * 1.25f;
            float scaleDuration = introSlideDuration * 0.5f; // Scaling is twice as fast

            tutorialHand.gameObject.SetActive(true);
            tutorialHand.anchoredPosition = startPos;
            tutorialHand.localScale = Vector3.zero;
            tutorialHand.localRotation = Quaternion.identity;

            while (elapsed < introSlideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / introSlideDuration);
                float tScale = Mathf.Clamp01(elapsed / scaleDuration);
                
                // Position movement
                float easedPos = 1f - Mathf.Pow(1f - t, 3f);
                tutorialHand.anchoredPosition = Vector2.Lerp(startPos, pingPongCenter, easedPos);

                // Scale pop-in (0 -> overshoot -> target)
                Vector3 scale;
                if (tScale < 0.6f)
                {
                    float subT = tScale / 0.6f;
                    float easedScale = 1f - Mathf.Pow(1f - subT, 2f); 
                    scale = Vector3.Lerp(Vector3.zero, overshoot, easedScale);
                }
                else
                {
                    float subT = Mathf.Clamp01((tScale - 0.6f) / 0.4f);
                    float easedScale = Mathf.SmoothStep(0f, 1f, subT);
                    scale = Vector3.Lerp(overshoot, _handOriginalScale, easedScale);
                }
                tutorialHand.localScale = scale;

                yield return null;
            }

            tutorialHand.anchoredPosition = pingPongCenter;
            tutorialHand.localScale = _handOriginalScale;

            // Phase 2: Ping-pong between (+X, Y) and (-X, Y) forever
            Vector2 posRight = pingPongCenter;
            Vector2 posLeft  = new Vector2(-pingPongCenter.x, pingPongCenter.y);

            float pingPongTimer = 0f;
            bool goingLeft = true;

            float startZRot = 0f;
            float targetZRot = pingPongRotationZ;

            while (true)
            {
                pingPongTimer += Time.deltaTime;
                float t = Mathf.Clamp01(pingPongTimer / pingPongDuration);
                float eased = pingPongCurve.Evaluate(t);

                tutorialHand.anchoredPosition = goingLeft
                    ? Vector2.Lerp(posRight, posLeft, eased)
                    : Vector2.Lerp(posLeft, posRight, eased);

                float rotDuration = pingPongDuration / 5f;
                float rotT = Mathf.Clamp01(pingPongTimer / rotDuration);
                float easedRot = rotT < 0.5f ? 4f * rotT * rotT * rotT : 1f - Mathf.Pow(-2f * rotT + 2f, 3f) / 2f;
                float currentZRot = Mathf.Lerp(startZRot, targetZRot, easedRot);

                tutorialHand.localRotation = Quaternion.Euler(0f, 0f, currentZRot);

                if (t >= 1f)
                {
                    tutorialHand.anchoredPosition = goingLeft ? posLeft : posRight;
                    goingLeft = !goingLeft;
                    pingPongTimer = 0f;

                    startZRot = currentZRot;
                    targetZRot = goingLeft ? pingPongRotationZ : -pingPongRotationZ;
                }

                yield return null;
            }
        }
    }
}
