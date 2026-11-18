using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    public sealed partial class BattleOrders
    {
        public const int MaximumWaypoints = 8;
        public int Revision { get; private set; }
        public IReadOnlyList<Vector3> Waypoints => waypoints;
        readonly List<Vector3> waypoints = new List<Vector3>();
        readonly Dictionary<BattleUnit, int> routeProgress = new Dictionary<BattleUnit, int>();
        readonly Dictionary<BattleUnit, int> routeSlots = new Dictionary<BattleUnit, int>();
        void ClearRouteData() { waypoints.Clear(); routeProgress.Clear(); routeSlots.Clear(); }
        public int RouteIndex(BattleUnit unit) => unit != null && routeProgress.TryGetValue(unit, out int index) ? index : 0;
        public bool IsAdvancing(BattleUnit unit) => Current == BattleOrder.Advance || Current == BattleOrder.Route &&
            unit != null && routeProgress.TryGetValue(unit, out int index) && index >= waypoints.Count;

        public bool TryValidateWaypoint(Vector3 point, out Vector3 ground, out string reason)
        {
            ground = point; reason = "Choose clear, walkable battlefield ground for the waypoint.";
            if (director == null || !float.IsFinite(point.sqrMagnitude) || Mathf.Abs(point.y) > .4f) return false;
            var terrain = director.GetComponent<BattlefieldBootstrap>();
            if (terrain != null && (Mathf.Abs(point.x) > terrain.ArenaSize.x * .5f - 2 || Mathf.Abs(point.z) > terrain.ArenaSize.y * .5f - 2)) return false;
            if (!NavMesh.SamplePosition(point, out var nav, .65f, NavMesh.AllAreas) || Mathf.Abs(nav.position.y) > .4f) return false;
            ground = nav.position; Physics.SyncTransforms();
            foreach (var collider in Physics.OverlapCapsule(ground + Vector3.up * .5f, ground + Vector3.up * 1.5f, .35f, ~0, QueryTriggerInteraction.Ignore))
                if (collider.GetComponentInParent<BattleUnit>() == null) return false;
            reason = ""; return true;
        }
        internal static bool RouteConnected(Vector3 from, Vector3 to, float startRadius = 1.5f)
        {
            from.y = 0; to.y = 0;
            if (!NavMesh.SamplePosition(from, out var a, startRadius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out var b, .8f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
        public bool IssueRoute(IReadOnlyList<Vector3> points, out string reason)
        {
            reason = "Plan between 1 and 8 walkable waypoints first.";
            if (director == null || !director.IsBattleRunning || points == null || points.Count == 0 || points.Count > MaximumWaypoints) return false;
            var validated = new List<Vector3>(points.Count);
            foreach (var point in points)
            {
                if (!TryValidateWaypoint(point, out var ground, out reason)) return false;
                if (validated.Count > 0 && ((ground - validated[validated.Count - 1]).sqrMagnitude < 4 || !RouteConnected(validated[validated.Count - 1], ground)))
                { reason = "Keep waypoints at least 2m apart and connected by a ground route."; return false; }
                validated.Add(ground);
            }
            int count = 0;
            foreach (var unit in director.Units)
            {
                if (!CanFollowRoute(unit)) continue;
                if (!RouteConnected(unit.transform.position, validated[0], unit.Definition.IsFlying ? 8 : 1.5f))
                { reason = "A friendly troop cannot reach the first waypoint. Choose another approach."; return false; }
                count++;
            }
            if (count == 0) { reason = "Deploy friendly troops and release possession before routing them."; return false; }
            ClearRouteData(); waypoints.AddRange(validated); heldPositions.Clear(); ProtectedUnit = null;
            foreach (var unit in director.Units) if (CanFollowRoute(unit)) { routeProgress[unit] = 0; routeSlots[unit] = routeSlots.Count % 9; }
            Anchor = waypoints[waypoints.Count - 1]; Current = BattleOrder.Route; Revision++; reason = ""; return true;
        }
        static bool CanFollowRoute(BattleUnit unit) => unit != null && unit.IsAlive && unit.Team == BattleTeam.Blue && !unit.IsPossessed;
        Vector3 RouteDestination(BattleUnit unit)
        {
            int index = RouteIndex(unit);
            if (waypoints.Count == 0) return Anchor;
            Vector3 point = waypoints[Mathf.Min(index, waypoints.Count - 1)];
            if (unit == null) return point;
            int slot = routeSlots.TryGetValue(unit, out int assigned) ? assigned : 0;
            Vector3 offset = new Vector3(slot % 3 - 1, 0, slot / 3 - 1) * 1.1f;
            return NavMesh.SamplePosition(point + offset, out var nav, .65f, NavMesh.AllAreas) ? nav.position : point;
        }
        public bool TryGetRouteDestination(BattleUnit unit, out Vector3 destination)
        {
            destination = default;
            if (Current != BattleOrder.Route || !CanFollowRoute(unit) || waypoints.Count == 0) return false;
            if (!routeProgress.ContainsKey(unit)) { routeProgress[unit] = 0; routeSlots[unit] = routeSlots.Count % 9; }
            while (routeProgress[unit] < waypoints.Count)
            {
                destination = RouteDestination(unit);
                Vector3 delta = Vector3.ProjectOnPlane(destination - unit.transform.position, Vector3.up);
                if (delta.sqrMagnitude > Mathf.Pow(Mathf.Max(1.5f, unit.Definition.BodyRadius + .7f), 2)) return true;
                routeProgress[unit]++;
            }
            FinishRouteIfComplete(); return false;
        }
        void FinishRouteIfComplete()
        {
            if (Current != BattleOrder.Route) return;
            foreach (var unit in director.Units)
                if (CanFollowRoute(unit) && (!routeProgress.TryGetValue(unit, out int index) || index < waypoints.Count)) return;
            Current = BattleOrder.Advance; ClearRouteData(); Revision++;
        }
    }
    public sealed partial class BattleUnitAI
    {
        int lastRouteRevision = -1;
        bool ActOnRoute()
        {
            if (orders == null || unit.Team != BattleTeam.Blue || orders.Current != BattleOrder.Route) return false;
            if (!orders.TryGetRouteDestination(unit, out var destination)) return false;
            if (lastRouteRevision != orders.Revision) { pathRefresh = 0; lastRouteRevision = orders.Revision; }
            bool attacking = enemy != null && enemy.IsAlive && enemyVisible &&
                Vector3.Distance(unit.EyePosition, enemy.TargetPointFor(unit)) <= unit.Definition.AttackRange * .9f;
            if (attacking)
            { Vector3 aim = enemy.TargetPointFor(unit) - unit.EyePosition; unit.Face(aim); unit.TryAttack(unit.EyePosition, aim); }
            State = BattleTacticalState.Pursuing; IsPursuing = true; TravelTo(destination, !attacking);
            return true;
        }
    }
}
