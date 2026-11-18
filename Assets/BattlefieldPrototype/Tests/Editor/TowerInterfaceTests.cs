using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator TouchPlusGuideFitsAndPreferencesDoNotChangeTheBattle()
        {
            StartSiege(); yield return null;
            var round = director.RoundId; float gold = battle.BlueGold;
            hud.OpenGuide(); yield return null;
            Click(Ui("How to play veil/How to play/Guide chapter 5")); yield return null;
            var body = Ui("How to play veil/How to play/Chapter text").GetComponent<Text>();
            Canvas.ForceUpdateCanvases();
            Assert.That(body.text, Does.Contain("WAVE GESTURE"));
            Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height + 1));
            Assert.That(player.ControllerGesturesEnabled, Is.True); Assert.That(player.ControllerHapticsEnabled, Is.True);
            Click(Ui("How to play veil/How to play/Controller gestures"));
            Click(Ui("How to play veil/How to play/Controller haptics"));
            Assert.That(player.ControllerGesturesEnabled, Is.False); Assert.That(player.ControllerHapticsEnabled, Is.False);
            Assert.That(director.RoundId, Is.EqualTo(round)); Assert.That(battle.BlueGold, Is.GreaterThanOrEqualTo(gold));
            Assert.That(hud.BlocksGameplayInput, Is.True);
            CaptureHud("TouchPlus-Controller-Guide");
            Click(Ui("How to play veil/How to play/Restart siege")); yield return null;
            Assert.That(director.RoundId, Is.GreaterThan(round)); Assert.That(hud.BlocksGameplayInput, Is.False);
            Assert.That(player.ControllerGesturesEnabled, Is.False); Assert.That(player.ControllerHapticsEnabled, Is.False);
        }
        [UnityTest] public IEnumerator InterfaceWorldSpaceGuideAndCharactersUseTheSharedRayPointerPath()
        {
            StartSiege(); Spawn(BattleTeam.Blue, UnitRole.Vanguard, FreePoint(battle.BoundTower));
            player.enabled = false;
            typeof(PossessionController).GetProperty("IsUsingXR").SetValue(player, true);
            typeof(PossessionController).GetField("desktopPointerActive", Private).SetValue(player, false);
            Invoke(hud, "Layout"); hud.OpenGuide(); yield return null;
            hud.Canvas.GetComponent<CanvasScaler>().SendMessage("Handle"); Canvas.ForceUpdateCanvases();
            Assert.That(hud.Canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            WorldUiClick(Ui("How to play veil/How to play/Guide chapter 1")); yield return null;
            Assert.That(Ui("How to play veil/How to play/Chapter heading").GetComponent<Text>().text, Does.Contain("Recruit"));
            WorldUiClick(Ui("How to play veil/How to play/Close guide")); yield return null;
            WorldUiClick(Ui("Live command HUD/HUD tools/Character library")); yield return null;
            Assert.That(hud.BlocksGameplayInput, Is.True);
            Assert.That(Ui("Live command HUD/Character browser").gameObject.activeInHierarchy, Is.True);
            WorldUiClick(Ui("Live command HUD/Character browser/Character favourite 0")); yield return null;
            Assert.That(hud.IsFavourite(UnitRole.Vanguard), Is.True);
            WorldUiClick(Ui("Live command HUD/Character browser/Control character 0"));
            Assert.That(hud.BlocksGameplayInput, Is.False);
            Invoke(player, "UpdateSwitch"); yield return new WaitForSecondsRealtime(.12f); Invoke(player, "UpdateSwitch");
            Assert.That(player.CurrentUnit, Is.Not.Null);
            Assert.That(player.CurrentUnit.Definition.Role, Is.EqualTo(UnitRole.Vanguard));
        }
        void WorldUiClick(Transform control)
        {
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)control;
            Vector3 point = rect.TransformPoint(rect.rect.center);
            Assert.That((bool)Invoke(hud, "DispatchWorld", point, true), Is.True, control.name);
            Invoke(hud, "DispatchWorld", point, false);
        }
        Transform Ui(string path) => hud.Canvas.transform.Find(path);
        [UnityTest] public IEnumerator InterfaceGuideIsAvailableBeforeStartingAndStartsSelectedMap()
        {
            hud.Refresh(); yield return null;
            Click(Ui("Home and outcome veil/Skyhold briefing/How to play")); yield return null;
            Assert.That(hud.BlocksGameplayInput, Is.True);
            Assert.That(director.Phase, Is.EqualTo(BattlePhase.Home));
            for (int i = 0; i < 5; i++)
            {
                Click(Ui("How to play veil/How to play/Guide chapter " + i)); yield return null;
                var text = Ui("How to play veil/How to play/Chapter text").GetComponent<Text>();
                Assert.That(text.text, Is.Not.Empty);
                Canvas.ForceUpdateCanvases(); Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1));
            }
            CaptureHud("UI-How-To-Play");
            Click(Ui("How to play veil/How to play/Continue")); yield return null;
            Assert.That(director.IsBattleRunning, Is.True); Assert.That(hud.BlocksGameplayInput, Is.False);
        }
        [UnityTest] public IEnumerator InterfaceFavouritesFilterRecruitmentWithoutPurchasingAndPersist()
        {
            StartSiege(); battle.ToggleMount(); hud.Refresh(); yield return null;
            Invoke(hud, "SelectCommandPage", 2); yield return null; Canvas.ForceUpdateCanvases();
            float gold = battle.BlueGold;
            Click(Ui("Live command HUD/Character browser/Character favourite 0")); yield return null;
            Assert.That(hud.IsFavourite(UnitRole.Vanguard), Is.True);
            Assert.That(battle.Recruitment.HasValue, Is.False);
            Assert.That(battle.BlueGold, Is.GreaterThanOrEqualTo(gold));
            Assert.That(PlayerPrefs.GetString(TowerBattleHud.FavouritesKey), Is.EqualTo("0"));
            Click(Ui("Live command HUD/Character browser/Character filter 1")); yield return null;
            Assert.That(Ui("Live command HUD/Character browser/Control character 0").gameObject.activeSelf, Is.True);
            Assert.That(Ui("Live command HUD/Character browser/Control character 1").gameObject.activeSelf, Is.False);
            Click(Ui("Live command HUD/Character browser/Control character 0")); yield return null;
            Assert.That(battle.Recruitment, Is.EqualTo(UnitRole.Vanguard));
            Invoke(hud, "SelectCommandPage", 2); yield return null; Canvas.ForceUpdateCanvases();
            Click(Ui("Live command HUD/Character browser/Character filter 1"));
            Click(Ui("Live command HUD/Character browser/Character favourite 0")); yield return null;
            Assert.That(Ui("Live command HUD/Character browser/Character status").GetComponent<Text>().text, Does.Contain("empty"));
            Assert.That(PlayerPrefs.GetString(TowerBattleHud.FavouritesKey), Is.Empty);
        }
        [UnityTest] public IEnumerator InterfaceCharacterBrowserSelectsHealthyAlliesAndRecordsRecentClasses()
        {
            StartSiege();
            var one = Spawn(BattleTeam.Blue, UnitRole.Vanguard, FreePoint(battle.BoundTower));
            var weak = Spawn(BattleTeam.Blue, UnitRole.Ranger, FreePoint(battle.BoundTower, UnitRole.Ranger));
            weak.TakeDamage(30, null);
            var two = Spawn(BattleTeam.Blue, UnitRole.Ranger, FreePoint(battle.BoundTower, UnitRole.Ranger) + Vector3.right * 3);
            hud.ToggleFavourite(UnitRole.Ranger); hud.Refresh(); yield return null;
            Click(Ui("Live command HUD/HUD tools/Character library")); yield return null;
            Assert.That(hud.BlocksGameplayInput, Is.True);
            Assert.That(Ui("Live command HUD/Summit powers").gameObject.activeSelf, Is.False);
            Assert.That(Ui("Live command HUD/Character browser/Control character 0").GetComponentInChildren<Text>().text, Does.Contain("Vanguard"));
            Click(Ui("Live command HUD/Character browser/Character filter 1")); yield return null;
            Assert.That(Ui("Live command HUD/Character browser/Control character 0").GetComponentInChildren<Text>().text, Does.Contain("Ranger"));
            CaptureHud("UI-Character-Favourites");
            Click(Ui("Live command HUD/Character browser/Control character 0")); yield return Simulate(.15f);
            Assert.That(player.CurrentUnit, Is.EqualTo(two)); Assert.That(hud.BlocksGameplayInput, Is.False);
            player.OpenMenuCursor(); hud.Refresh(); yield return null;
            Click(Ui("Live command HUD/HUD tools/Character library")); yield return null;
            Click(Ui("Live command HUD/Character browser/Character filter 3")); yield return null;
            Assert.That(Ui("Live command HUD/Character browser/Control character 0").GetComponentInChildren<Text>().text, Does.Contain("[YOU]"));
            var currentLabel = Ui("Live command HUD/Character browser/Control character 0").GetComponentInChildren<Text>();
            Assert.That(currentLabel.preferredHeight, Is.LessThanOrEqualTo(currentLabel.rectTransform.rect.height + 1));
            CaptureHud("Unified-current-character");
            Assert.That(hud.CanControlCharacter(one), Is.True);
        }
        [UnityTest] public IEnumerator InterfaceCharacterEligibilityHonoursOwnershipAndSummitTetherRules()
        {
            StartSiege(); var other = battle.Towers[1]; typeof(BattleTower).GetProperty("Owner").SetValue(other, (BattleTeam?)BattleTeam.Blue);
            var foreign = Spawn(BattleTeam.Blue, UnitRole.Ranger, FreePoint(other, UnitRole.Ranger), other);
            var local = Spawn(BattleTeam.Blue, UnitRole.Vanguard, FreePoint(battle.BoundTower));
            var enemy = Spawn(BattleTeam.Red, UnitRole.Ranger, FreePoint(battle.EnemyBoundTower, UnitRole.Ranger));
            Assert.That(hud.CanControlCharacter(foreign), Is.True);
            Assert.That(hud.CanControlCharacter(enemy), Is.False);
            battle.ToggleMount(); Assert.That(hud.CanControlCharacter(foreign), Is.True);
            Assert.That(hud.CanControlCharacter(local), Is.True);
            typeof(BattleTower).GetProperty("Owner").SetValue(other, (BattleTeam?)BattleTeam.Red); Assert.That(hud.CanControlCharacter(foreign), Is.False);
            typeof(BattleTower).GetProperty("Owner").SetValue(other, (BattleTeam?)BattleTeam.Blue);
            player.CycleUnit(); yield return Simulate(.15f);
            Assert.That(player.CurrentUnit, Is.EqualTo(local));
        }
        [UnityTest] public IEnumerator InterfaceUnifiedPagesKeepAlertsAndGuideAccessible()
        {
            StartSiege(); hud.Refresh(); yield return null;
            Assert.That(Ui("Live command HUD/Command advice/Movement hints").gameObject.activeSelf, Is.False);
            Click(Ui("Live command HUD/HUD tools/Toggle hints")); yield return null;
            Assert.That(PlayerPrefs.GetInt(TowerBattleHud.HintsKey), Is.EqualTo(1));
            Click(Ui("Live command HUD/HUD tools/Toggle hints")); yield return null;
            Invoke(hud, "SelectCommandPage", 0); yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            Assert.That(Ui("Live command HUD/Character dossier").gameObject.activeSelf, Is.False);
            Assert.That(Ui("Live command HUD/Command advice/Command feedback").gameObject.activeInHierarchy, Is.True);
            Assert.That(Ui("Live command HUD/VR HUD pages").gameObject.activeInHierarchy, Is.True);
            Assert.That(Ui("Live command HUD/HUD tools").Find("Toggle focus"), Is.Null);
            CaptureHud("UI-Focus-HUD");
            Click(Ui("Live command HUD/HUD tools/Open guide")); yield return null;
            Assert.That(hud.BlocksGameplayInput, Is.True);
            bool fired = false; player.ShotFired += () => fired = true;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                int round = director.RoundId;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R, Key.W, Key.Digit1)); InputSystem.Update();
                Invoke(player, "UpdatePlayerInput"); Invoke(player, "LateUpdate");
                Assert.That(fired, Is.False); Assert.That(director.RoundId, Is.EqualTo(round), "Guide must block restart input.");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
            CaptureHud("UI-In-Battle-Guide");
            Click(Ui("How to play veil/How to play/Close guide")); yield return null;
            Assert.That(hud.BlocksGameplayInput, Is.False);
            Invoke(hud, "SelectCommandPage", 0); yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            Invoke(hud, "SelectCommandPage", 1); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(Ui("Live command HUD/Tower network").gameObject.activeSelf, Is.True);
            Invoke(hud, "SelectCommandPage", 3); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(Ui("Live command HUD/Summit powers").gameObject.activeSelf, Is.True);
        }
    }
}
