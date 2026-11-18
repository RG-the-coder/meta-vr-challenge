using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace BattlefieldPrototype
{
    public interface IHandTrackingSource
    {
        bool IsTracked(bool left);
        bool TryGetJoint(bool left, XRHandJointID joint, out Pose pose);
    }

    public interface IHandAimSource
    {
        bool TryGetAim(bool left, out Pose aim, out float pinchStrength);
    }

    public sealed class OpenXrHandTrackingSource : IHandTrackingSource, IHandAimSource
    {
        readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        XRHandSubsystem subsystem;
        bool TryHand(bool left, out XRHand hand)
        {
            if (subsystem == null || !subsystem.running)
            {
                subsystem = null; SubsystemManager.GetSubsystems(subsystems);
                foreach (var candidate in subsystems) if (candidate.running) { subsystem = candidate; break; }
            }
            hand = subsystem == null ? default : left ? subsystem.leftHand : subsystem.rightHand;
            return subsystem != null && hand.isTracked;
        }
        public bool IsTracked(bool left) => TryHand(left, out _);
        public bool TryGetJoint(bool left, XRHandJointID joint, out Pose pose)
        {
            pose = default;
            return TryHand(left, out var hand) && hand.GetJoint(joint).TryGetPose(out pose);
        }
        public bool TryGetAim(bool left, out Pose aim, out float pinchStrength)
        {
            aim = default; pinchStrength = 0;
            var device = left ? MetaAimHand.left : MetaAimHand.right;
            if (device == null || !device.added || !device.isTracked.isPressed ||
                ((MetaAimFlags)device.aimFlags.ReadValue() & (MetaAimFlags.Valid | MetaAimFlags.SystemGesture)) != MetaAimFlags.Valid) return false;
            aim = new Pose(device.devicePosition.ReadValue(), device.deviceRotation.ReadValue());
            pinchStrength = device.pinchStrengthIndex.ReadValue();
            return float.IsFinite(aim.position.sqrMagnitude) && float.IsFinite(pinchStrength);
        }
    }

    public sealed class HandPinchGesture
    {
        bool needsRelease = true;
        public bool Held { get; private set; }
        public bool UpdateStrength(bool tracked, float strength) => Update(tracked && float.IsFinite(strength),
            strength >= (Held ? .55f : .8f) ? .02f : .07f);
        public bool Update(bool tracked, float distance)
        {
            if (!tracked || !float.IsFinite(distance)) { Held = false; needsRelease = true; return false; }
            if (needsRelease) { if (distance > .045f) needsRelease = false; return false; }
            Held = Held ? distance < .045f : distance <= .03f;
            return Held;
        }
    }

    public sealed class HandActionGate
    {
        bool needsRelease = true;
        public bool Update(bool allowed, bool pinch)
        {
            if (!allowed) { needsRelease = true; return false; }
            if (!pinch) { needsRelease = false; return false; }
            return !needsRelease;
        }
    }

    public sealed class HandDragMove
    {
        bool needsRelease = true;
        public bool Active { get; private set; }
        public Vector3 Origin { get; private set; }
        public Vector3 Update(bool allowed, bool pinch, Vector3 handInRig, Quaternion viewInRig)
        {
            if (!allowed || !float.IsFinite(handInRig.sqrMagnitude)) { Active = false; needsRelease = true; return Vector3.zero; }
            if (!pinch) { Active = false; needsRelease = false; return Vector3.zero; }
            if (needsRelease) return Vector3.zero;
            if (!Active) { Origin = handInRig; Active = true; }
            Vector3 forward = Vector3.ProjectOnPlane(viewInRig * Vector3.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 delta = Quaternion.Inverse(rotation) * (handInRig - Origin);
            Vector2 planar = new Vector2(delta.x, delta.z);
            planar = planar.magnitude <= .025f ? Vector2.zero : planar.normalized * Mathf.Clamp01((planar.magnitude - .025f) / .115f);
            float vertical = Mathf.Abs(delta.y) <= .04f ? 0 : Mathf.Sign(delta.y) * Mathf.Clamp01((Mathf.Abs(delta.y) - .04f) / .12f);
            return new Vector3(planar.x, vertical, planar.y);
        }
    }

    public sealed class HandPointerLatch
    {
        bool held;
        int releasedFrame = -1;
        Ray captured, hover;
        bool hasHover;
        public void Reset() { held = hasHover = false; releasedFrame = -1; }
        public void Translate(Vector3 offset)
        {
            captured.origin += offset;
            hover.origin += offset;
        }
        public Ray RenderRay(Ray live) => held ? captured : hasHover ? hover : live;
        public Ray Update(bool tracked, bool pinch, Ray live, int frame)
            => Update(tracked, pinch, live, frame, pinch);
        public Ray Update(bool tracked, bool pinch, Ray live, int frame, bool preparingPinch)
        {
            if (!tracked) { held = hasHover = false; releasedFrame = -1; return live; }
            if (pinch || preparingPinch)
            {
                // Closing the index finger must not shift the button being selected.
                if (!held) captured = hasHover ? hover : live;
                held = true; return captured;
            }
            if (held) { held = false; releasedFrame = frame; }
            hover = live; hasHover = true;
            return releasedFrame == frame ? captured : live;
        }
    }
}
