using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator HandsSummonPresetCardsSpendGoldOnceAndKeepMenuOpen()
        {
            battle.NewGame(); FreezeEnemy(); var hands = UseOpticalHands();
            yield return HandPage(hands, "RECRUIT");
            Assert.That(director.AliveBlue, Is.Zero); Assert.That(director.AliveRed, Is.Zero);
            Assert.That(director.IsBattleRunning, Is.True);
            Vector3 anchor = hud.Canvas.transform.position;
            CaptureHud("Hands-Summon-available");
            for (int slot = 0; slot < 5; slot++)
            {
                var card = (RectTransform)Ui("Live command HUD/Character browser/Control character " + slot);
                Assert.That(card.GetComponentInChildren<Text>().text, Does.Contain("SUMMON"));
                Canvas.ForceUpdateCanvases(); Vector3 target = card.TransformPoint(card.rect.center);
                float gold = battle.BlueGold; int cost = battle.SummonCost(slot);
                PointHand(hands, target, false); Invoke(hud, "ProcessInput");
                PointHand(hands, target, true); Invoke(hud, "ProcessInput");
                for (int held = 0; held < 4; held++) Invoke(hud, "ProcessInput");
                Assert.That(battle.BlueGold, Is.EqualTo(gold - cost).Within(.001f));
                Assert.That(director.AliveBlue, Is.EqualTo(slot + 1)); Assert.That(director.AliveRed, Is.EqualTo(slot + 1));
                Assert.That(player.CurrentUnit, Is.Null, "A held summon pinch must not also select PLAY AS.");
                Assert.That(battle.SquadMember(BattleTeam.Blue, slot).Definition.Role, Is.EqualTo(TowerBattle.SquadRoles[slot]));
                Assert.That(battle.SquadMember(BattleTeam.Red, slot).Definition.Role, Is.EqualTo(TowerBattle.SquadRoles[slot]));
                Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
                Assert.That(Vector3.Distance(anchor, hud.Canvas.transform.position), Is.LessThan(.001f));
                PointHand(hands, target, false); Invoke(hud, "ProcessInput"); yield return null;
            }
            battle.SquadMember(BattleTeam.Blue, 0).TakeDamage(100000, null); hud.Refresh();
            Assert.That(battle.TrySummonSlot(0, out _), Is.False);
            Assert.That(Ui("Live command HUD/Character browser/Control character 0").GetComponent<Button>().interactable, Is.False);
            CaptureHud("Hands-Summon-deployed");
        }

        [UnityTest] public IEnumerator HandsSummonGoldIncomeUnlocksAnUnaffordableCard()
        {
            battle.NewGame(); FreezeEnemy(); var hands = UseOpticalHands();
            var funds = (float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle);
            funds[0] = 0; yield return HandPage(hands, "RECRUIT");
            Assert.That(battle.TrySummonSlot(0, out _), Is.False);
            Assert.That(battle.WasSummoned(BattleTeam.Blue, 0), Is.False);
            Assert.That(director.AliveRed, Is.Zero);
            var card = Ui("Live command HUD/Character browser/Control character 0").GetComponent<Button>();
            Assert.That(card.interactable, Is.False);
            funds[0] = battle.SummonCost(0) - .1f;
            hud.Refresh();
            var treasury = (Text)typeof(TowerBattleHud).GetField("economy", Private).GetValue(hud);
            Assert.That(treasury.text, Does.Contain("GOLD 9 "), "Do not round unaffordable gold up to the purchase price.");
            yield return Simulate(.4f); hud.Refresh();
            Assert.That(battle.BlueGold, Is.GreaterThanOrEqualTo(battle.SummonCost(0)));
            Assert.That(card.interactable, Is.True);
            yield return PinchButton(hands, card.transform);
            Assert.That(director.AliveBlue, Is.EqualTo(1)); Assert.That(battle.BlueGold, Is.LessThan(1));
        }

        [UnityTest] public IEnumerator HandsSummonReservesSurviveCasualtiesAndResetOnEveryMap()
        {
            for (int map = 0; map < TowerMapCatalog.Count; map++)
            {
                battle.GoHome(); battle.SelectMap((TowerMapKind)map); battle.NewGame(); FreezeEnemy();
                Invoke(battle, "Update"); Assert.That(director.IsBattleRunning, Is.True);
                Assert.That(director.AliveBlue, Is.Zero);
                Assert.That(battle.TrySummonSlot(0, out _), Is.True);
                battle.SquadMember(BattleTeam.Blue, 0).TakeDamage(100000, null);
                battle.SquadMember(BattleTeam.Red, 0).TakeDamage(100000, null);
                Invoke(battle, "Update"); Assert.That(director.IsBattleRunning, Is.True);
                for (int slot = 1; slot < 5; slot++) Assert.That(battle.TrySummonSlot(slot, out _), Is.True, battle.Message);
                Assert.That(battle.TryQuickRecruit(UnitRole.Vanguard, out _), Is.False);
                for (int slot = 1; slot < 5; slot++) battle.SquadMember(BattleTeam.Blue, slot).TakeDamage(100000, null);
                Invoke(battle, "Update"); Assert.That(director.Phase, Is.EqualTo(BattlePhase.Defeat));
                yield return null;
            }
            battle.NewGame(); Assert.That(director.AliveBlue, Is.Zero); Assert.That(director.AliveRed, Is.Zero);
            Assert.That(director.AliveBlue, Is.Zero);
        }

        [UnityTest] public IEnumerator HandsBuildEveryDefenseOnMapWithoutLeavingTower()
        {
            StartSiege(); FundHandAudit(); var hands = UseOpticalHands();
            Vector3 eye = player.ViewCamera.transform.position;
            for (int index = 0; index < DefenseCatalog.Count; index++)
            {
                var kind = (DefenseKind)index;
                yield return HandPage(hands, "BUILD");
                yield return PinchButton(hands, Ui(Works + "Build " + kind));
                Assert.That(battle.IsMounted, Is.True); Assert.That(player.CurrentUnit, Is.Null);
                Assert.That(Vector3.Distance(eye, player.ViewCamera.transform.position), Is.LessThan(.001f));
                Assert.That(hud.Minimap.IsBuildingMap, Is.True);
                Assert.That(Ui("Live command HUD/Height-based minimap").gameObject.activeInHierarchy, Is.True);
                yield return PinchButton(hands, Ui(Works + "Rotate defense"));
                float angle = battle.DefenseRotation;
                var point = DefensePoint(kind);
                // Use the actual optical pointer and the map's world-coordinate hit path.
                hud.Minimap.Unproject(Vector2.zero);
                Vector2 local = (Vector2)Invoke(hud.Minimap, "Project", point);
                Vector3 target = hud.Minimap.rectTransform.TransformPoint(local);
                float gold = battle.BlueGold;
                PointHand(hands, target, false); Invoke(hud, "ProcessInput");
                PointHand(hands, target, true); Invoke(hud, "ProcessInput");
                Invoke(hud, "ProcessInput"); Invoke(hud, "ProcessInput");
                Assert.That(battle.Defenses.Count, Is.EqualTo(index + 1), battle.Message);
                Assert.That(battle.BlueGold, Is.EqualTo(gold - DefenseCatalog.Cost(kind)).Within(.001f));
                Assert.That(battle.Defenses[index].transform.eulerAngles.y, Is.EqualTo(angle).Within(.01f));
                PointHand(hands, target, false); Invoke(hud, "ProcessInput");
                Assert.That(player.IsDrawingBow, Is.False);
                if (index == 0) { hud.Refresh(); Invoke(hud, "LateUpdate"); CaptureHud("Hands-Build-map"); }
                yield return PinchButton(hands, Ui(Works + "Cancel defense"));
                Assert.That(battle.SelectedDefense, Is.Null);
            }
            battle.ChooseDefense(DefenseKind.TimberBarricade);
            Invoke(hud, "CloseCommandMenu"); Invoke(hud, "OpenCommandMenu");
            Assert.That((int)typeof(TowerBattleHud).GetField("vrHudPage", Private).GetValue(hud), Is.EqualTo(1));
            yield return PinchButton(hands, Ui("Live command HUD/VR HUD pages/VR COMMAND"));
            Assert.That(battle.SelectedDefense, Is.Null, "PLAY must leave building mode so the bow is usable.");
            battle.ChooseDefense(DefenseKind.TimberBarricade);
            Assert.That(battle.ChooseSummitPower(null), Is.True);
            Assert.That(battle.SelectedDefense, Is.Null, "Choosing the bow must leave placement mode.");
        }
    }
}
