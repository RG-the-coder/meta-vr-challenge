using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class BattleUnitAI
    {
        // Ground attackers can break an obstructing player wall rather than wait at a sealed route.
        bool ActOnDefense()
        {
            var battle = director.Towers;
            if (unit.Definition.IsFlying || battle.Defenses.Count == 0) return false;
            Vector3 goal;
            if (enemy != null && enemy.IsAlive && enemyVisible) goal = enemy.transform.position;
            else
            {
                var tower = battle.Objective(unit); if (tower == null) return false;
                goal = tower.transform.position;
            }
            Vector3 forward = Vector3.ProjectOnPlane(goal - transform.position, Vector3.up).normalized;
            BattleDefense target = null; float nearest = Mathf.Max(12, unit.Definition.AttackRange);
            foreach (var defense in battle.Defenses)
            {
                if (defense == null || !defense.IsActive || !defense.IsBarricade || defense.Owner == unit.Team) continue;
                Vector3 offset = Vector3.ProjectOnPlane(defense.transform.position - transform.position, Vector3.up);
                if (Vector3.Dot(forward, offset.normalized) < .35f) continue;
                float distance = Vector3.Distance(unit.EyePosition, defense.AttackPoint(unit.EyePosition));
                if (distance >= nearest || !defense.HasClearShot(unit.EyePosition)) continue;
                target = defense; nearest = distance;
            }
            if (target == null) return false;
            Vector3 point = target.AttackPoint(unit.EyePosition), aim = point - unit.EyePosition;
            unit.Face(aim); IsPursuing = true; State = BattleTacticalState.Pursuing;
            if (aim.magnitude <= unit.Definition.AttackRange * .85f)
            {
                unit.Move(Vector3.zero); unit.TryAttack(unit.EyePosition, aim); State = BattleTacticalState.Fighting;
            }
            else
            {
                Vector3 away = Vector3.ProjectOnPlane(transform.position - point, Vector3.up).normalized;
                if (away.sqrMagnitude < .01f) away = -forward;
                TravelTo(point + away * (unit.Definition.BodyRadius + .9f));
            }
            return true;
        }
    }
}
