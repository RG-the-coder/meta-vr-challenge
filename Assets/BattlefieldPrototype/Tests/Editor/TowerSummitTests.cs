using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        Ray SummitAim(Vector3 target) => new Ray(player.ViewCamera.transform.position, target - player.ViewCamera.transform.position);
        BattleUnit SummitTarget(BattleTeam team, Vector3 offset) => Spawn(team, UnitRole.Stormwing,
            battle.BoundTower.transform.position + Vector3.up * battle.BoundTower.Height + offset);

        [UnityTest] public IEnumerator BindingChangesRequireSummitIncludingCrossTowerPossession()
        {
            StartSiege(); var original = battle.BoundTower; var watch = battle.Towers[2];
            var ally = Spawn(BattleTeam.Blue, UnitRole.Militia, FreePoint(watch, UnitRole.Militia), watch);
            battle.ToggleMount();
            Assert.That(battle.Teleport(watch), Is.False); battle.TeleportNext();
            Assert.That(battle.BoundTower, Is.SameAs(original));
            Assert.That(player.Possess(ally), Is.False, "Ground possession cannot change the binding indirectly.");
            battle.ReturnToTower();
            Assert.That(player.Possess(ally), Is.True);
            Assert.That(battle.Teleport(original), Is.False, "A pending possession must lock summit actions immediately.");
            yield return Simulate(.15f);
            Assert.That(player.CurrentUnit, Is.SameAs(ally));
            Assert.That(battle.Teleport(original), Is.False);
            battle.ReturnToTower();
            Assert.That(battle.Teleport(original), Is.True);
            Assert.That(battle.Teleport(battle.EnemyBoundTower), Is.False);
            Assert.That(battle.Teleport(battle.Towers[4]), Is.False);
            watch.TakeDamage(100000, BattleTeam.Red);
            Assert.That(battle.Teleport(watch), Is.False);
            Assert.That(battle.BoundTower, Is.SameAs(original));
        }

        [UnityTest] public IEnumerator EverySummitActionLocksOnGroundDuringPossessionAndOnBrokenOrUnownedTowers()
        {
            Assert.That(battle.CanUseSummitActions, Is.False);
            StartSiege(); var tower = battle.BoundTower;
            var enemy = SummitTarget(BattleTeam.Red, Vector3.forward * 10);
            var ally = Spawn(BattleTeam.Blue, UnitRole.Vanguard, FreePoint(tower));
            Ray aim = SummitAim(enemy.EyePosition);
            void AssertLocked()
            {
                float gold = battle.BlueGold;
                Assert.That(battle.CanUseSummitActions, Is.False);
                Assert.That(battle.TryFireSummitBow(aim), Is.False);
                Assert.That(battle.TryTowerPulse(), Is.False);
                Assert.That(battle.TryUpgradeTowerPulse(), Is.False);
                for (int i = 0; i < 3; i++) Assert.That(battle.TryUseSummitPower(i, aim), Is.False);
                Assert.That(battle.ChooseSummitPower(0), Is.False);
                Assert.That(battle.Teleport(battle.Towers[2]), Is.False);
                Assert.That(battle.BlueGold, Is.EqualTo(gold));
            }
            battle.ToggleMount(); AssertLocked();
            Assert.That(player.Possess(ally), Is.True); AssertLocked();
            yield return Simulate(.15f); AssertLocked();
            battle.ReturnToTower(); Assert.That(battle.CanUseSummitActions, Is.True);
            var eye = player.ViewCamera.transform.position;
            player.ViewCamera.transform.position += Vector3.right * 10; AssertLocked();
            player.ViewCamera.transform.position = eye;
            typeof(BattleTower).GetProperty("Owner").SetValue(tower, BattleTeam.Red); AssertLocked();
            typeof(BattleTower).GetProperty("Owner").SetValue(tower, BattleTeam.Blue);
            tower.TakeDamage(100000, BattleTeam.Red); AssertLocked();
            battle.GoHome(); AssertLocked();
        }

        [UnityTest] public IEnumerator StrongerSummitBowUsesPhysicalArrowsAndCannotResetItsFireRateByRemounting()
        {
            StartSiege();
            var target = Spawn(BattleTeam.Red, UnitRole.Vanguard, battle.BoundTower.transform.position + Vector3.forward * 18);
            float health = target.Health, gold = battle.BlueGold;
            var aim = SummitAim(target.EyePosition);
            Assert.That(battle.TryFireSummitBow(aim), Is.True);
            Assert.That(battle.TryFireSummitBow(aim), Is.False);
            battle.ToggleMount(); battle.ToggleMount();
            Assert.That(battle.TryFireSummitBow(aim), Is.False);
            Assert.That(battle.BlueGold, Is.EqualTo(gold));
            yield return Simulate(.3f);
            Assert.That(target.Health, Is.EqualTo(Mathf.Max(0, health - TowerBattle.SummitBowDamage *
                (1 - target.Definition.ArmorFraction))).Within(.01));
            yield return Simulate(.2f);
            Assert.That(battle.TryFireSummitBow(SummitAim(target.transform.position + Vector3.up)), Is.True);
        }

        [UnityTest] public IEnumerator RepulseThrowsRaidersAwayWithoutDamageShieldLossOrSpending()
        {
            StartSiege(); var tower = battle.BoundTower;
            var raider = SummitTarget(BattleTeam.Red, Vector3.forward * 5);
            var ally = SummitTarget(BattleTeam.Blue, Vector3.right * 8);
            raider.GrantShield(35, 8);
            float health = raider.Health, shield = raider.Shield, lastDamage = raider.LastDamagedAt, gold = battle.BlueGold;
            int damageEvents = 0; director.UnitDamaged += (_, __, ___) => damageEvents++;
            Invoke(tower, "TickCapture", 2f); Assert.That(tower.CaptureProgress, Is.GreaterThan(0));
            Assert.That(battle.TryUseSummitPower(0, SummitAim(raider.EyePosition)), Is.True);
            Assert.That(battle.BlueGold, Is.EqualTo(gold));
            Assert.That(battle.TryUseSummitPower(0, SummitAim(raider.EyePosition)), Is.False);
            yield return Simulate(.7f);
            Assert.That(Vector3.ProjectOnPlane(raider.transform.position - tower.transform.position, Vector3.up).magnitude, Is.GreaterThan(11));
            Assert.That(raider.Health, Is.EqualTo(health)); Assert.That(raider.Shield, Is.EqualTo(shield));
            Assert.That(raider.LastDamagedAt, Is.EqualTo(lastDamage)); Assert.That(damageEvents, Is.Zero);
            Assert.That(ally.KnockbackVelocity, Is.EqualTo(Vector3.zero)); Assert.That(tower.CaptureProgress, Is.Zero);
        }

        [UnityTest] public IEnumerator PaidSpellsChargeOnceRespectTeamsAndKeepCooldownAcrossTravel()
        {
            StartSiege();
            var enemy = SummitTarget(BattleTeam.Red, Vector3.forward * 12);
            var ally = SummitTarget(BattleTeam.Blue, Vector3.forward * 12 + Vector3.right * 4);
            ally.TakeDamage(70, null); float health = ally.Health, enemyHealth = enemy.Health, red = battle.RedGold, gold = battle.BlueGold;
            Assert.That(battle.TryUseSummitPower(1, SummitAim(enemy.EyePosition)), Is.True);
            Assert.That(enemy.Health, Is.EqualTo(enemyHealth - 110 * (1 - enemy.Definition.ArmorFraction)).Within(.01));
            enemyHealth = enemy.Health;
            Assert.That(battle.TryUseSummitPower(1, SummitAim(enemy.EyePosition)), Is.False);
            Assert.That(battle.BlueGold, Is.EqualTo(gold - 30));
            Assert.That(battle.TryUseSummitPower(2, SummitAim(ally.EyePosition)), Is.True);
            Assert.That(ally.Health, Is.EqualTo(Mathf.Min(ally.MaxHealth, health + 60)));
            Assert.That(ally.Shield, Is.EqualTo(35)); Assert.That(enemy.Health, Is.EqualTo(enemyHealth)); Assert.That(enemy.Shield, Is.Zero);
            Assert.That(battle.BlueGold, Is.EqualTo(gold - 55)); Assert.That(battle.RedGold, Is.EqualTo(red));
            battle.ToggleMount(); battle.ToggleMount(); Assert.That(battle.Teleport(battle.Towers[2]), Is.True);
            Assert.That(battle.SummitPowerRemaining(1), Is.GreaterThan(5.8f));
            Assert.That(battle.SummitPowerRemaining(2), Is.GreaterThan(9.8f));
            director.RestartBattle(); FreezeEnemy();
            Assert.That(battle.BlueGold, Is.EqualTo(TowerBattle.StartingGold));
            for (int i = 0; i < 3; i++) Assert.That(battle.SummitPowerRemaining(i), Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator MissesCoverInvalidAimAndInsufficientGoldNeverSpendOrStartCooldowns()
        {
            StartSiege();
            var enemy = SummitTarget(BattleTeam.Red, Vector3.forward * 14);
            var ally = SummitTarget(BattleTeam.Blue, Vector3.forward * 14 + Vector3.right * 2);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform);
            wall.transform.position = (player.ViewCamera.transform.position + enemy.EyePosition) * .5f;
            wall.transform.localScale = new Vector3(8, 8, .6f); Physics.SyncTransforms();
            float gold = battle.BlueGold, health = enemy.Health;
            for (int i = 0; i < 3; i++)
            {
                Assert.That(battle.TryUseSummitPower(i, SummitAim(enemy.EyePosition)), Is.False, "Masonry must block both targeting and restoration fields.");
                Assert.That(battle.SummitPowerRemaining(i), Is.Zero);
            }
            Assert.That(ally.Shield, Is.Zero); Assert.That(enemy.Health, Is.EqualTo(health));
            wall.SetActive(false); Physics.SyncTransforms();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(battle.TryUseSummitPower(i, new Ray(player.ViewCamera.transform.position, Vector3.up)), Is.False);
                Assert.That(battle.TryUseSummitPower(i, new Ray(Vector3.one * float.NaN, Vector3.forward)), Is.False);
                Assert.That(battle.SummitPowerRemaining(i), Is.Zero);
            }
            Assert.That(battle.BlueGold, Is.EqualTo(gold));
            Assert.That(battle.TryUseSummitPower(1, SummitAim(ally.EyePosition)), Is.False, "Lightning cannot target an ally.");
            battle.TrySpend(BattleTeam.Blue, Mathf.FloorToInt(battle.BlueGold));
            Assert.That(battle.TryUseSummitPower(1, SummitAim(enemy.EyePosition)), Is.False);
            Assert.That(battle.TryUseSummitPower(2, SummitAim(ally.EyePosition)), Is.False);
            Assert.That(battle.TryUseSummitPower(0, SummitAim(enemy.EyePosition)), Is.True, "Repulse remains free.");
            yield return null;
        }

        [UnityTest] public IEnumerator SummitHudSelectsPowersAndDispatchesOneCastPerPress()
        {
            StartSiege(); hud.Refresh(); yield return null; hud.Refresh();
            Invoke(hud, "SelectCommandPage", 3); yield return null; Canvas.ForceUpdateCanvases();
            var panel = hud.Canvas.transform.Find("Live command HUD/Summit powers");
            var panelRect = (RectTransform)panel;
            var command = (RectTransform)hud.Canvas.transform.Find("Live command HUD/Command station");
            Assert.That(panelRect.anchorMin.x, Is.Zero, "Spell controls must anchor to the left edge.");
            Assert.That(panelRect.anchoredPosition.x, Is.EqualTo(command.anchoredPosition.x), "Spell controls must align with the left sidebar.");
            Assert.That(panelRect.rect.width, Is.EqualTo(650), "Spell cards use the wide command page.");
            var instructions = panel.Find("Summit instructions").GetComponent<Text>();
            Canvas.ForceUpdateCanvases();
            Assert.That(instructions.preferredHeight, Is.LessThanOrEqualTo(instructions.rectTransform.rect.height + 1));
            for (int i = 0; i < 3; i++)
            {
                var label = panel.Find("Summit power " + i).GetComponentInChildren<Text>();
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1));
            }
            var target = SummitTarget(BattleTeam.Red, Vector3.forward * 10);
            Assert.That(battle.CanUseSummitActions, Is.True);
            Assert.That(panel.Find("Summit power 1").GetComponent<Button>().interactable, Is.True);
            Invoke(hud, "CancelPointer");
            Click(panel.Find("Summit power 1"));
            Assert.That(battle.SelectedSummitPower, Is.EqualTo(1), "Clicked target: " + typeof(TowerBattleHud).GetField("hovered", Private).GetValue(hud));
            typeof(PossessionController).GetProperty("AimRay").SetValue(player, SummitAim(target.EyePosition));
            foreach (string field in new[] { "towerPrimaryHeld", "towerPowerPressConsumed" })
                typeof(PossessionController).GetField(field, Private).SetValue(player, false);
            float gold = battle.BlueGold, health = target.Health;
            Invoke(player, "HandleTowerPrimary"); Invoke(player, "HandleTowerPrimary");
            Assert.That(battle.BlueGold, Is.EqualTo(gold - 30));
            Assert.That(target.Health, Is.EqualTo(health - 110 * (1 - target.Definition.ArmorFraction)).Within(.01));
            hud.Refresh(); Assert.That(panel.Find("Summit power 1").GetComponent<Button>().interactable, Is.False);
            CaptureHud("Summit-powers-and-paid-cooldown");
            Click(panel.Find("Select bow")); Assert.That(battle.SelectedSummitPower, Is.Null);
            Invoke(player, "HandleTowerPrimary");
            Assert.That(root.GetComponentsInChildren<BattleProjectile>().Length, Is.Zero, "A held spell click cannot become a bow shot.");
            battle.ToggleMount(); hud.Refresh(); Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(hud.Canvas.transform.Find("Live command HUD/Command station/Travel").GetComponent<Button>().interactable, Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator MountedMovementStaysAboveTheTowerAndThrowStopsAtWalls()
        {
            StartSiege(); var tower = battle.BoundTower;
            Invoke(player, "MoveSpectator", Vector3.right * 100); yield return null;
            Assert.That(Vector3.ProjectOnPlane(player.ViewCamera.transform.position - tower.transform.position, Vector3.up).magnitude,
                Is.LessThanOrEqualTo(BattleTower.Radius)); Assert.That(battle.CanUseSummitActions, Is.True);
            battle.ReturnToTower();
            var target = SummitTarget(BattleTeam.Red, Vector3.forward * 8);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform);
            wall.transform.position = target.transform.position + Vector3.forward * 3 + Vector3.up;
            wall.transform.localScale = new Vector3(8, 8, .6f); Physics.SyncTransforms();
            float health = target.Health;
            Assert.That(battle.TryUseSummitPower(0, SummitAim(target.EyePosition)), Is.True);
            yield return Simulate(.7f);
            Assert.That(target.transform.position.z, Is.LessThan(wall.GetComponent<Collider>().bounds.min.z));
            Assert.That(target.Health, Is.EqualTo(health));
        }
    }
}
