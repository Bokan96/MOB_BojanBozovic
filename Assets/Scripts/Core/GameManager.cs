using UnityEngine;

namespace Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State")]
        public bool hasStarted = false;
        public bool hasEnded = false;

        [Header("Spawners")]
        [Tooltip("These game objects will be disabled on start and enabled once the player interacts with the game.")]
        #if LUNA_PLAYWORKS
        [Luna.Unity.HideInPlayground]
        #endif
        [SerializeField] private GameObject[] delayedSpawners;

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

        private CanonController _cannon;

        private void Start()
        {
            if (delayedSpawners != null)
            {
                foreach (var spawner in delayedSpawners)
                {
                    if (spawner != null) spawner.SetActive(false);
                }
            }

            #if LUNA_PLAYWORKS
            Luna.Unity.LifeCycle.GameStarted();
            Luna.Unity.LifeCycle.LevelStarted();
            #endif
        }

        private void Update()
        {
            // Lazy-cache cannon reference
            if (_cannon == null)
                _cannon = FindObjectOfType<CanonController>();

            // Don't allow game start until the hook intro animation is done
            if (_cannon != null && !_cannon.hookComplete) return;

            if (!hasStarted && Input.GetMouseButtonDown(0))
            {
                hasStarted = true;
                
                if (UI.UIManager.Instance != null)
                {
                    UI.UIManager.Instance.HideTutorial();
                }

                if (delayedSpawners != null)
                {
                    foreach (var spawner in delayedSpawners)
                    {
                        if (spawner != null) spawner.SetActive(true);
                    }
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
            if (_cannon != null)
            {
                _cannon.enabled = false; // Stop player from shooting/moving
                StartCoroutine(LerpCannonForward(_cannon.transform));
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
