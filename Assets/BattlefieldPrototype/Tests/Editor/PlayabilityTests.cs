using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator PlayabilityFixedSquadsMatchOnEveryMapAndRestart()
        {
            for (int map = 0; map < TowerMapCatalog.Count; map++)
            {
                battle.GoHome(); Assert.That(battle.SelectMap((TowerMapKind)map), Is.True);
                for (int round = 0; round < 2; round++)
                {
                    battle.NewGame(); SummonFixtureSquads(); FreezeEnemy();
                    Assert.That(director.AliveBlue, Is.EqualTo(5)); Assert.That(director.AliveRed, Is.EqualTo(5));
                    Assert.That(director.Units.Count, Is.EqualTo(10));
                    Assert.That(battle.BlueGold, Is.EqualTo(TowerBattle.StartingGold));
                    Assert.That(battle.RedGold, Is.EqualTo(battle.BlueGold));
                    for (int slot = 0; slot < 5; slot++)
                    {
                        var blue = battle.SquadMember(BattleTeam.Blue, slot); var red = battle.SquadMember(BattleTeam.Red, slot);
                        Assert.That(blue.Definition.Role, Is.EqualTo(TowerBattle.SquadRoles[slot]));
                        Assert.That(red.Definition, Is.SameAs(blue.Definition));
                        Assert.That(blue.SpawnTower, Is.SameAs(battle.BoundTower));
                        Assert.That(red.SpawnTower, Is.SameAs(battle.EnemyBoundTower));
                    }
                    yield return null;
                }
            }
        }

        [UnityTest] public IEnumerator PlayabilityNoRecruitmentAfterDeathsOrElapsedTime()
        {
            StartSiege(); var wallets = (float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle);
            wallets[0] = wallets[1] = 10000;
            battle.SquadMember(BattleTeam.Blue, 0).TakeDamage(100000, null);
            battle.SquadMember(BattleTeam.Red, 0).TakeDamage(100000, null);
            foreach (var definition in battle.Definitions)
            {
                Assert.That(battle.TryQuickRecruit(definition.Role, out _), Is.False);
                foreach (var team in new[] { BattleTeam.Blue, BattleTeam.Red })
                {
                    object[] args = { team, team == BattleTeam.Blue ? battle.BoundTower : battle.EnemyBoundTower, definition.Role, Vector3.zero, null };
                    Assert.That((bool)Invoke(battle, "TryRecruit", args), Is.False);
                }
            }
            typeof(BattleDirector).GetProperty("BattleDuration").SetValue(director, 1000f);
            typeof(TowerBattle).GetField("enemyRecruitAt", Private).SetValue(battle, 0f);
            Invoke(battle, "Update");
            Assert.That(director.Units.Count, Is.EqualTo(10));
            Assert.That(director.AliveBlue, Is.EqualTo(4)); Assert.That(director.AliveRed, Is.EqualTo(4));
            battle.ToggleMount(); battle.ChooseRecruit(UnitRole.Ranger);
            Assert.That(battle.Recruitment, Is.Null);
            Assert.That(battle.TryDeploy(UnitRole.Ranger, FreePoint(battle.BoundTower), out _), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator PlayabilitySquadOrdersKeepMenuAndAnchorOpen()
        {
            StartSiege(); var hands = UseOpticalHands(); Invoke(hud, "FinalizeVrHudAnchor", true);
            var position = hud.Canvas.transform.position; var rotation = hud.Canvas.transform.rotation;
            for (int i = 0; i < 3; i++)
            {
                yield return PinchButton(hands, Ui("Live command HUD/Easy commander/Quick troop " + i));
                Assert.That(battle.TroopOrders.Current, Is.EqualTo((BattleOrder)i));
                Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
                Assert.That(director.AliveBlue, Is.EqualTo(5)); Assert.That(director.AliveRed, Is.EqualTo(5));
                Assert.That(Vector3.Distance(position, hud.Canvas.transform.position), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(rotation, hud.Canvas.transform.rotation), Is.LessThan(.001f));
            }
            CaptureHud("Playability-play");
            yield return HandPage(hands, "RECRUIT");
            var cards = Ui("Live command HUD/Character browser").GetComponentsInChildren<Button>().Where(x => x.name.StartsWith("Control character")).ToArray();
            Assert.That(cards.Length, Is.EqualTo(5));
            Assert.That(Ui("Live command HUD/Character browser/Spawn another 0").gameObject.activeInHierarchy, Is.False);
            CaptureHud("Playability-army");
            yield return PinchButton(hands, Ui("Live command HUD/Character browser/Control character 1"));
            yield return new WaitForSecondsRealtime(.2f); Invoke(player, "UpdateSwitch");
            Assert.That(player.CurrentUnit, Is.SameAs(battle.SquadMember(BattleTeam.Blue, 1)));
            Invoke(player, "FinalizeView"); Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(hud.Canvas.transform.position, player.ViewCamera.transform.position), Is.LessThan(2));
        }

        [UnityTest] public IEnumerator PlayabilityHeadMotionKeepsAnchorAndTowerDamageTranslatesItVertically()
        {
            StartSiege(); UseTestHeadset(); Invoke(hud, "FinalizeVrHudAnchor", true);
            var panel = hud.Canvas.transform; Vector3 anchored = panel.position; Quaternion facing = panel.rotation;
            var eye = player.ViewCamera.transform;
            eye.position += Vector3.up * .15f; eye.Rotate(0, 5, 0);
            float eyeY = eye.position.y;
            Invoke(player, "ApplyTowerViewLimits");
            Assert.That(eye.position.y, Is.EqualTo(eyeY).Within(.001f), "Roomscale head movement must not be canceled.");
            battle.BoundTower.TakeDamage(BattleTower.HealthPerMetre * .1f, BattleTeam.Red);
            Invoke(player, "ApplyTowerViewLimits"); Invoke(hud, "FinalizeVrHudAnchor", true); hud.Refresh();
            Assert.That(Vector3.Distance(panel.position, anchored - Vector3.up * .1f), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(panel.rotation, facing), Is.LessThan(.001f));
            Assert.That(eye.position.y, Is.EqualTo(eyeY - .1f).Within(.001f));
            yield return null;
        }

        [UnityTest] public IEnumerator PlayabilityFingerCurlAndHeadMotionCannotSteerMenuPointer()
        {
            StartSiege(); var hands = UseOpticalHands();
            yield return null; // Let the newly opened canvas register its raycast graphics.
            var button = (RectTransform)Ui("Live command HUD/Easy commander/Quick troop 1");
            Canvas.ForceUpdateCanvases(); Vector3 target = button.TransformPoint(button.rect.center);
            var eye = player.ViewCamera.transform;
            Vector3 shoulder = (Vector3)typeof(TowerBattleHud).GetProperty("HandPointerShoulder", Private).GetValue(hud);
            Vector3 dir = (target - shoulder).normalized;
            Vector3 palm = shoulder + dir * .43f;
            hands.Set(false, palm, palm + dir * .07f, false); Invoke(hud, "ProcessInput");
            Assert.That(hud.ControllerOverUI, Is.True, "Open palm must hover the command button.");
            Assert.That(((GameObject)typeof(TowerBattleHud).GetField("hovered", Private).GetValue(hud)).GetComponentInParent<Button>(), Is.SameAs(button.GetComponent<Button>()));
            Vector3 before = Ui("UI pointing dot").position;
            // The palm stays still while the index curls far away from the target.
            hands.Set(false, palm, palm + Vector3.down * .08f, false); Invoke(hud, "ProcessInput");
            Assert.That(Vector3.Distance(Ui("UI pointing dot").position, before), Is.LessThan(.001f));
            eye.position += Vector3.right * .04f; eye.Rotate(0, 3, 0); Invoke(hud, "ProcessInput");
            Assert.That(Vector3.Distance(Ui("UI pointing dot").position, before), Is.LessThan(.001f));
            hands.Set(false, palm, palm + Vector3.down * .09f, true); Invoke(hud, "ProcessInput");
            Assert.That((bool)Invoke(player, "OpticalPinchHeld", false), Is.True, "The finger curl must register as a pinch.");
            Assert.That(battle.TroopOrders.Current, Is.EqualTo(BattleOrder.Hold));
            Assert.That(Vector3.Distance(Ui("UI pointing dot").position, before), Is.LessThan(.001f));
            yield return null;
        }

        [Test] public void PlayabilityPointerLocksBeforeFingerContactAndRecoversAfterLoss()
        {
            var pointer = new HandPointerLatch();
            var target = new Ray(Vector3.zero, Vector3.forward);
            var shifted = new Ray(Vector3.right, Vector3.down);
            pointer.Update(true, false, target, 1, false);
            var closing = pointer.Update(true, false, shifted, 2, true);
            Assert.That(closing.direction, Is.EqualTo(target.direction));
            Assert.That(pointer.Update(true, true, shifted, 3, true).direction, Is.EqualTo(target.direction));
            Assert.That(pointer.RenderRay(shifted).direction, Is.EqualTo(target.direction));
            pointer.Update(false, false, shifted, 4, false);
            Assert.That(pointer.Update(true, false, shifted, 5, false).direction, Is.EqualTo(shifted.direction));
        }

        [UnityTest] public IEnumerator PlayabilityEasyBowVisibleWithOneHandAndNoAccidentalRepeat()
        {
            player.PhysicalHandBow = false; StartSiege(); var hands = UseOpticalHands(); hands.tracked[0] = false;
            Invoke(hud, "ToggleVrHud");
            var eye = player.ViewCamera.transform; Vector3 target = eye.position + eye.forward * 20 + Vector3.up * 10;
            int shots = 0; player.ShotFired += () => shots++;
            PointHand(hands, target, false); OpticalGameplayFrame(); Invoke(hud, "LateUpdate");
            var bow = (Transform)typeof(TowerBattleHud).GetField("bow", Private).GetValue(hud);
            Assert.That(bow.gameObject.activeInHierarchy, Is.True);
            PointHand(hands, target, true); OpticalGameplayFrame(); Assert.That(shots, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.5f); OpticalGameplayFrame(); Assert.That(shots, Is.EqualTo(1));
            hands.tracked[1] = false; OpticalGameplayFrame(); hands.tracked[1] = true; OpticalGameplayFrame();
            Assert.That(shots, Is.EqualTo(1));
            PointHand(hands, target, false); OpticalGameplayFrame(); PointHand(hands, target, true); OpticalGameplayFrame();
            Assert.That(shots, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator PlayabilityPalmPosesDoNotOpenOrCloseTheMenu()
        {
            StartSiege(); var hands = UseOpticalHands(); Invoke(hud, "ToggleVrHud"); hands.tracked[1] = false;
            var eye = player.ViewCamera.transform;
            Vector3 palm = eye.position + eye.forward * .4f - Vector3.up * .3f - eye.right * .2f;
            hands.Set(true, palm, palm + eye.forward * .1f, false); hands.PalmRotation(true, Quaternion.Euler(0, 0, 180));
            Invoke(hud, "UpdateVrHudGesture"); yield return new WaitForSecondsRealtime(.5f); Invoke(hud, "UpdateVrHudGesture");
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.False);
            Invoke(hud, "OpenCommandMenu");
            hands.PalmRotation(true, Quaternion.identity); Invoke(hud, "UpdateVrHudGesture");
            hands.PalmRotation(true, Quaternion.Euler(0, 0, 180)); Invoke(hud, "UpdateVrHudGesture");
            yield return new WaitForSecondsRealtime(.5f); Invoke(hud, "UpdateVrHudGesture");
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest] public IEnumerator PlayabilitySquadEliminationEndsMatchAndRestartRestoresFive()
        {
            foreach (var loser in new[] { BattleTeam.Blue, BattleTeam.Red })
            {
                StartSiege();
                for (int i = 0; i < 5; i++) battle.SquadMember(loser, i).TakeDamage(100000, null);
                Invoke(battle, "Update");
                Assert.That(director.Phase, Is.EqualTo(loser == BattleTeam.Blue ? BattlePhase.Defeat : BattlePhase.Victory));
                hud.Refresh(); CaptureHud("Playability-" + director.Phase);
                yield return null;
            }
            battle.NewGame(); Assert.That(director.AliveBlue, Is.Zero); Assert.That(director.AliveRed, Is.Zero);
            Assert.That(director.AliveBlue, Is.Zero);
        }

        [UnityTest] public IEnumerator PlayabilityMenuButtonRecallsPanelLeftBehindWithoutHidingIt()
        {
            StartSiege(); var hands = UseOpticalHands(); yield return null;
            var eye = player.ViewCamera.transform;
            Vector3 previous = hud.Canvas.transform.position;
            eye.Rotate(0, 150, 0);
            Vector3 palm = eye.position + eye.forward * .4f - Vector3.up * .3f - eye.right * .2f;
            hands.Set(true, palm, palm + eye.forward * .1f, false);
            hands.PalmRotation(true, Quaternion.Euler(0, 0, 180));
            Invoke(hud, "UpdateMenuAccess"); Canvas.ForceUpdateCanvases();
            var access = (Canvas)typeof(TowerBattleHud).GetField("menuAccessCanvas", Private).GetValue(hud);
            yield return PinchButton(hands, access.transform.Find("Open menu"));
            Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
            Assert.That(Vector3.Distance(previous, hud.Canvas.transform.position), Is.GreaterThan(1));
            Assert.That(Vector3.Dot(eye.forward, (hud.Canvas.transform.position - eye.position).normalized), Is.GreaterThan(.8f));
        }
    }
}
