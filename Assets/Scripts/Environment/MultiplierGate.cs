using UnityEngine;
using Mobs;

namespace Environment
{
    /// <summary>
    /// Multiplies player mobs passing through it.
    /// Operates without physics using GateBase AABB collision.
    /// </summary>
    public class MultiplierGate : GateBase
    {
        [Header("Multiplier Settings")]
        [Tooltip("How many extra mobs to spawn when one enters")]
        public int multiplierAmount = 2;
        
        [Tooltip("The spawner reference to pull extra mobs from")]
        public MobSpawner mobSpawner;

        [Header("Juice & Animation")]
        [Tooltip("The visual transform to scale for the bump animation (e.g. the 3D model/quad)")]
        public Transform visualTransform;
        public float bumpScaleMultiplier = 1.15f;
        public float bumpRecoverySpeed = 15f;
        
        private Vector3 _originalScale;
        private Vector3 _targetScale;

        private void Start()
        {
            if (visualTransform != null)
            {
                _originalScale = visualTransform.localScale;
                _targetScale = _originalScale;
            }
        }

        protected override void Update()
        {
            // Critical: call base so GateBase can do the AABB checks!
            base.Update();

            // Handle the juicy bump recovery back to original scale
            if (visualTransform != null)
            {
                visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, _targetScale, Time.deltaTime * bumpRecoverySpeed);
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

            // Calculate spacing for an even horizontal line
            float spacing = 0.4f; // Distance between each spawned mob
            float totalWidth = (multiplierAmount - 1) * spacing;
            float startX = -(totalWidth / 2f);

            // Spawn the extra mobs
            for (int i = 0; i < multiplierAmount; i++)
            {
                float offsetX = startX + (i * spacing);
                // Center the spread exactly on the mob that triggered the gate
                // This ensures they share the exact same global Y and Z axis!
                Vector3 spawnPos = mob.transform.position + new Vector3(offsetX, 0f, 0f);
                
                // Spawn using the cannon's configured mob speed
                Mob newMob = mobSpawner.SpawnMob(spawnPos, mobSpawner.mobSpeed);
                
                // CRITICAL: Tell the gate to ignore this newly spawned mob so it doesn't 
                // trigger the gate again and cause an infinite spawning loop!
                if (newMob != null)
                {
                    IgnoreMob(newMob);
                }
            }

            // Recycle the original mob so it is cleanly replaced by the evenly spaced array
            mob.Recycle();
        }
    }
}
