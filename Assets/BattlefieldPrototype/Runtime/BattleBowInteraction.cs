using UnityEngine;
using UnityEngine.InputSystem;

namespace BattlefieldPrototype
{
    public sealed partial class PossessionController
    {
        readonly BowDrawGesture bowDraw = new BowDrawGesture();
        bool desktopBowHeld, desktopBowButtonWasHeld, bowPoseTracked;
        float desktopBowStarted;
        Pose bowPose;
        bool bowFullDrawPulsed;
        readonly HandActionGate easyBowGate = new HandActionGate();
        bool easyBowHeld;
        Ray heldBowAim;
        float heldBowPower;
        bool hasHeldBowAim;
        Vector3 steadyBowDirection;
        public bool PhysicalHandBow { get; set; } = true;
        internal bool UsesEasyHandBow => IsUsingOpticalHands && !PhysicalHandBow &&
            director?.Towers != null && director.Towers.CanUseSummitActions && !director.Towers.SelectedSummitPower.HasValue && !director.Towers.SelectedDefense.HasValue;
        public bool IsDrawingBow => bowDraw.Drawing || desktopBowHeld;
        public float BowPull => bowDraw.Drawing ? bowDraw.Pull : desktopBowHeld ? Mathf.Min(.4f, (Time.unscaledTime - desktopBowStarted) * .5f) : 0;
        internal float BowStringDistance => bowDraw.Drawing ? bowDraw.StringDistance : BowDrawGesture.RestDistance + BowPull;
        internal bool TryBowPose(out Pose pose) { pose = bowPose; return bowPoseTracked; }
        internal bool TryBowTrajectory(out Ray ray, out float power)
        { ray = heldBowAim; power = heldBowPower; return bowDraw.Drawing && hasHeldBowAim; }

        internal bool WantsPhysicalBowInput
        {
            get
            {
                var siege = director != null ? director.Towers : null;
                if (UsesEasyHandBow || !IsUsingXR || IsMouseLookCaptured || siege == null || !siege.CanUseSummitActions ||
                    siege.SelectedSummitPower.HasValue || siege.SelectedDefense.HasValue || siege.IsPlanningRoute || switchPending ||
                    GetComponent<TowerBattleHud>()?.BlocksGameplayInput == true || GetComponent<TowerBattleHud>()?.HandMenuVisible == true) return false;
                return bowDraw.Drawing || TryBowHand(true, out var left, out _) &&
                    TryBowHand(false, out var right, out bool held) && held && BowDrawGesture.CanNock(left.position, right.position);
            }
        }

        void UpdateBowInteraction()
        {
            var siege = director != null ? director.Towers : null;
            var hud = GetComponent<TowerBattleHud>();
            bool mouseHeld = !OpticalInputOwnsPointer && Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool allowed = siege != null && siege.CanUseSummitActions && !siege.SelectedSummitPower.HasValue &&
                !siege.SelectedDefense.HasValue && !siege.IsPlanningRoute && !switchPending && !opticalWaveHeld && hud?.BlocksGameplayInput != true &&
                hud?.HandMenuVisible != true && hud?.ControllerOverUI != true;
            bowPoseTracked = false;
            if (!allowed)
            { easyBowGate.Update(false, false); easyBowHeld = false; hasHeldBowAim = false; bowDraw.Cancel(); desktopBowHeld = false; desktopBowButtonWasHeld = mouseHeld; return; }
            if (UsesEasyHandBow)
            {
                bowDraw.Cancel(); hasHeldBowAim = false; desktopBowHeld = false;
                desktopBowButtonWasHeld = mouseHeld;
                bool tracked = TryOpticalHandPointer(out var aim, out bool pinch);
                bool quickHeld = easyBowGate.Update(tracked && hud?.ControllerOverUI != true && hud?.PointerOverUI != true, pinch);
                if (quickHeld && !easyBowHeld && siege.TryFireEasyBow(aim)) { PulseRight(.3f, .05f); ShotFired?.Invoke(); }
                easyBowHeld = quickHeld;
                if (TryOpticalHand(true, out var palm, out _, out _) || TryOpticalHand(false, out palm, out _, out _))
                { bowPoseTracked = true; bowPose = new Pose(palm.position, tracked ? Quaternion.LookRotation(aim.direction, viewCamera.transform.up) : palm.rotation); }
                return;
            }
            easyBowGate.Update(false, false); easyBowHeld = false;
            Pose leftPose = default, rightPose = default; bool drawHeld = false;
            bool left = IsUsingXR && TryBowHand(true, out leftPose, out _);
            bool right = IsUsingXR && TryBowHand(false, out rightPose, out drawHeld);
            bool nocking = left && right && drawHeld && BowDrawGesture.CanNock(leftPose.position, rightPose.position);
            if (ShouldUsePhysicalBow(left && right, nocking, mouseHeld))
            {
                desktopBowHeld = false; desktopBowButtonWasHeld = mouseHeld;
                UpdatePhysicalBow(left, leftPose, right, rightPose, drawHeld);
                return;
            }
            bowDraw.Cancel(); hasHeldBowAim = false;
            bool held = mouseHeld;
            if (hud?.PointerOverUI == true || IsMouseLookCaptured) { desktopBowHeld = false; desktopBowButtonWasHeld = held; return; }
            if (held && !desktopBowButtonWasHeld) { desktopBowHeld = true; desktopBowStarted = Time.unscaledTime; }
            else if (!held && desktopBowHeld)
            {
                desktopBowHeld = false;
                float time = Time.unscaledTime - desktopBowStarted;
                if (time >= .05f && siege.TryFireSummitBow(AimRay, Mathf.Lerp(.2f, 1, Mathf.Clamp01(time / .8f)))) ShotFired?.Invoke();
            }
            desktopBowButtonWasHeld = held;
        }

        bool ShouldUsePhysicalBow(bool handsTracked, bool nocking, bool mouseHeld) => IsUsingXR && !IsMouseLookCaptured &&
            (bowDraw.Drawing || !IsDesktopInputActive || handsTracked && (!mouseHeld || nocking));

        bool TryBowHand(bool left, out Pose pose, out bool held)
        {
            held = false;
            // The bow hand only needs its palm. Gripping naturally hides thumb
            // and index joints; those joints are not required to hold the bow.
            if (left && TryOpticalJoint(true, UnityEngine.XR.Hands.XRHandJointID.Palm, out pose)) return true;
            return TryPhysicalHand(left, out pose, out _, out held);
        }

        void UpdatePhysicalBow(bool leftTracked, Pose left, bool rightTracked, Pose right, bool held)
        {
            bool drawing = bowDraw.Drawing;
            // Proximity decides nocking; a HUD hit must not latch out a later pull.
            // Actual modals, selected powers and lost tracking still cancel normally.
            bool fire = bowDraw.Update(leftTracked && rightTracked, held,
                left.position, right.position, left.rotation * Vector3.forward, out float power);
            bool fullDraw = bowDraw.Drawing && bowDraw.Pull >= BowDrawGesture.FullDraw * .99f;
            if (fullDraw && !bowFullDrawPulsed) PulseRight(.3f, .05f);
            bowFullDrawPulsed = fullDraw;
            bowPoseTracked = leftTracked;
            Vector3 direction = rightTracked && (drawing || bowDraw.Drawing) ? left.position - right.position : viewCamera.transform.forward;
            if (direction.sqrMagnitude < .001f) direction = viewCamera.transform.forward;
            direction.Normalize();
            if (bowDraw.Drawing && held)
            {
                // A short filter damps tracking jitter without changing two-hand aim.
                if (!drawing || !hasHeldBowAim) steadyBowDirection = direction;
                else if (Vector3.Angle(steadyBowDirection, direction) > .25f)
                    steadyBowDirection = Vector3.Slerp(steadyBowDirection, direction, 1 - Mathf.Exp(-Time.unscaledDeltaTime / .045f)).normalized;
                heldBowAim = new Ray(left.position, steadyBowDirection);
                heldBowPower = Mathf.Lerp(.2f, 1, Mathf.Clamp01(bowDraw.Pull / BowDrawGesture.FullDraw));
                hasHeldBowAim = true;
            }
            // Opening a pinch can shift the tracked palm. Release exactly the
            // last drawn ray/power shown in the trajectory, not that relaxed pose.
            Ray shot = hasHeldBowAim ? heldBowAim : new Ray(left.position, direction);
            if (hasHeldBowAim) direction = heldBowAim.direction;
            bowPose = new Pose(left.position, Quaternion.LookRotation(direction, viewCamera.transform.up));
            if (fire && hasHeldBowAim && director.Towers.TryFireSummitBow(shot, heldBowPower))
            { PulseBoth(.3f, .06f); ShotFired?.Invoke(); }
            if (!bowDraw.Drawing) hasHeldBowAim = false;
        }
    }
}
