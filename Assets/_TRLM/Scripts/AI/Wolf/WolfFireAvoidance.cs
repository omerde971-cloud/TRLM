using UnityEngine;
using UnityEngine.AI;
using TRLM.World;

namespace TRLM.AI.Wolf
{
    /// <summary>
    /// Soft fire-avoidance nudge, added alongside WolfAI without modifying it. WolfAI does not
    /// expose a state-influence hook safe to call into non-invasively (its EnterState/Tick methods
    /// are private and its State enum has no public setter), so the honest, scoped fallback here
    /// is: while the wolf is NOT in Chase/Attack (read via the public CurrentState property) and a
    /// lit fire is within fireAvoidRadius, bias the NavMeshAgent's destination away from the fire
    /// with a small extra move. This is intentionally soft — it never blocks or overrides an
    /// active chase/attack, and it never sets a hard "no-go" zone.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(WolfAI))]
    public class WolfFireAvoidance : MonoBehaviour
    {
        [SerializeField] private float fireAvoidRadius = 10f;
        [SerializeField] private float nudgeStrength = 2.5f;
        [SerializeField] private float checkIntervalSeconds = 1f;

        private NavMeshAgent agent;
        private WolfAI wolfAI;
        private float checkTimer;

        public bool NearActiveFire { get; private set; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            wolfAI = GetComponent<WolfAI>();
        }

        private void Update()
        {
            checkTimer += Time.deltaTime;
            if (checkTimer < checkIntervalSeconds) return;
            checkTimer = 0f;

            NearActiveFire = FindNearestLitFire(out Vector3 firePos);
            if (!NearActiveFire) return;

            if (wolfAI.CurrentState == WolfAI.State.Chase || wolfAI.CurrentState == WolfAI.State.Attack)
                return; // never override an active hunt

            if (!agent.isOnNavMesh || agent.pathPending) return;

            Vector3 away = (transform.position - firePos);
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            away.Normalize();

            Vector3 nudged = transform.position + away * nudgeStrength;
            if (NavMesh.SamplePosition(nudged, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        private bool FindNearestLitFire(out Vector3 position)
        {
            position = default;
            float best = fireAvoidRadius;
            bool found = false;

            foreach (var fire in FirePoint.ActiveLitFires)
            {
                if (fire == null || !fire.IsLit) continue;
                float dist = Vector3.Distance(fire.transform.position, transform.position);
                if (dist <= best)
                {
                    best = dist;
                    position = fire.transform.position;
                    found = true;
                }
            }
            return found;
        }
    }
}
