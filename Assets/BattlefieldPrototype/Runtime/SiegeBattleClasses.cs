using UnityEngine;

namespace BattlefieldPrototype
{
    internal static class SiegeBattleClasses
    {
        public static string Specialty(UnitRole role)
        {
            switch(role)
            {
                case UnitRole.Halberdier: return "REACH / CHOKEPOINT DEFENSE";
                case UnitRole.Duelist: return "FAST MELEE / RIPOSTE";
                case UnitRole.Frostbinder: return "ICE / MOVEMENT CONTROL";
                case UnitRole.Alchemist: return "CORROSION / FIELD MEDICINE";
                case UnitRole.WarDrummer: return "FORMATION / TEAM TEMPO";
                case UnitRole.Sharpshooter: return "LONG RANGE / HIGH PRECISION";
                case UnitRole.Berserker: return "ASSAULT / CROWD BREAKER";
                default: return "NATURE / DEFENSIVE SUPPORT";
            }
        }
        public static string Description(UnitRole role)
        {
            switch(role)
            {
                case UnitRole.Halberdier: return "A long-reach polearm fighter. Brace before an enemy charge, then sweep the doorway to damage and slow attackers. Strong in narrow passages.";
                case UnitRole.Duelist: return "A quick rapier fighter with a buckler. Riposte grants brief guard and faster attacks; Lunge closes the gap with a collision-aware thrust.";
                case UnitRole.Frostbinder: return "An ice mage who controls approaches. Slow a distant enemy with Ice Lance, repel nearby enemies with Frozen Ring, and survive counterfire with Crystal Ward.";
                case UnitRole.Alchemist: return "A masked field chemist. Throw corrosive flasks that damage and burn groups, then restore nearby allies with healing mist. Walls block both mixtures.";
                case UnitRole.WarDrummer: return "A formation support soldier carrying a war drum. Accelerate the whole company with Battle Rhythm and shield allies with Iron Cadence. Keep close to the front line.";
                case UnitRole.Sharpshooter: return "A fragile long-range marksman with a scoped rifle. Line up a powerful Marked Shot; use Backstep to escape a charging enemy. Tall cover breaks every shot.";
                case UnitRole.Berserker: return "An aggressive axe fighter. Blood Rush boosts movement and attack speed, while Whirlwind clears nearby enemies. Strong assaults still need cover and allied support.";
                default: return "An armored grove guardian with a branch staff. Snare approaching enemies, shield nearby allies with Living Aegis, and heal with Renewal. Cover blocks nature's reach.";
            }
        }
        public static Color Color(UnitRole role)
        {
            switch(role)
            {
                case UnitRole.Halberdier: return new Color(.79f,.82f,.94f);
                case UnitRole.Duelist: return new Color(1,.67f,.79f);
                case UnitRole.Frostbinder: return new Color(.45f,.88f,1);
                case UnitRole.Alchemist: return new Color(.71f,.93f,.23f);
                case UnitRole.WarDrummer: return new Color(.96f,.75f,.48f);
                case UnitRole.Sharpshooter: return new Color(.65f,.85f,.75f);
                case UnitRole.Berserker: return new Color(1,.30f,.25f);
                default: return new Color(.37f,.86f,.43f);
            }
        }
        public static string Attack(UnitRole role)
        {
            switch(role)
            {
                case UnitRole.Halberdier: return "Halberd thrust";
                case UnitRole.Duelist: return "Rapier strike";
                case UnitRole.Frostbinder: return "Ice shard";
                case UnitRole.Alchemist: return "Chemical bolt";
                case UnitRole.WarDrummer: return "Rhythm bolt";
                case UnitRole.Sharpshooter: return "Rifle shot";
                case UnitRole.Berserker: return "War axe";
                default: return "Thorn bolt";
            }
        }
        public static BattleAbilityInfo Ability(UnitRole role,int slot)
        {
            switch(role)
            {
                case UnitRole.Halberdier: return slot==0?Info("Brace","Gain 35 shield for six seconds and guard for three seconds. Hold the opening against a charge.","Brace + shield"):
                    Info("Sweeping Hook","Sweep a 4.5m frontal cone for 36 damage, knockback and three seconds of slow. Walls block the sweep.","Reach + slow");
                case UnitRole.Duelist: return slot==0?Info("Riposte","Guard for two seconds and gain faster movement and attacks for three seconds.","Guard + tempo"):
                    Info("Lunge","Dash toward your aim and deal 24 damage to each enemy touched once. Walls stop the thrust.","Dash + thrust");
                case UnitRole.Frostbinder:
                    if(slot==0) return Info("Ice Lance","Strike a clearly aimed enemy up to 16m away for 25 damage and five seconds of slow.","Target slow");
                    if(slot==1) return Info("Frozen Ring","Damage and slow enemies within 4.5m, with a short push. Solid cover blocks the frost.","Area slow");
                    return Info("Crystal Ward","Gain a 45-point shield for six seconds.","Personal shield");
                case UnitRole.Alchemist: return slot==0?Info("Corrosive Flask","Burst a flask at your aim point within 12m. Damage nearby enemies and burn them for four seconds. Cover blocks the mixture.","Blast + corrosion"):
                    Info("Restorative Mist","Restore 32 health to allies within 4.5m, including yourself. Solid walls block the mist.","Local team heal");
                case UnitRole.WarDrummer: return slot==0?Info("Battle Rhythm","Nearby allies move 25% faster and attack more quickly for six seconds. Solid walls block the rhythm.","Team tempo"):
                    Info("Iron Cadence","Give yourself and nearby allies 28 shield for seven seconds. Solid walls block the effect.","Formation shield");
                case UnitRole.Sharpshooter: return slot==0?Info("Marked Shot","Fire a fast, extended-range shot for triple normal damage. The projectile stops at solid cover.","Precision shot"):
                    Info("Backstep","Dash backwards from your aim to make room against a charging enemy. Walls stop the dash.","Retreat dash");
                case UnitRole.Berserker: return slot==0?Info("Blood Rush","Surge for five seconds with faster movement and attacks; gain brief guard at the start.","Assault tempo"):
                    Info("Whirlwind","Deal 38 damage and push enemies within 3.5m. Solid cover blocks the swing.","Area axe strike");
                default:
                    if(slot==0) return Info("Root Snare","Damage enemies in a six-metre frontal cone and slow them for five seconds. Walls block the roots.","Cone snare");
                    if(slot==1) return Info("Living Aegis","Give allies within five metres 45 shield for seven seconds. Requires a clear path through the grove.","Team shield");
                    return Info("Renewal","Heal yourself and nearby allies for 45 health. Solid walls block the healing.","Team restoration");
            }
        }
        static BattleAbilityInfo Info(string name,string description,string hint) => new BattleAbilityInfo(name,description,hint);
    }
}
