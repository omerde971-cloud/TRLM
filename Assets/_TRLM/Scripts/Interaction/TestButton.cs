using UnityEngine;

namespace TRLM.Interaction
{
    /// <summary>Proves IInteractable works for a repeatable "trigger an effect" object.</summary>
    public class TestButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color pressedColor = Color.green;
        [SerializeField] private Color idleColor = Color.gray;

        private bool pressed;
        private MaterialPropertyBlock propertyBlock;

        public string InteractionPrompt => "Press Button";

        public void Interact(GameObject interactor)
        {
            pressed = !pressed;
            Debug.Log($"[TestButton] Button toggled: {pressed}");

            if (targetRenderer == null) return;

            // MaterialPropertyBlock avoids instantiating a per-object material copy
            // (renderer.material would leak a new material instance every call).
            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", pressed ? pressedColor : idleColor);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
