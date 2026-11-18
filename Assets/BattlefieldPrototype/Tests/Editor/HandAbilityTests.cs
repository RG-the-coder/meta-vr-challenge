using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        const string HandPowersPath = "Live command HUD/Hand character abilities";

        IEnumerator ShowHandPowers()
        {
            if (!Ui("Live command HUD").gameObject.activeInHierarchy) Invoke(hud, "ToggleVrHud");
            if (player.CurrentUnit != null) Invoke(hud, "SelectCommandPage", 3);
            hud.Refresh(); Invoke(hud, "LateUpdate"); Canvas.ForceUpdateCanvases();
            yield return null;
            Invoke(hud, "LateUpdate"); Canvas.ForceUpdateCanvases();
        }

        [UnityTest] public IEnumerator HandsOnlyAbilityCardsCastAllFourSlotsOnceAndRequireAFreshPinch()
        {
            yield return CreateBasicUi(); StartSiege();
            var unit = Spawn(BattleTeam.Blue, UnitRole.Paladin, FreePoint(battle.BoundTower, UnitRole.Paladin));
            Assert.That(player.Possess(unit), Is.True); yield return Simulate(.2f); Invoke(player, "UpdateSwitch"); Invoke(player, "FinalizeView");
            var hands = UseOpticalHands();
            for (int slot = 0; slot < 4; slot++)
            {
                yield return ShowHandPowers();
                yield return PinchButton(hands, Ui(HandPowersPath + "/Hand ability " + slot));
                Assert.That(player.SelectedHandAbility, Is.EqualTo(slot));
                Assert.That(unit.GetAbilityCooldownRemaining(slot), Is.Zero, "Selecting a card must not cast at the menu.");
                var aim = player.ViewCamera.transform.position + player.ViewCamera.transform.forward * 8;
                PointHand(hands, aim, true); OpticalGameplayFrame();
                Assert.That(unit.GetAbilityCooldownRemaining(slot), Is.Zero, "The selection pinch must be released before a cast.");
                PointHand(hands, aim, false); OpticalGameplayFrame();
                PointHand(hands, aim, true); OpticalGameplayFrame();
                Assert.That(unit.GetAbilityCooldownRemaining(slot), Is.GreaterThan(0), "Hand input must dispatch slot " + slot);
                Assert.That(player.SelectedHandAbility, Is.EqualTo(-1), "A successful cast returns to basic attacks.");
                float cooldown = unit.GetAbilityCooldownRemaining(slot);
                for (int held = 0; held < 3; held++)
                {
                    Invoke(hud, "ProcessInput"); Invoke(player, "UpdatePlayerInput");
                    Assert.That((bool)typeof(PossessionController).GetField("attackRequested", Private).GetValue(player), Is.False);
                    Invoke(player, "LateUpdate");
                }
                Assert.That(unit.GetAbilityCooldownRemaining(slot), Is.EqualTo(cooldown).Within(.05f));
                PointHand(hands, aim, false); OpticalGameplayFrame();
            }
            yield return ShowHandPowers();
            Assert.That(Ui(HandPowersPath + "/Hand ability 3").GetComponent<Button>().interactable, Is.False);
            Assert.That(Ui(HandPowersPath + "/Hand ability 3").GetComponentInChildren<Text>().text, Does.Contain("READY IN"));
        }

        [UnityTest] public IEnumerator HandsOnlyAimedAbilityUsesTheWorldTargetAndDoesNotFireABasicAttack()
        {
            yield return CreateBasicUi(); StartSiege();
            var unit = Spawn(BattleTeam.Blue, UnitRole.Stormcaller, FreePoint(battle.BoundTower, UnitRole.Stormcaller));
            Assert.That(player.Possess(unit), Is.True); yield return Simulate(.2f); Invoke(player, "UpdateSwitch"); Invoke(player, "FinalizeView");
            var enemy = Spawn(BattleTeam.Red, UnitRole.Vanguard, unit.transform.position + Vector3.forward * 6);
            enemy.enabled = false; Physics.SyncTransforms();
            var hands = UseOpticalHands(); yield return ShowHandPowers();
            float health = enemy.Health;
            yield return PinchButton(hands, Ui(HandPowersPath + "/Hand ability 0"));
            Assert.That(enemy.Health, Is.EqualTo(health));
            int projectiles = root.GetComponentsInChildren<BattleProjectile>().Length;
            PointHand(hands, enemy.AimPosition, false); OpticalGameplayFrame();
            PointHand(hands, enemy.AimPosition, true); OpticalGameplayFrame();
            Assert.That(enemy.Health, Is.LessThan(health), "The chosen chain lightning must hit the hand-aimed enemy.");
            Assert.That(root.GetComponentsInChildren<BattleProjectile>().Length, Is.EqualTo(projectiles), "A spell pinch must not also fire a basic bolt.");
        }

        [UnityTest] public IEnumerator HandsOnlyAbilitySelectionHandlesTrackingLossMenusCancelAndPossessionChanges()
        {
            yield return CreateBasicUi(); StartSiege();
            var unit = Spawn(BattleTeam.Blue, UnitRole.Vanguard, FreePoint(battle.BoundTower));
            Assert.That(player.Possess(unit), Is.True); yield return Simulate(.2f); Invoke(player, "UpdateSwitch"); Invoke(player, "FinalizeView");
            var hands = UseOpticalHands(); yield return ShowHandPowers();
            yield return PinchButton(hands, Ui(HandPowersPath + "/Hand ability 1"));
            var aim = player.ViewCamera.transform.position + player.ViewCamera.transform.forward * 8;
            PointHand(hands, aim, false); OpticalGameplayFrame();
            hands.tracked[1] = false; OpticalGameplayFrame();
            hands.tracked[1] = true; PointHand(hands, aim, true); OpticalGameplayFrame();
            Assert.That(unit.GetAbilityCooldownRemaining(1), Is.Zero);
            PointHand(hands, aim, false); OpticalGameplayFrame();
            PointHand(hands, aim, true); OpticalGameplayFrame();
            Assert.That(unit.Shield, Is.GreaterThan(0));

            Assert.That(player.ChooseHandAbility(0), Is.True);
            yield return ShowHandPowers();
            yield return PinchButton(hands, Ui(HandPowersPath + "/Basic attack"));
            Assert.That(player.SelectedHandAbility, Is.EqualTo(-1));
            Assert.That(unit.GetAbilityCooldownRemaining(0), Is.Zero);
            Assert.That(player.ChooseHandAbility(0), Is.True);
            hud.OpenGuide(); OpticalGameplayFrame();
            Assert.That(player.SelectedHandAbility, Is.EqualTo(-1));
            Assert.That(unit.GetAbilityCooldownRemaining(0), Is.Zero);
            Invoke(hud, "CloseGuide");
            Assert.That(player.ChooseHandAbility(0), Is.True);
            battle.ReturnToTower();
            Assert.That(player.SelectedHandAbility, Is.EqualTo(-1));
            var other = Spawn(BattleTeam.Blue, UnitRole.Vanguard, FreePoint(battle.BoundTower));
            Assert.That(player.Possess(other), Is.True); yield return Simulate(.2f); Invoke(player, "UpdateSwitch"); Invoke(player, "FinalizeView");
            Assert.That(player.SelectedHandAbility, Is.EqualTo(-1));
            Assert.That(other.GetAbilityCooldownRemaining(0), Is.Zero);
        }

        [UnityTest] public IEnumerator HandsOnlyEveryClassExposesItsPowersInUnifiedMenu()
        {
            yield return CreateBasicUi(); StartSiege();
            foreach (var definition in battle.Definitions)
            {
                var unit = Spawn(BattleTeam.Blue, definition.Role, FreePoint(battle.BoundTower, definition.Role));
                Assert.That(player.Possess(unit), Is.True, definition.DisplayName); yield return Simulate(.2f); Invoke(player, "UpdateSwitch"); Invoke(player, "FinalizeView");
                UseOpticalHands();
                for (int modeIndex = 0; modeIndex < 1; modeIndex++)
                {
                    yield return ShowHandPowers();
                    Assert.That(Ui(HandPowersPath).gameObject.activeInHierarchy, Is.True);
                    for (int slot = 0; slot < BattleClassCatalog.MaxAbilities; slot++)
                    {
                        var card = Ui(HandPowersPath + "/Hand ability " + slot);
                        Assert.That(card.gameObject.activeSelf, Is.EqualTo(slot < definition.AbilityCount), definition.DisplayName);
                        if (slot >= definition.AbilityCount) continue;
                        var label = card.GetComponentInChildren<Text>();
                        Assert.That(label.text, Does.Contain(BattleClassCatalog.Ability(definition.Role, slot).Name));
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), label.text);
                    }
                    Assert.That(Ui(HandPowersPath).gameObject.activeInHierarchy, Is.True);
                    if (definition.Role == UnitRole.Paladin) CaptureHud(modeIndex == 0 ? "Hand-powers-simple" : "Hand-powers-advanced");

                }
                battle.ReturnToTower(); Object.Destroy(unit.gameObject); yield return null;
            }
        }

        [UnityTest] public IEnumerator HandsOnlyAdvancedMenuTurnsAndFlareUsesItsSingleCharge()
        {
            yield return CreateBasicUi(); StartSiege();
            var unit = Spawn(BattleTeam.Blue, UnitRole.Vanguard, FreePoint(battle.BoundTower));
            Assert.That(player.Possess(unit), Is.True); yield return Simulate(.2f); Invoke(player, "UpdateSwitch"); Invoke(player, "FinalizeView");
            var hands = UseOpticalHands();  yield return ShowHandPowers();
            yield return HandPage(hands, "COMMAND");
            float yaw = player.ViewCamera.transform.eulerAngles.y;
            yield return PinchButton(hands, Ui("Live command HUD/Easy commander/Hand turn right"));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, player.ViewCamera.transform.eulerAngles.y)), Is.GreaterThan(20));
            Invoke(hud, "FinalizeVrHudAnchor", true); yield return ShowHandPowers();
            Assert.That(director.Flares.HasCharge(BattleTeam.Blue), Is.True);
            yield return PinchButton(hands, Ui(HandPowersPath + "/Hand flare"));
            Assert.That(director.Flares.HasCharge(BattleTeam.Blue), Is.False);
            Assert.That(director.Flares.TryGetFlare(BattleTeam.Blue, out var flare), Is.True);
            Assert.That(Vector3.Distance(flare.Position, unit.transform.position), Is.LessThan(.1f));
            Invoke(hud, "LateUpdate");
            Assert.That(Ui(HandPowersPath + "/Hand flare").GetComponent<Button>().interactable, Is.False);
        }

        [UnityTest] public IEnumerator HandsOnlyGroundCommandCanLiftAndLowerUsingLeftPinchDrag()
        {
            yield return CreateBasicUi(); StartSiege(); battle.ToggleMount();
            var hands = UseOpticalHands(); Invoke(hud, "ToggleVrHud");
            var eye = player.ViewCamera.transform;
            PointHand(hands, eye.position + eye.forward * 8, false);
            Vector3 palm = eye.position - eye.right * .4f - eye.up * .35f;
            hands.Set(true, palm, palm + eye.forward * .07f, false); OpticalGameplayFrame();
            hands.Set(true, palm, palm + eye.forward * .07f, true); OpticalGameplayFrame();
            float initialY = eye.position.y;
            hands.Set(true, palm + Vector3.up * .16f, palm + Vector3.up * .16f + eye.forward * .07f, true);
            float until = Time.realtimeSinceStartup + .12f;
            do { OpticalGameplayFrame(); yield return null; } while (Time.realtimeSinceStartup < until);
            Assert.That(eye.position.y, Is.GreaterThan(initialY + .03f));
            hands.tracked[0] = false; OpticalGameplayFrame();
            float stoppedY = eye.position.y; yield return null; OpticalGameplayFrame();
            Assert.That(eye.position.y, Is.EqualTo(stoppedY).Within(.01f));
        }
    }
}
