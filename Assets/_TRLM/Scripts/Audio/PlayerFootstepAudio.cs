using UnityEngine;
using TRLM.Player;

namespace TRLM.Audio
{
    [RequireComponent(typeof(FirstPersonController))]
    public class PlayerFootstepAudio : MonoBehaviour
    {
        [SerializeField] private FirstPersonController movement;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private AudioSource source;
        [SerializeField] private LayerMask surfaceMask = ~0;

        [Header("Clips")]
        [SerializeField] private AudioClip[] dirt;
        [SerializeField] private AudioClip[] gravel;
        [SerializeField] private AudioClip[] mud;
        [SerializeField] private AudioClip[] wood;

        [Header("Cadence")]
        [SerializeField] private float walkInterval = 0.62f;
        [SerializeField] private float sprintInterval = 0.38f;
        [SerializeField] private float crouchInterval = 0.82f;
        [SerializeField, Range(0f, 1f)] private float walkVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float sprintVolume = 0.62f;
        [SerializeField, Range(0f, 1f)] private float crouchVolume = 0.22f;

        private float timer;
        private int lastClipIndex = -1;
        private SurfaceFamily lastFamily = SurfaceFamily.Dirt;

        private enum SurfaceFamily { Dirt, Gravel, Mud, Wood }

        private void Awake()
        {
            if (movement == null) movement = GetComponent<FirstPersonController>();
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        private void Update()
        {
            if (movement == null || !movement.enabled || !movement.IsGrounded || movement.CurrentSpeed < 0.18f)
            {
                timer = 0f;
                return;
            }

            float interval = movement.IsCrouching ? crouchInterval : movement.IsSprinting ? sprintInterval : walkInterval;
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            SurfaceFamily family = DetectSurface();
            AudioClip clip = PickClip(ClipsFor(family), family);
            if (clip != null)
            {
                float volume = movement.IsCrouching ? crouchVolume : movement.IsSprinting ? sprintVolume : walkVolume;
                source.pitch = Random.Range(0.96f, 1.04f);
                source.PlayOneShot(clip, volume);
            }

            timer = interval;
        }

        private SurfaceFamily DetectSurface()
        {
            Vector3 origin = transform.position + Vector3.up * 0.25f;
            float distance = characterController != null ? characterController.height * 0.75f : 1.8f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, surfaceMask, QueryTriggerInteraction.Ignore))
                return SurfaceFamily.Dirt;

            string key = $"{hit.collider.name} {hit.collider.tag} {hit.collider.sharedMaterial?.name}".ToLowerInvariant();
            if (key.Contains("wood") || key.Contains("house") || key.Contains("door") || key.Contains("floor")) return SurfaceFamily.Wood;
            if (key.Contains("mud") || key.Contains("wet") || key.Contains("track")) return SurfaceFamily.Mud;
            if (key.Contains("gravel") || key.Contains("rock") || key.Contains("stone") || key.Contains("path")) return SurfaceFamily.Gravel;
            return SurfaceFamily.Dirt;
        }

        private AudioClip[] ClipsFor(SurfaceFamily family) => family switch
        {
            SurfaceFamily.Gravel => gravel,
            SurfaceFamily.Mud => mud,
            SurfaceFamily.Wood => wood,
            _ => dirt,
        };

        private AudioClip PickClip(AudioClip[] clips, SurfaceFamily family)
        {
            if (clips == null || clips.Length == 0) return null;
            if (family != lastFamily) lastClipIndex = -1;
            lastFamily = family;

            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastClipIndex)
                index = (index + 1) % clips.Length;
            lastClipIndex = index;
            return clips[index];
        }
    }
}
