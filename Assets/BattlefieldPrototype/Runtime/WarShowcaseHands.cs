#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace BattlefieldPrototype
{
    /// <summary>
    /// Opt-in, editor-only choreography of the game's real optical-hand input.
    /// No headset, controller model or gameplay replacement is created.
    /// </summary>
    [DefaultExecutionOrder(90)]
    public sealed class WarShowcaseHands : MonoBehaviour, IHandTrackingSource, IHandAimSource
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly HashSet<PossessionController> DrivenPlayers = new HashSet<PossessionController>();
        readonly Pose[] palms = new Pose[2], tips = new Pose[2], thumbs = new Pose[2], aims = new Pose[2];
        readonly bool[] tracked = new bool[2], aiming = new bool[2];
        readonly float[] strengths = new float[2];
        PossessionController player;
        TowerBattleHud hud;
        Transform rig;
        BattleTouchHands controllerHands;
        object previousSource;
        bool previousXR, previousEnabled, previousPhysicalBow, previousDesktopPointer, previousControllerHands;
        float previousHeightOffset;
        Vector3? bowTarget;

        public static bool IsDriving(PossessionController controller) => DrivenPlayers.Contains(controller);
        public bool IsInitialized => player != null;

        static FieldInfo Field(string name) => typeof(PossessionController).GetField(name, Private);
        static object Invoke(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static void SetXR(PossessionController target, bool value)
            => typeof(PossessionController).GetProperty(nameof(PossessionController.IsUsingXR)).SetValue(target, value);

        public void Initialize(PossessionController controller)
        {
            Clear();
            if (controller == null || controller.ViewCamera == null)
                throw new System.ArgumentException("Initialize the game player before showcase hands.", nameof(controller));
            player = controller;
            hud = player.GetComponent<TowerBattleHud>();
            rig = (Transform)Field("rigRoot").GetValue(player);
            previousSource = Field("opticalHandSource").GetValue(player);
            previousXR = player.IsUsingXR;
            previousEnabled = player.enabled;
            previousPhysicalBow = player.PhysicalHandBow;
            previousHeightOffset = (float)Field("trackingHeightOffset").GetValue(player);
            previousDesktopPointer = (bool)Field("desktopPointerActive").GetValue(player);
            controllerHands = player.GetComponent<BattleTouchHands>();
            previousControllerHands = controllerHands != null && controllerHands.enabled;
            DrivenPlayers.Add(player);
            player.enabled = true;
            player.OpenMenuCursor();
            SetXR(player, true);
            Field("trackingHeightOffset").SetValue(player, 0f);
            Field("desktopPointerActive").SetValue(player, false);
            Field("opticalHandSource").SetValue(player, this);
            player.PhysicalHandBow = true;
            if (controllerHands != null) controllerHands.enabled = false;
            tracked[0] = tracked[1] = true;
            SetNeutralPose();
            if (hud != null)
            {
                Invoke(hud, "Layout");
                Invoke(hud, "FinalizeVrHudAnchor", true);
                hud.Refresh();
            }
        }

        /// <summary>Normalized pull: prime with (0,false), nock (0,true), draw (1,true), release (1,false).</summary>
        public void SetBowPose(float draw, bool pinch)
        {
            if (player == null) return;
            var eye = player.ViewCamera.transform;
            Vector3 grip = eye.position + eye.forward * .8f - eye.right * .2f - eye.up * .1f;
            Vector3 direction = bowTarget.HasValue ? (bowTarget.Value - grip).normalized : eye.forward;
            if (direction.sqrMagnitude < .001f) direction = eye.forward;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 stringHand = grip - direction * (.2f + Mathf.Clamp01(draw) * BowDrawGesture.FullDraw);
            SetHand(true, grip, rotation, false);
            SetHand(false, stringHand, rotation, pinch);
        }

        public void SetBowTarget(Vector3 worldTarget) => bowTarget = worldTarget;
        public void ClearBowTarget() => bowTarget = null;

        /// <summary>Point and pinch a real world-space UI button or battlefield location.</summary>
        public void SetPointPose(Vector3 worldTarget, bool pinch)
        {
            if (player == null) return;
            var eye = player.ViewCamera.transform;
            Vector3 direction = (worldTarget - eye.position).normalized;
            if (direction.sqrMagnitude < .001f) direction = eye.forward;
            Quaternion rotation = Quaternion.LookRotation(direction, eye.up);
            SetHand(false, eye.position + direction * .43f, rotation, pinch);
            SetAim(false, new Pose(eye.position + direction * .43f, rotation), pinch ? 1 : 0);
            SetHand(true, eye.position + eye.forward * .42f - eye.right * .32f - eye.up * .28f,
                eye.rotation, false);
        }

        public void SetNeutralPose()
        {
            if (player == null) return;
            var eye = player.ViewCamera.transform;
            SetHand(true, eye.position + eye.forward * .43f - eye.right * .28f - eye.up * .28f, eye.rotation, false);
            SetHand(false, eye.position + eye.forward * .43f + eye.right * .28f - eye.up * .28f, eye.rotation, false);
        }

        /// <summary>World-space palm pose, used for two-hand wave gestures and close interaction.</summary>
        public void SetHand(bool left, Vector3 position, Quaternion rotation, bool pinch)
        {
            if (rig == null) return;
            int h = left ? 0 : 1;
            tracked[h] = true; aiming[h] = false;
            Quaternion localRotation = Quaternion.Inverse(rig.rotation) * rotation;
            palms[h] = new Pose(rig.InverseTransformPoint(position), localRotation);
            tips[h] = new Pose(rig.InverseTransformPoint(position + rotation * new Vector3(-.018f, 0, .08f)), localRotation);
            thumbs[h] = new Pose(tips[h].position + localRotation * Vector3.right * (pinch ? .015f : .07f), localRotation);
            strengths[h] = pinch ? 1 : 0;
        }

        public void SetAim(bool left, Pose worldAim, float pinchStrength)
        {
            if (rig == null) return;
            int h = left ? 0 : 1;
            aiming[h] = true;
            aims[h] = new Pose(rig.InverseTransformPoint(worldAim.position), Quaternion.Inverse(rig.rotation) * worldAim.rotation);
            strengths[h] = Mathf.Clamp01(pinchStrength);
        }

        public bool IsTracked(bool left) => player != null && tracked[left ? 0 : 1];
        public bool TryGetAim(bool left, out Pose aim, out float pinchStrength)
        {
            int h = left ? 0 : 1;
            aim = aims[h]; pinchStrength = strengths[h];
            return IsTracked(left) && aiming[h];
        }

        public bool TryGetJoint(bool left, XRHandJointID joint, out Pose pose)
        {
            int h = left ? 0 : 1;
            pose = palms[h];
            if (!IsTracked(left)) return false;
            if (joint == XRHandJointID.Palm) return true;
            if (joint == XRHandJointID.Wrist)
            { pose.position -= pose.rotation * Vector3.forward * .05f; return true; }
            if (joint == XRHandJointID.IndexTip) { pose = tips[h]; return true; }
            if (joint == XRHandJointID.ThumbTip) { pose = thumbs[h]; return true; }
            int finger = joint >= XRHandJointID.LittleMetacarpal ? 4 : joint >= XRHandJointID.RingMetacarpal ? 3 :
                joint >= XRHandJointID.MiddleMetacarpal ? 2 : joint >= XRHandJointID.IndexMetacarpal ? 1 : 0;
            int first = finger == 0 ? (int)XRHandJointID.ThumbMetacarpal : (int)XRHandJointID.IndexMetacarpal + (finger - 1) * 5;
            Vector3 root = palms[h].position + palms[h].rotation * Vector3.right * ((finger - 2) * .018f);
            Vector3 end = finger == 0 ? thumbs[h].position : finger == 1 ? tips[h].position :
                root + palms[h].rotation * Vector3.forward * (finger == 4 ? .073f : .09f);
            pose.position = Vector3.Lerp(root, end, Mathf.Clamp01(((int)joint - first + 1) / 5f));
            return true;
        }

        void Update()
        {
            if (player == null) return;
            SetXR(player, true);
            // Same native input path as the hands-only regression fixtures. The player's
            // ordinary LateUpdate still draws/fires the bow and applies the final view.
            Invoke(player, "UpdateTowerPrimaryLatch");
            Invoke(player, "FinalizeView");
            Invoke(player, "UpdateGroundHands");
            Invoke(player, "UpdatePlayerInput");
        }

        /// <summary>Restore the user's normal player and input source after the opt-in capture.</summary>
        public void Clear()
        {
            if (player == null) return;
            tracked[0] = tracked[1] = false;
            Invoke(player, "UpdateBowInteraction");
            Field("opticalHandSource").SetValue(player, previousSource);
            Field("trackingHeightOffset").SetValue(player, previousHeightOffset);
            Field("desktopPointerActive").SetValue(player, previousDesktopPointer);
            player.PhysicalHandBow = previousPhysicalBow;
            SetXR(player, previousXR);
            DrivenPlayers.Remove(player);
            if (controllerHands != null) controllerHands.enabled = previousControllerHands;
            player.enabled = previousEnabled;
            player = null; hud = null; rig = null; controllerHands = null; bowTarget = null;
        }

        void OnDisable() => Clear();
        void OnDestroy() => Clear();
    }
}
#endif
