using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        void FundWaveUpgrades() => ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[0] = 1000;

        [UnityTest] public IEnumerator TowerWavePushesEveryNearbyGroundAndFlyingEnemyWithoutHealthOrShieldDamage()
        {
            StartSiege(); var tower = battle.BoundTower;
            var enemies = new[] {
                Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.forward * 12),
                Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.back * 12),
                Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.right * 12),
                Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.left * 12),
                Spawn(BattleTeam.Red, UnitRole.Stormwing, tower.transform.position + new Vector3(6, 12, 6)) };
            var ally = Spawn(BattleTeam.Blue, UnitRole.Vanguard, tower.transform.position + Vector3.left * 9);
            var distant = Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.right * 28);
            foreach (var enemy in enemies) enemy.GrantShield(35, 30);
            float gold = battle.BlueGold; int damageEvents = 0;
            director.UnitDamaged += (_, __, ___) => damageEvents++;
            Assert.That(battle.TryTowerPulse(), Is.True);
            Assert.That(battle.BlueGold, Is.EqualTo(gold)); Assert.That(tower.PulseRemaining, Is.EqualTo(20).Within(.01));
            foreach (var enemy in enemies)
            {
                Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth)); Assert.That(enemy.Shield, Is.EqualTo(35));
                Vector3 away = Vector3.ProjectOnPlane(enemy.transform.position - tower.transform.position, Vector3.up).normalized;
                Assert.That(Vector3.Dot(away, enemy.KnockbackVelocity), Is.EqualTo(60).Within(.01));
            }
            Assert.That(ally.KnockbackVelocity, Is.EqualTo(Vector3.zero)); Assert.That(distant.KnockbackVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(battle.TryTowerPulse(), Is.False); yield return Simulate(.65f);
            foreach (var enemy in enemies) Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth));
            Assert.That(damageEvents, Is.Zero);
        }

        [UnityTest] public IEnumerator ShieldBreakUpgradeRemovesOnlyEnemyShieldsWithoutHealthDamageOrSlowing()
        {
            StartSiege(); FundWaveUpgrades(); var tower = battle.BoundTower;
            var ground = Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.right * 28);
            var flyer = Spawn(BattleTeam.Red, UnitRole.Stormwing, tower.transform.position + new Vector3(5, 12, 5));
            var ally = Spawn(BattleTeam.Blue, UnitRole.Vanguard, tower.transform.position + Vector3.left * 12);
            ground.GrantShield(40, 30); flyer.GrantShield(40, 30); ally.GrantShield(45, 30);
            float groundDamageTime = ground.LastDamagedAt, airDamageTime = flyer.LastDamagedAt;
            int damageEvents = 0; director.UnitDamaged += (_, __, ___) => damageEvents++;
            Assert.That(battle.TryUpgradeTowerPulse(), Is.True); Assert.That(tower.PulseLevel, Is.EqualTo(2));
            Assert.That(tower.PulseRadius, Is.EqualTo(32)); Assert.That(battle.BlueGold, Is.EqualTo(925));
            Assert.That(battle.TryTowerPulse(), Is.True); Assert.That(battle.BlueGold, Is.EqualTo(925));
            Assert.That(ground.Shield, Is.Zero); Assert.That(flyer.Shield, Is.Zero); Assert.That(ally.Shield, Is.EqualTo(45));
            Assert.That(ground.Health, Is.EqualTo(ground.MaxHealth)); Assert.That(flyer.Health, Is.EqualTo(flyer.MaxHealth));
            Assert.That(ground.IsSlowed, Is.False); Assert.That(flyer.IsSlowed, Is.False);
            Assert.That(ground.LastDamagedAt, Is.EqualTo(groundDamageTime)); Assert.That(flyer.LastDamagedAt, Is.EqualTo(airDamageTime));
            Assert.That(damageEvents, Is.Zero); Assert.That(tower.PulseRemaining, Is.EqualTo(16).Within(.01));
            yield return null;
        }

        [UnityTest] public IEnumerator AftershockUpgradeAddsWiderRangeAndTemporarySlowAndHasAFiniteMaximum()
        {
            StartSiege(); FundWaveUpgrades(); var tower = battle.BoundTower;
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.right * 37);
            var outside = Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.right * 43);
            enemy.GrantShield(35, 30); outside.GrantShield(35, 30);
            Assert.That(battle.TryUpgradeTowerPulse(), Is.True); Assert.That(battle.TryUpgradeTowerPulse(), Is.True);
            Assert.That(tower.PulseLevel, Is.EqualTo(3)); Assert.That(tower.PulseRadius, Is.EqualTo(40));
            Assert.That(battle.BlueGold, Is.EqualTo(805)); Assert.That(battle.TryUpgradeTowerPulse(), Is.False);
            Assert.That(battle.BlueGold, Is.EqualTo(805)); Assert.That(battle.TryTowerPulse(), Is.True);
            Assert.That(enemy.Shield, Is.Zero); Assert.That(enemy.IsSlowed, Is.True); Assert.That(enemy.Health, Is.EqualTo(enemy.MaxHealth));
            Assert.That(outside.Shield, Is.EqualTo(35)); Assert.That(outside.IsSlowed, Is.False); Assert.That(outside.KnockbackVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(tower.PulseRemaining, Is.EqualTo(12).Within(.01));
            yield return Simulate(3.3f); Assert.That(enemy.IsSlowed, Is.False);
        }

        [UnityTest] public IEnumerator TowerWaveCooldownAndUpgradesBelongToEachTowerAndCannotBeResetByTravelOrUpgrading()
        {
            StartSiege(); FundWaveUpgrades(); var original = battle.BoundTower; var watch = battle.Towers[2];
            Spawn(BattleTeam.Red, UnitRole.Vanguard, original.transform.position + Vector3.right * 12);
            Spawn(BattleTeam.Red, UnitRole.Vanguard, watch.transform.position + Vector3.right * 12);
            Assert.That(battle.TryTowerPulse(), Is.True); Assert.That(battle.TryUpgradeTowerPulse(), Is.True);
            Assert.That(original.PulseRemaining, Is.EqualTo(20).Within(.01), "An upgrade changes future cooldowns, not an active one.");
            battle.ToggleMount(); battle.ToggleMount(); Assert.That(battle.TryTowerPulse(), Is.False);
            Assert.That(battle.Teleport(watch), Is.True); Assert.That(watch.PulseLevel, Is.EqualTo(1));
            Assert.That(battle.TryTowerPulse(), Is.True); Assert.That(watch.PulseRemaining, Is.EqualTo(20).Within(.01));
            Assert.That(battle.Teleport(original), Is.True); Assert.That(original.PulseLevel, Is.EqualTo(2));
            Assert.That(battle.TryTowerPulse(), Is.False);
            original.TakeDamage(165, BattleTeam.Red); Assert.That(original.PulseLevel, Is.EqualTo(2));
            Assert.That(original.PulseRemaining, Is.GreaterThan(19.9f));
            director.RestartBattle(); FreezeEnemy();
            foreach (var tower in battle.Towers) { Assert.That(tower.PulseLevel, Is.EqualTo(1)); Assert.That(tower.PulseRemaining, Is.Zero); }
            yield return null;
        }

        [UnityTest] public IEnumerator EmptyWavesAndUnaffordableUpgradesDoNotConsumeResourcesAndPushesStopAtCover()
        {
            StartSiege(); var tower = battle.BoundTower;
            Assert.That(battle.TryTowerPulse(), Is.False); Assert.That(tower.PulseRemaining, Is.Zero);
            battle.TrySpend(BattleTeam.Blue, Mathf.FloorToInt(battle.BlueGold));
            Assert.That(battle.TryUpgradeTowerPulse(), Is.False); Assert.That(tower.PulseLevel, Is.EqualTo(1));
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.forward * 10);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform);
            wall.transform.position = enemy.transform.position + Vector3.forward * 2 + Vector3.up * 2;
            wall.transform.localScale = new Vector3(6, 4, .5f); Physics.SyncTransforms(); float health = enemy.Health;
            Assert.That(battle.TryTowerPulse(), Is.True, "The base tower wave can be cast without gold.");
            yield return Simulate(.7f);
            Assert.That(enemy.transform.position.z, Is.LessThan(wall.GetComponent<Collider>().bounds.min.z)); Assert.That(enemy.Health, Is.EqualTo(health));
        }

        [UnityTest] public IEnumerator TowerWaveHudUpgradesAndCastsWithoutAimingAndFourthPowerInputUsesTheBoundTower()
        {
            StartSiege(); FundWaveUpgrades(); hud.Refresh(); yield return null; hud.Refresh();
            var tower = battle.BoundTower; battle.SelectedTower = battle.EnemyBoundTower;
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, tower.transform.position + Vector3.right * 12); enemy.GrantShield(35, 30);
            Invoke(hud, "SelectCommandPage", 3); yield return null; Canvas.ForceUpdateCanvases();
            var panel = hud.Canvas.transform.Find("Live command HUD/Summit powers"); Invoke(hud, "CancelPointer");
            void AssertFits()
            {
                hud.Refresh(); Canvas.ForceUpdateCanvases();
                foreach (string name in new[] { "Summit instructions", "Tower wave/Label", "Upgrade tower wave/Label" })
                {
                    var label = panel.Find(name).GetComponent<Text>();
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), name + " must fit.");
                }
            }
            AssertFits(); Click(panel.Find("Upgrade tower wave")); Assert.That(tower.PulseLevel, Is.EqualTo(2));
            Assert.That(battle.EnemyBoundTower.PulseLevel, Is.EqualTo(1), "Upgrades apply to the bound tower, never a selected enemy tower.");
            AssertFits(); Click(panel.Find("Tower wave")); Assert.That(enemy.Shield, Is.Zero);
            Assert.That(panel.Find("Tower wave").GetComponent<Button>().interactable, Is.False);
            Click(panel.Find("Upgrade tower wave")); Assert.That(tower.PulseLevel, Is.EqualTo(3)); AssertFits();
            Assert.That(tower.PulseRemaining, Is.GreaterThan(15.9f)); Assert.That(panel.Find("Upgrade tower wave").GetComponent<Button>().interactable, Is.False);
            hud.SendMessage("LateUpdate"); CaptureHud("Repulsion-wave-max-upgrade");
            var watch = battle.Towers[2]; Assert.That(battle.Teleport(watch), Is.True);
            Spawn(BattleTeam.Red, UnitRole.Vanguard, watch.transform.position + Vector3.right * 12);
            Invoke(player, "HandleTowerPowers", false, false, 1 << 3);
            Assert.That(watch.PulseRemaining, Is.GreaterThan(19.9f));
        }
    }
}
