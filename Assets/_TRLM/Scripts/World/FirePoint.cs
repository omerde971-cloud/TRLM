using System.Collections.Generic;
using UnityEngine;
using TRLM.Interaction;
using TRLM.Inventory;

namespace TRLM.World
{
    /// <summary>
    /// Lightable campfire. Costs Wood from the interactor's inventory to light. Maintains a
    /// static registry of currently-lit fires so other systems (WetnessColdSystem's warmth
    /// radius, the wildlife "fires discourage wolves" hook owned by another agent) can query
    /// nearby fires cheaply instead of scanning the scene.
    /// </summary>
    public class FirePoint : MonoBehaviour, IInteractable
    {
        [Header("Fuel")]
        [SerializeField] private ItemDefinition woodItem;
        [SerializeField] private int woodCost = 1;

        [Header("Visual")]
        [SerializeField] private Light fireLight; // assign in Inspector, or one is created at runtime
        [SerializeField] private Color fireLightColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private float fireLightRange = 8f;
        [SerializeField] private float fireLightIntensity = 2.5f;
        [SerializeField] private Transform visualPlaceholder; // scaled-up emissive primitive stand-in for real fire VFX (future art pass)
        [SerializeField] private AudioSource fireAudioSource;
        [SerializeField] private AudioClip campfireLoopClip;
        [SerializeField, Range(0f, 1f)] private float campfireVolume = 0.35f;

        /// <summary>All currently-lit fires in the scene. Query this instead of scanning FindObjectsOfType.</summary>
        public static readonly List<FirePoint> ActiveLitFires = new List<FirePoint>();

        /// <summary>Fires once when this FirePoint transitions to lit. Added for ObjectiveSystem's
        /// LightFire step hook (Sub-Agent B2); no other behavior changed.</summary>
        public event System.Action OnLit;

        public bool IsLit { get; private set; }

        public string InteractionPrompt => IsLit ? "Fire Lit" : $"Light Fire ({woodCost} Wood)";

        public void Interact(GameObject interactor)
        {
            if (IsLit) return;

            var inventory = interactor.GetComponentInParent<PlayerInventory>();
            if (inventory == null || !inventory.TryRemoveItem(woodItem, woodCost)) return;

            Light();
        }

        /// <summary>Save/load restore only — relights without the wood cost/inventory check a real
        /// Interact() requires, since the wood was already spent last session.</summary>
        public void RestoreLit()
        {
            if (!IsLit) Light();
        }

        private void Light()
        {
            IsLit = true;

            if (fireLight == null)
            {
                var lightGo = new GameObject("FireLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 0.5f;
                fireLight = lightGo.AddComponent<Light>();
            }
            fireLight.type = LightType.Point;
            fireLight.color = fireLightColor;
            fireLight.range = fireLightRange;
            fireLight.intensity = fireLightIntensity;
            fireLight.enabled = true;

            // No real fire VFX yet — an enlarged emissive placeholder stands in (future art pass).
            if (visualPlaceholder != null)
                visualPlaceholder.gameObject.SetActive(true);

            if (fireAudioSource == null) fireAudioSource = GetComponent<AudioSource>();
            if (fireAudioSource != null && campfireLoopClip != null)
            {
                fireAudioSource.clip = campfireLoopClip;
                fireAudioSource.loop = true;
                fireAudioSource.volume = campfireVolume;
                fireAudioSource.spatialBlend = 1f;
                fireAudioSource.minDistance = 1.5f;
                fireAudioSource.maxDistance = 14f;
                if (!fireAudioSource.isPlaying) fireAudioSource.Play();
            }

            ActiveLitFires.Add(this);
            OnLit?.Invoke();
        }

        private void OnDisable()
        {
            ActiveLitFires.Remove(this);
        }
    }
}
