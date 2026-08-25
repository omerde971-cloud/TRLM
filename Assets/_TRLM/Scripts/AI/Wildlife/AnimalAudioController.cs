using UnityEngine;
using TRLM.AI.Bear;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// Spatial, cooldown-gated animal vocals for bear + passive wildlife, mirroring
    /// WolfAudioController's pattern. Clips are behavioral (state changes), never on a
    /// timer alone — a bear growls when it warns, roars on the bluff charge, prey calls
    /// out when it bolts. Ambient idle sounds (breathing/rustle) use a long randomized
    /// interval and only play when the player is close enough to plausibly hear them.
    /// </summary>
    public class AnimalAudioController : MonoBehaviour
    {
        [SerializeField] private AudioSource source;

        [Header("Aggro (bear)")]
        [SerializeField] private AudioClip growl;
        [SerializeField] private AudioClip roar;
        [SerializeField] private AudioClip attack;
        [SerializeField] private AudioClip pain;

        [Header("Passive")]
        [SerializeField] private AudioClip alertCall;
        [SerializeField] private AudioClip fleeCall;

        [Header("Idle Ambient")]
        [SerializeField] private AudioClip[] idleAmbient; // breathing / rustle / soft huffs
        [SerializeField] private Vector2 idleAmbientInterval = new Vector2(12f, 28f);
        [SerializeField] private float idleAmbientMaxPlayerDistance = 24f;

        [Header("Cooldowns")]
        [SerializeField] private float growlCooldown = 6f;
        [SerializeField] private float roarCooldown = 4f;
        [SerializeField] private float callCooldown = 7f;

        private BearAI bear;
        private PassiveWildlifeAI passive;
        private float nextGrowl;
        private float nextRoar;
        private float nextCall;
        private float nextIdleAmbient;

        private void Awake()
        {
            bear = GetComponentInParent<BearAI>();
            passive = GetComponentInParent<PassiveWildlifeAI>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 4f;
            source.maxDistance = 55f;
            ScheduleIdleAmbient();
        }

        private void OnEnable()
        {
            if (bear != null)
            {
                bear.OnStateChanged += HandleBearState;
                bear.OnRoar += HandleRoar;
                bear.OnAttackCommitted += HandleAttack;
                bear.OnDamaged += HandleDamaged;
            }
            if (passive != null) passive.OnStateChanged += HandlePassiveState;
        }

        private void OnDisable()
        {
            if (bear != null)
            {
                bear.OnStateChanged -= HandleBearState;
                bear.OnRoar -= HandleRoar;
                bear.OnAttackCommitted -= HandleAttack;
                bear.OnDamaged -= HandleDamaged;
            }
            if (passive != null) passive.OnStateChanged -= HandlePassiveState;
        }

        private void Update()
        {
            if (idleAmbient == null || idleAmbient.Length == 0) return;
            if (Time.time < nextIdleAmbient) return;
            ScheduleIdleAmbient();

            // Only bother when calm and the player is near enough to hear it.
            bool calm = (bear == null || bear.CurrentState == BearAI.State.Idle || bear.CurrentState == BearAI.State.Forage || bear.CurrentState == BearAI.State.Patrol)
                        && (passive == null || passive.CurrentState == PassiveWildlifeAI.State.Graze || passive.CurrentState == PassiveWildlifeAI.State.Wander);
            if (!calm) return;

            var manager = WildlifeSpawnManager.Instance;
            if (manager != null && manager.Player != null &&
                (manager.Player.position - transform.position).sqrMagnitude > idleAmbientMaxPlayerDistance * idleAmbientMaxPlayerDistance)
                return;

            Play(idleAmbient[Random.Range(0, idleAmbient.Length)], 0.35f);
        }

        private void ScheduleIdleAmbient()
            => nextIdleAmbient = Time.time + Random.Range(idleAmbientInterval.x, idleAmbientInterval.y);

        private void HandleBearState(BearAI.State state)
        {
            if (state == BearAI.State.Warn && Time.time >= nextGrowl)
            {
                Play(growl, 0.7f);
                nextGrowl = Time.time + growlCooldown;
            }
            else if ((state == BearAI.State.Charge || state == BearAI.State.BluffCharge) && Time.time >= nextRoar)
            {
                Play(roar, 0.9f);
                nextRoar = Time.time + roarCooldown;
            }
        }

        private void HandleRoar()
        {
            if (Time.time < nextRoar) return;
            Play(roar, 0.9f);
            nextRoar = Time.time + roarCooldown;
        }

        private void HandleAttack() => Play(attack != null ? attack : growl, 0.85f);

        private void HandleDamaged(float _) => Play(pain != null ? pain : growl, 0.8f);

        private void HandlePassiveState(PassiveWildlifeAI.State state)
        {
            if (Time.time < nextCall) return;
            if (state == PassiveWildlifeAI.State.Alert && alertCall != null)
            {
                Play(alertCall, 0.5f);
                nextCall = Time.time + callCooldown;
            }
            else if ((state == PassiveWildlifeAI.State.Flee || state == PassiveWildlifeAI.State.Relocate) && fleeCall != null)
            {
                Play(fleeCall, 0.55f);
                nextCall = Time.time + callCooldown;
            }
        }

        private void Play(AudioClip clip, float volume)
        {
            if (source == null || clip == null) return;
            source.pitch = Random.Range(0.95f, 1.05f);
            source.PlayOneShot(clip, volume);
        }
    }
}
