using System.Collections.Generic;
using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class BattleUnit
    {
        float dashSpeed = 7, dashDamage;
        readonly List<BattleUnit> dashVictims = new List<BattleUnit>();
        float burnRemaining, burnDamagePerSecond;
        float burnTickRemaining, pendingBurnDamage;
        BattleUnit burnSource;
        public bool IsBurning => burnRemaining > 0;

        void ResetExpandedState()
        {
            dashDamage = burnRemaining = burnDamagePerSecond = 0;
            dashSpeed = 7;
            burnSource = null;
            burnTickRemaining = pendingBurnDamage = 0;
            dashVictims.Clear();
        }

        void BeginDash(Vector3 direction, float duration, float speed, float damage)
        {
            dashDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (dashDirection.sqrMagnitude < .01f) dashDirection = transform.forward;
            dashRemaining = duration; dashSpeed = speed; dashDamage = damage;
            dashVictims.Clear();
        }

        void DamageDashContacts()
        {
            if (dashDamage <= 0) return;
            foreach (var other in Director.Units)
            {
                if (other == null || !other.IsAlive || other.Team == Team || dashVictims.Contains(other)) continue;
                float reach = Definition.BodyRadius + other.Definition.BodyRadius + .75f;
                if ((other.transform.position - transform.position).sqrMagnitude > reach * reach || !HasLineOfSight(other)) continue;
                dashVictims.Add(other);
                other.TakeDamage(dashDamage, this, dashDirection, 5);
            }
        }

        void UpdateBurn()
        {
            if (!IsBurning || Director == null || !Director.IsBattleRunning) return;
            float step = Mathf.Min(Time.deltaTime, burnRemaining);
            burnRemaining = Mathf.Max(0, burnRemaining - step);
            pendingBurnDamage += burnDamagePerSecond * step;
            burnTickRemaining -= step;
            if (burnTickRemaining <= 0 || burnRemaining <= 0)
            {
                float damage = pendingBurnDamage;
                pendingBurnDamage = 0;
                burnTickRemaining = .5f;
                TakeDamage(damage, burnSource, Vector3.zero, 0);
            }
        }

        void Burn(BattleUnit victim, float duration, float damagePerSecond)
        {
            if (victim == null || !victim.IsAlive || victim.Team == Team) return;
            if (!victim.IsBurning) { victim.burnTickRemaining = .5f; victim.pendingBurnDamage = 0; }
            victim.burnRemaining = Mathf.Max(victim.burnRemaining, duration);
            victim.burnDamagePerSecond = Mathf.Max(victim.burnDamagePerSecond, damagePerSecond);
            victim.burnSource = this;
        }

        bool UseExpandedAbility(int slot, Vector3 origin, Vector3 direction)
        {
            if((int)Definition.Role>=(int)UnitRole.Halberdier) return UseSiegeAbility(slot,origin,direction);
            var role = Definition.Role;
            var color = BattleClassCatalog.Color(role);
            if (role == UnitRole.Hound || role == UnitRole.Assassin && slot == 0)
            {
                BeginDash(direction, role == UnitRole.Hound ? .30f : .32f, 10, role == UnitRole.Hound ? 20 : 32);
                Director.GetComponent<BattleEffects>()?.Pulse(transform.position, .65f, color);
                return true;
            }
            if (role == UnitRole.Bombard || role == UnitRole.Pyromancer && slot == 0)
            {
                if (!HasClearAttackOrigin(origin)) return false;
                float range = role == UnitRole.Bombard ? 18 : 13;
                Vector3 target = AimBlastPoint(origin, direction, range);
                DamageArea(target, role == UnitRole.Bombard ? 3.3f : 3, Definition.AbilityPower, 5,
                    role == UnitRole.Pyromancer ? 3 : 0);
                Director.GetComponent<BattleEffects>()?.Impact(target, Vector3.up, color);
                Director.GetComponent<BattleEffects>()?.Pulse(new Vector3(target.x, transform.position.y, target.z), 3, color);
                return true;
            }
            switch (role)
            {
                case UnitRole.Sentinel:
                    foreach (var other in Director.Units) if (NearbyAlly(other, Definition.AbilityRadius)) other.GrantShield(other == this ? 70 : 30, 6);
                    guardRemaining = 3;
                    break;
                case UnitRole.Titan:
                    if (slot == 0) DamageArea(transform.position + Vector3.up, 6, Definition.AbilityPower, 10);
                    else if (slot == 1) { GrantShield(100, 8); guardRemaining = 4; }
                    else DamageArea(transform.position + Vector3.up, 8, 10, 10, 0, true);
                    break;
                case UnitRole.Pyromancer:
                    if (slot == 1) DamageArea(EyePosition, Definition.AbilityRadius, 22, 5, 3);
                    else
                    {
                        if (!HasClearAttackOrigin(origin) || !TraceTarget(origin, direction, 13, .22f, out var branded)) return false;
                        Burn(branded, 4, 6);
                        branded.TakeDamage(8, this, direction, 2);
                        Director.GetComponent<BattleEffects>()?.Lightning(origin + direction * .35f, branded.AimPosition);
                    }
                    break;
                case UnitRole.Assassin:
                    if (slot == 1) { guardRemaining = 2.5f; ReceiveOverdrive(2.5f, 1.3f, .7f); }
                    else
                    {
                        if (!HasClearAttackOrigin(origin) || !TraceTarget(origin, direction, Definition.AttackRange + 1, .35f, out var victim)) return false;
                        victim.TakeDamage(Definition.Damage * 1.6f + (1 - victim.HealthFraction) * 40, this, direction, 5);
                    }
                    break;
                case UnitRole.Paladin:
                    if (slot == 0) { DamageCone(direction, 3, Definition.AbilityPower, 5); guardRemaining = 2; }
                    else if (slot == 1) { foreach (var other in Director.Units) if (NearbyAlly(other, Definition.AbilityRadius)) other.Heal(40); }
                    else if (slot == 2) { foreach (var other in Director.Units) if (NearbyAlly(other, Definition.AbilityRadius)) other.ReceiveOverdrive(6, 1.25f, .8f); }
                    else DamageArea(EyePosition, 6, 60, 8);
                    break;
                default: return false;
            }
            Director.GetComponent<BattleEffects>()?.Pulse(transform.position,
                role == UnitRole.Assassin ? .7f : Definition.AbilityRadius, color);
            return true;
        }

        Vector3 AimBlastPoint(Vector3 origin, Vector3 direction, float range)
            => AimBlastPoint(origin, direction, range, out _);

        Vector3 AimBlastPoint(Vector3 origin, Vector3 direction, float range, out BattleTower tower)
        {
            tower = null;
            Vector3 point = origin + direction * range;
            float distance = range;
            foreach (var hit in Physics.RaycastAll(origin, direction, range, ~0, QueryTriggerInteraction.Ignore))
            {
                var unit = hit.collider.GetComponentInParent<BattleUnit>();
                if (unit == this || unit != null && (!unit.IsAlive || unit.Team == Team) || hit.distance >= distance) continue;
                distance = hit.distance;
                point = hit.point - direction * .06f;
                tower = hit.collider.GetComponentInParent<BattleTower>();
            }
            return point;
        }

        void DamageArea(Vector3 center, float radius, float damage, float force, float burn = 0, bool slow = false)
        {
            foreach (var other in Director.Units)
            {
                if (other == null || !other.IsAlive || other.Team == Team || (other.AimPosition - center).sqrMagnitude > radius * radius ||
                    !ClearArc(center, other.AimPosition)) continue;
                other.TakeDamage(damage, this, other.transform.position - center, force);
                if (burn > 0) Burn(other, burn, 6);
                if (slow && other.IsAlive) other.slowRemaining = Mathf.Max(other.slowRemaining, 3);
            }
        }

        void DamageCone(Vector3 direction, float range, float damage, float force)
        {
            Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = transform.forward;
            foreach (var other in Director.Units)
            {
                if (other == null || !other.IsAlive || other.Team == Team) continue;
                Vector3 offset = Vector3.ProjectOnPlane(other.transform.position - transform.position, Vector3.up);
                if (offset.sqrMagnitude <= range * range && Vector3.Dot(forward, offset.normalized) >= .5f && HasLineOfSight(other))
                    other.TakeDamage(damage, this, offset, force);
            }
        }
    }
}
