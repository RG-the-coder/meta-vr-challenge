using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        RectTransform menuShell;
        Button playReturn, squadReturn;

        void CreateSimpleInterface()
        {
            menuShell = Panel(live, "Menu background", 230, 40, 980, 820);
            menuShell.GetComponent<Image>().color = new Color(.025f, .045f, .055f, .99f);
            menuShell.SetAsFirstSibling();
            playReturn = Button(commandPanel, "Return to tower", "RETURN TO TOWER", 18, 72, 896, 56,
                () => { battle.ReturnToTower(); Refresh(); });
            squadReturn = Button(characterBrowser, "Return to tower", "RETURN TO TOWER\nUse your bow again", 618, 272, 284, 112,
                () => { battle.ReturnToTower(); SelectCommandPage(0); });
        }

        static void PlaceButton(Button button, float x, float y, float w, float h, int size = 24)
        {
            BattleUiStyle.Place((RectTransform)button.transform, x, y, w, h);
            button.GetComponentInChildren<Text>().fontSize = size;
        }

        void ApplySimpleLayout()
        {
            if (menuShell == null) return;
            bool running = battle.Director.IsBattleRunning;
            bool visible = running && (!world || vrHudVisible || charactersOpen) && !guideOpen;
            live.gameObject.SetActive(visible); menuShell.gameObject.SetActive(visible);
            vrHudTabPanel.gameObject.SetActive(visible);
            if (!running) return;
            bool play = visible && !charactersOpen && vrHudPage == 0;
            bool mapPage = visible && !charactersOpen && vrHudPage == 1;
            bool powers = visible && !charactersOpen && vrHudPage == 3;
            bool construction = visible && !charactersOpen && vrHudPage == 4;
            BattleUiStyle.Place(vrHudTabPanel, 254, 150, 932, 68);
            int[] positions = { 0, 2, 1, 3, 4 };
            string[] labels = { "PLAY", "MAP", "SUMMON", "POWERS", "BUILD" };
            for (int i = 0; i < 5; i++)
            {
                vrHudTabs[i].gameObject.SetActive(true);
                PlaceButton(vrHudTabs[i], positions[i] * 188, 0, 180, 68);
                vrHudTabs[i].GetComponentInChildren<Text>().text = labels[i];
                SetSelected(vrHudTabs[i], i == (charactersOpen ? 2 : vrHudPage));
            }
            vrHudTabs[5].gameObject.SetActive(world);
            PlaceButton(vrHudTabs[5], 266, 628, 666, 62, 27);
            vrHudTabs[5].GetComponentInChildren<Text>().text = "CLOSE MENU  /  PLAY";
            vrHudTabs[5].image.color = new Color(.08f, .37f, .42f, 1);
            var economyPanel = (RectTransform)economy.transform.parent;
            BattleUiStyle.Place(economyPanel, 254, 60, 932, 78); economyPanel.gameObject.SetActive(visible);
            economyPanel.Find("Brand").GetComponent<Text>().text = "WARDENS AND RAIDERS  /  WAR";
            BattleUiStyle.Place(economy.rectTransform, 16, 35, 900, 36); economy.fontSize = 26;
            economy.text = $"ALLIES {battle.Director.AliveBlue}     ENEMIES {battle.Director.AliveRed}     GOLD {Mathf.FloorToInt(battle.BlueGold)}  (+{battle.Income(BattleTeam.Blue):0.0}/s)";

            var tools = (RectTransform)hintsToggle.transform.parent;
            BattleUiStyle.Place(tools, 254, 778, 250, 62); tools.gameObject.SetActive(visible);
            tools.Find("Character library").gameObject.SetActive(false); hintsToggle.gameObject.SetActive(false);
            PlaceButton(tools.Find("Open guide").GetComponent<Button>(), 0, 0, 250, 62);
            tools.Find("Open guide").GetComponentInChildren<Text>().text = "HELP";
            var advice = (RectTransform)notice.transform.parent;
            BattleUiStyle.Place(advice, 254, 696, 932, 64); advice.gameObject.SetActive(visible);
            BattleUiStyle.Place(notice.rectTransform, 16, 8, 900, 50); notice.fontSize = 21; notice.color = Color.white;
            foreach (string name in new[] { "Controls page", "Previous controls", "Next controls", "Movement hints" }) advice.Find(name).gameObject.SetActive(false);
            dossier.transform.parent.gameObject.SetActive(false); rosterPanel.gameObject.SetActive(false); target.gameObject.SetActive(false);
            bool returnNeeded = !battle.IsMounted || player.CurrentUnit != null;
            bool lockedPowers = powers && !battle.CanUseSummitActions && player.CurrentUnit == null;
            commandPanel.gameObject.SetActive(play || mapPage || lockedPowers);
            BattleUiStyle.Place(commandPanel, mapPage ? 700 : 254, mapPage ? 620 : 236, mapPage ? 486 : 932, mapPage ? 70 : 124);
            mode.gameObject.SetActive(!mapPage); towerStats.gameObject.SetActive(!mapPage);
            BattleUiStyle.Place(mode.rectTransform, 18, 12, 896, 34); mode.fontSize = 27;
            BattleUiStyle.Place(towerStats.rectTransform, 18, 52, 896, 62); towerStats.fontSize = 22;
            commandPanel.Find("Risk").gameObject.SetActive(false); possess.gameObject.SetActive(false); groundWorks.gameObject.SetActive(false);
            mount.gameObject.SetActive(mapPage); travel.gameObject.SetActive(mapPage); build.gameObject.SetActive(mapPage);
            PlaceButton(mount, 8, 8, 150, 54, 20); PlaceButton(travel, 168, 8, 150, 54, 20); PlaceButton(build, 328, 8, 150, 54, 20);
            mount.GetComponentInChildren<Text>().text = battle.IsMounted ? "GO TO GROUND" : "RETURN TO TOWER";
            travel.GetComponentInChildren<Text>().text = "USE THIS TOWER";
            build.GetComponentInChildren<Text>().text = battle.SelectedTower == null ? "SELECT A TOWER" :
                (battle.SelectedTower.IsBroken ? "REBUILD\n" : "RAISE TOWER\n") + battle.SelectedTower.BuildCost + " GOLD";
            playReturn.gameObject.SetActive(!mapPage && (returnNeeded || lockedPowers));
            if (playReturn.gameObject.activeSelf)
            {
                towerStats.gameObject.SetActive(false);
                PlaceButton(playReturn, 18, 60, 896, 56);
                mode.text = lockedPowers ? "POWERS ARE AVAILABLE AT YOUR TOWER" : player.CurrentUnit != null ? "PLAYING AS " + player.CurrentUnit.Definition.DisplayName.ToUpperInvariant() : "GROUND COMMAND";
            }
            else if (play)
            { mode.text = "DEFEND YOUR TOWER"; towerStats.text = "Your allies fight automatically.\nDestroy or capture the enemy's bound tower."; }

            LayoutSimpleSquad(visible && charactersOpen);
            LayoutSimpleMap(mapPage);
            LayoutSimplePowers(powers && battle.CanUseSummitActions);
            LayoutSimpleBuild(construction);
            if (mapPage) notice.text = battle.IsPlanningRoute ? "Select points on the map, then SEND ROUTE. UNDO removes the last point." : "Select a tower to inspect it. Route planning is optional.";
            if (powers) notice.text = lockedPowers ? "Return to your tower to use your bow and powers." : "Choose a power to enter aiming. Point, then pinch to cast. MENU brings you back.";
            if (construction) notice.text = "Choose a defense to open the placement map. Pinch a clear gold area to build.";
            if (charactersOpen) notice.text = "Pinch any SUMMON card again to buy another ally. No soldier limit.";
            if (mapPage && battle.SelectedDefense.HasValue) LayoutHandBuildMap();
        }

        void LayoutSimpleSquad(bool visible)
        {
            characterBrowser.gameObject.SetActive(visible);
            BattleUiStyle.Place(characterBrowser, 254, 236, 932, 450);
            var heading = characterBrowser.Find("Title").GetComponent<Text>(); heading.text = "SUMMON ALLIES"; heading.fontSize = 27;
            BattleUiStyle.Place(heading.rectTransform, 18, 12, 896, 38);
            characterBrowser.Find("Close characters").gameObject.SetActive(false);
            BattleUiStyle.Place(characterStatus.rectTransform, 18, 58, 896, 64); characterStatus.fontSize = 22;
            characterStatus.text = $"Gold collects automatically: +{battle.Income(BattleTeam.Blue):0.0}/second.\nRepeat any card. No limit on living soldiers; each summon costs gold.";
            for (int i = 0; i < TowerBattle.SquadSize; i++) PlaceButton(characterCards[i], 18 + i % 3 * 300, 140 + i / 3 * 132, 284, 112);
            PlaceButton(squadReturn, 618, 272, 284, 112); squadReturn.gameObject.SetActive(visible);
            var footer = characterBrowser.Find("Browser footer").GetComponent<Text>();
            BattleUiStyle.Place(footer.rectTransform, 18, 406, 896, 36); footer.fontSize = 21;
            footer.text = "Allies fight automatically. PLAY gives ADVANCE, HOLD and GUARD orders.";
        }

        void LayoutSimpleMap(bool visible)
        {
            var mapPanel = (RectTransform)map.transform.parent; var towers = (RectTransform)towerButtons[0].transform.parent;
            mapPanel.gameObject.SetActive(visible); towers.gameObject.SetActive(visible); troopOrderPanel.gameObject.SetActive(visible);
            BattleUiStyle.Place(mapPanel, 254, 236, 430, 370); BattleUiStyle.Place(towers, 700, 236, 486, 370);
            BattleUiStyle.Place(mapTitle.rectTransform, 16, 10, 398, 32); mapTitle.fontSize = 23;
            BattleUiStyle.Place(map.rectTransform, 16, 50, 398, 260);
            BattleUiStyle.Place(mapLegend.rectTransform, 16, 320, 398, 46); mapLegend.fontSize = 18; mapLegend.text = "Gold: your tower. Blue: allies. Red: enemies.";
            BattleUiStyle.Place(towerNetworkTitle.rectTransform, 16, 10, 300, 32); towerNetworkTitle.fontSize = 23;
            PlaceButton(previousTowers, 336, 6, 62, 38); PlaceButton(nextTowers, 410, 6, 62, 38);
            for (int i = 0; i < towerButtons.Length; i++) PlaceButton(towerButtons[i], 16, 50 + 44 * i, 454, 40, 20);
            BattleUiStyle.Place(troopOrderPanel, 254, 620, 430, 70);
            troopOrderTitle.gameObject.SetActive(false); troopOrderHelp.gameObject.SetActive(false);
            advanceOrder.gameObject.SetActive(false); holdOrder.gameObject.SetActive(false); guardOrder.gameObject.SetActive(false);
            Button[] routes = { planRoute, sendRoute, undoRoute, cancelRoute };
            string[] labels = { battle.IsPlanningRoute ? "STOP\nPLANNING" : "PLAN\nROUTE", "SEND\nROUTE", "UNDO\nPOINT", "CANCEL\nROUTE" };
            for (int i = 0; i < routes.Length; i++) { PlaceButton(routes[i], 8 + i * 105, 8, 99, 54, 18); routes[i].GetComponentInChildren<Text>().text = labels[i]; }
        }

        void LayoutSimplePowers(bool visible)
        {
            summitPanel.gameObject.SetActive(visible); BattleUiStyle.Place(summitPanel, 254, 236, 932, 450);
            var heading = summitPanel.Find("Summit heading").GetComponent<Text>(); heading.text = "TOWER POWERS"; heading.fontSize = 27;
            BattleUiStyle.Place(heading.rectTransform, 18, 12, 560, 38);
            var bowButton = summitPanel.Find("Select bow").GetComponent<Button>(); PlaceButton(bowButton, 682, 10, 232, 44, 21);
            bowButton.GetComponentInChildren<Text>().text = "USE BOW / CANCEL";
            for (int i = 0; i < summitPowers.Length; i++)
            {
                PlaceButton(summitPowers[i], 18 + i * 300, 76, 284, 100);
                float remaining = battle.SummitPowerRemaining(i); int cost = TowerBattle.SummitPowerCost(i);
                summitPowers[i].GetComponentInChildren<Text>().text = TowerBattle.SummitPowerName(i).ToUpperInvariant() + "\n" +
                    (remaining > 0 ? $"Ready in {remaining:0}s" : cost == 0 ? "Free" : cost + " gold");
            }
            BattleUiStyle.Place(summitInfo.rectTransform, 18, 194, 896, 64); summitInfo.fontSize = 21;
            summitInfo.text = "Repulse pushes. Lightning damages. Restoration heals.\nSelect one to aim. Release your pinch, then pinch again to cast.";
            PlaceButton(towerPulse, 18, 278, 440, 78, 22); PlaceButton(pulseUpgrade, 474, 278, 440, 78, 21);
            towerPulse.GetComponentInChildren<Text>().text = battle.BoundTower.PulseRemaining > 0 ? $"PUSH ENEMIES BACK\nReady in {battle.BoundTower.PulseRemaining:0}s" : "PUSH ENEMIES BACK\nFree / immediate";
            PlaceButton(summitPanel.Find("Bow gesture").GetComponent<Button>(), 18, 376, 896, 56, 22);
        }

        void LayoutSimpleBuild(bool visible)
        {
            defensePanel.gameObject.SetActive(visible); BattleUiStyle.Place(defensePanel, 254, 236, 932, 450);
            defenseHeading.text = "BUILD A DEFENSE"; defenseHeading.fontSize = 27;
            BattleUiStyle.Place(defenseHeading.rectTransform, 18, 12, 896, 38); defenseChange.gameObject.SetActive(false);
            for (int i = 0; i < defenseChoices.Length; i++) { defenseChoices[i].gameObject.SetActive(true); PlaceButton(defenseChoices[i], 18 + i % 3 * 300, 68 + i / 3 * 112, 284, 96, 22); }
            PlaceButton(defenseRotate, 18, 306, 440, 60); PlaceButton(defenseCancel, 474, 306, 440, 60);
            defenseRotate.GetComponentInChildren<Text>().text = "ROTATE DEFENSE";
            BattleUiStyle.Place(defenseInfo.rectTransform, 18, 384, 896, 58); defenseInfo.fontSize = 20;
            defenseWorld.gameObject.SetActive(visible && world && battle.SelectedDefense.HasValue);
            PlaceButton(defenseWorld, 18, 376, 896, 56, 22);
            defenseInfo.gameObject.SetActive(!defenseWorld.gameObject.activeSelf);
            defenseWorld.GetComponentInChildren<Text>().text = "RESUME WORLD PLACEMENT";
            defenseInfo.text = "Choose a defense, then point at the battlefield ground and pinch.\nMENU opens rotate/cancel controls. Stay at your tower.";
        }

        void LayoutHandBuildMap()
        {
            var mapPanel = (RectTransform)map.transform.parent;
            towerButtons[0].transform.parent.gameObject.SetActive(false);
            troopOrderPanel.gameObject.SetActive(false); commandPanel.gameObject.SetActive(false);
            BattleUiStyle.Place(mapPanel, 254, 236, 608, 450);
            mapTitle.text = "PINCH THE MAP TO BUILD";
            BattleUiStyle.Place(mapTitle.rectTransform, 16, 10, 576, 36);
            BattleUiStyle.Place(map.rectTransform, 16, 54, 576, 330);
            BattleUiStyle.Place(mapLegend.rectTransform, 16, 396, 576, 46);
            mapLegend.text = "Gold: building zone. Avoid towers, troops and walls.";
            defensePanel.gameObject.SetActive(true); BattleUiStyle.Place(defensePanel, 878, 236, 308, 450);
            defenseHeading.text = "PLACE DEFENSE"; defenseHeading.fontSize = 24;
            BattleUiStyle.Place(defenseHeading.rectTransform, 16, 12, 276, 36);
            foreach (var choice in defenseChoices) choice.gameObject.SetActive(false);
            defenseChange.gameObject.SetActive(true);
            defenseWorld.gameObject.SetActive(true); PlaceButton(defenseWorld, 16, 178, 276, 56, 22);
            PlaceButton(defenseRotate, 16, 240, 276, 56, 22);
            PlaceButton(defenseChange, 16, 302, 276, 56, 22);
            PlaceButton(defenseCancel, 16, 364, 276, 56, 22);
            defenseRotate.GetComponentInChildren<Text>().text = "ROTATE / " + battle.DefenseRotation.ToString("0") + " DEGREES";
            defenseInfo.text = DefenseCatalog.Name(battle.SelectedDefense.Value) + "\n" + DefenseCatalog.Cost(battle.SelectedDefense.Value) + " gold each\nMap or world: point + pinch.";
            defenseInfo.fontSize = 21; BattleUiStyle.Place(defenseInfo.rectTransform, 16, 62, 276, 106);
            notice.text = battle.Message;
        }
    }
}
