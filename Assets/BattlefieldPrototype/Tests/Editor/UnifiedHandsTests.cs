using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator OpenRightPalmCannotChangePagesWhileAiming()
        {
            StartSiege(); var hands = UseOpticalHands();
            Invoke(hud, "FinalizeVrHudAnchor", true);
            var eye = player.ViewCamera.transform;
            Vector3 anchored = hud.Canvas.transform.position;
            Vector3 palm = eye.position + eye.forward * .5f - eye.up * .2f + eye.right * .25f;
            hands.Set(false, palm, palm + eye.forward * .1f, false);
            hands.PalmRotation(false, Quaternion.FromToRotation(Vector3.down, (eye.position - palm).normalized));
            Invoke(hud, "UpdateVrHudGesture");
            yield return new WaitForSecondsRealtime(.16f);
            palm -= eye.right * .24f;
            hands.Set(false, palm, palm + eye.forward * .1f, false);
            hands.PalmRotation(false, Quaternion.FromToRotation(Vector3.down, (eye.position - palm).normalized));
            Invoke(hud, "UpdateVrHudGesture");
            Assert.That((int)typeof(TowerBattleHud).GetField("vrHudPage", Private).GetValue(hud), Is.EqualTo(0));
            Invoke(hud, "UpdateVrHudGesture");
            Assert.That((int)typeof(TowerBattleHud).GetField("vrHudPage", Private).GetValue(hud), Is.EqualTo(0));
            Assert.That(Vector3.Distance(anchored, hud.Canvas.transform.position), Is.LessThan(.001f));
        }

        [UnityTest] public IEnumerator BowGestureChoiceDoesNotHideAnyCommands()
        {
            StartSiege(); var hands = UseOpticalHands();
            yield return HandPage(hands, "POWERS");
            yield return PinchButton(hands, Ui("Live command HUD/Summit powers/Bow gesture"));
            Assert.That(player.PhysicalHandBow, Is.False);
            Assert.That(Ui("Live command HUD/VR HUD pages/VR BUILD").gameObject.activeInHierarchy, Is.True);
            Assert.That(Ui("Live command HUD/VR HUD pages/VR RECRUIT").gameObject.activeInHierarchy, Is.True);
            yield return PinchButton(hands, Ui("Live command HUD/Summit powers/Bow gesture"));
            Assert.That(player.PhysicalHandBow, Is.True);
        }
        [UnityTest] public IEnumerator RaisedLeftPalmOpensOnceAndPinchNeverMovesTheMenu()
        {
            StartSiege(); var hands = UseOpticalHands();
            Invoke(hud, "ToggleVrHud");
            var eye = player.ViewCamera.transform;
            Vector3 palm = eye.position + eye.forward * .4f - Vector3.up * .3f - eye.right * .2f;
            hands.Set(true, palm, palm + eye.forward * .1f, false);
            Invoke(hud, "UpdateVrHudGesture");
            hands.PalmRotation(true, Quaternion.Euler(0, 0, 180));
            Invoke(hud, "UpdateVrHudGesture");
            yield return new WaitForSecondsRealtime(.5f); Invoke(hud, "UpdateVrHudGesture");
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
            Invoke(hud, "FinalizeVrHudAnchor", true);
            Vector3 position = hud.Canvas.transform.position;
            yield return new WaitForSecondsRealtime(.5f); Invoke(hud, "UpdateVrHudGesture");
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
            PointHand(hands, eye.position + eye.forward * 5, true);
            Invoke(hud, "UpdateVrHudGesture");
            PointHand(hands, eye.position + eye.forward * 5, false);
            yield return new WaitForSecondsRealtime(.5f); Invoke(hud, "UpdateVrHudGesture");
            Assert.That(Vector3.Distance(position, hud.Canvas.transform.position), Is.LessThan(.001f));
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
        }
        [Test] public void PalmMenuAcceptsInitialPresentationAndRequiresLoweringBeforeRepeating()
        {
            var gesture = new PalmMenuGesture();
            Assert.That(gesture.Update(true, true, true, 0), Is.False);
            Assert.That(gesture.Update(true, true, true, .5f), Is.True, "Already raised hands can open the menu.");
            Assert.That(gesture.Update(true, true, true, 1), Is.False);
            gesture.Update(false, false, false, 2);
            Assert.That(gesture.Update(true, true, true, 3), Is.False, "Tracking loss cannot rearm a spent gesture.");
            gesture.Update(true, true, false, 4);
            Assert.That(gesture.Update(true, false, true, 5), Is.False);
            Assert.That(gesture.Update(true, true, true, 6), Is.False);
            Assert.That(gesture.Update(true, true, true, 6.5f), Is.True);
        }

        [Test] public void OpenPalmSwipeRejectsJitterPinchesAndRepeatedMovement()
        {
            var gesture = new HandPageSwipe();
            Assert.That(gesture.Update(true, Vector3.zero, 0), Is.Zero);
            Assert.That(gesture.Update(true, Vector3.left * .03f, .15f), Is.Zero);
            Assert.That(gesture.Update(true, Vector3.left * .24f, .3f), Is.EqualTo(1));
            Assert.That(gesture.Update(true, Vector3.left * .5f, .45f), Is.Zero);
            gesture.Update(false, Vector3.zero, .6f);
            Assert.That(gesture.Update(true, Vector3.right * .3f, .7f), Is.Zero);
            Assert.That(gesture.Update(true, Vector3.right * .53f, 1), Is.EqualTo(-1));
            gesture.Update(false, Vector3.zero, 2);
            gesture.Update(true, Vector3.zero, 3);
            Assert.That(gesture.Update(true, new Vector3(.25f, .3f, 0), 3.3f), Is.Zero);
        }

        [UnityTest] public IEnumerator UnifiedEconomyStartsSmallAndCapsSnowballIncomeSymmetrically()
        {
            battle.NewGame(); FreezeEnemy();
            Assert.That(battle.BlueGold, Is.EqualTo(60));
            Assert.That(battle.RedGold, Is.EqualTo(60));
            Assert.That(battle.Income(BattleTeam.Blue), Is.EqualTo(.72f).Within(.001f));
            Assert.That(battle.Income(BattleTeam.Red), Is.EqualTo(.72f).Within(.001f));
            foreach (var tower in battle.Towers)
            {
                typeof(BattleTower).GetProperty("Owner").SetValue(tower, (BattleTeam?)BattleTeam.Blue);
                typeof(BattleTower).GetProperty("Height").SetValue(tower, BattleTower.MaximumHeight);
            }
            Assert.That(battle.Income(BattleTeam.Blue), Is.EqualTo(TowerBattle.MaximumIncome));
            foreach (var tower in battle.Towers) typeof(BattleTower).GetProperty("Owner").SetValue(tower, (BattleTeam?)BattleTeam.Red);
            Assert.That(battle.Income(BattleTeam.Red), Is.EqualTo(TowerBattle.MaximumIncome));
            yield return null;
        }

        [UnityTest] public IEnumerator UnifiedPagesStayAnchoredAcrossPinchSelections()
        {
            StartSiege(); var hands = UseOpticalHands();
            Invoke(hud, "FinalizeVrHudAnchor", true);
            var position = hud.Canvas.transform.position;
            var rotation = hud.Canvas.transform.rotation;
            yield return HandPage(hands, "MAP + TOWERS");
            yield return HandPage(hands, "RECRUIT");
            yield return HandPage(hands, "POWERS");
            Assert.That(Vector3.Distance(position, hud.Canvas.transform.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(rotation, hud.Canvas.transform.rotation), Is.LessThan(.01f));
            CaptureHud("Unified-powers");
            yield return HandPage(hands, "COMMAND"); CaptureHud("Unified-play");
            yield return HandPage(hands, "RECRUIT"); CaptureHud("Unified-army");
            yield return HandPage(hands, "BUILD"); CaptureHud("Unified-build");
        }
    }
}
