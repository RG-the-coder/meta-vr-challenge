using System.Collections.Generic;
using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattle
    {
        public bool IsPlanningRoute { get; private set; }
        public IReadOnlyList<Vector3> RouteDraft => routeDraft;
        public BattleOrders TroopOrders => Director != null ? Director.GetComponent<BattleOrders>() : null;
        readonly List<Vector3> routeDraft = new List<Vector3>();
        public bool BeginRoutePlanning()
        {
            if (Director == null || !Director.IsBattleRunning || Player == null || Player.IsSwitchingUnit || TroopOrders == null) return false;
            IsPlanningRoute = true; routeDraft.Clear(); Recruitment = null; SelectedDefense = null; SelectedSummitPower = null;
            Player.OpenMenuCursor();
            Report("Plan an alternate path: click up to 8 walkable points on the ground or minimap, then GO ROUTE / Enter."); return true;
        }
        public void CancelRoutePlanning() { IsPlanningRoute = false; routeDraft.Clear(); }
        public bool AddRouteWaypoint(Vector3 point)
        {
            if (!IsPlanningRoute || !Director.IsBattleRunning) return false;
            if (routeDraft.Count >= BattleOrders.MaximumWaypoints) return Fail("Route limit: 8 waypoints. Undo a point or send this route.");
            if (!TroopOrders.TryValidateWaypoint(point, out var ground, out var reason)) return Fail(reason);
            if (routeDraft.Count > 0 && ((ground - routeDraft[routeDraft.Count - 1]).sqrMagnitude < 4 ||
                !BattleOrders.RouteConnected(routeDraft[routeDraft.Count - 1], ground)))
                return Fail("Waypoints must be at least 2m apart and connected by a walkable route.");
            routeDraft.Add(ground); Report($"Waypoint {routeDraft.Count} / 8 added. Add more, or GO ROUTE / Enter to send troops."); return true;
        }
        public bool UndoRouteWaypoint()
        {
            if (!IsPlanningRoute || routeDraft.Count == 0) return false;
            routeDraft.RemoveAt(routeDraft.Count - 1); Report($"Route plan: {routeDraft.Count} / 8 waypoints. Current troop orders continue until GO ROUTE."); return true;
        }
        public bool StartTroopRoute()
        {
            if (!IsPlanningRoute || !Director.IsBattleRunning) return false;
            if (!TroopOrders.IssueRoute(routeDraft, out var reason)) return Fail(reason);
            CancelRoutePlanning(); Report("Route sent to autonomous friendly troops. They resume the siege after the last waypoint."); return true;
        }
        public void CancelTroopRoute()
        {
            if (IsPlanningRoute) { CancelRoutePlanning(); Report("Route plan canceled. Current troop orders retained."); }
            else if (TroopOrders != null && TroopOrders.Issue(BattleOrder.Advance)) Report("Route canceled. Troops resume advancing.");
        }
    }

    public sealed partial class PossessionController
    {
        internal bool IsSwitchingUnit => switchPending;
        void HandleRoutePrimary()
        {
            if (towerPrimaryHeld) return; towerPrimaryHeld = true;
            if (Physics.Raycast(AimRay, out var hit, 300, ~0, QueryTriggerInteraction.Ignore)) director.Towers.AddRouteWaypoint(hit.point);
        }
    }
}
