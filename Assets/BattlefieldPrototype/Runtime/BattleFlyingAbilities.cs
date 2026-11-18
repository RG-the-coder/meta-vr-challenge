using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class BattleUnit
    {
        void UseFlyingAbility(int slot, Vector3 origin, Vector3 direction)
        {
            if (slot == 0) GrantShield(BattleFlyingClasses.Ward(Definition.Role), 6);
            else if (Definition.Role == UnitRole.Dawnwing) SupportSiegeAllies(30, 20, 0);
            else if (Definition.Role == UnitRole.Emberwing)
            {
                Vector3 point = AimBlastPoint(origin, direction, Definition.AttackRange, out BattleTower tower);
                DamageArea(point, Definition.AbilityRadius, Definition.AbilityPower, 4, 3);
                if (tower != null) tower.TakeDamage(Definition.AbilityPower, Team);
                Director.GetComponent<BattleEffects>()?.Pulse(point, Definition.AbilityRadius, BattleFlyingClasses.Color(Definition.Role));
            }
            else { GrantShield(100, 8); guardRemaining = Mathf.Max(guardRemaining, 3); }
            Flash(BattleFlyingClasses.Color(Definition.Role), .25f);
        }
    }
}
