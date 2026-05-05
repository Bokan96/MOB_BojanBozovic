using System.Collections.Generic;
using UnityEngine;
using Mobs;

namespace Environment
{
    /// <summary>
    /// Base class for all physics-less gates.
    /// Uses mathematical bounding boxes (AABB) against active player mobs.
    /// </summary>
    public abstract class GateBase : MonoBehaviour
    {
        [Header("Gate Settings")]
        [Tooltip("Width of the gate on the X axis")]
        public float width = 3f;
        
        [Tooltip("Depth of the gate trigger zone on the Z axis")]
        public float zDepth = 1f;

        // Keep track of mobs that have already triggered this gate
        private HashSet<Mob> _triggeredMobs = new HashSet<Mob>();

        protected virtual void Update()
        {
            if (BattleManager.Instance == null) return;

            var playerMobs = BattleManager.Instance.PlayerMobs;
            
            // Define bounds based on current position
            Vector3 pos = transform.position;
            float minX = pos.x - (width / 2f);
            float maxX = pos.x + (width / 2f);
            float minZ = pos.z - (zDepth / 2f);
            float maxZ = pos.z + (zDepth / 2f);

            // Backwards iteration in case a mob is recycled during processing
            for (int i = playerMobs.Count - 1; i >= 0; i--)
            {
                Mob mob = playerMobs[i];
                if (!mob.IsActive) continue;

                Vector3 mobPos = mob.transform.position;

                // Check bounding box
                if (mobPos.x >= minX && mobPos.x <= maxX &&
                    mobPos.z >= minZ && mobPos.z <= maxZ)
                {
                    // If it hasn't triggered yet, trigger it
                    // HashSet.Add returns true if the element was added (wasn't already there)
                    if (_triggeredMobs.Add(mob))
                    {
                        OnMobEntered(mob);
                    }
                }
                else
                {
                    // If it's outside bounds, remove it from tracked mobs so that if this
                    // same mob object is recycled and shot again, it can trigger the gate anew.
                    if (_triggeredMobs.Contains(mob))
                    {
                        _triggeredMobs.Remove(mob);
                    }
                }
            }
        }

        /// <summary>
        /// Called once when a mob enters the mathematical bounds of this gate.
        /// </summary>
        protected abstract void OnMobEntered(Mob mob);

        /// <summary>
        /// Prevents a specific mob from triggering this gate. 
        /// Crucial for preventing infinite loops when a gate spawns new mobs inside its own trigger zone.
        /// </summary>
        public void IgnoreMob(Mob mob)
        {
            if (mob != null)
            {
                _triggeredMobs.Add(mob);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawCube(transform.position, new Vector3(width, 1f, zDepth));
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(width, 1f, zDepth));
        }
#endif
    }
}
