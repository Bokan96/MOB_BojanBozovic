using UnityEngine;

namespace Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State")]
        public bool hasStarted = false;
        public bool hasEnded = false;

        [Header("Luna Playground Parameters")]
        public float MobSpeed = 6f;
        public float SpawnInterval = 1f;
        public int MultiplierAmount = 2;
        public int FeverShotsRequired = 20;
        public int BigMobHP = 5;
        public int BlockerHP = 10;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            #if LUNA_PLAYWORKS
            Luna.Unity.LifeCycle.GameStarted();
            Luna.Unity.LifeCycle.LevelStarted();
            #endif
        }

        private void Update()
        {
            if (!hasStarted && Input.GetMouseButtonDown(0))
            {
                hasStarted = true;
                
                if (UI.UIManager.Instance != null)
                {
                    UI.UIManager.Instance.HideTutorial();
                }
            }
        }

        public void WinGame()
        {
            if (hasEnded) return;
            hasEnded = true;

            #if LUNA_PLAYWORKS
            Luna.Unity.LifeCycle.GameEnded();
            #endif

            if (UI.UIManager.Instance != null)
            {
                UI.UIManager.Instance.ShowWinCTA();
            }
            
            // Cannon lerp forward
            CanonController canon = FindObjectOfType<CanonController>();
            if (canon != null)
            {
                canon.enabled = false; // Stop player from shooting/moving
                StartCoroutine(LerpCannonForward(canon.transform));
            }
        }

        public void LoseGame()
        {
            if (hasEnded) return;
            hasEnded = true;

            #if LUNA_PLAYWORKS
            Luna.Unity.LifeCycle.GameEnded();
            #endif

            if (UI.UIManager.Instance != null)
            {
                UI.UIManager.Instance.ShowLoseCTA();
            }
        }

        public void InstallGame()
        {
            #if LUNA_PLAYWORKS
            Luna.Unity.Playable.InstallFullGame();
            #else
            Debug.Log("CTA Clicked! Redirect to app store.");
            #endif
        }

        public void TrackGatePassed()
        {
            #if LUNA_PLAYWORKS
            Luna.Unity.Analytics.LogEvent("gate_passed", 0);
            #endif
        }

        public void TrackMobMultiplied()
        {
            #if LUNA_PLAYWORKS
            Luna.Unity.Analytics.LogEvent("mob_multiplied", 0);
            #endif
        }

        public void TrackTowerDestroyed()
        {
            #if LUNA_PLAYWORKS
            Luna.Unity.Analytics.LogEvent("tower_destroyed", 0);
            #endif
        }

        private System.Collections.IEnumerator LerpCannonForward(Transform cannon)
        {
            Vector3 startPos = cannon.position;
            Vector3 endPos = startPos + new Vector3(0, 0, 15f); // Move forward along Z
            float duration = 2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                cannon.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }
        }
    }
}
