using UnityEngine;
using System.Collections.Generic;

namespace Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Pool")]
        [Tooltip("Number of concurrent audio sources to handle overlapping sounds")]
        public int sourcePoolSize = 5;
        private AudioSource[] _sources;
        private int _currentSourceIndex = 0;
        private AudioSource _musicSource;
        
        [Header("Clips")]
        public AudioClip cannonFireClip;
        public AudioClip mobDeathClip;
        public AudioClip gateMultiplyClip;
        public AudioClip towerDestroyClip;
        public AudioClip feverActivateClip;
        public AudioClip blockDestroyClip;
        public AudioClip towerDamagedClip;
        public AudioClip pipeEnterClip;
        public AudioClip pipeExitClip;
        public AudioClip backgroundMusicClip;

        [Header("Pitch Variation (Juice)")]
        [Tooltip("Random pitch variation for cannon fire")]
        public Vector2 cannonPitchRange = new Vector2(0.9f, 1.1f);
        [Tooltip("Random pitch variation for mob death")]
        public Vector2 mobDeathPitchRange = new Vector2(0.8f, 1.2f);
        [Tooltip("Random pitch variation for block hits")]
        public Vector2 blockDestroyPitchRange = new Vector2(0.9f, 1.1f);

        [Header("Sound Throttling")]
        [Tooltip("Prevent gate multiply sound from playing too frequently (seconds)")]
        public float gateMultiplyCooldown = 0.1f;
        private float _lastGateMultiplyTime;

        [Tooltip("Prevent mob death sound from playing too frequently (seconds)")]
        public float mobDeathCooldown = 0.05f;
        private float _lastMobDeathTime;

        [Tooltip("Prevent tower damaged sound from playing too frequently (seconds)")]
        public float towerDamagedCooldown = 0.1f;
        private float _lastTowerDamagedTime;

        [Tooltip("Prevent pipe sounds from playing too frequently (seconds)")]
        public float pipeSoundCooldown = 0.05f;
        private float _lastPipeEnterTime;
        private float _lastPipeExitTime;

        private void Awake()
        {
            Instance = this;
            
            // Initialize AudioSource pool
            _sources = new AudioSource[sourcePoolSize];
            for (int i = 0; i < sourcePoolSize; i++)
            {
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _sources[i] = src;
            }

            if (backgroundMusicClip != null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.clip = backgroundMusicClip;
                _musicSource.loop = true;
                _musicSource.volume = 0.4f; // Keep music volume balanced
                _musicSource.playOnAwake = true;
                _musicSource.Play();
            }
        }

        private AudioSource GetAvailableSource()
        {
            // Simple round-robin pooling
            AudioSource src = _sources[_currentSourceIndex];
            _currentSourceIndex = (_currentSourceIndex + 1) % sourcePoolSize;
            return src;
        }

        private void PlaySound(AudioClip clip, float volume = 1f, float minPitch = 1f, float maxPitch = 1f)
        {
            if (clip == null) return;
            
            AudioSource src = GetAvailableSource();
            src.clip = clip;
            src.volume = volume;
            src.pitch = Random.Range(minPitch, maxPitch);
            src.Play();
        }

        public void PlayCannonFire() 
        { 
            PlaySound(cannonFireClip, 1f, cannonPitchRange.x, cannonPitchRange.y); 
        }

        public void PlayMobDeath() 
        { 
            if (Time.time - _lastMobDeathTime < mobDeathCooldown) return;
            _lastMobDeathTime = Time.time;
            PlaySound(mobDeathClip, 0.7f, mobDeathPitchRange.x, mobDeathPitchRange.y); 
        }

        public void PlayGateMultiply() 
        { 
            if (Time.time - _lastGateMultiplyTime < gateMultiplyCooldown) return;
            _lastGateMultiplyTime = Time.time;
            // Slight pitch up for excitement
            PlaySound(gateMultiplyClip, 0.8f, 1f, 1.05f); 
        }

        public void PlayTowerDestroy() { PlaySound(towerDestroyClip); }
        
        public void PlayFeverActivate() { PlaySound(feverActivateClip, 1f, 1.1f, 1.2f); }
        
        public void PlayBlockDestroy() 
        { 
            PlaySound(blockDestroyClip, 0.9f, blockDestroyPitchRange.x, blockDestroyPitchRange.y); 
        }

        public void PlayTowerDamaged() 
        { 
            if (Time.time - _lastTowerDamagedTime < towerDamagedCooldown) return;
            _lastTowerDamagedTime = Time.time;
            PlaySound(towerDamagedClip, 0.9f, 0.95f, 1.05f); 
        }

        public void PlayPipeEnter() 
        { 
            if (Time.time - _lastPipeEnterTime < pipeSoundCooldown) return;
            _lastPipeEnterTime = Time.time;
            PlaySound(pipeEnterClip, 0.8f, 0.9f, 1.1f); 
        }

        public void PlayPipeExit() 
        { 
            if (Time.time - _lastPipeExitTime < pipeSoundCooldown) return;
            _lastPipeExitTime = Time.time;
            PlaySound(pipeExitClip, 0.8f, 0.9f, 1.1f); 
        }
    }
}
