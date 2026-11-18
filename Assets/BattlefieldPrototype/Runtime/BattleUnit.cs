using UnityEngine;

namespace BattlefieldPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class BattleUnit : MonoBehaviour
    {
        public BattleUnitDefinition Definition { get; private set; }
        public BattleTeam Team { get; private set; }
        public BattleDirector Director { get; private set; }
        public BattleTower SpawnTower { get; internal set; }
        float flightAltitude = 5;
        public float FlightAltitude
        {
            get => Definition != null && Definition.IsFlying ? Mathf.Clamp(flightAltitude, 1, Definition.MaximumFlightHeight) : flightAltitude;
            set => flightAltitude = float.IsFinite(value) ? value : 1;
        }
        public bool IsAlive { get; private set; }
        public bool IsPossessed { get; private set; }
        public float Health { get; private set; }
        public float LastDamagedAt { get; private set; } = float.NegativeInfinity;
        public Vector3 LastHitDirection { get; private set; }
        public float MaxHealth => Definition != null ? Mathf.Max(1f, Definition.MaxHealth) : 1f;
        public float HealthFraction => Mathf.Clamp01(Health / MaxHealth);
        public Vector3 EyePosition => transform.position + Vector3.up * (Definition != null ? Definition.EyeHeight : 1.55f);
        public Vector3 AimPosition => EyePosition - Vector3.up * (.35f * (Definition != null ? Definition.BodyScale : 1));
        public Vector3 TargetPointFor(BattleUnit attacker)
        {
            if (Definition == null || attacker == null || !attacker.IsMelee) return AimPosition;
            // Small melee creatures bite a giant's legs instead of aiming beyond
            // their reach at its chest. The same point is used for cover checks.
            return transform.position + Vector3.up * Mathf.Clamp(attacker.EyePosition.y - transform.position.y,
                Definition.BodyRadius, Definition.BodyHeight - Definition.BodyRadius);
        }
        public float AbilityCooldownRemaining { get; private set; }
        public float SecondaryAbilityCooldownRemaining { get; private set; }
        public float Shield { get; private set; }
        public bool IsSlowed => slowRemaining > 0f;
        public bool IsDashing => dashRemaining > 0f;
        readonly float[] additionalCooldowns = new float[2];
        public float GetAbilityCooldownRemaining(int slot) => slot == 0 ? AbilityCooldownRemaining : slot == 1 ? SecondaryAbilityCooldownRemaining
            : slot >= 2 && slot < 4 ? additionalCooldowns[slot - 2] : 0;
        public float AttackCooldownRemaining { get; private set; }
        public bool IsGuarding => guardRemaining > 0f;
        public bool IsOverdriving => overdriveRemaining > 0f;
        public bool IsMelee => Definition != null && Definition.IsMelee;
        public Vector3 PlanarVelocity { get; private set; }
        public float SightRange { get; private set; }
        bool observedByPlayer = true;
        readonly RaycastHit[] sightHits = new RaycastHit[48];
        readonly Collider[] sightOriginHits = new Collider[16];
        public Vector3 KnockbackVelocity => knockbackVelocity;
        public float RecoilAmount => Mathf.Clamp01(recoilRemaining / RecoilDuration);
        public event System.Action ShotFired;
        public Transform VisualRoot;

        private CharacterController motor;
        private Renderer[] visualRenderers;
        private Vector3 requestedMove;
        private float verticalVelocity;
        private float guardRemaining;
        private float overdriveRemaining;
        private float overdriveSpeed = 1f, overdriveAttack = 1f;
        private float shieldRemaining, slowRemaining, dashRemaining;
        private Vector3 dashDirection;
        private Color[] baseColors;
        private MaterialPropertyBlock feedbackProperties;
        private float feedbackRemaining;
        private Color feedbackColor;
        private bool visualsTinted;
        private Vector3 knockbackVelocity;
        private const float KnockbackDamping = 10f;
        private const float RecoilDuration = 0.12f;
        private float recoilRemaining;
        private Transform recoilRoot;
        private Vector3 visualRestPosition;
        private Quaternion visualRestRotation;

        private void Awake()
        {
            motor = GetComponent<CharacterController>();
        }

        public void Configure(BattleDirector director, BattleUnitDefinition definition, BattleTeam team)
        {
            Director = director;
            Definition = definition;
            Team = team;
            if (motor == null) motor = GetComponent<CharacterController>();
            IsAlive = definition != null;
            IsPossessed = false;
            IsHandHeld = IsThrown = false;
            Health = MaxHealth;
            AbilityCooldownRemaining = 0f;
            SecondaryAbilityCooldownRemaining = 0f;
            System.Array.Clear(additionalCooldowns, 0, additionalCooldowns.Length);
            ResetExpandedState();
            Shield = shieldRemaining = slowRemaining = dashRemaining = 0f;
            overdriveSpeed = overdriveAttack = 1f;
            AttackCooldownRemaining = 0f;
            guardRemaining = 0f;
            overdriveRemaining = 0f;
            requestedMove = Vector3.zero;
            PlanarVelocity = Vector3.zero;
            observedByPlayer = true;
            var terrain = director != null ? director.GetComponent<BattlefieldBootstrap>() : null;
            Vector2 mapSize = terrain != null ? terrain.ArenaSize : new Vector2(160, 192);
            SightRange = Mathf.Max(mapSize.x, mapSize.y) * (definition != null ? definition.SightMapFraction : 1f / 3f);
            LastDamagedAt = float.NegativeInfinity;
            LastHitDirection = Vector3.zero;
            verticalVelocity = 0f;
            knockbackVelocity = Vector3.zero;
            recoilRemaining = 0f;
            RefreshRecoil();
            motor.enabled = false;
            if (definition != null)
            {
                motor.radius = definition.BodyRadius;
                motor.height = definition.BodyHeight;
                motor.center = Vector3.up * (definition.BodyHeight * .5f);
                motor.stepOffset = Mathf.Min(.25f, definition.BodyHeight * .25f);
                motor.skinWidth = Mathf.Clamp(.04f * definition.BodyScale, .01f, .06f);
            }
            motor.enabled = IsAlive;
            if (Director != null && IsAlive) Director.RegisterUnit(this);
            RefreshVisuals();
        }

        public void SetVisuals(Renderer[] renderers)
        {
            visualRenderers = renderers;
            if (renderers != null)
            {
                baseColors = new Color[renderers.Length];
                feedbackProperties = new MaterialPropertyBlock();
                for (int i = 0; i < renderers.Length; i++)
                {
                    var material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                    baseColors[i] = material == null ? Color.white : material.HasProperty("_BaseColor")
                        ? material.GetColor("_BaseColor") : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                }
            }
            RefreshVisuals();
        }

        public void SetPossessed(bool possessed)
        {
            if (possessed) { IsHandHeld = IsThrown = false; }
            IsPossessed = possessed && IsAlive;
            requestedMove = Vector3.zero;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (visualRenderers == null) return;
            foreach (var visual in visualRenderers)
                if (visual != null) visual.enabled = IsAlive && !IsPossessed && observedByPlayer;
        }

        public void SetObservedByPlayer(bool observed)
        {
            if (observedByPlayer == observed) return;
            observedByPlayer = observed;
            RefreshVisuals();
        }

        public bool CanSee(BattleUnit other) => CanSeeFrom(other, EyePosition, transform.forward);

        public bool CanSeeFrom(BattleUnit other, Vector3 eye, Vector3 facing)
        {
            if (!IsAlive || Definition == null || other == null || !other.IsAlive) return false;
            if (other == this) return true;
            return InSightLimits(other.AimPosition, eye, facing) && ClearSightTo(other, eye, other.AimPosition);
        }

        public bool CanSeePositionFrom(Vector3 position, Vector3 eye, Vector3 facing)
            => IsAlive && Definition != null && InSightLimits(position, eye, facing) && ClearSightTo(null, eye, position);

        public bool IsSightOriginClear(Vector3 origin)
        {
            BattleDirector.SynchronizeSightPhysics();
            int count = Physics.OverlapSphereNonAlloc(origin, .025f, sightOriginHits, ~0, QueryTriggerInteraction.Ignore);
            if (count == sightOriginHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (sightOriginHits[i].GetComponentInParent<BattleUnit>() == null) return false;
            return true;
        }

        public float TraceTerrainSight(Vector3 origin, Vector3 direction, float distance)
        {
            BattleDirector.SynchronizeSightPhysics();
            if (distance <= 0 || direction.sqrMagnitude < .001f) return 0;
            direction.Normalize();
            float nearest = distance;
            int count = Physics.RaycastNonAlloc(origin, direction, sightHits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return 0;
            for (int i = 0; i < count; i++)
                if (sightHits[i].collider.GetComponentInParent<BattleUnit>() == null) nearest = Mathf.Min(nearest, sightHits[i].distance);
            count = Physics.RaycastNonAlloc(origin + direction * distance, -direction, sightHits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return 0;
            for (int i = 0; i < count; i++)
                if (sightHits[i].collider.GetComponentInParent<BattleUnit>() == null) nearest = Mathf.Min(nearest, distance - sightHits[i].distance);
            return Mathf.Max(0, nearest - .025f);
        }

        bool InSightLimits(Vector3 position, Vector3 eye, Vector3 facing)
        {
            Vector3 delta = position - eye;
            if (delta.sqrMagnitude > SightRange * SightRange) return false;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            if (flat.sqrMagnitude > .001f && Vector3.Dot(forward, flat.normalized) <
                Mathf.Cos(Mathf.Clamp(Definition.SightAngle, 90, 170) * .5f * Mathf.Deg2Rad)) return false;
            return true;
        }

        private void Update()
        {
            if (!IsAlive) return;
            AttackCooldownRemaining = Mathf.Max(0f, AttackCooldownRemaining - Time.deltaTime);
            AbilityCooldownRemaining = Mathf.Max(0f, AbilityCooldownRemaining - Time.deltaTime);
            SecondaryAbilityCooldownRemaining = Mathf.Max(0f, SecondaryAbilityCooldownRemaining - Time.deltaTime);
            for (int slot = 0; slot < additionalCooldowns.Length; slot++) additionalCooldowns[slot] = Mathf.Max(0, additionalCooldowns[slot] - Time.deltaTime);
            UpdateBurn();
            shieldRemaining = Mathf.Max(0f, shieldRemaining - Time.deltaTime);
            if (shieldRemaining <= 0f) Shield = 0f;
            slowRemaining = Mathf.Max(0f, slowRemaining - Time.deltaTime);
            guardRemaining = Mathf.Max(0f, guardRemaining - Time.deltaTime);
            overdriveRemaining = Mathf.Max(0f, overdriveRemaining - Time.deltaTime);
            feedbackRemaining = Mathf.Max(0f, feedbackRemaining - Time.deltaTime);
            recoilRemaining = Mathf.Max(0f, recoilRemaining - Time.deltaTime);
            RefreshFeedback();
            RefreshRecoil();
        }

        private void RefreshRecoil()
        {
            if (recoilRoot != VisualRoot)
            {
                if (recoilRoot != null)
                    recoilRoot.SetLocalPositionAndRotation(visualRestPosition, visualRestRotation);
                recoilRoot = VisualRoot;
                if (recoilRoot != null)
                {
                    visualRestPosition = recoilRoot.localPosition;
                    visualRestRotation = recoilRoot.localRotation;
                }
            }
            if (recoilRoot == null) return;
            // Animate the body/weapon only. Keeping the motor and camera steady
            // preserves the shot's exact aim and avoids involuntary XR head motion.
            float recoil = RecoilAmount;
            recoilRoot.SetLocalPositionAndRotation(visualRestPosition - Vector3.forward * (0.055f * recoil),
                visualRestRotation * Quaternion.Euler(-2.5f * recoil, 0f, 0f));
        }

        private void Flash(Color color, float duration)
        {
            feedbackColor = color;
            feedbackRemaining = duration;
            RefreshFeedback();
        }

        private void RefreshFeedback()
        {
            if (visualRenderers == null || baseColors == null || feedbackProperties == null) return;
            bool tint = feedbackRemaining > 0f || IsGuarding || IsOverdriving || Shield > 0f || IsSlowed || IsBurning;
            if (!tint && !visualsTinted) return;
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                var visual = visualRenderers[i];
                if (visual == null) continue;
                Color color = feedbackRemaining > 0f ? Color.Lerp(baseColors[i], feedbackColor, 0.75f)
                    : IsGuarding ? Color.Lerp(baseColors[i], new Color(1f, 0.82f, 0.2f), 0.45f)
                    : IsOverdriving ? Color.Lerp(baseColors[i], Color.cyan, 0.5f)
                    : Shield > 0f ? Color.Lerp(baseColors[i], new Color(.35f, .65f, 1f), .45f)
                    : IsSlowed ? Color.Lerp(baseColors[i], new Color(.7f, .5f, 1f), .45f)
                    : IsBurning ? Color.Lerp(baseColors[i], new Color(1, .35f, .1f), .35f) : baseColors[i];
                visual.GetPropertyBlock(feedbackProperties);
                feedbackProperties.SetColor("_BaseColor", color);
                feedbackProperties.SetColor("_Color", color);
                visual.SetPropertyBlock(feedbackProperties);
            }
            visualsTinted = tint;
        }

        // Input and AI queue a direction in Update; the single motor consumes it once.
        public void Move(Vector3 worldDirection)
        {
            if (!IsAlive) return;
            worldDirection.y = 0f;
            requestedMove = Vector3.ClampMagnitude(worldDirection, 1f);
        }

        private void LateUpdate()
        {
            if (!IsAlive || Definition == null || motor == null || !motor.enabled) return;
            if (Director == null || !Director.IsBattleRunning)
            {
                requestedMove = Vector3.zero;
                PlanarVelocity = Vector3.zero;
                knockbackVelocity = Vector3.zero;
                return;
            }

            if (UpdateHandMotion(Time.deltaTime)) return;

            if (motor.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity = Mathf.Max(-35f, verticalVelocity - 20f * Time.deltaTime);
            if (Definition.IsFlying) verticalVelocity = Mathf.Clamp((FlightAltitude - transform.position.y) * 3, -9, 9);
            Vector3 velocity = requestedMove * Mathf.Max(0.1f, Definition.CombatMoveSpeed);
            if (IsOverdriving) velocity *= overdriveSpeed;
            if (IsSlowed) velocity *= .6f;
            // Consume the dash in the motor, including its partial final frame.
            // Ticking it before movement discarded distance on slow frames.
            float dashStep = Mathf.Min(Time.deltaTime, dashRemaining);
            Vector3 dashMove = dashDirection * (dashSpeed * dashStep);
            dashRemaining = Mathf.Max(0, dashRemaining - Time.deltaTime);
            velocity.y = verticalVelocity;
            // Integrate exponential damping so an impact has the same short push
            // at different frame rates. CharacterController performs all collision.
            float decay = Mathf.Exp(-KnockbackDamping * Time.deltaTime);
            Vector3 impactMove = knockbackVelocity * ((1f - decay) / KnockbackDamping);
            knockbackVelocity *= decay;
            if (knockbackVelocity.sqrMagnitude < 0.0001f) knockbackVelocity = Vector3.zero;
            Vector3 previousPosition = transform.position;
            Vector3 movement = velocity * Time.deltaTime + impactMove + dashMove;
            // Apply the ceiling in the shared motor, so AI, possession and
            // upward knockback cannot bypass the class's altitude limit.
            if (Definition.IsFlying) movement.y = Mathf.Min(movement.y, Definition.MaximumFlightHeight - transform.position.y);
            CollisionFlags collisions = motor.Move(movement);
            PlanarVelocity = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up) / Mathf.Max(.0001f, Time.deltaTime);
            if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f) verticalVelocity = -2f;
            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            if (dashStep > 0) DamageDashContacts();
            requestedMove = Vector3.zero;
        }

        public void Face(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        }

        public bool TryAttack(Vector3 origin, Vector3 direction)
        {
            if (!CanAct() || IsHandControlled || AttackCooldownRemaining > 0f || direction.sqrMagnitude < 0.001f) return false;
            if (!HasClearAttackOrigin(origin)) return false;
            direction.Normalize();
            AttackCooldownRemaining = Mathf.Max(0.05f, Definition.CombatAttackCooldown * (IsOverdriving ? overdriveAttack : 1f));
            Director.NotifyAttack(this);
            if (IsMelee)
            {
                Flash(new Color(1f, 0.7f, 0.2f), 0.12f);
                BattleUnit victim;
                float swingRadius = Definition.Role == UnitRole.Brute ? 0.5f : 0.35f;
                if (TraceTarget(origin, direction, Mathf.Max(0.1f, Definition.AttackRange), swingRadius, out victim))
                    victim.TakeDamage(Definition.Damage, this, direction, Definition.Role == UnitRole.Brute ? 6f : 4.5f);
                else TryMeleeTower(origin, direction, swingRadius);
            }
            else
            {
                FireProjectile(origin, direction, Definition.Damage);
            }
            return true;
        }

        public bool TryAbility(Vector3 origin, Vector3 direction)
            => TryAbility(0, origin, direction);

        public bool TryAbility(int slot, Vector3 origin, Vector3 direction)
        {
            if (!CanAct() || IsHandControlled || slot < 0 || slot >= Definition.AbilityCount || GetAbilityCooldownRemaining(slot) > 0f) return false;
            if (float.IsNaN(direction.sqrMagnitude) || float.IsInfinity(direction.sqrMagnitude)) return false;
            bool aimed = Definition.Role == UnitRole.Ranger || (Definition.Role == UnitRole.Stormcaller && slot == 0) || (Definition.Role == UnitRole.Emberwing && slot == 1);
            if (aimed && !HasClearAttackOrigin(origin)) return false;
            if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
            direction.Normalize();
            if (Definition.IsFlying)
            {
                UseFlyingAbility(slot, origin, direction);
                SetAbilityCooldown(slot); return true;
            }
            if ((int)Definition.Role >= (int)UnitRole.Wisp)
            {
                if (!UseExpandedAbility(slot, origin, direction)) return false;
                SetAbilityCooldown(slot);
                Flash(BattleClassCatalog.Color(Definition.Role), .25f);
                return true;
            }
            BattleUnit chainTarget = null;
            if (Definition.Role == UnitRole.Stormcaller && slot == 0 &&
                !TraceTarget(origin, direction, Definition.AttackRange + 2f, .22f, out chainTarget)) return false;
            SetAbilityCooldown(slot);
            if (slot == 1) { UseSecondary(origin, direction); ShowAbilityPulse(slot); return true; }
            float radius = Mathf.Max(0.1f, Definition.AbilityRadius);

            switch (Definition.Role)
            {
                case UnitRole.Vanguard:
                    guardRemaining = 2.5f;
                    Flash(new Color(1f, 0.82f, 0.2f), 0.25f);
                    Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                    if (forward.sqrMagnitude < 0.01f) forward = transform.forward;
                    foreach (var other in Director.Units)
                    {
                        if (other == null || !other.IsAlive || other.Team == Team) continue;
                        Vector3 offset = other.transform.position - transform.position;
                        offset.y = 0f;
                        if (offset.sqrMagnitude <= radius * radius &&
                            Vector3.Dot(forward, offset.normalized) >= -0.1f && HasLineOfSight(other))
                            other.TakeDamage(Definition.AbilityPower, this, offset, 5.5f);
                    }
                    break;
                case UnitRole.Ranger:
                    Flash(new Color(1f, 0.82f, 0.2f), 0.25f);
                    // Horizontal spread in the camera's aim plane preserves vertical aiming.
                    Vector3 spreadAxis = Vector3.Cross(direction, Vector3.up).normalized;
                    if (spreadAxis.sqrMagnitude < 0.01f) spreadAxis = transform.right;
                    for (int shot = -1; shot <= 1; shot++)
                    {
                        Vector3 shotDirection = Quaternion.AngleAxis(shot * 7f, Vector3.Cross(spreadAxis, direction)) * direction;
                        FireProjectile(origin, shotDirection, Definition.AbilityPower);
                    }
                    break;
                case UnitRole.Medic:
                    Flash(new Color(0.4f, 1f, 0.45f), 0.25f);
                    foreach (var other in Director.Units)
                    {
                        if (other == null || !other.IsAlive || other.Team != Team) continue;
                        if ((other.transform.position - transform.position).sqrMagnitude <= radius * radius)
                            other.Heal(Definition.AbilityPower);
                    }
                    break;
                case UnitRole.Skirmisher:
                    ReceiveOverdrive(Mathf.Max(.1f, Definition.AbilityPower), Mathf.Max(1f, Definition.AbilityRadius), .6f);
                    Flash(Color.cyan, 0.25f);
                    break;
                case UnitRole.Brute:
                    Flash(new Color(1f, 0.5f, 0.15f), 0.3f);
                    foreach (var other in Director.Units)
                    {
                        if (other == null || !other.IsAlive || other.Team == Team) continue;
                        Vector3 offset = other.transform.position - transform.position;
                        offset.y = 0f;
                        if (offset.sqrMagnitude <= radius * radius && HasLineOfSight(other))
                            other.TakeDamage(Definition.AbilityPower, this, offset, 7f);
                    }
                    break;
                case UnitRole.Engineer:
                    foreach (var other in Director.Units)
                    {
                        if (!NearbyAlly(other, radius)) continue;
                        other.Heal(Definition.AbilityPower);
                        other.GrantShield(18f, 6f);
                    }
                    Flash(new Color(1f, .8f, .3f), .3f);
                    break;
                case UnitRole.Stormcaller:
                    ChainLightning(chainTarget, origin);
                    Flash(new Color(.75f, .5f, 1f), .3f);
                    break;
            }
            ShowAbilityPulse(0);
            return true;
        }

        void SetAbilityCooldown(int slot)
        {
            Director.NotifyAbility(this,slot);
            if (slot == 0) AbilityCooldownRemaining = Definition.GetAbilityCooldown(slot);
            else if (slot == 1) SecondaryAbilityCooldownRemaining = Definition.GetAbilityCooldown(slot);
            else additionalCooldowns[slot - 2] = Definition.GetAbilityCooldown(slot);
        }

        private void ShowAbilityPulse(int slot)
        {
            if (Definition.Role == UnitRole.Ranger || Definition.Role == UnitRole.Stormcaller && slot == 0) return;
            bool area = Definition.Role == UnitRole.Medic || Definition.Role == UnitRole.Engineer ||
                Definition.Role == UnitRole.Brute || Definition.Role == UnitRole.Stormcaller;
            Director.GetComponent<BattleEffects>()?.Pulse(transform.position,
                area ? Definition.AbilityRadius : .7f, BattleClassCatalog.Color(Definition.Role));
        }

        private bool NearbyAlly(BattleUnit other, float radius) => other != null && other.IsAlive &&
            other.Team == Team && (other.transform.position - transform.position).sqrMagnitude <= radius * radius;

        private void UseSecondary(Vector3 origin, Vector3 direction)
        {
            float radius = Mathf.Max(.1f, Definition.AbilityRadius);
            switch (Definition.Role)
            {
                case UnitRole.Vanguard:
                    GrantShield(40f, 6f);
                    guardRemaining = Mathf.Max(guardRemaining, 3f);
                    break;
                case UnitRole.Ranger:
                    FireProjectile(origin, direction, Definition.Damage * 2.4f, 1.5f, 1.4f);
                    break;
                case UnitRole.Medic:
                    foreach (var other in Director.Units)
                        if (NearbyAlly(other, radius)) other.GrantShield(35f, 6f);
                    break;
                case UnitRole.Skirmisher:
                    BeginDash(direction, .35f, 7, 0);
                    break;
                case UnitRole.Brute:
                    Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                    if (forward.sqrMagnitude < .01f) forward = transform.forward;
                    foreach (var other in Director.Units)
                    {
                        if (other == null || !other.IsAlive || other.Team == Team) continue;
                        Vector3 offset = Vector3.ProjectOnPlane(other.transform.position - transform.position, Vector3.up);
                        if (offset.sqrMagnitude <= 64f && Vector3.Dot(forward, offset.normalized) >= .65f && HasLineOfSight(other))
                            other.TakeDamage(Definition.Damage * .8f, this, offset, 8f);
                    }
                    break;
                case UnitRole.Engineer:
                    foreach (var other in Director.Units)
                        if (NearbyAlly(other, radius)) other.ReceiveOverdrive(4f, 1.2f, .75f);
                    break;
                case UnitRole.Stormcaller:
                    foreach (var other in Director.Units)
                    {
                        if (other == null || !other.IsAlive || other.Team == Team ||
                            (other.transform.position - transform.position).sqrMagnitude > radius * radius || !HasLineOfSight(other)) continue;
                        other.TakeDamage(16f, this, other.transform.position - transform.position, 6f);
                        if (other.IsAlive) other.slowRemaining = Mathf.Max(other.slowRemaining, 3f);
                    }
                    break;
            }
            Flash(BattleClassCatalog.Color(Definition.Role), .3f);
        }

        public void GrantShield(float amount, float duration)
        {
            if (!CanAct() || amount <= 0f || duration <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            Shield = Mathf.Max(Shield, Mathf.Min(Mathf.Max(80, MaxHealth * .5f), amount));
            shieldRemaining = Mathf.Max(shieldRemaining, duration);
            Flash(new Color(.35f, .7f, 1f), .25f);
        }

        private void ReceiveOverdrive(float duration, float speed, float attack)
        {
            if (!IsOverdriving) { overdriveSpeed = 1f; overdriveAttack = 1f; }
            overdriveRemaining = Mathf.Max(overdriveRemaining, duration);
            overdriveSpeed = Mathf.Max(overdriveSpeed, speed);
            float previous = overdriveAttack;
            overdriveAttack = Mathf.Min(overdriveAttack, attack);
            AttackCooldownRemaining *= overdriveAttack / previous;
            Flash(Color.cyan, .25f);
        }

        private void ChainLightning(BattleUnit first, Vector3 origin)
        {
            var struck = new System.Collections.Generic.List<BattleUnit>(3);
            BattleUnit current = first;
            Vector3 from = origin;
            for (int jump = 0; jump < 3 && current != null; jump++)
            {
                struck.Add(current);
                Vector3 to = current.EyePosition - Vector3.up * .3f;
                Director.GetComponent<BattleEffects>()?.Lightning(from + (to - from).normalized * .3f, to);
                from = to;
                current.TakeDamage(Definition.AbilityPower * Mathf.Pow(.8f, jump), this,
                    current.transform.position - transform.position, 2f);
                BattleUnit next = null;
                float nearest = 4f * 4f;
                foreach (var other in Director.Units)
                {
                    if (other == null || !other.IsAlive || other.Team == Team || struck.Contains(other)) continue;
                    float distance = (other.transform.position - current.transform.position).sqrMagnitude;
                    if (distance >= nearest || !ClearArc(current.EyePosition, other.EyePosition)) continue;
                    next = other;
                    nearest = distance;
                }
                current = next;
            }
        }

        private static bool ClearArc(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<BattleUnit>() == null) return false;
            return true;
        }

        private bool CanAct()
        {
            return IsAlive && Definition != null && Director != null && Director.IsBattleRunning;
        }

        private bool HasClearAttackOrigin(Vector3 origin)
        {
            Vector3 offset = origin - EyePosition;
            if (float.IsNaN(offset.sqrMagnitude) || float.IsInfinity(offset.sqrMagnitude)) return false;
            float distance = offset.magnitude;
            if (distance < 0.01f) return true;
            const float clearance = 0.06f;
            // A controller can stay in a player's real hand past a virtual wall.
            // Test the whole eye-to-hand segment and its end so clipping a hand
            // into (or through) cover never creates a muzzle on the far side.
            foreach (Collider overlap in Physics.OverlapSphere(origin, clearance, ~0, QueryTriggerInteraction.Ignore))
                if (overlap.GetComponentInParent<BattleUnit>() == null) return false;
            foreach (RaycastHit hit in Physics.SphereCastAll(EyePosition, clearance, offset / distance,
                distance, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<BattleUnit>() == null) return false;
            return true;
        }

        private void FireProjectile(Vector3 origin, Vector3 direction, float damage, float rangeMultiplier = 1f, float speedMultiplier = 1f)
        {
            Color color = Definition.Role == UnitRole.Medic ? new Color(0.4f, 1f, 0.45f)
                : Definition.Role == UnitRole.Skirmisher ? Color.cyan : (int)Definition.Role>=16?BattleClassCatalog.Color(Definition.Role):new Color(1f, 0.85f, 0.25f);
            BattleProjectile.Spawn(Director, this, origin, direction, Mathf.Max(0f, damage),
                Mathf.Max(0.1f, Definition.CombatProjectileSpeed * speedMultiplier), Mathf.Max(0.1f, Definition.AttackRange * rangeMultiplier), color);
            Director.GetComponent<BattleEffects>()?.Muzzle(origin, direction, color);
            recoilRemaining = RecoilDuration;
            RefreshRecoil();
            ShotFired?.Invoke();
        }

        public bool HasLineOfSight(BattleUnit other)
        {
            if (other == null || !other.IsAlive) return false;
            return ClearSightTo(other, EyePosition, other.TargetPointFor(this));
        }

        bool ClearSightTo(BattleUnit other, Vector3 origin, Vector3 targetPosition)
        {
            BattleDirector.SynchronizeSightPhysics();
            // A ray starting inside a collider can miss that collider entirely.
            if (!IsSightOriginClear(origin)) return false;
            Vector3 direction = targetPosition - origin;
            float distance = direction.magnitude;
            if (distance < 0.01f) return true;
            int count = Physics.RaycastNonAlloc(origin, direction / distance, sightHits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            float nearestObstacle = float.PositiveInfinity;
            float nearestTarget = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                var unit = hit.collider.GetComponentInParent<BattleUnit>();
                // Terrain blocks the full eye-to-body segment, even if the front
                // of a target's capsule protrudes in front of a thin wall.
                if (unit == null) return false;
                if (other == null) continue;
                if (unit == this || (unit != null && (!unit.IsAlive || unit.Team == Team))) continue;
                if (unit == other) nearestTarget = Mathf.Min(nearestTarget, hit.distance);
                else nearestObstacle = Mathf.Min(nearestObstacle, hit.distance);
            }
            if (nearestObstacle < Mathf.Min(nearestTarget, distance) - .005f) return false;
            // Check from the other end as well: back-facing mesh triangles must
            // not turn a wall into a one-way visibility portal.
            count = Physics.RaycastNonAlloc(targetPosition, -direction / distance, sightHits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (sightHits[i].collider.GetComponentInParent<BattleUnit>() == null) return false;
            return true;
        }

        private bool TraceTarget(Vector3 origin, Vector3 direction, float distance, float radius, out BattleUnit victim)
        {
            victim = null;
            float nearestDistance = float.PositiveInfinity;
            var hits = Physics.SphereCastAll(origin, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var unit = hit.collider.GetComponentInParent<BattleUnit>();
                if (unit == this || (unit != null && (!unit.IsAlive || unit.Team == Team))) continue;
                if (hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                victim = unit;
            }
            return victim != null;
        }

        public void TakeDamage(float amount, BattleUnit source)
        {
            Vector3 direction = source != null ? transform.position - source.transform.position : Vector3.zero;
            TakeDamage(amount, source, direction);
        }

        public void TakeDamage(float amount, BattleUnit source, Vector3 hitDirection, float knockback = 3f)
        {
            if (!CanAct() || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            if (source != null && source.Team == Team) return;
            float damage = amount * (1f - Mathf.Clamp(Definition.ArmorFraction, 0f, 0.85f));
            if (IsGuarding) damage *= 0.4f;
            float absorbed = Mathf.Min(Shield, damage);
            Shield -= absorbed;
            damage -= absorbed;
            Health = Mathf.Max(0f, Health - damage);
            LastDamagedAt=Time.time;
            LastHitDirection = Vector3.ProjectOnPlane(hitDirection, Vector3.up).normalized;
            Director.NotifyDamage(this,source,damage+absorbed);
            Director.GetComponent<BattleEffects>()?.Impact(EyePosition - Vector3.up * .4f,
                -hitDirection.normalized, new Color(.8f, .68f, .45f));
            Flash(Color.white, 0.14f);
            if (Health <= 0f) Die();
            else ApplyKnockback(hitDirection, knockback);
        }

        private void ApplyKnockback(Vector3 direction, float strength)
        {
            direction.y = 0f;
            if (float.IsNaN(direction.sqrMagnitude) || float.IsInfinity(direction.sqrMagnitude) ||
                float.IsNaN(strength) || float.IsInfinity(strength) || direction.sqrMagnitude < 0.0001f || strength <= 0f) return;
            float resistance = Definition.Role == UnitRole.Titan ? .4f : Definition.Role == UnitRole.Brute || Definition.Role == UnitRole.Sentinel ? .65f : 1f;
            if (IsGuarding) resistance *= 0.5f;
            knockbackVelocity = Vector3.ClampMagnitude(knockbackVelocity + direction.normalized * strength * resistance, 8f);
        }

        /// <summary>Collision-safe displacement without a hit, damage, shield loss or damage notification.</summary>
        public bool Repel(Vector3 direction, float distance)
        {
            direction.y = 0;
            if (!CanAct() || !float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < .0001f ||
                !float.IsFinite(distance) || distance <= 0) return false;
            knockbackVelocity = direction.normalized * Mathf.Min(80, distance * KnockbackDamping);
            return true;
        }

        public void ApplySlow(float duration)
        {
            if (!CanAct() || !float.IsFinite(duration) || duration <= 0) return;
            slowRemaining = Mathf.Max(slowRemaining, duration);
        }

        public bool BreakShield()
        {
            if (!CanAct() || Shield <= 0) return false;
            Shield = shieldRemaining = 0;
            Flash(new Color(.8f, .5f, 1), .25f);
            return true;
        }

        public void Heal(float amount)
        {
            if (!CanAct() || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            Flash(new Color(0.4f, 1f, 0.45f), 0.22f);
        }

        private void Die()
        {
            IsHandHeld = IsThrown = false;
            IsAlive = false;
            IsPossessed = false;
            requestedMove = Vector3.zero;
            guardRemaining = 0f;
            overdriveRemaining = 0f;
            Shield = shieldRemaining = slowRemaining = dashRemaining = 0f;
            knockbackVelocity = Vector3.zero;
            recoilRemaining = 0f;
            ResetExpandedState();
            RefreshRecoil();
            if (motor != null) motor.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            RefreshVisuals();
            if (visualRenderers == null && VisualRoot != null) VisualRoot.gameObject.SetActive(false);
            if (Director != null) Director.NotifyUnitDied(this);
            if (Director != null && Director.Towers != null)
            {
                Destroy(gameObject, 2f);
            }
        }
    }
}
