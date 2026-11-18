using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator EasyCommanderPinchOrdersKeepTheMenuOpenWithoutMovingThePlayer()
        {
            StartSiege(); var hands = UseOpticalHands(); var tower = battle.BoundTower;
            yield return null;
            int shots = 0; player.ShotFired += () => shots++;
            for (int i = 0; i < 3; i++)
            {
                yield return PinchButton(hands, Ui("Live command HUD/Easy commander/Quick troop " + i));
                Assert.That(battle.TroopOrders.Current, Is.EqualTo((BattleOrder)i));
                Assert.That(director.AliveBlue, Is.EqualTo(5)); Assert.That(director.AliveRed, Is.EqualTo(5));
                Assert.That(player.CurrentUnit, Is.Null); Assert.That(battle.IsMounted, Is.True);
                Assert.That(battle.BoundTower, Is.SameAs(tower));
                Assert.That(Ui("Live command HUD").gameObject.activeInHierarchy, Is.True);
                OpticalGameplayFrame(); Assert.That(shots, Is.Zero);
            }
        }

        [UnityTest] public IEnumerator EasyCommanderFixedSquadsRejectReinforcementsAndOrdersAreFree()
        {
            StartSiege();
            var wallets = (float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle);
            foreach (float gold in new[] { 0f, 500f })
            {
                wallets[0] = gold;
                foreach (var role in new[] { UnitRole.Vanguard, UnitRole.Ranger, UnitRole.Medic })
                    Assert.That(battle.TryQuickRecruit(role, out _), Is.False);
                Assert.That(battle.BlueGold, Is.EqualTo(gold)); Assert.That(director.AliveBlue, Is.EqualTo(5));
                Assert.That(battle.CommandSquad(BattleOrder.Hold), Is.True, "Orders are free, even without gold.");
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EasyCommanderShootsWithOneHandAndRequiresReleaseAfterTrackingLoss()
        {
            yield return CreateBasicUi(); player.PhysicalHandBow = false; StartSiege(); var hands = UseOpticalHands();
            hands.tracked[0] = false; Invoke(hud, "ToggleVrHud");
            var eye = player.ViewCamera.transform;
            Vector3 target = eye.position + eye.forward * 10 + Vector3.up * 10;
            int shots = 0; player.ShotFired += () => shots++;
            PointHand(hands, target, false); OpticalGameplayFrame();
            PointHand(hands, target, true); OpticalGameplayFrame(); Assert.That(shots, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.55f); OpticalGameplayFrame();
            Assert.That(shots, Is.EqualTo(1), "A held pinch fires only once, even after cooldown.");
            hands.tracked[1] = false; OpticalGameplayFrame();
            hands.tracked[1] = true; OpticalGameplayFrame();
            Assert.That(shots, Is.EqualTo(1), "Reconnecting a pinched hand must not shoot.");
            PointHand(hands, target, false); OpticalGameplayFrame();
            PointHand(hands, target, true); OpticalGameplayFrame(); Assert.That(shots, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator EasyCommanderAimAssistHitsANearbyVisibleTarget()
        {
            yield return CreateBasicUi(); StartSiege();
            var enemy = SummitTarget(BattleTeam.Red, Vector3.forward * 18);
            float health = enemy.Health;
            var ray = SummitAim(enemy.AimPosition);
            ray.direction = Quaternion.AngleAxis(4, Vector3.up) * ray.direction;
            Assert.That(battle.TryFireEasyBow(ray), Is.True);
            yield return Simulate(.4f);
            Assert.That(enemy.Health, Is.LessThan(health), "A small pointing error should still hit a visible enemy.");
        }

        [UnityTest] public IEnumerator UnifiedOpeningIgnoresLegacyModePreference()
        {
            yield return CreateBasicUi(); battle.NewGame();
            Assert.That((float)typeof(TowerBattle).GetField("enemyRecruitAt", Private).GetValue(battle), Is.EqualTo(20));
            Assert.That((float)typeof(TowerBattle).GetField("enemyBuildAt", Private).GetValue(battle), Is.EqualTo(75));
            PlayerPrefs.SetInt(TowerBattleHud.FocusKey, 0); battle.NewGame();
            Assert.That((float)typeof(TowerBattle).GetField("enemyRecruitAt", Private).GetValue(battle), Is.EqualTo(20));
            Assert.That((float)typeof(TowerBattle).GetField("enemyBuildAt", Private).GetValue(battle), Is.EqualTo(75));
            yield return null;
        }
    }
}
