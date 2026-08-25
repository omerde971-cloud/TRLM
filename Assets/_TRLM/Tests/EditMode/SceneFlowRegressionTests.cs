using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.SceneManagement;
using TRLM.Flow;

namespace TRLM.Tests
{
    public class SceneFlowRegressionTests
    {
        [Test]
        public void ProductionBuildScenes_StartAtMainMenuAndFollowAuthoredRoute()
        {
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "Assets/_TRLM/Scenes/Production/00_MainMenu.unity",
                "Assets/_TRLM/Scenes/Production/20_Island_Blockout.unity",
            }, enabledScenes);
        }

        [Test]
        public void ProductionScripts_LoadScenesOnlyThroughSceneFlowGate()
        {
            string root = Path.GetFullPath("Assets/_TRLM/Scripts");
            string sceneFlowPath = Path.GetFullPath("Assets/_TRLM/Scripts/SceneFlow/SceneFlow.cs");

            string[] offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => Path.GetFullPath(path) != sceneFlowPath)
                .Where(path => File.ReadAllText(path).Contains("SceneManager.LoadScene"))
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            CollectionAssert.IsEmpty(offenders, "Production scripts must request scene loads through SceneFlow.");
        }

        [Test]
        public void SceneFlow_RejectsRetiredNeighborhoodOpening()
        {
            Assert.IsFalse(SceneFlow.RequestLoad(SceneFlow.RetiredNeighborhoodOpeningScene, "RegressionTest"));
            Assert.AreNotEqual(SceneFlow.RetiredNeighborhoodOpeningScene, SceneManager.GetActiveScene().name);
        }
    }
}
