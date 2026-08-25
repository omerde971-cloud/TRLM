using System.Collections.Generic;
using UnityEngine;
using TRLM.World;

namespace TRLM.AI.Wildlife
{
    /// <summary>
    /// A single habitat/territory. Data + live population tracking only — the actual
    /// timed spawn decision lives in WildlifeSpawner so the two concerns (what/where vs.
    /// when) stay separable. Reuses the Sprint 02 WorldMarker's radius/label for editor
    /// visualization when one exists on the same object, so existing zone markers can gain
    /// this component without losing their gizmo.
    /// </summary>
    [DisallowMultipleComponent]
    public class WildlifeSpawnZone : MonoBehaviour
    {
        [SerializeField] private WildlifeSpeciesProfile species;
        [SerializeField] private float radius = 40f;

        private readonly List<GameObject> activeAnimals = new List<GameObject>();

        public WildlifeSpeciesProfile Species => species;
        public float Radius => radius;
        public Vector3 Center => transform.position;
        public int ActiveCount => activeAnimals.Count;

        private void OnValidate()
        {
            var marker = GetComponent<WorldMarker>();
            if (marker != null && radius <= 0f) radius = marker.radius;
        }

        public bool CanSpawnMore() => species != null && activeAnimals.Count < species.maxPopulation;

        public Vector3 GetRandomPointInZone()
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            return Center + new Vector3(offset.x, 0f, offset.y);
        }

        public void RegisterAnimal(GameObject animal)
        {
            activeAnimals.RemoveAll(a => a == null);
            if (!activeAnimals.Contains(animal)) activeAnimals.Add(animal);
        }

        public void UnregisterAnimal(GameObject animal) => activeAnimals.Remove(animal);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.3f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
