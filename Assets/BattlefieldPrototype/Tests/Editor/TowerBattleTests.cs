using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        GameObject root;
        BattlefieldBootstrap terrain;
        TowerBattle battle;
        BattleDirector director;
        PossessionController player;
        TowerBattleHud hud;
        readonly List<GameObject> hidden = new List<GameObject>();
        readonly Dictionary<string, string> savedUiPreferences = new Dictionary<string, string>();
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            foreach (string key in new[] { TowerBattleHud.FavouritesKey, TowerBattleHud.HintsKey, TowerBattleHud.FocusKey, PossessionController.GesturesKey, PossessionController.HapticsKey })
            {
                savedUiPreferences[key] = PlayerPrefs.HasKey(key) ? (key == TowerBattleHud.FavouritesKey ? PlayerPrefs.GetString(key) : PlayerPrefs.GetInt(key).ToString()) : null;
                PlayerPrefs.DeleteKey(key);
            }
            // Existing advanced-feature regressions explicitly opt into More Options.
            // Basics tests below create a fresh bootstrap with the production default.
            PlayerPrefs.SetInt(TowerBattleHud.FocusKey, 0);
            foreach (var obj in SceneManager.GetActiveScene().GetRootGameObjects())
                if (obj.activeSelf) { hidden.Add(obj); obj.SetActive(false); }
            root = new GameObject("Tower warfare regression");
            terrain = root.AddComponent<BattlefieldBootstrap>(); terrain.RandomizeBattlefield = false;
            terrain.PrimitiveMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/BattlefieldPrototype/Materials/Primitive.mat");
            yield return null;
            battle = root.GetComponent<TowerBattle>(); director = root.GetComponent<BattleDirector>();
            player = root.GetComponent<PossessionController>(); hud = root.GetComponent<TowerBattleHud>();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            yield return null;
            foreach (var obj in hidden) if (obj != null) obj.SetActive(true);
            hidden.Clear();
            foreach (var preference in savedUiPreferences)
                if (preference.Value == null) PlayerPrefs.DeleteKey(preference.Key);
                else if (preference.Key == TowerBattleHud.FavouritesKey) PlayerPrefs.SetString(preference.Key, preference.Value);
                else PlayerPrefs.SetInt(preference.Key, int.Parse(preference.Value));
            savedUiPreferences.Clear(); PlayerPrefs.Save();
            yield return new ExitPlayMode();
        }
        static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        static IEnumerator Simulate(float seconds)
        {
            float end = Time.time + seconds;
            do { yield return null; } while (Time.time < end);
            yield return null; // Let the motor and possession LateUpdate finish on the final frame.
        }
        void FreezeEnemy()
        {
            foreach (string field in new[] { "enemyRecruitAt", "enemyBuildAt", "enemyTravelAt" }) typeof(TowerBattle).GetField(field, Private).SetValue(battle, float.MaxValue);
        }
        void StartSiege()
        {
            director.NewGame(); SummonFixtureSquads(); FreezeEnemy();
        }
        void SummonFixtureSquads()
        {
            // Existing subsystem tests begin after both squads were purchased.
            // Restore their treasury to keep economy unrelated to those fixtures.
            for (int slot = 0; slot < TowerBattle.SquadSize; slot++) Assert.That(battle.TrySummonSlot(slot, out _), Is.True, battle.Message);
            var funds = (float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle);
            funds[0] = funds[1] = TowerBattle.StartingGold;
        }
        Vector3 FreePoint(BattleTower tower, UnitRole role = UnitRole.Vanguard)
        {
            for (float z = -22; z <= 22; z += 3) for (float x = -22; x <= 22; x += 3)
                if (battle.TryPlacement(tower, battle.Definition(role), tower.transform.position + new Vector3(x, 0, z), out var point, out _)) return point;
            Assert.Fail("A standing tower must have clear deployment ground."); return default;
        }
        BattleUnit Spawn(BattleTeam team, UnitRole role, Vector3 point, BattleTower tower = null)
        {
            var unit = (BattleUnit)Invoke(terrain, "CreateUnit", battle.Definition(role), team, point, 999);
            typeof(BattleUnit).GetProperty("SpawnTower").SetValue(unit, tower ?? (team == BattleTeam.Blue ? battle.BoundTower : battle.EnemyBoundTower));
            unit.GetComponent<BattleUnitAI>().enabled = false;
            unit.FlightAltitude = point.y; return unit;
        }
        static void Put(BattleUnit unit, Vector3 point)
        {
            var motor = unit.GetComponent<CharacterController>(); motor.enabled = false; unit.transform.position = point; motor.enabled = true;
            unit.FlightAltitude = point.y; Physics.SyncTransforms();
        }
        void Click(Transform control)
        {
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)control;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            Invoke(hud, "Dispatch", screen, true); Invoke(hud, "Dispatch", screen, false);
        }

        [UnityTest] public IEnumerator HomeStartsTheLiveSiegeThroughTheActualPointerPath()
        {
            Assert.That(terrain.TowerDefenseMode, Is.True); Assert.That(director.Phase, Is.EqualTo(BattlePhase.Home));
            Assert.That(battle.Definitions.Count, Is.EqualTo(29));
            for (int i = 0; i < 24; i++) Assert.That((int)battle.Definitions[i].Role, Is.EqualTo(i));
            CaptureHud("Tower-Home");
            var begin = hud.Canvas.transform.Find("Home and outcome veil/Skyhold briefing/Begin siege"); Assert.That(begin, Is.Not.Null);
            Click(begin); yield return null;
            Assert.That(hud.BlocksGameplayInput, Is.False);
            Assert.That(director.IsBattleRunning, Is.True); Assert.That(battle.Towers.Count, Is.EqualTo(7));
            Assert.That(director.AliveBlue, Is.EqualTo(5)); Assert.That(director.AliveRed, Is.EqualTo(5));
            Assert.That(player.CurrentUnit, Is.Null); Assert.That(battle.IsMounted, Is.True);
            Assert.That(battle.BlueGold, Is.GreaterThanOrEqualTo(TowerBattle.StartingGold)); Assert.That(battle.Income(BattleTeam.Blue), Is.EqualTo(.72f).Within(.001f));
            hud.Refresh(); CaptureHud("Tower-Summit");
        }

        [UnityTest] public IEnumerator GameplayHudFitsTheActualUnityGameViewAfterResizeAndZoom()
        {
            StartSiege(); battle.ToggleMount(); hud.Refresh();
            var gameViewType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var gameView = UnityEditor.EditorWindow.GetWindow(gameViewType);
            Rect previousPosition = gameView.position;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var zoom = gameViewType.GetField("m_ZoomArea", flags).GetValue(gameView);
            var transform = zoom.GetType().GetMethod("SetTransform", flags);
            var editorAssembly = System.Reflection.Assembly.Load("BattlefieldPrototype.Editor");
            var fit = editorAssembly.GetType("BattlefieldPrototype.Editor.BattlefieldGameView").GetMethod("FitGameView");
            try
            {
                foreach (var size in new[] { new Vector2(548, 289), new Vector2(800, 650), new Vector2(450, 450), new Vector2(1833, 750) })
                {
                    gameView.position = new Rect(30, 30, size.x, size.y); gameView.Repaint();
                    yield return null; yield return null;
                    // Exercise the editor zoom that offscreen camera captures omit.
                    transform.Invoke(zoom, new object[] { Vector2.zero, Vector2.one * 2 });
                    fit.Invoke(null, null);
                    yield return null;
                    // An already-running editor can retain the old unit-scale
                    // transform across a script reload without a scaler change.
                    hud.Canvas.transform.localScale = Vector3.one;
                    yield return null;
                    hud.SendMessage("Layout"); hud.Refresh();
                    hud.Canvas.GetComponent<CanvasScaler>().SendMessage("Handle");
                    Canvas.ForceUpdateCanvases();
                    Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                    Debug.Log($"LIVE_GAME_VIEW {Screen.width}x{Screen.height} HUD scale {hud.Canvas.scaleFactor}");
                    Debug.Log($"LIVE_CANVAS rect={((RectTransform)hud.Canvas.transform).rect} pixelRect={hud.Canvas.pixelRect} renderingDisplay={hud.Canvas.renderingDisplaySize} position={hud.Canvas.transform.position} lossyScale={hud.Canvas.transform.lossyScale} liveRect={((RectTransform)hud.Canvas.transform.Find("Live command HUD")).rect}");
                    Debug.Log($"LIVE_ZOOM view={gameViewType.GetProperty("viewInWindow", flags).GetValue(gameView)} target={gameViewType.GetProperty("targetInView", flags).GetValue(gameView)}");
                    foreach (string panel in new[] { "Command advice", "Character dossier", "Live recruitment", "Height-based minimap", "Tower network", "HUD tools", "Character browser" })
                    {
                        var rect = (RectTransform)hud.Canvas.transform.Find("Live command HUD/" + panel);
                        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                        foreach (var corner in corners)
                        {
                            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                            Assert.That(screen.x, Is.InRange(-1f, Screen.width + 1f), panel + " extends beyond the actual Game view width.");
                            Assert.That(screen.y, Is.InRange(-1f, Screen.height + 1f), panel + " extends beyond the actual Game view height.");
                        }
                    }
                    var view = (Rect)gameViewType.GetProperty("viewInWindow", flags).GetValue(gameView);
                    var targetRect = (Rect)gameViewType.GetProperty("targetInView", flags).GetValue(gameView);
                    Assert.That(targetRect.width, Is.LessThanOrEqualTo(view.width + 2), "Game view zoom must show the entire rendered width.");
                    Assert.That(targetRect.height, Is.LessThanOrEqualTo(view.height + 2), "Game view zoom must show the entire rendered height.");
                }
            }
            finally { gameView.position = previousPosition; fit.Invoke(null, null); }
        }

        [UnityTest] public IEnumerator RecruitmentIsLivePaidRadiusLimitedAndRequiresDismount()
        {
            StartSiege(); var point = FreePoint(battle.BoundTower);
            Assert.That(battle.TryDeploy(UnitRole.Vanguard, point, out _), Is.False);
            battle.ToggleMount(); Assert.That(battle.CanDeploy, Is.True);
            float gold = battle.BlueGold;
            Assert.That(battle.TryDeploy(UnitRole.Vanguard, new Vector3(10000, 0, 10000), out _), Is.False);
            Assert.That(battle.TryDeploy(UnitRole.Vanguard, new Vector3(float.NaN, 0, 0), out _), Is.False);
            Assert.That(battle.TryDeploy(UnitRole.Vanguard, battle.BoundTower.transform.position, out _), Is.False);
            Assert.That(battle.BlueGold, Is.EqualTo(gold));
            Assert.That(battle.TryDeploy(UnitRole.Vanguard, point, out var unit), Is.True);
            unit.GetComponent<BattleUnitAI>().enabled = false;
            Assert.That(battle.BlueGold, Is.EqualTo(gold - 10)); Assert.That(unit.SpawnTower, Is.SameAs(battle.BoundTower));
            Assert.That(battle.TryDeploy(UnitRole.Titan, point, out _), Is.False, "Bodies may not overlap and failed placements may not spend.");
            float beforeTick = battle.BlueGold; yield return null;
            Assert.That(battle.BlueGold, Is.GreaterThan(beforeTick));
            unit.TakeDamage(10000, null); Assert.That(director.IsBattleRunning, Is.True, "Army elimination is no longer the loss condition.");
            Assert.That(battle.TrySpend(BattleTeam.Blue, -1), Is.False);
            hud.Refresh(); CaptureHud("Tower-Command");
        }

        [UnityTest] public IEnumerator DamageShrinksHeightIncomeReachAndCameraWithoutPrematureDefeat()
        {
            StartSiege(); var tower = battle.BoundTower;
            float income = battle.Income(BattleTeam.Blue), reach = tower.DeploymentRadius, scout = tower.ScoutRadius;
            Assert.That(tower.TakeDamage(BattleTower.HealthPerMetre * 3, BattleTeam.Blue), Is.False);
            Assert.That(tower.TakeDamage(float.NaN, BattleTeam.Red), Is.False);
            Assert.That(tower.TakeDamage(BattleTower.HealthPerMetre * 3, BattleTeam.Red), Is.True);
            yield return null;
            Assert.That(tower.Height, Is.EqualTo(9).Within(.001)); Assert.That(tower.DeploymentRadius, Is.LessThan(reach));
            Assert.That(tower.ScoutRadius, Is.LessThan(scout)); Assert.That(battle.Income(BattleTeam.Blue), Is.LessThan(income));
            Assert.That(player.ViewCamera.transform.position.y, Is.EqualTo(tower.Summit.y).Within(.25));
            tower.TakeDamage(100000, BattleTeam.Red); yield return null;
            Assert.That(tower.IsBroken, Is.True); Assert.That(battle.CanDeploy, Is.False);
            Assert.That(tower.Income, Is.Zero); Assert.That(director.IsBattleRunning, Is.True, "Destruction is distinct from capture.");
            Assert.That(player.ViewCamera.transform.position.y, Is.GreaterThanOrEqualTo(1.6f));
        }

        [UnityTest] public IEnumerator GroundCaptureRequiresSecureFoundationAndGoldThenEndsTetheredMatch()
        {
            StartSiege(); var tower = battle.BoundTower;
            float redGold = battle.RedGold;
            Assert.That(tower.TryBuild(BattleTeam.Red), Is.False, "An intact tower cannot be bought away.");
            tower.TakeDamage(100000, BattleTeam.Red);
            Assert.That(tower.TryBuild(BattleTeam.Red), Is.False, "Troops must reach the foundation.");
            var attacker = Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.forward * 7);
            var defender = Spawn(BattleTeam.Blue, UnitRole.Vanguard, tower.transform.position + Vector3.back * 7);
            Assert.That(tower.TryBuild(BattleTeam.Red), Is.False, "Contested foundations cannot be rebuilt.");
            defender.TakeDamage(10000, null);
            Assert.That(tower.TryBuild(BattleTeam.Red), Is.True);
            Assert.That(tower.Height, Is.EqualTo(3)); Assert.That(tower.Owner, Is.EqualTo(BattleTeam.Red));
            Assert.That(battle.RedGold, Is.EqualTo(redGold - 35)); Assert.That(director.Phase, Is.EqualTo(BattlePhase.Defeat));
            yield return null; hud.Refresh(); CaptureHud("Tower-Defeat");
        }

        [UnityTest] public IEnumerator TeleportChangesTheAtRiskTowerAndUpgradesSpendTheCorrectWallet()
        {
            StartSiege(); var former = battle.BoundTower; var outpost = battle.Towers[2];
            Assert.That(battle.Teleport(battle.EnemyBoundTower), Is.False);
            Assert.That(battle.Teleport(battle.Towers[4]), Is.False);
            Assert.That(battle.Teleport(outpost), Is.True); Assert.That(battle.BoundTower, Is.SameAs(outpost));
            float gold = battle.BlueGold, red = battle.RedGold; int cost = former.BuildCost;
            Assert.That(former.TryBuild(BattleTeam.Blue), Is.True);
            Assert.That(former.Height, Is.EqualTo(15)); Assert.That(battle.BlueGold, Is.EqualTo(gold - cost)); Assert.That(battle.RedGold, Is.EqualTo(red));
            former.TakeDamage(100000, BattleTeam.Red);
            Spawn(BattleTeam.Red, UnitRole.Vanguard, former.transform.position + Vector3.forward * 7);
            Assert.That(former.TryBuild(BattleTeam.Red), Is.True); Assert.That(director.IsBattleRunning, Is.True);
            Assert.That(battle.BoundTower, Is.SameAs(outpost)); yield return null;
        }

        [UnityTest] public IEnumerator PossessionBindsToSpawnTowerAndDeathReturnsToItsSummit()
        {
            StartSiege(); battle.ToggleMount();
            Assert.That(battle.TryDeploy(UnitRole.Ranger, FreePoint(battle.BoundTower, UnitRole.Ranger), out var unit), Is.True);
            unit.GetComponent<BattleUnitAI>().enabled = false; var origin = unit.SpawnTower;
            battle.ToggleMount();
            Assert.That(battle.Teleport(battle.Towers[2]), Is.True);
            Assert.That(player.Possess(unit), Is.True); yield return Simulate(.15f);
            Assert.That(player.CurrentUnit, Is.SameAs(unit)); Assert.That(battle.BoundTower, Is.SameAs(origin)); Assert.That(battle.CanDeploy, Is.False);
            Assert.That(battle.TryDeploy(UnitRole.Wisp, FreePoint(origin, UnitRole.Wisp), out _), Is.False);
            unit.TakeDamage(100000, null); yield return null;
            Assert.That(player.CurrentUnit, Is.Null); Assert.That(battle.IsMounted, Is.True); Assert.That(battle.BoundTower, Is.SameAs(origin));
            Assert.That(director.IsBattleRunning, Is.True);
        }

        [UnityTest] public IEnumerator FlyingRaidCapturesIntactSummitAfterEightSecondsAndPaysGold()
        {
            StartSiege(); var enemy = battle.EnemyBoundTower; float height = enemy.Height, gold = battle.BlueGold;
            var flyer = Spawn(BattleTeam.Blue, UnitRole.Stormwing, enemy.transform.position + new Vector3(5, height + 1, 0));
            Invoke(enemy, "TickCapture", 4f); Assert.That(enemy.CaptureProgress, Is.EqualTo(.5f).Within(.001));
            Assert.That(enemy.Owner, Is.EqualTo(BattleTeam.Red));
            Invoke(enemy, "TickCapture", 4.1f);
            Assert.That(enemy.Owner, Is.EqualTo(BattleTeam.Blue)); Assert.That(enemy.Height, Is.EqualTo(height));
            Assert.That(battle.BlueGold, Is.EqualTo(gold - 35)); Assert.That(director.Phase, Is.EqualTo(BattlePhase.Victory));
            yield return null; hud.Refresh(); CaptureHud("Tower-Victory");
        }

        [UnityTest] public IEnumerator RaidsResetWhenDamagedLeavingOrContestedAndWaitForCaptureMoney()
        {
            StartSiege(); var enemy = battle.Towers[3];
            var flyer = Spawn(BattleTeam.Blue, UnitRole.Stormwing, enemy.transform.position + new Vector3(5, enemy.Height + 1, 0));
            Invoke(enemy, "TickCapture", 4f); flyer.TakeDamage(1, null); Invoke(enemy, "TickCapture", .1f);
            Assert.That(enemy.CaptureProgress, Is.Zero);
            yield return Simulate(.55f); Invoke(enemy, "TickCapture", 2f);
            Assert.That(enemy.CaptureProgress, Is.GreaterThan(0));
            var guard = Spawn(BattleTeam.Red, UnitRole.Skywing, enemy.transform.position + new Vector3(-5, enemy.Height + 1, 0));
            Invoke(enemy, "TickCapture", 8f); Assert.That(enemy.CaptureProgress, Is.Zero);
            Put(guard, enemy.transform.position + Vector3.right * 40);
            Put(flyer, enemy.transform.position + Vector3.right * 40); Invoke(enemy, "TickCapture", 1f); Assert.That(enemy.CaptureProgress, Is.Zero);
            Put(flyer, enemy.transform.position + new Vector3(5, enemy.Height + 1, 0));
            battle.TrySpend(BattleTeam.Blue, Mathf.FloorToInt(battle.BlueGold));
            Invoke(enemy, "TickCapture", 9f); Assert.That(enemy.Owner, Is.EqualTo(BattleTeam.Red), "Raid cannot capture on credit.");
            Assert.That(director.IsBattleRunning, Is.True);
        }

        [UnityTest] public IEnumerator ActualMeleeProjectilesAndSummitArrowsDamageTowersAndRespectCover()
        {
            StartSiege(); var target = battle.Towers[3];
            var melee = Spawn(BattleTeam.Blue, UnitRole.Vanguard, target.transform.position + Vector3.back * 4.5f);
            Physics.SyncTransforms(); float initial = target.Height;
            Assert.That(melee.TryAttack(melee.EyePosition, target.AttackPoint(melee.EyePosition) - melee.EyePosition), Is.True);
            Assert.That(target.Height, Is.LessThan(initial));
            var ranger = Spawn(BattleTeam.Blue, UnitRole.Ranger, target.transform.position + Vector3.right * 10);
            initial = target.Height; ranger.TryAttack(ranger.EyePosition, target.AttackPoint(ranger.EyePosition) - ranger.EyePosition);
            yield return Simulate(.5f); Assert.That(target.Height, Is.LessThan(initial));
            initial = target.Height;
            Vector3 origin = target.transform.position + new Vector3(12, 18, 0), aim = target.AttackPoint(origin) - origin;
            BattleProjectile.SpawnForTeam(director, BattleTeam.Blue, origin, aim, 55, 120, 60, Color.yellow);
            yield return Simulate(.4f); Assert.That(target.Height, Is.LessThan(initial));
            initial = target.Height; BattleProjectile.SpawnForTeam(director, BattleTeam.Red, origin, aim, 55, 120, 60, Color.yellow);
            yield return Simulate(.4f); Assert.That(target.Height, Is.EqualTo(initial));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform, false);
            wall.transform.position = origin + aim.normalized * 3; wall.transform.localScale = Vector3.one * 3; Physics.SyncTransforms();
            BattleProjectile.SpawnForTeam(director, BattleTeam.Blue, origin, aim, 55, 120, 60, Color.yellow);
            yield return Simulate(.4f); Assert.That(target.Height, Is.EqualTo(initial), "Terrain must block summit arrows.");
        }

        [UnityTest] public IEnumerator EveryFoundationHasACompleteGroundRouteAndFlyersClimbToSummits()
        {
            StartSiege(); yield return null; yield return null;
            var start = FreePoint(battle.BoundTower); var path = new NavMeshPath();
            foreach (var tower in battle.Towers)
            {
                Vector3 approach = tower.transform.position + Vector3.back * 7;
                Assert.That(NavMesh.SamplePosition(approach, out var hit, 2, NavMesh.AllAreas), Is.True, tower.Title);
                Assert.That(NavMesh.CalculatePath(start, hit.position, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), tower.Title);
                Assert.That(tower.GetComponent<NavMeshObstacle>().radius, Is.EqualTo(3.9f));
            }
            var air = Spawn(BattleTeam.Blue, UnitRole.Skywing, FreePoint(battle.BoundTower, UnitRole.Skywing));
            air.FlightAltitude = 15; yield return Simulate(.5f);
            Assert.That(air.transform.position.y, Is.GreaterThan(2));
            Assert.That(air.Definition.AbilityCount, Is.EqualTo(1)); Assert.That(air.TryAbility(air.EyePosition, Vector3.forward), Is.True); Assert.That(air.Shield, Is.GreaterThan(0));
        }

        [UnityTest] public IEnumerator MinimapPagesTravelAndCameraLimitsFollowCurrentTower()
        {
            StartSiege(); battle.ToggleMount(); hud.Refresh(); yield return Simulate(.05f); Canvas.ForceUpdateCanvases();
            Invoke(hud, "SelectCommandPage", 2); yield return null; Canvas.ForceUpdateCanvases();
            var roster = hud.Canvas.transform.Find("Live command HUD/Character browser");
            var encountered = new HashSet<string>();
            for (int i = 0; i < 4; i++)
            {
                foreach (var button in roster.GetComponentsInChildren<Button>())
                    if (button.name.StartsWith("Control character ")) encountered.Add(button.GetComponentInChildren<Text>().text.Split('\n')[0]);
                Click(roster.Find("Next characters"));
            }
            Assert.That(encountered.Count, Is.EqualTo(29));
            Invoke(hud, "SelectCommandPage", 0); yield return null; Canvas.ForceUpdateCanvases(); battle.ToggleMount();
            var smallTower = battle.Towers[2]; battle.SelectedTower = smallTower; hud.Refresh();
            Click(hud.Canvas.transform.Find("Live command HUD/Command station/Travel"));
            Assert.That(battle.BoundTower, Is.SameAs(smallTower));
            Invoke(player, "MoveSpectator", new Vector3(1000, 1000, 1000)); yield return null;
            Assert.That(player.ViewCamera.transform.position.y, Is.EqualTo(smallTower.Summit.y).Within(.3));
            Assert.That(Mathf.Abs(player.ViewCamera.transform.position.x), Is.LessThanOrEqualTo(terrain.ArenaSize.x * .5f));
            Assert.That(Mathf.Abs(player.ViewCamera.transform.position.z), Is.LessThanOrEqualTo(terrain.ArenaSize.y * .5f));
            Assert.That(hud.Minimap.Unproject(hud.Minimap.rectTransform.rect.center).x, Is.EqualTo(smallTower.transform.position.x).Within(.1));
        }

        [UnityTest] public IEnumerator EnemyRecruitsContinuouslyAndRestartResetsEconomiesOwnershipAndPossession()
        {
            director.NewGame(); float initial = battle.RedGold;
            Time.timeScale = 6;
            yield return Simulate(14);
            Assert.That(director.AliveRed, Is.Zero, "The opening lasts 20 seconds.");
            ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[1] = initial = 500;
            yield return Simulate(32);
            Assert.That(director.AliveRed, Is.GreaterThanOrEqualTo(2));
            foreach (var unit in director.Units) if (unit.Team == BattleTeam.Red) Assert.That(unit.SpawnTower, Is.Not.Null);
            Assert.That(battle.RedGold, Is.LessThan(initial + 32 * TowerBattle.MaximumIncome), "AI spends its finite economy.");
            director.RestartBattle(); FreezeEnemy();
            Assert.That(battle.BlueGold, Is.EqualTo(TowerBattle.StartingGold)); Assert.That(battle.RedGold, Is.EqualTo(TowerBattle.StartingGold));
            Assert.That(director.AliveBlue + director.AliveRed, Is.Zero); Assert.That(battle.BoundTower, Is.SameAs(battle.Towers[0]));
            Assert.That(battle.BoundTower.Height, Is.EqualTo(12)); Assert.That(player.CurrentUnit, Is.Null);
            Time.timeScale = 1; yield return null;
            CaptureOverview();
        }
        [UnityTest] public IEnumerator AutonomousGroundSiegeRebuildsAndAirRaidTakesAnIntactEnemyTether()
        {
            StartSiege(); Time.timeScale = 3;
            var outpost = battle.Towers[3];
            var brute = Spawn(BattleTeam.Blue, UnitRole.Brute, outpost.transform.position + Vector3.back * 4.5f);
            var ai = brute.GetComponent<BattleUnitAI>();
            typeof(BattleUnitAI).GetField("towerObjective", Private).SetValue(ai, outpost);
            typeof(BattleUnitAI).GetField("nextTowerChoice", Private).SetValue(ai, Time.time + 100); ai.enabled = true;
            float deadline = Time.time + 80;
            while (outpost.Owner != BattleTeam.Blue && Time.time < deadline) yield return null;
            ai.enabled = false;
            Assert.That(outpost.Owner, Is.EqualTo(BattleTeam.Blue), "A ground fighter must attack, break, secure and pay to rebuild the tower autonomously.");
            Assert.That(outpost.Height, Is.EqualTo(3).Within(.1));
            var keep = battle.EnemyBoundTower; float height = keep.Height;
            var flyer = Spawn(BattleTeam.Blue, UnitRole.Stormwing, keep.transform.position + new Vector3(5, 2, 0));
            ai = flyer.GetComponent<BattleUnitAI>();
            typeof(BattleUnitAI).GetField("towerObjective", Private).SetValue(ai, keep);
            typeof(BattleUnitAI).GetField("nextTowerChoice", Private).SetValue(ai, Time.time + 100); ai.enabled = true;
            yield return Simulate(15);
            Assert.That(keep.Owner, Is.EqualTo(BattleTeam.Blue), "A flying AI must climb, approach and hold the summit without destroying it.");
            Assert.That(keep.Height, Is.EqualTo(height)); Assert.That(director.Phase, Is.EqualTo(BattlePhase.Victory));
        }
        [UnityTest] public IEnumerator CapturingSpawnTowerWhileItsCharacterIsPossessedCausesDefeat()
        {
            StartSiege(); battle.ToggleMount();
            battle.TryDeploy(UnitRole.Militia, FreePoint(battle.BoundTower, UnitRole.Militia), out var unit);
            unit.GetComponent<BattleUnitAI>().enabled = false;
            Assert.That(player.Possess(unit), Is.True); yield return Simulate(.15f);
            var origin = unit.SpawnTower; Assert.That(player.CurrentUnit, Is.SameAs(unit));
            Put(unit, origin.transform.position + Vector3.right * 30);
            origin.TakeDamage(100000, BattleTeam.Red);
            Spawn(BattleTeam.Red, UnitRole.Vanguard, origin.transform.position + Vector3.forward * 7);
            Assert.That(origin.TryBuild(BattleTeam.Red), Is.True);
            Assert.That(director.Phase, Is.EqualTo(BattlePhase.Defeat));
            yield return null;
        }
        [UnityTest] public IEnumerator EveryFlightTierHasAnAuthoredAssetAndCostsMoreForGreaterReach()
        {
            float lastCeiling = 0; int lastCost = 0, count = 0;
            foreach (var definition in battle.Definitions)
            {
                if (!definition.IsFlying) continue;
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>($"Assets/BattlefieldPrototype/Definitions/{definition.Role}.asset");
                Assert.That(asset, Is.Not.Null);
                Assert.That(asset.MaximumFlightHeight, Is.EqualTo(definition.MaximumFlightHeight));
                Assert.That(asset.MaximumFlightHeight, Is.GreaterThan(lastCeiling));
                Assert.That(asset.DeploymentCost, Is.GreaterThan(lastCost));
                Assert.That(asset.AbilityCount, Is.EqualTo(definition.AbilityCount));
                lastCeiling = asset.MaximumFlightHeight; lastCost = asset.DeploymentCost; count++;
            }
            Assert.That(count, Is.EqualTo(5)); Assert.That(lastCeiling, Is.EqualTo(BattleTower.MaximumHeight));
            yield return null;
        }

        [UnityTest] public IEnumerator BothTeamsMustReachTheirFlightCeilingAndChannelBeforeCapturing()
        {
            StartSiege(); ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[1] = 500; ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[0] = 500;
            foreach (BattleTeam team in new[] { BattleTeam.Blue, BattleTeam.Red })
            {
                var tower = battle.Towers[team == BattleTeam.Blue ? 3 : 2];
                var previous = team == BattleTeam.Blue ? BattleTeam.Red : BattleTeam.Blue;
                foreach (var definition in battle.Definitions)
                {
                    if (!definition.IsFlying) continue;
                    typeof(BattleTower).GetProperty("Owner").SetValue(tower, previous);
                    float ceiling = definition.MaximumFlightHeight;
                    typeof(BattleTower).GetProperty("Height").SetValue(tower, Mathf.Min(36, ceiling + .25f)); Invoke(tower, "Refresh");
                    var flyer = Spawn(team, definition.Role, tower.transform.position + new Vector3(5, ceiling, 0));
                    float gold = battle.Gold(team);
                    if (ceiling < BattleTower.MaximumHeight)
                    {
                        Invoke(tower, "TickCapture", 9f);
                        Assert.That(tower.Owner, Is.EqualTo(previous), "A reachable position below a taller summit must not bypass tower-height eligibility.");
                        Assert.That(tower.CaptureProgress, Is.Zero);
                    }
                    Put(flyer, tower.transform.position + new Vector3(5, ceiling + .5f, 0));
                    Invoke(tower, "TickCapture", 9f);
                    Assert.That(tower.Owner, Is.EqualTo(previous), definition.Role + " cannot capture above its ceiling, even if placed there.");
                    Assert.That(tower.CaptureProgress, Is.Zero);
                    Assert.That(battle.Gold(team), Is.EqualTo(gold));
                    typeof(BattleTower).GetProperty("Height").SetValue(tower, ceiling); Invoke(tower, "Refresh");
                    Put(flyer, tower.transform.position + new Vector3(5, ceiling, 0));
                    Assert.That(tower.CanBeRaidedBy(definition), Is.True, "Equality at the ceiling must be eligible.");
                    Invoke(tower, "TickCapture", .1f);
                    Assert.That(tower.Owner, Is.EqualTo(previous), "Arrival is not an instant takeover.");
                    Invoke(tower, "TickCapture", 3.9f);
                    Assert.That(tower.CaptureProgress, Is.EqualTo(.5f).Within(.001));
                    Assert.That(tower.Owner, Is.EqualTo(previous));
                    Invoke(tower, "TickCapture", 4.1f);
                    Assert.That(tower.Owner, Is.EqualTo(team)); Assert.That(tower.Height, Is.EqualTo(ceiling));
                    Assert.That(battle.Gold(team), Is.EqualTo(gold - BattleTower.CaptureCost));
                    flyer.TakeDamage(100000, null);
                }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator FlightMotorCapsAiAndPossessedUnitsAndHandlesInvalidAltitudeRequests()
        {
            StartSiege();
            foreach (var definition in battle.Definitions)
            {
                if (!definition.IsFlying) continue;
                var flyer = Spawn(BattleTeam.Blue, definition.Role, FreePoint(battle.BoundTower, definition.Role));
                Put(flyer, flyer.transform.position + Vector3.up * (definition.MaximumFlightHeight - .05f));
                flyer.FlightAltitude = float.PositiveInfinity; Assert.That(float.IsFinite(flyer.FlightAltitude), Is.True);
                flyer.FlightAltitude = 1000;
                Assert.That(flyer.FlightAltitude, Is.EqualTo(definition.MaximumFlightHeight));
                yield return Simulate(.25f);
                Assert.That(flyer.transform.position.y, Is.LessThanOrEqualTo(definition.MaximumFlightHeight + .01f));
                Assert.That(player.Possess(flyer), Is.True); yield return Simulate(.15f);
                hud.Refresh();
                var flightPanel = hud.Canvas.transform.Find("Live command HUD/Character dossier/Dossier").GetComponent<Text>();
                Assert.That(flightPanel.text, Does.Contain($"MAX {definition.MaximumFlightHeight:0}m"));
                Assert.That(flightPanel.preferredHeight, Is.LessThanOrEqualTo(flightPanel.rectTransform.rect.height + 1), definition.Role + " possession instructions must fit.");
                flyer.FlightAltitude = 1000; yield return Simulate(.25f);
                Assert.That(flyer.transform.position.y, Is.LessThanOrEqualTo(definition.MaximumFlightHeight + .01f));
                player.ReleaseControl(); flyer.TakeDamage(100000, null);
            }
        }

        [UnityTest] public IEnumerator RaisingATowerBeyondTheRaidersCeilingCancelsAnOngoingRaid()
        {
            StartSiege(); ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[1] = 500; var tower = battle.Towers[3];
            Assert.That(tower.TryBuild(BattleTeam.Red), Is.True); Assert.That(tower.Height, Is.EqualTo(9));
            var flyer = Spawn(BattleTeam.Blue, UnitRole.Skywing, tower.transform.position + new Vector3(5, 9, 0));
            Invoke(tower, "TickCapture", 4f); Assert.That(tower.CaptureProgress, Is.GreaterThan(0));
            Assert.That(tower.TryBuild(BattleTeam.Red), Is.True); Assert.That(tower.Height, Is.EqualTo(12));
            Invoke(tower, "TickCapture", 9f);
            Assert.That(tower.CaptureProgress, Is.Zero); Assert.That(tower.Owner, Is.EqualTo(BattleTeam.Red));
            Assert.That(tower.CanBeRaidedBy(flyer.Definition), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator LowFlyingAiDamagesTallTowersThenRaidsOnlyOnceTheyAreReachable()
        {
            StartSiege(); ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[1] = 500; var tower = battle.Towers[3];
            Assert.That(tower.TryBuild(BattleTeam.Red), Is.True); Assert.That(tower.TryBuild(BattleTeam.Red), Is.True);
            var flyer = Spawn(BattleTeam.Blue, UnitRole.Skywing, tower.transform.position + new Vector3(7, 8.9f, 0));
            var ai = flyer.GetComponent<BattleUnitAI>();
            typeof(BattleUnitAI).GetField("towerObjective", Private).SetValue(ai, tower);
            typeof(BattleUnitAI).GetField("nextTowerChoice", Private).SetValue(ai, Time.time + 200);
            ai.enabled = true; Time.timeScale = 6;
            float deadline = Time.time + 90;
            while (tower.Owner != BattleTeam.Blue && Time.time < deadline) yield return null;
            Assert.That(tower.Owner, Is.EqualTo(BattleTeam.Blue));
            Assert.That(tower.Height, Is.GreaterThan(0).And.LessThanOrEqualTo(flyer.Definition.MaximumFlightHeight + .001f));
            Assert.That(flyer.transform.position.y, Is.LessThanOrEqualTo(flyer.Definition.MaximumFlightHeight + .01f));
        }

        [UnityTest] public IEnumerator NewFlyerPowersHealOnlyAlliesAndSiegeBlastsRespectCover()
        {
            StartSiege(); Vector3 point = FreePoint(battle.BoundTower) + Vector3.up * 3;
            var healer = Spawn(BattleTeam.Blue, UnitRole.Dawnwing, point);
            var ally = Spawn(BattleTeam.Blue, UnitRole.Vanguard, point + Vector3.right * 2);
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, point + Vector3.forward * 2);
            ally.TakeDamage(50, null); enemy.TakeDamage(50, null); float health = ally.Health, enemyHealth = enemy.Health;
            Assert.That(healer.TryAbility(1, healer.EyePosition, Vector3.forward), Is.True);
            Assert.That(ally.Health, Is.EqualTo(health + 30)); Assert.That(ally.Shield, Is.EqualTo(20));
            Assert.That(enemy.Health, Is.EqualTo(enemyHealth)); Assert.That(enemy.Shield, Is.Zero);
            var royal = Spawn(BattleTeam.Blue, UnitRole.Crownwing, point + Vector3.back * 5);
            Assert.That(royal.TryAbility(1, royal.EyePosition, Vector3.forward), Is.True);
            Assert.That(royal.Shield, Is.EqualTo(100)); Assert.That(royal.IsGuarding, Is.True);
            var tower = battle.Towers[3];
            var bomber = Spawn(BattleTeam.Blue, UnitRole.Emberwing, tower.transform.position + new Vector3(10, 7, 0));
            Physics.SyncTransforms(); float towerHealth = tower.Health;
            Vector3 targetPoint = tower.AttackPoint(bomber.EyePosition), aim = (targetPoint - bomber.EyePosition).normalized;
            Assert.That(bomber.TryAbility(1, bomber.EyePosition, aim), Is.True);
            Assert.That(tower.Health, Is.EqualTo(towerHealth - bomber.Definition.AbilityPower).Within(.01));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform);
            wall.transform.position = (targetPoint + bomber.EyePosition) * .5f;
            wall.transform.rotation = Quaternion.LookRotation(aim); wall.transform.localScale = new Vector3(6, 6, .6f);
            typeof(BattleUnit).GetProperty("SecondaryAbilityCooldownRemaining").SetValue(bomber, 0f);
            Physics.SyncTransforms(); towerHealth = tower.Health;
            Assert.That(bomber.TryAbility(1, bomber.EyePosition, aim), Is.True);
            Assert.That(tower.Health, Is.EqualTo(towerHealth));
            yield return null;
        }

        [UnityTest] public IEnumerator FlightCeilingsRaidWarningsAndAllFlyerDescriptionsFitTheGameplayPanels()
        {
            StartSiege(); battle.ToggleMount();
            foreach (var definition in battle.Definitions)
            {
                if (!definition.IsFlying) continue;
                battle.ChooseRecruit(definition.Role); hud.Refresh();
                var label = hud.Canvas.transform.Find("Live command HUD/Character dossier/Dossier").GetComponent<Text>();
                Assert.That(label.text, Does.Contain($"{definition.MaximumFlightHeight:0}m"));
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1));
            }
            typeof(TowerBattleHud).GetField("page", Private).SetValue(hud, 3); hud.Refresh(); CaptureHud("Five-flight-tiers");
            battle.ChooseRecruit(UnitRole.Skywing); battle.SelectedTower = battle.EnemyBoundTower; hud.Refresh();
            var notice = hud.Canvas.transform.Find("Live command HUD/Command advice/Command feedback").GetComponent<Text>();
            Assert.That(notice.text, Does.Contain("TOWER TOO HIGH"));
            battle.ChooseRecruit(UnitRole.Crownwing); hud.Refresh(); Assert.That(notice.text, Does.Contain("RAID IN REACH"));
            var watch = battle.Towers[3]; battle.SelectedTower = watch;
            Spawn(BattleTeam.Blue, UnitRole.Skywing, watch.transform.position + new Vector3(5, watch.Height, 0));
            battle.TrySpend(BattleTeam.Blue, Mathf.FloorToInt(battle.BlueGold));
            Invoke(watch, "TickCapture", 9f); hud.Refresh();
            Assert.That(notice.text, Does.Contain("RAID WAITING FOR GOLD")); Assert.That(watch.Owner, Is.EqualTo(BattleTeam.Red));
            var bound = battle.BoundTower;
            Spawn(BattleTeam.Red, UnitRole.Stormwing, bound.transform.position + new Vector3(5, bound.Height, 0));
            Invoke(bound, "TickCapture", 2f); hud.Refresh();
            Assert.That(notice.text, Does.Contain("BOUND TOWER UNDER RAID"));
            Assert.That(notice.preferredHeight, Is.LessThanOrEqualTo(notice.rectTransform.rect.height + 1));
            CaptureHud("Flight-tier-recruitment-and-raid-warning");
            yield return null;
        }

        [UnityTest] public IEnumerator FiveFlyingCharactersHaveWingedPresentationModels()
        {
            StartSiege(); int index = 0;
            foreach (var definition in battle.Definitions)
            {
                if (!definition.IsFlying) continue;
                var flyer = Spawn(BattleTeam.Blue, definition.Role, new Vector3((index++ - 2) * 4, 3, 0));
                flyer.Face(Vector3.back);
                Assert.That(flyer.VisualRoot.GetComponent<BattleCharacterModel>(), Is.Not.Null);
                Assert.That(flyer.VisualRoot.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(0));
            }
            yield return null;
            CaptureOverview("Five-flyer-lineup", new Vector3(0, 6.5f, -23), new Vector3(0, 4.3f, 0));
        }

        void CaptureHud(string name, bool verifyPointer = false)
        {
            string directory = Environment.GetEnvironmentVariable("BATTLEFIELD_PREVIEW_DIR"); if (string.IsNullOrEmpty(directory)) return;
            var camera = player.ViewCamera; var previousTarget = camera.targetTexture;
            var mode = hud.Canvas.renderMode; float distance = hud.Canvas.planeDistance;
            var panel = (RectTransform)hud.Canvas.transform;
            var panelPosition = panel.localPosition; var panelRotation = panel.localRotation; var panelScale = panel.localScale;
            var panelSize = panel.sizeDelta; var panelPivot = panel.pivot; var panelMin = panel.anchorMin; var panelMax = panel.anchorMax;
            var canvasScaler = hud.Canvas.GetComponent<CanvasScaler>(); float previousScale = canvasScaler.scaleFactor;
            var rt = new RenderTexture(1440, 900, 24); var image = new Texture2D(1440, 900, TextureFormat.RGB24, false); var active = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; hud.Canvas.renderMode = RenderMode.ScreenSpaceCamera; hud.Canvas.planeDistance = .5f;
                // Render the authored 1440x900 UI at the capture's resolution.
                // Actual overlay fitting is verified separately in the Game view.
                canvasScaler.scaleFactor = 1;
                hud.Canvas.GetComponent<CanvasScaler>().SendMessage("Handle");
                foreach (var text in hud.Canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt });
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); image.Apply();
                if (verifyPointer)
                {
                    var dot = Ui("UI pointing dot");
                    var pixelPosition = camera.WorldToViewportPoint(dot.position);
                    var pixel = image.GetPixel(Mathf.RoundToInt(pixelPosition.x * 1440), Mathf.RoundToInt(pixelPosition.y * 900));
                    TestContext.WriteLine($"Pointer pixel {pixelPosition}: {pixel}; local {dot.localPosition}; scale {dot.lossyScale}");
                    var graphic = dot.GetComponent<Graphic>();
                    TestContext.WriteLine($"Pointer mesh: {graphic.canvasRenderer.GetMesh()?.vertexCount} vertices; culled: {graphic.canvasRenderer.cull}; color: {graphic.color}; renderer: {graphic.canvasRenderer.GetColor()}");
                    Assert.That(pixel.g, Is.GreaterThan(.7f), "The pressed green cursor must actually render on the UI.");
                }
                System.IO.Directory.CreateDirectory(directory); System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; hud.Canvas.renderMode = mode; hud.Canvas.planeDistance = distance;
                canvasScaler.scaleFactor = previousScale;
                RenderTexture.active = active; hud.Canvas.GetComponent<CanvasScaler>().SendMessage("Handle");
                if (mode == RenderMode.WorldSpace)
                {
                    panel.anchorMin = panelMin; panel.anchorMax = panelMax; panel.pivot = panelPivot; panel.sizeDelta = panelSize;
                    panel.localPosition = panelPosition; panel.localRotation = panelRotation; panel.localScale = panelScale;
                }
                foreach (var text in hud.Canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(image);
            }
        }
        void CaptureOverview(string name = "Tower-Battlefield", Vector3? viewpoint = null, Vector3? focus = null)
        {
            string directory = Environment.GetEnvironmentVariable("BATTLEFIELD_PREVIEW_DIR"); if (string.IsNullOrEmpty(directory)) return;
            var camera = player.ViewCamera; var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
            var bow = (Transform)typeof(TowerBattleHud).GetField("bow", Private).GetValue(hud); bool showBow = bow.gameObject.activeSelf; bow.gameObject.SetActive(false);
            player.enabled = false; hud.Canvas.gameObject.SetActive(false);
            camera.fieldOfView = 55; camera.transform.position = viewpoint ?? new Vector3(70, 100, -110); camera.transform.LookAt(focus ?? Vector3.zero);
            var rt = new RenderTexture(1440, 900, 24); var image = new Texture2D(1440, 900, TextureFormat.RGB24, false); var active = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt });
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); image.Apply();
                System.IO.Directory.CreateDirectory(directory); System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = active; camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fov; bow.gameObject.SetActive(showBow);
                player.enabled = true; hud.Canvas.gameObject.SetActive(true); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(image);
            }
        }
    }
}
