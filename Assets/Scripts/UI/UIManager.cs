using System.Collections;
using UnityEngine;

namespace UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

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

        private Coroutine _handRoutine;
        
        private Vector2 _originalArrowLeftPos;
        private Vector2 _originalArrowRightPos;
        private bool _tutorialActive;

        private void Awake()
        {
            Instance = this;

            if (winCTA != null) winCTA.SetActive(false);
            if (loseCTA != null) loseCTA.SetActive(false);

            if (tutorialArrowLeft != null) _originalArrowLeftPos = tutorialArrowLeft.anchoredPosition;
            if (tutorialArrowRight != null) _originalArrowRightPos = tutorialArrowRight.anchoredPosition;

            ShowTutorial();
        }

        private void Update()
        {
            if (_tutorialActive)
            {
                // Ping-pong Arrows
                if (tutorialArrowLeft != null || tutorialArrowRight != null)
                {
                    // Ping pong between 0 and 1
                    float arrowT = Mathf.PingPong(Time.time * arrowMoveSpeed, 1f);
                    float easedArrowT = arrowT < 0.5f ? 4f * arrowT * arrowT * arrowT : 1f - Mathf.Pow(-2f * arrowT + 2f, 3f) / 2f;

                    if (tutorialArrowLeft != null)
                    {
                        // Move inward (to the right, +X)
                        tutorialArrowLeft.anchoredPosition = _originalArrowLeftPos + new Vector2(arrowMoveDistance * easedArrowT, 0f);
                    }
                    if (tutorialArrowRight != null)
                    {
                        // Move inward (to the left, -X)
                        tutorialArrowRight.anchoredPosition = _originalArrowRightPos + new Vector2(-arrowMoveDistance * easedArrowT, 0f);
                    }
                }
            }
        }

        // ──────────────── PUBLIC API ────────────────

        public void ShowTutorial()
        {
            if (tutorialHand != null)
            {
                tutorialHand.gameObject.SetActive(true);

                if (_handRoutine != null) StopCoroutine(_handRoutine);
                _handRoutine = StartCoroutine(HandAnimation());
            }

            if (tutorialText != null) tutorialText.SetActive(true);
            if (tutorialArrowLeft != null) tutorialArrowLeft.gameObject.SetActive(true);
            if (tutorialArrowRight != null) tutorialArrowRight.gameObject.SetActive(true);
            
            _tutorialActive = true;
        }

        public void HideTutorial()
        {
            if (_handRoutine != null)
            {
                StopCoroutine(_handRoutine);
                _handRoutine = null;
            }

            if (tutorialHand != null) tutorialHand.gameObject.SetActive(false);
            if (tutorialText != null) tutorialText.SetActive(false);
            if (tutorialArrowLeft != null) tutorialArrowLeft.gameObject.SetActive(false);
            if (tutorialArrowRight != null) tutorialArrowRight.gameObject.SetActive(false);
            
            _tutorialActive = false;
        }

        public void ShowWinCTA()
        {
            if (winCTA != null) winCTA.SetActive(true);
        }

        public void ShowLoseCTA()
        {
            if (loseCTA != null) loseCTA.SetActive(true);
        }

        public void OnCTAClicked()
        {
            if (Core.GameManager.Instance != null)
                Core.GameManager.Instance.InstallGame();
        }

        // ──────────────── HAND ANIMATION ────────────────

        private IEnumerator HandAnimation()
        {
            // Phase 1: Slide from screen center (0, 0) → pingPongCenter
            Vector2 startPos = Vector2.zero;
            float elapsed = 0f;

            tutorialHand.anchoredPosition = startPos;

            while (elapsed < introSlideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / introSlideDuration);
                // Cubic ease-out so it decelerates as it arrives
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                tutorialHand.anchoredPosition = Vector2.Lerp(startPos, pingPongCenter, eased);
                yield return null;
            }

            tutorialHand.anchoredPosition = pingPongCenter;

            // Phase 2: Ping-pong between (+X, Y) and (-X, Y) forever
            // pingPongCenter = (X, Y) → mirror = (-X, Y)
            Vector2 posRight = pingPongCenter;
            Vector2 posLeft  = new Vector2(-pingPongCenter.x, pingPongCenter.y);

            float pingPongTimer = 0f;
            bool goingLeft = true; // starts by sweeping left
            
            float startZRot = 0f; // Initially starts straight
            float targetZRot = pingPongRotationZ; // Moving left first -> +25

            while (true)
            {
                pingPongTimer += Time.deltaTime;
                float t = Mathf.Clamp01(pingPongTimer / pingPongDuration);
                float eased = pingPongCurve.Evaluate(t);

                tutorialHand.anchoredPosition = goingLeft
                    ? Vector2.Lerp(posRight, posLeft, eased)
                    : Vector2.Lerp(posLeft, posRight, eased);

                // Rotation over the first 1/5th of the movement
                float rotDuration = pingPongDuration / 5f;
                float rotT = Mathf.Clamp01(pingPongTimer / rotDuration);
                
                // Cubic ease in-out for rotation
                float easedRot = rotT < 0.5f ? 4f * rotT * rotT * rotT : 1f - Mathf.Pow(-2f * rotT + 2f, 3f) / 2f;
                float currentZRot = Mathf.Lerp(startZRot, targetZRot, easedRot);
                
                tutorialHand.localRotation = Quaternion.Euler(0f, 0f, currentZRot);

                if (t >= 1f)
                {
                    // Snap to target and reverse
                    tutorialHand.anchoredPosition = goingLeft ? posLeft : posRight;
                    goingLeft = !goingLeft;
                    pingPongTimer = 0f;
                    
                    // Setup rotation for the new direction
                    startZRot = currentZRot;
                    targetZRot = goingLeft ? pingPongRotationZ : -pingPongRotationZ;
                }

                yield return null;
            }
        }
    }
}
