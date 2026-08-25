using UnityEngine;
using UnityEngine.InputSystem;

namespace TRLM.Player
{
    /// <summary>
    /// Single source of truth for raw device input. Every other TRLM script reads input
    /// through this component's public properties/events instead of polling
    /// Keyboard/Mouse directly, so the key bindings only ever live in one place.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        private InputAction move;
        private InputAction look;
        private InputAction sprint;
        private InputAction crouch;
        private InputAction jump;
        private InputAction interact;
        private InputAction flashlight;
        private InputAction inventory;
        private InputAction equipmentWheel;
        private InputAction fire;
        private InputAction aim;
        private InputAction reload;
        private InputAction drop;
        private InputAction pause;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool EquipmentWheelHeld { get; private set; }

        public event System.Action JumpPressed;
        public event System.Action InteractPressed;
        public event System.Action FlashlightPressed;
        public event System.Action InventoryPressed;
        public event System.Action FirePressed;
        public event System.Action AimPressed;
        public event System.Action AimReleased;
        public event System.Action ReloadPressed;
        public event System.Action DropPressed;
        public event System.Action PausePressed;

        private void Awake()
        {
            move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");

            sprint = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            crouch = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            flashlight = new InputAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
            inventory = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/i");
            equipmentWheel = new InputAction("EquipmentWheel", InputActionType.Button, "<Keyboard>/tab");
            fire = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
            aim = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
            reload = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
            drop = new InputAction("Drop", InputActionType.Button, "<Keyboard>/g");
            pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");

            move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
            move.canceled += _ => MoveInput = Vector2.zero;
            jump.performed += _ => JumpPressed?.Invoke();
            look.performed += ctx => LookInput = ctx.ReadValue<Vector2>();
            look.canceled += _ => LookInput = Vector2.zero;
            interact.performed += _ => InteractPressed?.Invoke();
            flashlight.performed += _ => FlashlightPressed?.Invoke();
            inventory.performed += _ => InventoryPressed?.Invoke();
            fire.performed += _ => FirePressed?.Invoke();
            aim.performed += _ => AimPressed?.Invoke();
            aim.canceled += _ => AimReleased?.Invoke();
            reload.performed += _ => ReloadPressed?.Invoke();
            drop.performed += _ => DropPressed?.Invoke();
            pause.performed += _ => PausePressed?.Invoke();
        }

        private void OnEnable()
        {
            foreach (var action in AllActions()) action.Enable();
        }

        private void OnDisable()
        {
            foreach (var action in AllActions()) action.Disable();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Vector2 keyboardMove = Vector2.zero;
                if (keyboard.aKey.isPressed) keyboardMove.x -= 1f;
                if (keyboard.dKey.isPressed) keyboardMove.x += 1f;
                if (keyboard.sKey.isPressed) keyboardMove.y -= 1f;
                if (keyboard.wKey.isPressed) keyboardMove.y += 1f;
                if (keyboardMove.sqrMagnitude > 1f) keyboardMove.Normalize();
                MoveInput = keyboardMove;

                SprintHeld = sprint.IsPressed() || keyboard.leftShiftKey.isPressed;
                CrouchHeld = crouch.IsPressed() || keyboard.leftCtrlKey.isPressed;
            }
            else
            {
                SprintHeld = sprint.IsPressed();
                CrouchHeld = crouch.IsPressed();
            }
            EquipmentWheelHeld = equipmentWheel.IsPressed();
        }

        private void LateUpdate()
        {
            LookInput = Vector2.zero;
        }

        private System.Collections.Generic.IEnumerable<InputAction> AllActions()
        {
            yield return move;
            yield return look;
            yield return sprint;
            yield return crouch;
            yield return jump;
            yield return interact;
            yield return flashlight;
            yield return inventory;
            yield return equipmentWheel;
            yield return fire;
            yield return aim;
            yield return reload;
            yield return drop;
            yield return pause;
        }
    }
}
