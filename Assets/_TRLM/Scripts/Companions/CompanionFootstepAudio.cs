using UnityEngine;

namespace TRLM.Companions
{
    /// <summary>
    /// Spatial footsteps for companions, fired by the OnFootstep AnimationEvents baked into
    /// the StarterAssets Walk_N/Run_N clips (forwarded from CompanionLocomotionAnimator), so
    /// steps land exactly on foot plants instead of on a timer. Reuses the same dirt/gravel
    /// footstep clips as the player but as quiet 3D one-shots.
    /// </summary>
    public class CompanionFootstepAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.3f;
        [SerializeField] private float minInterval = 0.18f;

        private int lastClipIndex = -1;
        private float lastPlayTime;

        private void Awake()
        {
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.5f;
            source.maxDistance = 14f;
        }

        public void PlayFootstep()
        {
            if (clips == null || clips.Length == 0) return;
            if (Time.time - lastPlayTime < minInterval) return;
            lastPlayTime = Time.time;

            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastClipIndex) index = (index + 1) % clips.Length;
            lastClipIndex = index;

            source.pitch = Random.Range(0.94f, 1.06f);
            source.PlayOneShot(clips[index], volume);
        }
    }
}
