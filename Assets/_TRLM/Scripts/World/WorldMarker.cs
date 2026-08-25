using UnityEngine;

namespace TRLM.World
{
    /// <summary>
    /// Lightweight, editor-only level-design marker. One flexible component covers every
    /// marker category this sprint needs (safe houses, loot, traversal, set-pieces, wildlife
    /// zones, human threat zones, landmarks, storytelling props, water sources) instead of
    /// a separate class per category. No runtime AI/logic lives here yet — future systems
    /// (spawner, save system, AI) read these fields and act on them.
    /// </summary>
    public class WorldMarker : MonoBehaviour
    {
        public enum MarkerType
        {
            SafeHouse,
            LootPoint,
            Traversal,
            SetPiece,
            WildlifeZone,
            HumanThreatZone,
            Landmark,
            StorytellingProp,
            WaterSource
        }

        public enum ActivityPeriod { Day, Night, Both }

        [Header("General")]
        public MarkerType type;
        public string label;
        [TextArea] public string notes;
        public float radius = 10f;

        [Header("Wildlife Zone (only used when type == WildlifeZone)")]
        public string animalType = "Wolf";
        public int maxPopulation = 3;
        public ActivityPeriod activityPeriod = ActivityPeriod.Both;
        [Range(0f, 1f)] public float aggressionLevel = 0.5f;
        [Range(0f, 1f)] public float spawnProbability = 0.5f;
        public float respawnDelaySeconds = 300f;
        public float weatherModifier = 1f;

        private static readonly Color[] GizmoColors =
        {
            new Color(0.2f, 0.9f, 0.3f), // SafeHouse - green
            new Color(0.9f, 0.8f, 0.2f), // LootPoint - yellow
            new Color(0.3f, 0.7f, 0.9f), // Traversal - blue
            new Color(0.9f, 0.2f, 0.9f), // SetPiece - magenta
            new Color(0.8f, 0.3f, 0.1f), // WildlifeZone - burnt orange
            new Color(0.9f, 0.1f, 0.1f), // HumanThreatZone - red
            new Color(1f, 1f, 1f),       // Landmark - white
            new Color(0.6f, 0.6f, 0.6f), // StorytellingProp - grey
            new Color(0.2f, 0.5f, 1f),   // WaterSource - blue
        };

        private void OnDrawGizmos()
        {
            Color c = GizmoColors[(int)type];
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.color = new Color(c.r, c.g, c.b, 0.15f);
            Gizmos.DrawSphere(transform.position, radius);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * (radius + 1f),
                string.IsNullOrEmpty(label) ? name : label);
        }
#endif
    }
}
