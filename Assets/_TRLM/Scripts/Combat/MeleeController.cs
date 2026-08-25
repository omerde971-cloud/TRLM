using System;
using UnityEngine;
using TRLM.Player;
using TRLM.Equipment;
using TRLM.Core;
using TRLM.Survival;
using TRLM.Progression;
using TRLM.UI;

namespace TRLM.Combat
{
    /// <summary>
    /// Drives the player's melee weapon (PlayerEquipment's Melee slot). Mirrors WeaponController's
    /// exact gating pattern so LMB does the contextually correct thing: only acts when the
    /// currently ACTIVE slot is Melee, exactly the same "which system owns FirePressed right now"
    /// rule WeaponController already applies in reverse (it explicitly skips category==Melee).
    /// Reuses A1's WeaponDefinition/WeaponRuntimeState (category=Melee) rather than a parallel
    /// data type — magazine/reload/ammo fields simply go unused, which WeaponController itself
    /// already tolerates. Light-attack only: heavy attacks are explicitly out of scope per the
    /// brief's "only if useful" — a single well-scoped light attack is the honest minimum here.
    /// </summary>
    [RequireComponent(typeof(PlayerEquipment))]
    public class MeleeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private StaminaSystem stamina;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private GameplayHUD hud;
        [SerializeField] private EquipmentWheelUI wheelUI;

        [Header("Attack (fallback values used when the equipped WeaponDefinition leaves a field at 0)")]
        [SerializeField] private float attackRadius = 0.35f;
        [SerializeField] private float staminaCost = 8f;
        [SerializeField] private float fallbackCooldownSeconds = 0.6f;
        [SerializeField] private float fallbackRange = 1.8f;
        [SerializeField] private float fallbackDamage = 18f;
        [SerializeField] private float cameraKickPitch = 1.2f;

        public event Action<RaycastHit, float> OnMeleeHit;
        public event Action OnSwing;

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<PlayerEquipment>();
            if (raycastCamera == null) raycastCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (input != null) input.FirePressed += HandleFirePressed;
        }

        private void OnDisable()
        {
            if (input != null) input.FirePressed -= HandleFirePressed;
        }

        /// <summary>Same UI-claims-input gate WeaponController uses, plus the slot-type check that
        /// makes this a no-op whenever a firearm (or nothing) is actively drawn.</summary>
        private bool CanAct()
        {
            if (hud != null && hud.InventoryOpen) return false;
            if (wheelUI != null && wheelUI.IsOpen) return false;
            if (equipment == null || !equipment.ActiveSlot.HasValue) return false;
            if (equipment.ActiveSlot.Value != EquipmentSlotType.Melee) return false;
            return true;
        }

        private void HandleFirePressed()
        {
            if (!CanAct()) return;
            TryAttack();
        }

        public bool TryAttack()
        {
            if (equipment == null || !equipment.ActiveSlot.HasValue) return false;
            if (equipment.ActiveSlot.Value != EquipmentSlotType.Melee) return false;

            WeaponDefinition def = equipment.GetEquipped(EquipmentSlotType.Melee);
            WeaponRuntimeState state = equipment.GetRuntimeState(EquipmentSlotType.Melee);
            if (state == null) return false;

            if (Time.time < state.nextFireTime) return false; // shared cooldown field, same as firearms

            float cooldown = def != null && def.fireRateSeconds > 0f ? def.fireRateSeconds : fallbackCooldownSeconds;
            state.nextFireTime = Time.time + cooldown;

            // Section 19 — fully exhausted stamina blocks the attack outright (documented choice:
            // this project has no heavy attack to gate instead, so the light attack itself is what
            // "reduced attack availability at low stamina" means here).
            if (stamina != null && stamina.IsExhausted) return false;
            stamina?.ConsumeFlat(staminaCost);

            PerformSwing(def);
            OnSwing?.Invoke();
            return true;
        }

        private void PerformSwing(WeaponDefinition def)
        {
            Transform origin = raycastCamera != null ? raycastCamera.transform : transform;
            float range = def != null && def.range > 0f ? def.range : fallbackRange;
            float damage = def != null && def.damage > 0f ? def.damage : fallbackDamage;

            // Physics.SphereCast reports the FIRST collider along the cast, so a wall standing
            // between the player and a target behind it blocks the hit entirely (Section 46) —
            // no separate line-of-sight raycast needed on top of this. QueryTriggerInteraction.Ignore
            // is required — without it this cast would hit invisible trigger volumes first.
            if (Physics.SphereCast(origin.position, attackRadius, origin.forward, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
                ApplyHit(hit, damage);

            if (playerCamera != null)
                playerCamera.AddRecoilKick(cameraKickPitch, UnityEngine.Random.Range(-0.5f, 0.5f));
        }

        private void ApplyHit(RaycastHit hit, float damage)
        {
            var damageable = hit.collider.GetComponentInParent<IDamageable>()
                ?? hit.collider.GetComponentInChildren<IDamageable>();
            if (damageable == null || damageable.IsDead) return;

            // Friendly-fire foundation — identical rule to WeaponController's firearm hits: a
            // PlayerTeam attacker never damages a PlayerTeam target (e.g. the companion).
            var targetFaction = hit.collider.GetComponentInParent<FactionMember>()
                ?? hit.collider.GetComponentInChildren<FactionMember>();
            if (targetFaction != null && targetFaction.faction == Faction.PlayerTeam) return;

            float applied = damage * Mathf.Max(0f, DifficultySettings.EnemyDamageMultiplier);
            damageable.TakeDamage(applied, gameObject);
            OnMeleeHit?.Invoke(hit, applied);
        }
    }
}
