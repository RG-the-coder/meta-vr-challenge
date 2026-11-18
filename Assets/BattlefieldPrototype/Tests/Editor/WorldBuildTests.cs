using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator WorldBuildPinchesPlaceAllDefensesOnceWithBowSuppressed()
        {
            StartSiege(); FundHandAudit(); var hands=UseOpticalHands();
            for(int i=0;i<DefenseCatalog.Count;i++) {
                var kind=(DefenseKind)i;
                yield return HandPage(hands,"BUILD");
                yield return PinchButton(hands,Ui(Works+"Build "+kind));
                yield return PinchButton(hands,Ui(Works+"Place in world"));
                Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy,Is.False);
                Assert.That(battle.SelectedDefense,Is.EqualTo(kind));
                var point=DefensePoint(kind);
                PointHand(hands,point,false); OpticalGameplayFrame();
                Invoke(hud,"LateUpdate");
                var preview=(LineRenderer)typeof(TowerBattleHud).GetField("defensePreview",Private).GetValue(hud);
                Assert.That(preview.enabled,Is.True);
                float gold=battle.BlueGold;
                PointHand(hands,point,true); OpticalGameplayFrame();
                Assert.That(battle.Defenses.Count,Is.EqualTo(i+1),battle.Message);
                Assert.That(battle.BlueGold,Is.EqualTo(gold-DefenseCatalog.Cost(kind)).Within(.01f));
                OpticalGameplayFrame(); Assert.That(battle.Defenses.Count,Is.EqualTo(i+1));
                Assert.That(player.IsDrawingBow,Is.False);
                PointHand(hands,point,false); OpticalGameplayFrame();
            }
            Assert.That(DefenseCatalog.Cost(DefenseKind.StoneBarricade),Is.EqualTo(18));
        }
    }
}
