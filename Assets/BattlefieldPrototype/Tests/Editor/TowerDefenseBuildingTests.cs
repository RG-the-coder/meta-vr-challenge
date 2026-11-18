using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        Vector3 DefensePoint(DefenseKind kind, Vector3? near = null)
        {
            Vector3 center = near ?? battle.BoundTower.transform.position + Vector3.forward * 22;
            for (int z = 0; z <= 40; z += 5) for (int x = -20; x <= 20; x += 5)
                if (battle.TryDefensePlacement(kind, center + new Vector3(x, 0, z), battle.DefenseRotation, out var point, out _, out _)) return point;
            Assert.Fail("An owned tower must have buildable defense ground."); return default;
        }
        BattleDefense BuildDefense(DefenseKind kind, Vector3? near = null)
        {
            Assert.That(battle.TryPlaceDefense(kind, DefensePoint(kind, near), battle.DefenseRotation, out var defense), Is.True, battle.Message);
            return defense;
        }

        [UnityTest] public IEnumerator DefenseConstructionRequiresGroundLevelAndLocksImmediatelyOnMountOrPossession()
        {
            Assert.That(battle.CanConstruct, Is.False);
            StartSiege(); Vector3 point = DefensePoint(DefenseKind.TimberBarricade); float gold = battle.BlueGold;
            Assert.That(battle.TryPlaceDefense(DefenseKind.TimberBarricade, point, 0, out _), Is.False);
            Assert.That(battle.ChooseDefense(DefenseKind.Caltrops), Is.False);
            battle.ToggleMount(); Assert.That(battle.CanConstruct, Is.True);
            Vector3 eye = player.ViewCamera.transform.position;
            player.ViewCamera.transform.position += Vector3.up * 5;
            Assert.That(battle.CanConstruct, Is.False);
            Assert.That(battle.TryPlaceDefense(DefenseKind.Caltrops, point, 0, out _), Is.False);
            player.ViewCamera.transform.position = eye;
            var unit = Spawn(BattleTeam.Blue, UnitRole.Vanguard, FreePoint(battle.BoundTower));
            Assert.That(player.Possess(unit), Is.True); Assert.That(battle.CanConstruct, Is.False);
            Assert.That(battle.TryPlaceDefense(DefenseKind.Caltrops, point, 0, out _), Is.False);
            yield return Simulate(.15f); Assert.That(battle.CanConstruct, Is.False);
            battle.ReturnToTower(); Assert.That(battle.CanConstruct, Is.False);
            battle.ToggleMount(); Assert.That(battle.CanConstruct, Is.True);
            Assert.That(battle.ChooseDefense(DefenseKind.Caltrops), Is.True);
            battle.ToggleMount(); Assert.That(battle.SelectedDefense, Is.Null);
            Assert.That(battle.Defenses.Count, Is.Zero); Assert.That(battle.BlueGold, Is.GreaterThanOrEqualTo(gold));
            battle.GoHome(); Assert.That(battle.CanConstruct, Is.False);
        }

        [UnityTest] public IEnumerator DefensesUseMuchLargerZonesAroundAnyOwnedStandingTower()
        {
            StartSiege(); battle.ToggleMount(); var bound = battle.BoundTower; var watch = battle.Towers[2];
            Assert.That(TowerBattle.DefenseBuildRadius(bound), Is.GreaterThanOrEqualTo(bound.DeploymentRadius * 2.5f));
            Vector3 point = default; bool found = false;
            for (float z = -80; z < 80 && !found; z += 4) for (float x = -72; x < 72; x += 4)
            {
                var candidate = new Vector3(x, 0, z);
                if (Vector3.Distance(candidate, bound.transform.position) < TowerBattle.DefenseBuildRadius(bound) + 4) continue;
                if (!battle.TryDefensePlacement(DefenseKind.TimberBarricade, candidate, 0, out point, out var owner, out _) || owner != watch) continue;
                found = true; break;
            }
            Assert.That(found, Is.True, "The other owned tower must independently provide a larger construction zone.");
            Assert.That(battle.IsInDefenseBuildZone(point), Is.True);
            float gold = battle.BlueGold, red = battle.RedGold;
            Assert.That(battle.TryPlaceDefense(DefenseKind.TimberBarricade, point, 0, out var defense), Is.True);
            Assert.That(defense.BuiltNear, Is.SameAs(watch)); Assert.That(battle.BoundTower, Is.SameAs(bound));
            Assert.That(battle.BlueGold, Is.EqualTo(gold - 20)); Assert.That(battle.RedGold, Is.EqualTo(red));
            typeof(BattleTower).GetProperty("Owner").SetValue(watch, BattleTeam.Red);
            Assert.That(battle.IsInDefenseBuildZone(point), Is.False, "Lost towers cannot supply construction range.");
            yield return null;
        }

        [UnityTest] public IEnumerator FiveDefenseKindsHaveDistinctCostsHealthAndPhysicalNavigationFootprints()
        {
            StartSiege(); ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[0] = 500; battle.ToggleMount(); float gold = battle.BlueGold; int total = 0;
            for (int i = 0; i < DefenseCatalog.Count; i++)
            {
                var kind = (DefenseKind)i;
                Assert.That(battle.TryPlaceDefense(kind, battle.BoundTower.transform.position + new Vector3((i - 2) * 9, 0, 20), 0, out var defense), Is.True, battle.Message);
                total += DefenseCatalog.Cost(kind);
                Assert.That(defense.Kind, Is.EqualTo(kind)); Assert.That(defense.Owner, Is.EqualTo(BattleTeam.Blue));
                Assert.That(defense.Footprint.size, Is.EqualTo(DefenseCatalog.Size(kind)));
                Assert.That(defense.Footprint.isTrigger, Is.EqualTo(!DefenseCatalog.IsBarricade(kind)));
                Assert.That(defense.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(1));
                if (defense.IsBarricade)
                {
                    Assert.That(defense.Health, Is.EqualTo(DefenseCatalog.Health(kind)));
                    Assert.That(defense.GetComponent<NavMeshObstacle>().carving, Is.True);
                }
                else Assert.That(defense.GetComponent<NavMeshObstacle>(), Is.Null);
            }
            Assert.That(battle.Defenses.Count, Is.EqualTo(5)); Assert.That(battle.BlueGold, Is.EqualTo(gold - total));
            yield return null;
            Vector3 center = battle.BoundTower.transform.position + Vector3.forward * 20;
            CaptureOverview("Five-ground-defenses", center + new Vector3(0, 18, 30), center + Vector3.up);
        }

        [UnityTest] public IEnumerator InvalidDefenseFootprintsAndInsufficientFundsNeverChargeGold()
        {
            StartSiege(); battle.ToggleMount(); float gold = battle.BlueGold;
            foreach (var point in new[] { battle.BoundTower.transform.position, new Vector3(55, 0, 75), new Vector3(200, 0, 0),
                battle.BoundTower.transform.position + new Vector3(15, 6, 0), Vector3.one * float.NaN })
                Assert.That(battle.TryPlaceDefense(DefenseKind.TimberBarricade, point, 0, out _), Is.False);
            Assert.That(battle.TryPlaceDefense((DefenseKind)999, DefensePoint(DefenseKind.TimberBarricade), 0, out _), Is.False);
            Assert.That(battle.TryPlaceDefense(DefenseKind.TimberBarricade, DefensePoint(DefenseKind.TimberBarricade), float.NaN, out _), Is.False);
            Assert.That(battle.BlueGold, Is.EqualTo(gold));
            var wall = BuildDefense(DefenseKind.TimberBarricade); gold = battle.BlueGold;
            Assert.That(battle.TryPlaceDefense(DefenseKind.Caltrops, wall.transform.position, 90, out _), Is.False);
            Vector3 empty = DefensePoint(DefenseKind.Caltrops);
            var occupant = Spawn(BattleTeam.Red, UnitRole.Vanguard, empty);
            Assert.That(battle.TryPlaceDefense(DefenseKind.Caltrops, empty, 0, out _), Is.False);
            occupant.TakeDamage(100000, null); yield return null;
            Assert.That(battle.BlueGold, Is.GreaterThanOrEqualTo(gold));
            empty = DefensePoint(DefenseKind.Caltrops);
            battle.TrySpend(BattleTeam.Blue, Mathf.FloorToInt(battle.BlueGold));
            Assert.That(battle.TryPlaceDefense(DefenseKind.Caltrops, empty, 0, out _), Is.False);
            Assert.That(battle.Defenses.Count, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator BarricadesBlockProjectilesTakeHostileMeleeDamageAndOpenWhenDestroyed()
        {
            StartSiege(); battle.ToggleMount();
            Assert.That(battle.TryPlaceDefense(DefenseKind.StoneBarricade, battle.BoundTower.transform.position + Vector3.forward * 20, 0, out var wall), Is.True, battle.Message);
            Vector3 point = wall.transform.position;
            var ally = Spawn(BattleTeam.Blue, UnitRole.Vanguard, point + Vector3.back * 4);
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, point + Vector3.right * 8);
            float health = ally.Health, wallHealth = wall.Health;
            Vector3 origin = point + Vector3.forward * 6 + Vector3.up * 1.4f;
            Physics.SyncTransforms();
            BattleProjectile.SpawnForTeam(director, BattleTeam.Red, origin, Vector3.back, 80, 120, 20, Color.red);
            yield return Simulate(.2f);
            Assert.That(wall.Health, Is.EqualTo(wallHealth - 80)); Assert.That(ally.Health, Is.EqualTo(health));
            wallHealth = wall.Health;
            BattleProjectile.SpawnForTeam(director, BattleTeam.Blue, origin, Vector3.back, 80, 120, 20, Color.cyan);
            yield return Simulate(.2f);
            Assert.That(wall.Health, Is.EqualTo(wallHealth), "Friendly arrows stop at the wall without damaging it.");
            Put(enemy, point + Vector3.forward * 1.9f);
            Assert.That(enemy.TryAttack(enemy.EyePosition, wall.AttackPoint(enemy.EyePosition) - enemy.EyePosition), Is.True);
            Assert.That(wall.Health, Is.EqualTo(wallHealth - enemy.Definition.Damage));
            Assert.That(wall.TakeDamage(100000, BattleTeam.Blue), Is.False);
            Assert.That(wall.TakeDamage(100000, BattleTeam.Red), Is.True);
            Assert.That(wall.IsActive, Is.False); Assert.That(wall.Footprint.enabled, Is.False); Assert.That(battle.Defenses.Count, Is.Zero);
        }

        [UnityTest] public IEnumerator BarricadesCarveWalkableRoutesAndEnemyAiCanDestroyThem()
        {
            StartSiege(); battle.ToggleMount();
            Vector3 point = battle.BoundTower.transform.position + Vector3.forward * 20;
            Assert.That(battle.TryPlaceDefense(DefenseKind.TimberBarricade, point, 0, out var wall), Is.True, battle.Message);
            yield return Simulate(.4f);
            Vector3 start = point + Vector3.forward * 6, end = point + Vector3.back * 6;
            Assert.That(NavMesh.Raycast(start, end, out _, NavMesh.AllAreas), Is.True, "A barricade must carve the direct ground route.");
            var path = new NavMeshPath(); Assert.That(NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Troops must have a route around an isolated wall.");
            var brute = Spawn(BattleTeam.Red, UnitRole.Brute, point + Vector3.forward * 2.5f);
            brute.GetComponent<BattleUnitAI>().enabled = true; Time.timeScale = 3;
            yield return Simulate(12);
            Assert.That(wall == null || !wall.IsActive, Is.True, "Enemy AI must attack a nearby obstructing barricade rather than wait forever.");
        }

        [UnityTest] public IEnumerator CaltropsSlowOnlyEnemyGroundTroopsAndWearOffAfterLeaving()
        {
            StartSiege(); battle.ToggleMount(); var trap = BuildDefense(DefenseKind.Caltrops); Vector3 point = trap.transform.position;
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, point + Vector3.right * 1.8f);
            var ally = Spawn(BattleTeam.Blue, UnitRole.Vanguard, point + Vector3.left * 1.8f);
            var flyer = Spawn(BattleTeam.Red, UnitRole.Stormwing, point + Vector3.forward * 1.8f + Vector3.up * 2);
            float health = enemy.Health;
            yield return Simulate(1.3f);
            Assert.That(enemy.IsSlowed, Is.True); Assert.That(ally.IsSlowed, Is.False); Assert.That(flyer.IsSlowed, Is.False);
            Assert.That(enemy.Health, Is.EqualTo(health)); Assert.That(trap.IsActive, Is.True);
            Put(enemy, point + Vector3.right * 8); yield return Simulate(3.3f); Assert.That(enemy.IsSlowed, Is.False);
        }

        [UnityTest] public IEnumerator SpikedBarricadesHurtOnlyEnemyGroundTroopsInContact()
        {
            StartSiege(); battle.ToggleMount(); var wall = BuildDefense(DefenseKind.SpikeBarricade); Vector3 point = wall.transform.position;
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, point + Vector3.forward * 1.3f);
            var ally = Spawn(BattleTeam.Blue, UnitRole.Vanguard, point + Vector3.back * 1.3f);
            var flyer = Spawn(BattleTeam.Red, UnitRole.Stormwing, point + Vector3.up * 2);
            float health = enemy.Health, friendly = ally.Health, air = flyer.Health;
            yield return Simulate(1.6f);
            Assert.That(enemy.Health, Is.LessThan(health)); Assert.That(enemy.Health, Is.GreaterThan(0));
            Assert.That(ally.Health, Is.EqualTo(friendly)); Assert.That(flyer.Health, Is.EqualTo(air));
        }

        [UnityTest] public IEnumerator BlastMinesArmIgnoreAlliesAndFlyersDetonateOnceAndRespectCover()
        {
            StartSiege(); battle.ToggleMount(); var mine = BuildDefense(DefenseKind.BlastMine); Vector3 point = mine.transform.position;
            var ally = Spawn(BattleTeam.Blue, UnitRole.Vanguard, point + Vector3.back);
            var flyer = Spawn(BattleTeam.Red, UnitRole.Stormwing, point + Vector3.forward + Vector3.up * 2);
            yield return Simulate(1.3f); Assert.That(mine.IsActive, Is.True, "Allies and flyers cannot trigger a mine.");
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, point + Vector3.left * 1.6f);
            var covered = Spawn(BattleTeam.Red, UnitRole.Vanguard, point + Vector3.right * 3.5f);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube); cover.transform.SetParent(root.transform);
            cover.transform.position = point + new Vector3(2.6f, 2, 0); cover.transform.localScale = new Vector3(.3f, 4, 5); Physics.SyncTransforms();
            float health = enemy.Health, hidden = covered.Health, friendly = ally.Health, air = flyer.Health;
            yield return Simulate(.4f);
            Assert.That(battle.Defenses.Count, Is.Zero);
            Assert.That(enemy.Health, Is.EqualTo(health - 90 * (1 - enemy.Definition.ArmorFraction)).Within(.01));
            Assert.That(covered.Health, Is.EqualTo(hidden)); Assert.That(ally.Health, Is.EqualTo(friendly)); Assert.That(flyer.Health, Is.EqualTo(air));
            health = enemy.Health; yield return Simulate(.5f); Assert.That(enemy.Health, Is.EqualTo(health));
        }

        [UnityTest] public IEnumerator GroundWorksHudPlacesFromTheMinimapRotatesAndBuysOnlyOncePerPress()
        {
            StartSiege(); ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[0] = 500; battle.ToggleMount(); hud.Refresh(); yield return null; hud.Refresh();
            Invoke(hud, "SelectCommandPage", 4); yield return null; Canvas.ForceUpdateCanvases();
            var panel = hud.Canvas.transform.Find("Live command HUD/Ground works"); Assert.That(panel.gameObject.activeSelf, Is.True);
            Assert.That(battle.CanConstruct, Is.True);
            var panelRect = (RectTransform)panel;
            Assert.That(panelRect.anchoredPosition.x, Is.EqualTo(516));
            Assert.That(panelRect.rect.width, Is.EqualTo(408));
            Assert.That(hud.Canvas.transform.Find("Live command HUD/Character dossier").gameObject.activeSelf, Is.False, "The left construction panel must not overlap the dossier.");
            CaptureHud("Ground-works-left-palette");
            Invoke(hud, "CancelPointer");
            for (int i = 0; i < DefenseCatalog.Count; i++)
            {
                if (i > 0) { Click(panel.Find("Change defense")); yield return null; hud.Refresh(); }
                Click(panel.Find("Build " + (DefenseKind)i)); yield return null; hud.Refresh(); Canvas.ForceUpdateCanvases();
                Assert.That(((RectTransform)panel).rect.height, Is.EqualTo(174), "Selecting a defense uses the compact left toolbar.");
                Assert.That(((RectTransform)panel).anchoredPosition.x, Is.EqualTo(516), "The placement toolbar stays on the left.");
                Assert.That(battle.SelectedDefense, Is.EqualTo((DefenseKind)i), "Clicked target: " + typeof(TowerBattleHud).GetField("hovered", Private).GetValue(hud));
                var label = panel.Find("Build instructions").GetComponent<Text>();
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), "Instructions must fit for " + (DefenseKind)i);
                label = hud.Canvas.transform.Find("Live command HUD/Character dossier/Dossier").GetComponent<Text>();
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), "Dossier must fit for " + (DefenseKind)i);
            }
            Click(panel.Find("Change defense")); yield return null; hud.Refresh();
            Click(panel.Find("Build TimberBarricade")); yield return null; hud.Refresh();
            Click(panel.Find("Rotate defense")); Assert.That(battle.DefenseRotation, Is.EqualTo(45));
            Assert.That(hud.Minimap.IsBuildingMap, Is.True); Assert.That(hud.Minimap.Unproject(hud.Minimap.rectTransform.rect.center), Is.EqualTo(Vector3.zero));
            Invoke(hud, "SelectCommandPage", 1); yield return null; Canvas.ForceUpdateCanvases();
            Vector3 point = DefensePoint(DefenseKind.TimberBarricade, battle.BoundTower.transform.position + Vector3.forward * 55);
            Vector2 local = (Vector2)Invoke(hud.Minimap, "Project", point);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, hud.Minimap.rectTransform.TransformPoint(local));
            float gold = battle.BlueGold;
            Invoke(hud, "Dispatch", screen, true); Invoke(hud, "Dispatch", screen, false);
            Assert.That(battle.Defenses.Count, Is.EqualTo(1)); Assert.That(battle.BlueGold, Is.EqualTo(gold - 20));
            Assert.That(battle.Defenses[0].transform.eulerAngles.y, Is.EqualTo(45).Within(.01));
            point = DefensePoint(DefenseKind.TimberBarricade, battle.BoundTower.transform.position + Vector3.forward * 20);
            typeof(PossessionController).GetProperty("AimRay").SetValue(player, new Ray(player.ViewCamera.transform.position, point - player.ViewCamera.transform.position));
            typeof(PossessionController).GetField("towerPrimaryHeld", Private).SetValue(player, false);
            Invoke(player, "HandleTowerPrimary"); Invoke(player, "HandleTowerPrimary");
            Assert.That(battle.Defenses.Count, Is.EqualTo(2)); Assert.That(battle.BlueGold, Is.EqualTo(gold - 40));
            hud.Refresh(); hud.SendMessage("LateUpdate"); CaptureHud("Ground-works-and-build-zones");
            Invoke(hud, "SelectCommandPage", 4); yield return null; Canvas.ForceUpdateCanvases();
            Click(panel.Find("Cancel defense")); Assert.That(battle.SelectedDefense, Is.Null); Assert.That(hud.Minimap.IsBuildingMap, Is.False);
            battle.ChooseDefense(DefenseKind.Caltrops); battle.ChooseRecruit(UnitRole.Militia); hud.Refresh();
            Assert.That(battle.SelectedDefense, Is.Null); Assert.That(panel.gameObject.activeSelf, Is.True, "Recruitment must keep barricades and traps available.");
            Assert.That(battle.TryDeploy(UnitRole.Militia, FreePoint(battle.BoundTower, UnitRole.Militia), out _), Is.True);
            hud.Refresh(); yield return null; hud.Refresh();
            Assert.That(panel.gameObject.activeInHierarchy, Is.True, "Placing a character must not hide ground works.");
            Click(panel.Find("Build Caltrops"));
            Assert.That(battle.SelectedDefense, Is.EqualTo(DefenseKind.Caltrops));
            Assert.That(battle.Recruitment, Is.Null, "Choosing a trap switches away from character placement.");
            battle.ChooseRecruit(UnitRole.Militia); hud.Refresh(); yield return null;
            Click(hud.Canvas.transform.Find("Live command HUD/VR HUD pages/VR BUILD"));
            Assert.That(battle.Recruitment, Is.EqualTo(UnitRole.Militia), "Browsing does not cancel placement."); Assert.That(panel.gameObject.activeSelf, Is.True);
            yield return null; hud.Refresh();
            Click(panel.Find("Build Caltrops")); Assert.That(battle.Recruitment, Is.Null);
            battle.ToggleMount(); hud.Refresh(); Assert.That(panel.gameObject.activeSelf, Is.True);
            Assert.That(battle.CanConstruct, Is.False); yield return null;
            Click(panel.Find("Build Caltrops"));
            Assert.That(battle.CanConstruct, Is.True, "Choosing a defense automatically enters ground placement.");
            Assert.That(battle.SelectedDefense, Is.EqualTo(DefenseKind.Caltrops));
            director.RestartBattle(); FreezeEnemy();
            Assert.That(battle.Defenses.Count, Is.Zero); Assert.That(battle.SelectedDefense, Is.Null); Assert.That(battle.BlueGold, Is.EqualTo(TowerBattle.StartingGold));
            yield return null;
            Assert.That(root.GetComponentsInChildren<BattleDefense>().Length, Is.Zero);
        }
    }
}
