using UnityEngine;
using Mobs;

namespace Environment
{
    /// <summary>
    /// A gate that blocks mobs, takes damage, and destroys itself at 0 HP.
    /// Operates without physics using GateBase AABB collision.
    /// </summary>
    public class BlockerGate : GateBase
    {
        [Header("Blocker Settings")]
        [Tooltip("How many mobs it takes to destroy this blocker")]
        public int health = 10;
        
        [Header("Juice & Animation")]
        [Tooltip("The visual transform to scale for the hit animation (put both model and text under this so they both animate!)")]
        public Transform visualTransform;
        [Tooltip("The 3D Text displaying the health")]
        public TMPro.TextMeshPro healthText; 
        public ParticleSystem destructionVfx;
        
        [Tooltip("How much to shrink/squeeze when hit by a mob")]
        public float hitScaleMultiplier = 0.85f; 
        public float hitRecoverySpeed = 15f;

        private Vector3 _originalScale;
        private Vector3 _targetScale;
        private bool _isDestroyed;

        private void Start()
        {
            if (visualTransform != null)
            {
                _originalScale = visualTransform.localScale;
                _targetScale = _originalScale;
            }
            UpdateHealthText();
        }

        protected override void Update()
        {
            if (_isDestroyed) return;

            // Critical: call base so GateBase can do the AABB checks!
            base.Update();

            // Handle the juicy hit recovery back to original scale
            if (visualTransform != null)
            {
                visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, _targetScale, Time.deltaTime * hitRecoverySpeed);
            }
        }

        protected override void OnMobEntered(Mob mob)
        {
            if (_isDestroyed) return;

            // 1. Recycle (Destroy) the mob that hit the blocker
            mob.Recycle();

            // 2. Reduce Health
            health--;
            UpdateHealthText();

            // 3. Play Juicy Hit Animation (Squeeze)
            if (visualTransform != null)
            {
                visualTransform.localScale = _originalScale * hitScaleMultiplier;
            }

            // 4. Check for Destruction
            if (health <= 0)
            {
                DestroyBlocker();
            }
        }

        private void UpdateHealthText()
        {
            if (healthText != null)
            {
                healthText.text = health.ToString();
            }
        }

        private void DestroyBlocker()
        {
            _isDestroyed = true;

            // Play VFX if assigned
            if (destructionVfx != null)
            {
                // Unparent it so it doesn't get disabled when we hide the blocker GameObject
                destructionVfx.transform.SetParent(null);
                destructionVfx.Play();
                // Clean up the particle object after it finishes
                Destroy(destructionVfx.gameObject, 3f); 
            }

            // Hide the entire blocker game object, revealing whatever is behind it
            gameObject.SetActive(false);
        }
    }
}
