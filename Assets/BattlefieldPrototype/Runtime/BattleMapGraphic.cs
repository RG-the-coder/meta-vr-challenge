using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    /// <summary>Engraved grid and topographic contours in a single, noninteractive UI mesh.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleMapGraphic : MaskableGraphic
    {
        public bool GridOnly;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = GetPixelAdjustedRect();
            Color grid = new Color(.5f,.61f,.59f,.12f), contour = new Color(.64f,.66f,.51f,.12f);
            grid.a *= color.a;
            contour.a *= color.a;
            for (int i=1;i<12;i++) Line(vh,new Vector2(r.xMin+r.width*i/12,r.yMin),new Vector2(r.xMin+r.width*i/12,r.yMax),.65f,grid);
            for (int i=1;i<16;i++) Line(vh,new Vector2(r.xMin,r.yMin+r.height*i/16),new Vector2(r.xMax,r.yMin+r.height*i/16),.65f,grid);
            if (GridOnly) return;
            for (int island=0;island<6;island++) for (int ring=0;ring<5;ring++)
            {
                Vector2 center = new Vector2(r.xMin+r.width*(.1f+(island%3)*.4f),r.yMin+r.height*(.13f+(island/3)*.7f));
                Vector2 previous=Vector2.zero;
                for (int i=0;i<=72;i++)
                {
                    float a=i*Mathf.PI*2/72;
                    float radius=(.055f+ring*.028f)*(1+Mathf.Sin(a*3+island)*.16f+Mathf.Cos(a*5+island)*.11f);
                    Vector2 p=center+new Vector2(Mathf.Cos(a)*r.width*radius,Mathf.Sin(a)*r.height*radius*.7f);
                    p.x=Mathf.Clamp(p.x,r.xMin+1,r.xMax-1); p.y=Mathf.Clamp(p.y,r.yMin+1,r.yMax-1);
                    if(i>0) Line(vh,previous,p,.8f,contour); previous=p;
                }
            }
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f; int i=vh.currentVertCount;
            vh.AddVert(a-n,color,Vector2.zero); vh.AddVert(a+n,color,Vector2.zero);
            vh.AddVert(b+n,color,Vector2.zero); vh.AddVert(b-n,color,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
        }
    }
}
