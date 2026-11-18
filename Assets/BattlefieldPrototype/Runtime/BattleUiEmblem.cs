using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    /// <summary>Resolution-independent class heraldry; one UI mesh per emblem.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleUiEmblem : MaskableGraphic
    {
        public UnitRole Role;
        VertexHelper mesh;
        Rect bounds;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh = vh; bounds = GetPixelAdjustedRect();
            switch (Role)
            {
                case UnitRole.Dawnwing:
                    Path(new[] { P(.5f,.18f), P(.08f,.78f), P(.38f,.64f), P(.5f,.92f), P(.62f,.64f), P(.92f,.78f) }, true);
                    Line(.5f,.27f,.5f,.62f); Line(.34f,.45f,.66f,.45f); break;
                case UnitRole.Emberwing:
                    Path(new[] { P(.5f,.1f), P(.08f,.6f), P(.28f,.58f), P(.19f,.88f), P(.43f,.68f), P(.5f,.97f), P(.61f,.63f), P(.85f,.88f), P(.73f,.57f), P(.94f,.6f) }, true);
                    Line(.42f,.32f,.58f,.55f); break;
                case UnitRole.Crownwing:
                    Path(new[] { P(.5f,.12f), P(.07f,.68f), P(.29f,.61f), P(.27f,.94f), P(.5f,.75f), P(.73f,.94f), P(.71f,.61f), P(.93f,.68f) }, true);
                    Line(.39f,.39f,.61f,.39f); Line(.39f,.39f,.36f,.56f); Line(.61f,.39f,.64f,.56f); Line(.36f,.56f,.5f,.49f); Line(.5f,.49f,.64f,.56f); break;
                case UnitRole.Skywing:
                    Path(new[] { P(.5f,.15f), P(.08f,.82f), P(.4f,.61f), P(.5f,.83f), P(.6f,.61f), P(.92f,.82f) }, true);
                    Line(.5f,.2f,.5f,.72f); break;
                case UnitRole.Stormwing:
                    Path(new[] { P(.5f,.1f), P(.08f,.67f), P(.31f,.6f), P(.19f,.91f), P(.5f,.71f), P(.81f,.91f), P(.69f,.6f), P(.92f,.67f) }, true);
                    Line(.4f,.62f,.56f,.5f); Line(.56f,.5f,.44f,.38f); break;
                case UnitRole.Halberdier:
                    Line(.46f,.06f,.46f,.92f,.065f);
                    Path(new[]{P(.46f,.95f),P(.37f,.73f),P(.46f,.65f),P(.55f,.73f)},true);
                    Path(new[]{P(.51f,.77f),P(.81f,.85f),P(.87f,.64f),P(.51f,.59f)},true); break;
                case UnitRole.Duelist:
                    Ring(.27f,.31f,.18f,18); Line(.22f,.10f,.78f,.89f,.055f);
                    Line(.46f,.29f,.69f,.46f); Line(.78f,.89f,.65f,.79f); Line(.78f,.89f,.76f,.72f); break;
                case UnitRole.Frostbinder:
                    for(int i=0;i<6;i++)
                    {
                        float a=i*Mathf.PI/3; Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),n=new Vector2(-d.y,d.x);
                        Segment(P(.5f,.5f),P(.5f+d.x*.44f,.5f+d.y*.44f),.05f);
                        Vector2 tip=Vector2.one*.5f+d*.30f;
                        Segment(P(tip.x,tip.y),P(tip.x-d.x*.10f+n.x*.10f,tip.y-d.y*.10f+n.y*.10f),.045f);
                        Segment(P(tip.x,tip.y),P(tip.x-d.x*.10f-n.x*.10f,tip.y-d.y*.10f-n.y*.10f),.045f);
                    } break;
                case UnitRole.Alchemist:
                    Path(new[]{P(.36f,.92f),P(.64f,.92f),P(.64f,.81f),P(.59f,.81f),P(.59f,.59f),P(.84f,.16f),P(.77f,.09f),P(.23f,.09f),P(.16f,.16f),P(.41f,.59f),P(.41f,.81f),P(.36f,.81f)},true);
                    Line(.28f,.28f,.72f,.28f); Ring(.47f,.42f,.045f,10); break;
                case UnitRole.WarDrummer:
                    Path(new[]{P(.14f,.65f),P(.5f,.77f),P(.86f,.65f),P(.86f,.24f),P(.5f,.10f),P(.14f,.24f)},true);
                    Line(.14f,.65f,.5f,.51f); Line(.5f,.51f,.86f,.65f); Line(.5f,.51f,.5f,.10f);
                    Line(.18f,.94f,.69f,.64f,.055f); Line(.82f,.94f,.31f,.64f,.055f); break;
                case UnitRole.Sharpshooter:
                    Ring(.5f,.5f,.3f,24); Ring(.5f,.5f,.07f,12);
                    Line(.5f,.06f,.5f,.33f); Line(.5f,.67f,.5f,.94f); Line(.06f,.5f,.33f,.5f); Line(.67f,.5f,.94f,.5f); break;
                case UnitRole.Berserker:
                    Line(.18f,.10f,.74f,.82f,.075f); Line(.82f,.10f,.26f,.82f,.075f);
                    Path(new[]{P(.54f,.77f),P(.64f,.95f),P(.94f,.72f),P(.80f,.55f)},true);
                    Path(new[]{P(.46f,.77f),P(.36f,.95f),P(.06f,.72f),P(.20f,.55f)},true); break;
                case UnitRole.Warden:
                    Path(new[]{P(.5f,.1f),P(.18f,.38f),P(.25f,.74f),P(.5f,.94f),P(.75f,.74f),P(.82f,.38f)},true);
                    Line(.5f,.12f,.5f,.82f); Line(.5f,.38f,.29f,.55f); Line(.5f,.53f,.71f,.70f); break;
                case UnitRole.Vanguard:
                    Path(new Vector2[] { P(.18f,.82f), P(.5f,.94f), P(.82f,.82f), P(.77f,.38f), P(.5f,.09f), P(.23f,.38f) }, true);
                    Line(.5f,.29f,.5f,.77f);
                    break;
                case UnitRole.Ranger:
                    Path(new[] { P(.3f,.12f), P(.57f,.3f), P(.68f,.5f), P(.57f,.7f), P(.3f,.88f) }, false);
                    Line(.3f,.12f,.3f,.88f); Line(.12f,.5f,.92f,.5f); Line(.74f,.65f,.92f,.5f); Line(.74f,.35f,.92f,.5f);
                    break;
                case UnitRole.Militia:
                    Path(new[] { P(.08f,.64f), P(.26f,.77f), P(.5f,.81f), P(.74f,.77f), P(.92f,.64f) }, false);
                    Path(new[] { P(.08f,.64f), P(.5f,.5f), P(.92f,.64f) }, false);
                    Line(.5f,.1f,.5f,.94f,.08f); Line(.38f,.81f,.5f,.94f); Line(.5f,.94f,.62f,.81f);
                    break;
                case UnitRole.Medic:
                    Line(.5f,.15f,.5f,.85f,.14f); Line(.15f,.5f,.85f,.5f,.14f); Ring(.5f,.5f,.45f,28); break;
                case UnitRole.Brute:
                    Line(.25f,.16f,.65f,.73f,.075f);
                    Path(new[] { P(.34f,.75f), P(.48f,.94f), P(.91f,.64f), P(.77f,.45f) }, true);
                    break;
                case UnitRole.Titan:
                    Path(new[] { P(.24f,.08f), P(.24f,.26f), P(.12f,.52f), P(.12f,.76f), P(.29f,.82f),
                        P(.31f,.94f), P(.48f,.94f), P(.55f,.85f), P(.75f,.85f), P(.84f,.75f), P(.84f,.44f), P(.67f,.25f), P(.67f,.08f) }, true);
                    Line(.31f,.63f,.31f,.89f); Line(.48f,.63f,.48f,.87f); Line(.66f,.62f,.66f,.79f); Line(.16f,.52f,.54f,.38f);
                    break;
                case UnitRole.Stormcaller:
                    Path(new[] { P(.59f,.94f), P(.23f,.48f), P(.51f,.48f), P(.39f,.08f), P(.79f,.59f), P(.5f,.59f) }, true);
                    break;
                case UnitRole.Wisp:
                    Path(new[] { P(.5f,.96f), P(.61f,.61f), P(.94f,.5f), P(.61f,.39f), P(.5f,.04f), P(.39f,.39f), P(.06f,.5f), P(.39f,.61f) }, true);
                    break;
                case UnitRole.Sentinel:
                    Path(new[] { P(.15f,.12f), P(.15f,.88f), P(.32f,.88f), P(.32f,.72f), P(.43f,.72f), P(.43f,.91f),
                        P(.59f,.91f), P(.59f,.72f), P(.7f,.72f), P(.7f,.88f), P(.87f,.88f), P(.87f,.12f) }, true);
                    Path(new[] { P(.41f,.12f), P(.41f,.4f), P(.58f,.4f), P(.58f,.12f) }, false);
                    break;
                case UnitRole.Paladin:
                    Ring(.5f,.5f,.23f,24);
                    Line(.5f,.34f,.5f,.66f,.075f); Line(.34f,.5f,.66f,.5f,.075f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        Line(.5f+Mathf.Cos(a)*.34f,.5f+Mathf.Sin(a)*.34f,.5f+Mathf.Cos(a)*.46f,.5f+Mathf.Sin(a)*.46f,.065f);
                    }
                    break;
                case UnitRole.Engineer:
                    Ring(.5f,.5f,.28f,12); Ring(.5f,.5f,.1f,12);
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; Line(.5f+Mathf.Cos(a)*.29f,.5f+Mathf.Sin(a)*.29f,.5f+Mathf.Cos(a)*.43f,.5f+Mathf.Sin(a)*.43f,.085f); }
                    break;
                case UnitRole.Pyromancer:
                    Path(new[] { P(.5f,.96f), P(.7f,.62f), P(.79f,.74f), P(.85f,.36f), P(.69f,.14f), P(.42f,.1f), P(.2f,.3f), P(.24f,.59f), P(.37f,.47f) }, true);
                    Path(new[] { P(.48f,.2f), P(.63f,.35f), P(.52f,.58f), P(.39f,.34f) }, false); break;
                case UnitRole.Assassin:
                    Path(new[] { P(.5f,.95f), P(.62f,.44f), P(.5f,.34f), P(.38f,.44f) }, true);
                    Line(.25f,.32f,.75f,.32f,.075f); Line(.5f,.1f,.5f,.32f,.08f); Line(.4f,.09f,.6f,.09f,.075f);
                    break;
                case UnitRole.Skirmisher:
                    for (int i = 0; i < 3; i++)
                    {
                        float x = .12f + i * .25f;
                        Path(new[] { P(x,.82f), P(x+.23f,.5f), P(x,.18f) }, false);
                    }
                    break;
                case UnitRole.Bombard:
                    Ring(.48f,.42f,.26f,24); Line(.38f,.68f,.54f,.84f,.09f); Line(.54f,.84f,.74f,.8f); Line(.74f,.8f,.83f,.96f); break;
                case UnitRole.Hound:
                    Path(new[] { P(.2f,.9f), P(.42f,.73f), P(.58f,.73f), P(.8f,.9f), P(.76f,.38f), P(.5f,.13f), P(.24f,.38f) }, true);
                    Line(.29f,.57f,.41f,.53f,.07f); Line(.59f,.53f,.71f,.57f,.07f); Line(.43f,.34f,.57f,.34f); break;
            }
        }
        Vector2 P(float x, float y) => new Vector2(bounds.xMin + x * bounds.width, bounds.yMin + y * bounds.height);
        void Ring(float x, float y, float radius, int count)
        {
            for (int i = 0; i < count; i++) { float a = i*Mathf.PI*2/count, b=(i+1)*Mathf.PI*2/count;
                Line(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius,.045f); }
        }
        void Path(Vector2[] points, bool close)
        { for (int i = 0; i < points.Length - (close ? 0 : 1); i++) Segment(points[i], points[(i+1)%points.Length], .055f); }
        void Line(float x, float y, float xx, float yy, float width=.06f) => Segment(P(x,y),P(xx,yy),width);
        void Segment(Vector2 a, Vector2 b, float width)
        {
            Vector2 n = new Vector2(-(b-a).y,(b-a).x).normalized * Mathf.Min(bounds.width,bounds.height)*width*.5f;
            int index=mesh.currentVertCount;
            mesh.AddVert(a-n,color,Vector2.zero); mesh.AddVert(a+n,color,Vector2.zero);
            mesh.AddVert(b+n,color,Vector2.zero); mesh.AddVert(b-n,color,Vector2.zero);
            mesh.AddTriangle(index,index+1,index+2); mesh.AddTriangle(index,index+2,index+3);
        }
    }
}
