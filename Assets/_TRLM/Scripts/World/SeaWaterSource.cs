using UnityEngine;
using TRLM.Interaction;
using TRLM.Survival;

namespace TRLM.World
{
    /// <summary>Shoreline interactable that lets the player drink sea water — see ThirstSystem.DrinkSeaWater for why that's a bad trade.</summary>
    public class SeaWaterSource : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => "Drink Sea Water";

        public void Interact(GameObject interactor)
        {
            var thirst = interactor.GetComponentInParent<ThirstSystem>();
            thirst?.DrinkSeaWater();
        }
    }
}
