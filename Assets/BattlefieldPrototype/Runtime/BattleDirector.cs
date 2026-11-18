using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattlefieldPrototype
{
    public enum BattlePhase { Setup, Fighting, Victory, Defeat, Home }

    /// <summary>Owns the local match. Input and AI share the same unit combat API.</summary>
    public sealed class BattleDirector : MonoBehaviour
    {
        readonly List<BattleUnit> units = new List<BattleUnit>();
        Action spawnRound;
        BattleDeployment deployment;
        BattleTacticalField tacticalField;
        BattleFlares flares;
        public BattleFlares Flares
        {
            get
            {
                if (flares == null)
                {
                    flares = GetComponent<BattleFlares>();
                    if (flares == null) flares = gameObject.AddComponent<BattleFlares>();
                    flares.Initialize(this);
                }
                return flares;
            }
        }
        public TowerBattle Towers { get; internal set; }
        static int sightPhysicsFrame = -1;
        internal static int SightGeometryVersion { get; private set; }
        internal static void InvalidateSightPhysics() { sightPhysicsFrame = -1; SightGeometryVersion++; }
        internal static void SynchronizeSightPhysics()
        {
            if (sightPhysicsFrame == Time.frameCount) return;
            Physics.SyncTransforms();
            sightPhysicsFrame = Time.frameCount;
        }
        public BattleTacticalField Tactics => tacticalField != null ? tacticalField :
            (tacticalField = new BattleTacticalField(this));

        public IReadOnlyList<BattleUnit> Units => units;
        public BattlePhase Phase { get; private set; } = BattlePhase.Setup;
        public int RoundId { get; private set; } = 1;
        public bool IsBattleRunning => Phase == BattlePhase.Fighting;
        public float BattleDuration { get; private set; }
        public int AliveBlue => CountLiving(BattleTeam.Blue);
        public int AliveRed => CountLiving(BattleTeam.Red);
        public event Action<BattleUnit> UnitDied;
        public event Action RoundReset;
        public event Action<BattlePhase> PhaseChanged;
        public event Action<BattleUnit> UnitAttacked;
        public event Action<BattleUnit, int> AbilityActivated;
        public event Action<BattleUnit, BattleUnit, float> UnitDamaged;
        public void NotifyAttack(BattleUnit unit) => UnitAttacked?.Invoke(unit);
        public void NotifyAbility(BattleUnit unit, int slot) => AbilityActivated?.Invoke(unit, slot);
        public void NotifyDamage(BattleUnit target, BattleUnit source, float amount) => UnitDamaged?.Invoke(target, source, amount);

        public void Initialize(Action spawn)
        {
            spawnRound = spawn;
            _ = Flares;
        }

        private void Update() { if (IsBattleRunning) BattleDuration += Time.deltaTime; }

        public void RegisterUnit(BattleUnit unit)
        {
            if (unit != null && !units.Contains(unit)) units.Add(unit);
        }

        public void UnregisterUnit(BattleUnit unit)
        {
            units.Remove(unit);
            tacticalField?.Forget(unit);
        }

        internal void AttachDeployment(BattleDeployment editor) => deployment = editor;

        public void NewGame()
        {
            if (Towers != null) Towers.NewGame();
            else if (deployment != null) deployment.NewGame();
            else RestartBattle();
        }

        public void ReturnHome()
        {
            if (Towers != null) Towers.GoHome();
            else if (deployment != null) deployment.GoHome();
            else ResetRound(BattlePhase.Home, null);
        }

        public void BeginBattle()
        {
            if (Phase != BattlePhase.Setup) return;
            if (AliveBlue == 0 || AliveRed == 0)
            {
                Debug.LogWarning("Both armies need living units before the battle can start.", this);
                return;
            }
            Phase = BattlePhase.Fighting;
            PhaseChanged?.Invoke(Phase);
        }

        public void RestartBattle()
        {
            if (Towers != null) { Towers.NewGame(); return; }
            if (deployment != null)
            {
                deployment.NewGame();
                return;
            }
            ResetRound(BattlePhase.Setup, spawnRound);
        }

        internal void ResetRound(BattlePhase phase, Action rebuild)
        {
            Phase = phase;
            RoundId++;
            BattleDuration = 0f;
            tacticalField?.Clear();
            for (int i = 0; i < units.Count; i++)
                if (units[i] != null) units[i].SetPossessed(false);
            units.Clear();
            rebuild?.Invoke();
            RoundReset?.Invoke();
            PhaseChanged?.Invoke(Phase);
        }

        public void NotifyUnitDied(BattleUnit unit)
        {
            tacticalField?.Forget(unit);
            BattlePhase previousPhase = Phase;
            if (IsBattleRunning && Towers == null)
            {
                if (AliveBlue == 0) Phase = BattlePhase.Defeat;
                else if (AliveRed == 0) Phase = BattlePhase.Victory;
            }
            UnitDied?.Invoke(unit);
            if (previousPhase != Phase) PhaseChanged?.Invoke(Phase);
        }

        internal void EndTowerBattle(bool won)
        {
            if (!IsBattleRunning || Towers == null) return;
            Phase = won ? BattlePhase.Victory : BattlePhase.Defeat;
            PhaseChanged?.Invoke(Phase);
        }

        public BattleUnit FindNearestEnemy(BattleUnit seeker)
        {
            if (seeker == null) return null;
            BattleUnit best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnit unit = units[i];
                if (unit == null || !unit.IsAlive || unit.Team == seeker.Team) continue;
                float distance = (unit.transform.position - seeker.transform.position).sqrMagnitude;
                if (distance >= bestDistance || !seeker.CanSee(unit)) continue;
                bestDistance = distance;
                best = unit;
            }
            return best;
        }

        public BattleUnit FindPriorityEnemy(BattleUnit seeker)
        {
            if (seeker == null) return null;
            BattleUnit best=null; float bestScore=float.PositiveInfinity;
            bool hunter=BattleClassCatalog.IsFlanker(seeker.Definition.Role);
            foreach (var candidate in units)
            {
                if (candidate==null || !candidate.IsAlive || candidate.Team==seeker.Team) continue;
                float distance=(candidate.transform.position-seeker.transform.position).magnitude;
                float score=distance;
                bool support=BattleClassCatalog.IsSupport(candidate.Definition.Role);
                if (hunter && support) score*=.62f;
                if (hunter && !candidate.IsMelee) score*=.85f;
                if (!seeker.IsMelee && candidate.HealthFraction<.35f) score*=.75f;
                // Immediate melee threats outweigh tempting back-line targets.
                if (distance<4 && candidate.IsMelee) score*=.5f;
                // Only trace candidates that can improve the visible target.
                if (score < bestScore && seeker.CanSee(candidate)) { bestScore=score; best=candidate; }
            }
            return best;
        }

        public BattleUnit FindSupportAnchor(BattleUnit seeker, BattleUnit threat)
        {
            BattleUnit best=null; float score=float.PositiveInfinity;
            foreach (var candidate in units)
            {
                if (candidate==null || candidate==seeker || !candidate.IsAlive || candidate.Team!=seeker.Team || !candidate.IsMelee || !seeker.CanSee(candidate)) continue;
                float value=(candidate.transform.position-seeker.transform.position).sqrMagnitude;
                if (threat!=null) value+=(candidate.transform.position-threat.transform.position).sqrMagnitude*.25f;
                if (value<score) { best=candidate; score=value; }
            }
            return best;
        }

        public BattleUnit FindWoundedAlly(BattleUnit seeker, float range)
        {
            if (seeker == null) return null;
            BattleUnit best = null;
            float lowestHealth = 0.999f;
            float squaredRange = range * range;
            for (int i = 0; i < units.Count; i++)
            {
                BattleUnit unit = units[i];
                if (unit == null || !unit.IsAlive || unit.Team != seeker.Team) continue;
                if (unit.HealthFraction >= lowestHealth) continue;
                if ((unit.transform.position - seeker.transform.position).sqrMagnitude > squaredRange) continue;
                if (unit != seeker && !seeker.CanSee(unit)) continue;
                lowestHealth = unit.HealthFraction;
                best = unit;
            }
            return best;
        }

        int CountLiving(BattleTeam team)
        {
            int count = 0;
            for (int i = 0; i < units.Count; i++)
                if (units[i] != null && units[i].IsAlive && units[i].Team == team) count++;
            return count;
        }
    }
}
