using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class BattleUnit
    {
        void OnDestroy() { if (Director != null) Director.UnregisterUnit(this); }
        void TryMeleeTower(Vector3 origin, Vector3 direction, float radius)
        {
            if (Director.Towers == null) return;
            var hits = Physics.SphereCastAll(origin, radius, direction, Definition.AttackRange, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var unit = hit.collider.GetComponentInParent<BattleUnit>();
                if (unit == this || unit != null && (!unit.IsAlive || unit.Team == Team)) continue;
                var tower = hit.collider.GetComponentInParent<BattleTower>();
                if (tower != null) tower.TakeDamage(Definition.Damage, Team);
                var defense = hit.collider.GetComponentInParent<BattleDefense>();
                if (defense != null) defense.TakeDamage(Definition.Damage, Team);
                return; // Terrain and intervening fighters stop a strike.
            }
        }
    }

    public sealed partial class BattleUnitAI
    {
        BattleTower towerObjective;
        float nextTowerChoice;
        bool ActOnTower()
        {
            var siege = director.Towers;
            if (unit.Team == BattleTeam.Blue && orders != null && !orders.IsAdvancing(unit)) return false;
            if (towerObjective == null || Time.time >= nextTowerChoice || towerObjective.Owner == unit.Team && !towerObjective.IsBroken)
            { towerObjective = siege.Objective(unit); nextTowerChoice = Time.time + 2; }
            if (towerObjective == null) return false;
            State = BattleTacticalState.Pursuing; IsPursuing = true;
            Vector3 offset = Vector3.ProjectOnPlane(unit.transform.position - towerObjective.transform.position, Vector3.up);
            Vector3 radial = offset.sqrMagnitude > .01f ? offset.normalized : Vector3.back;
            if (towerObjective.CanBeRaidedBy(unit.Definition))
            {
                unit.FlightAltitude = towerObjective.transform.position.y + towerObjective.Height;
                Vector3 station = towerObjective.transform.position + radial * 5;
                TravelTo(station);
                if (offset.magnitude < 7)
                {
                    unit.TryAbility(0, unit.EyePosition, transform.forward);
                    if (unit.Definition.Role == UnitRole.Dawnwing || unit.Definition.Role == UnitRole.Crownwing)
                        unit.TryAbility(1, unit.EyePosition, transform.forward);
                }
                // Hold the summit instead of destroying the height we are trying to steal.
                return true;
            }
            if (unit.Definition.IsFlying) unit.FlightAltitude = towerObjective.IsBroken ? 2 : Mathf.Max(3, towerObjective.Height);
            if (towerObjective.IsBroken)
            {
                if (offset.magnitude > 7) TravelTo(towerObjective.transform.position + radial * 6);
                else
                {
                    unit.Move(Vector3.zero);
                    string message = siege.Message;
                    towerObjective.TryBuild(unit.Team);
                    if (unit.Team == BattleTeam.Red) siege.Report(message);
                }
                return true;
            }
            Vector3 target = towerObjective.AttackPoint(unit.EyePosition);
            Vector3 aim = target - unit.EyePosition;
            unit.Face(aim);
            float reach = unit.Definition.AttackRange;
            if (aim.magnitude <= reach * .85f && TowerAttackClear(target))
            {
                unit.Move(Vector3.zero); unit.TryAttack(unit.EyePosition, aim);
                if (unit.Definition.Role == UnitRole.Emberwing) unit.TryAbility(1, unit.EyePosition, aim);
                State = BattleTacticalState.Fighting;
            }
            else
            {
                // Stay outside the carved foundation; the path endpoint must be on ground.
                float standOff = BattleTower.Radius + (unit.IsMelee ? 1.15f : Mathf.Min(7, reach * .5f));
                TravelTo(towerObjective.transform.position + radial * standOff);
            }
            return true;
        }
        bool TowerAttackClear(Vector3 target)
        {
            Vector3 delta = target - unit.EyePosition;
            foreach (var hit in Physics.RaycastAll(unit.EyePosition, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<BattleUnit>() != null) continue;
                if (hit.collider.GetComponentInParent<BattleTower>() != towerObjective) return false;
            }
            return true;
        }
    }
}
