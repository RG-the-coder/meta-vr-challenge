using System.Collections.Generic;
using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class BattlefieldBootstrap
    {
        public TowerMapKind TowerMap = TowerMapKind.OpenMarch;
        public IReadOnlyList<Bounds> TowerSightBlockers => towerSightBlockers;
        readonly List<Bounds> towerSightBlockers = new List<Bounds>();
        TowerMapKind builtTowerMap;
        bool hasTowerArena;
        internal void EnsureTowerArena()
        {
            if (!TowerMapCatalog.IsValid(TowerMap)) TowerMap = TowerMapKind.OpenMarch;
            if (!hasTowerArena || builtTowerMap != TowerMap || arenaRoot == null) CreateArena(MapKind);
        }
        void BuildAlternateTowerTerrain()
        {
            if (TowerMap == TowerMapKind.TwinFronts)
            {
                for (int side = -1; side <= 1; side += 2)
                    Cover("Frontier barricade", new Vector3(side * ArenaSize.x * .15f, 1, 0), new Vector3(5,2,8));
                return;
            }
            if (TowerMap == TowerMapKind.Crossroads)
            {
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                    Cover("Crossroads ruin", new Vector3(x*ArenaSize.x*.13f,3,z*ArenaSize.y*.12f),new Vector3(14,6,3));
                return;
            }
            foreach (var wall in TowerMapCatalog.Walls(TowerMap,ArenaSize))
            {
                Cover("High sight-blocking wall", wall.center, wall.size); towerSightBlockers.Add(wall);
                Vector3 band = wall.size; band.y = .3f;
                if (wall.size.x > wall.size.z) band.z += .03f; else band.x += .03f;
                Shape("High wall stone coping",PrimitiveType.Cube,arenaRoot,wall.center + Vector3.up*(wall.extents.y-.4f),band,trim,false);
            }
        }
    }
    public sealed partial class TowerBattle
    {
        public TowerMapKind Map => Terrain.TowerMap;
        public string MapName => TowerMapCatalog.Name(Map);
        public bool SelectMap(TowerMapKind kind)
        {
            if (Director == null || Director.Phase != BattlePhase.Home) return Fail("Choose a battlefield from Home before starting a siege.");
            if (!TowerMapCatalog.IsValid(kind)) return false;
            // Already on Home: changing the preview must not reset the round,
            // teleport the camera, or summon the panel at a new head pose.
            if (Terrain.TowerMap != kind)
            {
                Terrain.TowerMap = kind;
                Terrain.ClearArmies(); ClearTowers(); Terrain.EnsureTowerArena(); CreateTowers();
            }
            Report(MapName + " selected: " + towers.Count + " towers / " + TowerMapCatalog.WallHeight(Map) + "m walls."); return true;
        }
    }
}
