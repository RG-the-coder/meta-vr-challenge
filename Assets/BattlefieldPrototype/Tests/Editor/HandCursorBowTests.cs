using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [Test] public void HandFixNativeQuestAimBridgeReadsPosePinchAndRejectsSystemGestures()
        {
            var previous = MetaAimHand.right;
            var device = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
            MetaAimHand.right = device;
            try
            {
                var source = new OpenXrHandTrackingSource();
                var pose = new Pose(new Vector3(.2f, 1.1f, .4f), Quaternion.Euler(-10, 20, 0));
                // No headset backend runs in this test. Populate the registered
                // native device controls directly, then exercise the real adapter.
                using (StateEvent.From(device, out var state))
                {
                    device.isTracked.WriteValueIntoEvent(1f, state);
                    device.aimFlags.WriteValueIntoEvent((int)MetaAimFlags.Valid, state);
                    device.devicePosition.WriteValueIntoEvent(pose.position, state);
                    device.deviceRotation.WriteValueIntoEvent(pose.rotation, state);
                    device.pinchStrengthIndex.WriteValueIntoEvent(.9f, state);
                    InputState.Change(device, state);
                }
                Assert.That(device.added, Is.True, "Native aim device must be registered.");
                Assert.That(device.isTracked.isPressed, Is.True, "Native aim tracked state must reach the Input System.");
                Assert.That(device.aimFlags.ReadValue(), Is.EqualTo((int)MetaAimFlags.Valid), "Native aim flags must reach the Input System.");
                Assert.That(source.TryGetAim(false, out var actual, out float strength), Is.True);
                Assert.That(actual.position, Is.EqualTo(pose.position));
                Assert.That(Quaternion.Angle(actual.rotation, pose.rotation), Is.LessThan(.01f));
                Assert.That(strength, Is.EqualTo(.9f).Within(.001f));
                InputState.Change(device.aimFlags, (int)(MetaAimFlags.Valid | MetaAimFlags.SystemGesture));
                Assert.That(source.TryGetAim(false, out _, out _), Is.False);
                using (StateEvent.From(device, out var state))
                {
                    device.isTracked.WriteValueIntoEvent(0f, state);
                    InputState.Change(device, state);
                }
                Assert.That(source.TryGetAim(false, out _, out _), Is.False);
            }
            finally { InputSystem.RemoveDevice(device); MetaAimHand.right = previous; }
        }

        [UnityTest] public IEnumerator HandFixNativeCursorWorksAtChestHeightWithoutFingertips()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return null;
            Canvas.ForceUpdateCanvases();
            var button = (RectTransform)Ui("Live command HUD/Easy commander/Quick troop 1");
            Vector3 target = button.TransformPoint(button.rect.center);
            var eye = player.ViewCamera.transform;
            Vector3 palm = eye.position + eye.forward * .4f + eye.right * .2f - Vector3.up * .45f;
            hands.Set(false, palm, palm, false); hands.tipsTracked[1] = false;
            var aim = new Pose(palm, Quaternion.LookRotation(target - palm, Vector3.up));
            hands.SetAim(false, aim, 0); Invoke(hud, "ProcessInput");
            var dot = Ui("UI pointing dot");
            Assert.That(dot.gameObject.activeInHierarchy, Is.True);
            Assert.That(Vector3.Distance(dot.position, target), Is.LessThan(.002f));
            Assert.That(dot.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(hud.Canvas.sortingOrder));
            Assert.That(dot.GetComponent<Graphic>().material.renderQueue, Is.GreaterThan(3550));
            CaptureHud("Native-hand-cursor", true);
            hands.SetAim(false, aim, 1); Invoke(hud, "ProcessInput");
            Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Hold));
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest] public IEnumerator HandFixCursorStaysVisibleOffPanelAndOnMissingPinchJoints()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return null;
            var eye = player.ViewCamera.transform;
            hands.Set(false, eye.position + eye.forward * .4f - Vector3.up * .3f + eye.right * .25f, Vector3.zero, false);
            hands.tipsTracked[1] = false;
            Invoke(hud, "ProcessInput");
            Assert.That(Ui("UI pointing dot").gameObject.activeInHierarchy, Is.True, "Palm tracking is enough to show a cursor.");
            var aim = new Pose(eye.position + eye.forward * .4f, Quaternion.LookRotation(eye.forward + eye.right * 5, Vector3.up));
            hands.SetAim(false, aim, 0); Invoke(hud, "ProcessInput");
            Assert.That(Ui("UI pointing dot").gameObject.activeInHierarchy, Is.True, "Missing the menu shows a boundary cursor.");
            Assert.That(hud.ControllerOverUI, Is.False);
            hands.SetAim(false, aim, 1); Invoke(hud, "ProcessInput");
            Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Advance), "Boundary feedback cannot click a clamped button.");
            hands.tracked[1] = false; Invoke(hud, "ProcessInput");
            Assert.That(Ui("UI pointing dot").gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator HandFixCursorRemainsVisibleWithMenuHiddenAndPhysicalBowIdle()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return null;
            Assert.That(player.PhysicalHandBow, Is.True);
            Invoke(hud, "ToggleVrHud");
            PointHand(hands, player.ViewCamera.transform.position + Vector3.forward * 10, false);
            Invoke(hud, "ProcessInput"); Invoke(hud, "UpdateHandControls");
            Assert.That(Ui("UI pointing dot").gameObject.activeInHierarchy, Is.True);
            var line = (LineRenderer)typeof(TowerBattleHud).GetField("pointerLine", Private).GetValue(hud);
            Assert.That(line.enabled, Is.True);
            Invoke(hud, "UpdateControllerRay");
            Assert.That(Ui("UI pointing dot").gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest] public IEnumerator HandFixDefaultBowDrawsWithBothHandsGrippingAndOccludedFingers()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return null;
            Assert.That(player.PhysicalHandBow, Is.True, "A fresh game must allow pulling the visible bow without changing settings.");
            Invoke(hud, "ToggleVrHud");
            var eye = player.ViewCamera.transform;
            Vector3 grip = eye.position + eye.forward * .8f - eye.right * .2f - eye.up * .1f;
            int shots = 0; player.ShotFired += () => shots++;
            hands.Set(true, grip, grip + eye.forward * .08f, false);
            hands.Set(false, grip - eye.forward * .2f, grip - eye.forward * .12f, false);
            OpticalGameplayFrame();
            hands.Set(true, grip, grip + eye.forward * .08f, true);
            hands.Set(false, grip - eye.forward * .2f, grip - eye.forward * .12f, true);
            OpticalGameplayFrame();
            Assert.That(player.IsDrawingBow, Is.True, "The wave gesture must not consume a two-handed bow grip.");
            Assert.That(player.WaveGestureCharging, Is.False);
            hands.tipsTracked[0] = hands.tipsTracked[1] = false;
            Vector3 draw = grip - eye.forward * .6f;
            hands.Set(false, draw, draw, true);
            var aim = new Pose(draw, Quaternion.LookRotation(eye.forward, Vector3.up));
            hands.SetAim(false, aim, 1); OpticalGameplayFrame();
            Assert.That(player.IsDrawingBow, Is.True); Assert.That(player.BowPull, Is.GreaterThan(.38f));
            Invoke(hud, "LateUpdate"); CaptureHud("Hand-bow-full-draw");
            hands.SetAim(false, aim, 0); OpticalGameplayFrame();
            Assert.That(shots, Is.EqualTo(1)); Assert.That(player.IsDrawingBow, Is.False);
            OpticalGameplayFrame(); Assert.That(shots, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator HandFixBowTrackingLossCancelsWithoutFiring()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return null; Invoke(hud, "ToggleVrHud");
            var eye = player.ViewCamera.transform; Vector3 grip = eye.position + eye.forward * .8f - eye.right * .2f;
            int shots = 0; player.ShotFired += () => shots++;
            hands.Set(true, grip, grip + eye.forward * .08f, false);
            hands.Set(false, grip - eye.forward * .2f, grip - eye.forward * .12f, false); OpticalGameplayFrame();
            hands.Set(false, grip - eye.forward * .2f, grip - eye.forward * .12f, true); OpticalGameplayFrame();
            hands.Set(false, grip - eye.forward * .6f, grip - eye.forward * .52f, true); OpticalGameplayFrame();
            Assert.That(player.IsDrawingBow, Is.True);
            hands.tracked[0] = false; OpticalGameplayFrame();
            Assert.That(player.IsDrawingBow, Is.False); Assert.That(shots, Is.Zero);
            hands.tracked[0] = true; OpticalGameplayFrame(); Assert.That(shots, Is.Zero);
        }
    }
}
