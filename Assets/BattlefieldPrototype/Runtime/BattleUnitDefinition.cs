using UnityEngine;

namespace BattlefieldPrototype
{
    public enum BattleTeam { Blue, Red }
    // Append roles so existing serialized balance assets retain their meaning.
    public enum UnitRole { Vanguard = 0, Ranger = 1, Medic = 2, Skirmisher = 3, Brute = 4, Engineer = 5, Stormcaller = 6,
        Wisp = 7, Militia = 8, Hound = 9, Bombard = 10, Sentinel = 11, Titan = 12, Pyromancer = 13, Assassin = 14, Paladin = 15,
        Halberdier = 16, Duelist = 17, Frostbinder = 18, Alchemist = 19, WarDrummer = 20, Sharpshooter = 21, Berserker = 22, Warden = 23,
        Skywing = 24, Stormwing = 25, Dawnwing = 26, Emberwing = 27, Crownwing = 28 }

    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "Battlefield Prototype/Unit Definition")]
    public sealed class BattleUnitDefinition : ScriptableObject
    {
        public string DisplayName = "Vanguard";
        public UnitRole Role = UnitRole.Vanguard;
        public int DeploymentCost => BattleDeployment.GetCost(Role);
        [Min(1f)] public float MaxHealth = 160f;
        [Min(0.1f)] public float MoveSpeed = 4.8f;
        [Min(0f)] public float Damage = 22f;
        [Min(0.1f)] public float AttackRange = 2.2f;
        [Min(0.05f)] public float AttackCooldown = 0.9f;
        [Min(0.1f)] public float AbilityCooldown = 8f;
        [Min(0.1f)] public float SecondaryAbilityCooldown = 12f;
        [Min(0.1f)] public float ThirdAbilityCooldown = 16f;
        [Min(0.1f)] public float FourthAbilityCooldown = 22f;
        [Range(.4f, 1.65f)] public float BodyScale = 1f;
        public int AbilityCount => BattleClassCatalog.GetAbilityCount(Role);
        public bool IsFlying => BattleFlyingClasses.IsFlying(Role);
        [Min(0f), Tooltip("Maximum flight altitude in metres. Zero uses the class flight tier.")]
        public float FlightCeiling;
        public float MaximumFlightHeight => IsFlying ? Mathf.Clamp(FlightCeiling > 0 ? FlightCeiling : BattleFlyingClasses.Ceiling(Role), 1, BattleTower.MaximumHeight) : 0;
        public float BodyRadius => .42f * Mathf.Clamp(BodyScale, .4f, 1.65f);
        public float BodyHeight => (Role == UnitRole.Hound ? 1.25f : 1.8f) * BodyScale;
        public float EyeHeight => (Role == UnitRole.Hound ? .98f : 1.55f) * BodyScale;
        public bool IsMelee => Role == UnitRole.Vanguard || Role == UnitRole.Brute || Role == UnitRole.Hound ||
            Role == UnitRole.Sentinel || Role == UnitRole.Titan || Role == UnitRole.Assassin || Role == UnitRole.Paladin ||
            Role == UnitRole.Halberdier || Role == UnitRole.Duelist || Role == UnitRole.Berserker;
        // Shared by possession and AI; authored assets keep their relative balance.
        public const float MovementTempo = 1.4f;
        public const float AttackTempo = .65f;
        public const float PowerTempo = .75f;
        public const float ProjectileTempo = 1.5f;
        public float CombatMoveSpeed => MoveSpeed * MovementTempo;
        public float CombatAttackCooldown => AttackCooldown * AttackTempo;
        public float CombatProjectileSpeed => ProjectileSpeed * ProjectileTempo;
        [Range(90f, 170f)] public float SightAngle = 130f;
        public float SightMapFraction => Role == UnitRole.Sharpshooter ? .6f :
            Role == UnitRole.Ranger || Role == UnitRole.Hound || Role == UnitRole.Assassin ? .48f :
            Role == UnitRole.Skirmisher || Role == UnitRole.Duelist || Role == UnitRole.Bombard ||
            Role == UnitRole.Stormcaller || Role == UnitRole.Frostbinder ? .4f : 1f / 3f;
        public float GetAbilityCooldown(int slot)
        {
            switch (slot) { case 0: return Mathf.Max(.1f, AbilityCooldown * PowerTempo); case 1: return Mathf.Max(.1f, SecondaryAbilityCooldown * PowerTempo);
                case 2: return Mathf.Max(.1f, ThirdAbilityCooldown * PowerTempo); case 3: return Mathf.Max(.1f, FourthAbilityCooldown * PowerTempo); default: return 0; }
        }
        [Range(0f, 0.85f)] public float ArmorFraction = 0.2f;
        [Min(0.1f)] public float ProjectileSpeed = 18f;
        [Min(0f)] public float AbilityPower = 28f;
        [Min(0.1f)] public float AbilityRadius = 2.8f;
        [Min(0f)] public float RetreatDistance = 0f;

        public static BattleUnitDefinition CreateDefault(UnitRole role)
        {
            var definition = CreateInstance<BattleUnitDefinition>();
            definition.hideFlags = HideFlags.DontSave;
            definition.Role = role;
            definition.DisplayName = role.ToString();
            definition.name = role + " (Runtime)";

            switch (role)
            {
                case UnitRole.Ranger:
                    definition.MaxHealth = 90f;
                    definition.MoveSpeed = 5.85f;
                    definition.Damage = 14f;
                    definition.AttackRange = 14f;
                    definition.AttackCooldown = 0.85f;
                    definition.AbilityCooldown = 6f;
                    definition.ArmorFraction = 0f;
                    definition.ProjectileSpeed = 23f;
                    definition.AbilityPower = 14f;
                    definition.AbilityRadius = 0.12f;
                    definition.RetreatDistance = 4.5f;
                    break;
                case UnitRole.Medic:
                    definition.MaxHealth = 105f;
                    definition.MoveSpeed = 5.25f;
                    definition.Damage = 9f;
                    definition.AttackRange = 10f;
                    definition.AttackCooldown = 1.1f;
                    definition.AbilityCooldown = 5f;
                    definition.ArmorFraction = 0.05f;
                    definition.ProjectileSpeed = 18f;
                    definition.AbilityPower = 32f;
                    definition.AbilityRadius = 5.5f;
                    definition.RetreatDistance = 3.5f;
                    break;
                case UnitRole.Skirmisher:
                    definition.MaxHealth = 80f;
                    definition.MoveSpeed = 6.75f;
                    definition.Damage = 8f;
                    definition.AttackRange = 11f;
                    definition.AttackCooldown = 0.28f;
                    definition.AbilityCooldown = 8f;
                    definition.ArmorFraction = 0f;
                    definition.ProjectileSpeed = 27f;
                    definition.AbilityPower = 4f; // Overdrive duration in seconds.
                    definition.AbilityRadius = 1.35f; // Overdrive movement multiplier.
                    definition.RetreatDistance = 4f;
                    break;
                case UnitRole.Brute:
                    definition.MaxHealth = 240f;
                    definition.MoveSpeed = 3.9f;
                    definition.Damage = 36f;
                    definition.AttackRange = 2.6f;
                    definition.AttackCooldown = 1.35f;
                    definition.AbilityCooldown = 9f;
                    definition.ArmorFraction = 0.25f;
                    definition.ProjectileSpeed = 18f;
                    definition.AbilityPower = 40f;
                    definition.AbilityRadius = 3.4f;
                    definition.RetreatDistance = 0f;
                    break;
                case UnitRole.Engineer:
                    definition.MaxHealth = 120f;
                    definition.MoveSpeed = 4.65f;
                    definition.Damage = 10f;
                    definition.AttackRange = 12f;
                    definition.AttackCooldown = .8f;
                    definition.AbilityCooldown = 8f;
                    definition.SecondaryAbilityCooldown = 12f;
                    definition.ArmorFraction = .12f;
                    definition.ProjectileSpeed = 21f;
                    definition.AbilityPower = 22f;
                    definition.AbilityRadius = 4.5f;
                    definition.RetreatDistance = 3f;
                    break;
                case UnitRole.Stormcaller:
                    definition.MaxHealth = 85f;
                    definition.MoveSpeed = 5.25f;
                    definition.Damage = 13f;
                    definition.AttackRange = 13f;
                    definition.AttackCooldown = .95f;
                    definition.AbilityCooldown = 7f;
                    definition.SecondaryAbilityCooldown = 11f;
                    definition.ArmorFraction = 0f;
                    definition.ProjectileSpeed = 24f;
                    definition.AbilityPower = 26f;
                    definition.AbilityRadius = 4.5f;
                    definition.RetreatDistance = 4f;
                    break;
                case UnitRole.Wisp:
                    Balance(definition, 18, 7.2f, 4, 6, .95f, .45f, 0, 2); break;
                case UnitRole.Militia:
                    Balance(definition, 55, 5.4f, 8, 8, 1, .83f, 0, 2.5f); break;
                case UnitRole.Hound:
                    Balance(definition, 65, 9.3f, 9, 1.5f, .55f, .7f, 0, 0);
                    definition.AbilityCooldown = 6; definition.AbilityRadius = 2.3f; break;
                case UnitRole.Bombard:
                    Balance(definition, 95, 4.05f, 24, 16, 1.8f, 1.05f, .05f, 5);
                    definition.AbilityCooldown = 10; definition.AbilityPower = 42; definition.AbilityRadius = 3.3f; break;
                case UnitRole.Sentinel:
                    Balance(definition, 320, 3.3f, 23, 2.8f, 1.35f, 1.2f, .35f, 0);
                    definition.AbilityCooldown = 11; definition.AbilityRadius = 4; break;
                case UnitRole.Titan:
                    Balance(definition, 650, 2.85f, 58, 3.8f, 1.65f, 1.6f, .3f, 0);
                    definition.AbilityCooldown = 12; definition.SecondaryAbilityCooldown = 16;
                    definition.ThirdAbilityCooldown = 18; definition.AbilityPower = 65; definition.AbilityRadius = 6; break;
                case UnitRole.Pyromancer:
                    Balance(definition, 90, 5.1f, 12, 12, .9f, .95f, 0, 4);
                    definition.AbilityCooldown = 7; definition.SecondaryAbilityCooldown = 10;
                    definition.ThirdAbilityCooldown = 13; definition.AbilityPower = 32; definition.AbilityRadius = 4; break;
                case UnitRole.Assassin:
                    Balance(definition, 75, 8.25f, 22, 1.8f, .6f, .85f, .05f, 0);
                    definition.AbilityCooldown = 6; definition.SecondaryAbilityCooldown = 10;
                    definition.ThirdAbilityCooldown = 12; definition.AbilityRadius = 2.8f; break;
                case UnitRole.Paladin:
                    Balance(definition, 350, 4.2f, 26, 2.7f, 1, 1.18f, .22f, 0);
                    definition.AbilityCooldown = 7; definition.SecondaryAbilityCooldown = 10;
                    definition.ThirdAbilityCooldown = 15; definition.FourthAbilityCooldown = 22;
                    definition.AbilityPower = 36; definition.AbilityRadius = 5; break;
                case UnitRole.Halberdier:
                    Balance(definition,185,4.5f,25,3.6f,1.15f,1.06f,.18f,0);
                    definition.AbilityCooldown=8; definition.SecondaryAbilityCooldown=10; definition.AbilityPower=36; definition.AbilityRadius=4.5f; break;
                case UnitRole.Duelist:
                    Balance(definition,100,7.5f,18,2.1f,.55f,.96f,.08f,0);
                    definition.AbilityCooldown=7; definition.SecondaryAbilityCooldown=9; definition.AbilityPower=24; definition.AbilityRadius=2.7f; break;
                case UnitRole.Frostbinder:
                    Balance(definition,100,4.8f,11,13,1.05f,1,.02f,5);
                    definition.AbilityCooldown=8; definition.SecondaryAbilityCooldown=11; definition.ThirdAbilityCooldown=14;
                    definition.AbilityPower=25; definition.AbilityRadius=4.5f; definition.ProjectileSpeed=25; break;
                case UnitRole.Alchemist:
                    Balance(definition,115,5.1f,10,11,.9f,.98f,.08f,4);
                    definition.AbilityCooldown=8; definition.SecondaryAbilityCooldown=10; definition.AbilityPower=24; definition.AbilityRadius=4.5f; break;
                case UnitRole.WarDrummer:
                    Balance(definition,165,4.5f,9,9,1.25f,1.08f,.16f,3);
                    definition.DisplayName="War Drummer"; definition.AbilityCooldown=10; definition.SecondaryAbilityCooldown=13; definition.AbilityRadius=7; break;
                case UnitRole.Sharpshooter:
                    Balance(definition,80,5.25f,23,23,1.75f,.97f,0,7);
                    definition.AbilityCooldown=9; definition.SecondaryAbilityCooldown=7; definition.ProjectileSpeed=38; definition.AbilityRadius=1; break;
                case UnitRole.Berserker:
                    Balance(definition,210,6f,30,2.5f,.85f,1.16f,.08f,0);
                    definition.AbilityCooldown=10; definition.SecondaryAbilityCooldown=8; definition.AbilityPower=38; definition.AbilityRadius=3.5f; break;
                case UnitRole.Warden:
                    Balance(definition,250,4.35f,16,10,1,1.12f,.2f,3);
                    definition.AbilityCooldown=9; definition.SecondaryAbilityCooldown=12; definition.ThirdAbilityCooldown=13;
                    definition.AbilityPower=18; definition.AbilityRadius=5; break;
                case UnitRole.Skywing:
                    Balance(definition, 85, 6.2f, 11, 13, .95f, .85f, .05f, 4);
                    definition.AbilityCooldown = 10; definition.ProjectileSpeed = 26; break;
                case UnitRole.Stormwing:
                    Balance(definition, 155, 4.4f, 19, 15, 1.25f, 1.1f, .12f, 4);
                    definition.AbilityCooldown = 13; definition.ProjectileSpeed = 24; break;
                case UnitRole.Dawnwing:
                    Balance(definition, 135, 5, 12, 14, 1, 1, .08f, 4);
                    definition.AbilityCooldown = 11; definition.SecondaryAbilityCooldown = 12; definition.AbilityRadius = 6; definition.ProjectileSpeed = 25; break;
                case UnitRole.Emberwing:
                    Balance(definition, 185, 4.2f, 24, 18, 1.4f, 1.18f, .12f, 5);
                    definition.AbilityCooldown = 12; definition.SecondaryAbilityCooldown = 13; definition.AbilityPower = 55; definition.AbilityRadius = 3.5f; definition.ProjectileSpeed = 24; break;
                case UnitRole.Crownwing:
                    Balance(definition, 310, 3.6f, 28, 16, 1.25f, 1.3f, .25f, 4);
                    definition.AbilityCooldown = 14; definition.SecondaryAbilityCooldown = 18; definition.ProjectileSpeed = 25; break;
            }

            return definition;
        }

        static void Balance(BattleUnitDefinition d, float hp, float speed, float damage, float range,
            float attackCooldown, float scale, float armor, float retreat)
        {
            d.MaxHealth = hp; d.MoveSpeed = speed; d.Damage = damage; d.AttackRange = range;
            d.AttackCooldown = attackCooldown; d.BodyScale = scale; d.ArmorFraction = armor; d.RetreatDistance = retreat;
        }

        private void OnValidate()
        {
            MaxHealth = Mathf.Max(1f, MaxHealth);
            MoveSpeed = Mathf.Max(0.1f, MoveSpeed);
            Damage = Mathf.Max(0f, Damage);
            AttackRange = Mathf.Max(0.1f, AttackRange);
            AttackCooldown = Mathf.Max(0.05f, AttackCooldown);
            AbilityCooldown = Mathf.Max(0.1f, AbilityCooldown);
            SecondaryAbilityCooldown = Mathf.Max(.1f, SecondaryAbilityCooldown);
            ThirdAbilityCooldown = Mathf.Max(.1f, ThirdAbilityCooldown);
            FourthAbilityCooldown = Mathf.Max(.1f, FourthAbilityCooldown);
            BodyScale = Mathf.Clamp(BodyScale, .4f, 1.65f);
            ArmorFraction = Mathf.Clamp(ArmorFraction, 0f, 0.85f);
            ProjectileSpeed = Mathf.Max(0.1f, ProjectileSpeed);
            AbilityPower = Mathf.Max(0f, AbilityPower);
            AbilityRadius = Mathf.Max(0.1f, AbilityRadius);
            RetreatDistance = Mathf.Max(0f, RetreatDistance);
            FlightCeiling = float.IsFinite(FlightCeiling) ? Mathf.Clamp(FlightCeiling, 0, BattleTower.MaximumHeight) : 0;
        }
    }
}
