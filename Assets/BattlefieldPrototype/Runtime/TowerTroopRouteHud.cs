using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        RectTransform troopOrderPanel;
        Text troopOrderTitle, troopOrderHelp;
        Button planRoute, sendRoute, undoRoute, advanceOrder, holdOrder, guardOrder, cancelRoute;
        LineRenderer troopRouteLine;
        readonly LineRenderer[] troopRouteMarkers = new LineRenderer[BattleOrders.MaximumWaypoints];
        void CreateTroopOrderControls()
        {
            troopOrderPanel = Panel(live, "Troop orders", 0, 0, 304, 208);
            troopOrderPanel.anchorMin = troopOrderPanel.anchorMax = troopOrderPanel.pivot = new Vector2(1, 0);
            troopOrderPanel.anchoredPosition = new Vector2(-18, 18);
            troopOrderTitle = Label(troopOrderPanel, "Current troop order", "", 14, 12, 8, 280, 24, BattleUiStyle.Accent);
            planRoute = Button(troopOrderPanel, "Plan route", "PLAN ROUTE [N]", 12, 38, 134, 30,
                () => { if (battle.IsPlanningRoute) battle.CancelTroopRoute(); else battle.BeginRoutePlanning(); Refresh(); });
            advanceOrder = Button(troopOrderPanel, "Advance order", "ADVANCE [Z]", 158, 38, 134, 30, () => IssueTroopOrder(BattleOrder.Advance));
            sendRoute = Button(troopOrderPanel, "Send route", "GO ROUTE", 12, 76, 134, 30, () => { battle.StartTroopRoute(); Refresh(); });
            undoRoute = Button(troopOrderPanel, "Undo route", "UNDO POINT", 158, 76, 134, 30, () => { battle.UndoRouteWaypoint(); Refresh(); });
            holdOrder = Button(troopOrderPanel, "Hold order", "HOLD [X]", 12, 114, 86, 30, () => IssueTroopOrder(BattleOrder.Hold));
            guardOrder = Button(troopOrderPanel, "Guard order", "GUARD [V]", 109, 114, 86, 30, () => IssueTroopOrder(BattleOrder.Protect));
            cancelRoute = Button(troopOrderPanel, "Cancel route", "CANCEL", 206, 114, 86, 30, () => { battle.CancelTroopRoute(); Refresh(); });
            foreach (var button in new[] { planRoute, sendRoute, undoRoute, advanceOrder, holdOrder, guardOrder, cancelRoute }) button.GetComponentInChildren<Text>().fontSize = 12;
            troopOrderHelp = Label(troopOrderPanel, "Route instructions", "", 12, 12, 155, 280, 43, Color.white);
            troopRouteLine = MakeLine("Troop route", BattleUiStyle.Accent, .12f);
            for (int i = 0; i < troopRouteMarkers.Length; i++)
            { troopRouteMarkers[i] = MakeLine("Route waypoint " + (i + 1), BattleUiStyle.Accent, .1f); troopRouteMarkers[i].loop = true; troopRouteMarkers[i].positionCount = 16; }
        }
        void IssueTroopOrder(BattleOrder order)
        {
            if (battle.TroopOrders.IssueForPlayer(order, player)) battle.Report(battle.TroopOrders.Description + " issued to friendly troops.");
            Refresh();
        }
        void RefreshTroopOrderControls()
        {
            var orders = battle.TroopOrders; bool planning = battle.IsPlanningRoute;
            troopOrderTitle.text = "ARMY / " + orders.Description;
            planRoute.interactable = !player.IsSwitchingUnit;
            planRoute.GetComponentInChildren<Text>().text = planning ? "CANCEL PLAN [N]" : "PLAN ROUTE [N]";
            sendRoute.interactable = planning && battle.RouteDraft.Count > 0;
            undoRoute.interactable = planning && battle.RouteDraft.Count > 0;
            holdOrder.interactable = guardOrder.interactable = battle.Director.AliveBlue > 0;
            cancelRoute.interactable = planning || orders.Current == BattleOrder.Route;
            troopOrderHelp.text = planning ? $"PLAN {battle.RouteDraft.Count} / 8. Click ground or map.\nGO ROUTE sends autonomous friendly troops.\nEnter: send / Backspace: undo." : orders.Current == BattleOrder.Route ?
                "Troops follow each waypoint, then resume siege.\nAdvance / Hold / Guard replace the route.\nPlan again to choose another path." :
                "Plan waypoints for an alternate path.\nOrders are free; combat continues.";
            if (player.IsUsingOpticalHands && planning)
                troopOrderHelp.text = $"PLAN {battle.RouteDraft.Count} / 8. Pinch ground or map.\nGO ROUTE sends your troops.\nUNDO POINT removes the last waypoint.";
            foreach (var button in new[] { advanceOrder, holdOrder, guardOrder })
                button.image.color = (button == advanceOrder && orders.Current == BattleOrder.Advance || button == holdOrder && orders.Current == BattleOrder.Hold ||
                    button == guardOrder && orders.Current == BattleOrder.Protect) ? new Color(.28f, .25f, .13f) : new Color(.08f, .13f, .15f);
        }
        void UpdateTroopRouteVisuals()
        {
            var points = battle.IsPlanningRoute ? battle.RouteDraft : battle.TroopOrders.Waypoints;
            bool show = battle.Director.IsBattleRunning && points.Count > 0;
            Color color = battle.IsPlanningRoute ? Color.white : BattleUiStyle.Accent;
            troopRouteLine.enabled = show && points.Count > 1;
            if (troopRouteLine.enabled)
            {
                troopRouteLine.startColor = troopRouteLine.endColor = color; troopRouteLine.positionCount = points.Count;
                for (int i = 0; i < points.Count; i++) troopRouteLine.SetPosition(i, points[i] + Vector3.up * .16f);
            }
            for (int i = 0; i < troopRouteMarkers.Length; i++)
            {
                var marker = troopRouteMarkers[i]; marker.enabled = show && i < points.Count;
                if (!marker.enabled) continue; marker.startColor = marker.endColor = color;
                for (int j = 0; j < 16; j++)
                { float a = j * Mathf.PI / 8; marker.SetPosition(j, points[i] + new Vector3(Mathf.Sin(a) * .8f, .15f, Mathf.Cos(a) * .8f)); }
            }
        }
    }
}
