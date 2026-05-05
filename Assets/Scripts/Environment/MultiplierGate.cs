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
                // Target global X for this specific mob in the spread
                float targetGlobalX = mob.transform.position.x + offsetX;
                
                // Spawn without the cannon speed boost, at the EXACT position of the original mob
                Mob newMob = mobSpawner.SpawnMob(mob.transform.position, mobSpawner.mobSpeed, applyBoost: false);
                
                if (newMob != null)
                {
                    // Trigger the smooth horizontal spread animation
                    newMob.ActivateSpread(targetGlobalX);

                    // CRITICAL: Tell the gate to ignore this newly spawned mob so it doesn't 
                    // trigger the gate again and cause an infinite spawning loop!
                    IgnoreMob(newMob);
                }
            }

            // Recycle the original mob so it is cleanly replaced by the evenly spaced array
            mob.Recycle();
        }
    }
}
