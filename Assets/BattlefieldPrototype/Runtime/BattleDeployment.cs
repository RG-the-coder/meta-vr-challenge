using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    /// <summary>Budgeted armies with full-map deployment and a shared per-battle power allowance.</summary>
    public sealed partial class BattleDeployment : MonoBehaviour
    {
        public const float MinimumSeparation = 1.3f;
        public int Budget { get; private set; } = 100;
        // The cheapest unit costs one Power. There is no separate army-size cap.
        public int MaxUnits => Budget;
        public string EnemyDoctrine { get; private set; } = "Shield wall";
        public int Spent
        {
            get
            {
                int value = 0;
                if (director != null)
                    foreach (BattleUnit unit in director.Units)
                        if (IsBlueUnit(unit)) value += GetCost(unit.Definition.Role);
                return value;
            }
        }
        public int Remaining => Mathf.Max(0, Budget - Spent);
        public int EnemySpent
        {
            get { int sum = 0; if (director != null) foreach (var unit in director.Units)
                if (unit != null && unit.IsAlive && unit.Team == BattleTeam.Red) sum += unit.Definition.DeploymentCost; return sum; }
        }
        public int Count
        {
            get
            {
                int count = 0;
                if (director != null)
                    foreach (BattleUnit unit in director.Units)
                        if (IsBlueUnit(unit)) count++;
                return count;
            }
        }
        public bool CanStart => IsEditing && Count > 0 && director.AliveRed > 0;
        public bool IsEditing => director != null && director.Phase == BattlePhase.Setup;
        public BattleUnit SelectedUnit { get; private set; }
        public string LastMessage { get; private set; } = "Build an army, then enter the battle.";
        public IReadOnlyList<BattleUnitDefinition> Definitions => definitions;
        public Vector2 ArenaSize => bootstrap != null ? bootstrap.ArenaSize : new Vector2(36, 44);
        public Bounds ArenaBounds => new Bounds(Vector3.zero, new Vector3(ArenaSize.x, 0, ArenaSize.y));
        public Bounds DeploymentBounds
        {
            get
            {
                return new Bounds(new Vector3(0, .9f, 0), new Vector3(ArenaSize.x - 4.4f, 1.8f, ArenaSize.y - 4.4f));
            }
        }
        public event Action Changed;

        BattleDirector director;
        BattlefieldBootstrap bootstrap;
        BattleUnitDefinition[] definitions = Array.Empty<BattleUnitDefinition>();
        int nextUnitId;

        internal void Initialize(BattleDirector battle, BattlefieldBootstrap owner, int budget)
        {
            director = battle;
            bootstrap = owner;
            Budget = NormalizeBudget(budget);
            definitions = owner.GetDefinitions();
            director.AttachDeployment(this);
        }

        public static int GetCost(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Vanguard: return 10;
                case UnitRole.Ranger: return 12;
                case UnitRole.Medic: return 14;
                case UnitRole.Skirmisher: return 8;
                case UnitRole.Brute: return 18;
                case UnitRole.Engineer: return 16;
                case UnitRole.Stormcaller: return 18;
                case UnitRole.Wisp: return 1;
                case UnitRole.Militia: return 3;
                case UnitRole.Hound: return 5;
                case UnitRole.Bombard: return 17;
                case UnitRole.Sentinel: return 24;
                case UnitRole.Titan: return 40;
                case UnitRole.Pyromancer: return 21;
                case UnitRole.Assassin: return 13;
                case UnitRole.Paladin: return 28;
                case UnitRole.Halberdier: return 15;
                case UnitRole.Duelist: return 14;
                case UnitRole.Frostbinder: return 23;
                case UnitRole.Alchemist: return 19;
                case UnitRole.WarDrummer: return 16;
                case UnitRole.Sharpshooter: return 22;
                case UnitRole.Berserker: return 20;
                case UnitRole.Warden: return 26;
                case UnitRole.Skywing: case UnitRole.Stormwing: case UnitRole.Dawnwing: case UnitRole.Emberwing: case UnitRole.Crownwing:
                    return BattleFlyingClasses.Cost(role);
                default: return 0;
            }
        }

        public static int NormalizeBudget(int value) => Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp(value, 10, 5000) / 10f) * 10, 10, 5000);

        public static int RollBudget(int minimum, int maximum, int previous)
        {
            int low = Mathf.Clamp(Mathf.CeilToInt(minimum / 50f), 1, 100);
            int high = Mathf.Clamp(Mathf.FloorToInt(maximum / 50f), low, 100);
            int count = high - low + 1;
            int index = UnityEngine.Random.Range(0, count);
            if (count > 1 && (low + index) * 50 == previous) index = (index + UnityEngine.Random.Range(1, count)) % count;
            return (low + index) * 50;
        }

        public void NewGame()
        {
            if (director == null || bootstrap == null) return;
            SelectedUnit = null;
            nextUnitId = 0;
            Budget = bootstrap.RandomizePowerBudget ? RollBudget(bootstrap.MinimumArmyPower, bootstrap.MaximumArmyPower, Budget)
                : NormalizeBudget(bootstrap.ArmyPowerBudget);
            LastMessage = "Choose a character, then place it on any clear battlefield ground.";
            director.ResetRound(BattlePhase.Setup, () =>
            {
                bootstrap.ClearArmies();
                bootstrap.PrepareBattlefield();
                SpawnRandomArmy(BattleTeam.Red);
            });
            Changed?.Invoke();
        }

        public void GoHome()
        {
            if (director == null || bootstrap == null) return;
            SelectedUnit = null;
            LastMessage = "Build your army. Take control. Win the battlefield.";
            director.ResetRound(BattlePhase.Home, () =>
            {
                bootstrap.ClearArmies();
                bootstrap.PrepareHomeBattlefield();
            });
            Changed?.Invoke();
        }

        public bool SelectUnit(BattleUnit unit)
        {
            if (!IsEditing) return false;
            if (unit != null && !OwnsBlueUnit(unit)) return Fail("Select one of your blue characters.");
            SelectedUnit = unit;
            LastMessage = unit == null ? "Choose a character to add." :
                $"{unit.Definition.DisplayName} selected. Move, change, or remove this character.";
            Changed?.Invoke();
            return true;
        }

        public bool TryPlace(UnitRole role, Vector3 worldPoint) => TryPlace(role, worldPoint, out _);

        public bool TryPlace(UnitRole role, Vector3 worldPoint, out BattleUnit placed)
        {
            placed = null;
            if (!CanEdit()) return false;
            BattleUnitDefinition definition = FindDefinition(role);
            if (definition == null) return Fail("Choose an available character.");
            if (GetCost(role) > Remaining) return Fail($"Not enough power: {definition.DisplayName} costs {GetCost(role)}.");
            if (!TryGetPlacement(worldPoint, null, definition, out Vector3 position, out string reason)) return Fail(reason);
            placed = bootstrap.CreateUnit(definition, BattleTeam.Blue, position, ++nextUnitId);
            SelectedUnit = placed;
            LastMessage = $"{definition.DisplayName} deployed. {Remaining} power remaining.";
            Physics.SyncTransforms();
            Changed?.Invoke();
            return true;
        }

        public bool TryMove(BattleUnit unit, Vector3 worldPoint)
        {
            if (!CanEdit()) return false;
            if (!OwnsBlueUnit(unit)) return Fail("Select one of your blue characters to move.");
            if (!TryGetPlacement(worldPoint, unit, out Vector3 position, out string reason)) return Fail(reason);
            CharacterController motor = unit.GetComponent<CharacterController>();
            bool motorEnabled = motor != null && motor.enabled;
            if (motor != null) motor.enabled = false;
            unit.transform.position = position;
            if (motor != null) motor.enabled = motorEnabled;
            SelectedUnit = unit;
            LastMessage = $"{unit.Definition.DisplayName} moved.";
            Physics.SyncTransforms();
            Changed?.Invoke();
            return true;
        }

        public bool TryReplace(BattleUnit unit, UnitRole role)
        {
            if (!CanEdit()) return false;
            if (!OwnsBlueUnit(unit)) return Fail("Select one of your blue characters to change.");
            BattleUnitDefinition definition = FindDefinition(role);
            if (definition == null) return Fail("Choose an available character.");
            int available = Remaining + GetCost(unit.Definition.Role);
            if (GetCost(role) > available) return Fail($"Not enough power to change this character to {definition.DisplayName}.");
            if (unit.Definition.Role == role) return SelectUnit(unit);
            if (!TryGetPlacement(unit.transform.position, unit, definition, out Vector3 position, out string reason)) return Fail(reason);
            DeleteUnit(unit);
            SelectedUnit = bootstrap.CreateUnit(definition, BattleTeam.Blue, position, ++nextUnitId);
            LastMessage = $"Changed to {definition.DisplayName}. {Remaining} power remaining.";
            Physics.SyncTransforms();
            Changed?.Invoke();
            return true;
        }

        public bool Remove(BattleUnit unit)
        {
            if (!CanEdit()) return false;
            if (!OwnsBlueUnit(unit)) return Fail("Select one of your blue characters to remove.");
            string displayName = unit.Definition.DisplayName;
            DeleteUnit(unit);
            LastMessage = $"{displayName} removed. Power refunded.";
            Physics.SyncTransforms();
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (!CanEdit()) return;
            ClearBlueUnits();
            LastMessage = "Army cleared. All power refunded.";
            Changed?.Invoke();
        }

        // Retained for existing scene bindings and callers of the former random-fill action.
        public bool RandomizeBlue() => MirrorEnemyArmy();

        public bool MirrorEnemyArmy()
        {
            if (!CanEdit()) return false;
            var mirror = new BattleArmyPreset { name = "Enemy mirror" };
            foreach (var enemy in director.Units)
                if (enemy != null && enemy.IsAlive && enemy.Team == BattleTeam.Red && enemy.Definition != null)
                    mirror.units.Add(new BattlePresetUnit
                    {
                        role = enemy.Definition.Role,
                        x = .5f - enemy.transform.position.x / ArenaSize.x,
                        z = .5f - enemy.transform.position.z / ArenaSize.y,
                        yaw = Mathf.Repeat(enemy.transform.eulerAngles.y + 180, 360)
                    });
            if (mirror.units.Count == 0) return Fail("There is no enemy army to copy. Your army was kept.");
            return TryApplyPreset(mirror, true);
        }

        /// <summary>Checks the actual snapped floor position without changing the army.</summary>
        public bool TryGetPlacement(Vector3 worldPoint, BattleUnit ignore, out Vector3 position, out string reason)
            => TryGetPlacement(worldPoint, ignore, ignore != null ? ignore.Definition : null, out position, out reason);

        bool TryGetPlacement(Vector3 worldPoint, BattleUnit ignore, BattleUnitDefinition definition, out Vector3 position, out string reason)
        {
            position = worldPoint;
            if (!IsEditing)
            {
                reason = "Characters can only be placed before battle.";
                return false;
            }
            if (float.IsNaN(worldPoint.sqrMagnitude) || float.IsInfinity(worldPoint.sqrMagnitude) || !InDeploymentZone(worldPoint))
            {
                reason = "Choose a clear position inside the battlefield.";
                return false;
            }
            if (!NavMesh.SamplePosition(worldPoint, out NavMeshHit hit, 0.65f, NavMesh.AllAreas) ||
                !InDeploymentZone(hit.position) || Mathf.Abs(hit.position.y) > 0.5f)
            {
                reason = "Choose clear, walkable battlefield ground.";
                return false;
            }
            position = hit.position;
            float radius = definition != null ? definition.BodyRadius : .42f;
            float height = definition != null ? definition.BodyHeight : 1.8f;
            foreach (BattleUnit unit in director.Units)
            {
                if (unit == null || unit == ignore || !unit.IsAlive) continue;
                Vector3 delta = unit.transform.position - position;
                delta.y = 0;
                float separation = Mathf.Max(MinimumSeparation, radius + unit.Definition.BodyRadius + .15f);
                if (delta.sqrMagnitude < separation * separation)
                {
                    reason = "Leave a little more room between characters.";
                    return false;
                }
            }
            Physics.SyncTransforms();
            foreach (Collider obstacle in Physics.OverlapCapsule(position + Vector3.up * (radius + .08f),
                position + Vector3.up * Mathf.Max(radius + .08f, height - radius), radius + .04f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (ignore != null && obstacle.GetComponentInParent<BattleUnit>() == ignore) continue;
                reason = "This spot is blocked. Choose clear ground.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        bool InDeploymentZone(Vector3 point)
        {
            Bounds bounds = DeploymentBounds;
            return point.x >= bounds.min.x && point.x <= bounds.max.x &&
                point.z >= bounds.min.z && point.z <= bounds.max.z;
        }

        bool CanEdit() => IsEditing || Fail("Start a new game to edit your army.");

        bool Fail(string message)
        {
            LastMessage = message;
            Changed?.Invoke();
            return false;
        }

        bool IsBlueUnit(BattleUnit unit) => unit != null && unit.Team == BattleTeam.Blue &&
            unit.IsAlive && unit.Definition != null;

        bool OwnsBlueUnit(BattleUnit unit)
        {
            if (!IsBlueUnit(unit)) return false;
            foreach (BattleUnit member in director.Units)
                if (unit == member) return true;
            return false;
        }

        BattleUnitDefinition FindDefinition(UnitRole role)
        {
            foreach (BattleUnitDefinition definition in definitions)
                if (definition != null && definition.Role == role) return definition;
            return null;
        }

        void DeleteUnit(BattleUnit unit)
        {
            if (SelectedUnit == unit) SelectedUnit = null;
            unit.SetPossessed(false);
            director.UnregisterUnit(unit);
            unit.gameObject.SetActive(false);
            Destroy(unit.gameObject);
        }

        void ClearBlueUnits()
        {
            var toRemove = new List<BattleUnit>();
            foreach (BattleUnit unit in director.Units)
                if (IsBlueUnit(unit)) toRemove.Add(unit);
            foreach (BattleUnit unit in toRemove) DeleteUnit(unit);
            SelectedUnit = null;
            Physics.SyncTransforms();
        }

        void SpawnRandomArmy(BattleTeam team)
        {
            List<Vector3> positions = bootstrap.BuildDeploymentPositions(team);
            int doctrine = UnityEngine.Random.Range(0, 4);
            if(UnityEngine.Random.value<.65f)
            {
                if(bootstrap.CurrentMapKind==BattleMapKind.RiverCrossing) doctrine=UnityEngine.Random.value<.5f?1:3;
                else if(bootstrap.CurrentMapKind==BattleMapKind.Woodland) doctrine=UnityEngine.Random.value<.5f?0:2;
                else if(bootstrap.CurrentMapKind==BattleMapKind.BlackstoneMaze || bootstrap.CurrentMapKind==BattleMapKind.ShatteredBazaar) doctrine=UnityEngine.Random.value<.5f?0:2;
                else if(bootstrap.CurrentMapKind==BattleMapKind.IronCitadel || bootstrap.CurrentMapKind==BattleMapKind.Ironworks || bootstrap.CurrentMapKind==BattleMapKind.SunlessCatacombs) doctrine=UnityEngine.Random.value<.5f?0:3;
            }
            if (team == BattleTeam.Red) EnemyDoctrine = BattleArmyPlanner.DoctrineName(doctrine);
            if (!BattleArmyPlanner.TryBuildStrategic(definitions, Budget, positions.Count, doctrine, out var roster))
            {
                LastMessage = "There is not enough clear space for an army using the full budget.";
                Debug.LogError(LastMessage, this);
                return;
            }
            Vector3 opposition = Vector3.zero;
            int opposingCount = 0;
            foreach (var other in director.Units)
                if (other != null && other.IsAlive && other.Team != team) { opposition += other.transform.position; opposingCount++; }
            if (opposingCount > 0) opposition /= opposingCount;
            var anchors=positions.FindAll(p=>Mathf.Abs(p.x)<ArenaSize.x*.5f-12 && Mathf.Abs(p.z)<ArenaSize.y*.5f-12 &&
                (opposingCount==0 || (p-opposition).sqrMagnitude>400));
            Vector3 anchor=anchors.Count>0?anchors[UnityEngine.Random.Range(0,anchors.Count)]:positions[UnityEngine.Random.Range(0,positions.Count)];
            Vector3 forward = Vector3.ProjectOnPlane(opposition - anchor, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = team == BattleTeam.Red ? Vector3.back : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            var coverScores=new List<float>();
            foreach(var p in positions)
            {
                float nearest=64;
                foreach(var cover in bootstrap.CoverFootprints) nearest=Mathf.Min(nearest,(cover.ClosestPoint(p)-p).sqrMagnitude);
                coverScores.Add(nearest);
            }
            for (int i = 0; i < roster.Count; i++)
            {
                var role = roster[i].Role;
                bool support = BattleClassCatalog.IsSupport(role);
                bool ranged = !roster[i].IsMelee;
                bool flanker = BattleClassCatalog.IsFlanker(role);
                float width = Mathf.Min(ArenaSize.x * .35f, Mathf.Max(10, Mathf.Sqrt(roster.Count) * 2.8f));
                float lateral = flanker ? (i%2==0?-1:1) * width*.8f : ((i%7)-3)*2.8f;
                Vector3 desired = anchor + right*lateral + forward*(support?-8:ranged?-4:5) - forward*(i/7)*2.7f;
                int best = 0; float bestScore = float.PositiveInfinity;
                for (int candidate = 0; candidate < positions.Count; candidate++)
                {
                    float score = (positions[candidate]-desired).sqrMagnitude + (support || ranged?coverScores[candidate]*.18f:0);
                    if (score < bestScore) { bestScore=score; best=candidate; }
                }
                var unit = bootstrap.CreateUnit(roster[i], team, positions[best], ++nextUnitId);
                unit.Face(forward);
                positions.RemoveAt(best);
                coverScores.RemoveAt(best);
            }
            Physics.SyncTransforms();
        }
    }
}
