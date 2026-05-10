using UnityEngine;

namespace Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State")]
        public bool hasStarted = false;
        public bool hasEnded = false;
        public bool isGameWon = false;

        [Header("Spawners")]
        [Tooltip("These game objects will be disabled on start and enabled once the player interacts with the game.")]

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

            Luna.Unity.LifeCycle.GameStarted();

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
            isGameWon = true;

            StartCoroutine(WinSequenceRoutine());
        }

        private System.Collections.IEnumerator WinSequenceRoutine()
        {
            // Trigger mob celebration jumps
            if (Mobs.BattleManager.Instance != null)
            {
                Mobs.BattleManager.Instance.TriggerVictoryCelebration();
            }

            // Wait a moment for the victory to sink in
            yield return new WaitForSeconds(1f);

            // Cannon Victory Sequence
            if (_cannon != null)
            {
                _cannon.isDead = true; // Stop player from shooting/moving
                _cannon.PlayVictoryAnimation();
            }

            // Wait for cannon animation to finish (1s delay + 3.5s animation)
            yield return new WaitForSeconds(3.5f);

            // Win CTA is no longer shown at the end, relying on persistent CTA instead.
            // if (UI.UIManager.Instance != null)
            // {
            //     UI.UIManager.Instance.ShowWinCTA();
            // }

            Luna.Unity.LifeCycle.GameEnded();
        }

        public void LoseGame(Mobs.Mob offendingMob = null)
        {
            if (hasEnded) return;
            hasEnded = true;

            // Play fail sound immediately
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayLoseSound();

            // Stop cannon input straight away
            if (_cannon != null)
                _cannon.isDead = true;

            StartCoroutine(LoseSequenceRoutine(offendingMob));
        }

        private System.Collections.IEnumerator LoseSequenceRoutine(Mobs.Mob offendingMob)
        {
            // 1. Unregister the offending mob from battle so it stops dying/being hit
            if (offendingMob != null && Mobs.BattleManager.Instance != null)
                Mobs.BattleManager.Instance.UnregisterMob(offendingMob, isEnemy: true);

            // 2. Drive mob toward the cannon (preserving mob's own Y)
            if (offendingMob != null && _cannon != null)
            {
                Vector3 cannonPos = _cannon.transform.position;
                float chargeSpeed = 10f;
                float arrivalSqrDist = 1.5f * 1.5f;

                // Freeze mob's own movement so we can drive it manually
                offendingMob.StartCharge();

                while (offendingMob != null && offendingMob.gameObject.activeSelf)
                {
                    Vector3 mobPos = offendingMob.transform.position;
                    Vector3 target = new Vector3(cannonPos.x, mobPos.y, cannonPos.z);

                    if ((mobPos - target).sqrMagnitude <= arrivalSqrDist)
                        break;

                    offendingMob.transform.position = Vector3.MoveTowards(mobPos, target, chargeSpeed * Time.deltaTime);
                    yield return null;
                }
            }

            // 3. Small dramatic pause before the boom
            yield return new WaitForSeconds(0.2f);

            // 4. Explode the cannon
            if (_cannon != null)
            {
                try 
                {
                    _cannon.ExplodeAndDie();
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Cannon explosion error: " + e.Message);
                }
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopMusic();
            }

            // 5. Wait for smoke to clear
            yield return new WaitForSeconds(1.5f);

            // 6. Show the Lose CTA
            if (UI.UIManager.Instance != null)
                UI.UIManager.Instance.ShowLoseCTA();

            Luna.Unity.LifeCycle.GameEnded();
        }



        public void InstallGame()
        {
            Luna.Unity.Playable.InstallFullGame();
        }

        public void TrackGatePassed()
        {
            Luna.Unity.Analytics.LogEvent("gate_passed", 0);
        }

        public void TrackMobMultiplied()
        {
            Luna.Unity.Analytics.LogEvent("mob_multiplied", 0);
        }

        public void TrackTowerDestroyed()
        {
            Luna.Unity.Analytics.LogEvent("tower_destroyed", 0);
        }

    }
}
