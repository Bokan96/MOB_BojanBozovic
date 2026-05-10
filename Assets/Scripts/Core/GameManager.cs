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

        [Header("Friendly Units")]
        [LunaPlaygroundField("Friendly Mob Speed", 1, "Friendly Units")]
        [Range(1f, 8f), LunaPlaygroundFieldStep(0.1f)]
        public float FriendlyMobSpeed = 4f;
        [LunaPlaygroundField("Cannon Fire Rate", 2, "Friendly Units")]
        [Range(0.1f, 1f), LunaPlaygroundFieldStep(0.01f)]
        public float CannonFireRate = 0.2f;
        [LunaPlaygroundField("Fever Charge Meter", 3, "Friendly Units")]
        [Range(5, 40), LunaPlaygroundFieldStep(1)]
        public int FeverChargeMeter = 20;
        [LunaPlaygroundField("Big Mob HP", 4, "Friendly Units")]
        [Range(2, 15), LunaPlaygroundFieldStep(1)]
        public int BigMobHP = 5;
        [LunaPlaygroundField("Cannon Move Speed", 5, "Friendly Units")]
        [Range(1f, 10f), LunaPlaygroundFieldStep(0.5f)]
        public float CannonMoveSpeed = 5f;

        [Header("Enemy Units")]
        [LunaPlaygroundField("Enemy Mob Speed", 1, "Enemy Units")]
        [Range(1f, 8f), LunaPlaygroundFieldStep(0.1f)]
        public float EnemyMobSpeed = 4f;
        [LunaPlaygroundField("Enemy Spawn Rate", 2, "Enemy Units")]
        [Range(0.1f, 1f), LunaPlaygroundFieldStep(0.01f)]
        public float EnemySpawnRate = 0.2f;
        [LunaPlaygroundField("Tower HP", 3, "Enemy Units")]
        [Range(1, 100), LunaPlaygroundFieldStep(1)]
        public int TowerHP = 50;

        [Header("Level / Obstacles")]
        [LunaPlaygroundField("Gate Multiplier", 1, "Level / Obstacles")]
        [Range(2, 10), LunaPlaygroundFieldStep(1)]
        public int GateMultiplier = 2;
        [LunaPlaygroundField("Moving Gate Multiplier", 2, "Level / Obstacles")]
        [Range(2, 10), LunaPlaygroundFieldStep(1)]
        public int MovingGateMultiplier = 3;
        [LunaPlaygroundField("Right Blocker HP", 3, "Level / Obstacles")]
        public int RightBlockerHP = 999;

        [Header("Visuals (A/B Testing)")]
        [LunaPlaygroundField("Friendly Color", 1, "Visuals")]
        public Color FriendlyColor = new Color(0.0f, 0.64f, 1.0f); // 00A3FF
        [LunaPlaygroundField("Friendly Accent Color", 2, "Visuals")]
        public Color FriendlyAccentColor = new Color(0.0f, 0.31f, 1.0f); // 0050FF
        [SerializeField] private Material friendlyMaterial;
        [SerializeField] private Material friendlyMobMaterial;

        [Space]
        [LunaPlaygroundField("Enemy Color", 3, "Visuals")]
        public Color EnemyColor = new Color(1.0f, 0.19f, 0.19f); // FF3131
        [LunaPlaygroundField("Enemy Accent Color", 4, "Visuals")]
        public Color EnemyAccentColor = new Color(0.61f, 0.0f, 0.0f); // 9B0000
        [SerializeField] private Material enemyMaterial;
        [SerializeField] private Material enemyMobMaterial;

        [Space]
        [LunaPlaygroundField("Ground Color Top", 5, "Visuals")]
        public Color GroundColorTop = new Color(1.0f, 0.77f, 0.0f); // FFC500
        [LunaPlaygroundField("Ground Color Bottom", 6, "Visuals")]
        public Color GroundColorBottom = new Color(0.36f, 0.6f, 0.74f); // 5D98BC
        [SerializeField] private Material groundMaterial;

        private void Awake()
        {
            Instance = this;
        }

        private CanonController _cannon;

        private void Start()
        {
            // Apply Playground colors
            if (friendlyMaterial != null)
            {
                friendlyMaterial.SetColor("_ColorTop", FriendlyColor);
                friendlyMaterial.SetColor("_ColorBottom", FriendlyAccentColor);
            }
            if (friendlyMobMaterial != null)
            {
                friendlyMobMaterial.SetColor("_ColorTop", FriendlyColor);
                friendlyMobMaterial.SetColor("_ColorBottom", FriendlyAccentColor);
            }

            if (enemyMaterial != null)
            {
                enemyMaterial.SetColor("_ColorTop", EnemyColor);
                enemyMaterial.SetColor("_ColorBottom", EnemyAccentColor);
            }
            if (enemyMobMaterial != null)
            {
                enemyMobMaterial.SetColor("_ColorTop", EnemyColor);
                enemyMobMaterial.SetColor("_ColorBottom", EnemyAccentColor);
            }

            if (groundMaterial != null)
            {
                groundMaterial.SetColor("_ColorTop", GroundColorTop);
                groundMaterial.SetColor("_ColorBottom", GroundColorBottom);
            }

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
                TrackGameStarted();
                
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
            TrackGameWon();

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

            // Show the Win CTA on the whole screen as requested
            if (UI.UIManager.Instance != null)
            {
                UI.UIManager.Instance.ShowWinCTA();
                // Show the tutorial hand again as requested for the "next level"
                UI.UIManager.Instance.ShowTutorial();
            }

            Luna.Unity.LifeCycle.GameEnded();
        }

        public void LoseGame(Mobs.Mob offendingMob = null)
        {
            if (hasEnded) return;
            hasEnded = true;
            TrackGameLost();

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
            // Freeze all mobs in the scene immediately
            if (Mobs.BattleManager.Instance != null)
                Mobs.BattleManager.Instance.FreezeAllMobs();

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
            Luna.Unity.Analytics.LogEvent("cta_click", 0);
            Luna.Unity.Playable.InstallFullGame();
        }

        // ── Luna Analytics (Debounced) ──
        private float _lastGatePassedTime;
        private float _lastGateMovingPassedTime;
        private const float ANALYTICS_COOLDOWN = 0.5f;

        public void TrackGatePassed()
        {
            if (Time.time - _lastGatePassedTime < ANALYTICS_COOLDOWN) return;
            _lastGatePassedTime = Time.time;
            Luna.Unity.Analytics.LogEvent("gate_passed", 0);
        }

        public void TrackGateMovingPassed()
        {
            if (Time.time - _lastGateMovingPassedTime < ANALYTICS_COOLDOWN) return;
            _lastGateMovingPassedTime = Time.time;
            Luna.Unity.Analytics.LogEvent("gate_moving_passed", 0);
        }

        public void TrackTowerDestroyed()
        {
            Luna.Unity.Analytics.LogEvent("tower_destroyed", 0);
        }

        public void TrackPipeEntered()
        {
            Luna.Unity.Analytics.LogEvent("pipe_entered", 0);
        }

        public void TrackEdgeReached()
        {
            Luna.Unity.Analytics.LogEvent("edge_reached", 0);
        }

        public void TrackGameStarted()
        {
            Luna.Unity.Analytics.LogEvent("game_start", 0);
        }

        public void TrackFeverActivated()
        {
            Luna.Unity.Analytics.LogEvent("fever_activated", 0);
        }

        public void TrackGameWon()
        {
            Luna.Unity.Analytics.LogEvent("game_win", 0);
        }

        public void TrackGameLost()
        {
            Luna.Unity.Analytics.LogEvent("game_lose", 0);
        }

    }
}
