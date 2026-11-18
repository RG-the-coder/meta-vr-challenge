using UnityEngine;

namespace BattlefieldPrototype
{
    /// <summary>Flight tiers are shared by recruitment, possession, AI and capture.</summary>
    public static class BattleFlyingClasses
    {
        public static bool IsFlying(UnitRole role) => role >= UnitRole.Skywing && role <= UnitRole.Crownwing;
        public static float Ceiling(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Skywing: return 9;
                case UnitRole.Stormwing: return 15;
                case UnitRole.Dawnwing: return 21;
                case UnitRole.Emberwing: return 27;
                case UnitRole.Crownwing: return 36;
                default: return 0;
            }
        }
        public static int Cost(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Skywing: return 25;
                case UnitRole.Stormwing: return 40;
                case UnitRole.Dawnwing: return 60;
                case UnitRole.Emberwing: return 85;
                case UnitRole.Crownwing: return 120;
                default: return 0;
            }
        }
        public static float Ward(UnitRole role) => role == UnitRole.Crownwing ? 90 : role == UnitRole.Stormwing ? 55 : 35;
        public static Color Color(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Skywing: return new Color(.35f, .9f, .9f);
                case UnitRole.Stormwing: return new Color(.7f, .6f, 1);
                case UnitRole.Dawnwing: return new Color(.5f, 1, .65f);
                case UnitRole.Emberwing: return new Color(1, .43f, .18f);
                default: return new Color(1, .82f, .35f);
            }
        }
        public static UnitRole ModelRole(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Skywing: return UnitRole.Ranger;
                case UnitRole.Stormwing: return UnitRole.Stormcaller;
                case UnitRole.Dawnwing: return UnitRole.Medic;
                case UnitRole.Emberwing: return UnitRole.Pyromancer;
                case UnitRole.Crownwing: return UnitRole.Paladin;
                default: return role;
            }
        }
        public static string Description(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Skywing: return "A fast, affordable raider with a 9m ceiling. Attack taller towers until they are low enough to raid. Wind Ward shields its approach.";
                case UnitRole.Stormwing: return "A tougher raider with a 15m ceiling and a stronger Wind Ward. Capture watchtowers and starting keeps, but damage taller towers first.";
                case UnitRole.Dawnwing: return "A 21m support flyer. Restoration Draft heals and shields nearby allies. Escort a raid or keep your formation alive from above.";
                case UnitRole.Emberwing: return "A 27m siege flyer. Cinder Burst burns exposed enemies and damages the first enemy tower in its path. A costly answer to tall defenses.";
                default: return "An armored 36m royal raider that can reach every tower. Royal Aegis grants heavy shields and guard. Expensive and slower than lighter flyers.";
            }
        }
        public static BattleAbilityInfo Ability(UnitRole role, int slot)
        {
            if (slot == 0) return new BattleAbilityInfo("Wind Ward", $"Gain {Ward(role):0} shield for six seconds. Incoming hits still interrupt a summit raid.", "Personal shield");
            switch (role)
            {
                case UnitRole.Dawnwing: return new BattleAbilityInfo("Restoration Draft", "Heal nearby visible allies for 30 health and grant 20 shield. Walls block the support field.", "Heal + shield allies");
                case UnitRole.Emberwing: return new BattleAbilityInfo("Cinder Burst", "Blast your aim point, burning exposed enemies and damaging an enemy tower on the direct line. Terrain blocks the blast.", "Siege blast + burn");
                default: return new BattleAbilityInfo("Royal Aegis", "Gain 100 shield for eight seconds and three seconds of guard. Incoming hits still interrupt capture.", "Heavy shield + guard");
            }
        }
    }
}
