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
                currentHP = Core.GameManager.Instance.TowerHP;
            }

            UpdateHealthText();
        }

        private void UpdateHealthText()
        {
            if (healthText != null)
            {
                healthText.text = Mathf.Max(0, currentHP).ToString();
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

            // Cache the position BEFORE disabling, so VFX spawns at the right spot
            Vector3 towerPos = transform.position;

            // ── CRITICAL: Do game-state changes FIRST ──
            // Luna can silently crash on Instantiate/ParticleSystem calls,
            // which would abort the method and prevent WinGame from ever firing.
            if (Core.AudioManager.Instance != null)
            {
                Core.AudioManager.Instance.PlayTowerDestroy();
            }

            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.TrackTowerDestroyed();
                Core.GameManager.Instance.WinGame();
            }

            // Immediately disable the entire tower
            gameObject.SetActive(false);

            // ── BEST-EFFORT: Spawn smoke VFX after everything critical is done ──
            if (destructionParticles != null)
            {
                try
                {
                    ParticleSystem vfxInstance = Instantiate(destructionParticles, towerPos, Quaternion.identity);
                    vfxInstance.Play();
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Luna VFX Exception in DestroyTower: " + e.Message);
                }
            }
        }
    }
}
