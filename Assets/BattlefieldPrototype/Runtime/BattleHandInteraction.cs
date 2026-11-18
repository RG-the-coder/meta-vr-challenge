using UnityEngine;
using UnityEngine.XR.Hands;

namespace BattlefieldPrototype
{
    public sealed partial class PossessionController
    {
        readonly BattleUnit[] handUnits = new BattleUnit[2];
        readonly Vector3[] handOffsets = new Vector3[2], lastHandPositions = new Vector3[2], handVelocities = new Vector3[2];
        readonly bool[] handGrabWasHeld = new bool[2], handPoseValid = new bool[2], handGrabNeedsRelease = new bool[2];
        public bool IsHoldingCharacter => handUnits[0] != null || handUnits[1] != null;
        bool GroundHandsAllowed => IsUsingXR && director != null && director.IsBattleRunning && director.Towers != null &&
            !director.Towers.IsMounted && !switchPending &&
            (IsInGroundCommandView || CurrentUnit != null && CurrentUnit.IsAlive && !CurrentUnit.Definition.IsFlying && CurrentUnit.transform.position.y < .8f) &&
            GetComponent<TowerBattleHud>()?.BlocksGameplayInput != true;

        internal bool TryPhysicalHand(bool left, out Pose pose, out bool grip, out bool draw)
        {
            if (TryOpticalHand(left, out pose, out _, out bool pinch))
            { grip = draw = pinch; return true; }
            if (TryControllerGripPose(left, out var position, out var rotation))
            {
                pose = new Pose(position, rotation);
                var input = left ? TouchInput.Left : TouchInput.Right;
                grip = input.GripHeld; draw = input.TriggerHeld;
                return true;
            }
            pose = default; grip = draw = false;
            return false;
        }

        void UpdateGroundHands()
        {
            bool allowed = GroundHandsAllowed;
            if (!allowed) { ReleaseAllHandUnits(); return; }
            var hud = GetComponent<TowerBattleHud>();
            for (int i = 0; i < 2; i++)
            {
                bool tracked = TryPhysicalHand(i == 0, out var pose, out bool grip, out _);
                if (!tracked || !allowed)
                {
                    if (handUnits[i] != null) handUnits[i].ReleaseHandHold(Vector3.zero, false);
                    handUnits[i] = null; handPoseValid[i] = false; handGrabNeedsRelease[i] = true;
                    handGrabWasHeld[i] = grip;
                    continue;
                }
                if (!grip) handGrabNeedsRelease[i] = false;
                Vector3 delta = pose.position - lastHandPositions[i];
                var velocity = handPoseValid[i] && delta.sqrMagnitude < 1
                    ? delta / Mathf.Max(.001f, Time.unscaledDeltaTime) : Vector3.zero;
                handVelocities[i] = Vector3.Lerp(handVelocities[i], Vector3.ClampMagnitude(velocity, 15), .5f);
                lastHandPositions[i] = pose.position; handPoseValid[i] = true;
                var held = handUnits[i];
                if (held != null && (!held.IsAlive || !held.IsHandHeld)) { held = null; handUnits[i] = null; }
                if (held != null)
                {
                    if (!grip)
                    { held.ReleaseHandHold(handVelocities[i], true); handUnits[i] = null; PulseBoth(.15f, .04f); }
                    else held.MoveHandHeld(pose.position + pose.rotation * handOffsets[i], Time.deltaTime);
                }
                else if (grip && !handGrabWasHeld[i] && !handGrabNeedsRelease[i] && hud?.ControllerOverUI != true)
                {
                    var target = FindHandGrabTarget(pose.position);
                    if (target != null && target.BeginHandHold())
                    {
                        handUnits[i] = target; handOffsets[i] = Quaternion.Inverse(pose.rotation) * (target.transform.position - pose.position);
                        handVelocities[i] = Vector3.zero; PulseBoth(.2f, .06f);
                    }
                }
                handGrabWasHeld[i] = grip;
            }
        }
        BattleUnit FindHandGrabTarget(Vector3 hand)
        {
            Vector3 eyeOffset = hand - viewCamera.transform.position;
            if (eyeOffset.sqrMagnitude > 6.25f) return null;
            foreach (var hit in Physics.RaycastAll(viewCamera.transform.position, eyeOffset.normalized, eyeOffset.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<BattleUnit>() == null) return null;
            BattleUnit best = null; float nearest = .28f;
            foreach (var unit in director.Units)
            {
                if (unit == null || !unit.IsAlive || unit.IsPossessed || unit.IsHandHeld) continue;
                var body = unit.Definition;
                Vector3 center = unit.transform.position + Vector3.up * Mathf.Clamp(hand.y - unit.transform.position.y, body.BodyRadius, body.BodyHeight - body.BodyRadius);
                float distance = Vector3.Distance(hand, center) - body.BodyRadius;
                if (distance >= nearest) continue;
                Vector3 offset = center - hand;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(hand, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.GetComponentInParent<BattleUnit>() == null) { blocked = true; break; }
                if (blocked) continue;
                best = unit; nearest = distance;
            }
            return best;
        }
        void ReleaseAllHandUnits()
        {
            for (int i = 0; i < 2; i++)
            {
                if (handUnits[i] != null) handUnits[i].ReleaseHandHold(Vector3.zero, false);
                handUnits[i] = null; handPoseValid[i] = false; handGrabNeedsRelease[i] = true;
            }
        }
    }
}
