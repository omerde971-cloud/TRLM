using UnityEngine;

namespace TRLM.Interaction
{
    /// <summary>Proves IInteractable works on something with persistent state. Swings open/closed.</summary>
    public class TestDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 2f;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip closeClip;

        private bool isOpen;
        private Quaternion closedRotation;
        private Quaternion openRotation;

        public string InteractionPrompt => isOpen ? "Close Door" : "Open Door";

        private void Awake()
        {
            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            Quaternion target = isOpen ? openRotation : closedRotation;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * openSpeed);
        }

        public void Interact(GameObject interactor)
        {
            isOpen = !isOpen;
            if (audioSource != null)
            {
                AudioClip clip = isOpen ? openClip : closeClip;
                if (clip != null) audioSource.PlayOneShot(clip, 0.65f);
            }
        }
    }
}
