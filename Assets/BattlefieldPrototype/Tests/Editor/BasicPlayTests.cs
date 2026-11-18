using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        IEnumerator CreateBasicUi()
        {
            root.SetActive(false); Object.Destroy(root);
            PlayerPrefs.DeleteKey(TowerBattleHud.FocusKey);
            root = new GameObject("Default basic play regression");
            terrain = root.AddComponent<BattlefieldBootstrap>(); terrain.RandomizeBattlefield = false;
            terrain.PrimitiveMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/BattlefieldPrototype/Materials/Primitive.mat");
            yield return null;
            battle = root.GetComponent<TowerBattle>(); director = root.GetComponent<BattleDirector>();
            player = root.GetComponent<PossessionController>(); hud = root.GetComponent<TowerBattleHud>();
        }

        [UnityTest] public IEnumerator BasicsStartsWithFiveSummonCardsAndKeepsCasualtiesVisible()
        {
            yield return CreateBasicUi();
            Click(Ui("Home and outcome veil/Skyhold briefing/Begin siege")); yield return null; FreezeEnemy(); hud.Refresh();
            Assert.That(director.IsBattleRunning, Is.True); Assert.That(hud.BlocksGameplayInput, Is.False);
            Assert.That(director.AliveBlue, Is.Zero); Assert.That(director.AliveRed, Is.Zero);
            Invoke(hud, "ToggleCharacters"); yield return null;
            int count = 0;
            foreach (var button in Ui("Live command HUD/Character browser").GetComponentsInChildren<Button>())
                if (button.name.StartsWith("Control character")) { count++; Assert.That(button.GetComponentInChildren<Text>().text, Does.Contain("SUMMON")); }
            Assert.That(count, Is.EqualTo(5));
            Assert.That(battle.TrySummonSlot(0, out _), Is.True);
            battle.SquadMember(BattleTeam.Blue, 0).TakeDamage(100000, null); hud.Refresh();
            var card = Ui("Live command HUD/Character browser/Control character 0").GetComponent<Button>();
            Assert.That(card.GetComponentInChildren<Text>().text, Does.Contain("DEFEATED")); Assert.That(card.interactable, Is.False);
            Assert.That(Ui("Live command HUD/Character browser/Spawn another 0").gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest] public IEnumerator BasicsVrSquadControlsExistingAllyAndReturnsToBow()
        {
            yield return CreateBasicUi(); StartSiege(); UseTestHeadset();
            Invoke(hud, "ToggleCharacters"); yield return null;
            WorldUiClick(Ui("Live command HUD/Character browser/Control character 2"));
            yield return new WaitForSecondsRealtime(.2f); Invoke(player, "UpdateSwitch");
            Assert.That(player.CurrentUnit, Is.SameAs(battle.SquadMember(BattleTeam.Blue, 2)));
            Assert.That(battle.Recruitment, Is.Null); Assert.That(director.AliveBlue, Is.EqualTo(5));
            battle.ReturnToTower(); Assert.That(player.CurrentUnit, Is.Null); Assert.That(battle.IsMounted, Is.True);
        }

        [UnityTest] public IEnumerator BasicsCatalogCanControlAnotherOwnedTowersAllyWithoutManualTravel()
        {
            yield return CreateBasicUi(); StartSiege(); battle.ToggleMount();
            var other = battle.Towers[1]; typeof(BattleTower).GetProperty("Owner").SetValue(other, (BattleTeam?)BattleTeam.Blue);
            var ally = battle.SquadMember(BattleTeam.Blue, 1);
            typeof(BattleUnit).GetProperty("SpawnTower").SetValue(ally, other);
            Invoke(hud, "ToggleCharacters"); yield return null;
            Click(Ui("Live command HUD/Character browser/Control character 1")); yield return Simulate(.2f);
            Assert.That(player.CurrentUnit, Is.EqualTo(ally)); Assert.That(battle.BoundTower, Is.EqualTo(other));
        }

        [UnityTest] public IEnumerator UnifiedPagesExposeAllCommandsWithoutChangingEconomyOrEquipment()
        {
            yield return CreateBasicUi(); StartSiege(); var hands = UseOpticalHands();
            Assert.That(Ui("Live command HUD/HUD tools").Find("Toggle focus"), Is.Null);
            yield return HandPage(hands, "POWERS");
            Assert.That(Ui("Live command HUD/Summit powers").gameObject.activeInHierarchy, Is.True);
            yield return HandPage(hands, "BUILD");
            Assert.That(battle.IsMounted, Is.True, "Browsing BUILD must not move the player.");
            Assert.That(Ui("Live command HUD/Ground works").gameObject.activeInHierarchy, Is.True);
            yield return HandPage(hands, "RECRUIT");
            Assert.That(Ui("Live command HUD/Character browser").gameObject.activeInHierarchy, Is.True);
            yield return HandPage(hands, "MAP + TOWERS");
            Assert.That(Ui("Live command HUD/Tower network").gameObject.activeInHierarchy, Is.True);
            Assert.That(battle.BlueGold, Is.GreaterThanOrEqualTo(TowerBattle.StartingGold));
            CaptureHud("Unified-map");
        }
    }
}
