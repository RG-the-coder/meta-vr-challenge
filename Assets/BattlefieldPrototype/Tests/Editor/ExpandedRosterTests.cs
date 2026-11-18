using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed class ExactArmyPlannerTests
    {
        [TestCase(-4, 10)] [TestCase(19, 20)] [TestCase(100, 100)] [TestCase(103, 110)] [TestCase(999, 1000)] [TestCase(9000, 5000)]
        public void PowerLimitsAreTensAndBounded(int input, int expected) => Assert.That(BattleDeployment.NormalizeBudget(input), Is.EqualTo(expected));

        [Test]
        public void RandomArmiesExactlySpendEverySupportedLimitWithinTheUnitCap()
        {
            var definitions = new List<BattleUnitDefinition>();
            var random = UnityEngine.Random.state;
            try
            {
                foreach (UnitRole role in Enum.GetValues(typeof(UnitRole))) definitions.Add(BattleUnitDefinition.CreateDefault(role));
                UnityEngine.Random.InitState(4291);
                bool sawWisp = false;
                var compositions = new HashSet<string>();
                for (int budget = 10; budget <= 200; budget += 10)
                    for (int roll = 0; roll < 30; roll++)
                    {
                        Assert.That(BattleArmyPlanner.TryBuild(definitions, budget, 20, out var roster), Is.True);
                        int sum = 0;
                        var roles = new List<string>();
                        foreach (var definition in roster) { sum += definition.DeploymentCost; roles.Add(definition.Role.ToString()); sawWisp |= definition.Role == UnitRole.Wisp; }
                        Assert.That(sum, Is.EqualTo(budget));
                        Assert.That(roster.Count, Is.LessThanOrEqualTo(20));
                        compositions.Add(string.Join(",", roles));
                    }
                Assert.That(sawWisp, Is.True);
                Assert.That(compositions.Count, Is.GreaterThan(50));
                var wisp = definitions.Find(d => d.Role == UnitRole.Wisp);
                Assert.That(BattleArmyPlanner.TryBuild(new[] { wisp }, 7, 7, out var fillers), Is.True);
                Assert.That(fillers.Count, Is.EqualTo(7));
                Assert.That(BattleArmyPlanner.TryBuild(new[] { wisp }, 100, 20, out var impossible), Is.False);
                Assert.That(impossible, Is.Empty, "Failure must never return an under-budget army.");
            }
            finally
            {
                UnityEngine.Random.state = random;
                foreach (var definition in definitions) UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void OriginalCharactersKeepTheirMovementIncreaseAndEveryAssetMatchesRuntimeSpeed()
        {
            float[] previousSpeeds = { 3.2f, 3.9f, 3.5f, 4.5f, 2.6f, 3.1f, 3.5f, 4.8f,
                3.6f, 6.2f, 2.7f, 2.2f, 1.9f, 3.4f, 5.5f, 2.8f, 3, 5, 3.2f, 3.4f, 3, 3.5f, 4, 2.9f, 6.2f / 1.5f, 4.4f / 1.5f };
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>("Assets/BattlefieldPrototype/Definitions/" + role + ".asset");
                var defaults = BattleUnitDefinition.CreateDefault(role);
                try
                {
                    Assert.That(asset, Is.Not.Null, role.ToString());
                    if ((int)role < previousSpeeds.Length)
                        Assert.That(defaults.MoveSpeed, Is.EqualTo(previousSpeeds[(int)role] * 1.5f).Within(.001f), role.ToString());
                    Assert.That(asset.MoveSpeed, Is.EqualTo(defaults.MoveSpeed).Within(.001f), "Scene assets and fallback definitions must agree for " + role);
                }
                finally { UnityEngine.Object.DestroyImmediate(defaults); }
            }
        }

        [Test]
        public void AuthoredDefinitionsMatchDefaultsAndCoverZeroThroughFourPowers()
        {
            var counts = new HashSet<int>();
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>("Assets/BattlefieldPrototype/Definitions/" + role + ".asset");
                Assert.That(asset, Is.Not.Null, role.ToString());
                var defaults = BattleUnitDefinition.CreateDefault(role);
                try
                {
                    Assert.That(asset.MaxHealth, Is.EqualTo(defaults.MaxHealth), role + " health");
                    Assert.That(asset.BodyScale, Is.EqualTo(defaults.BodyScale), role + " scale");
                    counts.Add(asset.AbilityCount);
                }
                finally { UnityEngine.Object.DestroyImmediate(defaults); }
            }
            Assert.That(counts, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(BattleDeployment.GetCost(UnitRole.Wisp), Is.EqualTo(1));
        }
    }

    public sealed class ExpandedRosterFrameTests
    {
        readonly List<GameObject> disabledRoots = new List<GameObject>();
        GameObject root;
        BattleDirector director;
        BattleDeployment deployment;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            foreach (var previous in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (previous.activeSelf) { previous.SetActive(false); disabledRoots.Add(previous); }
            root = new GameObject("Expanded roster regression");
            var bootstrap = root.AddComponent<BattlefieldBootstrap>(); bootstrap.TowerDefenseMode = false;
            bootstrap.RandomizePowerBudget=false;
            bootstrap.RandomizeBattlefield=false;
            bootstrap.ArenaSize=new Vector2(36,44);
            bootstrap.ArmyPowerBudget = 103; // Runtime callers receive the same rounding as the Inspector.
            yield return null;
            director = root.GetComponent<BattleDirector>();
            deployment = root.GetComponent<BattleDeployment>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            yield return null;
            foreach (var previous in disabledRoots) if (previous != null) previous.SetActive(true);
            disabledRoots.Clear();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RealEnemyArmiesAlwaysSpendTheFullRoundedBudgetAndSpawnClear()
        {
            Assert.That(deployment.Budget, Is.EqualTo(110));
            for (int round = 0; round < 16; round++)
            {
                deployment.NewGame();
                Assert.That(deployment.EnemySpent, Is.EqualTo(110));
                Assert.That(director.AliveRed, Is.LessThanOrEqualTo(deployment.MaxUnits));
                var enemies = new List<BattleUnit>(director.Units);
                for (int i = 0; i < enemies.Count; i++)
                    for (int j = i + 1; j < enemies.Count; j++)
                        Assert.That(Vector3.Distance(enemies[i].transform.position, enemies[j].transform.position),
                            Is.GreaterThan(enemies[i].Definition.BodyRadius + enemies[j].Definition.BodyRadius + .1f));
                yield return null;
            }
            Assert.That(deployment.RandomizeBlue(), Is.True);
            Assert.That(deployment.Spent, Is.EqualTo(deployment.Budget));
        }

        [UnityTest]
        public IEnumerator SizesPossessionFourPowerInputAndHudAdaptToTheSelectedCharacter()
        {
            deployment.NewGame();
            Assert.That(deployment.TryPlace(UnitRole.Wisp, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(-8, 0, -12)), out var wisp), Is.True);
            Assert.That(deployment.TryPlace(UnitRole.Titan, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(0, 0, -12)), out var titan), Is.True);
            Assert.That(deployment.TryPlace(UnitRole.Paladin, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(8, 0, -12)), out var paladin), Is.True);
            Assert.That(deployment.Spent, Is.EqualTo(69));
            Assert.That(wisp.GetComponent<CharacterController>().height, Is.EqualTo(wisp.Definition.BodyHeight).Within(.001));
            Assert.That(titan.GetComponent<CharacterController>().height, Is.EqualTo(titan.Definition.BodyHeight).Within(.001));
            Assert.That(titan.MaxHealth, Is.GreaterThan(wisp.MaxHealth * 20));
            foreach (var unit in director.Units) unit.GetComponent<BattleUnitAI>().enabled = false;
            director.BeginBattle();
            var player = root.GetComponent<PossessionController>();
            player.Possess(paladin);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(player.CurrentUnit, Is.SameAs(paladin));
            Assert.That(Vector3.Distance(player.ViewCamera.transform.position, paladin.EyePosition), Is.LessThan(.05));
            Canvas hud = null;
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>()) if (canvas.name == "Battle HUD") hud = canvas;
            Assert.That(hud.transform.Find("Panel/Ability 3").gameObject.activeInHierarchy, Is.True);
            Assert.That(hud.transform.Find("Panel/Ability 3/Name").GetComponent<Text>().text, Does.Contain("Judgment"));
            foreach (var label in hud.transform.Find("Panel").GetComponentsInChildren<Text>())
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), label.name + " must fit the four-power HUD.");
            BattleFrameBehaviorTests.CaptureUiPreview(hud, "Paladin-HUD");

            // Delivered simulator keyboard events must reach slots 3 and 4 even
            // when batch mode's desktop application-focus flag is false.
            player.enabled = false;
            typeof(PossessionController).GetProperty(nameof(PossessionController.IsUsingXR)).SetValue(player, true);
            var previousKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
            var background = InputSystem.settings.backgroundBehavior;
            try
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J, Key.K));
                typeof(InputSystem).GetMethod("Update", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Dynamic });
                player.SendMessage("UpdatePlayerInput", SendMessageOptions.RequireReceiver);
                player.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                Assert.That(paladin.GetAbilityCooldownRemaining(2), Is.GreaterThan(0));
                Assert.That(paladin.GetAbilityCooldownRemaining(3), Is.GreaterThan(0));
                Assert.That(paladin.AbilityCooldownRemaining, Is.Zero);
            }
            finally
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = routing;
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.RemoveDevice(keyboard);
                previousKeyboard?.MakeCurrent();
                typeof(PossessionController).GetProperty(nameof(PossessionController.IsUsingXR)).SetValue(player, false);
            }
            player.enabled = true;
            player.Possess(wisp);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(player.CurrentUnit, Is.SameAs(wisp));
            Assert.That(Vector3.Distance(player.ViewCamera.transform.position, wisp.EyePosition), Is.LessThan(.05));
            Assert.That(hud.transform.Find("Panel/Ability 0").gameObject.activeInHierarchy, Is.False);
            Assert.That(hud.transform.Find("Panel/No Powers").gameObject.activeInHierarchy, Is.True);
            BattleFrameBehaviorTests.CaptureUiPreview(hud, "Wisp-HUD");
            Assert.That(wisp.TryAbility(0, wisp.EyePosition, Vector3.forward), Is.False);
        }
    }
}
