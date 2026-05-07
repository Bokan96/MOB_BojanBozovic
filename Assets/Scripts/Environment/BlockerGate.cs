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
        public float hitScaleMultiplier = 0.9f; 
        public float hitRecoverySpeed = 15f;

        [Header("Shake")]
        [Tooltip("How far the blocker shakes on X/Z when hit")]
        public float shakeIntensity = 0.15f;
        [Tooltip("How long the shake lasts in seconds")]
        public float shakeDuration = 0.2f;

        private Vector3 _originalScale;
        private Vector3 _targetScale;
        private Vector3 _originalLocalPos;
        private float _shakeTimer;
        private float _currentShakeIntensity;
        private bool _isDestroyed;

        private void Start()
        {
            if (visualTransform != null)
            {
                _originalScale = visualTransform.localScale;
                _targetScale = _originalScale;
                _originalLocalPos = visualTransform.localPosition;
            }
            UpdateHealthText();
        }

        protected override void Update()
        {
            if (_isDestroyed) return;

            // Critical: call base so GateBase can do the AABB checks!
            base.Update();

            if (visualTransform != null)
            {
                // Handle the juicy hit recovery back to original scale
                visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, _targetScale, Time.deltaTime * hitRecoverySpeed);

                // Handle shake
                if (_shakeTimer > 0f)
                {
                    _shakeTimer -= Time.deltaTime;
                    float decay = _shakeTimer / shakeDuration; // 1 → 0 over the duration
                    float offsetX = Random.Range(-1f, 1f) * _currentShakeIntensity * decay;
                    float offsetZ = Random.Range(-1f, 1f) * _currentShakeIntensity * decay;
                    visualTransform.localPosition = _originalLocalPos + new Vector3(offsetX, 0f, offsetZ);
                }
                else
                {
                    // Snap back to original position when shake is done
                    visualTransform.localPosition = _originalLocalPos;
                }
            }
        }

        protected override void OnMobEntered(Mob mob)
        {
            if (_isDestroyed) return;

            if (mob.IsBigMob)
            {
                // Big Mobs are "tanks" — they deal damage to the blocker equal to their current HP
                // and then disappear (recycle).
                health -= mob.HitPoints;
                mob.Recycle();
            }
            else
            {
                // Regular mobs deal 1 damage and are recycled
                health--;
                mob.Recycle();
            }

            UpdateHealthText();

            // 3. Play Juicy Hit Animation (Squeeze + Shake)
            if (visualTransform != null)
            {
                float multiplier = mob.IsBigMob ? 0.75f : hitScaleMultiplier;
                visualTransform.localScale = _originalScale * multiplier;

                // Trigger shake — stronger for Big Mobs
                _shakeTimer = shakeDuration;
                _currentShakeIntensity = mob.IsBigMob ? shakeIntensity * 2.5f : shakeIntensity;
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
