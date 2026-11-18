using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator SimpleUiMenuButtonClosesReopensAndRecallsWithoutGestures()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return null;
            Vector3 anchor = hud.Canvas.transform.position;
            yield return PinchButton(hands, Ui("Live command HUD/VR HUD pages/VR HIDE"));
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.False);
            Invoke(hud, "UpdateMenuAccess"); Canvas.ForceUpdateCanvases();
            var access = (Canvas)typeof(TowerBattleHud).GetField("menuAccessCanvas", Private).GetValue(hud);
            Assert.That(access.gameObject.activeInHierarchy, Is.True);
            Vector3 menuViewport = player.ViewCamera.WorldToViewportPoint(access.transform.position);
            Assert.That(menuViewport.x, Is.InRange(.05f, .95f)); Assert.That(menuViewport.y, Is.InRange(.05f, .95f));
            Invoke(hud, "LateUpdate"); CaptureHud("Simple-Menu-closed");
            yield return PinchButton(hands, access.transform.Find("Open menu"));
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
            Assert.That(player.IsDrawingBow, Is.False);
            // Commands never close or relocate the panel.
            anchor = hud.Canvas.transform.position;
            yield return PinchButton(hands, Ui("Live command HUD/Easy commander/Quick troop 1"));
            Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Hold));
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
            Assert.That(Vector3.Distance(anchor, hud.Canvas.transform.position), Is.LessThan(.001f));
            hands.tracked[0] = false; Invoke(hud, "CloseCommandMenu"); Invoke(hud, "UpdateMenuAccess");
            Assert.That(access.gameObject.activeInHierarchy, Is.True, "MENU stays accessible with only the right hand tracked.");
            yield return PinchButton(hands, access.transform.Find("Open menu"));
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest] public IEnumerator SimpleUiPagesHaveLargeControlsAndRenderCleanly()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return null;
            string[] pages = { "COMMAND", "RECRUIT", "MAP + TOWERS", "POWERS", "BUILD" };
            string[] previews = { "Play", "Squad", "Map", "Powers", "Build" };
            for (int i = 0; i < pages.Length; i++)
            {
                yield return HandPage(hands, pages[i]); Invoke(hud, "LateUpdate"); Canvas.ForceUpdateCanvases();
                foreach (var button in Ui("Live command HUD/VR HUD pages").GetComponentsInChildren<Button>())
                {
                    Assert.That(((RectTransform)button.transform).rect.height, Is.GreaterThanOrEqualTo(60));
                    Assert.That(button.GetComponentInChildren<Text>().fontSize, Is.GreaterThanOrEqualTo(24));
                }
                Assert.That(Ui("Live command HUD/Live recruitment").gameObject.activeInHierarchy, Is.False);
                Assert.That(Ui("Live command HUD/VR HUD pages/VR HIDE").gameObject.activeInHierarchy, Is.True);
                CaptureHud("Simple-" + previews[i]);
            }
            Assert.That(director.AliveBlue, Is.EqualTo(5)); Assert.That(director.AliveRed, Is.EqualTo(5));
            hud.OpenGuide(); yield return null; CaptureHud("Simple-Help");
        }

        [UnityTest] public IEnumerator SimpleUiMenuOwnsPinchAndNeverStartsTheBow()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return null;
            var eye = player.ViewCamera.transform; Vector3 grip = eye.position + eye.forward * .7f - eye.right * .25f;
            hands.Set(true, grip, grip + eye.forward * .08f, false);
            hands.Set(false, grip - eye.forward * .2f, grip - eye.forward * .12f, false); OpticalGameplayFrame();
            hands.Set(false, grip - eye.forward * .2f, grip - eye.forward * .12f, true); OpticalGameplayFrame();
            Assert.That(player.IsDrawingBow, Is.False);
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
            Invoke(hud, "LateUpdate");
            var bow = (Transform)typeof(TowerBattleHud).GetField("bow", Private).GetValue(hud);
            Assert.That(bow.gameObject.activeInHierarchy, Is.False, "The bow must not cover menu controls.");
        }

        [UnityTest] public IEnumerator SimpleUiTrajectoryMatchesGravityAndReleaseDoesNotKickAim()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return null; Invoke(hud, "CloseCommandMenu");
            var eye = player.ViewCamera.transform; Vector3 grip = eye.position + eye.forward * .85f - eye.right * .24f;
            hands.Set(true, grip, grip + eye.forward * .08f, false);
            hands.Set(false, grip - eye.forward * .2f, grip - eye.forward * .12f, false); OpticalGameplayFrame();
            hands.Set(false, grip - eye.forward * .2f, grip - eye.forward * .12f, true); OpticalGameplayFrame();
            hands.Set(false, grip - eye.forward * .6f, grip - eye.forward * .52f, true); OpticalGameplayFrame();
            Assert.That(player.IsDrawingBow, Is.True); Invoke(hud, "LateUpdate");
            var guide = (LineRenderer)typeof(TowerBattleHud).GetField("bowTrajectory", Private).GetValue(hud);
            Assert.That(guide.enabled, Is.True); Assert.That(guide.positionCount, Is.GreaterThan(3));
            var ray = (Ray)typeof(PossessionController).GetField("heldBowAim", Private).GetValue(player);
            float power = (float)typeof(PossessionController).GetField("heldBowPower", Private).GetValue(player);
            Vector3 predicted = ray.origin + ray.direction * Mathf.Lerp(45, 110, power) * .03f + Vector3.down * (.5f * 9.81f * .03f * .03f);
            Assert.That(Vector3.Distance(guide.GetPosition(1), predicted), Is.LessThan(.003f));
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.False);
            CaptureHud("Simple-Bow-trajectory");
            // Finger opening shifts the palm sideways. The arrow keeps the last displayed draw.
            hands.Set(false, grip - eye.forward * .6f + eye.right * .2f, grip - eye.forward * .52f + eye.right * .2f, false);
            OpticalGameplayFrame();
            BattleProjectile arrow = null;
            foreach (var candidate in Object.FindObjectsByType<BattleProjectile>(FindObjectsSortMode.None))
                if (candidate.name == "Summit bow arrow") arrow = candidate;
            Assert.That(arrow, Is.Not.Null);
            Assert.That(Vector3.Angle(arrow.Direction, ray.direction), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(arrow.SpawnOrigin, ray.origin), Is.LessThan(.001f));
            Invoke(hud, "LateUpdate"); Assert.That(guide.enabled, Is.False);
        }
    }
}
