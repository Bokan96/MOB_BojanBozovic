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

            // Pull A/B testable values from GameManager
            if (Core.GameManager.Instance != null)
            {
                health = Core.GameManager.Instance.RightBlockerHP;
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

            if (visualTransform != null)
            {
                float multiplier = mob.IsBigMob ? 0.75f : hitScaleMultiplier;
                visualTransform.localScale = _originalScale * multiplier;

                // Trigger shake — stronger for Big Mobs
                _shakeTimer = shakeDuration;
                _currentShakeIntensity = mob.IsBigMob ? shakeIntensity * 2.5f : shakeIntensity;
            }

            if (Core.AudioManager.Instance != null)
            {
                Core.AudioManager.Instance.PlayTowerDamaged();
            }

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

            if (Core.AudioManager.Instance != null)
            {
                Core.AudioManager.Instance.PlayBlockDestroy();
            }

            // Play VFX if assigned
            if (destructionVfx != null)
            {
                // Instantiate a clone of the prefab at our current position
                ParticleSystem vfxInstance = Instantiate(destructionVfx, transform.position, Quaternion.identity);
                vfxInstance.Play();
                
                // Destroying a scene clone is perfectly fine in Luna. 
                // The error was because the code previously tried to destroy the Prefab Asset itself!
                Destroy(vfxInstance.gameObject, 3f); 
            }

            // Instead of instantly hiding, let's shrink it smoothly
            if (visualTransform != null)
            {
                StartCoroutine(ShrinkAndDestroyRoutine(visualTransform));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private System.Collections.IEnumerator ShrinkAndDestroyRoutine(Transform targetTransform)
        {
            float elapsed = 0f;
            float duration = 0.25f; // Super fast juicy shrink
            Vector3 startScale = targetTransform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease In Back curve for a nice "pop" shrinking effect
                float eased = t * t * t; 
                targetTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
                yield return null;
            }

            targetTransform.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }
    }
}
