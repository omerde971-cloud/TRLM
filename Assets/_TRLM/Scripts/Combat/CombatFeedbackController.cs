using UnityEngine;
using TRLM.Equipment;

namespace TRLM.Combat
{
    /// <summary>
    /// Quality Pass #1 fix — WeaponController/MeleeController already fire OnWeaponHit, OnImpact,
    /// OnDryFire, and OnMeleeHit, but nothing subscribed to them, so a hit and a miss felt
    /// identical. This wires minimal, restrained feedback onto those existing events: a small
    /// code-authored impact spark (no VFX asset exists in the project yet — see Quality Pass #1
    /// audit — so this builds one from a built-in shader rather than waiting on art) and a brief
    /// "+" hitmarker flash matching GameplayHUD's plain-text IMGUI style. Audio hooks follow the
    /// same AUDIO_ASSET_MISSING pattern as WeatherAudioController: AudioSource + AudioClip fields
    /// left null on purpose, null-checked before every PlayOneShot, so sound design only has to
    /// assign clips later with no code changes.
    /// </summary>
    public class CombatFeedbackController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeaponController weapon;
        [SerializeField] private MeleeController melee;
        [SerializeField] private PlayerEquipment equipment;

        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip pistolFireClip;
        [SerializeField] private AudioClip shotgunFireClip;
        [SerializeField] private AudioClip pistolMagInsertClip;
        [SerializeField] private AudioClip shotgunPumpClip;
        [SerializeField] private AudioClip dryFireClip;
        [SerializeField] private AudioClip meleeHitClip;
        [SerializeField] private AudioClip bulletRockClip;
        [SerializeField] private AudioClip bulletWoodClip;
        [SerializeField] private AudioClip fleshImpactClip;

        [Header("Hitmarker")]
        [SerializeField] private float hitmarkerSeconds = 0.15f;
        [SerializeField] private int hitmarkerFontSize = 22;

        [Header("Impact Burst")]
        [SerializeField] private float burstLifetimeSeconds = 1f;

        private float hitmarkerTimer;
        private GUIStyle hitmarkerStyle;
        private ParticleSystem impactBurstTemplate;

        private void Awake()
        {
            if (weapon == null) weapon = GetComponent<WeaponController>();
            if (melee == null) melee = GetComponent<MeleeController>();
            if (equipment == null) equipment = GetComponent<PlayerEquipment>();
            impactBurstTemplate = BuildImpactBurstTemplate();
        }

        private void OnEnable()
        {
            if (weapon != null)
            {
                weapon.OnWeaponHit += HandleWeaponHit;
                weapon.OnImpact += HandleImpact;
                weapon.OnDryFire += HandleDryFire;
                weapon.OnFire += HandleFire;
                weapon.OnReloadComplete += HandleReloadComplete;
            }
            if (melee != null) melee.OnMeleeHit += HandleMeleeHit;
        }

        private void OnDisable()
        {
            if (weapon != null)
            {
                weapon.OnWeaponHit -= HandleWeaponHit;
                weapon.OnImpact -= HandleImpact;
                weapon.OnDryFire -= HandleDryFire;
                weapon.OnFire -= HandleFire;
                weapon.OnReloadComplete -= HandleReloadComplete;
            }
            if (melee != null) melee.OnMeleeHit -= HandleMeleeHit;
        }

        private void Update()
        {
            if (hitmarkerTimer > 0f) hitmarkerTimer -= Time.deltaTime;
        }

        private void HandleWeaponHit(RaycastHit hit, float damage)
        {
            hitmarkerTimer = hitmarkerSeconds;
        }

        private void HandleMeleeHit(RaycastHit hit, float damage)
        {
            hitmarkerTimer = hitmarkerSeconds;
            if (sfxSource != null && meleeHitClip != null) sfxSource.PlayOneShot(meleeHitClip);
        }

        private void HandleImpact(Vector3 position, Vector3 normal, string surfaceType)
        {
            if (impactBurstTemplate == null) return;
            var instance = Instantiate(impactBurstTemplate, position, Quaternion.LookRotation(normal));
            instance.gameObject.SetActive(true);
            instance.Play();
            Destroy(instance.gameObject, burstLifetimeSeconds);

            AudioClip clip = surfaceType == "Flesh" ? fleshImpactClip
                : surfaceType != null && surfaceType.ToLowerInvariant().Contains("wood") ? bulletWoodClip
                : bulletRockClip;
            PlayAt(clip, position, 0.45f);
        }

        private void HandleDryFire()
        {
            if (sfxSource != null && dryFireClip != null) sfxSource.PlayOneShot(dryFireClip);
        }

        private void HandleFire()
        {
            var def = equipment != null ? equipment.GetActiveDefinition() : null;
            bool shotgun = def != null && def.category == WeaponCategory.LongGun && def.pelletCount > 1;
            if (sfxSource != null)
                sfxSource.PlayOneShot(shotgun ? shotgunFireClip : pistolFireClip, shotgun ? 0.95f : 0.78f);
            if (shotgun && shotgunPumpClip != null) StartCoroutine(PlayDelayed(shotgunPumpClip, 0.28f, 0.48f));
        }

        private void HandleReloadComplete()
        {
            var def = equipment != null ? equipment.GetActiveDefinition() : null;
            bool shotgun = def != null && def.category == WeaponCategory.LongGun && def.pelletCount > 1;
            AudioClip clip = shotgun ? shotgunPumpClip : pistolMagInsertClip;
            if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip, 0.5f);
        }

        private System.Collections.IEnumerator PlayDelayed(AudioClip clip, float delay, float volume)
        {
            yield return new WaitForSeconds(delay);
            if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip, volume);
        }

        private static void PlayAt(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        private void OnGUI()
        {
            if (hitmarkerTimer <= 0f) return;

            hitmarkerStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = hitmarkerFontSize,
                normal = { textColor = Color.white },
            };

            var rect = new Rect(Screen.width * 0.5f - 15f, Screen.height * 0.5f - 15f, 30f, 30f);
            GUI.Label(rect, "+", hitmarkerStyle);
        }

        /// <summary>Small spark burst built from Unity's always-available Sprites/Default shader —
        /// no VFX asset exists in the project yet (Quality Pass #1), so impacts get SOMETHING
        /// visible now rather than staying silent/invisible until art lands.</summary>
        private ParticleSystem BuildImpactBurstTemplate()
        {
            var go = new GameObject("ImpactBurstTemplate");
            go.SetActive(false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.25f;
            main.startSpeed = 2.5f;
            main.startSize = 0.05f;
            main.startColor = new Color(1f, 0.85f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.02f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            return ps;
        }
    }
}
