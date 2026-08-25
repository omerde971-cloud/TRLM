using UnityEngine;

namespace TRLM.Environment
{
    /// <summary>
    /// Cheap, animation-free wind sway: rotates the transform slightly around a sine wave instead
    /// of displacing vertices in a custom shader (the tree/grass materials are plain URP/Lit —
    /// adding a hand-authored wind shader would risk breaking lighting/shadows for a restrained
    /// visual ask). Placed on tree canopy/branches children (trunks stay rigid) and on grass patch
    /// roots. Phase is randomized per-instance from GetInstanceID so a whole forest doesn't sway
    /// in lockstep. No allocations, no GetComponent calls per frame.
    /// </summary>
    public class WindSway : MonoBehaviour
    {
        [SerializeField] private float swayDegrees = 2.5f;
        [SerializeField] private float swaySpeed = 0.6f;

        private Quaternion baseRotation;
        private float phase;

        private void Awake()
        {
            baseRotation = transform.localRotation;
            phase = (Mathf.Abs(GetInstanceID()) % 1000) * 0.01f; // 0..10, deterministic per-instance offset
        }

        private void Update()
        {
            float t = Time.time * swaySpeed + phase;
            float x = Mathf.Sin(t) * swayDegrees;
            float z = Mathf.Cos(t * 0.7f) * swayDegrees * 0.6f;
            transform.localRotation = baseRotation * Quaternion.Euler(x, 0f, z);
        }
    }
}
