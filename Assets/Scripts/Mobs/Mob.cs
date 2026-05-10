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
        private bool _applyBoost;
        private bool _isCharging; // Set true by GameManager during the lose sequence

        // Visual Hit Flash
        private Renderer _renderer;
        private Material _sharedMat;
        private Material _matInstance;
        private Color _originalTopColor = Color.white;
        private Color _originalBottomColor = Color.white;
        private bool _isFlashing;
        private float _flashTimer;
        private const float FLASH_DURATION = 0.08f; 

        private void Awake()
        {
            // Capture the prefab's original scale so we don't force it to Vector3.one
            _originalScale = transform.localScale;
            _baseScale = _originalScale;

            _renderer = GetComponentInChildren<SpriteRenderer>(true);
            if (_renderer == null) _renderer = GetComponentInChildren<MeshRenderer>(true);
            
            if (_renderer != null && _renderer.sharedMaterial != null)
            {
                _sharedMat = _renderer.sharedMaterial;
                _sharedMat.renderQueue = 2450; // Explicitly force AlphaTest queue to override any saved asset overrides

                if (_sharedMat.HasProperty("_ColorTop"))
                    _originalTopColor = _sharedMat.GetColor("_ColorTop");
                if (_sharedMat.HasProperty("_ColorBottom"))
                    _originalBottomColor = _sharedMat.GetColor("_ColorBottom");
            }
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

        // Follow animation state
        private bool _isFollowing;
        private Transform _followTarget;
        
        private bool _isCelebrating;

        public bool IsActive => _active;
        public bool IsBigMob => _isBigMob;
        public int HitPoints => _hitPoints;
        public bool IsEnemy => _isEnemy;

        /// <summary>
        /// Freezes the mob's own movement so GameManager can drive it manually during the lose sequence.
        /// </summary>
        public void StartCharge() => _isCharging = true;

        private const float BOOST_DURATION = 2f;
        private const float MAX_Z = 30f;
        private const float BIG_MOB_SCALE_MULTIPLIER = 2.5f;

        // Overlap settings — pushes overlapping mobs forward into a line
        private const float OVERLAP_MIN_DIST = 0.5f;
        private const float OVERLAP_MIN_DIST_BIG = 0.6f;  // Was 1.0f — reduced 40% so big mobs don't rocket forward
        private const float OVERLAP_PUSH_SPEED = 20f;
        private const float OVERLAP_PUSH_SPEED_BIG = 5f;  // Was implicit 20f — much gentler for big mobs

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
            _isFollowing = false;
            _followTarget = null;
            _isCelebrating = false;
            _isCharging = false;
            
            // Reset flash state
            _isFlashing = false;

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
            
            _applyBoost = applyBoost;

            if (_applyBoost)
            {
                // Start at 0.6x scale for the pop-in effect
                transform.localScale = _baseScale * 0.6f;
            }
            else
            {
                // Immediately set to full scale if no boost (e.g. exiting pipe)
                transform.localScale = _baseScale;
            }

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
            
            if (_applyBoost)
            {
                transform.localScale = _baseScale * 0.6f; // Re-apply pop-in at the big scale
            }
            else
            {
                transform.localScale = _baseScale;
            }
        }

        /// <summary>
        /// Called by BattleManager when this mob collides with an opponent.
        /// Decrements HP. Returns true if the mob is still alive.
        /// </summary>
        public bool TakeHit()
        {
            if (_isDying) return false;

            _hitPoints--;
            
            if (_isBigMob && _hitPoints > 0)
            {
                transform.localScale = _baseScale * 0.8f;
                _isFlashing = true;
                _flashTimer = 0f;

                if (_matInstance == null && _renderer != null && _sharedMat != null)
                {
                    _matInstance = _renderer.material;
                    _matInstance.renderQueue = 2450;
                }
            }

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

            if (Core.AudioManager.Instance != null)
            {
                Core.AudioManager.Instance.PlayMobDeath();
            }

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

            // Cleanup flash material to restore batching
            if (_isFlashing)
            {
                _isFlashing = false;
                if (_renderer != null && _sharedMat != null)
                {
                    _renderer.sharedMaterial = _sharedMat;
                }
                if (_matInstance != null)
                {
                    Destroy(_matInstance);
                    _matInstance = null;
                }
            }

            // Unregister from collision detection (if it wasn't already unregistered by Die())
            if (!_isDying && BattleManager.Instance != null)
            {
                BattleManager.Instance.UnregisterMob(this, _isEnemy);
            }

            Environment.GateBase.ClearMobFromAllGates(this);
            
            if (_onRecycle != null)
            {
                _onRecycle(this);
            }
        }

        public void CelebrateAndDie()
        {
            if (!_active || _isCelebrating || _isDying) return;
            _isCelebrating = true;
            StartCoroutine(CelebrateRoutine());
        }

        private System.Collections.IEnumerator CelebrateRoutine()
        {
            // Unregister from battle immediately so they don't hit anything
            if (BattleManager.Instance != null)
                BattleManager.Instance.UnregisterMob(this, _isEnemy);

            // Wait a random amount of time (0.5 to 2 seconds)
            yield return new WaitForSeconds(Random.Range(0.5f, 2f));

            // Jump up to Y=1 and back to 0
            float duration = 0.4f; // Very quick jump
            float elapsed = 0f;
            Vector3 startPos = transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                // Parabola: 4 * t * (1 - t) reaches 1 at t=0.5, and 0 at t=0 and t=1
                float yOffset = 4f * t * (1f - t); 
                
                Vector3 currentPos = startPos;
                currentPos.y = startPos.y + yOffset;
                transform.position = currentPos;
                
                yield return null;
            }

            transform.position = startPos;

            // Disappear
            Recycle();
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

        /// <summary>
        /// Commands the mob to stop its normal forward movement and instead constantly move towards a specific Transform.
        /// </summary>
        public void StartFollowing(Transform target, float customSpeed = -1f)
        {
            if (!_active || _isDying) return;

            _isFollowing = true;
            _followTarget = target;

            if (customSpeed > 0f)
            {
                _targetSpeed = customSpeed;
                _currentSpeed = customSpeed;
                // If it was still boosting, stop the boost so it doesn't fight the custom speed
                _applyBoost = false; 
            }
        }

        private void Update()
        {
            if (!_active) return;

            // Catch missed mobs / newly spawned mobs after win
            if (!_isCelebrating && !_isDying && !_isEnemy && !_isEnteringPipe && Core.GameManager.Instance != null && Core.GameManager.Instance.isGameWon)
            {
                CelebrateAndDie();
            }

            // Recover scale from TakeHit punch if it's a big mob
            if (_isBigMob && !_isDying && !_applyBoost && transform.localScale.x < _baseScale.x)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _baseScale, Time.deltaTime * 10f);
            }

            // Hit flash animation — direct material instance manipulation
            if (_isFlashing && _matInstance != null)
            {
                _flashTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_flashTimer / FLASH_DURATION);
                
                // Instant red at t=0, smoothly fade back to original
                float curve = 1f - t;
                Color currentTop = Color.Lerp(_originalTopColor, Color.red, curve);
                Color currentBottom = Color.Lerp(_originalBottomColor, Color.red, curve);
                
                _matInstance.SetColor("_ColorTop", currentTop);
                _matInstance.SetColor("_ColorBottom", currentBottom);

                if (t >= 1f)
                {
                    _isFlashing = false;
                    if (_renderer != null && _sharedMat != null)
                    {
                        _renderer.sharedMaterial = _sharedMat;
                    }
                    if (_matInstance != null)
                    {
                        Destroy(_matInstance);
                        _matInstance = null;
                    }
                }
            }

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
            if (_applyBoost)
            {
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
                    _applyBoost = false;
                    
                    if (_isLerpingY)
                    {
                        Vector3 pos = transform.position;
                        pos.y = _targetY;
                        transform.position = pos;
                        _isLerpingY = false;
                    }
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

            if (_isCharging || _isCelebrating)
            {
                // Movement is fully controlled externally (or waiting to jump) — skip forward movement
                return;
            }

            if (_isFollowing)
            {
                if (_followTarget != null)
                {
                    // Move towards the target at the current speed
                    transform.position = Vector3.MoveTowards(transform.position, _followTarget.position, _currentSpeed * Time.deltaTime);
                }
                else
                {
                    // If target is missing, stop following
                    _isFollowing = false;
                }
                // Skip the standard forward movement
            }
            else
            {
                // Player moves +Z (forward), Enemy moves -Z (backward)
                float direction = _isEnemy ? -1f : 1f;
                transform.position += new Vector3(0f, 0f, direction * _currentSpeed * Time.deltaTime);
            }

            // Clean up if they run way off-screen
            float z = transform.position.z;
            if (z > MAX_Z || z < 0)
            {
                Recycle();
                return;
            }

            // Strictly push overlapping friendly mobs forward on the Z axis
            if (!_isEnemy && !_isEnteringPipe && BattleManager.Instance != null)
            {
                ApplyOverlapNudge();
            }
        }

        /// <summary>
        /// Checks for overlapping friendly mobs and nudges the one in front strictly forward.
        /// If perfectly aligned, uses InstanceID as a tie-breaker to form a line.
        /// </summary>
        private void ApplyOverlapNudge()
        {
            var playerMobs = BattleManager.Instance.PlayerMobs;
            Vector3 myPos = transform.position;
            float nudgeZ = 0f;

            for (int i = 0; i < playerMobs.Count; i++)
            {
                Mob other = playerMobs[i];
                if (other == this || !other.IsActive) continue;

                Vector3 otherPos = other.transform.position;

                float dx = Mathf.Abs(myPos.x - otherPos.x);
                float dz = Mathf.Abs(myPos.z - otherPos.z);
                
                float effectiveMinDist = (_isBigMob || other.IsBigMob) ? OVERLAP_MIN_DIST_BIG : OVERLAP_MIN_DIST;

                if (dx < effectiveMinDist && dz < effectiveMinDist)
                {
                    // They are overlapping! We only want to push one of them forward.
                    // The one that is already slightly ahead gets pushed further ahead.
                    bool iAmAhead = myPos.z > otherPos.z;
                    
                    // If they are exactly on the same Z (e.g. spawned same frame), break the tie
                    if (Mathf.Abs(myPos.z - otherPos.z) < 0.001f)
                    {
                        iAmAhead = this.GetInstanceID() > other.GetInstanceID();
                    }

                    if (iAmAhead)
                    {
                        // The closer they are on Z, the harder we push them forward
                        float overlapZ = effectiveMinDist - dz;
                        float pushSpeed = (_isBigMob || other.IsBigMob) ? OVERLAP_PUSH_SPEED_BIG : OVERLAP_PUSH_SPEED;
                        nudgeZ += overlapZ * pushSpeed * Time.deltaTime;
                    }
                }
            }

            if (nudgeZ > 0.001f)
            {
                transform.position += new Vector3(0f, 0f, nudgeZ);
            }
        }
    }
}
