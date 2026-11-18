using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace BattlefieldPrototype
{
    /// <summary>Keyboard/mouse and basic OpenXR control without a vendor SDK dependency.</summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class PossessionController : MonoBehaviour
    {
        [Header("Optional existing XR rig (leave empty for the generated rig)")]
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform rightHandAim;
        [Header("Control")]
        public bool AllowEnemyPossession;
        [SerializeField, Min(1f)] private float possessionRange = 45f;
        [SerializeField, Min(0.1f)] private float spectatorSpeed = 7f;
        [SerializeField, Range(0.01f, 1f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Range(15f, 90f)] private float snapTurnDegrees = 30f;
        [SerializeField] private Vector3 spectatorPosition = new Vector3(0f, 8f, -16f);
        [SerializeField, Min(0.05f)] private float cameraCollisionRadius = 0.18f;

        public BattleUnit CurrentUnit { get; private set; }
        public Camera ViewCamera => viewCamera;
        public Transform RightHandAim => rightHandAim;
        public Transform LeftHandAim => leftHand;
        public bool HasTrackedLeftHand => IsUsingXR && leftHand != null && leftHand.gameObject.activeInHierarchy &&
            TouchInput.Left.TryPose(false, out _, out _);
        public bool IsUsingXR { get; private set; }
        // A tracking interruption must not switch an active headset to a mono overlay.
        public bool IsXrDisplayActive => IsUsingXR || XRSettings.isDeviceActive || (viewCamera != null && viewCamera.stereoEnabled);
        internal bool HasTrackedHeadPose => IsUsingXR && TryHeadPose(out _, out _);
        public bool IsMouseLookCaptured => mouseLookActive && Cursor.lockState == CursorLockMode.Locked;
        public bool IsDesktopInputActive => !IsUsingXR || IsMouseLookCaptured || desktopPointerActive && !IsUsingOpticalHands;
        public bool HasTrackedAim => IsUsingXR && !HasOpticalRightHand && rightHandAim != null && rightHandAim.gameObject.activeInHierarchy &&
            TouchInput.Right.TryPose(true, out _, out _);
        public bool IsAimingFromHead => IsDesktopInputActive || !HasTrackedAim;
        public BattleUnit AimedUnit { get; private set; }
        public Ray AimRay { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public Vector2 AimScreenPosition { get; private set; }
        public const float SwitchBlinkDuration = 0.1f;
        public float BlinkOpacity { get; private set; }
        public event System.Action BlinkStarted;
        public event System.Action PossessionChanged;
        public event System.Action ShotFired;
        public bool IsMenuActive => director != null &&
            director.Phase != BattlePhase.Fighting &&
            director.GetComponent<BattleDeployment>() != null;

        private BattleDirector director;
        private bool ownsCamera, ownsRightHand, selectInitialUnit = true;
        private Transform leftHand;
        private bool ownsLeftHand;
        private BattleTouchInput touchInput;
        public BattleTouchInput TouchInput => touchInput ?? (touchInput = new BattleTouchInput());
        public bool RightTriggerHeld => TouchInput.Right.TriggerHeld;
        private XRInputDevice headDevice;
        private readonly List<XRInputSubsystem> xrSubsystems = new List<XRInputSubsystem>(2);
        private readonly RaycastHit[] aimHits = new RaycastHit[32];
        private float nextDeviceRefresh, desktopPitch, trackingHeightOffset = 1.65f;
        private Vector3 possessionRigOffset;
        private Vector3 cameraCollisionOffset;
        private CameraCollisionSolver cameraCollision;
        private bool teleportCamera, attackRequested, abilityRequested, secondaryAbilityRequested, possessRequested;
        private bool mouseLookActive, turnLatched, previousPrimary, previousSecondary, previousStart;
        private bool mouseLookSuspended;
        private bool previousRestart, previousAbility, previousSecondaryAbility, previousRelease;
        private bool previousThirdAbility, previousFourthAbility;
        private int extraAbilityMask;
        private bool desktopPointerActive;
        private bool menuViewApplied, menuPitchedCamera;
        private Quaternion cameraRotationBeforeMenu;
        private bool suppressMouseAttack, suppressControllerAttack;
        private bool switchPending;
        private BattleUnit nextUnit;
        private float switchStarted, switchAt, blinkEnds;

        public void Initialize(BattleDirector battleDirector)
        {
            UnsubscribeDirector();
            director = battleDirector;
            if (director != null)
            {
                director.UnitDied += HandleUnitDied;
                director.RoundReset += HandleRoundReset;
                director.UnitAttacked += ControllerUnitAttacked;
                director.AbilityActivated += ControllerAbilityActivated;
                director.UnitDamaged += ControllerUnitDamaged;
            }
            EnsureRig();
            selectInitialUnit = true;
            PossessionChanged -= ControllerPossessionChanged;
            PossessionChanged += ControllerPossessionChanged;
            if (GetComponent<BattleTouchHands>() == null) gameObject.AddComponent<BattleTouchHands>();
            if (GetComponent<BattleOpticalHands>() == null) gameObject.AddComponent<BattleOpticalHands>();
        }

        /// <summary>Use this before Initialize, or assign the rig references in the Inspector.</summary>
        public void AssignRig(Transform root, Camera camera, Transform rightAim, Transform leftAim = null)
        {
            rigRoot = root;
            viewCamera = camera;
            rightHandAim = rightAim;
            leftHand = leftAim;
        }

        private void Start()
        {
            // A bootstrap may assign an existing Meta/OpenXR rig immediately after AddComponent.
            // Defer generated objects until Initialize or Start so that assignment takes effect.
            EnsureRig();
        }
        private void OnEnable()
        {
            _ = TouchInput;
            Application.onBeforeRender += UpdateTrackedPoses;
        }
        private void OnDisable()
        {
            ConsumeOpticalInput(); leftOpticalPinch.Update(false, 0); rightOpticalPinch.Update(false, 0);
            ReleaseAllHandUnits(); bowDraw.Cancel(); desktopBowHeld = false;
            Application.onBeforeRender -= UpdateTrackedPoses;
            ReleaseMouseLook();
            RestoreSight();
            touchInput?.Dispose(); touchInput = null;
            waveGesture.Reset();
        }
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ReleaseMouseLook();
        }
        private void OnDestroy()
        {
            UnsubscribeDirector();
            touchInput?.Dispose(); touchInput = null;
            if (CurrentUnit != null)
            {
                CurrentUnit.ShotFired -= HandleShotFired;
                CurrentUnit.SetPossessed(false);
            }
            cameraCollision?.Dispose();
        }

        private void UnsubscribeDirector()
        {
            if (director == null) return;
            director.UnitDied -= HandleUnitDied;
            director.RoundReset -= HandleRoundReset;
            director.UnitAttacked -= ControllerUnitAttacked;
            director.AbilityActivated -= ControllerAbilityActivated;
            director.UnitDamaged -= ControllerUnitDamaged;
        }

        private void EnsureRig()
        {
            if (rigRoot == null)
            {
                rigRoot = new GameObject("Player Rig").transform;
                rigRoot.SetParent(transform, false);
                rigRoot.position = spectatorPosition - Vector3.up * 1.65f;
                rigRoot.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
            if (viewCamera == null)
            {
                var cameraObject = new GameObject("Player View", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(rigRoot, false);
                viewCamera = cameraObject.GetComponent<Camera>();
                viewCamera.tag = "MainCamera";
                viewCamera.nearClipPlane = 0.05f;
                viewCamera.farClipPlane = 500f;
                viewCamera.fieldOfView = 75f;
                viewCamera.transform.localPosition = Vector3.up * 1.65f;
                desktopPitch = 20f;
                viewCamera.transform.localRotation = Quaternion.Euler(desktopPitch, 0f, 0f);
                ownsCamera = true;
            }
            if (rightHandAim == null)
            {
                rightHandAim = new GameObject("Tracked Right Hand").transform;
                rightHandAim.SetParent(rigRoot, false);
                ownsRightHand = true;
            }
            if (leftHand == null)
            {
                leftHand = new GameObject("Tracked Left Hand").transform;
                leftHand.SetParent(rigRoot, false);
                ownsLeftHand = true;
            }
            if (cameraCollision == null) cameraCollision = new CameraCollisionSolver();
        }

        private void Update()
        {
            if (director == null)
            {
                var found = FindAnyObjectByType<BattleDirector>();
                if (found != null) Initialize(found);
            }
            if (viewCamera == null || rigRoot == null) return;
            RefreshXRDevices();
            bool useXR = XRSettings.isDeviceActive;
            if (useXR != IsUsingXR) ChangeInputMode(useXR);
            // Unity can unlock the cursor itself on Escape or when Game view loses focus.
            if (mouseLookActive && !IsMouseLookCaptured) ReleaseMouseLook();
            UpdateTrackedPoses();
            UpdateSwitch();
            UpdateTowerPrimaryLatch();
            if (selectInitialUnit && !IsMenuActive && director != null && director.Towers == null && director.Units.Count > 0)
            {
                selectInitialUnit = false;
                Possess(FindNextUnit(null));
            }
            FinalizeView();
            // A simulator is a tracked XR device too. Keyboard/mouse and controllers
            // remain available together instead of selecting one exclusive input path.
            UpdateGroundHands();
            UpdatePlayerInput();
        }

        private void LateUpdate()
        {
            if (viewCamera == null || rigRoot == null) return;
            // BattleUnit's motor runs earlier in LateUpdate. Fire from the same final
            // camera pose that the HUD and rendering use, rather than last frame's eye.
            FinalizeView();
            if (possessRequested) Possess(AimedUnit);
            UpdateBowInteraction();
            ExecuteHandAbility();
            Attack(attackRequested, abilityRequested);
            HandleTowerPowers(abilityRequested, secondaryAbilityRequested, extraAbilityMask);
            if (secondaryAbilityRequested && CurrentUnit != null && !switchPending && director != null && director.IsBattleRunning)
                CurrentUnit.TryAbility(1, AimRay.origin, AimRay.direction);
            if (CurrentUnit != null && !switchPending && director != null && director.IsBattleRunning)
                for (int slot = 2; slot < BattleClassCatalog.MaxAbilities; slot++)
                    if ((extraAbilityMask & (1 << slot)) != 0) CurrentUnit.TryAbility(slot, AimRay.origin, AimRay.direction);
            extraAbilityMask = 0; attackRequested = abilityRequested = secondaryAbilityRequested = possessRequested = false;
        }

        private void ChangeInputMode(bool useXR)
        {
            Vector3 previousEyePosition = viewCamera.transform.position;
            RemoveCameraCollisionOffset();
            IsUsingXR = useXR;
            desktopPointerActive = false;
            turnLatched = false;
            previousPrimary = previousSecondary = previousStart = previousRestart = false;
            previousAbility = previousSecondaryAbility = previousRelease = false;
            previousThirdAbility = previousFourthAbility = false;
            if (useXR)
            {
                if (menuPitchedCamera && !ownsCamera)
                    viewCamera.transform.localRotation = cameraRotationBeforeMenu;
                menuPitchedCamera = false;
                ReleaseMouseLook();
                UpdateTrackedPoses();
            }
            else if (ownsCamera)
            {
                var angles = viewCamera.transform.eulerAngles;
                desktopPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), -75f, 75f);
                rigRoot.rotation = Quaternion.Euler(0f, angles.y, 0f);
                viewCamera.transform.localPosition = Vector3.up * 1.65f;
                viewCamera.transform.localRotation = Quaternion.Euler(desktopPitch, 0f, 0f);
            }
            if (CurrentUnit != null) CapturePossessionOffset();
            else
            {
                // Releasing mouse look may have installed a fresh collision offset.
                // Remove it before committing the preserved rendered viewpoint.
                RemoveCameraCollisionOffset();
                rigRoot.position += previousEyePosition - viewCamera.transform.position;
                teleportCamera = true;
            }
        }

        private void RefreshXRDevices()
        {
            if (Time.unscaledTime < nextDeviceRefresh) return;
            nextDeviceRefresh = Time.unscaledTime + 0.5f;
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!ownsCamera) return;
            SubsystemManager.GetSubsystems(xrSubsystems);
            for (int i = 0; i < xrSubsystems.Count; i++)
            {
                var subsystem = xrSubsystems[i];
                if (!subsystem.running) continue;
                if (subsystem.GetTrackingOriginMode() == TrackingOriginModeFlags.Floor ||
                    subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
                    trackingHeightOffset = 0f;
            }
        }

        private bool TryHeadPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (headDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) && !isTracked) return false;
            bool hasPosition = headDevice.TryGetFeatureValue(CommonUsages.centerEyePosition, out position) ||
                headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            bool hasRotation = headDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rotation) ||
                headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            return hasPosition && hasRotation;
        }

        [BeforeRenderOrder(100)]
        private void UpdateTrackedPoses() => FinalizeView();

        private void ApplyTrackedCameraPose()
        {
            if (!IsUsingXR || rigRoot == null || viewCamera == null) return;
            if (ownsCamera && TryHeadPose(out var position, out var rotation))
            {
                viewCamera.transform.localPosition = position + Vector3.up * trackingHeightOffset;
                // Keep desktop look stable in the XR simulator, including before-render
                // pose updates. Releasing the mouse restores headset-owned rotation.
                if (!mouseLookActive) viewCamera.transform.localRotation = rotation;
            }
        }

        private void RemoveCameraCollisionOffset()
        {
            if (rigRoot != null) rigRoot.position -= cameraCollisionOffset;
            cameraCollisionOffset = Vector3.zero;
        }

        private void FinalizeView()
        {
            if (rigRoot == null || viewCamera == null) return;
            RemoveCameraCollisionOffset();
            ApplyTrackedCameraPose();
            if (CurrentUnit != null && CurrentUnit.IsAlive)
                rigRoot.position = CurrentUnit.transform.position + rigRoot.TransformVector(possessionRigOffset);
            ApplyTowerViewLimits();

            Vector3 desiredEye = viewCamera.transform.position;
            if (cameraCollision != null)
            {
                BattleDirector.SynchronizeSightPhysics();
                float radius = GetCameraCollisionRadius();
                Vector3 safeEye = cameraCollision.Resolve(desiredEye, radius, rigRoot, teleportCamera);
                Vector3 correction = safeEye - desiredEye;
                rigRoot.position += correction;
                // This is temporary in both modes: returning a roomscale head from a
                // wall restores its original tracking origin instead of shifting it.
                cameraCollisionOffset = correction;
            }
            teleportCamera = false;
            if (IsUsingXR)
            {
                if (ownsRightHand) ApplyHandPose(false, rightHandAim, true);
                if (ownsLeftHand) ApplyHandPose(true, leftHand, false);
            }
            UpdateSight();
            UpdateAim();
        }

        private float GetCameraCollisionRadius()
        {
            // Also protect the near-plane corners at unusual Game view aspect ratios.
            float halfHeight = viewCamera.nearClipPlane * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * viewCamera.aspect;
            float nearCorner = new Vector3(halfWidth, halfHeight, viewCamera.nearClipPlane).magnitude;
            return Mathf.Max(cameraCollisionRadius, nearCorner);
        }

        private void MoveSpectator(Vector3 movement)
        {
            if (movement.sqrMagnitude < 0.0000001f) return;
            // Mouse/snap turning may have removed a temporary head correction since
            // the last pose update. Always constrain movement from a safe eye.
            FinalizeView();
            Vector3 safeEye = viewCamera.transform.position;
            RemoveCameraCollisionOffset();
            // Commit only allowed locomotion to the logical rig. Physical head
            // obstruction remains a separate temporary correction in FinalizeView.
            Vector3 allowed = cameraCollision != null
                ? cameraCollision.ConstrainMovement(safeEye, movement, GetCameraCollisionRadius(), rigRoot)
                : movement;
            rigRoot.position += allowed;
            FinalizeView();
        }

        private void ApplyHandPose(bool isLeft, Transform hand, bool aim)
        {
            if (hand == null) return;
            if ((isLeft ? TouchInput.Left : TouchInput.Right).TryPose(aim, out var position, out var rotation))
            {
                // Derive the device origin from the head actually rendered by the assigned rig.
                // This also supports a Meta rig with a nested/offset TrackingSpace transform.
                if (TryHeadPose(out var headPosition, out var headRotation))
                {
                    var trackingToWorld = viewCamera.transform.rotation * Quaternion.Inverse(headRotation);
                    var trackingOrigin = viewCamera.transform.position - trackingToWorld * headPosition;
                    hand.SetPositionAndRotation(trackingOrigin + trackingToWorld * position, trackingToWorld * rotation);
                }
            }
        }

        private void CaptureMouseLook()
        {
            if (ownsCamera)
            {
                var angles = viewCamera.transform.eulerAngles;
                desktopPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), -75f, 75f);
                TurnRig(Mathf.DeltaAngle(rigRoot.eulerAngles.y, angles.y));
                viewCamera.transform.localRotation = Quaternion.Euler(desktopPitch, 0f, 0f);
            }
            mouseLookActive = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ReleaseMouseLook()
        {
            if (mouseLookActive && IsUsingXR && ownsCamera && rigRoot != null && viewCamera != null &&
                TryHeadPose(out _, out var rotation))
            {
                // Preserve the current heading when handing rotation back to tracking.
                float trackedYaw = rotation.eulerAngles.y;
                float desiredYaw = viewCamera.transform.eulerAngles.y - trackedYaw;
                TurnRig(Mathf.DeltaAngle(rigRoot.eulerAngles.y, desiredYaw));
            }
            mouseLookActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UpdateTrackedPoses();
        }

        private void TurnRig(float degrees)
        {
            RemoveCameraCollisionOffset();
            rigRoot.RotateAround(viewCamera.transform.position, Vector3.up, degrees);
            if (CurrentUnit != null)
                possessionRigOffset = rigRoot.InverseTransformVector(rigRoot.position - CurrentUnit.transform.position);
        }

        private void UpdatePlayerInput()
        {
            extraAbilityMask = 0; attackRequested = abilityRequested = secondaryAbilityRequested = possessRequested = false;
            bool handsOwnPointer = OpticalInputOwnsPointer;
            if (handsOwnPointer) { desktopPointerActive = false; if (mouseLookActive) ReleaseMouseLook(); }
            if (IsMenuActive || (GetComponent<TowerBattleHud>() is TowerBattleHud interfaceHud && interfaceHud.BlocksGameplayInput))
            {
                if (CurrentUnit != null) CurrentUnit.Move(Vector3.zero);
                ConsumeMenuControllerEdges();
                waveGesture.Reset();
                if (mouseLookActive) ReleaseMouseLook();
                mouseLookSuspended = false;
                // A click on Fight must finish before it can become a gunshot.
                suppressMouseAttack = suppressControllerAttack = true;
                ConsumeOpticalInput();
                return;
            }
            // Desktop window focus must not disable controllers in a running headset.
            bool desktopFocused = Application.isFocused || IsUsingXR;
            if (!desktopFocused && mouseLookActive) ReleaseMouseLook();
            var keyboard = desktopFocused ? Keyboard.current : null;
            var mouse = desktopFocused && !handsOwnPointer ? Mouse.current : null;
            bool escaped = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            if (escaped)
            {
                mouseLookSuspended = true;
                ReleaseMouseLook();
            }
            bool overUI = !IsMouseLookCaptured && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!IsMouseLookCaptured && GetComponent<TowerBattleHud>() is TowerBattleHud towerHud)
                overUI |= towerHud.PointerOverUI;
            bool inView = mouse != null && viewCamera.pixelRect.Contains(mouse.position.ReadValue());
            bool desktopPointerUsed = mouse != null && inView && !overUI &&
                (mouse.leftButton.isPressed || mouse.delta.ReadValue().sqrMagnitude > 0.01f);
            if (desktopPointerUsed) desktopPointerActive = true;
            // Gameplay captures the mouse automatically. Escape leaves it free
            // for HUD controls until a left click on the game resumes looking.
            // A headset keeps tracked rotation until desktop input takes over.
            if (!escaped && mouseLookSuspended && inView && !overUI && mouse.leftButton.wasPressedThisFrame)
                mouseLookSuspended = false;
            bool towerCommander = director != null && director.Towers != null && (CurrentUnit == null || director.Towers.IsPlanningRoute);
            if (towerCommander && IsMouseLookCaptured && mouse != null && !mouse.rightButton.isPressed) ReleaseMouseLook();
            bool beginMouseLook = mouse != null && !escaped && !IsMouseLookCaptured &&
                inView && !overUI && (!IsUsingXR || desktopPointerActive) &&
                (towerCommander ? mouse.rightButton.isPressed : !mouseLookSuspended);
            if (beginMouseLook)
            {
                CaptureMouseLook();
                overUI = false;
            }
            if (mouse != null && IsMouseLookCaptured && !beginMouseLook)
            {
                var look = mouse.delta.ReadValue() * mouseSensitivity;
                TurnRig(look.x);
                if (ownsCamera)
                {
                    desktopPitch = Mathf.Clamp(desktopPitch - look.y, -75f, 75f);
                    viewCamera.transform.localRotation = Quaternion.Euler(desktopPitch, 0f, 0f);
                }
            }
            var move = keyboard == null ? Vector2.zero : new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            bool cycle = keyboard != null && keyboard.tabKey.wasPressedThisFrame;
            bool possess = keyboard != null && keyboard.eKey.wasPressedThisFrame;
            bool release = keyboard != null && keyboard.qKey.wasPressedThisFrame;
            bool start = keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || (director == null || director.Towers == null) && keyboard.xKey.wasPressedThisFrame);
            bool restart = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            bool canUseMouse = mouse != null && (IsMouseLookCaptured || inView) && !overUI;
            bool mouseAttack = mouse != null && mouse.leftButton.isPressed;
            if (!mouseAttack) suppressMouseAttack = false;
            bool attack = canUseMouse && mouseAttack && !suppressMouseAttack;
            bool ability = keyboard != null && (keyboard.gKey.wasPressedThisFrame || keyboard.digit1Key.wasPressedThisFrame);
            bool secondaryAbility = keyboard != null && (keyboard.hKey.wasPressedThisFrame || keyboard.digit2Key.wasPressedThisFrame);
            if (keyboard != null && (keyboard.jKey.wasPressedThisFrame || keyboard.digit3Key.wasPressedThisFrame)) extraAbilityMask |= 1 << 2;
            if (keyboard != null && (keyboard.kKey.wasPressedThisFrame || keyboard.digit4Key.wasPressedThisFrame)) extraAbilityMask |= 1 << 3;
            bool desktopAimRequested = desktopPointerUsed || ability || secondaryAbility || extraAbilityMask != 0 || possess;
            if (IsUsingXR)
            {
                var stickMove = TouchInput.Left.Move;
                var turn = TouchInput.Right.Move;
                if (director != null && director.Towers != null && Mathf.Abs(turn.y) > .2f)
                {
                    if (CurrentUnit != null && CurrentUnit.Definition.IsFlying)
                        CurrentUnit.FlightAltitude = Mathf.Clamp(CurrentUnit.FlightAltitude + turn.y * 8 * Time.deltaTime, 1, CurrentUnit.Definition.MaximumFlightHeight);
                    else if (CurrentUnit == null) MoveSpectator(Vector3.up * (turn.y * spectatorSpeed * Time.deltaTime));
                }
                float magnitude = stickMove.magnitude;
                if (magnitude > 0.15f)
                    move += stickMove.normalized * Mathf.Clamp01((magnitude - 0.15f) / 0.85f);
                if (Mathf.Abs(turn.x) < 0.3f) turnLatched = false;
                if (Mathf.Abs(turn.x) > 0.7f && !turnLatched)
                {
                    TurnRig(Mathf.Sign(turn.x) * snapTurnDegrees);
                    turnLatched = true;
                }
                // Read every edge even when the same keyboard action is pressed, so
                // short-circuiting cannot defer a held controller button to next frame.
                bool controllerCycle = Edge(TouchInput.Right.Primary, ref previousPrimary);
                bool controllerPossess = Edge(TouchInput.Right.Secondary, ref previousSecondary);
                cycle |= controllerCycle;
                possess |= controllerPossess;
                start |= Edge(TouchInput.Left.Primary, ref previousStart);
                // Y opens the character library; restarting remains an explicit HUD action.
                Edge(TouchInput.Left.Secondary, ref previousRestart);
                release |= Edge(TouchInput.Left.GripHeld, ref previousRelease);
                bool controllerAbility = Edge(TouchInput.Right.GripHeld, ref previousAbility);
                ability |= controllerAbility;
                bool controllerSecondaryAbility = Edge(TouchInput.Left.TriggerHeld, ref previousSecondaryAbility);
                secondaryAbility |= controllerSecondaryAbility;
                bool controllerThirdAbility = Edge(TouchInput.Right.StickClick, ref previousThirdAbility);
                bool controllerFourthAbility = Edge(TouchInput.Left.StickClick, ref previousFourthAbility);
                if (controllerThirdAbility) extraAbilityMask |= 1 << 2;
                if (controllerFourthAbility) extraAbilityMask |= 1 << 3;
                bool controllerAttack = RightTriggerHeld;
                if (!controllerAttack) suppressControllerAttack = false;
                if (GetComponent<TowerBattleHud>()?.ControllerOverUI == true)
                { controllerAttack = false; suppressControllerAttack = true; }
                if(GetComponent<BattleHud>()?.TacticalHud?.TryControllerPress(AimRay,controllerAttack)==true)
                { controllerAttack=false; suppressControllerAttack=true; }
                // Simulator bindings can mirror a mouse click into an XR trigger.
                // An explicit desktop shot/aim must retain the visible cursor ray.
                if (!desktopAimRequested && (controllerAttack || controllerPossess || controllerCycle || controllerAbility || controllerSecondaryAbility || controllerThirdAbility || controllerFourthAbility ||
                    magnitude > 0.15f || Mathf.Abs(turn.x) > 0.7f))
                {
                    desktopPointerActive = false;
                    if (mouseLookActive) ReleaseMouseLook();
                }
                attack |= controllerAttack && !suppressControllerAttack;
            }
            UpdateWaveGesture(ref release, ref ability);
            if (GroundHandsAllowed && (CurrentUnit == null || IsHoldingCharacter)) { release = false; ability = false; }
            if (IsHoldingCharacter) { attack = possess = secondaryAbility = false; extraAbilityMask = 0; }
            if (restart && director != null) { director.RestartBattle(); return; }
            if (director != null && director.Towers != null)
            {
                if (start)
                {
                    director.Towers.ToggleMount();
                }
                start = false;
                if (keyboard != null && keyboard.tKey.wasPressedThisFrame) director.Towers.TeleportNext();
                if (keyboard != null && keyboard.uKey.wasPressedThisFrame) director.Towers.SelectedTower?.TryBuild(BattleTeam.Blue);
                if (keyboard != null && keyboard.yKey.wasPressedThisFrame) director.Towers.RotateDefense();
                if (keyboard != null && keyboard.nKey.wasPressedThisFrame)
                { if (director.Towers.IsPlanningRoute) director.Towers.CancelTroopRoute(); else director.Towers.BeginRoutePlanning(); }
                if (keyboard != null && director.Towers.IsPlanningRoute)
                {
                    if (keyboard.enterKey.wasPressedThisFrame) director.Towers.StartTroopRoute();
                    if (keyboard.backspaceKey.wasPressedThisFrame) director.Towers.UndoRouteWaypoint();
                }
            }
            if (start && director != null) director.BeginBattle();
            if (cycle) CycleUnit();
            if (release) ReleaseControl();
            UpdateOpticalGameplay(ref move, ref attack);
            UpdateAim();
            if(keyboard!=null && director!=null && director.IsBattleRunning)
            {
                var orders=director.GetComponent<BattleOrders>();
                if(keyboard.zKey.wasPressedThisFrame) orders?.IssueForPlayer(BattleOrder.Advance,this);
                if(keyboard.xKey.wasPressedThisFrame) orders?.IssueForPlayer(BattleOrder.Hold,this);
                if(keyboard.vKey.wasPressedThisFrame) orders?.IssueForPlayer(BattleOrder.Protect,this);
                if(keyboard.mKey.wasPressedThisFrame) director.GetComponent<BattleAudio>()?.ToggleMute();
                if(keyboard.bKey.wasPressedThisFrame) TryUseFlare();
            }
            possessRequested = possess;
            MovePlayer(Vector2.ClampMagnitude(move, 1f));
            if (keyboard != null && CurrentUnit == null && !switchPending)
            {
                float vertical = (keyboard.fKey.isPressed ? 1f : 0f) - (keyboard.cKey.isPressed ? 1f : 0f);
                MoveSpectator(Vector3.up * (vertical * spectatorSpeed * Time.deltaTime));
            }
            if (keyboard != null && CurrentUnit != null && CurrentUnit.Definition.IsFlying)
                CurrentUnit.FlightAltitude = Mathf.Clamp(CurrentUnit.FlightAltitude + ((keyboard.fKey.isPressed ? 1 : 0) - (keyboard.cKey.isPressed ? 1 : 0)) * 8 * Time.deltaTime, 1, CurrentUnit.Definition.MaximumFlightHeight);
            attackRequested = attack;
            abilityRequested = ability;
            secondaryAbilityRequested = secondaryAbility;
        }

        void ConsumeMenuControllerEdges()
        {
            Edge(TouchInput.Right.Primary, ref previousPrimary);
            Edge(TouchInput.Right.Secondary, ref previousSecondary);
            Edge(TouchInput.Left.Primary, ref previousStart);
            Edge(TouchInput.Left.Secondary, ref previousRestart);
            Edge(TouchInput.Left.GripHeld, ref previousRelease);
            Edge(TouchInput.Right.GripHeld, ref previousAbility);
            Edge(TouchInput.Left.TriggerHeld, ref previousSecondaryAbility);
            Edge(TouchInput.Right.StickClick, ref previousThirdAbility);
            Edge(TouchInput.Left.StickClick, ref previousFourthAbility);
        }

        private static bool Edge(bool down, ref bool previous)
        {
            bool pressed = down && !previous; previous = down; return pressed;
        }

        private void MovePlayer(Vector2 input)
        {
            if (switchPending) return;
            var forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(rigRoot.forward, Vector3.up);
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var direction = forward * input.y + right * input.x;
            if (CurrentUnit != null && CurrentUnit.IsAlive)
            {
                CurrentUnit.Move(direction);
                CurrentUnit.Face(Vector3.ProjectOnPlane(AimRay.direction, Vector3.up).normalized);
            }
            else
            {
                MoveSpectator(direction * (spectatorSpeed * Time.deltaTime));
            }
        }

        private void Attack(bool attack, bool ability)
        {
            if (!switchPending && director != null && director.IsBattleRunning && director.Towers != null && director.Towers.IsPlanningRoute)
            { if (attack) HandleRoutePrimary(); return; }
            if (CurrentUnit == null && !switchPending && director != null && director.Towers != null)
            {
                if (attack) HandleTowerPrimary();
                return;
            }
            if (CurrentUnit == null || !CurrentUnit.IsAlive || switchPending || director == null || !director.IsBattleRunning) return;
            if (attack) CurrentUnit.TryAttack(AimRay.origin, AimRay.direction);
            if (ability) CurrentUnit.TryAbility(AimRay.origin, AimRay.direction);
        }

        private void UpdateAim()
        {
            Rect pixels = viewCamera.pixelRect;
            AimScreenPosition = pixels.center;
            if (!IsMouseLookCaptured && TryOpticalAim(out var handRay, out _))
            {
                if (director?.Towers?.SelectedDefense.HasValue == true && TryOpticalHandPointer(out var placementRay, out _)) handRay = placementRay;
                AimRay = handRay; AimScreenPosition = viewCamera.WorldToScreenPoint(handRay.GetPoint(possessionRange));
            }
            else if (IsAimingFromHead)
            {
                // Screen coordinates include the camera's actual viewport offset;
                // this also handles a resized Game view or split-screen camera.
                if (!IsMouseLookCaptured && Mouse.current != null && IsDesktopInputActive)
                {
                    Vector2 pointer = Mouse.current.position.ReadValue();
                    AimScreenPosition = new Vector2(Mathf.Clamp(pointer.x, pixels.xMin, pixels.xMax),
                        Mathf.Clamp(pointer.y, pixels.yMin, pixels.yMax));
                }
                AimRay = ViewRay(AimScreenPosition);
            }
            else
            {
                AimRay = new Ray(rightHandAim.position, rightHandAim.forward);
                AimScreenPosition = viewCamera.WorldToScreenPoint(AimRay.GetPoint(possessionRange));
            }
            AimedUnit = null;
            AimPoint = AimRay.GetPoint(possessionRange);
            int count = Physics.RaycastNonAlloc(AimRay, aimHits, possessionRange, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = aimHits[i];
                var unit = hit.collider.GetComponentInParent<BattleUnit>();
                if ((unit != null && unit == CurrentUnit) || hit.distance >= closest) continue;
                closest = hit.distance;
                AimPoint = hit.point;
                AimedUnit = unit != null && unit.IsAlive && CanSeeUnit(unit) ? unit : null;
            }
        }

        private Ray ViewRay(Vector2 screenPoint)
        {
            if (viewCamera.orthographic) return viewCamera.ScreenPointToRay(screenPoint);
            Rect pixels = viewCamera.pixelRect;
            // Rendering expands fractional viewport edges to whole pixels. The
            // float pixelRect/ScreenToViewportPoint values otherwise introduce a
            // constant aiming offset when a camera occupies part of the display.
            Vector2 rasterMin = new Vector2(Mathf.Floor(pixels.xMin), Mathf.Floor(pixels.yMin));
            Vector2 rasterMax = new Vector2(Mathf.Ceil(pixels.xMax), Mathf.Ceil(pixels.yMax));
            Vector2 viewportPoint = new Vector2(
                (screenPoint.x - rasterMin.x) / Mathf.Max(1f, rasterMax.x - rasterMin.x),
                (screenPoint.y - rasterMin.y) / Mathf.Max(1f, rasterMax.y - rasterMin.y));
            Vector4 clip = new Vector4(viewportPoint.x * 2f - 1f, viewportPoint.y * 2f - 1f, 1f, 1f);
            // Unproject in camera space before rotating into the world. Building a
            // near-plane ray in world coordinates loses precision with a small near
            // clip plane, causing visible cursor drift on offset/physical cameras.
            Vector4 cameraPoint = viewCamera.projectionMatrix.inverse * clip;
            Vector3 direction = viewCamera.cameraToWorldMatrix.MultiplyVector(
                new Vector3(cameraPoint.x, cameraPoint.y, cameraPoint.z)).normalized;
            return new Ray(viewCamera.transform.position, direction);
        }

        private bool CanPossess(BattleUnit unit) => unit != null && unit.IsAlive &&
            (director == null || director.Towers == null ? unit.Team == BattleTeam.Blue || AllowEnemyPossession :
                unit.Team == BattleTeam.Blue && unit.SpawnTower != null && unit.SpawnTower.Owner == BattleTeam.Blue);

        public bool Possess(BattleUnit unit)
        {
            if (director != null && director.Towers != null) return false;
            if (IsMenuActive || !CanPossess(unit) || (unit == CurrentUnit && !switchPending)) return false;
            if (director.Towers != null && !director.Towers.BindToUnit(unit)) return false;
            QueueSwitch(unit);
            return true;
        }

        public void CycleUnit()
        {
            var target = FindNextUnit(switchPending ? nextUnit : CurrentUnit);
            if (target != null) Possess(target);
        }

        public void ReleaseControl()
        {
            if (director != null && director.Towers != null) { director.Towers.ReturnToTower(); return; }
            if (CurrentUnit != null || switchPending) QueueSwitch(null);
        }

        public bool TryUseFlare() => !switchPending && director != null && director.Flares.TryUsePlayerFlare(this);

        private BattleUnit FindNextUnit(BattleUnit after)
        {
            if (director == null || director.Units.Count == 0) return null;
            int start = -1;
            for (int i = 0; i < director.Units.Count; i++) if (director.Units[i] == after) { start = i; break; }
            for (int offset = 1; offset <= director.Units.Count; offset++)
            {
                var unit = director.Units[(start + offset) % director.Units.Count];
                if (CanPossess(unit) && (director.Towers == null || director.Towers.CanUseSummitActions || unit.SpawnTower == director.Towers.BoundTower)) return unit;
            }
            return null;
        }

        private void QueueSwitch(BattleUnit unit)
        {
            nextUnit = unit;
            switchPending = true;
            switchStarted = Time.unscaledTime;
            switchAt = switchStarted + SwitchBlinkDuration * 0.5f;
            blinkEnds = switchStarted + SwitchBlinkDuration;
            BlinkStarted?.Invoke();
        }

        private void UpdateSwitch()
        {
            float now = Time.unscaledTime;
            if (switchPending && now >= switchAt)
            {
                RemoveCameraCollisionOffset();
                switchPending = false;
                if (CurrentUnit != null)
                {
                    CurrentUnit.ShotFired -= HandleShotFired;
                    CurrentUnit.SetPossessed(false);
                }
                CurrentUnit = CanPossess(nextUnit) ? nextUnit : null;
                if (CurrentUnit != null)
                {
                    if (menuViewApplied)
                    {
                        // A tactical camera may look down at the board. Restore
                        // external camera orientation and always level the tracking
                        // origin before following a character on the ground.
                        if (!ownsCamera && menuPitchedCamera)
                            viewCamera.transform.localRotation = cameraRotationBeforeMenu;
                        rigRoot.rotation = Quaternion.Euler(0f, CurrentUnit.transform.eulerAngles.y, 0f);
                        menuViewApplied = false;
                        menuPitchedCamera = false;
                    }
                    CurrentUnit.ShotFired += HandleShotFired;
                    CurrentUnit.SetPossessed(true);
                    if (!IsUsingXR && ownsCamera)
                    {
                        rigRoot.rotation = Quaternion.Euler(0f, CurrentUnit.transform.eulerAngles.y, 0f);
                        desktopPitch = 0f;
                        viewCamera.transform.localRotation = Quaternion.identity;
                    }
                    CapturePossessionOffset();
                }
                nextUnit = null;
                PossessionChanged?.Invoke();
            }
            float opacity = now >= blinkEnds ? 0f : now < switchAt
                ? Mathf.InverseLerp(switchStarted, switchAt, now) : Mathf.InverseLerp(blinkEnds, switchAt, now);
            BlinkOpacity = opacity;
        }

        private void HandleShotFired() => ShotFired?.Invoke();

        /// <summary>Returns a hybrid desktop/XR menu to the tracked pointing hand.</summary>
        public void UseTrackedMenuAim()
        {
            if (!IsUsingXR) return;
            desktopPointerActive = false;
            UpdateAim();
        }

        /// <summary>Places the free camera for army editing without moving a unit.</summary>
        public void SetMenuView(Vector3 position, Quaternion rotation)
        {
            EnsureRig();
            if (!menuViewApplied) cameraRotationBeforeMenu = viewCamera.transform.localRotation;
            menuViewApplied = true;
            desktopPointerActive = false;
            suppressMouseAttack = suppressControllerAttack = true;
            RemoveCameraCollisionOffset();
            if (CurrentUnit != null)
            {
                CurrentUnit.ShotFired -= HandleShotFired;
                CurrentUnit.SetPossessed(false);
            }
            CurrentUnit = nextUnit = null;
            switchPending = selectInitialUnit = false;
            extraAbilityMask = 0; attackRequested = abilityRequested = secondaryAbilityRequested = possessRequested = false;
            blinkEnds = 0f;
            BlinkOpacity = 0f;
            ReleaseMouseLook();
            RemoveCameraCollisionOffset();
            // Keep the tracking origin upright. Only a desktop camera pitches
            // down for the board; a headset always retains its real head pose.
            rigRoot.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            if (!IsUsingXR)
            {
                viewCamera.transform.rotation = rotation;
                menuPitchedCamera = true;
            }
            else ApplyTrackedCameraPose();
            rigRoot.position += position - viewCamera.transform.position;
            desktopPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, viewCamera.transform.eulerAngles.x), -75f, 75f);
            teleportCamera = true;
            FinalizeView();
            PossessionChanged?.Invoke();
        }

        private void CapturePossessionOffset()
        {
            possessionRigOffset = Vector3.zero;
            if (CurrentUnit == null || rigRoot == null || viewCamera == null) return;
            RemoveCameraCollisionOffset();
            ApplyTrackedCameraPose();
            if (ownsCamera && !IsUsingXR)
                viewCamera.transform.localPosition = Vector3.up * Mathf.Max(0.5f,
                    CurrentUnit.EyePosition.y - CurrentUnit.transform.position.y);
            // Translate the player to the unit. Full world-space subtraction handles
            // seated/floor tracking heights and nested external camera rig offsets.
            rigRoot.position += CurrentUnit.EyePosition - viewCamera.transform.position;
            possessionRigOffset = rigRoot.InverseTransformVector(rigRoot.position - CurrentUnit.transform.position);
            teleportCamera = true;
        }

        private void HandleUnitDied(BattleUnit unit)
        {
            if (IsMenuActive) return;
            if (director != null && director.Towers != null)
            {
                if (unit == CurrentUnit || switchPending && unit == nextUnit) director.Towers.ReturnToTower();
                return;
            }
            if (unit == CurrentUnit || (switchPending && unit == nextUnit)) QueueSwitch(FindNextUnit(unit));
        }

        public void OpenMenuCursor()
        {
            ReleaseMouseLook();
            extraAbilityMask = 0; attackRequested = abilityRequested = secondaryAbilityRequested = possessRequested = false;
            suppressMouseAttack = suppressControllerAttack = true;
            switchPending = selectInitialUnit = false;
            nextUnit = null;
            blinkEnds = 0f;
            BlinkOpacity = 0f;
        }

        private void HandleRoundReset()
        {
            RemoveCameraCollisionOffset();
            if (CurrentUnit != null)
            {
                CurrentUnit.ShotFired -= HandleShotFired;
                CurrentUnit.SetPossessed(false);
            }
            CurrentUnit = null;
            nextUnit = null;
            switchPending = false;
            extraAbilityMask = 0; attackRequested = abilityRequested = secondaryAbilityRequested = possessRequested = false;
            selectInitialUnit = true;
            if (director != null && director.Towers != null)
            {
                selectInitialUnit = false; blinkEnds = 0; BlinkOpacity = 0; ReleaseMouseLook(); return;
            }
            if (IsMenuActive)
            {
                selectInitialUnit = false;
                blinkEnds = 0f;
                BlinkOpacity = 0f;
                ReleaseMouseLook();
                return;
            }
            // The director has spawned the replacement army before this event. Keep the
            // previous viewpoint until the blink covers teleporting into the new round.
            var first = FindNextUnit(null);
            if (first != null)
            {
                selectInitialUnit = false;
                QueueSwitch(first);
            }
            else
            {
                rigRoot.position += spectatorPosition - viewCamera.transform.position;
                teleportCamera = true;
                blinkEnds = 0f;
                BlinkOpacity = 0f;
            }
        }
    }
}
