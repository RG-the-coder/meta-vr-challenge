using UnityEngine;

namespace BattlefieldPrototype
{
    public readonly struct BattleAbilityInfo
    {
        public readonly string Name, Description, Hint;
        public BattleAbilityInfo(string name, string description, string hint)
        { Name = name; Description = description; Hint = hint; }
    }

    /// <summary>Shared class/ability copy for the chooser, HUD, and tutorials.</summary>
    public static class BattleClassCatalog
    {
        public const int MaxAbilities = 4;
        public static bool IsSupport(UnitRole role) => role==UnitRole.Medic || role==UnitRole.Engineer || role==UnitRole.Paladin ||
            role==UnitRole.Alchemist || role==UnitRole.WarDrummer || role==UnitRole.Warden || role==UnitRole.Dawnwing;
        public static bool IsFlanker(UnitRole role) => role==UnitRole.Assassin || role==UnitRole.Hound || role==UnitRole.Skirmisher || role==UnitRole.Duelist;
        public static int GetAbilityCount(UnitRole role)
        {
            if (BattleFlyingClasses.IsFlying(role)) return role <= UnitRole.Stormwing ? 1 : 2;
            switch (role)
            {
                case UnitRole.Wisp: case UnitRole.Militia: return 0;
                case UnitRole.Hound: case UnitRole.Bombard: case UnitRole.Sentinel: return 1;
                case UnitRole.Titan: case UnitRole.Pyromancer: case UnitRole.Assassin: return 3;
                case UnitRole.Paladin: return 4;
                case UnitRole.Frostbinder: case UnitRole.Warden: return 3;
                default: return 2;
            }
        }
        public static string Binding(int slot, bool xr = false)
        {
            if (xr) return slot == 0 ? "R GRIP" : slot == 1 ? "L TRIG" : slot == 2 ? "R CLICK" : "L CLICK";
            return slot == 0 ? "G / 1" : slot == 1 ? "H / 2" : slot == 2 ? "J / 3" : "K / 4";
        }
        public static string Specialty(UnitRole role)
        {
            if (BattleFlyingClasses.IsFlying(role)) return "FLYING / SUMMIT CAPTURE";
            if((int)role>=16) return SiegeBattleClasses.Specialty(role);
            if ((int)role >= 7) return ExpandedBattleClasses.Specialty(role);
            switch (role)
            {
                case UnitRole.Vanguard: return "FRONTLINE / DEFENSE";
                case UnitRole.Ranger: return "RANGED / PRECISION";
                case UnitRole.Medic: return "SUPPORT / HEALING";
                case UnitRole.Skirmisher: return "MOBILITY / RAPID FIRE";
                case UnitRole.Brute: return "HEAVY / CROWD CONTROL";
                case UnitRole.Engineer: return "SUPPORT / TEAM BOOSTS";
                default: return "CASTER / AREA CONTROL";
            }
        }

        public static string Description(UnitRole role)
        {
            if (BattleFlyingClasses.IsFlying(role)) return BattleFlyingClasses.Description(role);
            if((int)role>=16) return SiegeBattleClasses.Description(role);
            if ((int)role >= 7) return ExpandedBattleClasses.Description(role);
            switch (role)
            {
                case UnitRole.Vanguard: return "An armored frontline fighter. Close the distance, protect your health with guard, and push enemies away from your team.";
                case UnitRole.Ranger: return "A precise ranged fighter. Keep your distance, use Volley against groups, and line up a powerful shot against tougher enemies.";
                case UnitRole.Medic: return "Keep your team alive. Stay near allies, heal the wounded, and grant protective shields before enemies break through.";
                case UnitRole.Skirmisher: return "A fragile but fast repeater fighter. Overdrive accelerates your attacks; an evasive dash helps you escape close combat.";
                case UnitRole.Brute: return "A durable hammer fighter. Ground Slam punishes nearby groups; Seismic Wave sends a powerful shove ahead of you.";
                case UnitRole.Engineer: return "A battlefield technician with a rivet launcher. Repair and shield nearby allies, then overclock their movement and attacks.";
                default: return "A fragile lightning caster. Chain attacks through enemy groups, or release a pulse that damages, pushes, and slows nearby enemies.";
            }
        }

        public static Color Color(UnitRole role)
        {
            if (BattleFlyingClasses.IsFlying(role)) return BattleFlyingClasses.Color(role);
            if((int)role>=16) return SiegeBattleClasses.Color(role);
            if ((int)role >= 7) return ExpandedBattleClasses.Color(role);
            switch (role)
            {
                case UnitRole.Vanguard: return new Color(.95f, .72f, .25f);
                case UnitRole.Ranger: return new Color(.35f, .76f, 1f);
                case UnitRole.Medic: return new Color(.32f, .9f, .61f);
                case UnitRole.Skirmisher: return new Color(.22f, .9f, .91f);
                case UnitRole.Brute: return new Color(1f, .45f, .27f);
                case UnitRole.Engineer: return new Color(1f, .76f, .32f);
                default: return new Color(.72f, .53f, 1f);
            }
        }

        public static string Attack(UnitRole role)
        {
            if (BattleFlyingClasses.IsFlying(role)) return role == UnitRole.Emberwing ? "Cinder bolt" : "Airborne bolt";
            if((int)role>=16) return SiegeBattleClasses.Attack(role);
            if ((int)role >= 7) return ExpandedBattleClasses.Attack(role);
            switch (role)
            {
                case UnitRole.Vanguard: return "Melee strike";
                case UnitRole.Ranger: return "Straight arrow";
                case UnitRole.Medic: return "Support bolt";
                case UnitRole.Skirmisher: return "Rapid repeater";
                case UnitRole.Brute: return "Heavy hammer";
                case UnitRole.Engineer: return "Rivet launcher";
                default: return "Storm bolt";
            }
        }

        public static BattleAbilityInfo Ability(UnitRole role, int slot)
        {
            if (slot < 0 || slot >= GetAbilityCount(role)) return new BattleAbilityInfo("None", "This character has no power in this slot.", "Basic attacks");
            if (BattleFlyingClasses.IsFlying(role)) return BattleFlyingClasses.Ability(role, slot);
            if((int)role>=16) return SiegeBattleClasses.Ability(role,slot);
            if ((int)role >= 7) return ExpandedBattleClasses.Ability(role, slot);
            if (slot == 0)
            {
                switch (role)
                {
                    case UnitRole.Vanguard: return new BattleAbilityInfo("Shield Bash", "Bash enemies in front of you and gain 2.5 seconds of guard, reducing incoming damage.", "Bash + guard");
                    case UnitRole.Ranger: return new BattleAbilityInfo("Volley", "Fire three arrows in a spread. Aim at a group or use it up close for concentrated damage.", "Arrow spread");
                    case UnitRole.Medic: return new BattleAbilityInfo("Healing Pulse", "Restore health to yourself and nearby allies. Gather your team before using it.", "Heal allies");
                    case UnitRole.Skirmisher: return new BattleAbilityInfo("Overdrive", "Temporarily increase movement speed and fire your repeater more quickly.", "Fire + speed");
                    case UnitRole.Brute: return new BattleAbilityInfo("Ground Slam", "Damage and knock back nearby enemies on all sides. Solid cover blocks the slam.", "Area push");
                    case UnitRole.Engineer: return new BattleAbilityInfo("Repair Field", "Heal allies and give them a small protective shield that lasts six seconds.", "Heal + shield");
                    default: return new BattleAbilityInfo("Chain Lightning", "Strike the enemy you aim at, then arc to up to two more enemies nearby. Cover breaks the arc.", "Chain 3 foes");
                }
            }
            switch (role)
            {
                case UnitRole.Vanguard: return new BattleAbilityInfo("Bulwark", "Gain a 40-point shield for six seconds and guard for three seconds. Use it before taking heavy fire.", "Shield + guard");
                case UnitRole.Ranger: return new BattleAbilityInfo("Power Shot", "Fire one fast, long-range arrow that deals more than twice your normal attack damage.", "Heavy arrow");
                case UnitRole.Medic: return new BattleAbilityInfo("Protective Ward", "Grant yourself and nearby allies a 35-point shield for six seconds. Shields absorb damage before health.", "Shield allies");
                case UnitRole.Skirmisher: return new BattleAbilityInfo("Evasive Dash", "Dash in your aim direction along the ground. Walls stop the dash; it never teleports through cover.", "Ground dash");
                case UnitRole.Brute: return new BattleAbilityInfo("Seismic Wave", "Send a wave up to eight metres ahead, damaging and strongly pushing enemies in its cone.", "Cone + push");
                case UnitRole.Engineer: return new BattleAbilityInfo("Overclock Field", "Team overclock for four seconds: faster movement and shorter attack cooldowns.", "Team overclock");
                default: return new BattleAbilityInfo("Thunder Pulse", "Damage and push nearby enemies, slowing their movement by 40% for three seconds.", "Area slow");
            }
        }
    }
}
