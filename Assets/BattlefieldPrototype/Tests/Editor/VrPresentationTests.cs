using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [Test] public void TwoHandHudGestureRequiresAReleaseAndDoesNotToggleOnTrackingLoss()
        {
            var gesture = new TwoHandHudGesture();
            Vector3 close = Vector3.right * .18f, apart = Vector3.right * .5f;
            Assert.That(gesture.Update(true, Vector3.zero, close, 0), Is.False);
            Assert.That(gesture.Update(true, Vector3.zero, close, .3f), Is.False);
            Assert.That(gesture.Update(true, Vector3.zero, close, .46f), Is.True);
            Assert.That(gesture.Update(true, Vector3.zero, close, 1f), Is.False);
            Assert.That(gesture.Update(true, Vector3.zero, apart, 1.1f), Is.False);
            Assert.That(gesture.Update(true, Vector3.zero, close, 1.2f), Is.False);
            Assert.That(gesture.Update(false, Vector3.zero, close, 1.5f), Is.False);
            Assert.That(gesture.Update(true, Vector3.zero, close, 1.6f), Is.False);
            Assert.That(gesture.Update(true, Vector3.zero, close, 2.1f), Is.False);
            gesture.Update(true, Vector3.zero, apart, 2.2f);
            gesture.Update(true, Vector3.zero, close, 2.3f);
            Assert.That(gesture.Update(true, Vector3.zero, close, 2.8f), Is.True);
        }

        [UnityTest] public IEnumerator VrHudGestureChoiceKeepsControlsInTheClearCenter()
        {
            StartSiege(); UseTestHeadset(); yield return null;
            var liveRoot = Ui("Live command HUD");
            var tabs = Ui("Live command HUD/VR HUD pages");
            var command = Ui("Live command HUD/Command station");
            var mapPanel = Ui("Live command HUD/Height-based minimap");
            Assert.That(tabs.gameObject.activeInHierarchy, Is.True);
            float commandAngle = Mathf.Atan(Mathf.Abs(command.position.x - player.ViewCamera.transform.position.x) /
                Vector3.Distance(command.position, player.ViewCamera.transform.position)) * Mathf.Rad2Deg;
            Assert.That(commandAngle, Is.LessThan(25f));
            Assert.That(mapPanel.gameObject.activeInHierarchy, Is.False);
            CaptureHud("VR-HUD-Command");
            Canvas.ForceUpdateCanvases();
            WorldUiClick(Ui("Live command HUD/VR HUD pages/VR MAP + TOWERS")); yield return null;
            Assert.That(command.gameObject.activeInHierarchy, Is.False);
            Assert.That(mapPanel.gameObject.activeInHierarchy, Is.True);
            CaptureHud("VR-HUD-Map");
            WorldUiClick(Ui("Live command HUD/VR HUD pages/VR RECRUIT")); yield return null;
            Assert.That(Ui("Live command HUD/Character browser").gameObject.activeInHierarchy, Is.True);
            CaptureHud("VR-HUD-Recruit");
            Invoke(hud, "ToggleVrHud"); yield return null;
            Assert.That(liveRoot.gameObject.activeInHierarchy, Is.False);
            hud.OpenGuide(); yield return null;
            Assert.That(Ui("How to play veil/How to play").gameObject.activeInHierarchy, Is.True);
            Invoke(hud, "CloseGuide"); Invoke(hud, "ToggleVrHud"); yield return null;
            Assert.That(liveRoot.gameObject.activeInHierarchy, Is.True);
        }

        void UseTestHeadset()
        {
            player.enabled = false;
            typeof(PossessionController).GetProperty("IsUsingXR").SetValue(player, true);
            typeof(PossessionController).GetField("desktopPointerActive", Private).SetValue(player, false);
            Invoke(hud, "Layout"); hud.Refresh();
        }

        [UnityTest] public IEnumerator VrWorldHitClicksWithoutScreenProjection()
        {
            UseTestHeadset(); hud.OpenGuide(); yield return null;
            Canvas.ForceUpdateCanvases();
            var next = (RectTransform)Ui("How to play veil/How to play/Next chapter");
            var title = Ui("How to play veil/How to play/Title").GetComponent<Text>();
            var point = next.TransformPoint(next.rect.center);
            // A deliberately incompatible screen projection must not affect a world hit.
            var camera = player.ViewCamera;
            var projection = camera.projectionMatrix;
            camera.projectionMatrix = Matrix4x4.Perspective(20, .3f, .05f, 500);
            Assert.That((bool)Invoke(hud, "DispatchWorld", point, true), Is.True);
            Invoke(hud, "DispatchWorld", point, false);
            camera.projectionMatrix = projection;
            Assert.That(title.text, Does.Contain("2 OF 12"));
        }

        [UnityTest] public IEnumerator VrMouseCanClickWithNoController()
        {
            UseTestHeadset(); hud.OpenGuide(); yield return null;
            hud.enabled = false; hud.Canvas.gameObject.SetActive(true);
            yield return null;
            hud.Canvas.GetComponent<CanvasScaler>().SendMessage("Handle");
            Canvas.ForceUpdateCanvases();
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                var next = (RectTransform)Ui("How to play veil/How to play/Next chapter");
                Vector3 viewport = player.ViewCamera.WorldToViewportPoint(next.TransformPoint(next.rect.center));
                var position = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = position }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                UnityEngine.InputSystem.InputSystem.Update();
                Invoke(hud, "ProcessInput");
                Assert.That(hud.PointerOverUI, Is.True);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = position });
                UnityEngine.InputSystem.InputSystem.Update();
                Invoke(hud, "ProcessInput");
                Assert.That(Ui("How to play veil/How to play/Title").GetComponent<Text>().text, Does.Contain("2 OF 12"));
            }
            finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest] public IEnumerator VrGuideKeepsSixShortPagesReadableAndReachableByRay()
        {
            UseTestHeadset(); hud.OpenGuide(); yield return null;
            var body = Ui("How to play veil/How to play/Chapter text").GetComponent<Text>();
            var title = Ui("How to play veil/How to play/Title").GetComponent<Text>();
            var next = Ui("How to play veil/How to play/Next chapter");
            var rect = (RectTransform)hud.Canvas.transform;
            Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(hud.Canvas.GetComponent<CanvasScaler>().dynamicPixelsPerUnit, Is.GreaterThanOrEqualTo(4));
            float panelDistance = Vector3.Distance(rect.position, player.ViewCamera.transform.position);
            Assert.That(panelDistance, Is.InRange(1.4f, 2f));
            // Measure angular font size, rather than just checking a pixel number.
            float angularSize = Mathf.Atan(body.fontSize * rect.localScale.y / panelDistance) * Mathf.Rad2Deg;
            Assert.That(angularSize, Is.GreaterThan(1.5f));
            for (int i = 0; i < 6; i++)
            {
                Canvas.ForceUpdateCanvases();
                Assert.That(title.text, Is.EqualTo("HELP"));
                Assert.That(body.fontSize, Is.GreaterThanOrEqualTo(32));
                Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height + 1), "VR guide page " + (i + 1));
                foreach (var text in Ui("How to play veil/How to play").GetComponentsInChildren<Text>())
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.transform.parent.name + "/" + text.name + " on page " + (i + 1));
                if (i == 0 || i == 5) CaptureHud("VR-Guide-" + (i + 1));
                WorldUiClick(next); yield return null;
            }
            Assert.That(title.text, Is.EqualTo("HELP"));
            Assert.That((int)typeof(TowerBattleHud).GetField("guidePage", Private).GetValue(hud), Is.Zero);
            WorldUiClick(Ui("How to play veil/How to play/Previous chapter")); yield return null;
            Assert.That((int)typeof(TowerBattleHud).GetField("guidePage", Private).GetValue(hud), Is.EqualTo(5));
            WorldUiClick(Ui("How to play veil/How to play/Continue")); yield return null;
            Assert.That(director.IsBattleRunning, Is.True);
            Assert.That(hud.BlocksGameplayInput, Is.False);
        }

        [UnityTest] public IEnumerator VrMirrorMouseKeepsStereoHudAndDesktopLayoutRecovers()
        {
            UseTestHeadset(); hud.OpenGuide(); yield return null;
            typeof(PossessionController).GetField("desktopPointerActive", Private).SetValue(player, true);
            Invoke(hud, "Layout"); hud.Refresh(); yield return null;
            Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(hud.Canvas.transform.parent, Is.Null, "Non-battle menus must stay fixed in the room.");
            Assert.That(Ui("How to play veil/How to play/Chapter text").GetComponent<Text>().fontSize, Is.EqualTo(32));
            typeof(PossessionController).GetProperty("IsUsingXR").SetValue(player, false);
            Invoke(hud, "Layout"); hud.Refresh(); yield return null;
            Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(((RectTransform)Ui("How to play veil/How to play")).rect.height, Is.EqualTo(630));
            var body = Ui("How to play veil/How to play/Chapter text").GetComponent<Text>();
            Assert.That(body.fontSize, Is.EqualTo(21));
            Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height + 1));
        }

        [UnityTest] public IEnumerator VrHomeMapChoicesAndBriefingFitAtHeadsetTextSizes()
        {
            UseTestHeadset(); yield return null;
            foreach (TowerMapKind mapKind in System.Enum.GetValues(typeof(TowerMapKind)))
            {
                battle.SelectMap(mapKind); hud.Refresh(); Canvas.ForceUpdateCanvases();
                var card = Ui("Home and outcome veil/Skyhold briefing");
                foreach (var text in card.GetComponentsInChildren<Text>())
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.transform.parent.name + "/" + text.name + " on " + mapKind);
            }
            CaptureHud("VR-Home");
            WorldUiClick(Ui("Home and outcome veil/Skyhold briefing/Begin siege")); yield return null;
            Assert.That(hud.BlocksGameplayInput, Is.False);
            Assert.That(director.IsBattleRunning, Is.True);
        }

        [UnityTest] public IEnumerator VrGauntletsMirrorPalmsIndexFingersAndCurlIntoEachPalm()
        {
            yield return null;
            for (int h = 0; h < 2; h++)
            {
                var hand = root.transform.Find(h == 0 ? "Left spectral gauntlet" : "Right spectral gauntlet");
                Assert.That(hand, Is.Not.Null);
                var model = hand.Find("Grip aligned model");
                Vector3 palmNormal = hand.InverseTransformDirection(model.forward);
                Assert.That(Vector3.Dot(palmNormal, h == 0 ? Vector3.right : Vector3.left), Is.GreaterThan(.99f));
                Vector3 index = hand.InverseTransformPoint(model.Find("Finger 0").position);
                Vector3 pinky = hand.InverseTransformPoint(model.Find("Finger 3").position);
                Assert.That(index.z, Is.GreaterThan(pinky.z), "Index and thumb must be on the +Z side of each grip.");
                Vector3 curl = model.TransformDirection(Quaternion.Euler(85, 0, 0) * Vector3.up);
                Assert.That(Vector3.Dot(curl, model.forward), Is.GreaterThan(.9f), "Fingers must close into the palm.");
                Assert.That(hand.gameObject.activeSelf, Is.False, "Untracked hands must stay hidden.");
            }
            player.enabled = false; yield return null;
            Assert.That(typeof(PossessionController).GetField("touchInput", Private).GetValue(player), Is.Null,
                "Hidden gauntlets must not recreate the disabled player's input actions.");
        }
    }
}
