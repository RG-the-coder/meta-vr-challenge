using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        readonly TwoHandHudGesture vrHudGesture = new TwoHandHudGesture();
        readonly PalmMenuGesture palmMenu = new PalmMenuGesture();
        readonly HandPageSwipe pageSwipe = new HandPageSwipe();
        readonly Button[] vrHudTabs = new Button[6];
        RectTransform vrHudTabPanel;
        bool vrHudVisible = true;
        int vrHudPage;

        void CreateVrHudTabs()
        {
            vrHudTabPanel = Panel(live, "VR HUD pages", 395, 16, 650, 116);
            string[] names = { "COMMAND", "MAP + TOWERS", "RECRUIT", "POWERS", "BUILD", "HIDE" };
            string[] labels = { "PLAY", "MAP", "ARMY", "POWERS", "BUILD", "HIDE" };
            for (int i = 0; i < vrHudTabs.Length; i++)
            {
                int index = i;
                vrHudTabs[i] = Button(vrHudTabPanel, "VR " + names[i], labels[i], 8 + 212 * (i % 3), 7 + 54 * (i / 3), 202, 48,
                    () => { if (index == 5) ToggleVrHud(); else SelectCommandPage(index); });
                vrHudTabs[i].GetComponentInChildren<Text>().fontSize = 22;
            }
            vrHudTabPanel.gameObject.SetActive(false);
        }

        void SelectCommandPage(int index)
        {
            if (!battle.Director.IsBattleRunning || player.IsSwitchingUnit) return;
            vrHudPage = (index + 5) % 5; charactersOpen = false; vrHudVisible = true;
            if (vrHudPage != 1 && vrHudPage != 4 && battle.SelectedDefense.HasValue) battle.ChooseDefense(null);
            if (vrHudPage == 2) { charactersOpen = true; characterFilter = 0; }
            if (vrHudPage == 4)
            {
                // Browsing never moves the player; choosing a defense enters placement.
                defenseOptionsExpanded = true;
            }
            CancelPointer(); Refresh();
        }

        void ToggleVrHud()
        {
            if (HandMenuVisible) CloseCommandMenu(); else OpenCommandMenu();
        }

        void UpdateVrHudGesture()
        {
            // Menu access is a visible button, with no palm pose or timing to learn.
        }

        void ApplyVrHudLayout()
        {
            ApplySimpleLayout();
        }
    }
}
