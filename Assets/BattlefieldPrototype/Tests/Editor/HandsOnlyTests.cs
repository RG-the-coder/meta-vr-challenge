using System;
using System.Collections;
using System.IO;
using System.Xml;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Hands;
using Object = UnityEngine.Object;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        sealed class TestHands : IHandTrackingSource, IHandAimSource
        {
            readonly Transform rig;
            public bool palmOnly;
            public readonly bool[] tracked = { true, true };
            readonly Pose[] palms = new Pose[2], indices = new Pose[2], thumbs = new Pose[2];
            readonly Pose[] aims = new Pose[2];
            readonly float[] strengths = new float[2];
            public readonly bool[] aimValid = new bool[2];
            public readonly bool[] tipsTracked = { true, true };
            public TestHands(Transform rig) { this.rig = rig; }
            public void Set(bool left, Vector3 palm, Vector3 index, bool pinch)
            {
                int h = left ? 0 : 1;
                aimValid[h] = false;
                palms[h] = new Pose(rig.InverseTransformPoint(palm), Quaternion.identity);
                indices[h] = new Pose(rig.InverseTransformPoint(index), Quaternion.identity);
                thumbs[h] = new Pose(indices[h].position + Vector3.right * (pinch ? .015f : .07f), Quaternion.identity);
            }
            public bool IsTracked(bool left) => tracked[left ? 0 : 1];
            public void SetAim(bool left, Pose aim, float strength)
            {
                int h = left ? 0 : 1; aimValid[h] = true;
                aims[h] = new Pose(rig.InverseTransformPoint(aim.position), Quaternion.Inverse(rig.rotation) * aim.rotation);
                strengths[h] = strength;
            }
            public bool TryGetAim(bool left, out Pose aim, out float strength)
            { int h = left ? 0 : 1; aim = aims[h]; strength = strengths[h]; return tracked[h] && aimValid[h]; }
            public void PalmRotation(bool left, Quaternion worldRotation)
            { palms[left ? 0 : 1].rotation = Quaternion.Inverse(rig.rotation) * worldRotation; }
            public bool TryGetJoint(bool left, XRHandJointID joint, out Pose pose)
            {
                int h = left ? 0 : 1;
                if (palmOnly && joint != XRHandJointID.Palm) { pose = default; return false; }
                if (joint == XRHandJointID.Palm) pose = palms[h];
                else if (joint == XRHandJointID.IndexTip) pose = indices[h];
                else if (joint == XRHandJointID.ThumbTip) pose = thumbs[h];
                else
                {
                    int finger = joint >= XRHandJointID.LittleMetacarpal ? 4 : joint >= XRHandJointID.RingMetacarpal ? 3 : joint >= XRHandJointID.MiddleMetacarpal ? 2 : joint >= XRHandJointID.IndexMetacarpal ? 1 : 0;
                    int start = finger == 0 ? (int)XRHandJointID.ThumbMetacarpal : (int)XRHandJointID.IndexMetacarpal + (finger - 1) * 5;
                    Vector3 root = palms[h].position + Vector3.right * ((finger - 2) * .018f);
                    Vector3 end = finger == 0 ? thumbs[h].position : finger == 1 ? indices[h].position : root + Vector3.forward * .09f;
                    pose = new Pose(Vector3.Lerp(root, end, Mathf.Clamp01(((int)joint - start + 1) / 5f)), Quaternion.identity);
                    if (joint == XRHandJointID.Wrist) pose.position = palms[h].position - Vector3.forward * .05f;
                }
                return tracked[h] && (tipsTracked[h] || joint != XRHandJointID.IndexTip && joint != XRHandJointID.ThumbTip);
            }
        }

        TestHands UseOpticalHands()
        {
            UseTestHeadset();
            typeof(PossessionController).GetField("trackingHeightOffset", Private).SetValue(player, 0f);
            var rig = (Transform)typeof(PossessionController).GetField("rigRoot", Private).GetValue(player);
            var source = new TestHands(rig);
            typeof(PossessionController).GetField("opticalHandSource", Private).SetValue(player, source);
            var eye = player.ViewCamera.transform;
            source.Set(true, eye.position - eye.right * .4f - eye.up * .35f, eye.position - eye.right * .4f - eye.up * .25f, false);
            PointHand(source, eye.position + eye.forward * 3, false);
            Invoke(hud, "FinalizeVrHudAnchor", true);
            return source;
        }
        void PointHand(TestHands hands, Vector3 target, bool pinch)
        {
            var eye = player.ViewCamera.transform;
            Vector3 direction = (target - eye.position).normalized;
            Vector3 index = eye.position + direction * .5f;
            hands.Set(false, index - direction * .07f, index, pinch);
            hands.SetAim(false, new Pose(index - direction * .07f, Quaternion.LookRotation(direction, eye.up)), pinch ? 1 : 0);
        }
        IEnumerator PinchButton(TestHands hands, Transform button)
        {
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)button;
            Vector3 target = rect.TransformPoint(rect.rect.center);
            PointHand(hands, target, false); Invoke(hud, "ProcessInput");
            PointHand(hands, target, true); Invoke(hud, "ProcessInput");
            PointHand(hands, target, false); Invoke(hud, "ProcessInput");
            yield return null;
        }
        void OpticalGameplayFrame()
        {
            Invoke(player, "UpdateTowerPrimaryLatch");
            Invoke(hud, "ProcessInput");
            Invoke(player, "UpdateGroundHands");
            Invoke(player, "UpdatePlayerInput");
            if (player.CurrentUnit != null) Invoke(player.CurrentUnit, "LateUpdate");
            Invoke(player, "LateUpdate");
        }

        [Test] public void HandsOnlyPinchAndMovementRequireReleaseAndHandleTrackingLoss()
        {
            var pinch = new HandPinchGesture();
            Assert.That(pinch.Update(true, .015f), Is.False);
            pinch.Update(true, .07f); Assert.That(pinch.Update(true, .02f), Is.True);
            Assert.That(pinch.Update(true, .036f), Is.True, "Pinch jitter must not release the action.");
            Assert.That(pinch.Update(false, 0), Is.False);
            Assert.That(pinch.Update(true, .015f), Is.False, "Reconnecting with a pinch must not fire.");
            pinch.Update(true, .07f); Assert.That(pinch.Update(true, .015f), Is.True);
            var move = new HandDragMove();
            move.Update(true, false, Vector3.zero, Quaternion.identity);
            move.Update(true, true, Vector3.zero, Quaternion.identity);
            Assert.That(move.Update(true, true, new Vector3(.1f, 0, .1f), Quaternion.identity).magnitude, Is.GreaterThan(.5f));
            Assert.That(move.Update(false, true, Vector3.one, Quaternion.identity), Is.EqualTo(Vector3.zero));
            Assert.That(move.Update(true, true, Vector3.one, Quaternion.identity), Is.EqualTo(Vector3.zero));
            var pointer = new HandPointerLatch(); var original = new Ray(Vector3.zero, Vector3.forward);
            pointer.Update(true, true, original, 1);
            Assert.That(pointer.Update(true, false, new Ray(Vector3.one, Vector3.right), 2).direction, Is.EqualTo(Vector3.forward));
            Assert.That(pointer.Update(true, false, new Ray(Vector3.one, Vector3.right), 2).direction, Is.EqualTo(Vector3.forward));
            Assert.That(pointer.Update(true, false, new Ray(Vector3.one, Vector3.right), 3).direction, Is.EqualTo(Vector3.right));
        }

        [UnityTest] public IEnumerator HandsOnlyCanStartSpawnAndControlUsingPinchesWithoutControllerInput()
        {
            yield return CreateBasicUi(); var hands = UseOpticalHands(); yield return null;
            yield return PinchButton(hands, Ui("Home and outcome veil/Skyhold briefing/Begin siege")); FreezeEnemy(); hud.Refresh();
            Assert.That(director.IsBattleRunning, Is.True);
            yield return PinchButton(hands, Ui("Live command HUD/Command station/Possess"));
            Assert.That(hud.BlocksGameplayInput, Is.True);
            yield return PinchButton(hands, Ui("Live command HUD/Character browser/Control character 0"));
            Assert.That(battle.CanDeploy, Is.True); Assert.That(battle.Recruitment, Is.EqualTo(UnitRole.Vanguard));
            Vector3 point = FreePoint(battle.BoundTower);
            PointHand(hands, point, false); OpticalGameplayFrame();
            PointHand(hands, point, true); OpticalGameplayFrame();
            Assert.That(director.AliveBlue, Is.EqualTo(1));
            OpticalGameplayFrame(); OpticalGameplayFrame();
            Assert.That(director.AliveBlue, Is.EqualTo(1), "Holding a pinch must not buy multiple troops.");
            PointHand(hands, point, false); OpticalGameplayFrame();
            var eye = player.ViewCamera.transform;
            Vector3 together = eye.position + eye.forward * .4f;
            hands.Set(true, together - eye.right * .1f, together - eye.right * .1f + eye.up * .05f, false);
            hands.Set(false, together + eye.right * .1f, together + eye.right * .1f + eye.up * .05f, false);
            Invoke(hud, "UpdateVrHudGesture"); yield return new WaitForSecondsRealtime(.55f);
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True, "Open hands together must summon the HUD.");
            hands.Set(true, eye.position - eye.right * .4f - eye.up * .35f, eye.position - eye.right * .4f - eye.up * .25f, false);
            yield return PinchButton(hands, Ui("Live command HUD/Command station/Possess"));
            yield return PinchButton(hands, Ui("Live command HUD/Character browser/Control character 0"));
            Invoke(player, "UpdateSwitch"); yield return new WaitForSecondsRealtime(.15f); Invoke(player, "UpdateSwitch");
            Assert.That(player.CurrentUnit, Is.Not.Null);
            Assert.That(player.CurrentUnit.Definition.Role, Is.EqualTo(UnitRole.Vanguard));
        }

        [UnityTest] public IEnumerator HandsOnlyCanMoveAttackTurnAndReturnToTheBow()
        {
            yield return CreateBasicUi(); StartSiege();
            var unit = battle.SquadMember(BattleTeam.Blue, 0);
            var selecting = UseOpticalHands(); yield return HandPage(selecting, "RECRUIT");
            yield return PinchButton(selecting, Ui("Live command HUD/Character browser/Control character 0"));
            yield return Simulate(.2f); Invoke(player, "UpdateSwitch"); Invoke(player, "FinalizeView");
            Assert.That(player.CurrentUnit, Is.SameAs(unit));
            var hands = UseOpticalHands(); var eye = player.ViewCamera.transform;
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.False);
            PointHand(hands, unit.transform.position + Vector3.forward * 6, false);
            Vector3 palm = eye.position - eye.right * .4f - eye.up * .35f;
            hands.Set(true, palm, palm + eye.forward * .07f, false); OpticalGameplayFrame();
            hands.Set(true, palm, palm + eye.forward * .07f, true); OpticalGameplayFrame();
            Vector3 before = unit.transform.position;
            var rig = (Transform)typeof(PossessionController).GetField("rigRoot", Private).GetValue(player);
            hands.Set(true, palm + eye.forward * .14f, palm + eye.forward * .21f, true);
            float movingUntil = Time.realtimeSinceStartup + .12f;
            do { OpticalGameplayFrame(); yield return null; } while (Time.realtimeSinceStartup < movingUntil);
            Assert.That(Vector3.ProjectOnPlane(unit.transform.position - before, Vector3.up).magnitude, Is.GreaterThan(.03f));
            hands.tracked[0] = false; OpticalGameplayFrame();
            Assert.That(unit.PlanarVelocity.magnitude, Is.LessThan(.1f), "Losing the movement hand must stop motion.");
            hands.tracked[0] = true; hands.Set(true, eye.position - eye.right * .4f, eye.position - eye.right * .4f + eye.forward * .07f, false);
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, unit.transform.position + Vector3.forward * 1.6f); enemy.enabled = false;
            Physics.SyncTransforms(); PointHand(hands, enemy.AimPosition, false); OpticalGameplayFrame();
            float health = enemy.Health;
            PointHand(hands, enemy.AimPosition, true); OpticalGameplayFrame();
            Assert.That(enemy.Health, Is.LessThan(health), "Right pinch must drive the actual character attack path.");
            PointHand(hands, enemy.AimPosition, false); OpticalGameplayFrame();
            Invoke(hud, "ToggleVrHud"); yield return null; Invoke(hud, "LateUpdate");
            float yaw = eye.eulerAngles.y;
            yield return PinchButton(hands, Ui("Live command HUD/Easy commander/Hand turn right"));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, eye.eulerAngles.y)), Is.GreaterThan(20));
            yield return HandPage(hands, "RECRUIT");
            yield return PinchButton(hands, Ui("Live command HUD/Character browser/Return to tower"));
            Assert.That(player.CurrentUnit, Is.Null); Assert.That(battle.CanUseSummitActions, Is.True);
        }

        [UnityTest] public IEnumerator HandsOnlyBowUsesOpticalPalmsAndHandVisualsDisappearOnTrackingLoss()
        {
            yield return CreateBasicUi(); player.PhysicalHandBow = true; StartSiege(); var hands = UseOpticalHands(); var eye = player.ViewCamera.transform;
            Invoke(hud, "CloseCommandMenu");
            Vector3 grip = eye.position + eye.forward * .8f - eye.right * .2f - eye.up * .1f;
            hands.Set(true, grip, grip + eye.forward * .08f, false);
            hands.Set(false, grip - eye.forward * .2f, grip - eye.forward * .12f, false);
            OpticalGameplayFrame();
            hands.Set(false, grip - eye.forward * .2f, grip - eye.forward * .12f, true); OpticalGameplayFrame();
            Assert.That(player.IsDrawingBow, Is.True);
            hands.Set(false, grip - eye.forward * .6f, grip - eye.forward * .52f, true); OpticalGameplayFrame();
            Assert.That(player.BowPull, Is.GreaterThan(.38f));
            int arrows = root.GetComponentsInChildren<BattleProjectile>().Length;
            hands.Set(false, grip - eye.forward * .6f, grip - eye.forward * .52f, false); OpticalGameplayFrame();
            Assert.That(root.GetComponentsInChildren<BattleProjectile>().Length, Is.EqualTo(arrows + 1));
            hud.Refresh(); Invoke(hud, "LateUpdate");
            player.enabled = true;
            var visuals = root.GetComponent<BattleOpticalHands>(); Invoke(visuals, "UpdateHands");
            var models = (Transform[])typeof(BattleOpticalHands).GetField("hands", Private).GetValue(visuals);
            var fingers = (Transform[,,])typeof(BattleOpticalHands).GetField("fingers", Private).GetValue(visuals);
            Assert.That(models[0].gameObject.activeSelf, Is.True); Assert.That(models[1].gameObject.activeSelf, Is.True);
            var tip = fingers[1, 1, 2];
            Assert.That(Vector3.Distance(tip.position + tip.up * (tip.localScale.y - .003f) * .5f, grip - eye.forward * .52f), Is.LessThan(.01f));
            CaptureHud("Optical-hands-and-bow");
            hands.tracked[1] = false; Invoke(visuals, "UpdateHands"); Assert.That(models[1].gameObject.activeSelf, Is.False);
            player.enabled = false; yield return null;
        }

        [UnityTest] public IEnumerator HandsOnlyCanCastASelectedAdvancedSummitPowerWithAPinch()
        {
            StartSiege(); var hands = UseOpticalHands();
            var target = SummitTarget(BattleTeam.Red, Vector3.forward * 12);
            Assert.That(battle.ChooseSummitPower(1), Is.True);
            Invoke(hud, "ToggleVrHud");
            PointHand(hands, target.EyePosition, false); OpticalGameplayFrame();
            float health = target.Health, gold = battle.BlueGold;
            PointHand(hands, target.EyePosition, true); OpticalGameplayFrame();
            Assert.That(target.Health, Is.LessThan(health));
            Assert.That(battle.BlueGold, Is.EqualTo(gold - 30).Within(.01f));
            OpticalGameplayFrame();
            Assert.That(battle.BlueGold, Is.EqualTo(gold - 30).Within(.01f), "A held pinch must not spend again.");
            yield return null;
        }

        [UnityTest] public IEnumerator HandsOnlyPinchGrabsLiftsAndReleasesACharacterWithoutStartingMovement()
        {
            StartSiege(); battle.ToggleMount();
            Vector3 point = FreePoint(battle.BoundTower);
            player.SetMenuView(point + new Vector3(0, 1.7f, -1.2f), Quaternion.identity);
            var unit = Spawn(BattleTeam.Red, UnitRole.Vanguard, point);
            var hands = UseOpticalHands();
            PointHand(hands, point + Vector3.forward * 4, false);
            Vector3 palm = point + Vector3.up;
            hands.Set(true, palm, palm + Vector3.forward * .07f, false); OpticalGameplayFrame();
            hands.Set(true, palm, palm + Vector3.forward * .07f, true); OpticalGameplayFrame();
            Assert.That(unit.IsHandHeld, Is.True); Assert.That(player.IsHoldingCharacter, Is.True);
            var movement = (HandDragMove)typeof(PossessionController).GetField("opticalMove", Private).GetValue(player);
            Assert.That(movement.Active, Is.False, "Grabbing a nearby troop must take priority over movement.");
            hands.Set(true, palm + Vector3.up * .8f, palm + Vector3.up * .8f + Vector3.forward * .07f, true);
            float liftingUntil = Time.realtimeSinceStartup + .12f;
            do { OpticalGameplayFrame(); yield return null; } while (Time.realtimeSinceStartup < liftingUntil);
            Assert.That(unit.transform.position.y, Is.GreaterThan(.15f));
            hands.Set(true, palm + Vector3.up * .8f, palm + Vector3.up * .8f + Vector3.forward * .07f, false); OpticalGameplayFrame();
            Assert.That(unit.IsHandHeld, Is.False); Assert.That(unit.IsThrown, Is.True);
        }

        [Test] public void HandsOnlyQuestConfigurationDeclaresHandsAndEnablesTheSubsystem()
        {
            var directory = new DirectoryInfo(Application.dataPath);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Assets/Oculus/OculusProjectConfig.asset"))) directory = directory.Parent;
            Assert.That(directory, Is.Not.Null);
            string config = File.ReadAllText(Path.Combine(directory.FullName, "Assets/Oculus/OculusProjectConfig.asset"));
            Assert.That(config, Does.Contain("handTrackingSupport: 1"));
            // Unity 6 can append the typed integer-array marker when it saves this asset.
            Assert.That(config, Does.Match(@"targetDeviceTypes: (?:[0-9a-fA-F]{8})+i?\s"), "The target-device list must be valid Unity serialized integers.");
            var manifest = new XmlDocument(); manifest.Load(Path.Combine(directory.FullName, "Assets/Plugins/Android/AndroidManifest.xml"));
            var ns = new XmlNamespaceManager(manifest.NameTable); ns.AddNamespace("android", "http://schemas.android.com/apk/res/android");
            Assert.That(manifest.SelectSingleNode("/manifest/uses-feature[@android:name='oculus.software.handtracking']", ns), Is.Not.Null);
            Assert.That(manifest.SelectSingleNode("/manifest/uses-permission[@android:name='com.oculus.permission.HAND_TRACKING']", ns), Is.Not.Null);
            string openxr = File.ReadAllText(Path.Combine(directory.FullName, "Assets/XR/Settings/OpenXR Package Settings.asset"));
            Assert.That(openxr, Does.Match(@"(?s)m_Name: HandTracking Android.*?m_enabled: 1"));
            Assert.That(openxr, Does.Match(@"m_Name: HandTracking Standalone\r?\n[^\n]*\r?\n  m_enabled: 1"));
            Assert.That(openxr, Does.Match(@"m_Name: MetaHandTrackingAim Standalone\r?\n[^\n]*\r?\n  m_enabled: 1"));
            // Optional source-selection extension currently asserts during Link tracker creation.
            Assert.That(openxr, Does.Match(@"m_Name: HandTrackingDataSourceFeature Standalone\r?\n[^\n]*\r?\n  m_enabled: 0"));
            Assert.That(openxr, Does.Match(@"(?s)m_Name: HandTrackingDataSourceFeature Android.*?m_enabled: 1.*?m_LeftHandPreference: 0.*?m_RightHandPreference: 0"));
        }
    }
}
