using UnityEngine;
using TRLM.Player;

namespace TRLM.Player
{
    /// <summary>
    /// Same pattern as Companions.CompanionLocomotionAnimator, for the player's own visible body
    /// (added so Elias's hands/feet are visible in first person, not just a bare camera). Drives
    /// the body Animator's "Speed" float from FirstPersonController.CurrentSpeed.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class PlayerBodyLocomotionAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        [SerializeField] private Animator bodyAnimator;

        private FirstPersonController movement;

        private void Awake()
        {
            movement = GetComponent<FirstPersonController>();
        }

        private void Update()
        {
            if (bodyAnimator == null) return;
            bodyAnimator.SetFloat(SpeedHash, movement.CurrentSpeed);
        }
    }
}
