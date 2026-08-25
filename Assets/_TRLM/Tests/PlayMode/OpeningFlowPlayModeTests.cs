using System.Collections;
using NUnit.Framework;
using TRLM.Boat;
using TRLM.Flow;
using TRLM.Player;
using TRLM.Progression;
using TRLM.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TRLM.Tests
{
    public class OpeningFlowPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator MainMenuNewGame_StartsPlayableRowboat_ThenLandsWithLandMovement()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();

            PendingLoad.ClearAll();
            SceneManager.LoadScene(SceneFlow.MainMenuScene);
            yield return WaitForScene(SceneFlow.MainMenuScene);

            var newGameButton = GameObject.Find("NewGameButton")?.GetComponent<Button>();
            Assert.NotNull(newGameButton, "Main menu must expose the NewGameButton.");
            newGameButton.onClick.Invoke();
            yield return WaitForScene(SceneFlow.IslandScene);
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.AreNotEqual("05_Neighborhood_Cinematic", SceneManager.GetActiveScene().name);

            var boat = Object.FindFirstObjectByType<RowboatController>();
            var player = GameObject.FindGameObjectWithTag("Player");
            Assert.NotNull(boat, "Island scene must contain a rowboat controller.");
            Assert.NotNull(player, "Island scene must contain the player.");
            Assert.IsTrue(boat.IsRowing, "New Game should start with the player already rowing.");

            var input = player.GetComponent<PlayerInputHandler>();
            var movement = player.GetComponent<FirstPersonController>();
            var controller = player.GetComponent<CharacterController>();
            var camera = player.GetComponentInChildren<PlayerCamera>();
            Assert.NotNull(input);
            Assert.NotNull(movement);
            Assert.NotNull(controller);
            Assert.NotNull(camera);
            Assert.IsTrue(input.enabled, "Input must remain enabled during rowing.");
            Assert.IsFalse(movement.enabled, "Land movement should be suspended only while seated.");
            Assert.IsFalse(controller.enabled, "CharacterController should be disabled only while attached to the boat.");
            Assert.IsTrue(camera.enabled, "First-person camera look must remain enabled while rowing.");

            Vector3 start = boat.transform.position;
            PressAndRelease(keyboard.spaceKey);
            yield return new WaitForSeconds(boat.StrokeCooldown + 0.35f);
            Assert.Greater(Vector3.Distance(start, boat.transform.position), 0.2f, "SPACE must produce boat movement.");

            float timeout = Time.time + 20f;
            while (boat != null && boat.IsRowing && Time.time < timeout)
            {
                PressAndRelease(keyboard.spaceKey);
                yield return new WaitForSeconds(boat.StrokeCooldown + 0.05f);
            }

            Assert.IsFalse(boat.IsRowing, "Repeated valid strokes should reach the landing and exit rowing.");
            Assert.IsTrue(movement.enabled, "Land movement must be re-enabled after landing.");
            Assert.IsTrue(controller.enabled, "CharacterController must be re-enabled after landing.");
            Assert.GreaterOrEqual(ObjectiveSystem.Instance.Current, ObjectiveStep.ReachLandingZone);

            Vector3 landStart = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.Greater(Vector3.Distance(landStart, player.transform.position), 0.25f, "WASD land movement must move the player after landing.");

            float yawBefore = player.transform.eulerAngles.y;
            Move(mouse.position, new Vector2(240f, 0f), new Vector2(240f, 0f), queueEventOnly: true);
            yield return null;
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yawBefore, player.transform.eulerAngles.y)), 0.1f, "Mouse look must rotate the first-person body.");

            Vector3 sprintStart = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return new WaitForSeconds(1f);
            float sprintDistance = Vector3.Distance(sprintStart, player.transform.position);
            Assert.IsTrue(
                movement.IsSprinting || movement.CurrentSpeed > 2.25f || sprintDistance > 2.25f,
                $"Shift sprint must engage or raise land movement speed. sprintHeld={input.SprintHeld}, move={input.MoveInput}, currentSpeed={movement.CurrentSpeed:0.00}, distance={sprintDistance:0.00}, pos={player.transform.position}");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());

            float standingHeight = controller.height;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            yield return new WaitForSeconds(0.35f);
            Assert.Less(controller.height, standingHeight, "Ctrl crouch must lower the CharacterController height.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.2f);

            float jumpStartY = player.transform.position.y;
            PressAndRelease(keyboard.spaceKey);
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(player.transform.position.y, jumpStartY + 0.05f, "Space must trigger a land jump after landing.");
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            float timeout = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }
    }
}
