using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed class FlareTests
    {
        TestBattleWorld world;
        [SetUp] public void SetUp() => world = new TestBattleWorld();
        [TearDown] public void TearDown() => world.Dispose();
        internal static bool Launch(BattleFlares flares, BattleTeam team, Vector3 position) =>
            (bool)typeof(BattleFlares).GetMethod("Launch", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flares, new object[] { team, position });
        BattleUnitAI Brain(BattleUnit unit)
        { var ai = unit.gameObject.AddComponent<BattleUnitAI>(); ai.Initialize(unit, world.Director); return ai; }

        [Test]
        public void EachSideHasOneChargeForTheMatchAndResetRestoresBoth()
        {
            var blue = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var red = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.back * 90);
            var flares = world.Director.Flares;
            Assert.That(Launch(flares, BattleTeam.Blue, blue.transform.position), Is.False);
            Assert.That(flares.HasCharge(BattleTeam.Blue), Is.True);
            world.Director.BeginBattle();
            Assert.That(Launch(flares, BattleTeam.Blue, blue.transform.position), Is.True);
            Assert.That(Launch(flares, BattleTeam.Blue, blue.transform.position + Vector3.right * 10), Is.False);
            Assert.That(flares.HasCharge(BattleTeam.Red), Is.True);
            Assert.That(Launch(flares, BattleTeam.Red, red.transform.position), Is.True);
            Assert.That(Launch(flares, BattleTeam.Red, red.transform.position), Is.False);
            Assert.That(flares.TryGetLatest(out var latest), Is.True); Assert.That(latest.Team, Is.EqualTo(BattleTeam.Red));
            world.Director.RestartBattle();
            Assert.That(flares.HasCharge(BattleTeam.Blue), Is.True); Assert.That(flares.HasCharge(BattleTeam.Red), Is.True);
            Assert.That(flares.TryGetLatest(out _), Is.False);
        }

        [Test]
        public void IdleUnitsOnBothTeamsInvestigateTheSignalWithoutReceivingEnemyKnowledge()
        {
            var orders = world.Root.AddComponent<BattleOrders>(); orders.Initialize(world.Director);
            var blue = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var launcher = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.right * 30);
            var red = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.back * 80);
            var blueAi = Brain(blue); var redAi = Brain(red);
            world.Director.BeginBattle(); orders.Issue(BattleOrder.Hold); Physics.SyncTransforms();
            Assert.That(Launch(world.Director.Flares, BattleTeam.Blue, launcher.transform.position), Is.True);
            SightAndTacticsTests.Call(blueAi, "Update"); SightAndTacticsTests.Call(redAi, "Update");
            Assert.That(blueAi.State, Is.EqualTo(BattleTacticalState.Rallying));
            Assert.That(redAi.State, Is.EqualTo(BattleTacticalState.Rallying));
            Assert.That(blueAi.HasEnemyMemory, Is.False); Assert.That(redAi.HasEnemyMemory, Is.False);
            Assert.That(blueAi.FollowedUnit, Is.Null);
            Assert.That(world.Director.Tactics.Heat(blue, launcher.transform.position), Is.Zero);
            Assert.That(world.Director.Tactics.Heat(red, launcher.transform.position), Is.Zero);
            Vector3 destination = (Vector3)typeof(BattleUnitAI).GetField("lastDestination", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(blueAi);
            Assert.That(destination.x - blue.transform.position.x, Is.GreaterThan(20), "The signal temporarily overrides an idle Hold order.");
            var seen = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 12);
            blue.Face(Vector3.forward); Physics.SyncTransforms(); SightAndTacticsTests.Set(blueAi, "targetRefresh", 0f);
            SightAndTacticsTests.Call(blueAi, "Update");
            Assert.That(blueAi.State, Is.Not.EqualTo(BattleTacticalState.Rallying));
            Assert.That(blueAi.HasEnemyMemory, Is.True); Assert.That(blueAi.LastSeenPosition, Is.EqualTo(seen.transform.position));
        }

        [Test]
        public void ArrivingAtASignalAllowsSearchingAndANewerSignalCanRallyAgain()
        {
            var blue = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var red = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.back * 90);
            var ai = Brain(blue); world.Director.BeginBattle(); Physics.SyncTransforms();
            Vector3 first = blue.transform.position + Vector3.right * 25;
            Launch(world.Director.Flares, BattleTeam.Blue, first); SightAndTacticsTests.Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Rallying));
            blue.transform.position = first; Physics.SyncTransforms();
            SightAndTacticsTests.Call(ai, "Update"); SightAndTacticsTests.Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Searching));
            Launch(world.Director.Flares, BattleTeam.Red, red.transform.position); SightAndTacticsTests.Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Rallying));
        }

        [Test]
        public void EnemyUsesOnlyItsOwnFlareAfterLosingContactAndKeepsBlueChargeUntouched()
        {
            world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var red = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.back * 90);
            var flares = world.Director.Flares; world.Director.BeginBattle(); Physics.SyncTransforms();
            SightAndTacticsTests.Call(flares, "Update"); Assert.That(flares.HasCharge(BattleTeam.Red), Is.True);
            SightAndTacticsTests.Set(world.Director, "<BattleDuration>k__BackingField", 14f);
            SightAndTacticsTests.Call(flares, "Update");
            Assert.That(flares.HasCharge(BattleTeam.Red), Is.False);
            Assert.That(flares.HasCharge(BattleTeam.Blue), Is.True);
            Assert.That(flares.TryGetFlare(BattleTeam.Red, out var flare), Is.True);
            Assert.That(flare.Position, Is.EqualTo(red.transform.position));
        }

        [Test]
        public void EnemyKeepsItsFlareWhileItCanSeeAnOpponent()
        {
            world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var red = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 20); red.Face(Vector3.back);
            var ai = Brain(red); world.Director.BeginBattle(); Physics.SyncTransforms(); SightAndTacticsTests.Call(ai, "Update");
            var flares = world.Director.Flares;
            SightAndTacticsTests.Set(world.Director, "<BattleDuration>k__BackingField", 20f); SightAndTacticsTests.Call(flares, "Update");
            Assert.That(flares.HasCharge(BattleTeam.Red), Is.True);
            Assert.That(flares.TryGetLatest(out _), Is.False);
        }

        [Test]
        public void EnemyDoesNotSpendAFlareAtAnAlreadyBroadcastLocation()
        {
            world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var red = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.back * 90);
            world.Director.BeginBattle(); Physics.SyncTransforms();
            var flares = world.Director.Flares; Launch(flares, BattleTeam.Blue, red.transform.position);
            SightAndTacticsTests.Set(world.Director, "<BattleDuration>k__BackingField", 25f); SightAndTacticsTests.Call(flares, "Update");
            Assert.That(flares.HasCharge(BattleTeam.Red), Is.True);
        }

        [Test]
        public void ASignalAcrossAWallDoesNotCountAsArrivedJustBecauseItIsNearby()
        {
            var blue = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.back * 90);
            var ai = Brain(blue); world.Director.BeginBattle();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(world.Root.transform, false);
            wall.transform.localPosition = new Vector3(0, 3, 2.5f); wall.transform.localScale = new Vector3(8, 6, .5f);
            Physics.SyncTransforms(); Launch(world.Director.Flares, BattleTeam.Blue, blue.transform.position + Vector3.forward * 5);
            SightAndTacticsTests.Call(ai, "Update"); SightAndTacticsTests.Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Rallying));
            wall.SetActive(false); Physics.SyncTransforms();
            SightAndTacticsTests.Call(ai, "Update"); SightAndTacticsTests.Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Searching));
        }
    }

    public sealed class FlareAndVisibilityFrameTests
    {
        [UnityTest]
        public IEnumerator PlayerChargePersistsAcrossPossessionAndSpectatorCannotSpendIt()
        {
            yield return new EnterPlayMode();
            using (var world = new TestBattleWorld())
            {
                var blue = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
                var next = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.right * 5);
                world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.back * 90);
                var player = world.Root.AddComponent<PossessionController>(); player.Initialize(world.Director);
                Assert.That(player.TryUseFlare(), Is.False);
                world.Director.BeginBattle(); player.Possess(blue); yield return new WaitForSecondsRealtime(.2f);
                Assert.That(player.CurrentUnit, Is.SameAs(blue));
                Assert.That(player.TryUseFlare(), Is.True);
                Assert.That(world.Director.Flares.TryGetFlare(BattleTeam.Blue, out var flare), Is.True);
                Assert.That(flare.Position, Is.EqualTo(blue.transform.position));
                player.Possess(next); yield return new WaitForSecondsRealtime(.15f);
                Assert.That(player.TryUseFlare(), Is.False);
                player.ReleaseControl(); yield return new WaitForSecondsRealtime(.15f);
                Assert.That(player.TryUseFlare(), Is.False);
                Assert.That(world.Director.Flares.HasCharge(BattleTeam.Red), Is.True);
                world.Director.RestartBattle();
                Assert.That(world.Director.Flares.HasCharge(BattleTeam.Blue), Is.True);
            }
            yield return null; yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ActualAllyAndEnemyModelsRenderIncludingAHeadAboveCover()
        {
            yield return new EnterPlayMode();
            var previousRoots = new List<GameObject>();
            foreach (var item in SceneManager.GetActiveScene().GetRootGameObjects())
                if (item.activeSelf) { previousRoots.Add(item); item.SetActive(false); }
            var random = UnityEngine.Random.state; UnityEngine.Random.InitState(3177);
            var root = new GameObject("Visible teams and signal regression");
            try
            {
                var bootstrap = root.AddComponent<BattlefieldBootstrap>(); bootstrap.TowerDefenseMode = false;
                bootstrap.RandomizePowerBudget = bootstrap.RandomizeBattlefield = false; bootstrap.ArmyPowerBudget = 100;
                yield return null;
                var director = root.GetComponent<BattleDirector>(); var deployment = root.GetComponent<BattleDeployment>(); deployment.NewGame();
                Assert.That(deployment.TryPlace(UnitRole.Ranger, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(20, 0, -75)), out var actor), Is.True);
                Assert.That(deployment.TryPlace(UnitRole.Vanguard, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(18, 0, -63)), out var ally), Is.True);
                BattleUnit enemy = null;
                foreach (var unit in director.Units)
                    if (unit.Team == BattleTeam.Red && (enemy == null || unit.Definition.BodyHeight > enemy.Definition.BodyHeight)) enemy = unit;
                Assert.That(enemy, Is.Not.Null);
                foreach (var unit in new List<BattleUnit>(director.Units))
                {
                    unit.GetComponent<BattleUnitAI>().enabled = false;
                    unit.GetComponent<CharacterController>().enabled = false;
                    if (unit != actor && unit != ally && unit != enemy) { director.UnregisterUnit(unit); unit.gameObject.SetActive(false); }
                }
                ally.transform.position = actor.transform.position + new Vector3(-2, 0, 12);
                enemy.transform.position = actor.transform.position + new Vector3(2, 0, 12);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform, false);
                wall.transform.position = actor.transform.position + new Vector3(-1.2f, .8f, 7); wall.transform.localScale = new Vector3(1.6f, 1.6f, .35f);
                var player = root.GetComponent<PossessionController>(); director.BeginBattle(); player.Possess(actor);
                yield return new WaitForSecondsRealtime(.25f); Physics.SyncTransforms(); player.SendMessage("LateUpdate");
                var allyRenderers = ally.VisualRoot.GetComponentsInChildren<Renderer>();
                var enemyRenderers = enemy.VisualRoot.GetComponentsInChildren<Renderer>();
                foreach (var renderer in allyRenderers) Assert.That(renderer.enabled, Is.True, "Partial cover cannot disable the entire ally.");
                foreach (var renderer in enemyRenderers) Assert.That(renderer.enabled, Is.True, "A nearby enemy must remain rendered.");
                Assert.That(player.CanSeeUnit(ally), Is.False, "The ally's chest is behind cover even though its head is visible.");
                if (!player.CanSeeUnit(enemy))
                {
                    Vector3 eye = player.ViewCamera.transform.position, delta = enemy.AimPosition - eye;
                    string hits = string.Join(", ", Array.ConvertAll(Physics.RaycastAll(eye, delta.normalized, delta.magnitude), hit => hit.collider.name + ":" + hit.distance));
                    string overlaps = string.Join(", ", Array.ConvertAll(Physics.OverlapSphere(eye, .025f), hit => hit.name));
                    Render(player.ViewCamera, "Visibility-Diagnostic");
                    Assert.Fail("eye=" + eye + " origin=" + actor.transform.position + " forward=" + player.ViewCamera.transform.forward + " enemy=" + enemy.AimPosition + " hits=" + hits + " overlaps=" + overlaps);
                }
                var canvas = root.GetComponent<BattleHud>().TacticalHud.GetComponent<Canvas>(); canvas.enabled = false;
                Color32[] both = Render(player.ViewCamera, "Visible-Teams-Partial-Cover");
                foreach (var renderer in allyRenderers) renderer.enabled = false;
                Color32[] noAlly = Render(player.ViewCamera, null);
                foreach (var renderer in allyRenderers) renderer.enabled = true;
                foreach (var renderer in enemyRenderers) renderer.enabled = false;
                Color32[] noEnemy = Render(player.ViewCamera, null);
                foreach (var renderer in enemyRenderers) renderer.enabled = true;
                Assert.That(HeadPixelDifference(player.ViewCamera, ally, both, noAlly), Is.GreaterThan(4), "The ally's exposed head must produce real visible pixels.");
                Assert.That(HeadPixelDifference(player.ViewCamera, enemy, both, noEnemy), Is.GreaterThan(10), "The enemy must produce real visible pixels.");
                canvas.enabled = true;
                var button = canvas.transform.Find("Live tactical map/Flare").GetComponent<Button>();
                Assert.That(button.interactable, Is.True); button.onClick.Invoke();
                Assert.That(director.Flares.HasCharge(BattleTeam.Blue), Is.False);
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(button.interactable, Is.False);
                BattleFrameBehaviorTests.CaptureUiPreview(canvas, "Visible-Teams-And-Flare");
            }
            finally
            {
                root.SetActive(false); UnityEngine.Object.Destroy(root); UnityEngine.Random.state = random;
                foreach (var item in previousRoots) if (item != null) item.SetActive(true);
            }
            yield return null; yield return new ExitPlayMode();
        }

        static int HeadPixelDifference(Camera camera, BattleUnit unit, Color32[] before, Color32[] after)
        {
            Vector3 bottom = camera.WorldToScreenPoint(unit.transform.position + Vector3.up * unit.Definition.BodyHeight * .7f);
            Vector3 top = camera.WorldToScreenPoint(unit.transform.position + Vector3.up * unit.Definition.BodyHeight * 1.1f);
            int width = Screen.width, height = Screen.height, count = 0;
            int x0 = Mathf.Clamp((int)top.x - 22, 0, width - 1), x1 = Mathf.Clamp((int)top.x + 22, 0, width - 1);
            int y0 = Mathf.Clamp((int)bottom.y, 0, height - 1), y1 = Mathf.Clamp((int)top.y, 0, height - 1);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                int i = y * width + x;
                if (Math.Abs(before[i].r - after[i].r) + Math.Abs(before[i].g - after[i].g) + Math.Abs(before[i].b - after[i].b) > 25) count++;
            }
            return count;
        }

        static Color32[] Render(Camera camera, string label)
        {
            var buffer = new RenderTexture(Screen.width, Screen.height, 24);
            var pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            var active = RenderTexture.active; var target = camera.targetTexture;
            try
            {
                camera.targetTexture = buffer;
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = buffer });
                else camera.Render();
                RenderTexture.active = buffer; pixels.ReadPixels(new Rect(0, 0, buffer.width, buffer.height), 0, 0); pixels.Apply();
                string directory = Environment.GetEnvironmentVariable("BATTLEFIELD_PREVIEW_DIR");
                if (!string.IsNullOrEmpty(directory) && label != null)
                { System.IO.Directory.CreateDirectory(directory); System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, label + ".png"), pixels.EncodeToPNG()); }
                return pixels.GetPixels32();
            }
            finally
            { camera.targetTexture = target; RenderTexture.active = active; UnityEngine.Object.Destroy(buffer); UnityEngine.Object.Destroy(pixels); }
        }
    }
}
