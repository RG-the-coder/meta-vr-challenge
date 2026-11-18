using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        RectTransform defensePanel;
        Text defenseInfo, defenseHeading;
        Button defenseRotate, defenseCancel, defenseChange, defenseWorld, groundWorks;
        bool defenseOptionsExpanded;
        readonly Button[] defenseChoices = new Button[DefenseCatalog.Count];
        readonly LineRenderer[] defenseRings = new LineRenderer[TowerMapCatalog.MaximumTowers];
        LineRenderer defensePreview;

        void CreateDefensePanel()
        {
            groundWorks = Button(commandPanel, "Ground works", "BUILD DEFENSES", 16, 209, 182, 26,
                () => { SelectCommandPage(4); });
            groundWorks.GetComponentInChildren<Text>().fontSize = 13;
            defensePanel = Panel(live, "Ground works", 18, 370, 408, 304);
            defenseHeading = Label(defensePanel, "Ground heading", "GROUND WORKS / BARRICADES & TRAPS", 16, 14, 12, 470, 28, BattleUiStyle.Accent);
            defenseChange = Button(defensePanel, "Change defense", "CHANGE TYPE", 330, 10, 150, 30, () => SelectCommandPage(4));
            defenseChange.GetComponentInChildren<Text>().fontSize = 14;
            defenseRotate = Button(defensePanel, "Rotate defense", "ROTATE [Y]", 494, 10, 142, 30, () => { battle.RotateDefense(); Refresh(); });
            defenseRotate.GetComponentInChildren<Text>().fontSize = 14;
            for (int i = 0; i < defenseChoices.Length; i++)
            {
                var kind = (DefenseKind)i;
                defenseChoices[i] = Button(defensePanel, "Build " + kind, "", 14 + i % 2 * 196, 52 + i / 2 * 58, 184, 48,
                    () =>
                    {
                        if (player.CurrentUnit != null) battle.ReturnToTower();
                        if (battle.ChooseDefense(kind))
                        {
                            defenseOptionsExpanded = false;
                            if (world) CloseCommandMenu();
                            else SelectCommandPage(1);
                        }
                        Refresh();
                    });
                defenseChoices[i].GetComponentInChildren<Text>().fontSize = 15;
            }
            defenseCancel = Button(defensePanel, "Cancel defense", "CANCEL PLACEMENT", 438, 118, 198, 54, () => { battle.ChooseDefense(null); SelectCommandPage(4); });
            defenseCancel.GetComponentInChildren<Text>().fontSize = 14;
            defenseInfo = Label(defensePanel, "Build instructions", "", 17, 14, 188, 622, 114, Color.white);
            defenseWorld = Button(defensePanel, "Place in world", "PLACE IN WORLD", 16, 178, 276, 56, CloseCommandMenu);
        }
        void CreateDefenseVisuals()
        {
            for (int i = 0; i < defenseRings.Length; i++) defenseRings[i] = MakeLine("Owned tower build zone " + i, new Color(1, .76f, .26f, .75f), .16f);
            defensePreview = MakeLine("Defense footprint preview", new Color(.2f, 1, .55f), .12f);
            defensePreview.loop = true; defensePreview.positionCount = 4;
        }
        void RefreshDefenseControls()
        {
            bool groundCommand = !battle.IsMounted && player.CurrentUnit == null;
            defenseWorld.gameObject.SetActive(false);
            groundWorks.interactable = !player.IsSwitchingUnit;
            defensePanel.gameObject.SetActive(groundCommand && !battle.IsPlanningRoute);
            bool expanded = !battle.SelectedDefense.HasValue || defenseOptionsExpanded;
            BattleUiStyle.Place(defensePanel, 18, expanded ? 370 : 500, 408, expanded ? 304 : 174);
            BattleUiStyle.Place((RectTransform)defenseChange.transform, 14, 44, 184, 30);
            BattleUiStyle.Place((RectTransform)defenseRotate.transform, 210, expanded ? 10 : 44, 184, 30);
            defenseChange.gameObject.SetActive(!expanded);
            defenseHeading.text = expanded ? "GROUND WORKS" : DefenseCatalog.Name(battle.SelectedDefense.Value).ToUpperInvariant() +
                " / " + DefenseCatalog.Cost(battle.SelectedDefense.Value) + "g";
            BattleUiStyle.Place(defenseHeading.rectTransform, 14, 12, expanded ? 184 : 380, 28);
            defenseHeading.fontSize = 15;
            bool canBuild = battle.Director.IsBattleRunning && !player.IsSwitchingUnit && battle.Defenses.Count < TowerBattle.MaximumDefenses;
            for (int i = 0; i < defenseChoices.Length; i++)
            {
                var kind = (DefenseKind)i;
                defenseChoices[i].gameObject.SetActive(expanded);
                defenseChoices[i].GetComponentInChildren<Text>().text = DefenseCatalog.Name(kind).ToUpperInvariant() + "\n" + DefenseCatalog.Cost(kind) + "g / " +
                    (DefenseCatalog.IsBarricade(kind) ? DefenseCatalog.Health(kind) + " HP" : kind == DefenseKind.Caltrops ? "REUSABLE" : "ONE USE");
                defenseChoices[i].interactable = canBuild && battle.BlueGold >= DefenseCatalog.Cost(kind);
                defenseChoices[i].image.color = battle.SelectedDefense == kind ? new Color(.15f, .39f, .43f) : new Color(.08f, .13f, .15f);
            }
            defenseRotate.interactable = battle.CanConstruct && battle.SelectedDefense.HasValue;
            defenseRotate.GetComponentInChildren<Text>().text = $"ROTATE {battle.DefenseRotation:0}" + (player.IsUsingOpticalHands ? "" : " [Y]");
            defenseCancel.interactable = battle.SelectedDefense.HasValue;
            BattleUiStyle.Place((RectTransform)defenseCancel.transform, 210, expanded ? 168 : 142, 184, expanded ? 48 : 24);
            BattleUiStyle.Place(defenseInfo.rectTransform, 14, expanded ? 230 : 84, 380, expanded ? 60 : 52);
            defenseInfo.fontSize = 13;
            string selection = battle.SelectedDefense.HasValue ? DefenseCatalog.Description(battle.SelectedDefense.Value) : "";
            defenseInfo.text = !expanded ? (!battle.CanConstruct ? "BUILDING LOCKED: descend near ground.\n" : selection + "\n") +
                "Click ground / minimap. Green: clear. Red: blocked." :
                "BARRICADES & TRAPS / Owned towers' gold zones.\n" +
                (!battle.CanConstruct ? "Choose a defense to enter ground placement.\n" : "Choose a defense, then click ground / minimap.\n") +
                $"{battle.Defenses.Count} / {TowerBattle.MaximumDefenses} built. Each placement costs gold.";
            if (player.IsUsingOpticalHands) defenseInfo.text = defenseInfo.text.Replace("Click ground", "Pinch ground").Replace("click ground", "pinch ground");
        }
        void UpdateDefenseVisuals()
        {
            bool building = battle.CanConstruct && battle.SelectedDefense.HasValue;
            for (int i = 0; i < defenseRings.Length; i++)
            {
                var line = defenseRings[i]; var tower = i < battle.Towers.Count ? battle.Towers[i] : null;
                line.enabled = building && tower != null && tower.Owner == BattleTeam.Blue && !tower.IsBroken;
                if (!line.enabled) continue;
                float radius = TowerBattle.DefenseBuildRadius(tower); line.loop = true; line.positionCount = 96;
                for (int j = 0; j < 96; j++)
                { float a = j * Mathf.PI * 2 / 96; line.SetPosition(j, tower.transform.position + new Vector3(Mathf.Sin(a) * radius, .13f, Mathf.Cos(a) * radius)); }
            }
            defensePreview.enabled = false;
            if (!building || PointerOverUI || ControllerOverUI || !battle.TryDefenseAimPoint(player.AimRay, out var hitPoint)) return;
            var kind = battle.SelectedDefense.Value;
            bool valid = battle.TryDefensePlacement(kind, hitPoint, battle.DefenseRotation, out var position, out _, out _) &&
                battle.BlueGold >= DefenseCatalog.Cost(kind) && battle.Defenses.Count < TowerBattle.MaximumDefenses;
            Color color = valid ? new Color(.25f, 1, .55f) : new Color(1, .28f, .18f);
            defensePreview.startColor = defensePreview.endColor = color; defensePreview.enabled = true;
            Vector3 half = DefenseCatalog.Size(kind) * .5f;
            var rotation = Quaternion.Euler(0, battle.DefenseRotation, 0);
            for (int i = 0; i < 4; i++)
                defensePreview.SetPosition(i, (valid ? position : hitPoint) + Vector3.up * .12f +
                    rotation * new Vector3((i < 2 ? -1 : 1) * half.x, 0, (i == 0 || i == 3 ? -1 : 1) * half.z));
        }
    }
}
