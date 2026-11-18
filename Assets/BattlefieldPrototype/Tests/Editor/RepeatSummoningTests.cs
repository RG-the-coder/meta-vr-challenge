using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator RepeatSummonsBuySameTypeReplaceCasualtiesAndNeverPossess()
        {
            battle.NewGame(); FreezeEnemy(); FundHandAudit(); var hands=UseOpticalHands();
            yield return HandPage(hands,"RECRUIT");
            var card=Ui("Live command HUD/Character browser/Control character 0");
            BattleUnit first=null;
            for(int i=0;i<5;i++) {
                yield return PinchButton(hands,card);
                Assert.That(director.AliveBlue,Is.EqualTo(i+1));
                Assert.That(director.AliveRed,Is.LessThanOrEqualTo(i+1));
                Assert.That(card.GetComponentInChildren<Text>().text,Does.Contain("SUMMON"));
                Assert.That(player.CurrentUnit,Is.Null);
                if(i==0) first=battle.SquadMember(BattleTeam.Blue,0);
            }
            Assert.That(battle.TrySummonSlot(0,out _),Is.False);
            Assert.That(player.Possess(first),Is.False);
            first.TakeDamage(100000,null); Invoke(battle,"Update"); hud.Refresh();
            Assert.That(director.IsBattleRunning,Is.True);
            yield return PinchButton(hands,card);
            Assert.That(director.AliveBlue,Is.EqualTo(5));
            Assert.That(director.AliveRed,Is.LessThanOrEqualTo(5));
            foreach(var unit in director.Units) if(unit.Team==BattleTeam.Blue && unit.IsAlive) unit.TakeDamage(100000,null);
            Invoke(battle,"Update");
            Assert.That(director.IsBattleRunning,Is.True,"An empty squad must still be able to summon replacements.");
            Assert.That(battle.TrySummonSlot(0,out _),Is.True);
        }
    }
}
