using UnityEngine;

namespace TRLM.Player
{
    /// <summary>
    /// Last-resort failsafe against falling through the world: remembers the most recent grounded
    /// position (sampled on a slow timer, only while safely above the kill height), and if the
    /// player ever drops below killY — a state normal gameplay can never reach on the island —
    /// teleports them back to that spot instead of letting them fall forever. Applies no damage:
    /// tunnelling through a collider is an engine failure, not a player mistake.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerVoidRecovery : MonoBehaviour
    {
        [SerializeField] private float killY = -30f;
        [SerializeField] private float sampleIntervalSeconds = 2f;

        private CharacterController controller;
        private Vector3 lastSafePosition;
        private bool hasSafePosition;
        private float sampleTimer;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (transform.position.y < killY)
            {
                Recover();
                return;
            }

            sampleTimer += Time.deltaTime;
            if (sampleTimer < sampleIntervalSeconds) return;
            sampleTimer = 0f;

            if (controller.enabled && controller.isGrounded)
            {
                lastSafePosition = transform.position;
                hasSafePosition = true;
            }
        }

        private void Recover()
        {
            Vector3 target = hasSafePosition ? lastSafePosition + Vector3.up * 0.5f : FallbackPosition();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = target;
            controller.enabled = wasEnabled;
            Debug.LogWarning($"[PlayerVoidRecovery] Player fell below {killY}; recovered to {target}.");
        }

        private Vector3 FallbackPosition()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                Vector3 pos = transform.position;
                float h = terrain.SampleHeight(pos) + terrain.transform.position.y;
                return new Vector3(pos.x, h + 1.5f, pos.z);
            }
            return Vector3.up * 5f;
        }
    }
}
