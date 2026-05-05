using System.Collections.Generic;
using UnityEngine;

namespace Mobs
{
    /// <summary>
    /// Spawns enemy mobs that move towards the player.
    /// Uses object pooling and strips colliders to maximize performance.
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
        public float spawnSpread = 0.5f;
        public float mobSpeed = 6f;

        private Queue<Mob> _pool = new Queue<Mob>();
        private float _nextSpawnTime;

        private void Start()
        {
            // Pre-allocate the pool
            for (int i = 0; i < poolSize; i++)
            {
                Mob m = Instantiate(mobPrefab, transform);
                
                // CRITICAL for Luna: Strip all colliders and rigidbodies.
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
            for (int i = 0; i < spawnCountPerInterval; i++)
            {
                if (_pool.Count == 0) break; // Pool exhausted
                
                Mob mob = _pool.Dequeue();

                // Fan out on the X axis
                float xOffset = 0f;
                if (spawnCountPerInterval > 1)
                {
                    float t = (float)i / (spawnCountPerInterval - 1);
                    xOffset = Mathf.Lerp(-spawnSpread, spawnSpread, t);
                }

                Vector3 pos = spawnPoint.position + new Vector3(xOffset, 0f, 0f);
                
                // isEnemy is true because these are shot from the enemy tower
                mob.Activate(pos, mobSpeed, RecycleMob, isEnemy: true);
            }
        }

        private void RecycleMob(Mob mob)
        {
            _pool.Enqueue(mob);
        }
    }
}
