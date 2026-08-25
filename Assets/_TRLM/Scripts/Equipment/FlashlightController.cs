using System;
using UnityEngine;
using TRLM.Player;
using TRLM.Inventory;

namespace TRLM.Equipment
{
    /// <summary>
    /// Player-worn flashlight. Toggled with F (PlayerInputHandler.FlashlightPressed), drains a
    /// battery meter while on, flickers near-empty, and auto-shuts-off at 0%. Battery swap is
    /// bound to R (ReloadPressed) — "reload" has no firearm to attach to yet this sprint, so
    /// reusing it here for "replace battery" is a deliberate, documented contextual reuse rather
    /// than a real second meaning; it only does anything while the flashlight exists on the player.
    /// </summary>
    public class FlashlightController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private Light spotLight; // created under CameraRoot/MainCamera if not assigned
        [SerializeField] private ItemDefinition batteryItem;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip switchOnClip;
        [SerializeField] private AudioClip switchOffClip;

        [Header("Battery")]
        // Quality Pass #1: 3%/sec emptied in ~33s, forcing a battery swap almost every time the
        // light was used — busywork rather than an occasional resource decision. 1.2%/sec gives
        // ~83s of continuous use; the flicker window at lowBatteryThreshold stays proportional
        // since it's percentage-based, not time-based.
        [SerializeField] private float drainPerSecond = 1.2f;
        [SerializeField] private float lowBatteryThreshold = 20f;
        [SerializeField] private float flickerIntensityJitter = 0.4f;
        [SerializeField] private float flickerSpeed = 14f;

        [Header("Light Settings")]
        [SerializeField] private float baseIntensity = 4f;
        [SerializeField] private float spotAngle = 45f;
        [SerializeField] private float range = 18f;

        private float batteryPercent = 100f;
        private bool isOn;
        private bool tutorialShown;

        public event Action<float> OnBatteryChanged;

        public bool IsOn => isOn;
        public float BatteryPercent => batteryPercent;

        private void Awake()
        {
            if (spotLight == null)
                spotLight = CreateDefaultLight();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private Light CreateDefaultLight()
        {
            Transform cam = transform.Find("CameraRoot/MainCamera");
            Transform parent = cam != null ? cam : transform;

            var go = new GameObject("FlashlightSpot");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = spotAngle;
            light.range = range;
            light.intensity = baseIntensity;
            light.enabled = false;
            return light;
        }

        private void OnEnable()
        {
            if (input != null)
            {
                input.FlashlightPressed += HandleFlashlightPressed;
                input.ReloadPressed += HandleReloadPressed;
            }
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.FlashlightPressed -= HandleFlashlightPressed;
                input.ReloadPressed -= HandleReloadPressed;
            }
        }

        private void Update()
        {
            if (!isOn) return;

            SetBattery(batteryPercent - drainPerSecond * Time.deltaTime);

            if (batteryPercent <= 0f)
            {
                SetOn(false);
                return;
            }

            if (spotLight != null)
            {
                float flicker = batteryPercent <= lowBatteryThreshold
                    ? 1f - flickerIntensityJitter * Mathf.Abs(Mathf.Sin(Time.time * flickerSpeed) * UnityEngine.Random.value)
                    : 1f;
                spotLight.intensity = baseIntensity * flicker;
            }
        }

        private void HandleFlashlightPressed()
        {
            SetOn(!isOn);
            if (!tutorialShown && isOn)
            {
                tutorialShown = true;
                TRLM.UI.SimpleTutorialPrompt.ShowGlobal("F — Flashlight  |  R — Replace Battery", 3f);
            }
        }

        private void HandleReloadPressed()
        {
            TryReplaceBattery();
        }

        private void SetOn(bool value)
        {
            if (isOn == value) return;
            isOn = value && batteryPercent > 0f;
            if (spotLight != null) spotLight.enabled = isOn;
            if (audioSource != null)
            {
                AudioClip clip = isOn ? switchOnClip : switchOffClip;
                if (clip != null) audioSource.PlayOneShot(clip, 0.55f);
            }
        }

        /// <summary>Consumes one Battery item to refill to 100%. Returns false if none available.</summary>
        public bool TryReplaceBattery()
        {
            if (inventory == null || batteryItem == null) return false;
            if (batteryPercent >= 100f) return false;
            if (!inventory.TryRemoveItem(batteryItem, 1)) return false;

            SetBattery(100f);
            return true;
        }

        private void SetBattery(float value)
        {
            batteryPercent = Mathf.Clamp(value, 0f, 100f);
            OnBatteryChanged?.Invoke(batteryPercent);
        }

        /// <summary>Save/load restore only — sets the battery directly without consuming a Battery
        /// item, and turns the light off if the restored charge is empty.</summary>
        public void RestoreBattery(float percent)
        {
            SetBattery(percent);
            if (batteryPercent <= 0f) SetOn(false);
        }
    }
}
