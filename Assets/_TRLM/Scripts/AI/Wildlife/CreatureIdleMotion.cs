using UnityEngine;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// Cheap procedural "alive" motion for static (unrigged) creature meshes: a subtle breathing
    /// scale pulse plus a slow body sway, phase-offset per instance so a group doesn't move in
    /// lockstep. This is not a substitute for a real animation rig — it just keeps AI-generated
    /// static meshes (bear, etc.) from reading as frozen statues until a proper rig is added.
    /// Attach to the visual child, not the NavMeshAgent root.
    /// </summary>
    public class CreatureIdleMotion : MonoBehaviour
    {
        [Header("Breathing (scale pulse)")]
        [SerializeField] private float breathAmplitude = 0.015f;
        [SerializeField] private float breathSpeed = 1.1f;

        [Header("Sway (rotation)")]
        [SerializeField] private float swayDegrees = 0.8f;
        [SerializeField] private float swaySpeed = 0.7f;

        private Vector3 baseScale;
        private Quaternion baseRot;
        private float phase;

        private void Awake()
        {
            baseScale = transform.localScale;
            baseRot = transform.localRotation;
            phase = (Mathf.Abs(GetInstanceID()) % 628) * 0.01f;
        }

        private void Update()
        {
            float t = Time.time + phase;
            float breath = 1f + Mathf.Sin(t * breathSpeed) * breathAmplitude;
            // Breathe mostly along the vertical (chest rise) with a touch on depth.
            transform.localScale = new Vector3(baseScale.x, baseScale.y * breath, baseScale.z * (1f + (breath - 1f) * 0.5f));

            float sway = Mathf.Sin(t * swaySpeed) * swayDegrees;
            transform.localRotation = baseRot * Quaternion.Euler(sway * 0.3f, 0f, sway);
        }
    }
}
