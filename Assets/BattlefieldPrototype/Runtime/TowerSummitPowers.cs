using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattle
    {
        public const float SummitBowDamage = 250;
        public const float SummitBowInterval = .45f;
        public const float SummitPowerRange = 100;
        public const float RestorationRadius = 8;
        public int? SelectedSummitPower { get; private set; }
        public bool CanUseSummitActions => Director != null && Director.IsBattleRunning && IsMounted &&
            BoundTower != null && BoundTower.Owner == BattleTeam.Blue && !BoundTower.IsBroken &&
            Player != null && Player.IsAtTowerSummit(BoundTower);

        readonly float[] summitPowerReadyAt = new float[3];
        float summitBowReadyAt;

        public static string SummitPowerName(int slot) => slot == 0 ? "Repulse" : slot == 1 ? "Lightning" : slot == 2 ? "Restoration" : "";
        public static int SummitPowerCost(int slot) => slot == 1 ? 30 : slot == 2 ? 25 : 0;
        public static float SummitPowerCooldown(int slot) => slot == 0 ? 3 : slot == 1 ? 6 : slot == 2 ? 10 : 0;
        public float SummitPowerRemaining(int slot) => slot >= 0 && slot < 3 ? Mathf.Max(0, summitPowerReadyAt[slot] - Time.time) : 0;

        void ResetSummitPowers()
        {
            SelectedSummitPower = null; summitBowReadyAt = 0;
            System.Array.Clear(summitPowerReadyAt, 0, summitPowerReadyAt.Length);
        }

        public bool ChooseSummitPower(int? slot)
        {
            if (!CanUseSummitActions) return Fail("Bow, throw, spells and binding changes require the top of your standing tower.");
            if (slot.HasValue && (slot.Value < 0 || slot.Value >= 3)) return false;
            CancelRoutePlanning(); SelectedDefense = null; Recruitment = null;
            SelectedSummitPower = slot;
            Report(slot.HasValue ? SummitPowerName(slot.Value) + " selected. Aim into the world and fire; choose Bow to switch back."
                : "Bow ready. Hold with your left hand; right pinch near the string, pull back, then release.");
            return true;
        }

        // Validate the rendered eye-to-hand segment too: a tracked hand cannot cast through masonry.
        bool TrySummitAim(Ray input, out Ray ray)
        {
            ray = default;
            if (!CanUseSummitActions || !float.IsFinite(input.origin.sqrMagnitude) ||
                !float.IsFinite(input.direction.sqrMagnitude) || input.direction.sqrMagnitude < .001f) return false;
            Vector3 eye = Player.ViewCamera.transform.position, offset = input.origin - eye;
            if (offset.sqrMagnitude > 9) return false;
            Physics.SyncTransforms();
            foreach (var collider in Physics.OverlapSphere(input.origin, .06f, ~0, QueryTriggerInteraction.Ignore))
                if (collider.GetComponentInParent<BattleUnit>() == null) return false;
            if (offset.sqrMagnitude > .0001f)
                foreach (var hit in Physics.SphereCastAll(eye, .06f, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.GetComponentInParent<BattleUnit>() == null) return false;
            ray = new Ray(input.origin, input.direction.normalized); return true;
        }

        public bool TryFireSummitBow(Ray aim) => TryFireSummitBow(aim, 1);
        public bool TryFireSummitBow(Ray aim, float drawPower)
        {
            if (!float.IsFinite(drawPower) || drawPower <= 0 || Time.time < summitBowReadyAt || !TrySummitAim(aim, out var ray)) return false;
            drawPower = Mathf.Clamp(drawPower, .2f, 1);
            summitBowReadyAt = Time.time + SummitBowInterval;
            var color = new Color(1, .8f, .35f);
            BattleProjectile.SpawnSummitArrow(Director, ray.origin, ray.direction, drawPower, color);
            Director.GetComponent<BattleAudio>()?.SummitBow(ray.origin);
            Director.GetComponent<BattleEffects>()?.Muzzle(ray.origin, ray.direction, color);
            return true;
        }

        public bool TryUseSummitPower(int slot, Ray aim)
        {
            if (!CanUseSummitActions) return Fail("Return to the top of your standing tower to throw enemies or cast spells.");
            if (slot < 0 || slot >= 3) return false;
            if (SummitPowerRemaining(slot) > 0) return Fail(SummitPowerName(slot) + $" is recharging ({SummitPowerRemaining(slot):0.0}s).");
            int cost = SummitPowerCost(slot);
            if (BlueGold + .0001f < cost) return Fail(SummitPowerName(slot) + " requires " + cost + " gold per cast.");
            if (!TrySummitAim(aim, out var ray)) return Fail("Aim from your summit with a clear path from your eye to your hand.");
            var effects = Director.GetComponent<BattleEffects>();
            if (slot < 2)
            {
                var target = SummitEnemy(ray);
                if (target == null) return Fail("Aim at a visible enemy within 100m. No gold spent.");
                if (slot == 0)
                {
                    Vector3 away = Vector3.ProjectOnPlane(target.transform.position - BoundTower.transform.position, Vector3.up);
                    if (away.sqrMagnitude < .001f) away = Vector3.ProjectOnPlane(ray.direction, Vector3.up);
                    if (away.sqrMagnitude < .001f) away = Vector3.forward;
                    target.Repel(away, 8);
                    effects?.Pulse(target.transform.position, 3, new Color(.55f, .9f, 1));
                    Report("Repulse: enemy thrown away. No damage dealt.");
                }
                else
                {
                    TrySpend(BattleTeam.Blue, cost);
                    effects?.Lightning(ray.origin, target.EyePosition);
                    target.TakeDamage(110, null, ray.direction, 0);
                    Report("Lightning: 110 damage before armor. 30 gold spent.");
                }
            }
            else
            {
                if (!Physics.Raycast(ray, out var hit, SummitPowerRange, ~0, QueryTriggerInteraction.Ignore))
                    return Fail("Aim Restoration at the ground or an ally within 100m. No gold spent.");
                Vector3 center = hit.point + hit.normal * .12f;
                bool restored = false;
                foreach (var ally in Director.Units)
                {
                    if (ally == null || !ally.IsAlive || ally.Team != BattleTeam.Blue ||
                        (ally.EyePosition - center).sqrMagnitude > RestorationRadius * RestorationRadius ||
                        (ally.Health >= ally.MaxHealth && ally.Shield >= 35) || !ClearSummitLine(center, ally.EyePosition)) continue;
                    ally.Heal(60); ally.GrantShield(35, 8); restored = true;
                }
                if (!restored) return Fail("No allies need restoration in that 8m area. No gold spent.");
                TrySpend(BattleTeam.Blue, cost);
                effects?.Pulse(center, RestorationRadius, new Color(.35f, 1, .6f));
                Report("Restoration: allies in 8m heal 60 HP and gain 35 shield for 8s. 25 gold spent.");
            }
            summitPowerReadyAt[slot] = Time.time + SummitPowerCooldown(slot);
            return true;
        }

        BattleUnit SummitEnemy(Ray ray)
        {
            BattleUnit target = null; float nearest = SummitPowerRange;
            foreach (var hit in Physics.SphereCastAll(ray, .28f, SummitPowerRange, ~0, QueryTriggerInteraction.Ignore))
            {
                var unit = hit.collider.GetComponentInParent<BattleUnit>();
                if (unit != null && (!unit.IsAlive || unit.Team == BattleTeam.Blue)) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance; target = unit;
            }
            return target != null && ClearSummitLine(ray.origin, target.EyePosition) ? target : null;
        }

        static bool ClearSummitLine(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return true;
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<BattleUnit>() == null) return false;
            foreach (var hit in Physics.RaycastAll(to, -delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<BattleUnit>() == null) return false;
            return true;
        }
    }
}
