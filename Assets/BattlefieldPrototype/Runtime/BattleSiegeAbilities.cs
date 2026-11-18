using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class BattleUnit
    {
        bool UseSiegeAbility(int slot,Vector3 origin,Vector3 direction)
        {
            switch(Definition.Role)
            {
                case UnitRole.Halberdier:
                    if(slot==0) { GrantShield(35,6); guardRemaining=Mathf.Max(guardRemaining,3); }
                    else SiegeCone(direction,4.5f,Definition.AbilityPower,6,3);
                    break;
                case UnitRole.Duelist:
                    if(slot==0) { guardRemaining=Mathf.Max(guardRemaining,2); ReceiveOverdrive(3,1.2f,.65f); }
                    else BeginDash(direction,.27f,11,Definition.AbilityPower);
                    break;
                case UnitRole.Frostbinder:
                    if(slot==0)
                    {
                        if(!HasClearAttackOrigin(origin) || !TraceTarget(origin,direction,16,.22f,out var frozen)) return false;
                        frozen.TakeDamage(Definition.AbilityPower,this,direction,2); frozen.slowRemaining=Mathf.Max(frozen.slowRemaining,5);
                        Director.GetComponent<BattleEffects>()?.Lightning(origin,frozen.AimPosition);
                    }
                    else if(slot==1) DamageArea(EyePosition,Definition.AbilityRadius,16,5,0,true);
                    else GrantShield(45,6);
                    break;
                case UnitRole.Alchemist:
                    if(slot==0)
                    {
                        if(!HasClearAttackOrigin(origin)) return false;
                        var target=AimBlastPoint(origin,direction,12);
                        DamageArea(target,3.5f,Definition.AbilityPower,3,4);
                        Director.GetComponent<BattleEffects>()?.Pulse(target,3.5f,BattleClassCatalog.Color(Definition.Role));
                    }
                    else SupportSiegeAllies(32,0,0);
                    break;
                case UnitRole.WarDrummer:
                    if(slot==0) SupportSiegeAllies(0,0,6);
                    else SupportSiegeAllies(0,28,0);
                    break;
                case UnitRole.Sharpshooter:
                    if(slot==0)
                    { if(!HasClearAttackOrigin(origin)) return false; FireProjectile(origin,direction,Definition.Damage*3,1.3f,1.3f); }
                    else BeginDash(-direction,.32f,9,0);
                    break;
                case UnitRole.Berserker:
                    if(slot==0) { ReceiveOverdrive(5,1.4f,.55f); guardRemaining=Mathf.Max(guardRemaining,1.5f); }
                    else DamageArea(EyePosition,Definition.AbilityRadius,Definition.AbilityPower,7);
                    break;
                case UnitRole.Warden:
                    if(slot==0) SiegeCone(direction,6,Definition.AbilityPower,3,5);
                    else if(slot==1) SupportSiegeAllies(0,45,0);
                    else SupportSiegeAllies(45,0,0);
                    break;
                default: return false;
            }
            Director.GetComponent<BattleEffects>()?.Pulse(transform.position,Definition.AbilityRadius,BattleClassCatalog.Color(Definition.Role));
            return true;
        }
        void SupportSiegeAllies(float heal,float shield,float tempo)
        {
            foreach(var ally in Director.Units)
            {
                if(!NearbyAlly(ally,Definition.AbilityRadius) || !ClearArc(EyePosition,ally.AimPosition)) continue;
                if(heal>0) ally.Heal(heal);
                if(shield>0) ally.GrantShield(shield,7);
                if(tempo>0) ally.ReceiveOverdrive(tempo,1.25f,.7f);
            }
        }
        void SiegeCone(Vector3 direction,float range,float damage,float force,float slow)
        {
            Vector3 forward=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
            if(forward.sqrMagnitude<.01f) forward=transform.forward;
            foreach(var other in Director.Units)
            {
                if(other==null || !other.IsAlive || other.Team==Team) continue;
                Vector3 delta=Vector3.ProjectOnPlane(other.transform.position-transform.position,Vector3.up);
                if(delta.sqrMagnitude>range*range || Vector3.Dot(forward,delta.normalized)<.45f || !HasLineOfSight(other)) continue;
                other.TakeDamage(damage,this,delta,force); other.slowRemaining=Mathf.Max(other.slowRemaining,slow);
            }
        }
    }
}
