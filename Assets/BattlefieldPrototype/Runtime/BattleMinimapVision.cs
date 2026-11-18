using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class BattleMinimapGraphic
    {
        public const int SightRayBudget = 100;
        public int LastVisibilityRayCount { get; private set; }
        public int VisibilityRefreshCount { get; private set; }
        readonly List<float> sightAngles = new List<float>(108);
        readonly List<Vector3> sightBoundary = new List<Vector3>(108);
        readonly List<Vector3> sightFeather = new List<Vector3>(108);
        readonly List<Vector3> mapBoundary = new List<Vector3>(108);
        BattleUnit sightUnit;
        Vector3 sightOrigin, sightDirection;
        Vector2 sightMapSize;
        float nextSightRefresh;
        int sightVersion = -1;

        void DrawSightFog(VertexHelper vh)
        {
            Vector3 eye = Player.ViewCamera.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(Player.ViewCamera.transform.forward, Vector3.up).normalized;
            if (sightUnit != Player.CurrentUnit || sightVersion != BattleDirector.SightGeometryVersion || sightMapSize != size ||
                Time.unscaledTime >= nextSightRefresh || (eye - sightOrigin).sqrMagnitude > .16f || Vector3.Dot(forward, sightDirection) < .9994f)
                RefreshSightBoundary(eye, forward);
            Color shadow = new Color(.015f, .025f, .035f, .72f), transparent = new Color(.015f, .025f, .035f, 0);
            for (int i = 0; i < sightBoundary.Count - 1; i++)
            {
                Vector2 a = Project(sightBoundary[i]), b = Project(sightBoundary[i + 1]);
                FogQuad(vh, a, b, Project(mapBoundary[i + 1]), Project(mapBoundary[i]), shadow, shadow);
                FogQuad(vh, Project(sightFeather[i]), Project(sightFeather[i + 1]), b, a, transparent, shadow);
                if ((a - Project(sightOrigin)).sqrMagnitude > 1 && (b - Project(sightOrigin)).sqrMagnitude > 1)
                    Line(vh, a, b, .65f, new Color(.75f, .87f, .88f, .28f));
            }
        }

        void RefreshSightBoundary(Vector3 eye, Vector3 forward)
        {
            VisibilityRefreshCount++;
            sightUnit = Player.CurrentUnit; sightOrigin = eye; sightDirection = forward; sightMapSize = size;
            sightVersion = BattleDirector.SightGeometryVersion; nextSightRefresh = Time.unscaledTime + .075f;
            sightAngles.Clear(); sightBoundary.Clear(); sightFeather.Clear(); mapBoundary.Clear();
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float halfAngle = Mathf.Clamp(sightUnit.Definition.SightAngle, 90, 170) * .5f;
            sightAngles.Add(-180); sightAngles.Add(180);
            const int rays = 95;
            for (int i = 0; i <= rays; i++) sightAngles.Add(Mathf.Lerp(-halfAngle, halfAngle, i / (float)rays));
            Vector3 center = Director.transform.position;
            Vector2 half = size * .5f;
            Vector3 origin = eye;
            origin.x = Mathf.Clamp(origin.x, center.x - half.x + .001f, center.x + half.x - .001f);
            origin.z = Mathf.Clamp(origin.z, center.z - half.y + .001f, center.z + half.y - .001f);
            // Include the exact four map corners so no bright corner slivers remain.
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                sightAngles.Add(Mathf.DeltaAngle(yaw, Mathf.Atan2(center.x + x * half.x - origin.x, center.z + z * half.y - origin.z) * Mathf.Rad2Deg));
            sightAngles.Sort();
            bool clearOrigin = sightUnit.IsSightOriginClear(eye);
            LastVisibilityRayCount = 0;
            float previous = -1000;
            foreach (float angle in sightAngles)
            {
                if (angle - previous < .001f) continue;
                previous = angle;
                float radians = (yaw + angle) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians));
                float edge = MapEdgeDistance(origin - center, direction, half);
                float visible = 0;
                if (Mathf.Abs(angle) <= halfAngle + .001f && clearOrigin)
                {
                    visible = sightUnit.TraceTerrainSight(eye, direction, Mathf.Min(Player.ViewSightRange, edge));
                    LastVisibilityRayCount++;
                }
                sightBoundary.Add(origin + direction * visible);
                sightFeather.Add(origin + direction * Mathf.Max(0, visible - 1.4f));
                mapBoundary.Add(origin + direction * edge);
            }
        }

        static float MapEdgeDistance(Vector3 origin, Vector3 direction, Vector2 half)
        {
            float x = Mathf.Abs(direction.x) < .00001f ? float.PositiveInfinity :
                ((direction.x > 0 ? half.x : -half.x) - origin.x) / direction.x;
            float z = Mathf.Abs(direction.z) < .00001f ? float.PositiveInfinity :
                ((direction.z > 0 ? half.y : -half.y) - origin.z) / direction.z;
            return Mathf.Max(0, Mathf.Min(x, z));
        }

        static void FogQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color inner, Color outer)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, inner, Vector2.zero); vh.AddVert(b, inner, Vector2.zero);
            vh.AddVert(c, outer, Vector2.zero); vh.AddVert(d, outer, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        void DrawFlareMarkers(VertexHelper vh)
        {
            if (Director == null || !Director.IsBattleRunning) return;
            for (int team = 0; team < 2; team++)
            {
                if (!Director.Flares.TryGetFlare((BattleTeam)team, out var flare)) continue;
                Vector2 p = Project(flare.Position);
                Color tint = team == 0 ? new Color(.25f, .9f, 1) : new Color(1, .48f, .12f);
                float radius = 5 + Mathf.Sin((Director.BattleDuration - flare.LaunchedAt) * 3) * 1.2f;
                Line(vh, p + Vector2.up * 4, p + Vector2.right * 4, 1.5f, tint);
                Line(vh, p + Vector2.right * 4, p + Vector2.down * 4, 1.5f, tint);
                Line(vh, p + Vector2.down * 4, p + Vector2.left * 4, 1.5f, tint);
                Line(vh, p + Vector2.left * 4, p + Vector2.up * 4, 1.5f, tint);
                for (int i = 0; i < 20; i++)
                {
                    float a = i * Mathf.PI / 10, b = (i + 1) * Mathf.PI / 10;
                    Line(vh, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                        p + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, .8f, tint * new Color(1, 1, 1, .6f));
                }
            }
        }
    }
}
