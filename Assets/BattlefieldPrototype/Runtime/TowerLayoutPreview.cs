using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TowerLayoutPreview : MaskableGraphic
    {
        public TowerBattle Battle;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (Battle == null || Battle.Terrain == null) return;
            Rect rect = GetPixelAdjustedRect(); Vector2 size = Battle.Terrain.ArenaSize;
            float scale = Mathf.Min((rect.width-12)/size.x,(rect.height-12)/size.y);
            Vector2 center = rect.center;
            Box(vh,center,rect.size,new Color(.02f,.04f,.05f)); Box(vh,center,size*scale,new Color(.11f,.17f,.17f));
            foreach (var wall in TowerMapCatalog.Walls(Battle.Map,size))
                Box(vh,center + new Vector2(wall.center.x,wall.center.z)*scale,new Vector2(wall.size.x,wall.size.z)*scale,new Color(.65f,.59f,.45f));
            var spawns = TowerMapCatalog.Spawns(Battle.Map);
            for (int i=0; i<spawns.Count; i++)
            {
                var spawn=spawns[i]; Vector2 point = center + Vector2.Scale(spawn.Position,size)*scale;
                if (i < 2) Box(vh,point,Vector2.one*9,BattleUiStyle.Accent);
                Box(vh,point,Vector2.one*5,spawn.Owner==BattleTeam.Blue ? BattleUiStyle.Friendly : spawn.Owner==BattleTeam.Red ? BattleUiStyle.Enemy : new Color(.7f,.7f,.64f));
            }
        }
        static void Box(VertexHelper vh,Vector2 point,Vector2 size,Color color)
        {
            int i=vh.currentVertCount; Vector2 half=size*.5f;
            vh.AddVert(point+new Vector2(-half.x,-half.y),color,Vector2.zero); vh.AddVert(point+new Vector2(-half.x,half.y),color,Vector2.zero);
            vh.AddVert(point+half,color,Vector2.zero); vh.AddVert(point+new Vector2(half.x,-half.y),color,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
        }
    }
}
