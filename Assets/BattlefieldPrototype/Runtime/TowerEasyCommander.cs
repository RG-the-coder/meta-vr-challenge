using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattle
    {

        public bool TryQuickRecruit(UnitRole role, out BattleUnit unit)
        {
            unit = null;
            for (int slot = 0; slot < SquadSize; slot++)
                if (SquadRoles[slot] == role) return TrySummonSlot(slot, out unit);
            return Fail("This character is not available on the summon cards.");
        }

        public bool TryFireEasyBow(Ray aim)
        {
            if (!CanUseSummitActions || SelectedSummitPower.HasValue || IsPlanningRoute ||
                !float.IsFinite(aim.direction.sqrMagnitude) || aim.direction.sqrMagnitude < .001f) return false;
            Vector3 direction = aim.direction.normalized;
            BattleUnit target = null;
            float bestAlignment = Mathf.Cos(6 * Mathf.Deg2Rad);
            foreach (var enemy in Director.Units)
            {
                if (enemy == null || !enemy.IsAlive || enemy.Team != BattleTeam.Red) continue;
                Vector3 delta = enemy.AimPosition - aim.origin;
                if (delta.sqrMagnitude > SummitPowerRange * SummitPowerRange || delta.sqrMagnitude < .01f) continue;
                float alignment = Vector3.Dot(direction, delta.normalized);
                if (alignment <= bestAlignment || !ClearSummitLine(aim.origin, enemy.AimPosition)) continue;
                bestAlignment = alignment; target = enemy;
            }
            if (target != null)
            {
                // A small aim assist and low ballistic arc make pointing at a visible enemy sufficient.
                Vector3 destination = target.AimPosition + target.PlanarVelocity * (Vector3.Distance(aim.origin, target.AimPosition) / 110f);
                Vector3 delta = destination - aim.origin;
                Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
                float distance = horizontal.magnitude, speedSquared = 110f * 110f;
                float discriminant = speedSquared * speedSquared - 9.81f * (9.81f * distance * distance + 2 * delta.y * speedSquared);
                if (distance > .01f && discriminant >= 0)
                {
                    float elevation = Mathf.Atan((speedSquared - Mathf.Sqrt(discriminant)) / (9.81f * distance));
                    direction = horizontal.normalized * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation);
                }
            }
            return TryFireSummitBow(new Ray(aim.origin, direction));
        }
    }

    public sealed partial class TowerBattleHud
    {
        RectTransform easyCommanderPanel;
        readonly Button[] quickTroops = new Button[3];
        Button easyWave, easyDefend;
        float easyFeedbackUntil;

        void CreateEasyCommander()
        {
            easyCommanderPanel = Panel(live, "Easy commander", 448, 370, 650, 260);
            Label(easyCommanderPanel, "Heading", "YOUR ARMY", 22, 16, 12, 618, 30, BattleUiStyle.Accent);
            Label(easyCommanderPanel, "Instructions", "2 guards, 2 archers, 1 healer. They move and fight automatically.", 17, 16, 48, 618, 42, Color.white);
            for (int i = 0; i < quickTroops.Length; i++)
            {
                int slot = i;
                quickTroops[i] = Button(easyCommanderPanel, "Quick troop " + i, "", 16 + i * 212, 94, 194, 88, () =>
                {
                    battle.CommandSquad((BattleOrder)slot);
                    easyFeedbackUntil = Time.unscaledTime + 4; Refresh();
                });
                quickTroops[i].GetComponentInChildren<Text>().fontSize = 19;
            }
            easyDefend = Button(easyCommanderPanel, "Easy defend", "DEFEND / HIDE MENU", 16, 204, 300, 40, () =>
            {
                SelectCommandPage(2);
            });
            easyWave = Button(easyCommanderPanel, "Easy wave", "PUSH ENEMIES BACK", 334, 204, 300, 40, () =>
            {
                battle.TryTowerPulse(); easyFeedbackUntil = Time.unscaledTime + 4; Refresh();
            });
            easyDefend.GetComponentInChildren<Text>().fontSize = easyWave.GetComponentInChildren<Text>().fontSize = 16;
            easyCommanderPanel.gameObject.SetActive(false);
        }

        void RefreshEasyCommander()
        {
            if (easyCommanderPanel == null) return;
            bool commander = battle.Director.IsBattleRunning;
            bool visible = commander && !charactersOpen && !guideOpen && (!world || vrHudVisible) && vrHudPage == 0;
            easyCommanderPanel.gameObject.SetActive(visible);
            if (!visible) return;
            if (battle.IsMounted && player.CurrentUnit == null) mode.text = "DEFEND YOUR TOWER";
            towerStats.text = $"SUMMON: no soldier limit. Towers collect gold.\nSummoned allies fight automatically. Capture their tower to win.";
            if (battle.BoundTower.CapturingTeam == BattleTeam.Red && battle.BoundTower.CaptureProgress > 0)
                notice.text = "YOUR TOWER IS UNDER ATTACK!\nShoot the raiders or PUSH ENEMIES BACK.";
            else if (Time.unscaledTime < easyFeedbackUntil) notice.text = battle.Message;
            else if (player.CurrentUnit != null)
                notice.text = "LEFT pinch + drag: move. Release to stop. RIGHT point + pinch: attack.\nTURN buttons change facing. POWERS selects an ability. SUMMON has RETURN TO TOWER.";
            else if (player.IsUsingOpticalHands)
                notice.text = player.PhysicalHandBow
                    ? "Close the menu to draw. Aim with both hands along the curved guide.\nThe MENU button beside your left hand reopens controls."
                    : "Close the menu, point at an enemy and pinch once to shoot.\nThe MENU button beside your left hand reopens controls.";
            BattleUiStyle.Place(easyCommanderPanel, 254, 374, 932, 312);
            var heading = easyCommanderPanel.Find("Heading").GetComponent<Text>(); heading.text = "COMMAND YOUR SQUAD"; heading.fontSize = 27;
            BattleUiStyle.Place(heading.rectTransform, 18, 12, 896, 36);
            var instructions = easyCommanderPanel.Find("Instructions").GetComponent<Text>(); instructions.text = "Choose an order for your surviving allies."; instructions.fontSize = 22;
            BattleUiStyle.Place(instructions.rectTransform, 18, 54, 896, 36);
            string[] names = { "ADVANCE", "HOLD", "GUARD" };
            string[] hints = { "Attack the enemy", "Stay in position", "Protect an ally" };
            for (int i = 0; i < quickTroops.Length; i++)
            {
                PlaceButton(quickTroops[i], 18 + i * 300, 108, 284, 100, 25);
                quickTroops[i].GetComponentInChildren<Text>().text = names[i] + "\n" + hints[i];
                quickTroops[i].interactable = battle.Director.IsBattleRunning && battle.Director.AliveBlue > 0;
                SetSelected(quickTroops[i], battle.TroopOrders.Current == (BattleOrder)i);
            }
            float remaining = battle.BoundTower != null ? battle.BoundTower.PulseRemaining : 0;
            easyWave.interactable = battle.CanUseSummitActions && remaining <= 0;
            easyWave.GetComponentInChildren<Text>().text = remaining > 0 ? $"PUSH READY IN {remaining:0}s" : "PUSH ENEMIES BACK";
            easyDefend.gameObject.SetActive(player.CurrentUnit == null);
            easyDefend.GetComponentInChildren<Text>().text = "SUMMON ALLIES";
            PlaceButton(easyDefend, 18, 228, 440, 64, 24);
            PlaceButton(easyWave, player.CurrentUnit == null ? 474 : 18, 228, 440, 64, 24);
        }
    }
}
