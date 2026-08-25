using UnityEngine;

namespace TRLM.World
{
    /// <summary>
    /// Lightweight sampled-wave buoyancy — no fluid simulation. Evaluates the same
    /// two-layer Gerstner-style wave function the Uber Stylized Water shader uses
    /// (matched against the water material's _1st_Wave_*/_2nd_Wave_* properties) at a
    /// handful of sample points on the hull, and pushes each point toward the wave
    /// surface with a spring-damper force. Asymmetric forces across sample points
    /// naturally produce pitch/roll — no separate rotation logic needed.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BuoyancyController : MonoBehaviour
    {
        [Header("Wave Source (read-only reference, values copied at Awake)")]
        [SerializeField] private Material waterMaterial;
        [SerializeField] private float seaLevel = 1.5f;

        [Header("Sample Points (local space, hull corners)")]
        [SerializeField] private Vector3[] floatPoints = new Vector3[]
        {
            new Vector3(0.6f, 0f, 1.6f),
            new Vector3(-0.6f, 0f, 1.6f),
            new Vector3(0.6f, 0f, -1.6f),
            new Vector3(-0.6f, 0f, -1.6f),
        };

        [Header("Buoyancy Tuning")]
        [SerializeField] private float buoyancyStrength = 12f;
        [SerializeField] private float damping = 2.2f;
        [SerializeField] private float waterDrag = 1.5f;
        [SerializeField] private float waterAngularDrag = 1.2f;

        private Rigidbody rb;
        private float wave1Length = 3f, wave1Height = 0.01f, wave1Speed = 1f, wave1Sharpness = 0.3f;
        private Vector2 wave1Dir = Vector2.right;
        private float wave2Length = 5f, wave2Height = 0.015f, wave2Speed = 1.3f, wave2Sharpness = 0.3f;
        private Vector2 wave2Dir = new Vector2(-1f, 1f);

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.linearDamping = 0f; // we apply our own water drag only while in contact with the surface
            rb.angularDamping = 0.05f;

            if (waterMaterial != null)
            {
                wave1Length = waterMaterial.GetFloat("_1st_Wave_Length");
                wave1Height = waterMaterial.GetFloat("_1st_Wave_Height");
                wave1Speed = waterMaterial.GetFloat("_1st_Wave_Speed");
                wave1Sharpness = waterMaterial.GetFloat("_1st_Wave_Sharpness");
                var d1 = waterMaterial.GetVector("_1st_Wave_Direction");
                wave1Dir = new Vector2(d1.x, d1.z).normalized;

                wave2Length = waterMaterial.GetFloat("_2nd_Wave_Length");
                wave2Height = waterMaterial.GetFloat("_2nd_Wave_Height");
                wave2Speed = waterMaterial.GetFloat("_2nd_Wave_Speed");
                wave2Sharpness = waterMaterial.GetFloat("_2nd_Wave_Sharpness");
                var d2 = waterMaterial.GetVector("_2nd_Wave_Direction");
                wave2Dir = new Vector2(d2.x, d2.z).normalized;
            }
        }

        /// <summary>Wave height offset above seaLevel at a given world XZ, matching the water shader's wave layers (scaled up for visible gameplay motion — the shader's own values are tuned tiny/subtle).</summary>
        public float SampleWaveHeight(float worldX, float worldZ, float visualMultiplier = 25f)
        {
            float t = Time.time;
            float h1 = Mathf.Sin(Vector2.Dot(new Vector2(worldX, worldZ), wave1Dir) / Mathf.Max(0.01f, wave1Length) + t * wave1Speed);
            h1 = Mathf.Sign(h1) * Mathf.Pow(Mathf.Abs(h1), Mathf.Lerp(1f, 0.4f, wave1Sharpness));
            float h2 = Mathf.Sin(Vector2.Dot(new Vector2(worldX, worldZ), wave2Dir) / Mathf.Max(0.01f, wave2Length) + t * wave2Speed);
            h2 = Mathf.Sign(h2) * Mathf.Pow(Mathf.Abs(h2), Mathf.Lerp(1f, 0.4f, wave2Sharpness));
            return (h1 * wave1Height + h2 * wave2Height) * visualMultiplier;
        }

        private void FixedUpdate()
        {
            int submerged = 0;
            foreach (var localPoint in floatPoints)
            {
                Vector3 worldPoint = transform.TransformPoint(localPoint);
                float waveY = seaLevel + SampleWaveHeight(worldPoint.x, worldPoint.z);
                float depth = waveY - worldPoint.y;

                if (depth > 0f)
                {
                    submerged++;
                    float upForce = depth * buoyancyStrength;
                    Vector3 pointVelocity = rb.GetPointVelocity(worldPoint);
                    upForce -= pointVelocity.y * damping;
                    rb.AddForceAtPosition(Vector3.up * upForce, worldPoint, ForceMode.Acceleration);
                }
            }

            if (submerged > 0)
            {
                rb.AddForce(-rb.linearVelocity * waterDrag, ForceMode.Acceleration);
                rb.AddTorque(-rb.angularVelocity * waterAngularDrag, ForceMode.Acceleration);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            foreach (var p in floatPoints)
                Gizmos.DrawSphere(transform.TransformPoint(p), 0.1f);
        }
#endif
    }
}
