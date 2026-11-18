using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        const string Command = "Live command HUD/Command station/";
        const string Works = "Live command HUD/Ground works/";
        const string Route = "Live command HUD/Troop orders/";
        const string Guide = "How to play veil/How to play/";

        void FundHandAudit() => ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[0] = 2000;
        IEnumerator HandPage(TestHands hands, string page)
        {
            yield return ShowHandPowers();
            yield return PinchButton(hands, Ui("Live command HUD/VR HUD pages/VR " + page));
            hud.Refresh(); yield return null; Invoke(hud, "LateUpdate"); Canvas.ForceUpdateCanvases();
        }
        void PinchWorld(TestHands hands, Vector3 point)
        {
            PointHand(hands, point, false); OpticalGameplayFrame();
            PointHand(hands, point, true); OpticalGameplayFrame();
        }

        [UnityTest] public IEnumerator HandsParityBuildsEveryDefenseRotatesAndCancelsUsingOnlyPinches()
        {
            StartSiege(); FundHandAudit(); var hands = UseOpticalHands(); yield return ShowHandPowers();
            yield return HandPage(hands, "MAP + TOWERS");
            yield return PinchButton(hands, Ui(Command + "Mount"));
            Assert.That(battle.IsMounted, Is.False); hands = UseOpticalHands();
            for (int i = 0; i < DefenseCatalog.Count; i++)
            {
                yield return HandPage(hands, "BUILD");
                battle.ChooseRecruit(UnitRole.Vanguard); hud.Refresh(); yield return null; Invoke(hud, "LateUpdate");
                yield return PinchButton(hands, Ui("Live command HUD/VR HUD pages/VR BUILD"));
                var kind = (DefenseKind)i;
                yield return PinchButton(hands, Ui(Works + "Build " + kind));
                Assert.That(battle.Recruitment, Is.Null, "Choosing a defense switches away from troop placement.");
                Assert.That(battle.SelectedDefense, Is.EqualTo(kind));
                float angle = battle.DefenseRotation;
                yield return PinchButton(hands, Ui(Works + "Rotate defense"));
                Assert.That(battle.DefenseRotation, Is.EqualTo((angle + 45) % 360));
                if (i == 0) CaptureHud("Hands-defense-controls");
                Vector3 point = DefensePoint(kind);
                Invoke(hud, "ToggleVrHud"); PinchWorld(hands, point);
                Assert.That(battle.Defenses.Count, Is.EqualTo(i + 1), battle.Message);
                OpticalGameplayFrame(); Assert.That(battle.Defenses.Count, Is.EqualTo(i + 1), "Held pinch cannot purchase a second defense.");
                Assert.That((bool)typeof(TowerBattleHud).GetProperty("ShowHandWorldPointer", Private).GetValue(hud), Is.True);
                PointHand(hands, point, false); OpticalGameplayFrame();
                yield return HandPage(hands, "BUILD");
                yield return PinchButton(hands, Ui(Works + "Cancel defense"));
                Assert.That(battle.SelectedDefense, Is.Null);
            }
        }

        [UnityTest] public IEnumerator HandsParityPlansWorldRoutesFromSummitAndUsesEveryOrderButton()
        {
            StartSiege(); var hands = UseOpticalHands();
            Spawn(BattleTeam.Blue, UnitRole.Vanguard, RoutePoint(0, 12));
            int shots = 0; player.ShotFired += () => shots++;
            yield return HandPage(hands, "MAP + TOWERS");
            yield return PinchButton(hands, Ui(Route + "Plan route"));
            Assert.That(battle.IsPlanningRoute, Is.True);
            Invoke(hud, "ToggleVrHud");
            PinchWorld(hands, RoutePoint(0, 20));
            Assert.That(battle.RouteDraft.Count, Is.EqualTo(1), "Summit bow mode must not consume route pinches.");
            OpticalGameplayFrame(); Assert.That(battle.RouteDraft.Count, Is.EqualTo(1));
            PinchWorld(hands, RoutePoint(10, 20)); Assert.That(battle.RouteDraft.Count, Is.EqualTo(2));
            Assert.That(shots, Is.Zero);
            yield return HandPage(hands, "MAP + TOWERS");
            yield return PinchButton(hands, Ui(Route + "Undo route")); Assert.That(battle.RouteDraft.Count, Is.EqualTo(1));
            yield return PinchButton(hands, Ui(Route + "Send route")); Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Route));
            yield return PinchButton(hands, Ui(Route + "Cancel route")); Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Advance));
            yield return HandPage(hands, "COMMAND");
            yield return PinchButton(hands, Ui("Live command HUD/Easy commander/Quick troop 1")); Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Hold));
            yield return PinchButton(hands, Ui("Live command HUD/Easy commander/Quick troop 2")); Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Protect));
            yield return PinchButton(hands, Ui("Live command HUD/Easy commander/Quick troop 0")); Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Advance));
        }

        [UnityTest] public IEnumerator HandsParitySelectsCastsAndCancelsAllSummitSpellsThroughTheMenu()
        {
            StartSiege(); FundHandAudit(); var hands = UseOpticalHands();
            for (int slot = 0; slot < 3; slot++)
            {
                var target = SummitTarget(slot == 2 ? BattleTeam.Blue : BattleTeam.Red, Vector3.forward * 12 + Vector3.right * (slot * 4));
                if (slot == 2) target.TakeDamage(35, null);
                float health = target.Health;
                yield return HandPage(hands, "POWERS");
                yield return PinchButton(hands, Ui("Live command HUD/Summit powers/Summit power " + slot));
                Assert.That(battle.SelectedSummitPower, Is.EqualTo(slot));
                Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.False);
                Assert.That(battle.SummitPowerRemaining(slot), Is.Zero);
                PinchWorld(hands, target.AimPosition);
                Assert.That(battle.SummitPowerRemaining(slot), Is.GreaterThan(0), battle.Message);
                if (slot == 1) Assert.That(target.Health, Is.LessThan(health));
                if (slot == 2) Assert.That(target.Health, Is.GreaterThan(health));
                yield return HandPage(hands, "POWERS");
                yield return PinchButton(hands, Ui("Live command HUD/Summit powers/Select bow"));
                Assert.That(battle.SelectedSummitPower, Is.Null);
            }
        }

        [UnityTest] public IEnumerator HandsParitySelectsTravelsRaisesTowersAndUpgradesTheWave()
        {
            StartSiege(); FundHandAudit(); var hands = UseOpticalHands(); int other = -1;
            for (int i = 0; i < battle.Towers.Count; i++)
                if (battle.Towers[i].Owner == BattleTeam.Blue && battle.Towers[i] != battle.BoundTower) { other = i; break; }
            Assert.That(other, Is.GreaterThanOrEqualTo(0));
            yield return HandPage(hands, "MAP + TOWERS");
            yield return PinchButton(hands, Ui("Live command HUD/Tower network/Tower " + other));
            Assert.That(battle.SelectedTower, Is.SameAs(battle.Towers[other]));
            yield return HandPage(hands, "MAP + TOWERS");
            yield return PinchButton(hands, Ui(Command + "Travel"));
            Assert.That(battle.BoundTower, Is.SameAs(battle.Towers[other]));
            hands = UseOpticalHands(); yield return ShowHandPowers();
            float height = battle.BoundTower.Height;
            yield return PinchButton(hands, Ui(Command + "Raise"));
            Assert.That(battle.BoundTower.Height, Is.GreaterThan(height));
            Invoke(player, "FinalizeView"); hands = UseOpticalHands(); yield return ShowHandPowers();
            Assert.That(hud.Canvas.transform.position.y, Is.EqualTo(player.ViewCamera.transform.position.y + .08f).Within(.01f), "Raising the tower must keep its menu at the new eye height.");
            yield return HandPage(hands, "POWERS");
            yield return PinchButton(hands, Ui("Live command HUD/Summit powers/Upgrade tower wave"));
            Assert.That(battle.BoundTower.PulseLevel, Is.EqualTo(2));
            yield return PinchButton(hands, Ui("Live command HUD/Summit powers/Upgrade tower wave"));
            Assert.That(battle.BoundTower.PulseLevel, Is.EqualTo(3));
            SummitTarget(BattleTeam.Red, Vector3.forward * 5);
            yield return PinchButton(hands, Ui("Live command HUD/Summit powers/Tower wave"));
            Assert.That(battle.BoundTower.PulseRemaining, Is.GreaterThan(0));
        }

        [UnityTest] public IEnumerator HandsParityWaveUsesTwoPinchesWithoutAnAccidentalBowShot()
        {
            StartSiege(); var hands = UseOpticalHands(); Invoke(hud, "ToggleVrHud");
            SummitTarget(BattleTeam.Red, Vector3.forward * 5);
            var eye = player.ViewCamera.transform;
            Vector3 center = eye.position + eye.forward * .5f - eye.up * .2f;
            int shots = 0; player.ShotFired += () => shots++;
            foreach (bool pinched in new[] { false, true })
            {
                hands.Set(true, center - eye.right * .1f, center - eye.right * .1f + eye.forward * .06f, pinched);
                hands.Set(false, center + eye.right * .1f, center + eye.right * .1f + eye.forward * .06f, pinched);
                OpticalGameplayFrame();
            }
            Assert.That(player.WaveGestureCharging, Is.True);
            Assert.That(player.IsDrawingBow, Is.False);
            yield return new WaitForSecondsRealtime(.3f);
            hands.Set(true, center - eye.right * .5f, center - eye.right * .5f + eye.forward * .06f, true);
            hands.Set(false, center + eye.right * .5f, center + eye.right * .5f + eye.forward * .06f, true);
            OpticalGameplayFrame();
            Assert.That(battle.BoundTower.PulseRemaining, Is.GreaterThan(0)); Assert.That(shots, Is.Zero);
            OpticalGameplayFrame(); Assert.That(player.WaveGestureCharging, Is.False);
            hands.tracked[1] = false; OpticalGameplayFrame();
            Assert.That(player.WaveGestureCharging, Is.False); Assert.That(shots, Is.Zero);
        }

        [UnityTest] public IEnumerator HandsParityCanMuteRestartAndReturnHomeThroughPinchButtons()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return ShowHandPowers();
            yield return PinchButton(hands, Ui("Live command HUD/HUD tools/Open guide"));
            yield return PinchButton(hands, Ui(Guide + "Guide chapter 5"));
            Assert.That(Ui(Guide + "Controller haptics").gameObject.activeSelf, Is.False);
            var sound = director.GetComponent<BattleAudio>(); bool muted = sound.Muted;
            yield return PinchButton(hands, Ui(Guide + "Sound")); Assert.That(sound.Muted, Is.EqualTo(!muted));
            yield return PinchButton(hands, Ui(Guide + "Sound")); Assert.That(sound.Muted, Is.EqualTo(muted));
            CaptureHud("Hands-input-settings");
            int round = director.RoundId;
            yield return PinchButton(hands, Ui(Guide + "Restart siege"));
            Assert.That(director.RoundId, Is.GreaterThan(round)); FreezeEnemy();
            Invoke(director, "EndTowerBattle", false); hud.Refresh(); hands = UseOpticalHands(); yield return null;
            yield return PinchButton(hands, Ui("Home and outcome veil/Skyhold briefing/Return home"));
            Assert.That(director.Phase, Is.EqualTo(BattlePhase.Home));
            yield return PinchButton(hands, Ui("Home and outcome veil/Skyhold briefing/Begin siege"));
            Assert.That(director.IsBattleRunning, Is.True);
        }
    }
}
