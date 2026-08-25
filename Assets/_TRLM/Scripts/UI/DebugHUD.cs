using UnityEngine;
using TRLM.Player;
using TRLM.Survival;

namespace TRLM.UI
{
    /// <summary>
    /// Developer-only debug overlay (Health/Stamina/Speed/Grounded). Not final game UI —
    /// just toggle the "enabled" checkbox or delete this component to remove it.
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        [SerializeField] private HealthSystem health;
        [SerializeField] private StaminaSystem stamina;
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private bool visible = false;

        private void Update()
        {
            if (Keyboard_F3Pressed())
                visible = !visible;
        }

        private bool Keyboard_F3Pressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.f3Key.wasPressedThisFrame;
        }

        private void OnGUI()
        {
            if (!visible) return;

            GUI.Box(new Rect(10, 10, 220, 110), "TRLM DEBUG (F3 to hide)");

            int y = 30;
            if (health != null)
                GUI.Label(new Rect(20, y, 200, 20), $"Health: {health.CurrentHealth:0}/{health.MaxHealth:0}");
            y += 20;

            if (stamina != null)
                GUI.Label(new Rect(20, y, 200, 20), $"Stamina: {stamina.CurrentStamina:0}/{stamina.MaxStamina:0}");
            y += 20;

            if (controller != null)
            {
                GUI.Label(new Rect(20, y, 200, 20), $"Speed: {controller.CurrentSpeed:0.00} m/s");
                y += 20;
                GUI.Label(new Rect(20, y, 200, 20), $"Grounded: {controller.IsGrounded}");
            }
        }
    }
}
