using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    public enum DefenseKind { TimberBarricade, StoneBarricade, SpikeBarricade, Caltrops, BlastMine }

    public static class DefenseCatalog
    {
        public const int Count = 5;
        public static bool IsValid(DefenseKind kind) => (int)kind >= 0 && (int)kind < Count;
        public static bool IsBarricade(DefenseKind kind) => IsValid(kind) && (int)kind < 3;
        public static string Name(DefenseKind kind) => kind == DefenseKind.TimberBarricade ? "Timber barricade" :
            kind == DefenseKind.StoneBarricade ? "Stone barricade" : kind == DefenseKind.SpikeBarricade ? "Spiked barricade" :
            kind == DefenseKind.Caltrops ? "Caltrops" : kind == DefenseKind.BlastMine ? "Blast mine" : "Unknown defense";
        public static int Cost(DefenseKind kind) => kind == DefenseKind.TimberBarricade ? 8 : kind == DefenseKind.StoneBarricade ? 18 :
            kind == DefenseKind.SpikeBarricade ? 12 : kind == DefenseKind.Caltrops ? 6 : kind == DefenseKind.BlastMine ? 14 : int.MaxValue;
        public static float Health(DefenseKind kind) => kind == DefenseKind.TimberBarricade ? 180 : kind == DefenseKind.StoneBarricade ? 480 :
            kind == DefenseKind.SpikeBarricade ? 240 : 0;
        public static Vector3 Size(DefenseKind kind) => kind == DefenseKind.TimberBarricade ? new Vector3(5.4f, 2, .7f) :
            kind == DefenseKind.StoneBarricade ? new Vector3(5.4f, 3, 1.1f) : kind == DefenseKind.SpikeBarricade ? new Vector3(5.4f, 1.3f, 1.5f) :
            kind == DefenseKind.Caltrops ? new Vector3(3.4f, .16f, 3.4f) : new Vector3(1.6f, .16f, 1.6f);
        public static string Description(DefenseKind kind) => kind == DefenseKind.TimberBarricade ? "Cheap cover: 180 HP. Blocks movement and arrows." :
            kind == DefenseKind.StoneBarricade ? "Heavy cover: 480 HP. A tall wall that blocks sight and arrows." :
            kind == DefenseKind.SpikeBarricade ? "240 HP. Enemy ground troops touching it take 8 damage/s." :
            kind == DefenseKind.Caltrops ? "Reusable: slows enemy ground troops to 60% speed for 3s." :
            "One use: 90 damage to enemy ground troops within 4m. Cover blocks the blast.";
    }

    public sealed partial class TowerBattle
    {
        public const int MaximumDefenses = 48;
        public IReadOnlyList<BattleDefense> Defenses => defenses;
        public DefenseKind? SelectedDefense { get; private set; }
        public float DefenseRotation { get; private set; }
        public bool CanConstruct => Director != null && Director.IsBattleRunning && Player != null &&
            Player.CurrentUnit == null && !Player.IsSwitchingUnit && OwnedCount(BattleTeam.Blue) > 0;
        readonly List<BattleDefense> defenses = new List<BattleDefense>();
        readonly RaycastHit[] defenseAimHits = new RaycastHit[64];
        internal bool TryDefenseAimPoint(Ray ray, out Vector3 point)
        {
            point = default; float closest = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(ray, defenseAimHits, 300, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = defenseAimHits[i];
                // A commander looking down from the summit must be able to aim
                // past their own tower. Other obstacles still block placement.
                if (IsMounted && BoundTower != null && hit.collider.GetComponentInParent<BattleTower>() == BoundTower) continue;
                if (hit.distance >= closest) continue;
                closest = hit.distance; point = hit.point;
            }
            return float.IsFinite(closest);
        }
        public static float DefenseBuildRadius(BattleTower tower) => tower != null && !tower.IsBroken ? Mathf.Max(60, tower.DeploymentRadius * 2.5f) : 0;
        public bool IsInDefenseBuildZone(Vector3 point)
        {
            foreach (var tower in towers)
                if (tower.Owner == BattleTeam.Blue && !tower.IsBroken &&
                    Vector3.ProjectOnPlane(point - tower.transform.position, Vector3.up).sqrMagnitude <= Mathf.Pow(DefenseBuildRadius(tower), 2)) return true;
            return false;
        }

        void ClearDefenses() { defenses.Clear(); SelectedDefense = null; DefenseRotation = 0; }
        internal void ForgetDefense(BattleDefense defense) => defenses.Remove(defense);
        public bool ChooseDefense(DefenseKind? kind)
        {
            if (kind.HasValue && (!CanConstruct || !DefenseCatalog.IsValid(kind.Value)))
                return Fail("Return to your tower to build a defense.");
            CancelRoutePlanning();
            SelectedDefense = kind; Recruitment = null; SelectedSummitPower = null;
            Report(kind.HasValue ? DefenseCatalog.Name(kind.Value) + " selected. Point at clear ground in the battlefield and pinch to place. MENU opens rotate/cancel controls."
                : "Defense placement canceled.");
            return true;
        }
        public bool RotateDefense()
        {
            if (!CanConstruct || !SelectedDefense.HasValue) return false;
            DefenseRotation = Mathf.Repeat(DefenseRotation + 45, 360);
            Report(DefenseCatalog.Name(SelectedDefense.Value) + $" rotated to {DefenseRotation:0} degrees."); return true;
        }

        public bool TryPlaceDefense(DefenseKind kind, Vector3 point, float angle, out BattleDefense defense)
        {
            defense = null;
            if (!CanConstruct) return Fail("Return to your tower to build a defense.");
            if (defenses.Count >= MaximumDefenses) return Fail("Defense limit reached (48). Existing barricades can be destroyed and mines are consumed when triggered.");
            if (!TryDefensePlacement(kind, point, angle, out var position, out var tower, out var reason)) return Fail(reason);
            if (!TrySpend(BattleTeam.Blue, DefenseCatalog.Cost(kind))) return Fail("Not enough gold for " + DefenseCatalog.Name(kind) + ".");
            var obj = new GameObject(DefenseCatalog.Name(kind)); obj.transform.SetParent(towerRoot, false);
            obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0, Mathf.Repeat(angle, 360), 0));
            defense = obj.AddComponent<BattleDefense>();
            defense.Initialize(this, kind, BattleTeam.Blue, tower, masonry, metal);
            defenses.Add(defense); Physics.SyncTransforms(); BattleDirector.InvalidateSightPhysics();
            Report(DefenseCatalog.Name(kind) + " built for " + DefenseCatalog.Cost(kind) + " gold. Selection stays active; each press builds once.");
            return true;
        }

        public bool TryDefensePlacement(DefenseKind kind, Vector3 point, float angle, out Vector3 position, out BattleTower tower, out string reason)
        {
            position = point; tower = null; reason = "Choose clear, flat ground in an owned standing tower's gold build zone.";
            if (!DefenseCatalog.IsValid(kind) || !float.IsFinite(point.sqrMagnitude) || !float.IsFinite(angle) || Mathf.Abs(point.y) > .4f) return false;
            Quaternion rotation = Quaternion.Euler(0, Mathf.Repeat(angle, 360), 0);
            Vector3 size = DefenseCatalog.Size(kind), half = size * .5f;
            var corners = new Vector3[4];
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = point + rotation * new Vector3((i < 2 ? -1 : 1) * (half.x + .2f), 0, (i % 2 == 0 ? -1 : 1) * (half.z + .2f));
                if (Mathf.Abs(corners[i].x) > Terrain.ArenaSize.x * .5f - 1.6f || Mathf.Abs(corners[i].z) > Terrain.ArenaSize.y * .5f - 1.6f)
                { reason = "Keep the entire defense inside the battlefield."; return false; }
            }
            foreach (var candidate in towers)
            {
                if (candidate.Owner != BattleTeam.Blue || candidate.IsBroken) continue;
                float radius = DefenseBuildRadius(candidate); bool inside = true;
                foreach (var corner in corners)
                    if (Vector3.ProjectOnPlane(corner - candidate.transform.position, Vector3.up).sqrMagnitude > radius * radius) { inside = false; break; }
                if (inside) { tower = candidate; break; }
            }
            if (tower == null) return false;
            foreach (var other in towers)
                if (FootprintDistance(other.transform.position, point, rotation, half) < 7)
                { reason = "Leave at least 7m clear around every tower foundation."; return false; }
            foreach (var unit in Director.Units)
                if (unit != null && unit.IsAlive && unit.transform.position.y < size.y + 1.5f &&
                    FootprintDistance(unit.transform.position, point, rotation, half) < unit.Definition.BodyRadius + .3f)
                { reason = "Do not build on characters. Choose unoccupied ground."; return false; }
            if (DefenseCatalog.IsBarricade(kind) && Player != null &&
                FootprintDistance(Player.ViewCamera.transform.position, point, rotation, half) < .6f)
            { reason = "Move clear of the barricade footprint before building."; return false; }
            Physics.SyncTransforms();
            var overlaps = Physics.OverlapBox(point + Vector3.up * (half.y + .08f), half + new Vector3(.15f, 0, .15f), rotation, ~0, QueryTriggerInteraction.Collide);
            foreach (var collider in overlaps)
                if (!collider.isTrigger || collider.GetComponentInParent<BattleDefense>() != null)
                { reason = "The footprint overlaps terrain, cover or another defense."; return false; }
            if (!NavMesh.SamplePosition(point, out var nav, .3f, NavMesh.AllAreas) || Mathf.Abs(nav.position.y) > .3f) return false;
            foreach (var corner in corners)
            {
                if (!Physics.Raycast(corner + Vector3.up, Vector3.down, out var ground, 1.5f, ~0, QueryTriggerInteraction.Ignore) ||
                    ground.normal.y < .95f || Mathf.Abs(ground.point.y) > .2f || ground.collider.GetComponentInParent<BattleDefense>() != null ||
                    ground.collider.GetComponentInParent<BattleTower>() != null || ground.collider.GetComponentInParent<BattleUnit>() != null)
                { reason = "Support the whole footprint on flat battlefield ground."; return false; }
            }
            position.y = 0; reason = ""; return true;
        }

        internal static float FootprintDistance(Vector3 target, Vector3 point, Quaternion rotation, Vector3 half)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (target - point);
            return new Vector2(Mathf.Max(0, Mathf.Abs(local.x) - half.x), Mathf.Max(0, Mathf.Abs(local.z) - half.z)).magnitude;
        }
    }
}
