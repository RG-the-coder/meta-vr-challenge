using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattle
    {
        // Number of summon cards, not a limit on living units.
        public const int SquadSize = 5;
        static readonly UnitRole[] squadRoles = {
            UnitRole.Vanguard, UnitRole.Vanguard, UnitRole.Ranger, UnitRole.Ranger, UnitRole.Medic
        };
        readonly BattleUnit[,] squads = new BattleUnit[2, SquadSize];
        readonly bool[,] summoned = new bool[2, SquadSize];
        public static IReadOnlyList<UnitRole> SquadRoles { get; } = Array.AsReadOnly(squadRoles);
        public BattleUnit SquadMember(BattleTeam team, int slot) => slot >= 0 && slot < SquadSize ? squads[(int)team, slot] : null;
        public bool WasSummoned(BattleTeam team, int slot) => slot >= 0 && slot < SquadSize && summoned[(int)team, slot];
        public int SummonCost(int slot) => slot >= 0 && slot < SquadSize ? Definition(squadRoles[slot]).DeploymentCost : int.MaxValue;
        public int SummonedCount(BattleTeam team)
        { int count = 0; for (int i = 0; i < SquadSize; i++) if (summoned[(int)team, i]) count++; return count; }

        void ResetSquadSummoning()
        { Array.Clear(squads, 0, squads.Length); Array.Clear(summoned, 0, summoned.Length); }

        bool FindSummonPosition(BattleTeam team, BattleTower tower, UnitRole role, out Vector3 position)
        {
            position = default;
            if (tower == null || tower.IsBroken || tower.Owner != team) return false;
            var opponent = team == BattleTeam.Blue ? EnemyBoundTower : BoundTower;
            if (opponent == null) return false;
            Vector3 facing = Vector3.ProjectOnPlane(opponent.transform.position - tower.transform.position, Vector3.up).normalized;
            for (int ring = 0; ring < 8; ring++) for (int step = 0; step < 48; step++)
            {
                float angle = (step % 2 == 0 ? 1 : -1) * Mathf.Ceil(step / 2f) * 7.5f;
                var candidate = tower.transform.position + Quaternion.Euler(0, angle, 0) * facing * (8 + ring * 3);
                if (TryPlacement(tower, Definition(role), candidate, out position, out _)) return true;
            }
            return false;
        }

        BattleUnit CreateSummonedMember(BattleTeam team, int slot, BattleTower tower, Vector3 position)
        {
            var unit = Terrain.CreateUnit(Definition(squadRoles[slot]), team, position, ++nextId);
            unit.SpawnTower = tower; squads[(int)team, slot] = unit; summoned[(int)team, slot] = true;
            Physics.SyncTransforms(); return unit;
        }

        bool TryEnemyReinforcement()
        {
            if (!Director.IsBattleRunning) return false;
            int guards = 0, archers = 0, healers = 0;
            foreach (var member in Director.Units)
            {
                if (member == null || !member.IsAlive || member.Team != BattleTeam.Red) continue;
                if (member.Definition.Role == UnitRole.Vanguard) guards++;
                else if (member.Definition.Role == UnitRole.Ranger) archers++;
                else if (member.Definition.Role == UnitRole.Medic) healers++;
            }
            // Establish a front line, then ranged pressure and support. Replace
            // the missing role after casualties instead of waiting for a player purchase.
            int slot = guards == 0 ? 0 : archers == 0 ? 2 : healers * 4 < guards + archers ? 4 : guards <= archers ? 1 : 3;
            int cost = SummonCost(slot);
            if (RedGold < cost || !FindSummonPosition(BattleTeam.Red, EnemyBoundTower, squadRoles[slot], out var point)) return false;
            if (!TrySpend(BattleTeam.Red, cost)) return false;
            CreateSummonedMember(BattleTeam.Red, slot, EnemyBoundTower, point);
            return true;
        }

        public bool TrySummonSlot(int slot, out BattleUnit unit)
        {
            unit = null;
            if (!Director.IsBattleRunning || slot < 0 || slot >= SquadSize || Player.IsSwitchingUnit) return false;
            int cost = SummonCost(slot);
            if (BlueGold + .0001f < cost) return Fail("Need " + cost + " gold. Your towers keep collecting gold while you play.");
            if (!FindSummonPosition(BattleTeam.Blue, BoundTower, squadRoles[slot], out var point))
                return Fail("No clear summoning space at this tower. Try again when the area is clear.");
            if (!TrySpend(BattleTeam.Blue, cost)) return false;
            CancelRoutePlanning(); Recruitment = null; SelectedDefense = null; SelectedSummitPower = null;
            unit = CreateSummonedMember(BattleTeam.Blue, slot, BoundTower, point);
            if (Director.AliveRed < Director.AliveBlue &&
                RedGold >= cost && FindSummonPosition(BattleTeam.Red, EnemyBoundTower, squadRoles[slot], out var enemyPoint) && TrySpend(BattleTeam.Red, cost))
                CreateSummonedMember(BattleTeam.Red, slot, EnemyBoundTower, enemyPoint);
            Report(unit.Definition.DisplayName + " summoned for " + cost + " gold. " + Director.AliveBlue + " allies alive. Summon again whenever you can afford it.");
            return true;
        }

        public bool CommandSquad(BattleOrder order)
        {
            if (!Director.IsBattleRunning || TroopOrders == null) return false;
            bool issued = TroopOrders.IssueForPlayer(order, Player);
            if (issued) Report(order == BattleOrder.Advance ? "Summoned allies advancing and fighting automatically." :
                order == BattleOrder.Hold ? "Squad holding position. ADVANCE sends them forward again." : "Squad guarding an ally.");
            return issued;
        }
    }
}
