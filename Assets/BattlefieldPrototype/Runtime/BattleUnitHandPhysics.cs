using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class BattleUnit
    {
        public bool IsHandHeld { get; private set; }
        public bool IsThrown { get; private set; }
        public bool IsHandControlled => IsHandHeld || IsThrown;
        public float HandMass => Definition == null ? 1 : Mathf.Clamp(Mathf.Pow(Definition.BodyScale, 3), .25f, 5);
        Vector3 throwVelocity, heldVelocity;
        float throwAge, collisionSpeed;
        BattleUnit impactVictim;

        internal bool BeginHandHold()
        {
            if (!CanAct() || IsPossessed || IsHandHeld) return false;
            IsHandHeld = true; IsThrown = false; heldVelocity = throwVelocity = requestedMove = knockbackVelocity = Vector3.zero;
            verticalVelocity = dashRemaining = 0;
            return true;
        }
        internal void MoveHandHeld(Vector3 target, float dt)
        {
            if (!IsHandHeld || !IsAlive || !motor.enabled || dt <= 0 || !float.IsFinite(target.sqrMagnitude)) return;
            Vector3 before = transform.position;
            // Large bodies lag behind the hand; CharacterController sweeps keep them out of walls.
            motor.Move(Vector3.ClampMagnitude(target - before, 12f / Mathf.Sqrt(HandMass) * dt));
            heldVelocity = (transform.position - before) / dt;
            PlanarVelocity = Vector3.ProjectOnPlane(heldVelocity, Vector3.up);
        }
        internal void ReleaseHandHold(Vector3 handVelocity, bool throwFromHand)
        {
            if (!IsHandHeld) return;
            IsHandHeld = false;
            if (!IsAlive || Director == null || !Director.IsBattleRunning) { IsThrown = false; return; }
            Vector3 launch = throwFromHand && float.IsFinite(handVelocity.sqrMagnitude)
                ? Vector3.Lerp(heldVelocity, handVelocity, .4f) * (1.8f / Mathf.Sqrt(HandMass)) : Vector3.zero;
            throwVelocity = Vector3.ClampMagnitude(launch, 22);
            throwAge = 0; IsThrown = true;
        }
        bool UpdateHandMotion(float dt)
        {
            if (IsHandHeld) { requestedMove = Vector3.zero; return true; }
            if (!IsThrown) return false;
            requestedMove = Vector3.zero; throwAge += dt;
            throwVelocity += Vector3.down * (18 * dt);
            collisionSpeed = 0; impactVictim = null;
            var before = transform.position;
            var collisions = motor.Move(throwVelocity * dt);
            PlanarVelocity = Vector3.ProjectOnPlane(transform.position - before, Vector3.up) / Mathf.Max(.001f, dt);
            if (collisions != CollisionFlags.None && collisionSpeed > 0)
            {
                float damage = Mathf.Clamp(Mathf.Pow(Mathf.Max(0, collisionSpeed - 3), 2) * 1.8f * Mathf.Sqrt(HandMass), 0, MaxHealth * .9f);
                if (damage > 0)
                {
                    var victim = impactVictim;
                    TakeDamage(damage, null, throwVelocity, 0);
                    if (victim != null && victim.IsAlive && victim.Team != Team) victim.TakeDamage(damage * .5f, this, throwVelocity, 3);
                }
                throwVelocity *= .25f;
                if ((collisions & CollisionFlags.Below) != 0 || !IsAlive) FinishThrow();
            }
            if (throwAge > 5 || throwVelocity.sqrMagnitude < .1f) FinishThrow();
            return true;
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!IsThrown) return;
            float speed = Mathf.Max(0, -Vector3.Dot(throwVelocity, hit.normal));
            if (speed <= collisionSpeed) return;
            collisionSpeed = speed; impactVictim = hit.collider.GetComponentInParent<BattleUnit>();
        }
        void FinishThrow()
        {
            IsThrown = false; throwVelocity = Vector3.zero; verticalVelocity = -2;
            if (Definition.IsFlying) FlightAltitude = transform.position.y;
        }
    }
}
