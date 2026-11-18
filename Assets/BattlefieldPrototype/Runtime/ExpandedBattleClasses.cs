using UnityEngine;

namespace BattlefieldPrototype
{
    internal static class ExpandedBattleClasses
    {
        public static string Specialty(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Wisp: return "TINY / BUDGET FILLER / 0 POWERS";
                case UnitRole.Militia: return "LIGHT INFANTRY / 0 POWERS";
                case UnitRole.Hound: return "SMALL / PURSUIT / 1 POWER";
                case UnitRole.Bombard: return "ARTILLERY / AREA DAMAGE / 1 POWER";
                case UnitRole.Sentinel: return "ARMORED DEFENDER / 1 POWER";
                case UnitRole.Titan: return "GIANT / SIEGE / 3 POWERS";
                case UnitRole.Pyromancer: return "CASTER / BURN DAMAGE / 3 POWERS";
                case UnitRole.Assassin: return "MELEE / BURST / 3 POWERS";
                default: return "HEAVY SUPPORT / 4 POWERS";
            }
        }
        public static string Description(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Wisp: return "A tiny, fragile spark creature costing just 1 Power. It fires weak sparks automatically and has no special powers. Fill the last gaps in your budget.";
                case UnitRole.Militia: return "Affordable light infantry with a short-range crossbow. No special powers: rely on numbers, cover, and basic attacks to support stronger teammates.";
                case UnitRole.Hound: return "A small, fast hunting creature. Bite enemies at close range, or pounce forward to deal damage on contact. Fragile against concentrated fire.";
                case UnitRole.Bombard: return "A slow artillery fighter with a heavy launcher. Its explosive special hits a group at your aim point; solid cover protects enemies behind it.";
                case UnitRole.Sentinel: return "A large armored defender. Hold the front line with a mace, then use Fortress Guard to shield yourself and nearby allies before taking heavy fire.";
                case UnitRole.Titan: return "An enormous, expensive siege fighter with 650 health. Smash groups with Quake, absorb fire with Iron Skin, and scatter enemies with a slowing roar.";
                case UnitRole.Pyromancer: return "A fragile fire caster. Burst enemies at your aim point, burn close attackers with a nova, and brand a target for sustained fire damage.";
                case UnitRole.Assassin: return "A small, quick melee specialist. Dash into enemies, guard briefly while accelerating, then execute a wounded target. Stay away from heavy ranged fire.";
                default: return "A durable holy warrior with four powers. Smite enemies, heal nearby allies, rally your team, and unleash Judgment against groups. High cost, broad utility.";
            }
        }
        public static Color Color(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Wisp: return new Color(.55f, .95f, 1);
                case UnitRole.Militia: return new Color(.72f, .79f, .68f);
                case UnitRole.Hound: return new Color(.91f, .71f, .48f);
                case UnitRole.Bombard: return new Color(1, .65f, .3f);
                case UnitRole.Sentinel: return new Color(.55f, .73f, .94f);
                case UnitRole.Titan: return new Color(.95f, .50f, .35f);
                case UnitRole.Pyromancer: return new Color(1, .42f, .16f);
                case UnitRole.Assassin: return new Color(.76f, .62f, 1);
                default: return new Color(1, .88f, .56f);
            }
        }
        public static string Attack(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Wisp: return "Weak spark";
                case UnitRole.Militia: return "Crossbow bolt";
                case UnitRole.Hound: return "Quick bite";
                case UnitRole.Bombard: return "Heavy shell";
                case UnitRole.Sentinel: return "Mace strike";
                case UnitRole.Titan: return "Siege hammer";
                case UnitRole.Pyromancer: return "Fire bolt";
                case UnitRole.Assassin: return "Twin blades";
                default: return "Holy blade";
            }
        }
        public static BattleAbilityInfo Ability(UnitRole role, int slot)
        {
            switch (role)
            {
                case UnitRole.Hound: return Info("Pounce", "Dash toward your aim and bite enemies you touch. Each enemy is hit once; walls stop the pounce.", "Dash + bite");
                case UnitRole.Bombard: return Info("Mortar Burst", "Detonate an explosive at your aim point up to 18m away. Hits nearby enemies; cover blocks the blast.", "Aimed blast");
                case UnitRole.Sentinel: return Info("Fortress Guard", "Gain 70 shield and three seconds of guard. Nearby allies receive 30 shield for six seconds.", "Team defense");
                case UnitRole.Titan:
                    if (slot == 0) return Info("Quake", "Smash the ground, damaging and pushing enemies within six metres. Cover blocks the shockwave.", "Heavy area hit");
                    if (slot == 1) return Info("Iron Skin", "Gain 100 shield for eight seconds and guard for four seconds. Absorb fire while your team advances.", "Shield + guard");
                    return Info("Dread Roar", "Damage, push, and slow nearby enemies by 40% for three seconds. Solid cover blocks the roar.", "Push + slow");
                case UnitRole.Pyromancer:
                    if (slot == 0) return Info("Fireball", "Burst fire at your aim point. Deals area damage and burns enemies for three seconds; cover protects them.", "Blast + burn");
                    if (slot == 1) return Info("Flame Nova", "Burn and push nearby enemies on all sides. Useful against attackers who get too close.", "Area burn");
                    return Info("Fire Brand", "Brand the enemy under your aim for six damage per second over four seconds. Requires a clear shot.", "Target burn");
                case UnitRole.Assassin:
                    if (slot == 0) return Info("Dash Strike", "Dash forward and slash enemies on contact. Each target is hit once, and walls stop your movement.", "Dash + slash");
                    if (slot == 1) return Info("Shadow Guard", "Guard for two and a half seconds while gaining a brief movement and attack-speed boost.", "Guard + speed");
                    return Info("Execution", "Strike a close enemy under your aim. Deals bonus damage based on how much health the target is missing.", "Finish a foe");
                default:
                    if (slot == 0) return Info("Smite", "Strike enemies in a short frontal cone and gain two seconds of guard. Cover blocks the strike.", "Strike + guard");
                    if (slot == 1) return Info("Radiance", "Heal yourself and nearby allies for 40 health. Gather your team before using it.", "Team heal");
                    if (slot == 2) return Info("Rally", "Boost nearby allies' movement and attack speed for six seconds. Includes yourself.", "Team boost");
                    return Info("Judgment", "Deal 60 damage and strong knockback to enemies within six metres. Solid cover blocks the blast.", "Heavy area hit");
            }
        }
        static BattleAbilityInfo Info(string name, string description, string hint) => new BattleAbilityInfo(name, description, hint);
    }
}
