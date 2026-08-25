using UnityEngine;

namespace TRLM.AI.Wolf
{
    [RequireComponent(typeof(WolfAI))]
    public class WolfAudioController : MonoBehaviour
    {
        [SerializeField] private WolfAI wolf;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip distantHowl;
        [SerializeField] private AudioClip closeGrowl;
        [SerializeField] private AudioClip attackBark;
        [SerializeField] private AudioClip pain;
        [SerializeField] private float howlCooldown = 18f;
        [SerializeField] private float growlCooldown = 5f;
        [SerializeField] private float barkCooldown = 1.2f;
        [SerializeField] private float painCooldown = 0.4f;

        private float nextHowl;
        private float nextGrowl;
        private float nextBark;
        private float nextPain;

        private void Awake()
        {
            if (wolf == null) wolf = GetComponent<WolfAI>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 3f;
            source.maxDistance = 42f;
        }

        private void OnEnable()
        {
            if (wolf == null) return;
            wolf.OnStateChanged += HandleStateChanged;
            wolf.OnAttackCommitted += HandleAttackCommitted;
            wolf.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (wolf == null) return;
            wolf.OnStateChanged -= HandleStateChanged;
            wolf.OnAttackCommitted -= HandleAttackCommitted;
            wolf.OnDamaged -= HandleDamaged;
        }

        private void HandleStateChanged(WolfAI.State state)
        {
            if ((state == WolfAI.State.Alert || state == WolfAI.State.Investigate) && Time.time >= nextHowl)
            {
                Play(distantHowl, 0.55f);
                nextHowl = Time.time + howlCooldown;
            }
            else if ((state == WolfAI.State.Stalk || state == WolfAI.State.Chase) && Time.time >= nextGrowl)
            {
                Play(closeGrowl, 0.72f);
                nextGrowl = Time.time + growlCooldown;
            }
        }

        private void HandleAttackCommitted()
        {
            if (Time.time < nextBark) return;
            Play(attackBark, 0.82f);
            nextBark = Time.time + barkCooldown;
        }

        private void HandleDamaged(float _)
        {
            if (Time.time < nextPain) return;
            Play(pain, 0.75f);
            nextPain = Time.time + painCooldown;
        }

        private void Play(AudioClip clip, float volume)
        {
            if (source != null && clip != null)
                source.PlayOneShot(clip, volume);
        }
    }
}
