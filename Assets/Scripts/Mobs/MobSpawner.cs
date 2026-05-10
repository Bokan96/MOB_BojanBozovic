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
        [Tooltip("Optional. If set, mobs will lerp down to this Y position when shot.")]
        public Transform landingPointMob;
        public Mob mobPrefab;

        [Header("Big Mob")]
        [Tooltip("Prefab for the Big Mob (uses Big Minion sprite)")]
        public Mob bigMobPrefab;
        [Tooltip("Optional. If set, Big Mobs will lerp down to this Y position when shot.")]
        public Transform landingPointBigMob;
        public int bigMobPoolSize = 5;
        public int bigMobHitPoints = 5;

        [Header("Settings")]
        public int poolSize = 200; // Increased to support Multiplier Gates
        public int burstCount = 3;
        public float burstSpread = 0.5f;
        public float mobSpeed = 8f;
        public float fireInterval = 0.35f;

        /// <summary>
        /// Fired every time the cannon successfully shoots a burst.
        /// Subscribers (like FeverBar) use this to track shot progress.
        /// </summary>
        public static System.Action OnPlayerMobShot;

        private Queue<Mob> _pool = new Queue<Mob>();
        private Queue<Mob> _bigMobPool = new Queue<Mob>();
        private float _nextFireTime;

        private void Start()
        {
            if (Core.GameManager.Instance != null)
            {
                bigMobHitPoints = Core.GameManager.Instance.BigMobHP;
                mobSpeed = Core.GameManager.Instance.FriendlyMobSpeed;
                fireInterval = Core.GameManager.Instance.CannonFireRate;
            }

            // Pre-allocate the normal mob pool
            for (int i = 0; i < poolSize; i++)
            {
                Mob m = Instantiate(mobPrefab, transform);
                
                // We use purely mathematical checks later, saving massive CPU overhead.
                foreach(var col in m.GetComponentsInChildren<Collider>()) Destroy(col);
                var rb = m.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
                
                m.gameObject.SetActive(false);
                _pool.Enqueue(m);
            }

            // Pre-allocate the Big Mob pool
            if (bigMobPrefab != null)
            {
                for (int i = 0; i < bigMobPoolSize; i++)
                {
                    Mob m = Instantiate(bigMobPrefab, transform);
                    foreach(var col in m.GetComponentsInChildren<Collider>()) Destroy(col);
                    var rb = m.GetComponent<Rigidbody>();
                    if (rb != null) Destroy(rb);
                    m.gameObject.SetActive(false);
                    _bigMobPool.Enqueue(m);
                }
            }
        }

        /// <summary>
        /// Public method for Gates (like MultiplierGate and PipeGate) to spawn extra mobs.
        /// </summary>
        public Mob SpawnMob(Vector3 position, float speed, bool applyBoost = true)
        {
            Mob mob = GetMob();
            if (mob != null)
            {
                mob.Activate(position, speed, RecycleMob, isEnemy: false, applyBoost: applyBoost);
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
                
                bool doYLerp = landingPointMob != null;
                float targetY = doYLerp ? landingPointMob.position.y : 0f;

                // isEnemy is false because these are shot from the player cannon
                mob.Activate(pos, mobSpeed, RecycleMob, isEnemy: false, applyBoost: true, doYLerp: doYLerp, targetY: targetY);
            }

            if (fired)
            {
                OnPlayerMobShot?.Invoke();
            }

            return fired;
        }

        /// <summary>
        /// Spawns a Big Mob. Called by CanonController when Fever Bar is full, or by Multiplier Gates.
        /// </summary>
        public Mob SpawnBigMob(Vector3 position, bool applyBoost = true)
        {
            if (_bigMobPool.Count == 0) return null;

            bool doYLerp = applyBoost && landingPointBigMob != null;
            float targetY = doYLerp ? landingPointBigMob.position.y : 0f;

            Mob mob = _bigMobPool.Dequeue();
            mob.Activate(position, mobSpeed, RecycleBigMob, isEnemy: false, applyBoost: applyBoost, doYLerp: doYLerp, targetY: targetY);
            mob.MakeBig(bigMobHitPoints);
            return mob;
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

        private void RecycleBigMob(Mob mob)
        {
            _bigMobPool.Enqueue(mob);
        }
    }
}
