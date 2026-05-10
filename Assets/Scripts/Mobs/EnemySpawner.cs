using System.Collections.Generic;
using UnityEngine;

namespace Mobs
{
    /// <summary>
    /// Spawns enemy mobs that move towards the player.
    /// Uses object pooling and strips colliders to maximize performance.
    /// 
    /// All mobs spawn at the same origin point and then spread out horizontally
    /// using a self-balancing weighted random. Mobs gravitate towards the center
    /// and shift away from whichever side is overcrowded ("water flow" behaviour).
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("References")]
        public Transform spawnPoint;
        public Mob mobPrefab;

        [Header("Settings")]
        public int poolSize = 60;
        public float spawnInterval = 1.0f;
        public int spawnCountPerInterval = 2;
        public float mobSpeed = 6f;

        [Header("Spread Behaviour")]
        [Tooltip("Maximum distance from center a mob can spread to.")]
        public float maxSpreadWidth = 3f;
        [Tooltip("How strongly mobs are pulled towards center (higher = tighter center cluster).")]
        [Range(0.5f, 4f)]
        public float centerBias = 1.5f;
        [Tooltip("How aggressively the system corrects when one side is overcrowded (0 = none).")]
        [Range(0f, 2f)]
        public float balanceStrength = 0.8f;

        private Queue<Mob> _pool = new Queue<Mob>();
        private float _nextSpawnTime;

        // Running average of recent spawn X positions for self-balancing.
        // Positive = too many on the right, negative = too many on the left.
        private float _runningBias;
        private const float BIAS_DECAY = 0.92f; // How quickly old bias fades (0-1)

        private void Start()
        {
            // Pull A/B testable values from GameManager
            if (Core.GameManager.Instance != null)
            {
                spawnInterval = Core.GameManager.Instance.EnemySpawnRate;
                mobSpeed = Core.GameManager.Instance.EnemyMobSpeed;
            }

            // Pre-allocate the pool
            for (int i = 0; i < poolSize; i++)
            {
                Mob m = Instantiate(mobPrefab, transform);
                
                foreach(var col in m.GetComponentsInChildren<Collider>()) Destroy(col);
                var rb = m.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
                
                m.gameObject.SetActive(false);
                _pool.Enqueue(m);
            }
        }

        private void Update()
        {
            if (Time.time >= _nextSpawnTime)
            {
                _nextSpawnTime = Time.time + spawnInterval;
                SpawnEnemies();
            }
        }

        private void SpawnEnemies()
        {
            List<float> spawnedXPositions = new List<float>();

            for (int i = 0; i < spawnCountPerInterval; i++)
            {
                if (_pool.Count == 0) break; // Pool exhausted
                
                float targetX = 0f;
                bool validPositionFound = false;
                
                // Attempt to find a position that is at least 0.5 units away from other mobs spawned this interval
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    targetX = GenerateBalancedX(spawnPoint.position.x);
                    validPositionFound = true;
                    
                    foreach (float prevX in spawnedXPositions)
                    {
                        if (Mathf.Abs(targetX - prevX) < 0.5f)
                        {
                            validPositionFound = false;
                            break;
                        }
                    }
                    
                    if (validPositionFound) break;
                }
                
                spawnedXPositions.Add(targetX);

                Mob mob = _pool.Dequeue();

                // All mobs spawn at the exact same origin, but we add a tiny Z jitter
                // to prevent exact Z-clipping as they emerge
                Vector3 spawnPos = spawnPoint.position;
                spawnPos.z += Random.Range(-0.2f, 0.2f);
                
                mob.Activate(spawnPos, mobSpeed, RecycleMob, isEnemy: true);
                mob.ActivateSpread(targetX);
            }
        }

        /// <summary>
        /// Generates a random X position that:
        /// </summary>
        private float GenerateBalancedX(float originX)
        {
            // Generate a center-biased random value in [-1, 1].
            // By averaging multiple Random.Range calls we get a bell-curve shape.
            // centerBias controls how tight the cluster is.
            float raw = 0f;
            int samples = Mathf.Max(1, Mathf.RoundToInt(centerBias * 2f));
            for (int s = 0; s < samples; s++)
            {
                raw += Random.Range(-1f, 1f);
            }
            raw /= samples; // Averaging pulls values towards 0 (center)

            // Apply balance correction: shift away from the overcrowded side
            float correction = -_runningBias * balanceStrength;
            raw = Mathf.Clamp(raw + correction, -1f, 1f);

            // Map [-1, 1] to world X
            float targetX = originX + raw * maxSpreadWidth;

            // Update the running bias tracker (exponential moving average)
            _runningBias = _runningBias * BIAS_DECAY + raw * (1f - BIAS_DECAY);

            return targetX;
        }

        private void RecycleMob(Mob mob)
        {
            _pool.Enqueue(mob);
        }
    }
}
