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

        public float collisionRadius = 0.6f;
        private float _sqrCollisionRadius;

        // Using simple Lists. We use a "swap-and-pop" method for O(1) removal.
        private List<Mob> _activePlayerMobs = new List<Mob>(200);
        private List<Mob> _activeEnemyMobs = new List<Mob>(200);

        private void Awake()
        {
            Instance = this;
            _sqrCollisionRadius = collisionRadius * collisionRadius;
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

        private void Update()
        {
            // We iterate backwards because mobs might be removed (recycled) during the loop
            for (int p = _activePlayerMobs.Count - 1; p >= 0; p--)
            {
                Mob pMob = _activePlayerMobs[p];
                if (!pMob.IsActive) continue;
                
                Vector3 pPos = pMob.transform.position;

                for (int e = _activeEnemyMobs.Count - 1; e >= 0; e--)
                {
                    Mob eMob = _activeEnemyMobs[e];
                    if (!eMob.IsActive) continue;

                    // Pure mathematical distance check (sqrMagnitude avoids expensive square root calculation)
                    if ((pPos - eMob.transform.position).sqrMagnitude < _sqrCollisionRadius)
                    {
                        // Collision detected! Destroy (recycle) both.
                        pMob.Recycle();
                        eMob.Recycle();
                        break; // Player mob is dead, stop checking enemies for this player mob
                    }
                }
            }
        }
    }
}
