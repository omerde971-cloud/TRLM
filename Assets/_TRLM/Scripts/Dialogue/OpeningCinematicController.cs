using System.Collections;
using UnityEngine;
using TRLM.Progression;
using TRLM.Flow;

namespace TRLM.Dialogue
{
    /// <summary>
    /// Lightweight authored opening cinematic controller for Sprint 11. It uses existing scene
    /// camera markers and prop/character transforms instead of building a bespoke Timeline graph,
    /// which keeps the scene playable while final animation/audio assets are still missing.
    /// </summary>
    public class OpeningCinematicController : MonoBehaviour
    {
        [System.Serializable]
        public class CinematicBeat
        {
            public string id;
            public Transform cameraMarker;
            public float holdSeconds = 5f;
            public DialogueLine[] lines;
        }

        [Header("Scene")]
        [SerializeField] private Camera cinematicCamera;
        [SerializeField] private string islandSceneName = "20_Island_Blockout";
        [SerializeField] private bool playOnStart = true;

        [Header("Blocking")]
        [SerializeField] private Transform elias;
        [SerializeField] private Transform mira;
        [SerializeField] private Transform jonah;
        [SerializeField] private Transform lena;
        [SerializeField] private Transform noah;
        [SerializeField] private Transform gearFocus;
        [SerializeField] private Transform mapFocus;
        [SerializeField] private Transform boatFocus;

        [Header("Beats")]
        [SerializeField] private CinematicBeat[] beats;
        [SerializeField] private float sceneEndDelaySeconds = 1.5f;

        private bool started;
        private bool completed;

        private void Start()
        {
            if (playOnStart) Begin();
        }

        public void Begin()
        {
            if (started) return;
            started = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            BlockCharacters();

            if (cinematicCamera != null)
            {
                cinematicCamera.gameObject.SetActive(true);
                cinematicCamera.enabled = true;
            }

            if (beats != null)
            {
                foreach (var beat in beats)
                {
                    if (beat == null) continue;
                    ApplyCameraMarker(beat.cameraMarker);
                    if (beat.lines != null)
                    {
                        foreach (var line in beat.lines)
                            DialogueSystem.Instance?.Play(line);
                    }

                    yield return new WaitForSeconds(Mathf.Max(0.5f, beat.holdSeconds));
                }
            }

            yield return new WaitForSeconds(sceneEndDelaySeconds);
            ObjectiveSystem.Instance?.AdvanceTo(ObjectiveStep.PreparationComplete);
            if (completed) yield break;
            completed = true;
            SceneFlow.RequestLoad(islandSceneName, "OpeningCinematicComplete", this);
        }

        private void ApplyCameraMarker(Transform marker)
        {
            if (cinematicCamera == null || marker == null) return;
            cinematicCamera.transform.SetPositionAndRotation(marker.position, marker.rotation);
        }

        private void BlockCharacters()
        {
            Place(elias, new Vector3(-1.2f, 0f, 1.5f), 35f, 1.02f);
            Place(mira, new Vector3(1.2f, 0f, 5.9f), 200f, 0.98f);
            Place(jonah, new Vector3(-2.8f, 0f, 2.5f), 80f, 1.05f);
            Place(lena, new Vector3(2.4f, 0f, 6.9f), 205f, 0.96f);
            Place(noah, new Vector3(-4.4f, 0f, 1.1f), 100f, 1.04f);

            Face(elias, boatFocus);
            Face(mira, mapFocus);
            Face(jonah, gearFocus);
            Face(lena, mira);
            Face(noah, boatFocus);
        }

        private static void Place(Transform target, Vector3 position, float yaw, float scale)
        {
            if (target == null) return;
            target.position = position;
            target.rotation = Quaternion.Euler(0f, yaw, 0f);
            target.localScale = Vector3.one * scale;
        }

        private static void Face(Transform target, Transform lookAt)
        {
            if (target == null || lookAt == null) return;
            Vector3 direction = lookAt.position - target.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            target.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
