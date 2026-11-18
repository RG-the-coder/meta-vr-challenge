using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        Button towerPulse, pulseUpgrade;
        void CreateTowerPulseControls()
        {
            Button(summitPanel, "Bow gesture", "BOW: POINT + PINCH", 14, 344, 380, 52, () =>
            { player.PhysicalHandBow = !player.PhysicalHandBow; Refresh(); });
            towerPulse = Button(summitPanel, "Tower wave", "REPULSION WAVE [4]", 14, 258, 184, 66,
                () => { battle.TryTowerPulse(); Refresh(); });
            pulseUpgrade = Button(summitPanel, "Upgrade tower wave", "", 210, 258, 184, 66,
                () => { battle.TryUpgradeTowerPulse(); Refresh(); });
            towerPulse.GetComponentInChildren<Text>().fontSize = 14;
            pulseUpgrade.GetComponentInChildren<Text>().fontSize = 14;
        }
        void RefreshTowerPulseControls()
        {
            summitPanel.Find("Bow gesture").GetComponentInChildren<Text>().text = player.PhysicalHandBow
                ? "CURRENT: DRAW + RELEASE  /  SWITCH TO POINT + PINCH" : "CURRENT: POINT + PINCH  /  SWITCH TO DRAW + RELEASE";
            summitPanel.Find("Bow gesture").gameObject.SetActive(player.IsUsingOpticalHands);
            var tower = battle.BoundTower; float remaining = tower.PulseRemaining;
            towerPulse.GetComponentInChildren<Text>().text = (player.IsUsingOpticalHands ? "REPULSION WAVE\nLV " : "REPULSION WAVE [" + (world ? "L STICK" : "4 / K") + "]\nLV ") +
                tower.PulseLevel + " / FREE / " + (remaining > 0 ? remaining.ToString("0.0") + "s" : "READY");
            towerPulse.interactable = battle.CanUseSummitActions && remaining <= 0;
            bool max = tower.PulseLevel >= BattleTower.MaximumPulseLevel;
            pulseUpgrade.GetComponentInChildren<Text>().text = max ? "MAX LEVEL 3\nSHIELD BREAK + AFTERSHOCK" :
                "UPGRADE: " + tower.NextPulseUpgrade.ToUpperInvariant() + "\n" + tower.PulseUpgradeCost + "g / THIS TOWER";
            pulseUpgrade.interactable = battle.CanUseSummitActions && !max && battle.BlueGold >= tower.PulseUpgradeCost;
            summitInfo.text = (player.IsUsingOpticalHands && !player.PhysicalHandBow ? "Bow: point + pinch. " : "Bow: draw / release. ") +
                "Lightning 110. Repulse 8m. Heal 60 + 35 shield / 8s.\n" +
                $"Wave LV {tower.PulseLevel}: {tower.PulseRadius:0}m radius / {tower.PulsePushDistance:0}m push / {tower.PulseCooldown:0}s. Zero health damage.\n" +
                (max ? "This tower: shield break + 3s slow. Wave affects all nearby ground / air enemies." :
                    "Next: " + tower.NextPulseUpgrade + (tower.PulseLevel == 1 ? " removes enemy shields." : " adds a 3s slow.") + " Upgrade applies to this tower.");
        }
    }
}
