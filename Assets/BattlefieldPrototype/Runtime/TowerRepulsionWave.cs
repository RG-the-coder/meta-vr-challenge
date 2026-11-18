using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class BattleTower
    {
        public const int MaximumPulseLevel = 3;
        public int PulseLevel { get; private set; } = 1;
        public float PulseRadius => PulseLevel == 1 ? 24 : PulseLevel == 2 ? 32 : 40;
        public float PulsePushDistance => PulseLevel == 1 ? 6 : 8;
        public float PulseCooldown => PulseLevel == 1 ? 20 : PulseLevel == 2 ? 16 : 12;
        public float PulseRemaining => Mathf.Max(0, pulseReadyAt - Time.time);
        public int PulseUpgradeCost => PulseLevel == 1 ? 75 : PulseLevel == 2 ? 120 : 0;
        public string NextPulseUpgrade => PulseLevel == 1 ? "Shield Break" : PulseLevel == 2 ? "Aftershock" : "Maximum level";
        float pulseReadyAt;

        bool CanCommandPulse => Battle != null && Battle.CanUseSummitActions && Battle.BoundTower == this && Owner == BattleTeam.Blue && !IsBroken;
        internal bool UpgradePulse()
        {
            if (!CanCommandPulse) return false;
            if (PulseLevel >= MaximumPulseLevel) return Battle.Fail("This tower's Repulsion Wave is fully upgraded.");
            int price = PulseUpgradeCost; string upgrade = NextPulseUpgrade;
            if (!Battle.TrySpend(BattleTeam.Blue, price)) return Battle.Fail(upgrade + " requires " + price + " gold for this tower.");
            PulseLevel++; // Keep an active cooldown intact; only future casts use the shorter interval.
            Battle.Report(Title + ": Repulsion Wave level " + PulseLevel + " / " + upgrade + " unlocked for " + price + " gold.");
            return true;
        }
        internal bool CastPulse()
        {
            if (!CanCommandPulse) return false;
            if (PulseRemaining > 0) return Battle.Fail("This tower's Repulsion Wave is recharging (" + PulseRemaining.ToString("0.0") + "s).");
            int count = 0, shields = 0;
            foreach (var enemy in Battle.Director.Units)
            {
                if (enemy == null || !enemy.IsAlive || enemy.Team == Owner) continue;
                Vector3 away = Vector3.ProjectOnPlane(enemy.transform.position - transform.position, Vector3.up);
                if (away.sqrMagnitude > PulseRadius * PulseRadius) continue;
                if (away.sqrMagnitude < .0001f) away = Vector3.forward;
                if (!enemy.Repel(away, PulsePushDistance)) continue;
                count++;
                if (PulseLevel >= 2 && enemy.BreakShield()) shields++;
                if (PulseLevel >= 3) enemy.ApplySlow(3);
            }
            if (count == 0) return Battle.Fail("No enemies within this tower's " + PulseRadius + "m Repulsion Wave. Cooldown preserved.");
            pulseReadyAt = Time.time + PulseCooldown;
            var effects = Battle.Director.GetComponent<BattleEffects>();
            var color = PulseLevel >= 2 ? new Color(.75f, .55f, 1) : new Color(.35f, .9f, 1);
            effects?.TowerWave(transform.position, PulseRadius, color);
            effects?.TowerWave(transform.position + Vector3.up * Height, PulseRadius, color);
            Battle.Report("Repulsion Wave: " + count + " enemies pushed; zero health damage." +
                (PulseLevel >= 2 ? " " + shields + " shields broken." : "") + (PulseLevel >= 3 ? " Slowed for 3s." : ""));
            return true;
        }
    }

    public sealed partial class TowerBattle
    {
        public bool TryTowerPulse()
        {
            if (!CanUseSummitActions) return Fail("Return to the summit of your owned standing tower to use Repulsion Wave.");
            return BoundTower.CastPulse();
        }
        public bool TryUpgradeTowerPulse()
        {
            if (!CanUseSummitActions) return Fail("Repulsion Wave upgrades require commanding your tower from its summit.");
            return BoundTower.UpgradePulse();
        }
    }
}
