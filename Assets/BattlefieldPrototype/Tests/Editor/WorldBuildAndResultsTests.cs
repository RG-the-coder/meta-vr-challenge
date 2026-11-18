using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator WorldDefenseSelectionClosesMenuAndPinchPlacesOnceOnTerrain()
        {
            battle.NewGame(); FreezeEnemy(); FundHandAudit(); var hands = UseOpticalHands();
            for (int i = 0; i < DefenseCatalog.Count; i++)
            {
                var kind = (DefenseKind)i;
                Invoke(hud, "OpenCommandMenu"); Invoke(hud, "SelectCommandPage", 4);
                yield return null;
                yield return PinchButton(hands, Ui(Works + "Build " + kind));
                Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.False, kind + ": " + battle.Message + "; selected=" + battle.SelectedDefense);
                Assert.That(battle.SelectedDefense, Is.EqualTo(kind));
                var point = DefensePoint(kind); float gold = battle.BlueGold;
                PointHand(hands, point, false); OpticalGameplayFrame();
                PointHand(hands, point, true); OpticalGameplayFrame();
                OpticalGameplayFrame(); OpticalGameplayFrame();
                Assert.That(battle.Defenses.Count, Is.EqualTo(i + 1), battle.Message);
                Assert.That(battle.BlueGold, Is.EqualTo(gold - DefenseCatalog.Cost(kind)).Within(.001f));
                Assert.That(Vector3.Distance(battle.Defenses[i].transform.position, point), Is.LessThan(1));
                PointHand(hands, point, false); OpticalGameplayFrame();
            }
        }

        [UnityTest] public IEnumerator TowerDamageLowersMenuAndDestructionShowsReachableDefeat()
        {
            battle.NewGame(); FreezeEnemy(); UseOpticalHands();
            Invoke(hud, "FinalizeVrHudAnchor", true);
            var panel = hud.Canvas.transform;
            Vector3 position = panel.position; Quaternion rotation = panel.rotation;
            battle.BoundTower.TakeDamage(BattleTower.HealthPerMetre, BattleTeam.Red);
            Assert.That(Vector3.Distance(panel.position, position - Vector3.up), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(rotation, panel.rotation), Is.LessThan(.001f));
            hud.OpenGuide();
            player.ViewCamera.transform.Rotate(0, 100, 0);
            battle.BoundTower.TakeDamage(100000, BattleTeam.Red);
            Assert.That(director.Phase, Is.EqualTo(BattlePhase.Defeat));
            Invoke(hud, "Layout"); hud.Refresh(); Invoke(hud, "FinalizeVrHudAnchor", true);
            var modal = (RectTransform)typeof(TowerBattleHud).GetField("modal", Private).GetValue(hud);
            Assert.That(modal.gameObject.activeInHierarchy, Is.True);
            var eye = player.ViewCamera.transform;
            Assert.That(Vector3.Angle(eye.forward, panel.position - eye.position), Is.LessThan(10));
            Assert.That(((Text)typeof(TowerBattleHud).GetField("modalTitle", Private).GetValue(hud)).text, Is.EqualTo("DEFEAT"));
            var restart = (Button)typeof(TowerBattleHud).GetField("newMatch", Private).GetValue(hud);
            restart.onClick.Invoke();
            Assert.That(director.IsBattleRunning, Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator WorldBuildRayCanPassTheCommandersOwnTower()
        {
            battle.NewGame(); FreezeEnemy(); UseOpticalHands();
            Vector3 ground = DefensePoint(DefenseKind.TimberBarricade);
            Vector3 origin = battle.BoundTower.transform.position + Vector3.up * (battle.BoundTower.Height + .1f);
            var ray = new Ray(origin, (ground - origin).normalized);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(ray, out var blocked, 300), Is.True);
            Assert.That(blocked.collider.GetComponentInParent<BattleTower>(), Is.SameAs(battle.BoundTower));
            object[] aimArgs = { ray, Vector3.zero };
            Assert.That((bool)Invoke(battle, "TryDefenseAimPoint", aimArgs), Is.True);
            var point = (Vector3)aimArgs[1];
            Assert.That(Vector3.Distance(point, ground), Is.LessThan(.1f));
            yield return null;
        }

        [UnityTest] public IEnumerator SummonCardStaysEnabledBeyondFiveAndEnemyTowerDestructionWins()
        {
            StartSiege(); FundHandAudit(); var hands = UseOpticalHands();
            yield return HandPage(hands, "RECRUIT");
            var card = Ui("Live command HUD/Character browser/Control character 0").GetComponent<Button>();
            Assert.That(card.interactable, Is.True);
            Assert.That(card.GetComponentInChildren<Text>().text, Does.Contain("SUMMON"));
            yield return PinchButton(hands, card.transform);
            Assert.That(director.AliveBlue, Is.EqualTo(6), battle.Message);
            hud.Refresh(); Assert.That(card.interactable, Is.True);
            battle.EnemyBoundTower.TakeDamage(100000, BattleTeam.Blue);
            Assert.That(director.Phase, Is.EqualTo(BattlePhase.Victory));
        }
    }
}
