using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    [RequireComponent(typeof(BattleUnit))]
    public sealed partial class BattleUnitAI : MonoBehaviour
    {
        private BattleUnit unit;
        private BattleDirector director;
        private BattleUnit enemy;
        private BattleUnit supportAnchor;
        private BattleOrders orders;
        readonly Collider[] separationHits = new Collider[32];
        float separationRefresh;
        bool enemyVisible;
        Vector3 cachedSeparation;
        private NavMeshPath path;
        private Vector3[] corners;
        private int cornerIndex;
        private float targetRefresh;
        private float pathRefresh;
        private Vector3 lastDestination;
        bool pathBlocked;

        public void Initialize(BattleUnit controlledUnit, BattleDirector battle)
        {
            unit = controlledUnit;
            director = battle;
            orders=battle.GetComponent<BattleOrders>();
            path = new NavMeshPath();
            targetRefresh = 0f;
            pathRefresh = 0f;
            ResetAwareness();
        }

        private void Awake()
        {
            unit = GetComponent<BattleUnit>();
            path = new NavMeshPath();
        }

        private void Update()
        {
            if (unit == null || !unit.IsAlive) return;
            if (unit.IsHandControlled) { unit.Move(Vector3.zero); return; }
            if (unit.IsPossessed) { ResetAwareness(); State = BattleTacticalState.Controlled; return; }
            if (director == null) director = unit.Director;
            if (director == null || !director.IsBattleRunning || unit.Definition == null) return;

            targetRefresh -= Time.deltaTime;
            pathRefresh -= Time.deltaTime;
            lastSeenAge += Time.deltaTime;
            perceptionElapsed += Time.deltaTime;
            IsPursuing = false;
            if (unit.LastDamagedAt > reactedToDamageAt)
            {
                reactedToDamageAt = unit.LastDamagedAt;
                if (!enemyVisible && unit.LastHitDirection.sqrMagnitude > .01f)
                { unit.Face(-unit.LastHitDirection); targetRefresh = 0; }
            }
            if (targetRefresh <= 0f)
            {
                RefreshAwareness(perceptionElapsed);
                perceptionElapsed = 0;
                targetRefresh = .18f + (Mathf.Abs(transform.GetSiblingIndex())%11)*.008f;
            }

            var definition = unit.Definition;
            if (ActOnRoute()) return;
            if (director.Towers != null && ActOnDefense()) return;
            int healSlot=BattleTacticalField.HealingSlot(definition.Role);
            if (healSlot>=0 && unit.GetAbilityCooldownRemaining(healSlot)<=0)
            {
                var wounded = director.FindWoundedAlly(unit, Mathf.Max(12f, definition.AbilityRadius * 2f));
                if (wounded == null && unit.HealthFraction < 0.85f) wounded = unit;
                if (wounded != null && wounded.HealthFraction < 0.9f)
                {
                    float allyDistance = PlanarDistance(unit.transform.position, wounded.transform.position);
                    if (allyDistance <= definition.AbilityRadius * 0.9f)
                        unit.TryAbility(healSlot,unit.EyePosition, unit.transform.forward);
                    else if (orders==null || unit.Team!=BattleTeam.Blue || orders.Current!=BattleOrder.Hold ||
                        (wounded.transform.position-orders.Destination(unit)).sqrMagnitude<64)
                    {
                        TravelTo(wounded.transform.position);
                        State = BattleTacticalState.Supporting;
                        return;
                    }
                }
            }

            if (enemy == null || !enemy.IsAlive || !enemyVisible)
            {
                if (director.Towers != null && ActOnTower()) return;
                if (!TryRecover()) ActWithoutEnemy();
                return;
            }

            Vector3 aim = enemy.TargetPointFor(unit) - unit.EyePosition;
            if (unit.Definition.IsFlying)
            {
                unit.FlightAltitude = Mathf.Max(5, enemy.transform.position.y);
                if (unit.HealthFraction < .9f || aim.sqrMagnitude <= definition.AttackRange * definition.AttackRange)
                {
                    unit.TryAbility(0, unit.EyePosition, aim);
                    if (definition.AbilityCount > 1) unit.TryAbility(1, unit.EyePosition, aim);
                }
            }
            Vector3 offset = enemy.transform.position - unit.transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            // Never read a hidden target's current position to steer or attack.
            bool visible = unit.CanSee(enemy);
            if (!visible) { enemyVisible = false; if (!TryRecover()) ActWithoutEnemy(); return; }
            lastSeenPosition = enemy.transform.position;
            lastSeenAge = 0;
            hasLastSeen = true;
            State = BattleTacticalState.Fighting;
            unit.Face(offset);

            if (unit.IsMelee && (int)definition.Role < 7 && distance <= definition.AbilityRadius && visible)
                unit.TryAbility(unit.EyePosition, aim);
            else if ((definition.Role == UnitRole.Ranger || definition.Role == UnitRole.Skirmisher) &&
                distance <= definition.AttackRange * 0.9f && visible)
                unit.TryAbility(unit.EyePosition, aim);
            else if (definition.Role == UnitRole.Stormcaller && distance <= definition.AttackRange && visible)
                unit.TryAbility(unit.EyePosition, aim);

            // Both ability slots use the same combat API as possession. Choose
            // defenses/support for incoming threats and mobility for close enemies.
            switch (definition.Role)
            {
                case UnitRole.Vanguard:
                    if ((distance < 4f && visible) || unit.HealthFraction < .65f)
                        unit.TryAbility(1, unit.EyePosition, aim);
                    break;
                case UnitRole.Ranger:
                    if (visible && distance <= definition.AttackRange * 1.3f)
                        unit.TryAbility(1, unit.EyePosition, aim);
                    break;
                case UnitRole.Medic:
                    if (unit.SecondaryAbilityCooldownRemaining<=0 && (visible && distance < 14f || unit.HealthFraction < .65f) && NeedsShields(definition.AbilityRadius))
                        unit.TryAbility(1, unit.EyePosition, aim);
                    break;
                case UnitRole.Skirmisher:
                    if (visible && distance < definition.RetreatDistance)
                        unit.TryAbility(1, unit.EyePosition, -offset);
                    break;
                case UnitRole.Brute:
                    if (visible && distance <= 8f) unit.TryAbility(1, unit.EyePosition, aim);
                    break;
                case UnitRole.Engineer:
                    if (visible && distance < 14f)
                    {
                        if (unit.AbilityCooldownRemaining<=0 && NeedsShields(definition.AbilityRadius)) unit.TryAbility(0, unit.EyePosition, aim);
                        if (!unit.IsOverdriving) unit.TryAbility(1, unit.EyePosition, aim);
                    }
                    break;
                case UnitRole.Stormcaller:
                    if (visible && distance <= definition.AbilityRadius)
                        unit.TryAbility(1, unit.EyePosition, aim);
                    break;
                case UnitRole.Hound:
                    if (visible && distance < 8) unit.TryAbility(0, unit.EyePosition, aim);
                    break;
                case UnitRole.Bombard:
                    if (visible && distance <= 18) unit.TryAbility(0, unit.EyePosition, aim);
                    break;
                case UnitRole.Sentinel:
                    if (visible && distance < 12 && (NeedsShields(definition.AbilityRadius) || unit.HealthFraction < .8f))
                        unit.TryAbility(0, unit.EyePosition, aim);
                    break;
                case UnitRole.Titan:
                    if (visible && distance <= 6) unit.TryAbility(0, unit.EyePosition, aim);
                    if (visible && distance < 14 || unit.HealthFraction < .7f) unit.TryAbility(1, unit.EyePosition, aim);
                    if (visible && distance <= 8) unit.TryAbility(2, unit.EyePosition, aim);
                    break;
                case UnitRole.Pyromancer:
                    if (visible && distance <= 13)
                    {
                        unit.TryAbility(0, unit.EyePosition, aim);
                        if (distance <= definition.AbilityRadius) unit.TryAbility(1, unit.EyePosition, aim);
                        if (!enemy.IsBurning || enemy.Health > 24) unit.TryAbility(2, unit.EyePosition, aim);
                    }
                    break;
                case UnitRole.Assassin:
                    if (visible && distance < 8) unit.TryAbility(0, unit.EyePosition, aim);
                    if (visible && distance < 6 || unit.HealthFraction < .7f) unit.TryAbility(1, unit.EyePosition, aim);
                    if (visible && distance <= definition.AttackRange + 1 && enemy.HealthFraction < .7f)
                        unit.TryAbility(2, unit.EyePosition, aim);
                    break;
                case UnitRole.Paladin:
                    if (visible && distance <= 3) unit.TryAbility(0, unit.EyePosition, aim);
                    var woundedAlly = unit.SecondaryAbilityCooldownRemaining<=0?director.FindWoundedAlly(unit, definition.AbilityRadius):null;
                    if (woundedAlly != null) unit.TryAbility(1, unit.EyePosition, aim);
                    if (visible && distance < 14 && !unit.IsOverdriving) unit.TryAbility(2, unit.EyePosition, aim);
                    if (visible && distance <= 6) unit.TryAbility(3, unit.EyePosition, aim);
                    break;
                case UnitRole.Halberdier:
                    if(visible && distance<7) unit.TryAbility(0,unit.EyePosition,aim);
                    if(visible && distance<4.5f) unit.TryAbility(1,unit.EyePosition,aim);
                    break;
                case UnitRole.Duelist:
                    if(visible && distance<7) { unit.TryAbility(0,unit.EyePosition,aim); unit.TryAbility(1,unit.EyePosition,aim); }
                    break;
                case UnitRole.Frostbinder:
                    if(visible && distance<=16) unit.TryAbility(0,unit.EyePosition,aim);
                    if(visible && distance<definition.AbilityRadius) unit.TryAbility(1,unit.EyePosition,aim);
                    if(visible && distance<10 || unit.HealthFraction<.7f) unit.TryAbility(2,unit.EyePosition,aim);
                    break;
                case UnitRole.Alchemist:
                    if(visible && distance<=12) unit.TryAbility(0,unit.EyePosition,aim);
                    break;
                case UnitRole.WarDrummer:
                    if(visible && distance<18)
                    { unit.TryAbility(0,unit.EyePosition,aim); if(unit.SecondaryAbilityCooldownRemaining<=0 && NeedsShields(definition.AbilityRadius)) unit.TryAbility(1,unit.EyePosition,aim); }
                    break;
                case UnitRole.Sharpshooter:
                    if(visible && distance<=27) unit.TryAbility(0,unit.EyePosition,aim);
                    if(visible && distance<definition.RetreatDistance) unit.TryAbility(1,unit.EyePosition,aim);
                    break;
                case UnitRole.Berserker:
                    if(visible && distance<10) unit.TryAbility(0,unit.EyePosition,aim);
                    if(visible && distance<definition.AbilityRadius) unit.TryAbility(1,unit.EyePosition,aim);
                    break;
                case UnitRole.Warden:
                    if(visible && distance<6) unit.TryAbility(0,unit.EyePosition,aim);
                    if(visible && distance<14 && unit.SecondaryAbilityCooldownRemaining<=0 && NeedsShields(definition.AbilityRadius)) unit.TryAbility(1,unit.EyePosition,aim);
                    break;
            }

            // A small range margin keeps moving targets inside the actual swept attack range.
            if (distance <= definition.AttackRange * 0.9f && visible)
                unit.TryAttack(unit.EyePosition, aim);

            if (TryRecover()) return;

            if(unit.Team==BattleTeam.Blue && orders!=null && !orders.IsAdvancing(unit))
            {
                Vector3 anchor=orders.Destination(unit);
                bool returnToOrder=orders.Current==BattleOrder.Hold || (unit.transform.position-anchor).sqrMagnitude>64 ||
                    orders.ProtectedUnit!=null && (enemy.transform.position-orders.ProtectedUnit.transform.position).sqrMagnitude>196;
                if(returnToOrder)
                {
                    HoldOrGuard(anchor);
                    return;
                }
            }

            if(BattleClassCatalog.IsSupport(definition.Role) && !unit.IsMelee && supportAnchor!=null && supportAnchor.IsAlive && distance>definition.RetreatDistance)
            {
                Vector3 behind=supportAnchor.transform.position-offset.normalized*4;
                if((unit.transform.position-behind).sqrMagnitude>9) { State=BattleTacticalState.Supporting; TravelTo(behind,false); }
                else CombatFootwork(offset, distance);
                return;
            }

            if (!unit.IsMelee && distance < definition.RetreatDistance)
            {
                RetreatFrom(offset);
            }
            else if (distance > definition.AttackRange * 0.78f || !visible)
            {
                bool flank=BattleClassCatalog.IsFlanker(definition.Role);
                Vector3 approach=enemy.transform.position;
                if(flank && distance>8) approach+=Vector3.Cross(Vector3.up,offset.normalized)*((transform.GetSiblingIndex()%2==0?1:-1)*5);
                IsPursuing = true;
                State = BattleTacticalState.Pursuing;
                TravelTo(approach, false);
            }
            else
            {
                CombatFootwork(offset, distance);
            }
        }

        private bool NeedsShields(float radius)
        {
            foreach (var other in director.Units)
                if (other != null && other.IsAlive && other.Team == unit.Team && other.Shield < 10f &&
                    (other.transform.position - unit.transform.position).sqrMagnitude <= radius * radius) return true;
            return false;
        }

        private void TravelTo(Vector3 destination, bool faceMovement = true)
        {
            bool belowHighWalls = director != null && director.Towers != null && director.Towers.Terrain.TowerSightBlockers.Count > 0 &&
                unit.transform.position.y < TowerMapCatalog.WallHeight(director.Towers.Map) + .4f;
            if (unit.Definition.IsFlying && !belowHighWalls && (orders == null || unit.Team != BattleTeam.Blue || orders.Current != BattleOrder.Route || orders.IsAdvancing(unit)))
            {
                Vector3 flight = Vector3.ProjectOnPlane(destination - unit.transform.position, Vector3.up);
                unit.Move(flight.normalized * Mathf.Clamp01(flight.magnitude / 2));
                if (faceMovement) unit.Face(flight); return;
            }
            destination = ConstrainToOrder(destination);
            if (pathRefresh <= 0f || (destination - lastDestination).sqrMagnitude > 4f)
            {
                pathRefresh = .55f+(Mathf.Abs(transform.GetSiblingIndex())%13)*.017f;
                lastDestination = destination;
                corners = null;
                cornerIndex = 0;
                pathBlocked = false;
                NavMeshHit startHit;
                NavMeshHit endHit;
                Vector3 pathOrigin = unit.transform.position;
                if (unit.Definition.IsFlying) pathOrigin.y = 0;
                if (NavMesh.SamplePosition(pathOrigin, out startHit, unit.Definition.IsFlying ? 8 : 1.5f, NavMesh.AllAreas))
                {
                    pathBlocked = true;
                    if (NavMesh.SamplePosition(destination, out endHit, 2f, NavMesh.AllAreas) &&
                        NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path) &&
                        path.status == NavMeshPathStatus.PathComplete)
                    {
                        pathBlocked = false;
                        corners = path.corners;
                        cornerIndex = corners.Length > 1 ? 1 : 0;
                    }
                }
            }

            if (pathBlocked)
            {
                unit.Move(Vector3.zero);
                nextTacticalChoice = 0;
                if (faceMovement) Scan();
                return;
            }

            Vector3 steeringTarget = destination;
            if (corners != null && corners.Length > 0)
            {
                float reach = Mathf.Max(.6f, unit.Definition.CombatMoveSpeed * Time.deltaTime * 1.2f);
                while (cornerIndex < corners.Length - 1 && PlanarDistance(unit.transform.position, corners[cornerIndex]) < reach)
                    cornerIndex++;
                steeringTarget = corners[cornerIndex];
            }
            Vector3 direction = steeringTarget - unit.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.04f)
            {
                unit.Move(Vector3.zero);
                return;
            }
            direction.Normalize();

            // Local separation reduces crowd jams without a second movement component.
            if(Time.time>=separationRefresh)
            {
            separationRefresh=Time.time+.1f;
            cachedSeparation=Vector3.zero;
            int nearby=Physics.OverlapSphereNonAlloc(unit.transform.position,2.2f,separationHits,~0,QueryTriggerInteraction.Ignore);
            for (int hit=0;hit<nearby;hit++)
            {
                var other=separationHits[hit].GetComponentInParent<BattleUnit>();
                if (other == null || other == unit || !other.IsAlive) continue;
                Vector3 away = unit.transform.position - other.transform.position;
                away.y = 0f;
                float distanceSquared = away.sqrMagnitude;
                float spacing = unit.Definition.BodyRadius + other.Definition.BodyRadius + .4f;
                if (distanceSquared > 0.001f && distanceSquared < spacing * spacing)
                {
                    if (other.Team != unit.Team && !unit.CanSee(other)) continue;
                    cachedSeparation += away.normalized * (1f - Mathf.Sqrt(distanceSquared) / spacing);
                }
            }
            }
            direction = (direction + cachedSeparation * 0.55f).normalized;
            // Brake on the final corner so faster units do not oscillate past it.
            float arrival = corners == null || cornerIndex == corners.Length - 1 ?
                Mathf.Clamp01(PlanarDistance(unit.transform.position, steeringTarget) / Mathf.Max(.5f, unit.Definition.CombatMoveSpeed * Time.deltaTime * 1.5f)) : 1;
            unit.Move(direction * arrival);
            if (faceMovement) unit.Face(direction);
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
