using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        Button handTurnLeft, handTurnRight;
        RectTransform handAbilityPanel;
        readonly Button[] handAbilities = new Button[BattleClassCatalog.MaxAbilities];
        Button handBasicAttack, handFlare;
        Text handAbilityHeading, handAbilityHelp;
        Text handTrackingStatus;
        bool ShowHandWorldPointer => world && !BlocksGameplayInput && !ControllerOverUI && !PointerOverUI &&
            !player.WantsPhysicalBowInput && player.IsUsingOpticalHands;
        void CreateHandControls()
        {
            handTurnLeft = Button(commandPanel, "Hand turn left", "TURN LEFT", 16, 167, 182, 34, () => player.TurnFromHandMenu(-1));
            handTurnRight = Button(commandPanel, "Hand turn right", "TURN RIGHT", 210, 167, 182, 34, () => player.TurnFromHandMenu(1));
            handTurnLeft.gameObject.SetActive(false); handTurnRight.gameObject.SetActive(false);
            handAbilityPanel = Panel(live, "Hand character abilities", 395, 530, 650, 260);
            handAbilityHeading = Label(handAbilityPanel, "Character powers", "CHARACTER POWERS", 20, 16, 12, 618, 28, BattleUiStyle.Accent);
            handAbilityHelp = Label(handAbilityPanel, "Ability instructions", "Choose a power, then aim and pinch to use it.\nBasic attacks resume after a successful cast.", 17, 16, 42, 618, 42, Color.white);
            for (int i = 0; i < handAbilities.Length; i++)
            {
                int slot = i;
                handAbilities[i] = Button(handAbilityPanel, "Hand ability " + i, "", 16 + i % 2 * 318, 90 + i / 2 * 58, 300, 52, () =>
                {
                    if (!player.ChooseHandAbility(slot)) return;
                    if (world) CloseCommandMenu(); else Refresh();
                });
                handAbilities[i].GetComponentInChildren<Text>().fontSize = 18;
            }
            handBasicAttack = Button(handAbilityPanel, "Basic attack", "BASIC ATTACK / CANCEL", 16, 210, 300, 36, () => { player.ClearHandAbility(); Refresh(); });
            handFlare = Button(handAbilityPanel, "Hand flare", "SIGNAL FLARE", 334, 210, 300, 36, () => { player.TryUseFlare(); Refresh(); });
            handBasicAttack.GetComponentInChildren<Text>().fontSize = handFlare.GetComponentInChildren<Text>().fontSize = 16;
            handAbilityPanel.gameObject.SetActive(false);
            handTrackingStatus = Label((RectTransform)canvas.transform, "Hand tracking status", "", 15, 395, 850, 650, 48, Color.white);
        }
        void UpdateHandControls()
        {
            if (handTrackingStatus == null) return;
            RefreshEasyCommander();
            if (!HandMenuVisible && ShowHandWorldPointer && player.TryOpticalAim(out var ray, out bool pinching))
                ShowWorldHandPointer(ray, pinching);
            bool optical = player.IsUsingOpticalHands;
            bool turning = world && optical && battle.Director.IsBattleRunning && !charactersOpen && !guideOpen;
            bool showTurns = turning && player.CurrentUnit != null && vrHudPage == 0 && vrHudVisible;
            handTurnLeft.gameObject.SetActive(showTurns); handTurnRight.gameObject.SetActive(showTurns);
            handTurnLeft.transform.SetParent(easyCommanderPanel, false); handTurnRight.transform.SetParent(easyCommanderPanel, false);
            PlaceButton(handTurnLeft, 474, 228, 214, 64, 22); PlaceButton(handTurnRight, 700, 228, 214, 64, 22);
            if (showTurns) PlaceButton(easyWave, 18, 228, 440, 64, 22);
            var unit = player.CurrentUnit;
            bool abilities = turning && unit != null && unit.IsAlive && vrHudVisible && vrHudPage == 3;
            handAbilityPanel.gameObject.SetActive(abilities);
            if (abilities)
            {
                BattleUiStyle.Place(handAbilityPanel, 254, 236, 932, 450);
                BattleUiStyle.Place(handAbilityHeading.rectTransform, 18, 12, 896, 38); handAbilityHeading.fontSize = 27;
                BattleUiStyle.Place(handAbilityHelp.rectTransform, 18, 60, 896, 64); handAbilityHelp.fontSize = 22;
                PlaceButton(handBasicAttack, 18, 370, 440, 60, 22); PlaceButton(handFlare, 474, 370, 440, 60, 22);
                dossier.transform.parent.gameObject.SetActive(false);
                handAbilityHeading.text = unit.Definition.DisplayName.ToUpperInvariant() + " / POWERS";
                handAbilityHelp.text = unit.Definition.AbilityCount == 0
                    ? "This character uses basic attacks.\nSignal flare rallies your team once per match."
                    : "Choose a power, then aim and pinch to use it.\nBasic attacks resume after a successful cast.";
                for (int i = 0; i < handAbilities.Length; i++)
                {
                    bool available = i < unit.Definition.AbilityCount;
                    handAbilities[i].gameObject.SetActive(available);
                    if (!available) continue;
                    PlaceButton(handAbilities[i], 18 + i % 2 * 456, 136 + i / 2 * 112, 440, 96, 23);
                    float cooldown = unit.GetAbilityCooldownRemaining(i);
                    handAbilities[i].GetComponentInChildren<Text>().text = BattleClassCatalog.Ability(unit.Definition.Role, i).Name +
                        "\n" + (cooldown > 0 ? $"READY IN {cooldown:0.0}s" : "SELECT + AIM");
                    handAbilities[i].interactable = !player.IsSwitchingUnit && !player.IsHoldingCharacter && cooldown <= 0;
                }
                handFlare.interactable = !player.IsSwitchingUnit && battle.Director.Flares.HasCharge(BattleTeam.Blue);
                handFlare.GetComponentInChildren<Text>().text = handFlare.interactable ? "SIGNAL FLARE" : "FLARE USED";
            }
            int selectedAbility = player.SelectedHandAbility;
            bool visible = world && (!battle.Director.IsBattleRunning || vrHudVisible || charactersOpen || guideOpen || selectedAbility >= 0);
            handTrackingStatus.gameObject.SetActive(visible);
            BattleUiStyle.Place(handTrackingStatus.rectTransform, 254, 866, 932, 32); handTrackingStatus.fontSize = 18;
            if (!visible) return;
            bool left = player.TryOpticalHand(true, out _, out _, out _);
            bool right = player.TryOpticalHand(false, out _, out _, out _);
            if (left && right && selectedAbility < 0) { handTrackingStatus.gameObject.SetActive(false); return; }
            handTrackingStatus.text = right && left ? "HANDS / Right pinch: select. Left pinch + drag: move." :
                right ? "RIGHT HAND / Bring your left hand into view to move and use the bow." :
                left ? "LEFT HAND / Bring your right hand into view to point and pinch." :
                optical ? "HANDS PARTLY TRACKED / Open your hands and keep fingers in view." :
                "NO HANDS DETECTED / Enable headset hand tracking and keep hands in view.";
            handTrackingStatus.color = left || right ? new Color(.4f, 1, .65f) : Color.white;
            if (unit == null && battle.IsMounted && right)
                handTrackingStatus.text = "MENU button / open controls   CLOSE MENU / resume\n" +
                    (player.PhysicalHandBow ? "BOW / Pinch near bow, pull back, release to shoot" : "PINCH / select or shoot");
            if (selectedAbility >= 0 && right)
                handTrackingStatus.text = BattleClassCatalog.Ability(unit.Definition.Role, selectedAbility).Name.ToUpperInvariant() +
                    " / Aim, then pinch to cast.\nSelect MENU to reopen controls and cancel.";
        }
    }
}
