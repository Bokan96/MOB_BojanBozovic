using UnityEngine;

namespace Mobs
{
    /// <summary>
    /// GPU-efficient flipbook animation for sprite sheets on Quads.
    /// Uses MaterialPropertyBlock to change UV offset per-frame without
    /// creating new material instances — critical for Luna build size and performance.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        [Header("Sheet Layout")]
        public int frameCount = 14;
        public int columns = 14;
        public int rows = 1;

        [Header("Playback")]
        public float fps = 12f;

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private float _timer;
        private int _currentFrame;
        private float _randomFps;

        // Cached for performance
        private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            // Randomize start frame so not all mobs animate perfectly in sync
            _currentFrame = Random.Range(0, frameCount);
            
            // Randomize playback speed slightly (between 80% and 120%) for variety
            _randomFps = fps * Random.Range(0.8f, 1.2f);
            
            _timer = 0f;
            ApplyFrame();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float frameDuration = 1f / _randomFps;

            if (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _currentFrame = (_currentFrame + 1) % frameCount;
                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            float tileX = 1f / columns;
            float tileY = 1f / rows;

            int col = _currentFrame % columns;
            int row = _currentFrame / columns;

            float offsetX = col * tileX;
            // UV Y is flipped in Unity: bottom row = row 0 in UV space
            float offsetY = (rows - 1 - row) * tileY;

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetVector(MainTexST, new Vector4(tileX, tileY, offsetX, offsetY));
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
