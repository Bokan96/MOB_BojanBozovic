using UnityEngine;
using Mobs;
using TMPro;

namespace Environment
{
    /// <summary>
    /// Multiplies player mobs passing through it.
    /// Operates without physics using GateBase AABB collision.
    /// </summary>
    public class MultiplierGate : GateBase
    {
        public enum MovementType
        {
            Linear,
            SineInOut,
            CubicInOut
        }

        [Header("Movement")]
        [Tooltip("If true, the gate will ping-pong along the X axis.")]
        public bool isMovable = false;
        [Tooltip("The destination X position. Starts from its initial position.")]
        public float finalX = 3f;
        [Tooltip("How fast the gate moves back and forth.")]
        public float moveSpeed = 1f;
        [Tooltip("The easing function for movement. CubicInOut keeps it at edges longer.")]
        public MovementType movementType = MovementType.CubicInOut;

        [Header("Multiplier Settings")]
        [Tooltip("How many extra mobs to spawn when one enters")]
        public int multiplierAmount = 2;
        
        [Tooltip("The spawner reference to pull extra mobs from")]
        public MobSpawner mobSpawner;

        [Header("Spread Variance")]
        [Tooltip("Max half-width of the spread zone (world units). Mobs will land within this range of the original mob's X.")]
        public float spreadWidth = 2.0f;
        [Tooltip("How strongly mobs cluster toward the center. Higher = tighter group (matches EnemySpawner centerBias).")]
        [Range(0.5f, 4f)]
        public float centerBias = 1.5f;
        [Tooltip("How aggressively the spread corrects when one side is getting overcrowded.")]
        [Range(0f, 2f)]
        public float balanceStrength = 0.6f;
        [Tooltip("Hard minimum spacing between any two spawned mobs (world units).")]
        public float minSpacing = 0.35f;

        [Header("Juice & Animation")]
        [Tooltip("The visual transform to scale for the bump animation (e.g. the 3D model/quad)")]
        public Transform visualTransform;
        public float bumpScaleMultiplier = 1.15f;
        public float bumpRecoverySpeed = 15f;
        
        [Tooltip("The TextMeshPro object to pop when a mob passes through")]
        public TextMeshPro multiplierText;
        public float textPopScaleMultiplier = 1.3f;
        public float textPopRecoverySpeed = 10f;
        
        private Vector3 _originalScale;
        private Vector3 _targetScale;
        private Vector3 _originalTextScale;
        
        private float _startX;
        private float _pingPongTimer;

        // Running spread bias — mirrors EnemySpawner._runningBias
        private float _spreadBias;
        private const float BIAS_DECAY = 0.85f;

        private void Start()
        {
            _startX = transform.position.x;

            if (visualTransform != null)
            {
                _originalScale = visualTransform.localScale;
                _targetScale = _originalScale;
            }

            if (multiplierText != null)
            {
                _originalTextScale = multiplierText.transform.localScale;
                
                // Force the text to render after all transparent shaders (Queue 3050)
                // so it never gets drawn over by the transparent pipe geometry.
                multiplierText.fontMaterial.renderQueue = 3050;
            }
        }

        protected override void Update()
        {
            if (isMovable)
            {
                _pingPongTimer += Time.deltaTime * moveSpeed;
                // Mathf.PingPong bounces between 0 and 1
                float t = Mathf.PingPong(_pingPongTimer, 1f);
                float easedT = t;
                
                switch (movementType)
                {
                    case MovementType.SineInOut:
                        easedT = -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                        break;
                    case MovementType.CubicInOut:
                        easedT = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                        break;
                    case MovementType.Linear:
                    default:
                        easedT = t;
                        break;
                }

                Vector3 pos = transform.position;
                pos.x = Mathf.Lerp(_startX, finalX, easedT);
                transform.position = pos;
            }

            // Critical: call base so GateBase can do the AABB checks!
            base.Update();

            // Handle the juicy bump recovery back to original scale
            if (visualTransform != null)
            {
                visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, _targetScale, Time.deltaTime * bumpRecoverySpeed);
            }

            // Handle text pop recovery
            if (multiplierText != null)
            {
                multiplierText.transform.localScale = Vector3.Lerp(multiplierText.transform.localScale, _originalTextScale, Time.deltaTime * textPopRecoverySpeed);
            }
        }

        protected override void OnMobEntered(Mob mob)
        {
            if (mobSpawner == null) return;

            // Trigger the juicy bump animation instantly
            if (visualTransform != null)
            {
                visualTransform.localScale = _originalScale * bumpScaleMultiplier;
            }

            // Trigger text pop animation instantly
            if (multiplierText != null)
            {
                multiplierText.transform.localScale = _originalTextScale * textPopScaleMultiplier;
            }

            if (Core.AudioManager.Instance != null)
            {
                Core.AudioManager.Instance.PlayGateMultiply();
            }

            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.TrackGatePassed();
                Core.GameManager.Instance.TrackMobMultiplied();
            }
            
            bool isBig = mob.IsBigMob;

            // Gather weighted-random target X positions for all spawned mobs
            // Uses the same bell-curve + balance-correction approach as EnemySpawner
            var spawnedXPositions = new System.Collections.Generic.List<float>(multiplierAmount);

            for (int i = 0; i < multiplierAmount; i++)
            {
                float targetX = GenerateSpreadX(mob.transform.position.x, isBig);

                // Rejection sampling: try up to 8 times to respect minSpacing
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    bool tooClose = false;
                    foreach (float prev in spawnedXPositions)
                    {
                        if (Mathf.Abs(targetX - prev) < minSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (!tooClose) break;
                    targetX = GenerateSpreadX(mob.transform.position.x, isBig);
                }

                spawnedXPositions.Add(targetX);
            }

            // Spawn all mobs at the calculated positions
            for (int i = 0; i < spawnedXPositions.Count; i++)
            {
                Mob newMob = isBig
                    ? mobSpawner.SpawnBigMob(mob.transform.position, applyBoost: false)
                    : mobSpawner.SpawnMob(mob.transform.position, mobSpawner.mobSpeed, applyBoost: false);

                if (newMob != null)
                {
                    newMob.ActivateSpread(spawnedXPositions[i]);

                    // CRITICAL: Permanently ignore all freshly spawned mobs so the gate
                    // can never be re-triggered by them, even if nudged backward.
                    IgnoreMob(newMob);
                }
            }

            // Recycle the original mob — it is replaced by the spread array
            mob.Recycle();
        }

        /// <summary>
        /// Generates a weighted-random global X position for a spawned mob.
        /// Bell-curve distribution (via averaging) + running bias correction.
        /// Mirrors the logic in EnemySpawner.GenerateBalancedX.
        /// </summary>
        private float GenerateSpreadX(float originX, bool isBig)
        {
            // Bell-curve: average multiple uniform samples → tends toward center
            float raw = 0f;
            int samples = Mathf.Max(1, Mathf.RoundToInt(centerBias * 2f));
            for (int s = 0; s < samples; s++)
                raw += Random.Range(-1f, 1f);
            raw /= samples;

            // Nudge away from the overcrowded side
            float correction = -_spreadBias * balanceStrength;
            raw = Mathf.Clamp(raw + correction, -1f, 1f);

            // Update running bias (exponential moving average)
            _spreadBias = _spreadBias * BIAS_DECAY + raw * (1f - BIAS_DECAY);

            // Big mobs need tighter clustering (40% less spread) to avoid stacking
            float effectiveWidth = isBig ? spreadWidth * 0.6f : spreadWidth;

            return Mathf.Clamp(originX + raw * effectiveWidth, -3f, 3f);
        }
    }
}
