using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator BothSidesCanSummonBeyondFiveWhileGoldIsStillRequired()
        {
            battle.NewGame(); FreezeEnemy(); FundHandAudit();
            ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[1] = 2000;
            typeof(TowerBattle).GetField("enemyRecruitAt", Private).SetValue(battle, 0f);
            float gold = battle.RedGold;
            Invoke(battle, "Update");
            Assert.That(director.AliveBlue, Is.Zero);
            Assert.That(director.AliveRed, Is.EqualTo(1));
            Assert.That(battle.RedGold, Is.LessThan(gold));
            for (int i = 0; i < 10; i++) Invoke(battle, "TryEnemyReinforcement");
            Assert.That(director.AliveRed, Is.EqualTo(11));
            gold = battle.RedGold;
            Assert.That((bool)Invoke(battle, "TryEnemyReinforcement"), Is.True);
            Assert.That(battle.RedGold, Is.LessThan(gold));
            for (int i = 0; i < 12; i++) Assert.That(battle.TrySummonSlot(0, out _), Is.True, battle.Message);
            float blueGold = battle.BlueGold;
            Assert.That(battle.TrySummonSlot(0, out _), Is.True);
            Assert.That(director.AliveBlue, Is.EqualTo(13));
            Assert.That(battle.BlueGold, Is.EqualTo(blueGold - battle.SummonCost(0)));
            var funds = (float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle);
            funds[0] = funds[1] = 0;
            Assert.That(battle.TrySummonSlot(0, out _), Is.False);
            Assert.That((bool)Invoke(battle, "TryEnemyReinforcement"), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator EnemySkipsUnaffordableRuinsAndMovesTowardAnEnemyTower()
        {
            battle.NewGame(); FreezeEnemy();
            Assert.That((bool)Invoke(battle, "TryEnemyReinforcement"), Is.True);
            var unit = battle.SquadMember(BattleTeam.Red, 0);
            ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[1] = 0;
            var objective = battle.Objective(unit);
            Assert.That(objective, Is.Not.Null);
            Assert.That(objective.IsBroken, Is.False);
            Vector3 start = unit.transform.position;
            Time.timeScale = 4;
            yield return Simulate(4);
            Assert.That(Vector3.Distance(start, unit.transform.position), Is.GreaterThan(1), "Enemy soldiers must leave their spawn without player purchases.");
            Assert.That(Vector3.Distance(unit.transform.position, objective.transform.position), Is.LessThan(Vector3.Distance(start, objective.transform.position)));
        }
    }
}
