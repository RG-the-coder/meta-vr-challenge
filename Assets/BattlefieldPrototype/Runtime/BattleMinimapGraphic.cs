using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed partial class BattleMinimapGraphic : MaskableGraphic
    {
        public BattleDirector Director;
        public BattlefieldBootstrap Terrain;
        public PossessionController Player;
        Rect area;
        Vector2 size;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if(Director==null) return;
            area=GetPixelAdjustedRect(); size=Terrain!=null?Terrain.ArenaSize:new Vector2(160,192);
            float width=Mathf.Min(area.width,area.height*size.x/size.y);
            float height=width*size.y/size.x;
            area=new Rect(area.center.x-width*.5f,area.center.y-height*.5f,width,height);
            Box(vh,area,new Color(.075f,.11f,.12f));
            for(int i=1;i<8;i++)
            {
                Line(vh,new Vector2(area.xMin+width*i/8,area.yMin),new Vector2(area.xMin+width*i/8,area.yMax),.6f,new Color(.32f,.41f,.4f,.25f));
                Line(vh,new Vector2(area.xMin,area.yMin+height*i/8),new Vector2(area.xMax,area.yMin+height*i/8),.6f,new Color(.32f,.41f,.4f,.25f));
            }
            if(Terrain!=null) foreach(var cover in Terrain.CoverFootprints)
            {
                Vector2 a=Project(cover.min), b=Project(cover.max);
                Box(vh,Rect.MinMaxRect(Mathf.Clamp(a.x,area.xMin,area.xMax),Mathf.Clamp(a.y,area.yMin,area.yMax),
                    Mathf.Clamp(b.x,area.xMin,area.xMax),Mathf.Clamp(b.y,area.yMin,area.yMax)),new Color(.37f,.39f,.31f));
            }
            if(Terrain!=null) foreach(var gate in Terrain.MovingObstacles)
            {
                Vector2 a=Project(gate.ClosedBounds.min), b=Project(gate.ClosedBounds.max);
                Box(vh,Rect.MinMaxRect(a.x,a.y,b.x,b.y),gate.IsPassable?new Color(.2f,.85f,.5f,.5f):new Color(1,.58f,.16f));
            }
            if (Player != null && Player.HasLimitedSight) DrawSightFog(vh);
            foreach(var unit in Director.Units)
            {
                if(unit==null || !unit.IsAlive || Player != null && !Player.CanSeeUnit(unit)) continue;
                Vector2 p=Project(unit.transform.position);
                if(!area.Contains(p)) continue;
                float dot=unit==Player?.CurrentUnit?4:2.5f;
                Color tint=unit.Team==BattleTeam.Blue?new Color(.3f,.78f,1):new Color(1,.3f,.2f);
                Box(vh,new Rect(p.x-dot*.5f,p.y-dot*.5f,dot,dot),tint);
            }
            if(Player!=null)
            {
                Vector2 p=Project(Player.ViewCamera.transform.position);
                p=new Vector2(Mathf.Clamp(p.x,area.xMin+5,area.xMax-5),Mathf.Clamp(p.y,area.yMin+5,area.yMax-5));
                Vector3 f=Player.ViewCamera.transform.forward;
                Vector2 forward=new Vector2(f.x,f.z).normalized, right=new Vector2(forward.y,-forward.x);
                Triangle(vh,p+forward*6,p-forward*4+right*3.5f,p-forward*4-right*3.5f,Color.white);
            }
            DrawFlareMarkers(vh);
        }
        public Vector2 Project(Vector3 position)
        { position -= Director != null ? Director.transform.position : Vector3.zero;
          return new Vector2(area.xMin+(position.x/size.x+.5f)*area.width,area.yMin+(position.z/size.y+.5f)*area.height); }
        static void Box(VertexHelper vh,Rect r,Color tint)
        { int i=vh.currentVertCount; vh.AddVert(new Vector2(r.xMin,r.yMin),tint,Vector2.zero); vh.AddVert(new Vector2(r.xMin,r.yMax),tint,Vector2.zero); vh.AddVert(new Vector2(r.xMax,r.yMax),tint,Vector2.zero); vh.AddVert(new Vector2(r.xMax,r.yMin),tint,Vector2.zero); vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3); }
        internal static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
        { Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f; int i=vh.currentVertCount;
          vh.AddVert(a-n,tint,Vector2.zero); vh.AddVert(a+n,tint,Vector2.zero); vh.AddVert(b+n,tint,Vector2.zero); vh.AddVert(b-n,tint,Vector2.zero); vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3); }
        static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color tint)
        { int i=vh.currentVertCount; vh.AddVert(a,tint,Vector2.zero); vh.AddVert(b,tint,Vector2.zero); vh.AddVert(c,tint,Vector2.zero); vh.AddTriangle(i,i+1,i+2); }
    }
}
