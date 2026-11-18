using UnityEngine;
using UnityEngine.XR.Hands;

namespace BattlefieldPrototype
{
    public sealed partial class PossessionController
    {
        IHandTrackingSource opticalHandSource;
        IHandTrackingSource OpticalSource => opticalHandSource ??= new OpenXrHandTrackingSource();
        readonly HandPinchGesture leftOpticalPinch = new HandPinchGesture(), rightOpticalPinch = new HandPinchGesture();
        readonly HandActionGate opticalAction = new HandActionGate();
        readonly HandDragMove opticalMove = new HandDragMove();
        readonly HandPointerLatch opticalPointer = new HandPointerLatch();
        readonly HandActionGate handAbilityGate = new HandActionGate();
        BattleUnit handAbilityUnit;
        int handAbilitySlot = -1, pendingHandAbility = -1;
        bool handAbilityHeld;
        public int SelectedHandAbility => handAbilityUnit != null && handAbilityUnit == CurrentUnit &&
            handAbilityUnit.IsAlive && IsUsingXR && !switchPending && director != null && director.IsBattleRunning ? handAbilitySlot : -1;
        public bool IsUsingOpticalHands => IsUsingXR && (OpticalSource.IsTracked(true) || OpticalSource.IsTracked(false) ||
            TryNativeHandAim(false, out _, out _) || TryNativeHandAim(true, out _, out _));
        internal bool HasOpticalRightHand => IsUsingXR && (OpticalSource.IsTracked(false) || TryNativeHandAim(false, out _, out _));
        bool opticalInputFocus;
        // A short hand-tracking gap must not give a pinch-emulated mouse the UI
        // or camera. Only leaving XR or picking up a tracked controller hands it back.
        internal bool OpticalInputOwnsPointer
        {
            get
            {
                if (!IsUsingXR) opticalInputFocus = false;
                else if (IsUsingOpticalHands) opticalInputFocus = true;
                else if (HasTrackedAim) opticalInputFocus = false;
                return opticalInputFocus;
            }
        }
        internal bool OpticalHandOpen(bool left) =>
            TryOpticalJoint(left, XRHandJointID.IndexTip, out var index) &&
            TryOpticalJoint(left, XRHandJointID.ThumbTip, out var thumb) &&
            Vector3.Distance(index.position, thumb.position) > .055f;

        internal bool OpticalPalmOpen(bool left)
        {
            if (!OpticalHandOpen(left) || !TryOpticalJoint(left, XRHandJointID.Palm, out var palm)) return false;
            return ExtendedFromPalm(left, XRHandJointID.MiddleTip, palm.position) &&
                ExtendedFromPalm(left, XRHandJointID.RingTip, palm.position) &&
                ExtendedFromPalm(left, XRHandJointID.LittleTip, palm.position);
        }
        bool ExtendedFromPalm(bool left, XRHandJointID joint, Vector3 palm) =>
            TryOpticalJoint(left, joint, out var tip) && Vector3.Distance(palm, tip.position) >= .065f;

        internal bool TryOpticalJoint(bool left, XRHandJointID joint, out Pose pose)
        {
            pose = default;
            if (!IsUsingXR || viewCamera == null || rigRoot == null || !OpticalSource.IsTracked(left) ||
                !OpticalSource.TryGetJoint(left, joint, out var local) || !float.IsFinite(local.position.sqrMagnitude)) return false;
            pose = OpticalPoseToWorld(local); return true;
        }

        Pose OpticalPoseToWorld(Pose local)
        {
            Quaternion basis = rigRoot.rotation; Vector3 origin = rigRoot.position;
            if (ownsCamera && !IsMouseLookCaptured)
                origin = rigRoot.TransformPoint(Vector3.up * trackingHeightOffset);
            else if (TryHeadPose(out var headPosition, out var headRotation))
            {
                basis = viewCamera.transform.rotation * Quaternion.Inverse(headRotation);
                origin = viewCamera.transform.position - basis * headPosition;
            }
            return new Pose(origin + basis * local.position, basis * local.rotation);
        }

        bool TryNativeHandAim(bool left, out Pose pose, out float strength)
        {
            pose = default; strength = 0;
            if (!IsUsingXR || viewCamera == null || rigRoot == null || !(OpticalSource is IHandAimSource source) ||
                !source.TryGetAim(left, out var local, out strength)) return false;
            pose = OpticalPoseToWorld(local); return true;
        }

        bool TryOpticalPinch(bool left, out bool pinch)
        {
            var gesture = left ? leftOpticalPinch : rightOpticalPinch;
            if (TryNativeHandAim(left, out _, out float strength))
            { pinch = gesture.UpdateStrength(true, strength); return true; }
            if (TryOpticalJoint(left, XRHandJointID.IndexTip, out var index) &&
                TryOpticalJoint(left, XRHandJointID.ThumbTip, out var thumb))
            { pinch = gesture.Update(true, Vector3.Distance(index.position, thumb.position)); return true; }
            pinch = gesture.Update(false, 0); return false;
        }

        internal bool TryOpticalHand(bool left, out Pose palm, out Pose index, out bool pinch)
        {
            palm = index = default; pinch = false;
            if (!TryOpticalJoint(left, XRHandJointID.Palm, out palm))
            { (left ? leftOpticalPinch : rightOpticalPinch).Update(false, 0); return false; }
            if (!TryOpticalJoint(left, XRHandJointID.IndexTip, out index)) index = palm;
            return TryOpticalPinch(left, out pinch);
        }

        internal bool TryOpticalAim(out Ray ray, out bool pinch)
            => TryOpticalPointing(false, out ray, out pinch);

        bool TryOpticalPointing(bool menu, out Ray ray, out bool pinch)
        {
            ray = default; pinch = false;
            if (TryNativeHandAim(false, out var aim, out float strength))
            {
                pinch = rightOpticalPinch.UpdateStrength(true, strength);
                ray = new Ray(aim.position, aim.rotation * Vector3.forward); return true;
            }
            // Pose tracking and click tracking are independent. A temporarily
            // hidden fingertip must not make the entire cursor disappear.
            if (!TryOpticalJoint(false, XRHandJointID.Palm, out var palm))
            { rightOpticalPinch.Update(false, 0); return false; }
            TryOpticalPinch(false, out pinch);
            var hud = GetComponent<TowerBattleHud>();
            Vector3 shoulder = menu && hud != null ? hud.HandPointerShoulder :
                viewCamera.transform.position - Vector3.up * .25f + viewCamera.transform.right * .18f;
            Vector3 direction = palm.position - shoulder;
            if (direction.sqrMagnitude < .001f) return false;
            ray = new Ray(palm.position, direction.normalized); return true;
        }

        internal bool OpticalPinchHeld(bool left) => TryOpticalHand(left, out _, out _, out bool held) && held;

        void ConsumeOpticalInput()
        {
            opticalAction.Update(false, false);
            opticalMove.Update(false, false, Vector3.zero, Quaternion.identity);
            ClearHandAbility();
        }

        public bool ChooseHandAbility(int slot)
        {
            if (!IsUsingOpticalHands || switchPending || director == null || !director.IsBattleRunning ||
                CurrentUnit == null || !CurrentUnit.IsAlive || IsHoldingCharacter ||
                slot < 0 || slot >= CurrentUnit.Definition.AbilityCount) return false;
            ClearHandAbility();
            handAbilityUnit = CurrentUnit; handAbilitySlot = slot;
            return true;
        }

        public void ClearHandAbility()
        {
            handAbilityUnit = null; handAbilitySlot = pendingHandAbility = -1; handAbilityHeld = false;
            handAbilityGate.Update(false, false);
            opticalAction.Update(false, false);
        }

        void ExecuteHandAbility()
        {
            int slot = pendingHandAbility; pendingHandAbility = -1;
            var hud = GetComponent<TowerBattleHud>();
            if (slot < 0 || slot != SelectedHandAbility || IsHoldingCharacter ||
                hud?.BlocksGameplayInput == true || hud?.ControllerOverUI == true || hud?.PointerOverUI == true ||
                !TryOpticalAim(out var ray, out _)) return;
            // Use the final optical ray, after movement/aim updates, rather than the menu selection ray.
            if (CurrentUnit.TryAbility(slot, ray.origin, ray.direction)) ClearHandAbility();
        }

        void UpdateOpticalGameplay(ref Vector2 movement, ref bool attack)
        {
            var hud = GetComponent<TowerBattleHud>();
            bool playing = director != null && director.IsBattleRunning && !switchPending && hud?.BlocksGameplayInput != true;
            bool overUi = hud?.ControllerOverUI == true || hud?.PointerOverUI == true;
            bool right = TryOpticalAim(out _, out bool pinch);
            bool onSummit = director?.Towers != null && director.Towers.IsMounted;
            bool bowOnly = onSummit && !director.Towers.SelectedSummitPower.HasValue && !director.Towers.SelectedDefense.HasValue && !director.Towers.IsPlanningRoute;
            if (handAbilitySlot >= 0 && SelectedHandAbility < 0) ClearHandAbility();
            bool selected = SelectedHandAbility >= 0;
            bool allowed = playing && right && !overUi && hud?.HandMenuVisible != true && !IsHoldingCharacter && !WantsPhysicalBowInput && !bowOnly && !opticalWaveHeld;
            attack |= opticalAction.Update(allowed && !selected, pinch);
            bool abilityHeld = handAbilityGate.Update(allowed && selected, pinch);
            pendingHandAbility = abilityHeld && !handAbilityHeld ? SelectedHandAbility : -1;
            handAbilityHeld = abilityHeld;
            bool left = TryOpticalHand(true, out var palm, out _, out bool leftPinch);
            Vector3 axes = opticalMove.Update(playing && left && !overUi && hud?.HandMenuVisible != true && !onSummit && !IsHoldingCharacter && !IsDrawingBow,
                leftPinch, rigRoot.InverseTransformPoint(palm.position), Quaternion.Inverse(rigRoot.rotation) * viewCamera.transform.rotation);
            movement += new Vector2(axes.x, axes.z);
            if (CurrentUnit != null && CurrentUnit.IsAlive && CurrentUnit.Definition.IsFlying)
                CurrentUnit.FlightAltitude = Mathf.Clamp(CurrentUnit.FlightAltitude + axes.y * 6 * Time.deltaTime, 1, CurrentUnit.Definition.MaximumFlightHeight);
            else if (CurrentUnit == null && !onSummit)
                MoveSpectator(Vector3.up * (axes.y * spectatorSpeed * Time.deltaTime));
        }

        public void TurnFromHandMenu(float direction)
        {
            if (!IsUsingXR || switchPending || director == null || !director.IsBattleRunning) return;
            TurnRig(Mathf.Sign(direction) * snapTurnDegrees); FinalizeView();
        }

        internal bool TryHandMoveFeedback(out Vector3 origin, out Vector3 hand)
        {
            origin = hand = default;
            if (!opticalMove.Active || !TryOpticalHand(true, out var palm, out _, out _)) return false;
            origin = rigRoot.TransformPoint(opticalMove.Origin); hand = palm.position; return true;
        }
    }
}
