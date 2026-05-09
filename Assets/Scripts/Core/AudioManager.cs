using UnityEngine;

namespace Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public AudioSource source;
        
        [Header("Clips")]
        public AudioClip cannonFireClip;
        public AudioClip mobDeathClip;
        public AudioClip gateMultiplyClip;
        public AudioClip towerDestroyClip;
        public AudioClip feverActivateClip;
        public AudioClip blockDestroyClip;
        
        private void Awake()
        {
            Instance = this;
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }
        }

        public void PlayCannonFire() { if (cannonFireClip != null) source.PlayOneShot(cannonFireClip); }
        public void PlayMobDeath() { if (mobDeathClip != null) source.PlayOneShot(mobDeathClip); }
        public void PlayGateMultiply() { if (gateMultiplyClip != null) source.PlayOneShot(gateMultiplyClip); }
        public void PlayTowerDestroy() { if (towerDestroyClip != null) source.PlayOneShot(towerDestroyClip); }
        public void PlayFeverActivate() { if (feverActivateClip != null) source.PlayOneShot(feverActivateClip); }
        public void PlayBlockDestroy() { if (blockDestroyClip != null) source.PlayOneShot(blockDestroyClip); }
    }
}
