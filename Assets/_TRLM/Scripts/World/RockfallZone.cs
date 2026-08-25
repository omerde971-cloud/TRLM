using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TRLM.World
{
    /// <summary>
    /// Authored, pooled rockfall event zone. Occasionally launches a pooled rock from a
    /// high spawn point down the slope toward a target area, with real Rigidbody physics
    /// while active. Only zones with this component generate events — nothing spawns
    /// randomly across the mountain. Rocks are pooled and recycled, never destroyed, so
    /// there's no unbounded object growth even after many events.
    /// </summary>
    public class RockfallZone : MonoBehaviour
    {
        [Header("Rock Source")]
        [SerializeField] private GameObject[] rockPrefabs;
        [SerializeField] private int poolSize = 4;

        [Header("Spawn / Target")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Vector3 launchDirection = new Vector3(0f, -0.3f, 1f);
        [SerializeField] private float launchSpeed = 4f;
        [SerializeField] private float scaleMin = 0.8f;
        [SerializeField] private float scaleMax = 2.2f;

        [Header("Timing")]
        [SerializeField] private float minIntervalSeconds = 25f;
        [SerializeField] private float maxIntervalSeconds = 70f;
        [SerializeField] private float settleTimeoutSeconds = 20f;
        [SerializeField] private float settleVelocityThreshold = 0.15f;

        /// <summary>Fired when an active rock's collider hits something — hook point for future impact audio/damage.</summary>
        public event System.Action<Collision> OnRockImpact;

        private readonly List<GameObject> pool = new List<GameObject>();
        private readonly List<Rigidbody> activeRocks = new List<Rigidbody>();

        private void Start()
        {
            if (rockPrefabs == null || rockPrefabs.Length == 0 || spawnPoint == null)
            {
                Debug.LogWarning($"[RockfallZone] {name} is missing rockPrefabs or spawnPoint — disabled.");
                enabled = false;
                return;
            }

            BuildPool();
            StartCoroutine(EventLoop());
        }

        private void BuildPool()
        {
            for (int i = 0; i < poolSize; i++)
            {
                var prefab = rockPrefabs[i % rockPrefabs.Length];
                var inst = Instantiate(prefab, spawnPoint.position, Quaternion.identity, transform);
                inst.name = "RockfallInstance_" + i;

                var rb = inst.GetComponent<Rigidbody>();
                if (rb == null) rb = inst.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                // A moving Rigidbody needs a primitive or convex collider to generate real
                // collisions. Use a fitted sphere here so high-poly dressing rocks do not
                // generate partial convex hull warnings at runtime.
                if (inst.GetComponent<Collider>() == null)
                {
                    var sphere = inst.AddComponent<SphereCollider>();
                    var renderer = inst.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        Vector3 localCenter = inst.transform.InverseTransformPoint(renderer.bounds.center);
                        Vector3 localExtents = inst.transform.InverseTransformVector(renderer.bounds.extents);
                        sphere.center = localCenter;
                        sphere.radius = Mathf.Max(0.25f, Mathf.Max(Mathf.Abs(localExtents.x), Mathf.Abs(localExtents.y), Mathf.Abs(localExtents.z)));
                    }
                }
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var relay = inst.GetComponent<RockfallImpactRelay>();
                if (relay == null) relay = inst.AddComponent<RockfallImpactRelay>();
                relay.Zone = this;

                inst.SetActive(false);
                pool.Add(inst);
            }
        }

        private IEnumerator EventLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minIntervalSeconds, maxIntervalSeconds));
                TriggerRockfall();
            }
        }

        /// <summary>Launch one pooled rock now. Public so a future scripted set-piece can call it directly.</summary>
        public void TriggerRockfall()
        {
            var rock = GetPooledRock();
            if (rock == null) return; // all rocks currently active — skip this cycle rather than spawning more

            rock.transform.position = spawnPoint.position + Random.insideUnitSphere * 0.5f;
            rock.transform.rotation = Random.rotation;
            rock.transform.localScale = Vector3.one * Random.Range(scaleMin, scaleMax);
            rock.SetActive(true);

            var rb = rock.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Vector3 dir = (launchDirection + Random.insideUnitSphere * 0.15f).normalized;
            rb.AddForce(dir * launchSpeed, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * launchSpeed, ForceMode.VelocityChange);

            activeRocks.Add(rb);
            StartCoroutine(ReturnToPoolWhenSettled(rb));
        }

        private GameObject GetPooledRock()
        {
            foreach (var go in pool)
                if (!go.activeSelf) return go;
            return null;
        }

        private IEnumerator ReturnToPoolWhenSettled(Rigidbody rb)
        {
            float elapsed = 0f;
            // Give it a moment to actually start moving before checking for "settled".
            yield return new WaitForSeconds(1.5f);
            elapsed += 1.5f;

            while (elapsed < settleTimeoutSeconds)
            {
                if (rb == null) yield break;
                if (rb.linearVelocity.sqrMagnitude < settleVelocityThreshold * settleVelocityThreshold)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (rb == null) yield break;
            rb.isKinematic = true;
            activeRocks.Remove(rb);
            rb.gameObject.SetActive(false);
        }

        internal void RelayImpact(Collision collision) => OnRockImpact?.Invoke(collision);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (spawnPoint == null) return;
            Gizmos.color = new Color(1f, 0.4f, 0.1f);
            Gizmos.DrawWireSphere(spawnPoint.position, 1f);
            Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + launchDirection.normalized * 15f);
        }
#endif
    }

    /// <summary>Tiny relay so pooled rocks can report impacts back to their owning zone without each needing its own listener wiring.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RockfallImpactRelay : MonoBehaviour
    {
        public RockfallZone Zone { get; set; }
        private void OnCollisionEnter(Collision collision) => Zone?.RelayImpact(collision);
    }
}
