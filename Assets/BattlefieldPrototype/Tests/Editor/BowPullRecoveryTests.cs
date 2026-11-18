using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [Test] public void BowPullAcceptsTiltedWristsAndHoldingTriggerBeforeReachingTheString()
        {
            var draw = new BowDrawGesture();
            draw.Update(true, true, Vector3.zero, Vector3.back * .6f, Vector3.right, out _);
            Assert.That(draw.Drawing, Is.False);
            draw.Update(true, true, Vector3.zero, Vector3.back * .24f, Vector3.right, out _);
            Assert.That(draw.Drawing, Is.True);
            draw.Update(true, true, Vector3.zero, Vector3.back * .28f, Vector3.up, out _);
            Assert.That(draw.Pull, Is.EqualTo(.04f).Within(.001f), "A short pull must work from the actual nocking position.");
            Assert.That(draw.Update(true, false, Vector3.zero, Vector3.back * .28f, Vector3.left, out float power), Is.True);
            Assert.That(power, Is.InRange(.2f, .4f));
            Assert.That(draw.Update(true, false, Vector3.zero, Vector3.back * .28f, out _), Is.False);
        }

        [UnityTest] public IEnumerator VrBowPullKeepsStringOnHandAfterClosingMenu()
        {
            StartSiege(); UseTestHeadset(); Invoke(hud, "FinalizeVrHudAnchor", true);
            Invoke(hud, "CloseCommandMenu");
            var view = player.ViewCamera.transform;
            Vector3 grip = view.position + view.forward * .4f - view.right * .2f - view.up * .1f;
            var left = new Pose(grip, Quaternion.LookRotation(view.right, view.up));
            var right = new Pose(grip - view.forward * .24f, Quaternion.identity);
            int arrows = root.GetComponentsInChildren<BattleProjectile>().Length;
            // Clear the required release following the player's disabled/tracking state.
            typeof(TowerBattleHud).GetProperty("ControllerOverUI").SetValue(hud, true);
            Invoke(player, "UpdatePhysicalBow", true, left, true, right, false);
            Invoke(player, "UpdatePhysicalBow", true, left, true, right, true);
            Assert.That(player.IsDrawingBow, Is.True, "A nearby HUD must not steal deliberate nocking.");
            typeof(PossessionController).GetField("desktopPointerActive", Private).SetValue(player, true);
            Assert.That((bool)Invoke(player, "ShouldUsePhysicalBow", true, false, true), Is.True,
                "An active physical draw must survive mirror mouse activity.");
            Invoke(hud, "ProcessInput");
            Assert.That(hud.ControllerOverUI, Is.False);
            Assert.That((bool)typeof(PossessionController).GetProperty("WantsPhysicalBowInput", Private).GetValue(player), Is.True);

            right.position = grip - view.forward * .64f;
            left.rotation = Quaternion.LookRotation(-view.forward, view.up);
            Invoke(player, "UpdatePhysicalBow", true, left, true, right, true);
            Assert.That(player.BowPull, Is.EqualTo(.4f).Within(.001f));
            Invoke(hud, "UpdateBowPresentation");
            var bowString = (LineRenderer)typeof(TowerBattleHud).GetField("bowString", Private).GetValue(hud);
            Assert.That(Vector3.Distance(bowString.transform.TransformPoint(bowString.GetPosition(1)), right.position), Is.LessThan(.005f));
            var arrow = (Transform)typeof(TowerBattleHud).GetField("bowArrow", Private).GetValue(hud);
            Assert.That(Vector3.Distance(arrow.TransformPoint(Vector3.back * BowDrawGesture.RestDistance), right.position), Is.LessThan(.005f));
            Assert.That(root.GetComponentsInChildren<BattleProjectile>().Length, Is.EqualTo(arrows));
            Invoke(player, "UpdatePhysicalBow", true, left, true, right, false);
            Assert.That(root.GetComponentsInChildren<BattleProjectile>().Length, Is.EqualTo(arrows + 1));
            Invoke(player, "UpdatePhysicalBow", true, left, true, right, false);
            Assert.That(root.GetComponentsInChildren<BattleProjectile>().Length, Is.EqualTo(arrows + 1));
            Assert.That((bool)Invoke(player, "ShouldUsePhysicalBow", true, false, false), Is.True,
                "Idle tracked hands must observe trigger releases despite mirror pointer ownership.");
            yield return null;
        }
    }
}
