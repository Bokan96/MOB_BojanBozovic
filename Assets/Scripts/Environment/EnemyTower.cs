using UnityEngine;
using Mobs;

namespace Environment
{
    public class EnemyTower : GateBase
    {
        public int currentHP = 20;
        public TMPro.TextMeshPro healthText;
        
        [Header("Juice")]
        [Tooltip("The ParticleSystem that is already in the scene (child of the tower). We play this instead of instantiating to avoid Luna crashes.")]
        public ParticleSystem destructionVfxInstance;
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

            // Ensure the VFX instance starts stopped
            if (destructionVfxInstance != null)
                destructionVfxInstance.Stop();

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

            // ── CRITICAL: Do game-state changes FIRST ──
            if (Core.AudioManager.Instance != null)
            {
                Core.AudioManager.Instance.PlayTowerDestroy();
            }

            if (Core.GameManager.Instance != null)
            {
                Core.GameManager.Instance.TrackTowerDestroyed();
                Core.GameManager.Instance.WinGame();
            }

            // Immediately disable the tower visuals and collider
            // We don't disable the whole gameObject immediately because the VFX might be a child
            if (towerMesh != null) towerMesh.gameObject.SetActive(false);
            if (healthText != null) healthText.gameObject.SetActive(false);
            
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // ── BEST-EFFORT: Play the pre-existing VFX ──
            if (destructionVfxInstance != null)
            {
                try
                {
                    // Unparent so it doesn't get disabled if we eventually disable the tower
                    destructionVfxInstance.transform.SetParent(null);
                    destructionVfxInstance.Play();
                    // Destroy the loose particle object after it's done
                    Destroy(destructionVfxInstance.gameObject, 3f);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Luna VFX Exception in DestroyTower: " + e.Message);
                }
            }

            // Deactivate the tower logic object
            gameObject.SetActive(false);
        }
    }
}
