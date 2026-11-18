using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        Vector3 RoutePoint(float x, float forward) => battle.BoundTower.transform.position + new Vector3(x, 0, forward);

        [UnityTest] public IEnumerator RoutePlansValidateGroundLimitEightPointsAndDoNotChangeOrdersOrSpendGoldUntilSent()
        {
            Assert.That(battle.BeginRoutePlanning(), Is.False); StartSiege(); var orders = battle.TroopOrders;
            Assert.That(orders.Issue(BattleOrder.Hold), Is.True); float gold = battle.BlueGold;
            Assert.That(battle.BeginRoutePlanning(), Is.True);
            foreach (var point in new[] { Vector3.one * float.NaN, new Vector3(300, 0, 0), battle.BoundTower.transform.position, RoutePoint(0, 20) + Vector3.up * 4 })
                Assert.That(battle.AddRouteWaypoint(point), Is.False);
            Assert.That(battle.RouteDraft.Count, Is.Zero);
            for (int i = 0; i < 8; i++) Assert.That(battle.AddRouteWaypoint(RoutePoint(-14 + i * 4, 20)), Is.True, battle.Message);
            Assert.That(battle.AddRouteWaypoint(RoutePoint(22, 20)), Is.False); Assert.That(battle.RouteDraft.Count, Is.EqualTo(8));
            Assert.That(battle.UndoRouteWaypoint(), Is.True); Assert.That(battle.RouteDraft.Count, Is.EqualTo(7));
            Assert.That(battle.AddRouteWaypoint(RoutePoint(14, 20)), Is.True);
            Assert.That(battle.StartTroopRoute(), Is.False, "A route needs at least one autonomous friendly troop.");
            Assert.That(orders.Current, Is.EqualTo(BattleOrder.Hold)); Assert.That(battle.RouteDraft.Count, Is.EqualTo(8));
            Spawn(BattleTeam.Blue, UnitRole.Vanguard, RoutePoint(0, 12));
            Assert.That(battle.StartTroopRoute(), Is.True, battle.Message);
            Assert.That(orders.Current, Is.EqualTo(BattleOrder.Route)); Assert.That(orders.Waypoints.Count, Is.EqualTo(8));
            Assert.That(battle.IsPlanningRoute, Is.False); Assert.That(battle.RouteDraft.Count, Is.Zero); Assert.That(battle.BlueGold, Is.EqualTo(gold));
            yield return null;
        }

        [UnityTest] public IEnumerator TroopsActuallyFollowAnAlternateMultiWaypointPathThenResumeTheSiege()
        {
            StartSiege(); var unit = Spawn(BattleTeam.Blue, UnitRole.Vanguard, RoutePoint(0, 12));
            var points = new[] { RoutePoint(-15, 12), RoutePoint(-15, 42), RoutePoint(0, 47) };
            var orders = battle.TroopOrders; Assert.That(orders.IssueRoute(points, out var reason), Is.True, reason);
            unit.GetComponent<BattleUnitAI>().enabled = true; Time.timeScale = 3;
            bool first = false, second = false; float deadline = Time.time + 32;
            while (orders.Current == BattleOrder.Route && Time.time < deadline)
            {
                int index = orders.RouteIndex(unit);
                if (index >= 1) first = true;
                if (index >= 2) second = true;
                yield return null;
            }
            Assert.That(first, Is.True, "The troop must visit the lateral detour instead of taking the direct tower lane.");
            Assert.That(second, Is.True); Assert.That(orders.Current, Is.EqualTo(BattleOrder.Advance));
            Assert.That(Vector3.ProjectOnPlane(unit.transform.position - points[2], Vector3.up).magnitude, Is.LessThan(5));
            Assert.That(orders.Waypoints.Count, Is.Zero);
        }

        [UnityTest] public IEnumerator GroundAndLowFlyingTroopsNavigateAroundBarricadesOnAnIssuedRoute()
        {
            StartSiege(); battle.ToggleMount();
            Vector3 wallPoint = RoutePoint(0, 25);
            Assert.That(battle.TryPlaceDefense(DefenseKind.StoneBarricade, wallPoint, 0, out _), Is.True, battle.Message);
            yield return Simulate(.4f);
            var ground = Spawn(BattleTeam.Blue, UnitRole.Vanguard, RoutePoint(-1, 15));
            var flyer = Spawn(BattleTeam.Blue, UnitRole.Stormwing, RoutePoint(1, 15) + Vector3.up);
            var orders = battle.TroopOrders; Assert.That(orders.IssueRoute(new[] { RoutePoint(0, 37) }, out var reason), Is.True, reason);
            ground.GetComponent<BattleUnitAI>().enabled = flyer.GetComponent<BattleUnitAI>().enabled = true;
            Time.timeScale = 3; float maxGround = 0, maxAir = 0, deadline = Time.time + 25;
            while (orders.Current == BattleOrder.Route && Time.time < deadline)
            {
                maxGround = Mathf.Max(maxGround, Mathf.Abs(ground.transform.position.x));
                maxAir = Mathf.Max(maxAir, Mathf.Abs(flyer.transform.position.x)); yield return null;
            }
            Assert.That(maxGround, Is.GreaterThan(2.9f)); Assert.That(maxAir, Is.GreaterThan(2.9f), "A low flyer must use route corners instead of flying into the wall.");
            Assert.That(orders.Current, Is.EqualTo(BattleOrder.Advance));
            Assert.That(ground.Health, Is.EqualTo(ground.MaxHealth)); Assert.That(flyer.Health, Is.EqualTo(flyer.MaxHealth));
        }

        [UnityTest] public IEnumerator RoutingFiresAtNearbyEnemiesWithoutChasingAndLeavesPossessedAndEnemyUnitsUnderTheirOwnControl()
        {
            StartSiege();
            var ranger = Spawn(BattleTeam.Blue, UnitRole.Ranger, RoutePoint(0, 12));
            var possessed = Spawn(BattleTeam.Blue, UnitRole.Vanguard, RoutePoint(0, -12));
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, RoutePoint(12, 12));
            Assert.That(player.Possess(possessed), Is.True); yield return Simulate(.15f);
            Vector3 controlledPosition = possessed.transform.position;
            Assert.That(battle.BeginRoutePlanning(), Is.True);
            var goal = RoutePoint(-18, 12);
            typeof(PossessionController).GetProperty("AimRay").SetValue(player, new Ray(player.ViewCamera.transform.position, goal - player.ViewCamera.transform.position));
            // Use the minimap-equivalent API here; physical aiming from the opposite side of the tower is intentionally blocked by masonry.
            Assert.That(battle.AddRouteWaypoint(goal), Is.True); Assert.That(battle.StartTroopRoute(), Is.True);
            Assert.That(battle.TroopOrders.TryGetRouteDestination(possessed, out _), Is.False);
            Assert.That(battle.TroopOrders.TryGetRouteDestination(enemy, out _), Is.False);
            ranger.Face(Vector3.right); Physics.SyncTransforms();
            Assert.That(ranger.CanSee(enemy), Is.True, "The combat check must start with the enemy in the ranger's sight cone.");
            ranger.GetComponent<BattleUnitAI>().enabled = true;
            yield return Simulate(1.5f);
            Assert.That(ranger.transform.position.x, Is.LessThan(-2), "The visible enemy is east; the ordered detour is west.");
            Assert.That(player.CurrentUnit, Is.SameAs(possessed)); Assert.That(Vector3.Distance(possessed.transform.position, controlledPosition), Is.LessThan(.3f));
            Assert.That(enemy.Health, Is.LessThan(enemy.MaxHealth), "A routed ranger may still fire at a visible enemy in range while moving.");
        }

        [UnityTest] public IEnumerator RouteProgressIsIndependentNewTroopsJoinAndBasicOrdersOrResetCancelTheRoute()
        {
            StartSiege(); var orders = battle.TroopOrders;
            var first = Spawn(BattleTeam.Blue, UnitRole.Vanguard, RoutePoint(0, 12));
            var second = Spawn(BattleTeam.Blue, UnitRole.Vanguard, RoutePoint(5, 12));
            var points = new[] { RoutePoint(-10, 25) };
            Assert.That(orders.IssueRoute(points, out var reason), Is.True, reason);
            Assert.That(orders.TryGetRouteDestination(first, out var destination), Is.True); Put(first, destination);
            Assert.That(orders.TryGetRouteDestination(first, out _), Is.False);
            Assert.That(orders.IsAdvancing(first), Is.True); Assert.That(orders.IsAdvancing(second), Is.False); Assert.That(orders.Current, Is.EqualTo(BattleOrder.Route));
            var recruited = Spawn(BattleTeam.Blue, UnitRole.Militia, RoutePoint(10, 12));
            Assert.That(orders.TryGetRouteDestination(recruited, out _), Is.True); Assert.That(orders.RouteIndex(recruited), Is.Zero);
            Assert.That(orders.Issue(BattleOrder.Hold), Is.True); Assert.That(orders.Waypoints.Count, Is.Zero);
            Assert.That(orders.Destination(first), Is.EqualTo(first.transform.position));
            Assert.That(orders.Issue(BattleOrder.Protect, second), Is.True); Assert.That(orders.Issue(BattleOrder.Route), Is.False);
            Assert.That(orders.Current, Is.EqualTo(BattleOrder.Protect));
            Assert.That(orders.IssueRoute(points, out reason), Is.True, reason);
            Assert.That(orders.TryGetRouteDestination(first, out _), Is.False);
            second.TakeDamage(100000, null); recruited.TakeDamage(100000, null); Invoke(orders, "Update");
            Assert.That(orders.Current, Is.EqualTo(BattleOrder.Advance));
            battle.BeginRoutePlanning(); battle.AddRouteWaypoint(RoutePoint(0, 25));
            director.RestartBattle(); FreezeEnemy(); Assert.That(battle.IsPlanningRoute, Is.False);
            Assert.That(orders.Current, Is.EqualTo(BattleOrder.Advance)); Assert.That(orders.Waypoints.Count, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator InvalidRoutesAreRejectedAtomicallyAndChangingViewsCancelsOnlyTheUnsentPlan()
        {
            StartSiege(); var orders = battle.TroopOrders; var unit = Spawn(BattleTeam.Blue, UnitRole.Vanguard, RoutePoint(0, 12));
            Assert.That(orders.Issue(BattleOrder.Hold), Is.True); Vector3 held = orders.Destination(unit);
            foreach (var points in new[] { new Vector3[0], new[] { RoutePoint(0, 20), battle.BoundTower.transform.position }, new[] { RoutePoint(0, 20), RoutePoint(0, 20) } })
            {
                Assert.That(orders.IssueRoute(points, out _), Is.False); Assert.That(orders.Current, Is.EqualTo(BattleOrder.Hold));
                Assert.That(orders.Destination(unit), Is.EqualTo(held));
            }
            Assert.That(orders.IssueRoute(new[] { RoutePoint(-10, 20) }, out var reason), Is.True, reason);
            battle.BeginRoutePlanning(); Assert.That(battle.AddRouteWaypoint(RoutePoint(10, 20)), Is.True);
            battle.ToggleMount(); Assert.That(battle.IsPlanningRoute, Is.False); Assert.That(orders.Current, Is.EqualTo(BattleOrder.Route));
            battle.BeginRoutePlanning(); battle.AddRouteWaypoint(RoutePoint(10, 20)); battle.ChooseRecruit(UnitRole.Militia);
            Assert.That(battle.IsPlanningRoute, Is.False); Assert.That(orders.Current, Is.EqualTo(BattleOrder.Route));
            battle.BeginRoutePlanning(); battle.TroopOrders.IssueForPlayer(BattleOrder.Advance, player);
            Assert.That(battle.IsPlanningRoute, Is.False); Assert.That(orders.Current, Is.EqualTo(BattleOrder.Advance));
            yield return null;
        }

        [UnityTest] public IEnumerator RouteHudUsesMinimapAndWorldClicksWithoutFiringAndShowsTheCommittedPath()
        {
            StartSiege(); Spawn(BattleTeam.Blue, UnitRole.Vanguard, RoutePoint(0, 12));
            hud.Refresh(); yield return null; hud.Refresh(); Invoke(hud, "CancelPointer");
            Invoke(hud, "SelectCommandPage", 1); yield return null; Canvas.ForceUpdateCanvases();
            var panel = hud.Canvas.transform.Find("Live command HUD/Troop orders");
            Click(panel.Find("Plan route")); yield return null; hud.Refresh();
            Assert.That(battle.IsPlanningRoute, Is.True); Assert.That(hud.Minimap.IsRoutingMap, Is.True);
            Assert.That(hud.Minimap.Unproject(hud.Minimap.rectTransform.rect.center), Is.EqualTo(Vector3.zero));
            float gold = battle.BlueGold; var point = RoutePoint(0, 20);
            Vector2 local = (Vector2)Invoke(hud.Minimap, "Project", point);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, hud.Minimap.rectTransform.TransformPoint(local));
            Invoke(hud, "Dispatch", screen, true); Invoke(hud, "Dispatch", screen, false);
            Assert.That(battle.RouteDraft.Count, Is.EqualTo(1));
            point = RoutePoint(10, 20);
            typeof(PossessionController).GetProperty("AimRay").SetValue(player, new Ray(player.ViewCamera.transform.position, point - player.ViewCamera.transform.position));
            typeof(PossessionController).GetField("towerPrimaryHeld", Private).SetValue(player, false);
            Invoke(player, "Attack", true, false); Invoke(player, "Attack", true, false);
            Assert.That(battle.RouteDraft.Count, Is.EqualTo(2)); Assert.That(root.GetComponentsInChildren<BattleProjectile>().Length, Is.Zero);
            hud.Refresh(); Click(panel.Find("Undo route")); Assert.That(battle.RouteDraft.Count, Is.EqualTo(1));
            Assert.That(battle.AddRouteWaypoint(RoutePoint(-15, 35)), Is.True); hud.Refresh(); Canvas.ForceUpdateCanvases();
            var label = panel.Find("Route instructions").GetComponent<Text>(); Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1));
            label = hud.Canvas.transform.Find("Live command HUD/Character dossier/Dossier").GetComponent<Text>();
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1));
            var corners = new Vector3[4]; ((RectTransform)panel).GetWorldCorners(corners);
            foreach (var corner in corners)
            { var pixel = RectTransformUtility.WorldToScreenPoint(null, corner); Assert.That(pixel.x, Is.InRange(-1, Screen.width + 1)); Assert.That(pixel.y, Is.InRange(-1, Screen.height + 1)); }
            hud.SendMessage("LateUpdate"); CaptureHud("Troop-route-plan");
            Click(panel.Find("Send route")); Assert.That(battle.IsPlanningRoute, Is.False);
            Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Route)); Assert.That(battle.TroopOrders.Waypoints.Count, Is.EqualTo(2));
            Assert.That(battle.BlueGold, Is.EqualTo(gold)); hud.Refresh(); hud.SendMessage("LateUpdate"); CaptureHud("Troop-route-active");
            Click(panel.Find("Advance order")); Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Advance));
            Assert.That(battle.TroopOrders.Waypoints.Count, Is.Zero);
        }
    }
}
