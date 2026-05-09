using UnityEngine;

namespace Environment
{
    /// <summary>
    /// Handles the visual presentation of a powerup, including a continuous Y-axis rotation 
    /// and a subtle floating bobbing animation to make it stand out without using heavy VFX.
    /// </summary>
    public class PowerupVisuals : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("Degrees per second to rotate on the Y axis.")]
        public float rotationSpeed = 180f;

        [Tooltip("How high the powerup bobs up and down.")]
        public float floatAmplitude = 0.25f;

        [Tooltip("How fast the powerup bobs up and down.")]
        public float floatFrequency = 2f;

        private Vector3 _startPosition;
        private Transform _transform;

        private void Awake()
        {
            _transform = transform;
            _startPosition = _transform.position;
        }

        private void Update()
        {
            // 1. Continuous Y-axis rotation
            _transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

            // 2. Subtle floating (bobbing) animation using a sine wave
            float newY = _startPosition.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
            _transform.position = new Vector3(_transform.position.x, newY, _transform.position.z);
        }
    }
}
