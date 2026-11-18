using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace BattlefieldPrototype
{
    public sealed partial class PossessionController
    {
        public const string GesturesKey = "Skyhold.VR.Gestures";
        public const string HapticsKey = "Skyhold.VR.Haptics";
        readonly TowerWaveGesture waveGesture = new TowerWaveGesture();
        bool opticalWaveHeld;
        public bool WaveGestureCharging => waveGesture.IsCharging;
        public bool ControllerGesturesEnabled => PlayerPrefs.GetInt(GesturesKey, 1) != 0;
        public bool ControllerHapticsEnabled => PlayerPrefs.GetInt(HapticsKey, 1) != 0;
        public void ToggleControllerGestures()
        { PlayerPrefs.SetInt(GesturesKey, ControllerGesturesEnabled ? 0 : 1); PlayerPrefs.Save(); waveGesture.Reset(); }
        public void ToggleControllerHaptics()
        { PlayerPrefs.SetInt(HapticsKey, ControllerHapticsEnabled ? 0 : 1); PlayerPrefs.Save(); }
        public void PulseRight(float strength, float duration)
        { if (IsUsingXR && ControllerHapticsEnabled) TouchInput.Right.Pulse(strength, duration); }
        public void PulseBoth(float strength, float duration)
        {
            if (!IsUsingXR || !ControllerHapticsEnabled) return;
            TouchInput.Left.Pulse(strength, duration); TouchInput.Right.Pulse(strength, duration);
        }
        void ControllerUnitAttacked(BattleUnit unit)
        { if (unit == CurrentUnit) PulseRight(.22f, .045f); }
        void ControllerAbilityActivated(BattleUnit unit, int slot)
        { if (unit == CurrentUnit) PulseBoth(.45f, .12f); }
        void ControllerUnitDamaged(BattleUnit target, BattleUnit source, float damage)
        { if (target == CurrentUnit && damage > 0) PulseBoth(.4f, .09f); }
        void ControllerPossessionChanged() => PulseBoth(.22f, .08f);
        internal bool TryControllerGripPose(bool left, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            if (viewCamera == null) return false;
            if (!(left ? TouchInput.Left : TouchInput.Right).TryPose(false, out var localPosition, out var localRotation) ||
                !TryHeadPose(out var headPosition, out var headRotation)) return false;
            var trackingToWorld = viewCamera.transform.rotation * Quaternion.Inverse(headRotation);
            var origin = viewCamera.transform.position - trackingToWorld * headPosition;
            position = origin + trackingToWorld * localPosition; rotation = trackingToWorld * localRotation;
            return true;
        }
        internal bool TryOpticalHandPalm(bool left, out Vector3 position)
        {
            position = default;
            if (!TryOpticalJoint(left, XRHandJointID.Palm, out var palm)) return false;
            position = palm.position; return true;
        }
        internal void ResetHandPointer() => opticalPointer.Reset();
        internal void TranslateHandPointer(Vector3 offset) => opticalPointer.Translate(offset);
        internal bool TryOpticalHandPointer(out Ray ray, out bool pinching, bool updateLatch = true)
        {
            var hud = GetComponent<TowerBattleHud>();
            bool tracked = TryOpticalPointing(hud != null && hud.HandMenuVisible && !hud.MenuNeedsRecall, out var live, out pinching);
            bool preparing = tracked && (TryNativeHandAim(false, out _, out float strength) ? strength > .45f :
                TryOpticalJoint(false, XRHandJointID.IndexTip, out var index) &&
                TryOpticalJoint(false, XRHandJointID.ThumbTip, out var thumb) && Vector3.Distance(index.position, thumb.position) < .04f);
            ray = updateLatch ? opticalPointer.Update(tracked, pinching, live, Time.frameCount, preparing) : opticalPointer.RenderRay(live);
            return tracked;
        }
        void UpdateWaveGesture(ref bool release, ref bool ability)
        {
            bool optical = IsUsingOpticalHands;
            Vector3 leftPosition, rightPosition;
            bool leftTracked, rightTracked, both;
            if (optical)
            {
                leftTracked = TryOpticalHand(true, out var left, out _, out bool leftPinch);
                rightTracked = TryOpticalHand(false, out var right, out _, out bool rightPinch);
                leftPosition = left.position; rightPosition = right.position; both = leftPinch && rightPinch;
            }
            else
            {
                leftTracked = TryControllerGripPose(true, out leftPosition, out _);
                rightTracked = TryControllerGripPose(false, out rightPosition, out _);
                both = TouchInput.Left.GripHeld && TouchInput.Right.GripHeld;
            }
            bool allowed = IsUsingXR && !IsDesktopInputActive && ControllerGesturesEnabled && (!optical || !PhysicalHandBow) &&
                !switchPending && director != null && director.Towers != null && director.Towers.CanUseSummitActions &&
                !director.Towers.IsPlanningRoute && !director.Towers.SelectedSummitPower.HasValue && !IsDrawingBow && leftTracked && rightTracked &&
                GetComponent<TowerBattleHud>()?.HandMenuVisible != true &&
                GetComponent<TowerBattleHud>()?.BlocksGameplayInput != true && GetComponent<TowerBattleHud>()?.ControllerOverUI != true;
            opticalWaveHeld = allowed && optical && both;
            bool wasCharging = waveGesture.IsCharging;
            bool fired = waveGesture.Update(allowed, both, leftPosition, rightPosition, Time.unscaledTime);
            if (allowed && both) { release = false; ability = false; }
            if (!wasCharging && waveGesture.IsCharging) PulseBoth(.16f, .07f);
            if (fired) extraAbilityMask |= 1 << 3;
        }
    }
}
