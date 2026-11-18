using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator PinchIgnoresConcurrentMousePressMotionAndTrackingDropout()
        {
            yield return CreateBasicUi(); var hands = UseOpticalHands(); yield return null;
            var button = Ui("Home and outcome veil/Skyhold briefing/Begin siege").GetComponent<Button>();
            button.onClick.RemoveAllListeners(); int clicks = 0; button.onClick.AddListener(() => clicks++);
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)button.transform;
            Vector3 target = rect.TransformPoint(rect.rect.center);
            Vector3 viewport = player.ViewCamera.WorldToViewportPoint(target);
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                PointHand(hands, target, false);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height), buttons = 1 });
                InputSystem.Update(); Invoke(hud, "ProcessInput");
                PointHand(hands, target, true);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.one * 10, delta = Vector2.one * 80, buttons = 1 });
                InputSystem.Update(); Invoke(hud, "ProcessInput");
                Assert.That(clicks, Is.EqualTo(1), "An emulated mouse press must not steal an optical selection.");
                Vector3 cursor = Ui("UI pointing dot").position;
                PointHand(hands, target + player.ViewCamera.transform.right * .6f, true);
                Invoke(hud, "ProcessInput"); Invoke(hud, "UpdateControllerRay");
                Assert.That(Vector3.Distance(Ui("UI pointing dot").position, cursor), Is.LessThan(.001f));
                hands.tracked[0] = hands.tracked[1] = false; Invoke(hud, "ProcessInput");
                Assert.That(hud.PointerOverUI, Is.False);
                Assert.That(Ui("UI pointing dot").gameObject.activeSelf, Is.False, "A tracking gap must not fall back to the moving mouse.");
                Assert.That(clicks, Is.EqualTo(1));
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest] public IEnumerator PinchMouseEventsCannotCaptureOrRotateTheHeadsetView()
        {
            StartSiege(); var unit = Spawn(BattleTeam.Blue, UnitRole.Vanguard, FreePoint(battle.BoundTower));
            Assert.That(player.Possess(unit), Is.True); yield return Simulate(.2f);
            var hands = UseOpticalHands(); Invoke(hud, "ToggleVrHud");
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var eye = player.ViewCamera.transform; Quaternion rotation = eye.rotation;
                PointHand(hands, eye.position + eye.forward * 5, false); Invoke(player, "UpdatePlayerInput");
                PointHand(hands, eye.position + eye.forward * 5, true);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width, Screen.height) * .5f, delta = new Vector2(90, -40), buttons = 3 });
                InputSystem.Update(); Invoke(player, "UpdatePlayerInput");
                Assert.That(player.IsMouseLookCaptured, Is.False, "Optical hands must not hand rotation to emulated mouse input in the same frame.");
                Assert.That((bool)typeof(PossessionController).GetField("desktopPointerActive", Private).GetValue(player), Is.False);
                Assert.That(Quaternion.Angle(eye.rotation, rotation), Is.LessThan(.001f));
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest] public IEnumerator PinchNavigationKeepsTheVisiblePanelAtItsAnchor()
        {
            yield return CreateBasicUi(); StartSiege(); var hands = UseOpticalHands(); yield return null;
            Invoke(hud, "FinalizeVrHudAnchor", true);
            Vector3 anchored = hud.Canvas.transform.position; Quaternion rotation = hud.Canvas.transform.rotation;
            player.ViewCamera.transform.position += Vector3.right * .15f;
            player.ViewCamera.transform.Rotate(0, 12, 0);
            yield return PinchButton(hands, Ui("Live command HUD/Command station/Possess"));
            Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(hud.Canvas.transform.position, anchored), Is.LessThan(.001f), "Opening CHARACTERS by pinch must not recenter the visible panel.");
            Assert.That(Quaternion.Angle(hud.Canvas.transform.rotation, rotation), Is.LessThan(.001f));
            hud.OpenGuide(); Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(hud.Canvas.transform.position, anchored), Is.LessThan(.001f));
        }

        [Test] public void PinchReleaseCannotRearmTheHandsTogetherMenuGesture()
        {
            var gesture = new TwoHandHudGesture();
            Vector3 left = Vector3.left * .08f, right = Vector3.right * .08f;
            gesture.Update(true, left, right, 0);
            Assert.That(gesture.Update(true, left, right, .5f), Is.True);
            gesture.Update(false, left, right, .6f); // Pinch suppresses the open-hands gesture.
            Assert.That(gesture.Update(true, left, right, .7f), Is.False);
            Assert.That(gesture.Update(true, left, right, 1.3f), Is.False, "Releasing a pinch must not hide/reopen and reposition the HUD.");
            gesture.Update(true, Vector3.left, Vector3.right, 1.4f);
            gesture.Update(true, left, right, 1.5f);
            Assert.That(gesture.Update(true, left, right, 2.1f), Is.True);
        }

        [UnityTest] public IEnumerator PinchLeftHandCannotDragTheViewWhileTheMenuIsVisible()
        {
            StartSiege(); battle.ToggleMount(); var hands = UseOpticalHands();
            var eye = player.ViewCamera.transform; Vector3 before = eye.position;
            PointHand(hands, eye.position + eye.right * 5, false); // The right pointer misses the UI.
            Vector3 palm = eye.position - eye.right * .4f - eye.up * .3f;
            hands.Set(true, palm, palm + eye.forward * .07f, false); OpticalGameplayFrame();
            hands.Set(true, palm, palm + eye.forward * .07f, true); OpticalGameplayFrame();
            hands.Set(true, palm + Vector3.up * .2f, palm + Vector3.up * .2f + eye.forward * .07f, true);
            for (int i = 0; i < 3; i++) { OpticalGameplayFrame(); yield return null; }
            Assert.That(Vector3.Distance(eye.position, before), Is.LessThan(.01f), "A menu pinch must not drag the camera relative to its fixed panel.");
            var move = (HandDragMove)typeof(PossessionController).GetField("opticalMove", Private).GetValue(player);
            Assert.That(move.Active, Is.False);
        }
    }
}
