using System.Collections.Generic;
using UnityEngine;

namespace BattlefieldPrototype
{
    public enum TowerMapKind { OpenMarch, TwinFronts, Crossroads, IronDivide, SkyCitadel }
    public readonly struct TowerMapSpawn
    {
        public readonly string Title;
        public readonly Vector2 Position;
        public readonly BattleTeam? Owner;
        public readonly float Height;
        public TowerMapSpawn(string title, float x, float z, BattleTeam? owner = null, float height = 0)
        { Title = title; Position = new Vector2(x, z); Owner = owner; Height = height; }
    }
    public static class TowerMapCatalog
    {
        public const int Count = 5, MaximumTowers = 13;
        public static bool IsValid(TowerMapKind kind) => (int)kind >= 0 && (int)kind < Count;
        public static string Name(TowerMapKind kind) => kind == TowerMapKind.TwinFronts ? "Twin Fronts" : kind == TowerMapKind.Crossroads ? "Crossroads" :
            kind == TowerMapKind.IronDivide ? "Iron Divide" : kind == TowerMapKind.SkyCitadel ? "Sky Citadel" : "Open March";
        public static float WallHeight(TowerMapKind kind) => kind == TowerMapKind.IronDivide ? 18 : kind == TowerMapKind.SkyCitadel ? 28 : kind == TowerMapKind.Crossroads ? 6 : kind == TowerMapKind.TwinFronts ? 2 : 3;
        public static string Description(TowerMapKind kind) => kind == TowerMapKind.TwinFronts ? "Five towers on opposite diagonal fronts. Open ground and one central ruin." :
            kind == TowerMapKind.Crossroads ? "Nine towers and five neutral objectives, with 6m cover around crossing lanes." :
            kind == TowerMapKind.IronDivide ? "An 18m wall splits eleven towers. Two flank gates connect the ground routes. Build tall to scout over it." :
            kind == TowerMapKind.SkyCitadel ? "Thirteen towers across a 28m wall. Two gates lead between courtyards. Near-maximum towers can overlook far courtyards." :
            "Seven towers, low ruined walls and open lanes. The original battlefield.";
        static readonly TowerMapSpawn[][] layouts = {
            new[] {
                new TowerMapSpawn("Dawn Keep",0,-.35f,BattleTeam.Blue,12), new TowerMapSpawn("Ember Keep",0,.35f,BattleTeam.Red,12),
                new TowerMapSpawn("Willow Watch",-.25f,-.18f,BattleTeam.Blue,6), new TowerMapSpawn("Cinder Watch",.25f,.18f,BattleTeam.Red,6),
                new TowerMapSpawn("West Spire",-.29f,.1f), new TowerMapSpawn("East Spire",.29f,-.1f), new TowerMapSpawn("Crown Crossing",0,0) },
            new[] {
                new TowerMapSpawn("Dawn Keep",-.26f,-.34f,BattleTeam.Blue,12), new TowerMapSpawn("Ember Keep",.26f,.34f,BattleTeam.Red,12),
                new TowerMapSpawn("Willow Watch",.26f,-.34f,BattleTeam.Blue,6), new TowerMapSpawn("Cinder Watch",-.26f,.34f,BattleTeam.Red,6),
                new TowerMapSpawn("Crossing Keep",0,0) },
            new[] {
                new TowerMapSpawn("Dawn Keep",-.25f,-.34f,BattleTeam.Blue,12), new TowerMapSpawn("Ember Keep",.25f,.34f,BattleTeam.Red,12),
                new TowerMapSpawn("Willow Watch",.25f,-.34f,BattleTeam.Blue,6), new TowerMapSpawn("Cinder Watch",-.25f,.34f,BattleTeam.Red,6),
                new TowerMapSpawn("South Crossing",0,-.16f), new TowerMapSpawn("North Crossing",0,.16f), new TowerMapSpawn("West Crossing",-.28f,0),
                new TowerMapSpawn("East Crossing",.28f,0), new TowerMapSpawn("Crown Crossing",0,0) },
            new[] {
                new TowerMapSpawn("Dawn Keep",0,-.35f,BattleTeam.Blue,12), new TowerMapSpawn("Ember Keep",0,.35f,BattleTeam.Red,12),
                new TowerMapSpawn("Willow Watch",-.28f,-.25f,BattleTeam.Blue,6), new TowerMapSpawn("Cinder Watch",.28f,.25f,BattleTeam.Red,6),
                new TowerMapSpawn("South Bastion",.28f,-.25f), new TowerMapSpawn("North Bastion",-.28f,.25f),
                new TowerMapSpawn("West Approach",-.22f,-.11f), new TowerMapSpawn("East Approach",.22f,.11f),
                new TowerMapSpawn("South Gatewatch",0,-.14f), new TowerMapSpawn("North Gatewatch",0,.14f), new TowerMapSpawn("Iron Watch",0,.07f) },
            new[] {
                new TowerMapSpawn("Dawn Keep",-.12f,-.30f,BattleTeam.Blue,12), new TowerMapSpawn("Ember Keep",.12f,.30f,BattleTeam.Red,12),
                new TowerMapSpawn("Willow Watch",-.32f,.25f,BattleTeam.Blue,6), new TowerMapSpawn("Cinder Watch",.32f,-.25f,BattleTeam.Red,6),
                new TowerMapSpawn("West Gatewatch",-.12f,-.08f), new TowerMapSpawn("East Gatewatch",.12f,.08f),
                new TowerMapSpawn("Southwest Spire",-.32f,-.30f), new TowerMapSpawn("Northeast Spire",.32f,.30f),
                new TowerMapSpawn("West Courtyard",-.32f,0), new TowerMapSpawn("East Courtyard",.32f,0),
                new TowerMapSpawn("Northwest Spire",-.12f,.30f), new TowerMapSpawn("Southeast Spire",.12f,-.30f), new TowerMapSpawn("Sky Watch",-.12f,.10f) }
        };
        public static IReadOnlyList<TowerMapSpawn> Spawns(TowerMapKind kind) => layouts[IsValid(kind) ? (int)kind : 0];
        public static Bounds[] Walls(TowerMapKind kind, Vector2 size)
        {
            if (kind != TowerMapKind.IronDivide && kind != TowerMapKind.SkyCitadel) return new Bounds[0];
            bool vertical = kind == TowerMapKind.SkyCitadel;
            float length = vertical ? size.y : size.x, gate = length * (vertical ? .12f : .32f), halfGap = 7;
            float[] ends = { -length * .5f, -gate-halfGap, -gate+halfGap, gate-halfGap, gate+halfGap, length*.5f };
            var walls = new Bounds[3]; float height = WallHeight(kind);
            for (int i = 0; i < 3; i++)
            {
                float center = (ends[i*2] + ends[i*2+1]) * .5f, extent = ends[i*2+1] - ends[i*2];
                walls[i] = new Bounds(vertical ? new Vector3(0,height*.5f,center) : new Vector3(center,height*.5f,0),
                    vertical ? new Vector3(3,height,extent) : new Vector3(extent,height,3));
            }
            return walls;
        }
    }
}
