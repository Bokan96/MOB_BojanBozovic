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
            
            bool isBig = mob.IsBigMob;
            
            // Calculate spacing for an even horizontal line
            float spacing = isBig ? 0.7f : 0.4f; // Distance between each spawned mob
            float totalWidth = (multiplierAmount - 1) * spacing;
            float startX = -(totalWidth / 2f);

            // Check if the entering mob is a Big Mob to preserve the type
            

            // Spawn the extra mobs
            for (int i = 0; i < multiplierAmount; i++)
            {
                float offsetX = startX + (i * spacing);
                // Target global X for this specific mob in the spread
                float targetGlobalX = mob.transform.position.x + offsetX;
                
                // Spawn the appropriate type without the cannon speed boost
                Mob newMob = isBig 
                    ? mobSpawner.SpawnBigMob(mob.transform.position, applyBoost: false)
                    : mobSpawner.SpawnMob(mob.transform.position, mobSpawner.mobSpeed, applyBoost: false);
                
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
