using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    public enum BattleTacticalState { Searching, Investigating, Following, Pursuing, Fighting,
        Retreating, SeekingHealer, TakingCover, Supporting, Holding, Controlled, Rallying }

    public sealed partial class BattleUnitAI
    {
        public const float PursuitRecognitionSeconds = 3f;
        public const float EnemyMemorySeconds = 7f;
        public BattleTacticalState State { get; private set; }
        public bool IsPursuing { get; private set; }
        public BattleUnit FollowedUnit { get; private set; }
        public Vector3 LastSeenPosition => lastSeenPosition;
        public bool HasEnemyMemory => hasLastSeen && lastSeenAge < EnemyMemorySeconds;
        BattleUnit observedPursuer;
        float observationSeconds, perceptionElapsed, lastSeenAge, followLostAge;
        Vector3 observationHeading, lastSeenPosition, followPosition, followHeading, tacticalGoal;
        BattleTacticalState recoveryState;
        bool hasLastSeen, recovering, spacingRetreat;
        float recoveryStarted, recoveryCooldown, nextTacticalChoice, searchWaitUntil;
        float reactedToDamageAt = float.NegativeInfinity;
        int answeredFlare, flareRound;
        NavMeshPath tacticalPath;
        readonly RaycastHit[] coverHits = new RaycastHit[32];

        void ResetAwareness()
        {
            enemy = supportAnchor = observedPursuer = FollowedUnit = null;
            hasLastSeen = enemyVisible = recovering = spacingRetreat = IsPursuing = false;
            lastSeenAge = float.PositiveInfinity;
            observationSeconds = perceptionElapsed = targetRefresh = nextTacticalChoice = 0;
            followLostAge = recoveryCooldown = searchWaitUntil = 0;
            reactedToDamageAt = float.NegativeInfinity;
            State = BattleTacticalState.Searching;
        }

        void RefreshAwareness(float elapsed)
        {
            var previousEnemy = enemy;
            enemy = director.FindPriorityEnemy(unit);
            if (enemy != previousEnemy && !recovering) nextTacticalChoice = 0;
            enemyVisible = enemy != null;
            supportAnchor = BattleClassCatalog.IsSupport(unit.Definition.Role) && !unit.IsMelee ?
                director.FindSupportAnchor(unit, enemy) : null;
            if (enemyVisible)
            {
                lastSeenPosition = enemy.transform.position;
                hasLastSeen = true;
                lastSeenAge = 0;
                director.Tactics.RecordContact(unit, enemy);
                FollowedUnit = observedPursuer = null;
                observationSeconds = 0;
                return;
            }
            if (FollowedUnit != null)
            {
                if (unit.CanSee(FollowedUnit))
                { followPosition = FollowedUnit.transform.position; followHeading = FollowedUnit.transform.forward; followLostAge = 0; }
                else followLostAge += elapsed;
                if (!FollowedUnit.IsAlive || followLostAge > 4)
                { FollowedUnit = null; nextTacticalChoice = 0; }
                else return;
            }

            BattleUnit candidate = null;
            float nearest = float.PositiveInfinity;
            foreach (var ally in director.Units)
            {
                if (ally == null || ally == unit || !ally.IsAlive || ally.Team != unit.Team) continue;
                if (ally.PlanarVelocity.sqrMagnitude < 1 || !unit.CanSee(ally)) continue;
                var ai = ally.GetComponent<BattleUnitAI>();
                bool purposeful = ai != null && ai.IsPursuing;
                if (ally.IsPossessed)
                {
                    var seenByLeader = director.FindPriorityEnemy(ally);
                    purposeful = seenByLeader != null && Vector3.Dot(ally.PlanarVelocity.normalized,
                        (seenByLeader.transform.position - ally.transform.position).normalized) > .7f;
                }
                if (!purposeful) continue;
                // Observe steady forward progress. Never copy the leader's target.
                if (Vector3.Dot(ally.PlanarVelocity.normalized, ally.transform.forward) < .65f) continue;
                float distance = (ally.transform.position - unit.transform.position).sqrMagnitude;
                if (ally == observedPursuer) { candidate = ally; break; }
                if (distance < nearest) { nearest = distance; candidate = ally; }
            }
            if (candidate == null) { observedPursuer = null; observationSeconds = 0; return; }
            Vector3 heading = candidate.PlanarVelocity.normalized;
            if (candidate != observedPursuer || Vector3.Dot(heading, observationHeading) < .7f)
            { observedPursuer = candidate; observationSeconds = 0; }
            else observationSeconds += Mathf.Min(elapsed, .4f);
            observationHeading = heading;
            if (observationSeconds >= PursuitRecognitionSeconds)
            {
                FollowedUnit = candidate;
                followPosition = candidate.transform.position;
                followHeading = candidate.transform.forward;
                followLostAge = 0;
                nextTacticalChoice = 0;
            }
        }

        void ActWithoutEnemy()
        {
            if (!HasEnemyMemory && TryInvestigateFlare()) return;
            if (unit.Team == BattleTeam.Blue && orders != null && !orders.IsAdvancing(unit))
            { HoldOrGuard(orders.Destination(unit)); return; }
            if (FollowedUnit != null)
            {
                State = BattleTacticalState.Following;
                Vector3 behind = followPosition - followHeading * 4;
                if ((unit.transform.position - behind).sqrMagnitude > 6) TravelTo(behind);
                else { unit.Move(Vector3.zero); Scan(); }
                return;
            }
            if (HasEnemyMemory)
            {
                State = BattleTacticalState.Investigating;
                if (PlanarDistance(unit.transform.position, lastSeenPosition) > 2)
                { IsPursuing = true; TravelTo(lastSeenPosition); }
                else { unit.Move(Vector3.zero); Scan(); }
                return;
            }
            // Fragile support stays behind a visible frontline while looking around.
            if (supportAnchor != null && BattleClassCatalog.IsSupport(unit.Definition.Role) && !unit.IsMelee)
            {
                State = BattleTacticalState.Supporting;
                Vector3 behind = supportAnchor.transform.position - supportAnchor.transform.forward * 4;
                if (PlanarDistance(unit.transform.position, behind) > 5) TravelTo(behind);
                else { unit.Move(Vector3.zero); Scan(); }
                return;
            }
            State = BattleTacticalState.Searching;
            if (Time.time < searchWaitUntil) { unit.Move(Vector3.zero); Scan(); return; }
            if (Time.time >= nextTacticalChoice || PlanarDistance(unit.transform.position, tacticalGoal) < 2)
            {
                if (nextTacticalChoice > 0 && PlanarDistance(unit.transform.position, tacticalGoal) < 2)
                { searchWaitUntil = Time.time + (BattleClassCatalog.IsSupport(unit.Definition.Role) || unit.Definition.AttackRange > 15 ? 1.5f : .5f);
                    nextTacticalChoice = 0; unit.Move(Vector3.zero); Scan(); return; }
                tacticalGoal = ChooseSearchPosition();
                nextTacticalChoice = Time.time + 3.5f;
                director.Tactics.Visit(unit.Team, unit.transform.position);
            }
            TravelTo(tacticalGoal);
        }

        bool TryInvestigateFlare()
        {
            if (flareRound != director.RoundId) { flareRound = director.RoundId; answeredFlare = 0; }
            if (!director.Flares.TryGetLatest(out var signal) || signal.Sequence <= answeredFlare) return false;
            State = BattleTacticalState.Rallying;
            if (PlanarDistance(unit.transform.position, signal.Position) <= 6 &&
                unit.CanSeePositionFrom(signal.Position + Vector3.up * unit.Definition.EyeHeight, unit.EyePosition, signal.Position - unit.transform.position))
            {
                answeredFlare = signal.Sequence;
                nextTacticalChoice = 0;
                unit.Move(Vector3.zero); Scan();
                return true;
            }
            int seed = Mathf.Abs(transform.GetSiblingIndex());
            float angle = seed * 2.39996f;
            Vector3 destination = signal.Position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (2 + seed % 3);
            if (NavMesh.SamplePosition(destination, out var position, 3, NavMesh.AllAreas)) destination = position.position;
            else destination = signal.Position;
            // A signal broadcasts a location, not enemy identity, heat or coordinates.
            TravelTo(destination);
            return true;
        }

        Vector3 ChooseSearchPosition()
        {
            Vector3 origin = unit.transform.position, best = origin;
            float bestScore = float.NegativeInfinity;
            int seed = Mathf.Abs(transform.GetSiblingIndex()) + director.RoundId * 7;
            float phase = seed * 2.39996f + Time.time * .19f;
            for (int i = 0; i < 8; i++)
            {
                float angle = phase + i * Mathf.PI / 4;
                Vector3 candidate = director.Tactics.ClampToMap(origin + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (18 + seed % 17));
                if (!Reachable(candidate, out candidate)) continue;
                float heat = director.Tactics.Heat(unit, candidate);
                bool cautious = BattleClassCatalog.IsSupport(unit.Definition.Role) || unit.HealthFraction < .5f;
                float score = -director.Tactics.RecentVisit(unit.Team, candidate) * 22 + heat * (cautious ? -18 : 18) -
                    PlanarDistance(candidate, director.transform.position) * .12f + PlanarDistance(origin, candidate) * .08f;
                if ((cautious || unit.Definition.AttackRange > 15) && NearbyCover(candidate)) score += 7;
                // Flankers favor outer lanes; other fighters gradually sweep inward.
                if (BattleClassCatalog.IsFlanker(unit.Definition.Role)) score += Mathf.Abs(candidate.x - director.transform.position.x) * .045f;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            director.Tactics.Visit(unit.Team, best);
            return best;
        }

        bool TryRecover()
        {
            if (!recovering && unit.HealthFraction < .28f && Time.time >= recoveryCooldown)
            { recovering = true; recoveryStarted = Time.time; nextTacticalChoice = 0; }
            if (!recovering) return false;
            if (unit.HealthFraction >= .62f || Time.time - recoveryStarted > 7)
            { recovering = false; recoveryCooldown = Time.time + 5; nextTacticalChoice = 0; return false; }
            if (Time.time >= nextTacticalChoice)
            {
                BattleUnit refuge = null;
                bool healerFound = false;
                float best = float.PositiveInfinity;
                foreach (var ally in director.Units)
                {
                    if (ally == null || ally == unit || !ally.IsAlive || ally.Team != unit.Team || !unit.CanSee(ally)) continue;
                    int slot = BattleTacticalField.HealingSlot(ally.Definition.Role);
                    bool healer = slot >= 0;
                    if (!healer && !BattleTacticalField.IsProtector(ally.Definition.Role)) continue;
                    float score = PlanarDistance(unit.transform.position, ally.transform.position) +
                        (healer ? ally.GetAbilityCooldownRemaining(slot) * 2 : 24) + (1 - ally.HealthFraction) * 12;
                    Vector3 destination = healer ? ally.transform.position : ally.transform.position +
                        (ally.transform.position - lastSeenPosition).normalized * 3;
                    if (score < best && Reachable(destination, out var reachable))
                    { best = score; refuge = ally; healerFound = healer; tacticalGoal = reachable; }
                }
                recoveryState = refuge != null && healerFound ? BattleTacticalState.SeekingHealer : BattleTacticalState.TakingCover;
                if (refuge != null && healerFound)
                {
                    Vector3 away = (unit.transform.position - refuge.transform.position).normalized;
                    // Leave space around the healer and stay inside their healing field.
                    Vector3 nearHealer = refuge.transform.position + away * Mathf.Max(1.5f, refuge.Definition.AbilityRadius * .65f);
                    if (Reachable(nearHealer, out var reachable)) tacticalGoal = reachable;
                }
                else if (refuge == null)
                {
                    Vector3 away = hasLastSeen ? unit.transform.position - lastSeenPosition : -unit.transform.forward;
                    away.y = 0;
                    tacticalGoal = TacticalPosition(unit.transform.position + away.normalized * 9, lastSeenPosition, true);
                }
                nextTacticalChoice = Time.time + .8f;
            }
            State = recoveryState;
            if (PlanarDistance(unit.transform.position, tacticalGoal) > 1.3f) TravelTo(tacticalGoal, !enemyVisible);
            else { unit.Move(Vector3.zero); if (!enemyVisible) Scan(); }
            return true;
        }

        void CombatFootwork(Vector3 offset, float distance)
        {
            if (spacingRetreat) { spacingRetreat = false; nextTacticalChoice = 0; }
            if (unit.IsMelee)
            {
                // Circle during recovery instead of standing still between swings.
                if (distance < unit.Definition.AttackRange * .65f && unit.AttackCooldownRemaining > .12f)
                { State = BattleTacticalState.Fighting; TravelTo(unit.transform.position + Vector3.Cross(Vector3.up, offset.normalized) * 1.5f * Side, false); }
                else unit.Move(Vector3.zero);
                return;
            }
            if (Time.time >= nextTacticalChoice)
            {
                Vector3 lateral = Vector3.Cross(Vector3.up, offset.normalized) * Side * 3;
                tacticalGoal = TacticalPosition(unit.transform.position + lateral, lastSeenPosition, false);
                nextTacticalChoice = Time.time + 1.1f;
            }
            if (PlanarDistance(unit.transform.position, tacticalGoal) > .8f) TravelTo(tacticalGoal, false);
            else unit.Move(Vector3.zero);
        }

        void RetreatFrom(Vector3 offset)
        {
            if (!spacingRetreat || Time.time >= nextTacticalChoice)
            {
                Vector3 away = offset.sqrMagnitude > .01f ? -offset.normalized : -transform.forward;
                tacticalGoal = TacticalPosition(unit.transform.position + away * 6, lastSeenPosition, true);
                nextTacticalChoice = Time.time + .6f;
            }
            spacingRetreat = true;
            State = BattleTacticalState.Retreating;
            TravelTo(tacticalGoal, false);
        }

        int Side => transform.GetSiblingIndex() % 2 == 0 ? 1 : -1;
        void HoldOrGuard(Vector3 anchor)
        {
            State = BattleTacticalState.Holding;
            if (PlanarDistance(unit.transform.position, anchor) > 1.3f) TravelTo(anchor, !enemyVisible);
            else { unit.Move(Vector3.zero); if (!enemyVisible) Scan(); }
        }
        void Scan() => unit.Face(Quaternion.Euler(0, 100 * Time.deltaTime, 0) * unit.transform.forward);

        Vector3 ConstrainToOrder(Vector3 destination)
        {
            if (State == BattleTacticalState.Rallying) return destination;
            if (orders == null || unit.Team != BattleTeam.Blue || orders.IsAdvancing(unit) || orders.Current == BattleOrder.Route) return destination;
            Vector3 anchor = orders.Destination(unit);
            return anchor + Vector3.ClampMagnitude(destination - anchor, orders.Current == BattleOrder.Hold ? 3 : 8);
        }

        bool Reachable(Vector3 destination, out Vector3 reachable)
        {
            reachable = ConstrainToOrder(director.Tactics.ClampToMap(destination));
            if (!NavMesh.SamplePosition(unit.transform.position, out var start, 1.5f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(reachable, out var end, 3, NavMesh.AllAreas)) return false;
            if (tacticalPath == null) tacticalPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, tacticalPath) ||
                tacticalPath.status != NavMeshPathStatus.PathComplete) return false;
            reachable = end.position;
            return true;
        }

        Vector3 TacticalPosition(Vector3 preferred, Vector3 threat, bool hide)
        {
            Vector3 best = unit.transform.position;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI / 4;
                Vector3 candidate = i == 8 ? preferred : preferred + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 3;
                if (!Reachable(candidate, out candidate)) continue;
                bool covered = CoverBetween(candidate + Vector3.up * unit.Definition.EyeHeight, threat + Vector3.up);
                float distance = PlanarDistance(candidate, threat);
                float ideal = hide ? 16 : Mathf.Max(unit.Definition.RetreatDistance + 2, unit.Definition.AttackRange * .75f);
                float score = PlanarDistance(candidate, preferred) * .7f + Mathf.Abs(distance - ideal) * .5f +
                    director.Tactics.Heat(unit, candidate) * (hide ? 14 : 3) + (covered ? hide ? -12 : 8 : 0);
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
            return best;
        }
        bool CoverBetween(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            int count = Physics.RaycastNonAlloc(from, offset.normalized, coverHits, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (coverHits[i].collider.GetComponentInParent<BattleUnit>() == null) return true;
            return false;
        }
        bool NearbyCover(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position + Vector3.up * 2, 2.5f, separationHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (separationHits[i].GetComponentInParent<BattleUnit>() == null && separationHits[i].bounds.max.y > position.y + 1.5f) return true;
            return false;
        }
    }
}
