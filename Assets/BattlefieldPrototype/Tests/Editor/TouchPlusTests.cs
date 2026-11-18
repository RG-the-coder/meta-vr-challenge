using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace BattlefieldPrototype.Tests
{
    public sealed class TouchPlusTests
    {
        InputDevice left, right;
        BattleTouchInput input;
        bool registeredLayout;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode routing;
        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            background = InputSystem.settings.backgroundBehavior;
            routing = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // The isolated regression project deliberately has no OpenXR runtime or connected headset.
            if (InputSystem.LoadLayout("QuestTouchPlusController") == null)
            {
                InputSystem.RegisterLayout(@"{
                    ""name"":""QuestTouchPlusController"", ""extend"":""XRController"",
                    ""controls"":[
                        {""name"":""thumbstick"",""layout"":""Vector2""},
                        {""name"":""primaryButton"",""layout"":""Button""},
                        {""name"":""secondaryButton"",""layout"":""Button""},
                        {""name"":""menu"",""layout"":""Button""},
                        {""name"":""thumbstickClicked"",""layout"":""Button""},
                        {""name"":""grip"",""layout"":""Axis""},
                        {""name"":""gripPressed"",""layout"":""Button""},
                        {""name"":""trigger"",""layout"":""Axis""},
                        {""name"":""triggerPressed"",""layout"":""Button""},
                        {""name"":""pointerPosition"",""layout"":""Vector3""},
                        {""name"":""pointerRotation"",""layout"":""Quaternion""}
                    ]}");
                registeredLayout = true;
            }
            left = InputSystem.AddDevice("QuestTouchPlusController"); InputSystem.SetDeviceUsage(left, "LeftHand");
            right = InputSystem.AddDevice("QuestTouchPlusController"); InputSystem.SetDeviceUsage(right, "RightHand");
            input = new BattleTouchInput();
            Set(left, "isTracked", 1); Set(right, "isTracked", 1); InputSystem.Update();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            input?.Dispose();
            if (left != null) InputSystem.RemoveDevice(left);
            if (right != null) InputSystem.RemoveDevice(right);
            if (registeredLayout) InputSystem.RemoveLayout("QuestTouchPlusController");
            registeredLayout = false;
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = routing;
            yield return new ExitPlayMode();
        }
        static void Set(InputDevice device, string name, float value)
        {
            using (StateEvent.From(device, out var state))
            {
                ((AxisControl)device[name]).WriteValueIntoEvent(value, state);
                InputSystem.QueueEvent(state);
            }
            InputSystem.Update();
        }
        [Test] public void TouchPlusBindingsSeparateHandsAndUseAnalogTriggersAndAimPose()
        {
            Set(left, "secondaryButton", 1); Set(right, "primaryButton", 1); Set(right, "trigger", .8f); Set(left, "grip", .7f);
            InputSystem.QueueDeltaStateEvent((Vector2Control)left["thumbstick"], new Vector2(.2f, .9f));
            InputSystem.QueueDeltaStateEvent((Vector3Control)right["devicePosition"], new Vector3(.3f, 1, 0));
            InputSystem.QueueDeltaStateEvent((Vector3Control)right["pointerPosition"], new Vector3(.3f, 1, .1f));
            InputSystem.QueueDeltaStateEvent((QuaternionControl)right["deviceRotation"], Quaternion.identity);
            InputSystem.QueueDeltaStateEvent((QuaternionControl)right["pointerRotation"], Quaternion.Euler(0, 35, 0));
            InputSystem.Update();
            Assert.That(input.Left.Tracked, Is.True, "Left action tracking state");
            Assert.That(input.Right.Tracked, Is.True, "Right action tracking state");
            Assert.That(input.Left.Secondary, Is.True); Assert.That(input.Right.Secondary, Is.False);
            Assert.That(input.Right.Primary, Is.True); Assert.That(input.Left.Primary, Is.False);
            Assert.That(input.Right.TriggerHeld, Is.True); Assert.That(input.Left.TriggerHeld, Is.False);
            Assert.That(input.Left.GripHeld, Is.True); Assert.That(input.Right.GripHeld, Is.False);
            Assert.That(input.Left.Move.y, Is.EqualTo(.9f).Within(.001));
            Assert.That(input.Right.TryPose(true, out var aim, out var rotation), Is.True);
            Assert.That(aim.z, Is.EqualTo(.1f).Within(.001)); Assert.That(rotation.eulerAngles.y, Is.EqualTo(35).Within(.01));
            input.Right.TryPose(false, out var grip, out _); Assert.That(grip.z, Is.Zero);
        }
        [Test] public void LostTrackingNeutralizesHeldButtonsSticksAndStalePoses()
        {
            Set(right, "trigger", 1); Set(right, "primaryButton", 1);
            InputSystem.QueueDeltaStateEvent((Vector2Control)right["thumbstick"], Vector2.one);
            InputSystem.Update(); Assert.That(input.Right.TriggerHeld, Is.True);
            Set(right, "isTracked", 0); InputSystem.Update();
            Assert.That(input.Right.TriggerHeld, Is.False); Assert.That(input.Right.Primary, Is.False);
            Assert.That(input.Right.Move, Is.EqualTo(Vector2.zero)); Assert.That(input.Right.TryPose(true, out _, out _), Is.False);
        }
        [Test] public void ControllersAlreadyTrackedAtReaderStartupAreRecognized()
        {
            input.Dispose(); input = new BattleTouchInput(); InputSystem.Update();
            Assert.That(input.Left.Tracked, Is.True); Assert.That(input.Right.Tracked, Is.True);
        }
        [UnityTest] public IEnumerator TouchPlusHudTrackingRecoveryRequiresAFreshTriggerPress()
        {
            var root = new GameObject("Tracked HUD recovery regression");
            try
            {
                root.AddComponent<BattlefieldBootstrap>().RandomizeBattlefield = false;
                yield return null;
                var player = root.GetComponent<PossessionController>();
                var hud = root.GetComponent<TowerBattleHud>();
                player.enabled = false; hud.enabled = false;
                hud.Canvas.gameObject.SetActive(true);
                typeof(PossessionController).GetProperty("IsUsingXR").SetValue(player, true);
                player.UseTrackedMenuAim(); hud.SendMessage("Layout"); hud.OpenGuide();
                // Let the newly opened world-space graphics become raycastable.
                yield return null;
                hud.Canvas.GetComponent<UnityEngine.UI.CanvasScaler>().SendMessage("Handle");
                Canvas.ForceUpdateCanvases();
                var next = (RectTransform)hud.Canvas.transform.Find("How to play veil/How to play/Next chapter");
                Vector3 point = next.TransformPoint(next.rect.center);
                player.RightHandAim.SetPositionAndRotation(player.ViewCamera.transform.position,
                    Quaternion.LookRotation(point - player.ViewCamera.transform.position));
                var title = hud.Canvas.transform.Find("How to play veil/How to play/Title").GetComponent<UnityEngine.UI.Text>();
                // Establish tracking on the player's fresh action clone before
                // exercising loss/recovery (the fixture owns a separate reader).
                Set(right, "isTracked", 0); Set(right, "isTracked", 1);
                Set(right, "trigger", 0);
                Assert.That(player.HasTrackedAim, Is.True,
                    $"XR={player.IsUsingXR}, active aim={player.RightHandAim.gameObject.activeInHierarchy}, " +
                    $"player tracking={player.TouchInput.Right.Tracked}, fixture tracking={input.Right.Tracked}");
                hud.SendMessage("ProcessInput");
                Assert.That(hud.ControllerOverUI, Is.True, "The controller ray must reach the guide before testing recovery.");
                Set(right, "trigger", 1); hud.SendMessage("ProcessInput");
                Set(right, "isTracked", 0); hud.SendMessage("ProcessInput");
                Set(right, "isTracked", 1); hud.SendMessage("ProcessInput");
                Set(right, "trigger", 0); hud.SendMessage("ProcessInput");
                Assert.That(title.text, Does.Contain("1 OF 12"), "Reconnecting with a held trigger must cancel the old click.");
                Set(right, "trigger", 1); hud.SendMessage("ProcessInput");
                Set(right, "trigger", 0); hud.SendMessage("ProcessInput");
                Assert.That(title.text, Does.Contain("2 OF 12"), "A fresh trigger press must work after recovery.");
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
        [Test] public void WaveRequiresCloseSqueezeThenSpreadAndReleaseBeforeRepeating()
        {
            var gesture = new TowerWaveGesture();
            Assert.That(gesture.Update(true, true, Vector3.zero, Vector3.right, 0), Is.False, "Ordinary separated grips cannot cast.");
            Assert.That(gesture.Update(true, true, Vector3.zero, Vector3.right * .2f, .1f), Is.False);
            Assert.That(gesture.IsCharging, Is.True);
            Assert.That(gesture.Update(true, true, Vector3.zero, Vector3.right, .2f), Is.False, "Reject a tracking jump or immediate spread.");
            Assert.That(gesture.Update(true, true, Vector3.zero, Vector3.right, .5f), Is.True);
            Assert.That(gesture.Update(true, true, Vector3.zero, Vector3.right, .6f), Is.False, "Held grips must not repeat.");
            gesture.Update(true, false, Vector3.zero, Vector3.zero, .7f);
            gesture.Update(true, true, Vector3.zero, Vector3.right * .2f, .8f);
            Assert.That(gesture.Update(true, true, Vector3.zero, Vector3.right, 1.2f), Is.True);
        }
        [Test] public void WaveCancelsOnMenuTrackingLossTimeoutOrReleasedGrip()
        {
            var gesture = new TowerWaveGesture();
            gesture.Update(true, true, Vector3.zero, Vector3.right * .2f, 0);
            Assert.That(gesture.Update(false, true, Vector3.zero, Vector3.right, .5f), Is.False);
            Assert.That(gesture.IsCharging, Is.False);
            gesture.Update(true, true, Vector3.zero, Vector3.right * .2f, 1);
            Assert.That(gesture.Update(true, true, Vector3.zero, Vector3.right, 3.1f), Is.False);
            Assert.That(gesture.Update(true, true, Vector3.zero, Vector3.right * .2f, 3.2f), Is.False);
            Assert.That(gesture.IsCharging, Is.False, "Timed out gestures require release.");
        }
    }
}
