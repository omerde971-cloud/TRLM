using UnityEngine;

namespace TRLM.Player
{
    /// <summary>
    /// The player's visible body (FirstPersonBody, a PF_Elias instance) exists so hands are
    /// visible in first person during normal play (running/jumping/firing) — not so the player
    /// sees their own torso, head, or feet. CC_Base_Body is a single SkinnedMeshRenderer with
    /// per-region submeshes (Head/Body/Arm/Leg/Nails/Eyelash); Head/Body/Leg/Eyelash are swapped
    /// for an invisible material (no color, no depth write — so it can't occlude the camera
    /// either) permanently, and the small head-attachment renderers (eyes, teeth, tongue,
    /// tearline) are disabled. The Arm submesh (which carries the hands) additionally hides
    /// itself once the camera pitches down past armHidePitch — full arm/forearm geometry looks
    /// wrong dangling in view when glancing at the ground; only a glimpse near the wrist should
    /// remain, which the collapsed-when-idle stance already gives once the rest is hidden. Runs
    /// only on the player's own body instance — PF_Elias's canonical prefab (used elsewhere, e.g.
    /// cutscenes) is untouched.
    /// </summary>
    public class FirstPersonBodyMask : MonoBehaviour
    {
        [SerializeField] private Material invisibleMaterial;
        [SerializeField] private string[] alwaysHiddenSubmeshMaterialNames = { "Std_Skin_Head_Diffuse", "Std_Skin_Body_Diffuse", "Std_Skin_Leg_Diffuse", "Std_Eyelash_Diffuse" };
        [SerializeField] private string armSubmeshMaterialName = "Std_Skin_Arm_Diffuse";
        [SerializeField] private string[] hiddenAttachmentNames = { "CC_Base_Eye", "CC_Base_EyeOcclusion", "CC_Base_TearLine", "CC_Base_Teeth", "CC_Base_Tongue" };

        [Tooltip("Camera pitch (degrees, positive = looking down) past which the arm/hand mesh hides.")]
        [SerializeField] private float armHidePitch = 35f;
        [SerializeField] private Transform cameraPivot; // CameraRoot; auto-found from the parent (PF_Player) if left empty

        private SkinnedMeshRenderer bodyRenderer;
        private Material[] materials;
        private Material realArmMaterial;
        private int armIndex = -1;
        private bool armCurrentlyVisible = true;

        private void Awake()
        {
            if (cameraPivot == null && transform.parent != null)
                cameraPivot = transform.parent.Find("CameraRoot");

            bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (bodyRenderer == null || invisibleMaterial == null) return;

            materials = bodyRenderer.materials; // instance copy, safe to edit without touching the shared asset
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;

                if (materials[i].name.StartsWith(armSubmeshMaterialName))
                {
                    armIndex = i;
                    realArmMaterial = materials[i];
                    continue;
                }

                foreach (var hiddenName in alwaysHiddenSubmeshMaterialNames)
                {
                    if (materials[i].name.StartsWith(hiddenName))
                    {
                        materials[i] = invisibleMaterial;
                        break;
                    }
                }
            }
            bodyRenderer.materials = materials;

            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (var hiddenName in hiddenAttachmentNames)
                {
                    if (renderer.gameObject.name == hiddenName)
                    {
                        renderer.enabled = false;
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            if (armIndex < 0 || cameraPivot == null) return;

            float pitch = cameraPivot.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f; // normalize to [-180,180]; positive = looking down

            bool shouldBeVisible = pitch < armHidePitch;
            if (shouldBeVisible == armCurrentlyVisible) return;

            armCurrentlyVisible = shouldBeVisible;
            materials[armIndex] = shouldBeVisible ? realArmMaterial : invisibleMaterial;
            bodyRenderer.materials = materials;
        }

        // Walk_N/Run_N carry a baked "OnFootstep" AnimationEvent; Unity sends it via SendMessage
        // to the GameObject holding the Animator (this one), not to PF_Player's driver script.
        private void OnFootstep(AnimationEvent animationEvent) { }
    }
}
