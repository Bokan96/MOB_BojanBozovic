using System.Collections.Generic;
using UnityEngine;

namespace Mobs
{
    /// <summary>
    /// Spawns bursts of player mobs from the cannon.
    /// Uses object pooling and strips colliders to maximize performance.
    /// </summary>
    public class MobSpawner : MonoBehaviour
    {
        [Header("References")]
        public Transform shootPoint;
        public Mob mobPrefab;

        [Header("Settings")]
        public int poolSize = 200; // Increased to support Multiplier Gates
        public int burstCount = 3;
        public float burstSpread = 0.5f;
        public float mobSpeed = 8f;
        public float fireInterval = 0.35f;

        private Queue<Mob> _pool = new Queue<Mob>();
        private float _nextFireTime;

        private void Start()
        {
            // Pre-allocate the pool
            for (int i = 0; i < poolSize; i++)
            {
                Mob m = Instantiate(mobPrefab, transform);
                
                // CRITICAL for Luna: Strip all colliders and rigidbodies.
                // We use purely mathematical checks later, saving massive CPU overhead.
                foreach(var col in m.GetComponentsInChildren<Collider>()) Destroy(col);
                var rb = m.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
                
                m.gameObject.SetActive(false);
                _pool.Enqueue(m);
            }
        }

        /// <summary>
        /// Public method for Gates (like MultiplierGate and PipeGate) to spawn extra mobs.
        /// </summary>
        public Mob SpawnMob(Vector3 position, float speed)
        {
            Mob mob = GetMob();
            if (mob != null)
            {
                mob.Activate(position, speed, RecycleMob, isEnemy: false);
            }
            return mob;
        }

        public bool TryShoot()
        {
            if (Time.time < _nextFireTime) return false;
            
            _nextFireTime = Time.time + Mathf.Max(fireInterval, 0.15f);
            bool fired = false;

            for (int i = 0; i < burstCount; i++)
            {
                Mob mob = GetMob();
                if (mob == null) break;

                fired = true;

                // Fan out on the X axis
                float xOffset = 0f;
                if (burstCount > 1)
                {
                    float t = (float)i / (burstCount - 1);
                    xOffset = Mathf.Lerp(-burstSpread, burstSpread, t);
                }

                Vector3 pos = shootPoint.position + new Vector3(xOffset, 0f, 0f);
                
                // isEnemy is false because these are shot from the player cannon
                mob.Activate(pos, mobSpeed, RecycleMob, isEnemy: false);
            }

            return fired;
        }

        private Mob GetMob()
        {
            if (_pool.Count > 0) return _pool.Dequeue();
            return null; // Pool exhausted
        }

        private void RecycleMob(Mob mob)
        {
            _pool.Enqueue(mob);
        }
    }
}
