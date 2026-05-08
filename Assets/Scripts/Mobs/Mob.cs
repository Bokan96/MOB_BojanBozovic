using UnityEngine;

namespace Mobs
{
    /// <summary>
    /// A single mob entity handling BOTH player and enemy logic to avoid redundancy.
    /// Pure transform movement (no physics) for maximum Luna performance.
    /// </summary>
    public class Mob : MonoBehaviour
    {
        private float _currentSpeed;
        private float _targetSpeed;
        private float _lerpTimer;
        private bool _active;
        private bool _isEnemy;
        private System.Action<Mob> _onRecycle;
        
        private Vector3 _originalScale; // Set once in Awake, never modified
        private Vector3 _baseScale;     // Reset every Activate(); MakeBig() multiplies this

        // Big Mob / HP system
        private int _hitPoints;
        private bool _isBigMob;

        private void Awake()
        {
            // Capture the prefab's original scale so we don't force it to Vector3.one
            _originalScale = transform.localScale;
            _baseScale = _originalScale;
        }

        // Spread animation state
        private bool _isSpreading;
        private float _spreadTargetX;
        private float _startSpreadX;
        private float _spreadTimer;

        // Y Landing animation state
        private bool _isLerpingY;
        private float _startY;
        private float _targetY;

        // Death animation state
        private bool _isDying;
        private float _deathTimer;
        private const float DEATH_DURATION = 0.15f;

        // Pipe entry animation state
        private bool _isEnteringPipe;
        private Vector3 _pipeStartPos;
        private Vector3 _pipeTargetPos;
        private float _pipeEnterTimer;
        private float _pipeEnterDuration;
        private System.Action _onPipeEnterComplete;

        public bool IsActive => _active;
        public bool IsBigMob => _isBigMob;
        public int HitPoints => _hitPoints;

        private const float BOOST_DURATION = 2f;
        private const float MAX_Z = 30f;
        private const float BIG_MOB_SCALE_MULTIPLIER = 2.5f;

        public void Activate(Vector3 position, float speed, System.Action<Mob> recycleCallback, bool isEnemy = false, bool applyBoost = true, bool doYLerp = false, float targetY = 0f)
        {
            transform.position = position;
            _targetSpeed = speed;
            _currentSpeed = applyBoost ? speed * 4f : speed; // Only boost if shot from cannon
            _lerpTimer = 0f;
            _isSpreading = false;
            _hitPoints = 1;
            _isBigMob = false;
            _baseScale = _originalScale; // Reset to original in case this was previously a Big Mob
            _isDying = false;
            _isEnteringPipe = false;

            _isLerpingY = doYLerp;
            if (doYLerp)
            {
                _startY = position.y;
                _targetY = targetY;
            }

            _isEnemy = isEnemy;
            _onRecycle = recycleCallback;
            _active = true;
            gameObject.SetActive(true);

            // Start at 0.6x scale for the pop-in effect
            transform.localScale = _baseScale * 0.6f;

            // Register for collision detection
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.RegisterMob(this, _isEnemy);
            }
        }

        /// <summary>
        /// Configures this mob as a Big Mob with extra HP and larger scale.
        /// Must be called immediately after Activate().
        /// </summary>
        public void MakeBig(int hitPoints)
        {
            _hitPoints = hitPoints;
            _isBigMob = true;
            // Override the base scale for this instance so all animations use the big size
            _baseScale = _baseScale * BIG_MOB_SCALE_MULTIPLIER;
            transform.localScale = _baseScale * 0.6f; // Re-apply pop-in at the big scale
        }

        /// <summary>
        /// Called by BattleManager when this mob collides with an opponent.
        /// Decrements HP. Returns true if the mob is still alive.
        /// </summary>
        public bool TakeHit()
        {
            if (_isDying) return false;

            _hitPoints--;
            if (_hitPoints <= 0)
            {
                Die();
                return false; // Dead
            }
            return true; // Still alive
        }

        public void Die()
        {
            if (_isDying || !_active) return;
            
            _isDying = true;
            _deathTimer = 0f;

            // Unregister from collision immediately so it stops hitting things while playing the death animation
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.UnregisterMob(this, _isEnemy);
            }
        }

        public void Recycle()
        {
            if (!_active) return; // Prevent double-recycling
            
            _active = false;
            gameObject.SetActive(false);

            // Unregister from collision detection (if it wasn't already unregistered by Die())
            if (!_isDying && BattleManager.Instance != null)
            {
                BattleManager.Instance.UnregisterMob(this, _isEnemy);
            }

            _onRecycle?.Invoke(this);
        }

        /// <summary>
        /// Smoothly slides the mob horizontally to a target global X position while moving forward.
        /// </summary>
        public void ActivateSpread(float targetGlobalX)
        {
            _isSpreading = true;
            _spreadTargetX = targetGlobalX;
            _startSpreadX = transform.position.x;
            _spreadTimer = 0f;
        }

        /// <summary>
        /// Sucks the mob into a specific target point (like a pipe entrance) over a duration,
        /// bypassing standard forward movement, then triggers a callback.
        /// </summary>
        public void EnterPipe(Vector3 targetPos, float duration, System.Action onComplete)
        {
            if (!_active || _isDying || _isEnteringPipe) return;

            _isEnteringPipe = true;
            _pipeStartPos = transform.position;
            _pipeTargetPos = targetPos;
            _pipeEnterTimer = 0f;
            _pipeEnterDuration = duration;
            _onPipeEnterComplete = onComplete;

            // Stop colliding with enemies while being sucked into the pipe
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.UnregisterMob(this, _isEnemy);
            }
        }

        private void Update()
        {
            if (!_active) return;

            if (_isDying)
            {
                _deathTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_deathTimer / DEATH_DURATION);
                
                // Pop explode: scale up slightly then squash to 0 rapidly
                float scaleT;
                if (t < 0.5f) {
                    scaleT = Mathf.Lerp(1f, 1.4f, t * 2f);
                } else {
                    scaleT = Mathf.Lerp(1.4f, 0f, (t - 0.5f) * 2f);
                }
                transform.localScale = _baseScale * scaleT;

                if (t >= 1f)
                {
                    Recycle();
                }
                return; // Skip normal movement while exploding
            }

            if (_isEnteringPipe)
            {
                _pipeEnterTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_pipeEnterTimer / _pipeEnterDuration);
                
                // Ease out cubic
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                
                Vector3 pos = transform.position;
                
                // Only lerp the X position towards the pipe center
                pos.x = Mathf.Lerp(_pipeStartPos.x, _pipeTargetPos.x, easedT);
                
                // Keep moving forward on the Z axis normally
                float zDirection = _isEnemy ? -1f : 1f;
                pos.z += zDirection * _currentSpeed * Time.deltaTime;
                
                transform.position = pos;
                transform.localScale = Vector3.Lerp(_baseScale, _baseScale * 0.5f, easedT);

                if (t >= 1f)
                {
                    _isEnteringPipe = false;
                    _onPipeEnterComplete?.Invoke();
                }
                return; // Skip normal movement while being sucked in
            }

            // Handle speed decay & scale pop (Fast out of cannon -> Walk speed)
            if (_lerpTimer < BOOST_DURATION)
            {
                _lerpTimer += Time.deltaTime;
                
                // Cubic easing out for speed
                float t = Mathf.Clamp01(_lerpTimer / BOOST_DURATION);
                float easedT = 1f - Mathf.Pow(1f - t, 3f); 
                _currentSpeed = Mathf.Lerp(_currentSpeed, _targetSpeed, easedT);

                // Scale animation (Linear over 0.2 seconds, from 0.6x to 1.0x)
                float scaleT = Mathf.Clamp01(_lerpTimer / 0.2f); 
                float currentScaleMult = Mathf.Lerp(0.6f, 1f, scaleT);
                transform.localScale = _baseScale * currentScaleMult;

                // Y Landing animation
                if (_isLerpingY)
                {
                    Vector3 pos = transform.position;
                    // Landing happens a bit faster than full speed decay for a snappy feel
                    float landT = Mathf.Clamp01(_lerpTimer / (BOOST_DURATION * 0.5f)); 
                    float easedLandT = 1f - Mathf.Pow(1f - landT, 3f);
                    pos.y = Mathf.Lerp(_startY, _targetY, easedLandT);
                    transform.position = pos;
                }
            }
            else
            {
                _currentSpeed = _targetSpeed;
                transform.localScale = _baseScale;
                
                if (_isLerpingY)
                {
                    Vector3 pos = transform.position;
                    pos.y = _targetY;
                    transform.position = pos;
                    _isLerpingY = false;
                }
            }

            // Handle Horizontal Spread (from Multiplier Gate)
            if (_isSpreading)
            {
                _spreadTimer += Time.deltaTime;
                float spreadT = Mathf.Clamp01(_spreadTimer / 0.25f); // 0.25s duration to spread
                float easedSpreadT = 1f - Mathf.Pow(1f - spreadT, 3f); // Ease out cubic
                
                Vector3 pos = transform.position;
                pos.x = Mathf.Lerp(_startSpreadX, _spreadTargetX, easedSpreadT);
                transform.position = pos;

                if (spreadT >= 1f) _isSpreading = false;
            }

            // Player moves +Z (forward), Enemy moves -Z (backward)
            float direction = _isEnemy ? -1f : 1f;
            transform.position += new Vector3(0f, 0f, direction * _currentSpeed * Time.deltaTime);

            // Clean up if they run way off-screen
            float z = transform.position.z;
            if (z > MAX_Z || z < 0)
            {
                Recycle();
            }
        }
    }
}
