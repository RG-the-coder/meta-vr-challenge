using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    /// <summary>Character library, persistent favourites and optional guidance share both HUD input paths.</summary>
    public sealed partial class TowerBattleHud
    {
        public const string FavouritesKey = "Skyhold.UI.Favourites";
        public const string HintsKey = "Skyhold.UI.Hints";
        public const string FocusKey = "Skyhold.UI.EasyHands";
        readonly HashSet<UnitRole> favourites = new HashSet<UnitRole>();
        readonly List<UnitRole> recentCharacters = new List<UnitRole>();
        readonly List<BattleUnitDefinition> rosterEntries = new List<BattleUnitDefinition>();
        readonly List<BattleUnitDefinition> characterEntries = new List<BattleUnitDefinition>();
        readonly Button[] recruitFavourites = new Button[8], characterCards = new Button[8], characterFavourites = new Button[8];
        readonly Button[] characterSpawns = new Button[8];
        readonly Button[] rosterFilters = new Button[4], characterFilters = new Button[4], guideTabs = new Button[6];
        RectTransform characterBrowser, guide;
        Button hintsToggle, homeGuide, guideContinue, gestureToggle, hapticsToggle, controllerRestart, audioToggle;
        bool previousControllerMenu, previousControllerLibrary;
        Text characterStatus, characterPageText, rosterEmpty, guideHeading, guideBody, guideFooter;
        bool charactersOpen, guideOpen, showHints;
        int rosterFilter, characterFilter, characterPage, guidePage, guideSection;
        public bool BlocksGameplayInput => charactersOpen || guideOpen;
        public bool IsFavourite(UnitRole role) => favourites.Contains(role);

        public void ToggleFavourite(UnitRole role)
        {
            if (!favourites.Add(role)) favourites.Remove(role);
            var saved = new List<int>(); foreach (var item in favourites) saved.Add((int)item);
            saved.Sort(); PlayerPrefs.SetString(FavouritesKey, string.Join(",", saved)); PlayerPrefs.Save();
            Refresh();
        }
        void CreateInterfaceTools(RectTransform homeCard)
        {
            foreach (string value in PlayerPrefs.GetString(FavouritesKey, "").Split(','))
                if (int.TryParse(value, out int role) && System.Enum.IsDefined(typeof(UnitRole), role)) favourites.Add((UnitRole)role);
            showHints = PlayerPrefs.GetInt(HintsKey, 0) != 0;

            player.PossessionChanged += RememberCharacter;
            var tools = Panel(live, "HUD tools", 798, 16, 300, 82);
            SmallButton(tools, "Character library", "CHARACTERS [P]", 10, 9, 136, 28, ToggleCharacters);
            SmallButton(tools, "Open guide", "HOW TO PLAY [F1]", 154, 9, 136, 28, OpenGuide);
            hintsToggle = SmallButton(tools, "Toggle hints", "", 10, 45, 136, 28, ToggleHints);
            homeGuide = Button(homeCard, "How to play", "HOW TO PLAY", 510, 537, 450, 54, OpenGuide);
            var heading = rosterPanel.Find("Recruitment heading").GetComponent<Text>();
            heading.text = "RECRUIT / CHOOSE, THEN PLACE"; heading.fontSize = 13; heading.rectTransform.sizeDelta = new Vector2(390, 26);
            string[] filters = { "ALL", "FAV", "AIR", "RECENT" };
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                rosterFilters[i] = SmallButton(rosterPanel, "Roster filter " + i, filters[i], 414 + i * 84, 8, 78, 30,
                    () => { rosterFilter = slot; page = 0; Refresh(); });
            }
            for (int i = 0; i < 8; i++)
            {
                int slot = i;
                recruitFavourites[i] = SmallButton(rosterPanel, "Recruit favourite " + i, "+ FAV", 218 + i % 4 * 262, 48 + i / 4 * 60, 48, 51,
                    () => { int index = page * 8 + slot; if (index < rosterEntries.Count) ToggleFavourite(rosterEntries[index].Role); });
            }
            rosterEmpty = Label(rosterPanel, "Empty roster", "", 18, 24, 70, 1000, 80, BattleUiStyle.Muted);
            characterBrowser = Panel(live, "Character browser", 448, 112, 650, 562);
            Label(characterBrowser, "Title", "CHARACTERS", 24, 16, 12, 440, 36, Color.white);
            SmallButton(characterBrowser, "Close characters", "CLOSE / ESC", 498, 14, 136, 32, CloseCharacters);
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                characterFilters[i] = SmallButton(characterBrowser, "Character filter " + i, filters[i], 16 + i * 156, 60, 146, 32,
                    () => { characterFilter = slot; characterPage = 0; Refresh(); });
            }
            characterStatus = Label(characterBrowser, "Character status", "", 15, 16, 104, 618, 62, BattleUiStyle.Muted);
            for (int i = 0; i < 8; i++)
            {
                int slot = i; float x = 16 + i % 2 * 314, y = 178 + i / 2 * 72;
                characterCards[i] = SmallButton(characterBrowser, "Control character " + i, "", x, y, 248, 62, () => ControlCharacter(slot));
                characterCards[i].GetComponentInChildren<Text>().fontSize = 15;
                characterFavourites[i] = SmallButton(characterBrowser, "Character favourite " + i, "", x + 254, y, 48, 62,
                    () => { int index = characterPage * 8 + slot; if (index < characterEntries.Count) ToggleFavourite(characterEntries[index].Role); });
                characterSpawns[i] = SmallButton(characterBrowser, "Spawn another " + i, "", x + 204, y, 98, 70,
                    () => { int index = characterPage * 8 + slot; if (index < characterEntries.Count) BeginRecruitment(characterEntries[index].Role); });
            }
            SmallButton(characterBrowser, "Previous characters", "<", 16, 484, 48, 34, () => TurnCharacterPage(-1));
            characterPageText = Label(characterBrowser, "Character page", "", 15, 76, 490, 186, 28, BattleUiStyle.Muted);
            SmallButton(characterBrowser, "Next characters", ">", 266, 484, 48, 34, () => TurnCharacterPage(1));
            SmallButton(characterBrowser, "Next favourite", "NEXT FAVOURITE", 330, 484, 304, 34, NextFavourite);
            Label(characterBrowser, "Browser footer", "SPAWN: choose, then point at a green circle. CONTROL: play a living ally.", 14, 16, 524, 618, 36, BattleUiStyle.Muted);
            var veil = BattleUiStyle.Box(canvas.transform, "How to play veil", new Color(.012f, .025f, .035f, .97f), overlay);
            guide = veil.rectTransform; Stretch(guide); veil.raycastTarget = true;
            var card = Panel(guide, "How to play", 0, 0, 1000, 630);
            card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f; card.anchoredPosition = Vector2.zero;
            Label(card, "Title", "HOW TO PLAY", 38, 40, 28, 720, 54, Color.white);
            SmallButton(card, "Close guide", "CLOSE / ESC", 804, 36, 156, 34, CloseGuide);
            string[] chapters = { "START", "SUMMON", "BOW", "BUILD", "MAP", "SETTINGS" };
            for (int i = 0; i < guideTabs.Length; i++)
            {
                int slot = i;
                guideTabs[i] = SmallButton(card, "Guide chapter " + i, chapters[i], 40 + i * 155, 104, 145, 40, () => { guidePage = slot; guideSection = 0; Refresh(); });
            }
            guideHeading = Label(card, "Chapter heading", "", 27, 40, 176, 920, 42, BattleUiStyle.Accent);
            guideBody = Label(card, "Chapter text", "", 21, 40, 236, 920, 246, Color.white);
            guideFooter = Label(card, "Guide footer", "", 15, 40, 490, 920, 30, BattleUiStyle.Muted);
            SmallButton(card, "Previous chapter", "< PREVIOUS", 40, 546, 194, 48, () => TurnGuide(-1));
            SmallButton(card, "Next chapter", "NEXT >", 246, 546, 194, 48, () => TurnGuide(1));
            guideContinue = Button(card, "Continue", "", 510, 546, 450, 48, () => { CloseGuide(); if (battle.Director.Phase == BattlePhase.Home) battle.NewGame(); });
            gestureToggle = SmallButton(card, "Controller gestures", "", 40, 488, 290, 32, () => { player.ToggleControllerGestures(); Refresh(); });
            hapticsToggle = SmallButton(card, "Controller haptics", "", 346, 488, 290, 32, () => { player.ToggleControllerHaptics(); Refresh(); });
            controllerRestart = SmallButton(card, "Restart siege", "RESTART SIEGE", 652, 488, 308, 32,
                () => { CloseGuide(); battle.NewGame(); });
            audioToggle = SmallButton(card, "Sound", "", 508, 488, 218, 32,
                () => { battle.Director.GetComponent<BattleAudio>()?.ToggleMute(); Refresh(); });
            BuildRosterEntries();
        }
        void TurnGuide(int direction)
        {
            guidePage = (guidePage + direction + guideTabs.Length) % guideTabs.Length; guideSection = 0; Refresh();
        }
        void LayoutGuide()
        {
            if (guide == null) return;
            guide.GetComponent<Image>().color = new Color(.012f, .025f, .035f, world ? 1 : .97f);
            var card = (RectTransform)guide.Find("How to play");
            card.sizeDelta = new Vector2(1000, world ? 760 : 630);
            var title = card.Find("Title").GetComponent<Text>();
            BattleUiStyle.Place(title.rectTransform, 40, 28, 720, 54); title.fontSize = world ? 44 : 38;
            PlaceGuideButton(card, "Close guide", world ? 780 : 804, world ? 28 : 36, world ? 180 : 156, world ? 52 : 34);
            card.Find("Close guide").GetComponentInChildren<Text>().text = world ? "CLOSE" : "CLOSE / ESC";
            for (int i = 0; i < guideTabs.Length; i++)
                PlaceGuideButton(card, "Guide chapter " + i, 40 + (world ? i % 3 * 312 : i * 155),
                    world ? 104 + i / 3 * 64 : 104, world ? 296 : 145, world ? 52 : 40);
            BattleUiStyle.Place(guideHeading.rectTransform, 40, world ? 244 : 176, 920, world ? 48 : 42);
            guideHeading.fontSize = world ? 32 : 27;
            BattleUiStyle.Place(guideBody.rectTransform, 40, world ? 310 : 236, 920, world ? 286 : 246);
            guideBody.lineSpacing = world ? 1.1f : 1;
            BattleUiStyle.Place(guideFooter.rectTransform, 40, world ? 604 : 490, 920, 32);
            guideFooter.fontSize = world ? 23 : 15;
            guideFooter.color = world ? Color.white : BattleUiStyle.Muted;
            PlaceGuideButton(card, "Previous chapter", 40, world ? 680 : 546, 194, world ? 52 : 48);
            PlaceGuideButton(card, "Next chapter", 246, world ? 680 : 546, 194, world ? 52 : 48);
            PlaceGuideButton(card, "Continue", 510, world ? 680 : 546, 450, world ? 52 : 48);
            PlaceGuideButton(card, "Controller gestures", 40, world ? 606 : 488, 218, world ? 52 : 32);
            PlaceGuideButton(card, "Controller haptics", 274, world ? 606 : 488, 218, world ? 52 : 32);
            PlaceGuideButton(card, "Sound", 508, world ? 606 : 488, 218, world ? 52 : 32);
            PlaceGuideButton(card, "Restart siege", 742, world ? 606 : 488, 218, world ? 52 : 32);
        }
        void PlaceGuideButton(RectTransform card, string name, float x, float y, float w, float h)
        {
            var button = card.Find(name).GetComponent<Button>();
            BattleUiStyle.Place((RectTransform)button.transform, x, y, w, h);
            button.GetComponentInChildren<Text>().fontSize = world ? 24 : name == "Continue" ? 20 : 12;
        }
        Button SmallButton(Transform parent, string title, string copy, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        { var button = Button(parent, title, copy, x, y, w, h, action); button.GetComponentInChildren<Text>().fontSize = 12; return button; }
        void RememberCharacter()
        {
            if (player.CurrentUnit == null) return;
            ReanchorAfterTravel();
            var role = player.CurrentUnit.Definition.Role; recentCharacters.Remove(role); recentCharacters.Insert(0, role);
            if (recentCharacters.Count > 8) recentCharacters.RemoveAt(8);
        }
        bool MatchesFilter(BattleUnitDefinition definition, int filter) => filter == 0 ||
            (filter == 1 && IsFavourite(definition.Role)) || (filter == 2 && definition.IsFlying) ||
            (filter == 3 && recentCharacters.Contains(definition.Role));
        void BuildRosterEntries()
        {
            rosterEntries.Clear();
            foreach (var definition in battle.Definitions) if (MatchesFilter(definition, rosterFilter)) rosterEntries.Add(definition);
            if (rosterFilter == 3) rosterEntries.Sort((a, b) => recentCharacters.IndexOf(a.Role).CompareTo(recentCharacters.IndexOf(b.Role)));
        }
        public bool CanControlCharacter(BattleUnit unit) => unit != null && unit.IsAlive && unit.Team == BattleTeam.Blue &&
            unit.SpawnTower != null && unit.SpawnTower.Owner == BattleTeam.Blue;
        BattleUnit BestCharacter(UnitRole role)
        {
            BattleUnit best = null;
            foreach (var unit in battle.Director.Units)
                if (CanControlCharacter(unit) && unit.Definition.Role == role && unit != player.CurrentUnit &&
                    (best == null || unit.Health > best.Health)) best = unit;
            return best;
        }
        void ControlCharacter(int slot)
        { battle.TrySummonSlot(slot, out _); Refresh(); }

        void ControlLivingCharacter(BattleUnit unit)
        {
            if (unit.SpawnTower != battle.BoundTower && !battle.CanUseSummitActions) battle.ReturnToTower();
            if (player.Possess(unit))
            { if (world) CloseCommandMenu(); else CloseCharacters(); }
        }

        void BeginRecruitment(UnitRole role)
        {
            battle.TryQuickRecruit(role, out _);
            Refresh();
        }

        void NextFavourite()
        {
            int start = player.CurrentUnit == null ? -1 : (int)player.CurrentUnit.Definition.Role;
            for (int n = 1; n <= battle.Definitions.Count; n++)
            {
                var definition = battle.Definitions[(start + n) % battle.Definitions.Count];
                if (!IsFavourite(definition.Role)) continue;
                var unit = BestCharacter(definition.Role); if (unit == null) continue;
                ControlLivingCharacter(unit); return;
            }
            battle.Report("No other available favourite. Recruit a favourite, or return to your summit to switch towers.");
        }
        void TurnCharacterPage(int direction)
        { int count = Mathf.Max(1, Mathf.CeilToInt(characterEntries.Count / 8f)); characterPage = (characterPage + direction + count) % count; Refresh(); }
        void ToggleCharacters()
        {
            ResetHudRound();
            if (charactersOpen) { CloseCharacters(); return; }
            if (!battle.Director.IsBattleRunning || player.IsSwitchingUnit) return;
            AnchorHiddenMenu();
            guideOpen = false; charactersOpen = true; characterFilter = 0; characterPage = 0; player.OpenMenuCursor(); Refresh();
        }
        void CloseCharacters() { charactersOpen = false; if (vrHudPage == 2) vrHudPage = 0; Refresh(); }
        public void OpenGuide()
        {
            ResetHudRound();
            if (player.IsSwitchingUnit) return;
            AnchorHiddenMenu();
            charactersOpen = false; guideOpen = true; guideSection = 0; player.OpenMenuCursor(); Refresh();
        }
        void CloseGuide() { guideOpen = false; if (battle.Director.IsBattleRunning) vrHudVisible = true; if (vrHudPage == 2) vrHudPage = 0; Refresh(); }
        void ToggleHints() { showHints = !showHints; PlayerPrefs.SetInt(HintsKey, showHints ? 1 : 0); PlayerPrefs.Save(); Refresh(); }
        void InterfaceHotkeys()
        {
            bool menu = player.IsUsingXR && player.TouchInput.Left.Menu;
            bool library = player.IsUsingXR && player.TouchInput.Left.Secondary;
            bool menuPressed = menu && !previousControllerMenu, libraryPressed = library && !previousControllerLibrary;
            previousControllerMenu = menu; previousControllerLibrary = library;
            if (menuPressed)
            {
                player.UseTrackedMenuAim();
                ToggleVrHud();
            }
            if (libraryPressed)
            {
                player.UseTrackedMenuAim();
                if (guideOpen) CloseGuide();
                else if (battle.Director.IsBattleRunning) ToggleCharacters();
                else { OpenGuide(); guidePage = 5; Refresh(); }
            }
            var keyboard = Keyboard.current; if (keyboard == null || (!Application.isFocused && !player.IsUsingXR)) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { if (guideOpen) CloseGuide(); else if (charactersOpen) CloseCharacters(); return; }
            if (keyboard.f1Key.wasPressedThisFrame) { if (guideOpen) CloseGuide(); else OpenGuide(); }
            if (keyboard.f2Key.wasPressedThisFrame) ToggleHints();
            if (keyboard.f4Key.wasPressedThisFrame && world) ToggleVrHud();
            if (keyboard.pKey.wasPressedThisFrame && !guideOpen) ToggleCharacters();
        }
        void RefreshInterfaceTools()
        {
            if (guide == null) return;
            bool running = battle.Director.IsBattleRunning;
            if (!running) charactersOpen = false;
            homeGuide.gameObject.SetActive(battle.Director.Phase == BattlePhase.Home);
            guide.gameObject.SetActive(guideOpen); characterBrowser.gameObject.SetActive(charactersOpen && running);
            hintsToggle.GetComponentInChildren<Text>().text = (showHints ? "HIDE" : "SHOW") + (player.IsUsingOpticalHands ? " HINTS" : " HINTS [F2]");
            commandPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 300);
            travel.gameObject.SetActive(true); build.gameObject.SetActive(true); groundWorks.gameObject.SetActive(true);
            commandPanel.Find("Risk").gameObject.SetActive(false);
            hintsToggle.gameObject.SetActive(true);
            var advice = (RectTransform)notice.transform.parent;
            advice.sizeDelta = new Vector2(650, showHints ? 230 : 88);
            foreach (string name in new[] { "Controls page", "Previous controls", "Next controls", "Movement hints" }) advice.Find(name).gameObject.SetActive(showHints);
            bool details = !charactersOpen && (showHints || player.CurrentUnit != null ||
                battle.Recruitment.HasValue || battle.SelectedDefense.HasValue || battle.IsPlanningRoute);
            dossier.transform.parent.gameObject.SetActive(details);
            towerButtons[0].transform.parent.gameObject.SetActive(true);
            troopOrderPanel.gameObject.SetActive(running && !charactersOpen);
            if (charactersOpen)
            { summitPanel.gameObject.SetActive(false); defensePanel.gameObject.SetActive(false); }
            if (defensePanel.gameObject.activeSelf || summitPanel.gameObject.activeSelf) dossier.transform.parent.gameObject.SetActive(false);
            if (charactersOpen) rosterPanel.gameObject.SetActive(false);
            advice.gameObject.SetActive(!charactersOpen);
            rosterEmpty.gameObject.SetActive(rosterEntries.Count == 0);
            rosterEmpty.text = rosterFilter == 1 ? "No favourites yet. Use + FAV on any character card." : "No characters here yet. Control an ally to add it to Recent.";
            for (int i = 0; i < 4; i++)
            {
                SetSelected(rosterFilters[i], rosterFilter == i); SetSelected(characterFilters[i], characterFilter == i);
                rosterFilters[i].gameObject.SetActive(true); characterFilters[i].gameObject.SetActive(true);
            }
            characterBrowser.Find("Next favourite").gameObject.SetActive(true);
            for (int i = 0; i < 8; i++)
            {
                int index = page * 8 + i; recruitFavourites[i].gameObject.SetActive(index < rosterEntries.Count);
                var recruitRect = (RectTransform)recruits[i].transform;
                if (!world) recruitRect.sizeDelta = new Vector2(196, 51);
                if (index < rosterEntries.Count) FavouriteLabel(recruitFavourites[i], rosterEntries[index].Role);
            }
            if (charactersOpen) RefreshCharacters();
            if (guideOpen) RefreshGuide();
        }
        void RefreshCharacters()
        {
            characterBrowser.Find("Title").GetComponent<Text>().text = "SUMMON ALLIES";
            characterStatus.text = "Spend gold to summon allies. Towers collect more gold.\nRepeat any card. No limit on living soldiers.";
            characterStatus.color = Color.white;
            characterBrowser.Find("Close characters").GetComponentInChildren<Text>().text = "CLOSE";
            characterBrowser.Find("Close characters").GetComponentInChildren<Text>().fontSize = 20;
            characterBrowser.Find("Browser footer").GetComponent<Text>().text = "Your squad fights automatically. PLAY has ADVANCE, HOLD and GUARD.";
            foreach (var filter in characterFilters) filter.gameObject.SetActive(false);
            foreach (string name in new[] { "Previous characters", "Next characters", "Next favourite", "Character page" })
                characterBrowser.Find(name).gameObject.SetActive(false);
            for (int i = 0; i < characterCards.Length; i++)
            {
                characterFavourites[i].gameObject.SetActive(false); characterSpawns[i].gameObject.SetActive(false);
                bool exists = i < TowerBattle.SquadSize;
                characterCards[i].gameObject.SetActive(exists); if (!exists) continue;
                var unit = battle.SquadMember(BattleTeam.Blue, i);
                bool alive = unit != null && unit.IsAlive;
                bool current = alive && player.CurrentUnit == unit;
                string name = i < 2 ? "GUARD " + (i + 1) : i < 4 ? "ARCHER " + (i - 1) : "HEALER";
                bool used = battle.WasSummoned(BattleTeam.Blue, i);
                string state = battle.BlueGold + .0001f < battle.SummonCost(i) ? "NEED " + battle.SummonCost(i) + " GOLD" :
                    "SUMMON / " + battle.SummonCost(i) + " GOLD";
                characterCards[i].GetComponentInChildren<Text>().text = name + "\n" + state;
                characterCards[i].GetComponentInChildren<Text>().fontSize = 21;
                characterCards[i].interactable = battle.BlueGold + .0001f >= battle.SummonCost(i);
                BattleUiStyle.Place((RectTransform)characterCards[i].transform, 16 + i % 2 * 314, 178 + i / 2 * 104, 302, 92);
                SetSelected(characterCards[i], current);
            }
        }

        void FavouriteLabel(Button button, UnitRole role)
        { button.GetComponentInChildren<Text>().text = IsFavourite(role) ? "FAV\nON" : "+\nFAV"; SetSelected(button, IsFavourite(role)); }
        static void SetSelected(Button button, bool selected)
        { button.image.color = selected ? new Color(.15f, .39f, .43f) : new Color(.08f, .13f, .15f); }
        void RefreshGuide()
        {
            string[] titles = { "START HERE", "SUMMON ALLIES", "TWO-HANDED BOW", "BUILD WITH YOUR HANDS", "MAP & TOWERS", "CONTROLS & SETTINGS" };
            string[] body = {
                "Open SUMMON and pinch a card to buy an ally.\nYour towers collect gold automatically.\nNeither team has a living-soldier limit.\nGive orders on PLAY; allies fight automatically.\nCLOSE MENU returns to the battlefield.\nPinch MENU to reopen. Every action works with hands.",
                "Guards cost 10 gold, archers 12, healers 14.\nPinch any card to summon another of that type.\nSummon as many allies as you can afford.\nEvery summon costs gold and needs clear ground.\nAllies fight automatically; PLAY gives squad orders.\nCapture the enemy tower to win.",
                "Hold the bow with your left hand.\nPinch near the string with your right hand.\nPull back and aim with both hands.\nThe curved line shows the arrow's flight.\nThe ring marks its predicted landing point.\nRelease to shoot; green means full power.",
                "Choose a defense on BUILD. The menu closes.\nPoint at battlefield ground and pinch to build.\nGreen footprint: clear. Red: blocked.\nEach placement spends the displayed gold cost.\nMENU opens ROTATE, CANCEL and RESUME controls.\nYou can build while staying at your tower.",
                "Gold marks your tower; blue marks your team.\nSelect a tower to inspect it.\nPLAN ROUTE lets you select map waypoints.\nSEND ROUTE sends your squad along that path.\nUNDO POINT removes your last waypoint.\nRouting and tower upgrades are optional.",
                "Right hand: point and pinch to select.\nMENU is beside your left hand.\nCLOSE MENU returns to play.\nPLAY gives orders to your autonomous allies.\nAll actions have hand controls.\nNo controller or keyboard is required."
            };
            guideHeading.text = titles[guidePage]; guideBody.text = body[guidePage]; guideBody.fontSize = world ? 32 : 24;
            guide.Find("How to play/Title").GetComponent<Text>().text = "HELP";
            gestureToggle.gameObject.SetActive(false); hapticsToggle.gameObject.SetActive(false);
            audioToggle.gameObject.SetActive(guidePage == 5);
            controllerRestart.gameObject.SetActive(guidePage == 5 && battle.Director.IsBattleRunning);
            audioToggle.GetComponentInChildren<Text>().text = "SOUND: " + (battle.Director.GetComponent<BattleAudio>()?.Muted == true ? "OFF" : "ON");
            var card = (RectTransform)guide.Find("How to play");
            PlaceGuideButton(card, "Sound", 40, world ? 606 : 488, 440, world ? 52 : 40);
            PlaceGuideButton(card, "Restart siege", 520, world ? 606 : 488, 440, world ? 52 : 40);
            guideFooter.gameObject.SetActive(guidePage != 5);
            guideFooter.text = $"{guidePage + 1} / 6   /   " + (battle.Director.IsBattleRunning ? "Your squad keeps fighting while the menu is open." : "Choose START SIEGE when ready.");
            guideContinue.GetComponentInChildren<Text>().text = battle.Director.Phase == BattlePhase.Home ? "START SIEGE" : battle.Director.IsBattleRunning ? "BACK TO MENU" : "BACK TO RESULTS";
        }
    }
}
