using System;
using UnityEngine;

namespace TRLM.Environment
{
    /// <summary>
    /// Applies persistent per-bone localScale overrides to a skinned rig at startup. Used by
    /// the bear (a CC0 Quaternius quadruped base re-proportioned into ursine bulk: heavier
    /// torso, shoulder hump, stub tail) — animation clips animate bone position/rotation only,
    /// so scale set once here survives every clip. Runs in LateUpdate for the first few frames
    /// too, in case an Animator rebind resets scales on the first evaluated frame.
    /// </summary>
    public class BoneProportionOverride : MonoBehaviour
    {
        [Serializable]
        public struct BoneScale
        {
            [Tooltip("Bone name to match (exact, or a substring when matchSubstring is on).")]
            public string boneName;
            public Vector3 scale;
            public bool matchSubstring;
        }

        [SerializeField] private BoneScale[] overrides = Array.Empty<BoneScale>();
        [Tooltip("Frames after enable during which scales are re-applied (Animator rebind safety).")]
        [SerializeField] private int reapplyFrames = 3;

        private Transform[] targets;
        private Vector3[] scales;
        private int framesLeft;

        private void OnEnable()
        {
            Collect();
            framesLeft = Mathf.Max(1, reapplyFrames);
        }

        private void Collect()
        {
            var all = GetComponentsInChildren<Transform>(true);
            int count = 0;
            var t = new Transform[overrides.Length * 4];
            var s = new Vector3[overrides.Length * 4];
            foreach (var o in overrides)
            {
                if (string.IsNullOrEmpty(o.boneName)) continue;
                foreach (var tr in all)
                {
                    bool match = o.matchSubstring
                        ? tr.name.IndexOf(o.boneName, StringComparison.OrdinalIgnoreCase) >= 0
                        : tr.name.Equals(o.boneName, StringComparison.OrdinalIgnoreCase);
                    if (!match) continue;
                    if (count >= t.Length)
                    {
                        Array.Resize(ref t, t.Length * 2);
                        Array.Resize(ref s, s.Length * 2);
                    }
                    t[count] = tr;
                    s[count] = o.scale;
                    count++;
                }
            }
            targets = new Transform[count];
            scales = new Vector3[count];
            Array.Copy(t, targets, count);
            Array.Copy(s, scales, count);
            Apply();
        }

        private void LateUpdate()
        {
            if (framesLeft <= 0) { enabled = false; return; }
            framesLeft--;
            Apply();
        }

        private void Apply()
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) targets[i].localScale = scales[i];
        }

        /// <summary>Editor/setup helper: overwrite the override table from code.</summary>
        public void SetOverrides(BoneScale[] newOverrides)
        {
            overrides = newOverrides ?? Array.Empty<BoneScale>();
            if (isActiveAndEnabled) OnEnable();
        }
    }
}
