using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    /// <summary>The map follows the bound tower. Taller towers reveal and display more ground.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TowerMinimapGraphic : MaskableGraphic, IPointerClickHandler
    {
        TowerBattle battle;
        PossessionController player;
        Rect area;
        Vector3 center;
        float span;
        public bool IsBuildingMap => battle != null && battle.SelectedDefense.HasValue && battle.CanConstruct;
        public bool IsRoutingMap => battle != null && (battle.IsPlanningRoute || battle.TroopOrders.Current == BattleOrder.Route);
        public void Initialize(TowerBattle siege, PossessionController controller) { battle = siege; player = controller; raycastTarget = true; }
        void RefreshProjection()
        {
            area = GetPixelAdjustedRect();
            center = IsBuildingMap || IsRoutingMap ? Vector3.zero : battle.BoundTower != null ? battle.BoundTower.transform.position : Vector3.zero;
            span = IsBuildingMap || IsRoutingMap ? Mathf.Max(battle.Terrain.ArenaSize.x, battle.Terrain.ArenaSize.y) + 8 : Mathf.Max(16, battle.ScoutRadius * 2);
        }
        Vector2 Project(Vector3 point) => new Vector2(area.center.x + (point.x - center.x) / span * area.width, area.center.y + (point.z - center.z) / span * area.height);
        public Vector3 Unproject(Vector2 point)
        { RefreshProjection(); return center + new Vector3((point.x - area.center.x) / area.width * span, 0, (point.y - area.center.y) / area.height * span); }
        public void OnPointerClick(PointerEventData data)
        {
            if (battle == null || !battle.Director.IsBattleRunning) return;
            Camera eventCamera = canvas.renderMode == RenderMode.WorldSpace ? player.ViewCamera : null;
            Vector2 local;
            if (data is BattleUiPointerEventData worldPointer && worldPointer.HasWorldPosition)
                local = rectTransform.InverseTransformPoint(worldPointer.WorldPosition);
            else if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, eventCamera, out local)) return;
            var point = Unproject(local);
            if (battle.IsPlanningRoute) { battle.AddRouteWaypoint(point); return; }
            if (battle.SelectedDefense.HasValue)
            { battle.TryPlaceDefense(battle.SelectedDefense.Value, point, battle.DefenseRotation, out _); return; }
            foreach (var tower in battle.Towers)
                if (Vector3.ProjectOnPlane(tower.transform.position - point, Vector3.up).sqrMagnitude < 45)
                { battle.SelectedTower = tower; return; }
            if (battle.Recruitment.HasValue) battle.TryDeploy(battle.Recruitment.Value, point, out _);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (battle == null || battle.BoundTower == null) return;
            RefreshProjection(); Box(vh, area.center, area.size, new Color(.025f, .045f, .055f));
            int divisions = 24;
            for (int x = 0; x < divisions; x++) for (int y = 0; y < divisions; y++)
            {
                Vector2 p = new Vector2(area.xMin + (x + .5f) * area.width / divisions, area.yMin + (y + .5f) * area.height / divisions);
                Vector3 worldPoint = center + new Vector3((p.x - area.center.x) / area.width * span, 0, (p.y - area.center.y) / area.height * span);
                if (Mathf.Abs(worldPoint.x) > battle.Terrain.ArenaSize.x * .5f || Mathf.Abs(worldPoint.z) > battle.Terrain.ArenaSize.y * .5f) continue;
                bool scouted = battle.IsScouted(worldPoint);
                bool buildZone = IsBuildingMap && battle.IsInDefenseBuildZone(worldPoint);
                Box(vh, p, new Vector2(area.width / divisions - .4f, area.height / divisions - .4f), buildZone ? new Color(.20f, .19f, .105f) :
                    scouted ? new Color(.11f, .18f, .18f) : new Color(.055f, .08f, .09f));
            }
            foreach (var cover in battle.Terrain.CoverFootprints)
            {
                bool wall = false; foreach (var blocker in battle.Terrain.TowerSightBlockers) if (blocker == cover) { wall = true; break; }
                if (!wall && !battle.IsScouted(cover.center)) continue;
                var p = Project(cover.center); var half = new Vector2(Mathf.Max(2, cover.size.x / span * area.width), Mathf.Max(2, cover.size.z / span * area.height)) * .5f;
                float left = Mathf.Max(area.xMin,p.x-half.x), right = Mathf.Min(area.xMax,p.x+half.x), bottom = Mathf.Max(area.yMin,p.y-half.y), top = Mathf.Min(area.yMax,p.y+half.y);
                if (right > left && top > bottom) Box(vh,new Vector2((left+right)*.5f,(bottom+top)*.5f),new Vector2(right-left,top-bottom),wall ? new Color(.47f,.44f,.37f) : new Color(.36f,.37f,.29f));
            }
            if (!IsBuildingMap && !IsRoutingMap) Ring(vh, Project(center), battle.DeploymentRadius / span * area.width, battle.DeploymentRadius / span * area.height, new Color(.2f, .85f, 1, .8f));
            else if (IsBuildingMap) foreach (var tower in battle.Towers)
                if (tower.Owner == BattleTeam.Blue && !tower.IsBroken) BuildRing(vh, Project(tower.transform.position), TowerBattle.DefenseBuildRadius(tower));
            foreach (var defense in battle.Defenses)
            {
                if (defense == null || !defense.IsActive || defense.Owner != BattleTeam.Blue && !battle.IsScouted(defense.transform.position)) continue;
                var p = Project(defense.transform.position); if (!Inside(p)) continue;
                Box(vh, p, defense.IsBarricade ? new Vector2(7, 3) : Vector2.one * 4, defense.IsBarricade ? BattleUiStyle.Friendly : new Color(1, .62f, .22f));
            }
            DrawRoute(vh, battle.TroopOrders.Waypoints, BattleUiStyle.Accent);
            if (battle.IsPlanningRoute) DrawRoute(vh, battle.RouteDraft, Color.white);
            foreach (var tower in battle.Towers)
            {
                var p = Project(tower.transform.position); if (!Inside(p)) continue;
                Color tint = tower.Owner == BattleTeam.Blue ? BattleUiStyle.Friendly : tower.Owner == BattleTeam.Red ? BattleUiStyle.Enemy : new Color(.65f, .61f, .49f);
                if (tower.Owner == BattleTeam.Red && !battle.IsScouted(tower.Summit)) tint = new Color(.25f, .28f, .27f);
                if (tower == battle.BoundTower || tower == battle.EnemyBoundTower) Box(vh, p, Vector2.one * 14, BattleUiStyle.Accent);
                Box(vh, p, Vector2.one * 9, tint);
                if (tower.IsBroken) Box(vh, p, Vector2.one * 4, new Color(.06f, .08f, .09f));
                if (tower == battle.SelectedTower) Ring(vh, p, 9, 9, Color.white);
                if (tower.CaptureProgress > 0) Box(vh, p + Vector2.up * 11, new Vector2(18 * tower.CaptureProgress, 3), BattleUiStyle.Accent);
            }
            foreach (var unit in battle.Director.Units)
            {
                if (unit == null || !unit.IsAlive || unit.Team != BattleTeam.Blue && !battle.IsScouted(unit.EyePosition)) continue;
                if (player.CurrentUnit != null && unit.Team != BattleTeam.Blue && !player.CanSeeUnit(unit)) continue;
                Vector2 p = Project(unit.transform.position); if (!Inside(p)) continue;
                Box(vh, p, Vector2.one * (unit.Definition.IsFlying ? 4 : 2.8f), unit.Team == BattleTeam.Blue ? BattleUiStyle.Friendly : BattleUiStyle.Enemy);
            }
            Vector2 camera = Project(player.ViewCamera.transform.position);
            if (Inside(camera))
            {
                Vector3 f = player.ViewCamera.transform.forward; Vector2 dir = new Vector2(f.x, f.z).normalized;
                BattleMinimapGraphic.Line(vh, camera, camera + dir * 10, 2, Color.white); Box(vh, camera, Vector2.one * 4, Color.white);
            }
        }
        bool Inside(Vector2 point) => point.x > area.xMin + 10 && point.x < area.xMax - 10 && point.y > area.yMin + 10 && point.y < area.yMax - 10;
        void DrawRoute(VertexHelper vh, System.Collections.Generic.IReadOnlyList<Vector3> points, Color color)
        {
            for (int i = 0; i < points.Count; i++)
            {
                var point = Project(points[i]); if (!area.Contains(point)) continue;
                if (i > 0) BattleMinimapGraphic.Line(vh, Project(points[i - 1]), point, 2, color);
                Box(vh, point, Vector2.one * 7, color); Box(vh, point, Vector2.one * 3, new Color(.035f, .055f, .065f));
            }
        }
        void BuildRing(VertexHelper vh, Vector2 p, float radius)
        {
            float rx = radius / span * area.width, ry = radius / span * area.height;
            for (int i = 0; i < 128; i++)
            {
                float a = i * Mathf.PI / 64, b = (i + 1) * Mathf.PI / 64;
                Vector2 from = p + new Vector2(Mathf.Sin(a) * rx, Mathf.Cos(a) * ry), to = p + new Vector2(Mathf.Sin(b) * rx, Mathf.Cos(b) * ry);
                if (area.Contains(from) && area.Contains(to)) BattleMinimapGraphic.Line(vh, from, to, 1.2f, BattleUiStyle.Accent);
            }
        }
        static void Box(VertexHelper vh, Vector2 p, Vector2 size, Color tint)
        {
            int i = vh.currentVertCount; Vector2 half = size * .5f;
            vh.AddVert(p + new Vector2(-half.x, -half.y), tint, Vector2.zero); vh.AddVert(p + new Vector2(-half.x, half.y), tint, Vector2.zero);
            vh.AddVert(p + half, tint, Vector2.zero); vh.AddVert(p + new Vector2(half.x, -half.y), tint, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        static void Ring(VertexHelper vh, Vector2 p, float rx, float ry, Color tint)
        {
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64, b = (i + 1) * Mathf.PI * 2 / 64;
                BattleMinimapGraphic.Line(vh, p + new Vector2(Mathf.Sin(a) * rx, Mathf.Cos(a) * ry), p + new Vector2(Mathf.Sin(b) * rx, Mathf.Cos(b) * ry), 1.2f, tint);
            }
        }
    }
}
