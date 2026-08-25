using System;
using System.Collections;
using UnityEngine;
using TRLM.Player;
using TRLM.Inventory;
using TRLM.Core;
using TRLM.Survival;
using TRLM.AI.Perception;
using TRLM.Progression;
using TRLM.UI;

namespace TRLM.Equipment
{
    /// <summary>
    /// Data-driven firearm controller. Drives whichever weapon PlayerEquipment currently has
    /// active (ActiveSlot), reading every behavioral number from that weapon's
    /// WeaponDefinition — one controller for both the pistol and the long gun, per the "separate
    /// weapon data from weapon runtime state" instruction, instead of two near-duplicate
    /// per-weapon controllers. Reads Fire/Aim/Reload exclusively from PlayerInputHandler.
    /// </summary>
    [RequireComponent(typeof(PlayerEquipment))]
    public class WeaponController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private Camera raycastCamera; // MainCamera under CameraRoot — raycast origin
        [SerializeField] private StaminaSystem stamina;
        [SerializeField] private FirstPersonController movement;
        [SerializeField] private GameplayHUD hud;
        [SerializeField] private EquipmentWheelUI wheelUI;

        [Header("Aim")]
        [SerializeField] private float aimSpeedMultiplier = 0.6f;

        public event Action OnFire;
        public event Action OnDryFire;
        public event Action OnReloadStart;
        public event Action OnReloadComplete;

        /// <summary>(hit, damage actually applied) — A2's injury system determines hit region
        /// from hit.collider itself; this event just proves the contract exists and fires.</summary>
        public event Action<RaycastHit, float> OnWeaponHit;

        /// <summary>(position, normal, surfaceType) — VFX/audio hook, no VFX required this sprint.</summary>
        public event Action<Vector3, Vector3, string> OnImpact;

        private readonly WeaponSway sway = new WeaponSway();
        private Coroutine reloadRoutine;

        /// <summary>Same source-keyed, worst-(largest)-value-wins pattern as WeaponSway. Sprint 07
        /// left this as a documented gap (arm injury doesn't slow reload); RegionalInjurySystem can
        /// now call SetReloadSpeedModifier("ArmInjury", x) without WeaponController knowing about
        /// injuries. Default 1 = no change.</summary>
        private readonly System.Collections.Generic.Dictionary<string, float> reloadSpeedModifiers = new System.Collections.Generic.Dictionary<string, float>();
        public float ReloadSpeedMultiplier { get; private set; } = 1f;

        public bool IsAiming { get; private set; }
        public float CurrentSwayDegrees { get; private set; }

        public void SetSwayModifier(string sourceId, float multiplier) => sway.SetSwayModifier(sourceId, multiplier);
        public void ClearSwayModifier(string sourceId) => sway.ClearSwayModifier(sourceId);

        public void SetReloadSpeedModifier(string sourceId, float multiplier)
        {
            reloadSpeedModifiers[sourceId] = Mathf.Max(0f, multiplier);
            float worst = 1f;
            foreach (var value in reloadSpeedModifiers.Values) worst = Mathf.Max(worst, value);
            ReloadSpeedMultiplier = worst;
        }

        public void ClearReloadSpeedModifier(string sourceId)
        {
            if (!reloadSpeedModifiers.Remove(sourceId)) return;
            float worst = 1f;
            foreach (var value in reloadSpeedModifiers.Values) worst = Mathf.Max(worst, value);
            ReloadSpeedMultiplier = worst;
        }

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.FirePressed += HandleFirePressed;
                input.AimPressed += HandleAimPressed;
                input.AimReleased += HandleAimReleased;
                input.ReloadPressed += HandleReloadPressed;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.FirePressed -= HandleFirePressed;
                input.AimPressed -= HandleAimPressed;
                input.AimReleased -= HandleAimReleased;
                input.ReloadPressed -= HandleReloadPressed;
            }
        }

        private void Update()
        {
            var def = equipment != null ? equipment.GetActiveDefinition() : null;
            float baseSway = def != null ? def.baseSwayDegrees : 0f;
            bool moving = movement != null && movement.CurrentSpeed > 0.1f;
            bool crouching = movement != null && movement.IsCrouching;
            float staminaNorm = stamina != null ? stamina.Normalized : 1f;
            CurrentSwayDegrees = sway.ComputeSwayDegrees(baseSway, staminaNorm, crouching, moving, IsAiming);
        }

        private void HandleAimPressed()
        {
            if (!CanAct()) return;
            IsAiming = true;
            if (movement != null) movement.SetSpeedModifier("Aiming", aimSpeedMultiplier);
        }

        private void HandleAimReleased()
        {
            IsAiming = false;
            if (movement != null) movement.ClearSpeedModifier("Aiming");
        }

        private void HandleReloadPressed() => TryReload();

        private void HandleFirePressed()
        {
            if (!CanAct()) return;
            TryFire();
        }

        /// <summary>Gate shared by fire and aim: the inventory panel and the equipment wheel
        /// both claim mouse/Tab input while open, so weapon actions stand down while either is
        /// showing (avoids the two systems fighting over the same LMB/RMB events).</summary>
        private bool CanAct()
        {
            if (hud != null && hud.InventoryOpen) return false;
            if (wheelUI != null && wheelUI.IsOpen) return false;
            return true;
        }

        public bool TryFire()
        {
            if (equipment == null || !equipment.ActiveSlot.HasValue) return false;

            EquipmentSlotType slot = equipment.ActiveSlot.Value;
            WeaponDefinition def = equipment.GetEquipped(slot);
            WeaponRuntimeState state = equipment.GetRuntimeState(slot);
            if (def == null || state == null || def.category == WeaponCategory.Melee) return false; // melee is A2's territory

            if (state.isReloading) return false;
            if (Time.time < state.nextFireTime) return false;

            state.nextFireTime = Time.time + Mathf.Max(0.01f, def.fireRateSeconds);

            if (state.currentMagazine <= 0)
            {
                OnDryFire?.Invoke();
                return false;
            }

            state.currentMagazine--;
            FireShots(def);
            NoiseEvents.Raise(transform.position, def.noiseLoudness);

            if (playerCamera != null)
                playerCamera.AddRecoilKick(def.recoilPitch, UnityEngine.Random.Range(-def.recoilYawRandom, def.recoilYawRandom));

            OnFire?.Invoke();
            return true;
        }

        private void FireShots(WeaponDefinition def)
        {
            Transform origin = raycastCamera != null ? raycastCamera.transform : transform;

            // Cap per Sprint 07 brief — never "hundreds of rays" even if a definition is misconfigured.
            int pellets = Mathf.Clamp(def.pelletCount, 1, 10);
            bool isPelletWeapon = pellets > 1;

            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = ApplySpread(origin.forward, def.spreadAngleDegrees, isPelletWeapon);
                dir = ApplySway(dir);

                // Single-hit Physics.Raycast is zero-alloc (Section 40 target) — no RaycastAll,
                // no per-shot array/list allocation. QueryTriggerInteraction.Ignore is required —
                // without it this raycast would hit invisible trigger volumes (SafeHouseArea,
                // LandingZone, pickup colliders, etc.) before reaching a real target.
                if (Physics.Raycast(origin.position, dir, out RaycastHit hit, def.range, ~0, QueryTriggerInteraction.Ignore))
                    ApplyHit(hit, def);
            }
        }

        private static Vector3 ApplySpread(Vector3 forward, float coneDegrees, bool isPelletWeapon)
        {
            if (!isPelletWeapon || coneDegrees <= 0f) return forward;
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * coneDegrees;
            return Quaternion.Euler(jitter.y, jitter.x, 0f) * forward;
        }

        private Vector3 ApplySway(Vector3 forward)
        {
            if (CurrentSwayDegrees <= 0f) return forward;
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * CurrentSwayDegrees;
            return Quaternion.Euler(jitter.y, jitter.x, 0f) * forward;
        }

        private void ApplyHit(RaycastHit hit, WeaponDefinition def)
        {
            string surfaceType = DetermineSurfaceType(hit.collider);
            OnImpact?.Invoke(hit.point, hit.normal, surfaceType);

            // Matches the pattern already used by WolfAI's own attack code / RockfallPlayerDamage:
            // search up first (most components sit on the hit collider's own object or a parent),
            // fall back to searching down (the player's HealthSystem lives under a "Systems" child).
            var damageable = hit.collider.GetComponentInParent<IDamageable>()
                ?? hit.collider.GetComponentInChildren<IDamageable>();
            if (damageable == null || damageable.IsDead) return;

            // Friendly-fire foundation (Section 29): player never damages a PlayerTeam target.
            var targetFaction = hit.collider.GetComponentInParent<FactionMember>()
                ?? hit.collider.GetComponentInChildren<FactionMember>();
            if (targetFaction != null && targetFaction.faction == Faction.PlayerTeam) return;

            float damage = def.damage * Mathf.Max(0f, DifficultySettings.EnemyDamageMultiplier);
            damageable.TakeDamage(damage, gameObject);
            OnWeaponHit?.Invoke(hit, damage);
        }

        private static string DetermineSurfaceType(Collider hitCollider)
        {
            if (hitCollider.GetComponentInParent<FactionMember>() != null || hitCollider.CompareTag("Player"))
                return "Flesh";
            return string.IsNullOrEmpty(hitCollider.tag) || hitCollider.CompareTag("Untagged") ? "Environment" : hitCollider.tag;
        }

        public bool TryReload()
        {
            if (equipment == null || !equipment.ActiveSlot.HasValue) return false;

            EquipmentSlotType slot = equipment.ActiveSlot.Value;
            WeaponDefinition def = equipment.GetEquipped(slot);
            WeaponRuntimeState state = equipment.GetRuntimeState(slot);
            if (def == null || state == null || def.category == WeaponCategory.Melee) return false;
            if (state.isReloading) return false;
            if (state.currentMagazine >= def.magazineCapacity) return false;
            if (inventory == null || def.requiredAmmo == null) return false;
            if (!inventory.HasItem(def.requiredAmmo, 1)) return false; // wrong/no ammo type: fail gracefully

            reloadRoutine = StartCoroutine(ReloadRoutine(def, state));
            return true;
        }

        private IEnumerator ReloadRoutine(WeaponDefinition def, WeaponRuntimeState state)
        {
            state.isReloading = true;
            OnReloadStart?.Invoke();

            yield return new WaitForSeconds(def.reloadSeconds * ReloadSpeedMultiplier);

            int needed = def.magazineCapacity - state.currentMagazine;
            int available = CountAvailableAmmo(def.requiredAmmo);
            int toLoad = Mathf.Min(needed, available); // partial reload if reserve < capacity gap

            for (int i = 0; i < toLoad; i++)
                inventory.TryRemoveItem(def.requiredAmmo, 1);

            state.currentMagazine += toLoad;
            state.isReloading = false;
            reloadRoutine = null;
            OnReloadComplete?.Invoke();
        }

        private int CountAvailableAmmo(ItemDefinition ammo)
        {
            if (inventory == null || ammo == null) return 0;
            int total = 0;
            foreach (var slotData in inventory.Slots)
                if (slotData.item == ammo) total += slotData.count;
            return total;
        }
    }
}
