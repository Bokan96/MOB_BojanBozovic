using UnityEngine;

namespace Mobs
{
    /// <summary>
    /// A single mob entity handling BOTH player and enemy logic to avoid redundancy.
    /// Pure transform movement (no physics) for maximum Luna performance.
    /// </summary>
    public class Mob : MonoBehaviour
    {
        private float _speed;
        private bool _active;
        private bool _isEnemy;
        private System.Action<Mob> _onRecycle;

        // Boundary to recycle mobs if they run off-screen
        private const float MAX_Z = 30f;

        public void Activate(Vector3 position, float speed, System.Action<Mob> recycleCallback, bool isEnemy = false)
        {
            transform.position = position;
            _speed = speed;
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

            // Player moves +Z (forward), Enemy moves -Z (backward)
            float direction = _isEnemy ? -1f : 1f;
            transform.position += new Vector3(0f, 0f, direction * _speed * Time.deltaTime);

            // Clean up if they run way off-screen
            float z = transform.position.z;
            if (z > MAX_Z || z < -MAX_Z)
            {
                Recycle();
            }
        }
    }
}
