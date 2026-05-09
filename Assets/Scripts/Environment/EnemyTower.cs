using UnityEngine;
using Mobs;

namespace Environment
{
    public class EnemyTower : GateBase
    {
        public int currentHP = 20;
        public TMPro.TextMeshPro healthText;
        
        [Header("Juice")]
        public ParticleSystem destructionParticles;
        public Transform towerMesh;
        public float hitPunchScale = 0.9f;
        public float springSpeed = 20f;
        
        private Vector3 _baseScale;
        private bool _isDestroyed;

        private void Start()
        {
            if (towerMesh != null)
                _baseScale = towerMesh.localScale;

            if (Core.GameManager.Instance != null)
            {
                // Optionally get HP from Luna parameters if desired
                // currentHP = (int)Core.GameManager.Instance.TowerHP;
            }

            UpdateHealthText();
        }

        private void UpdateHealthText()
        {
            if (healthText != null)
            {
                healthText.text = currentHP.ToString();
            }
        }

        protected override void Update()
        {
            if (_isDestroyed) return;
            
            base.Update();

            if (towerMesh != null)
            {
                towerMesh.localScale = Vector3.Lerp(towerMesh.localScale, _baseScale, Time.deltaTime * springSpeed);
            }
        }

        protected override void OnMobEntered(Mob mob)
        {
            if (_isDestroyed) return;

            int damage = mob.IsBigMob ? mob.HitPoints : 1;
            TakeDamage(damage);

            mob.Recycle();
        }

        public void TakeDamage(int damage)
        {
            if (currentHP <= 0 || _isDestroyed) return;

            currentHP -= damage;

            if (towerMesh != null)
            {
                towerMesh.localScale = _baseScale * hitPunchScale;
            }

            UpdateHealthText();

            if (Core.AudioManager.Instance != null)
            {
                Core.AudioManager.Instance.PlayTowerDamaged();
            }

            if (currentHP <= 0)
            {
                DestroyTower();
            }
        }

        private void DestroyTower()
        {
            _isDestroyed = true;
            
            if (destructionParticles != null)
            {
                ParticleSystem vfxInstance = Instantiate(destructionParticles, transform.position, Quaternion.identity);
                vfxInstance.Play();
                Destroy(vfxInstance.gameObject, 3f);
            }

            if (Core.AudioManager.Instance != null)
            {
                Core.AudioManager.Instance.PlayTowerDestroy();
            }

            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.TrackTowerDestroyed();
                Core.GameManager.Instance.WinGame();
            }

            // Smooth shrink instead of instant pop
            if (towerMesh != null)
            {
                StartCoroutine(ShrinkAndDestroyRoutine(towerMesh));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private System.Collections.IEnumerator ShrinkAndDestroyRoutine(Transform targetTransform)
        {
            float elapsed = 0f;
            float duration = 0.25f; // Fast, juicy shrink
            Vector3 startScale = targetTransform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * t; // Cubic ease in
                targetTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
                yield return null;
            }

            targetTransform.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }
    }
}
