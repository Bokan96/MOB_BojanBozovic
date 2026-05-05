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

        private const float BOOST_DURATION = 2.5f; // 0.4s of "shot" speed
        private const float MAX_Z = 30f;

        public void Activate(Vector3 position, float speed, System.Action<Mob> recycleCallback, bool isEnemy = false)
        {
            transform.position = position;
            _targetSpeed = speed;
            _currentSpeed = speed * 4f; // Start 3.5x faster for "kick"
            _lerpTimer = 0f;

            _isEnemy = isEnemy;
            _onRecycle = recycleCallback;
            _active = true;
            gameObject.SetActive(true);
        }

        public void Recycle()
        {
            _active = false;
            gameObject.SetActive(false);
            _onRecycle?.Invoke(this);
        }

        private void Update()
        {
            if (!_active) return;

            // Handle speed decay (Fast out of cannon -> Walk speed)
            if (_lerpTimer < BOOST_DURATION)
            {
                _lerpTimer += Time.deltaTime;
                // Cubic easing out feels more "shot-like" than a linear Lerp
                float t = _lerpTimer / BOOST_DURATION;
                float easedT = 1f - Mathf.Pow(1f - t, 3f); 
                _currentSpeed = Mathf.Lerp(_currentSpeed, _targetSpeed, easedT);
            }
            else
            {
                _currentSpeed = _targetSpeed;
            }

            // Player moves +Z (forward), Enemy moves -Z (backward)
            float direction = _isEnemy ? -1f : 1f;
            transform.position += new Vector3(0f, 0f, direction * _currentSpeed * Time.deltaTime);

            // Clean up if they run way off-screen
            float z = transform.position.z;
            if (z > MAX_Z || z < -MAX_Z)
            {
                Recycle();
            }
        }
    }
}
