using System.Collections.Generic;
using UnityEngine;

namespace Mobs
{
    /// <summary>
    /// Handles pure mathematical collision detection between Player and Enemy mobs.
    /// Replaces Physics collision for maximum Luna performance.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        /// <summary>
        /// Read-only access to active player mobs. Used by Gate scripts for
        /// mathematical trigger-zone checks (no physics colliders needed).
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<Mob> PlayerMobs => _activePlayerMobs;

        public float collisionRadius = 0.6f;
        
        [Tooltip("Wider collision radius for Big Mobs (they're visually larger)")]
        public float bigMobCollisionRadius = 1.2f;

        [Header("Lose Condition")]
        [Tooltip("If any enemy mob's Z position is less than or equal to this, the player loses.")]
        public float loseZThreshold = 1f;
        
        private float _sqrCollisionRadius;
        private float _sqrBigMobCollisionRadius;

        // Using simple Lists. We use a "swap-and-pop" method for O(1) removal.
        private List<Mob> _activePlayerMobs = new List<Mob>(200);
        private List<Mob> _activeEnemyMobs = new List<Mob>(200);

        private void Awake()
        {
            Instance = this;
            _sqrCollisionRadius = collisionRadius * collisionRadius;
            _sqrBigMobCollisionRadius = bigMobCollisionRadius * bigMobCollisionRadius;
        }

        public void RegisterMob(Mob mob, bool isEnemy)
        {
            if (isEnemy)
                _activeEnemyMobs.Add(mob);
            else
                _activePlayerMobs.Add(mob);
        }

        public void UnregisterMob(Mob mob, bool isEnemy)
        {
            var list = isEnemy ? _activeEnemyMobs : _activePlayerMobs;
            int index = list.IndexOf(mob);
            if (index >= 0)
            {
                // Swap with the last element and remove last (O(1) performance)
                list[index] = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
            }
        }

        public void TriggerVictoryCelebration()
        {
            for (int i = _activePlayerMobs.Count - 1; i >= 0; i--)
            {
                _activePlayerMobs[i].CelebrateAndDie();
            }
        }


        public void FreezeAllMobs()
        {
            // StartCharge stops normal movement and allows them to be driven manually,
            // or just stay frozen if they aren't driven manually.
            for (int i = 0; i < _activePlayerMobs.Count; i++)
            {
                if (_activePlayerMobs[i].IsActive) _activePlayerMobs[i].StartCharge();
            }
            for (int i = 0; i < _activeEnemyMobs.Count; i++)
            {
                if (_activeEnemyMobs[i].IsActive) _activeEnemyMobs[i].StartCharge();
            }
        }


        private void Update()
        {
            if (Core.GameManager.Instance != null && Core.GameManager.Instance.hasEnded) return;

            // 1. Check Lose Condition (Iterate over enemies)
            for (int e = _activeEnemyMobs.Count - 1; e >= 0; e--)
            {
                Mob eMob = _activeEnemyMobs[e];
                if (!eMob.IsActive) continue;

                if (eMob.transform.position.z <= loseZThreshold)
                {
                    if (Core.GameManager.Instance != null)
                    {
                        Core.GameManager.Instance.LoseGame(eMob);
                    }
                    return;
                }
            }

            // 2. Collision Detection
            // We iterate backwards because mobs might be removed (recycled) during the loop
            for (int p = _activePlayerMobs.Count - 1; p >= 0; p--)
            {
                Mob pMob = _activePlayerMobs[p];
                if (!pMob.IsActive) continue;
                
                Vector3 pPos = pMob.transform.position;

                // Big Mobs use a wider collision radius
                float sqrRadius = pMob.IsBigMob ? _sqrBigMobCollisionRadius : _sqrCollisionRadius;

                for (int e = _activeEnemyMobs.Count - 1; e >= 0; e--)
                {
                    Mob eMob = _activeEnemyMobs[e];
                    if (!eMob.IsActive) continue;

                    // Pure mathematical distance check (sqrMagnitude avoids expensive square root calculation)
                    if ((pPos - eMob.transform.position).sqrMagnitude < sqrRadius)
                    {
                        // Enemy always dies on contact (1 HP)
                        eMob.Die();

                        // Player mob takes a hit — Big Mobs survive multiple hits
                        bool playerSurvived = pMob.TakeHit();

                        if (!playerSurvived)
                        {
                            break; // Player mob is dead, stop checking enemies for this one
                        }
                        // If survived (Big Mob), continue checking more enemies this frame
                    }
                }
            }
        }
    }
}
