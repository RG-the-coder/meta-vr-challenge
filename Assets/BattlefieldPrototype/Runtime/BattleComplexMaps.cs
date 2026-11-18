using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    public sealed partial class BattlefieldBootstrap
    {
        readonly List<BattleMovingObstacle> movingObstacles = new List<BattleMovingObstacle>();
        readonly List<Mesh> mapMeshes = new List<Mesh>();
        readonly List<Vector3> routeCheckpoints = new List<Vector3>();
        public IReadOnlyList<BattleMovingObstacle> MovingObstacles => movingObstacles;
        public IReadOnlyList<Vector3> RouteCheckpoints => routeCheckpoints;
        Material timber, roofTile, sandstone, factoryIron;
        Mesh gableRoof, cragMesh;

        void BuildComplexMap()
        {
            if(timber==null)
            {
                timber=MakeMaterial(new Color(.24f,.16f,.09f)); timber.mainTexture=leather.mainTexture;
                roofTile=MakeMaterial(new Color(.22f,.29f,.31f)); roofTile.mainTexture=stone.mainTexture;
                sandstone=MakeMaterial(new Color(.48f,.34f,.23f)); sandstone.mainTexture=stone.mainTexture;
                factoryIron=MakeMaterial(new Color(.19f,.22f,.23f),.65f,.25f); factoryIron.mainTexture=armor.mainTexture;
            }
            routeCheckpoints.Clear();
            switch(CurrentMapKind)
            {
                case BattleMapKind.IronCitadel: BuildCitadel(); break;
                case BattleMapKind.BlackstoneMaze: BuildLabyrinth(); break;
                case BattleMapKind.Ironworks: BuildIronworks(); break;
                case BattleMapKind.SplitstoneCanyon: BuildCanyon(); break;
                case BattleMapKind.SunkenHarbor: BuildHarbor(); break;
                case BattleMapKind.SunlessCatacombs: BuildCatacombs(); break;
                case BattleMapKind.ShatteredBazaar: BuildBazaar(); break;
                case BattleMapKind.ThornSanctum: BuildSanctum(); break;
                case BattleMapKind.EmberAqueduct: BuildAqueduct(); break;
                case BattleMapKind.CrownQuarry: BuildQuarry(); break;
                case BattleMapKind.StormMonastery: BuildMonastery(); break;
                case BattleMapKind.ObsidianBastion: BuildBastion(); break;
            }
            BatchMapGeometry();
        }

        // Tall solid cores own collision; dressed edges, masonry courses and roofs are visual children.
        void Wall(string name, Vector2 center, Vector2 size, float height=8, Material material=null)
        {
            var p=new Vector3(center.x,height*.5f,center.y);
            var dimensions=new Vector3(size.x,height,size.y);
            Shape(name,PrimitiveType.Cube,arenaRoot,p,dimensions,material!=null?material:stone,true);
            coverFootprints.Add(new Bounds(p,dimensions));
            bool alongX=size.x>size.y;
            Shape(name+" coping",PrimitiveType.Cube,arenaRoot,new Vector3(p.x,height+.12f,p.z),
                new Vector3(size.x+.24f,.24f,size.y+.24f),material==timber?timber:trim,false);
            for(float y=1.7f;y<height;y+=2.5f)
                Shape("Masonry course",PrimitiveType.Cube,arenaRoot,new Vector3(p.x,y,p.z),
                    new Vector3(size.x+.07f,.10f,size.y+.07f),material==factoryIron?armor:roofTile,false);
            float length=alongX?size.x:size.y;
            for(float a=-length*.5f+2;a<length*.5f-1;a+=7)
                Shape("Wall buttress",PrimitiveType.Cube,arenaRoot,new Vector3(p.x+(alongX?a:0),height*.45f,p.z+(alongX?0:a)),
                    new Vector3(alongX?.55f:size.x+.35f,height*.9f,alongX?size.y+.35f:.55f),material!=null?material:stone,false);
        }

        void DoorWall(string name,Vector2 center,float length,bool alongX,float height,float gap,params float[] doors)
        {
            var ordered=new List<float>(doors); ordered.Sort();
            float edge=-length*.5f;
            foreach(float door in ordered)
            {
                float start=Mathf.Clamp(door-gap*.5f,-length*.5f,length*.5f);
                if(start>edge) Wall(name,center+(alongX?Vector2.right:Vector2.up)*((edge+start)*.5f),
                    alongX?new Vector2(start-edge,1.5f):new Vector2(1.5f,start-edge),height);
                edge=Mathf.Max(edge,door+gap*.5f);
                // Lintels frame entrances without admitting a view over the solid wall sections.
                Shape("Gateway lintel",PrimitiveType.Cube,arenaRoot,new Vector3(center.x+(alongX?door:0),height-.65f,center.y+(alongX?0:door)),
                    alongX?new Vector3(gap,1.3f,1.65f):new Vector3(1.65f,1.3f,gap),stone,true);
            }
            if(edge<length*.5f) Wall(name,center+(alongX?Vector2.right:Vector2.up)*((edge+length*.5f)*.5f),
                alongX?new Vector2(length*.5f-edge,1.5f):new Vector2(1.5f,length*.5f-edge),height);
        }

        void Room(string name,Vector2 center,Vector2 size,float height=8,bool roof=true)
        {
            DoorWall(name+" front",center+Vector2.down*size.y*.5f,size.x,true,height,6.5f,0);
            DoorWall(name+" rear",center+Vector2.up*size.y*.5f,size.x,true,height,6.5f,0);
            DoorWall(name+" west",center+Vector2.left*size.x*.5f,size.y,false,height,5.5f,0);
            DoorWall(name+" east",center+Vector2.right*size.x*.5f,size.y,false,height,5.5f,0);
            if(roof)
            {
                if(gableRoof==null) gableRoof=CreateGableRoof();
                Shape(name+" roof",PrimitiveType.Cube,arenaRoot,new Vector3(center.x,height+1.45f,center.y),new Vector3(size.x+1,2.5f,size.y+1),roofTile,true).GetComponent<MeshFilter>().sharedMesh=gableRoof;
                Shape("Roof ridge",PrimitiveType.Cube,arenaRoot,new Vector3(center.x,height+2.8f,center.y),new Vector3(.4f,.2f,size.y+1.5f),trim,false);
            }
            Floor(name+" paving",center,size,stone);
            routeCheckpoints.Add(new Vector3(center.x,0,center.y));
        }

        void Floor(string name,Vector2 center,Vector2 size,Material material)
            => Shape(name,PrimitiveType.Cube,arenaRoot,new Vector3(center.x,.008f,center.y),new Vector3(size.x,.012f,size.y),material,false);

        void Pillar(string name,Vector2 center,float radius=1.4f,float height=9,Material material=null)
        {
            Shape(name,PrimitiveType.Cylinder,arenaRoot,new Vector3(center.x,height/2,center.y),new Vector3(radius*2,height/2,radius*2),material!=null?material:stone,true);
            coverFootprints.Add(new Bounds(new Vector3(center.x,height/2,center.y),new Vector3(radius*2,height,radius*2)));
            foreach(float y in new[]{.3f,height-.25f}) Shape("Pillar capital",PrimitiveType.Cylinder,arenaRoot,new Vector3(center.x,y,center.y),new Vector3(radius*2.45f,.3f,radius*2.45f),trim,false);
        }

        void BuildCitadel()
        {
            float x=ArenaSize.x*.34f,z=ArenaSize.y*.34f;
            foreach(int side in new[]{-1,1})
            {
                DoorWall("Outer curtain",new Vector2(0,side*z),x*2,true,9,9,-x*.52f,x*.52f);
                DoorWall("Outer curtain",new Vector2(side*x,0),z*2,false,9,9,0);
                DoorWall("Inner curtain",new Vector2(0,side*z*.47f),x*1.12f,true,8,8,0);
                for(int end=-1;end<=1;end+=2) Pillar("Citadel tower",new Vector2(side*x,end*z),3.8f,13);
                Room("Barracks",new Vector2(side*x*.7f,0),new Vector2(18,24),8.5f);
                Room("Gatehouse",new Vector2(side*x*.48f,side*z*.75f),new Vector2(18,18),9);
            }
            for(int i=-1;i<=1;i+=2) Room("Command hall",new Vector2(0,i*z*.78f),new Vector2(22,15),9);
            Pillar("Siege monument",Vector2.zero,2.8f,11);
            Floor("Citadel road",Vector2.zero,new Vector2(10,ArenaSize.y-6),stone);
            for(int i=-2;i<=2;i++) Wall("Training barricade",new Vector2(i*19,-z-14),new Vector2(8,2),7);
        }

        void BuildLabyrinth()
        {
            const int columns=9,rows=11;
            float cell=Mathf.Min((ArenaSize.x-22)/columns,(ArenaSize.y-22)/rows);
            Vector2 origin=new Vector2(-columns*cell/2,-rows*cell/2);
            var east=new bool[columns,rows]; var north=new bool[columns,rows];
            var visited=new bool[columns,rows]; var stack=new Stack<Vector2Int>();
            var random=new System.Random(21763); stack.Push(Vector2Int.zero); visited[0,0]=true;
            var directions=new[]{Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down};
            while(stack.Count>0)
            {
                var p=stack.Peek(); var choices=new List<Vector2Int>();
                foreach(var d in directions) { var n=p+d; if(n.x>=0 && n.x<columns && n.y>=0 && n.y<rows && !visited[n.x,n.y]) choices.Add(d); }
                if(choices.Count==0) { stack.Pop(); continue; }
                var delta=choices[random.Next(choices.Count)]; var next=p+delta;
                if(delta.x!=0) east[Mathf.Min(p.x,next.x),p.y]=true; else north[p.x,Mathf.Min(p.y,next.y)]=true;
                visited[next.x,next.y]=true; stack.Push(next);
            }
            for(int x=0;x<columns;x++) for(int z=0;z<rows;z++)
            {
                // Loops create alternative routes; the spanning tree keeps every chamber connected.
                if(x<columns-1 && random.NextDouble()<.17) east[x,z]=true;
                if(z<rows-1 && random.NextDouble()<.17) north[x,z]=true;
                Vector2 p=origin+new Vector2((x+.5f)*cell,(z+.5f)*cell);
                routeCheckpoints.Add(new Vector3(p.x,0,p.y));
                if(x<columns-1 && !east[x,z]) Wall("Labyrinth cross wall",p+Vector2.right*cell/2,new Vector2(1.4f,cell+1.4f),8);
                if(z<rows-1 && !north[x,z]) Wall("Labyrinth turn wall",p+Vector2.up*cell/2,new Vector2(cell+1.4f,1.4f),8);
                if((x+z)%7==0) Floor("Chamber mosaic",p,Vector2.one*(cell-2),roofTile);
            }
            DoorWall("Maze outer wall",new Vector2(0,origin.y),columns*cell,true,9,cell,0);
            DoorWall("Maze outer wall",new Vector2(0,-origin.y),columns*cell,true,9,cell,0);
            DoorWall("Maze outer wall",new Vector2(origin.x,0),rows*cell,false,9,cell,0);
            DoorWall("Maze outer wall",new Vector2(-origin.x,0),rows*cell,false,9,cell,0);
            for(int i=0;i<4;i++) Pillar("Entrance obelisk",new Vector2((i<2?1:-1)*(columns*cell/2+3),(i%2==0?1:-1)*cell/2),1,11);
        }

        void BuildIronworks()
        {
            float x=ArenaSize.x*.3f,z=ArenaSize.y*.2f;
            foreach(int side in new[]{-1,1})
            {
                DoorWall("Foundry divider",new Vector2(0,side*z),ArenaSize.x-18,true,9,10,-x,0,x);
                Gate(new Vector2(0,side*z),9,8,false,side<0?0:8);
                for(int i=-1;i<=1;i++)
                {
                    Vector2 p=new Vector2(i*x,side*z*1.68f);
                    Pillar("Riveted furnace",p,4.1f,11,factoryIron);
                    Shape("Furnace chimney",PrimitiveType.Cylinder,arenaRoot,new Vector3(p.x,14,p.y),new Vector3(3,4,3),factoryIron,false);
                    Shape("Hot furnace mouth",PrimitiveType.Cube,arenaRoot,new Vector3(p.x,3,p.y-4.13f),new Vector3(2,3,.04f),visor,false);
                }
                Room("Machine house",new Vector2(side*x,0),new Vector2(22,28),9);
                for(int i=-2;i<=2;i++) Wall("Iron stockpile",new Vector2(i*23,side*15),new Vector2(9,4),7.5f,factoryIron);
            }
            for(int i=-1;i<=1;i++)
            {
                Vector3 p=new Vector3(i*x,12,0);
                Shape("Overhead steam main",PrimitiveType.Cylinder,arenaRoot,p,new Vector3(1.6f,ArenaSize.y*.43f,1.6f),factoryIron,false).transform.localRotation=Quaternion.Euler(90,0,0);
                for(int s=-1;s<=1;s+=2) Pillar("Pipe gantry",new Vector2(i*x+5,s*24),.85f,12,factoryIron);
            }
            Floor("Foundry service lane",Vector2.zero,new Vector2(9,ArenaSize.y-4),roofTile);
        }

        void Rock(Vector2 p,Vector2 size,float height)
        {
            if(cragMesh==null) cragMesh=CreateCrag();
            var rock=new GameObject("Fractured canyon crag",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
            rock.transform.SetParent(arenaRoot,false); rock.transform.localPosition=new Vector3(p.x,0,p.y);
            rock.transform.localScale=new Vector3(size.x,height,size.y);
            rock.GetComponent<MeshFilter>().sharedMesh=cragMesh; rock.GetComponent<MeshCollider>().sharedMesh=cragMesh;
            rock.GetComponent<MeshRenderer>().sharedMaterial=sandstone;
            // Exclude the crag's interior and undercuts from navigation, preventing tiny
            // disconnected walkable pockets beneath the irregular overhangs.
            var solid=rock.AddComponent<NavMeshModifierVolume>(); solid.center=new Vector3(0,.54f,0);
            solid.size=new Vector3(1.14f,1.08f,1.14f); solid.area=NavMesh.GetAreaFromName("Not Walkable");
            coverFootprints.Add(new Bounds(new Vector3(p.x,height*.54f,p.y),new Vector3(size.x*1.14f,height*1.08f,size.y*1.14f)));
        }

        void BuildCanyon()
        {
            float x=ArenaSize.x*.34f;
            for(int row=-3;row<=3;row++)
            {
                float z=row*ArenaSize.y*.115f;
                float shift=Mathf.Sin(row*1.8f)*10;
                Rock(new Vector2(-x+shift,z),new Vector2(20,19),12+(row+3)%3*2);
                Rock(new Vector2(x+shift,z),new Vector2(20,19),12+(row+2)%3*2);
                if(row%2==0) Rock(new Vector2((row%4==0?-1:1)*13,z+5),new Vector2(22,8),9);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Room("Canyon outpost",new Vector2(side*ArenaSize.x*.37f,side*ArenaSize.y*.39f),new Vector2(19,18),8);
                Pillar("Stone arch pier",new Vector2(side*15,0),3.1f,13,sandstone);
            }
            Shape("Natural stone arch",PrimitiveType.Cube,arenaRoot,new Vector3(0,13,0),new Vector3(34,4,7),sandstone,true);
            Floor("Dry riverbed",Vector2.zero,new Vector2(12,ArenaSize.y-4),sandstone);
        }

        void Cargo(Vector2 p,Vector2 size,int variant)
        {
            Wall("Stacked cargo",p,size,7.2f,variant%2==0?timber:factoryIron);
            for(int y=1;y<=6;y+=2) for(int side=-1;side<=1;side+=2)
                Shape("Cargo iron band",PrimitiveType.Cube,arenaRoot,new Vector3(p.x+side*size.x*.32f,y,p.y),new Vector3(.14f,1.9f,size.y+.08f),trim,false);
        }

        void BuildHarbor()
        {
            float x=ArenaSize.x*.29f,z=ArenaSize.y*.28f;
            for(int side=-1;side<=1;side+=2)
            {
                for(int row=-1;row<=1;row++) Room("Quay warehouse",new Vector2(side*x,row*z),new Vector2(24,29),9);
                for(int row=-2;row<=2;row++) Cargo(new Vector2(side*15,row*31+(side<0?7:-7)),new Vector2(9,13),row);
                DoorWall("Dock flood wall",new Vector2(0,side*ArenaSize.y*.40f),ArenaSize.x-10,true,8,10,-x,0,x);
                Gate(new Vector2(0,side*ArenaSize.y*.40f),9,7.5f,false,side<0?4:12);
                Floor("Quay water channel",new Vector2(side*(ArenaSize.x*.5f-7),0),new Vector2(8,ArenaSize.y-8),water);
            }
            Floor("Wharf cobbles",Vector2.zero,new Vector2(10,ArenaSize.y-5),stone);
            for(int i=-2;i<=2;i++)
            {
                Pillar("Dock crane mast",new Vector2((i%2==0?-1:1)*33,i*30),1,14,timber);
                Shape("Crane boom",PrimitiveType.Cube,arenaRoot,new Vector3((i%2==0?-1:1)*27,14,i*30),new Vector3(15,.65f,.65f),timber,false);
            }
        }

        void BuildCatacombs()
        {
            int columns=5,rows=6;
            float sx=(ArenaSize.x-22)/columns,sz=(ArenaSize.y-22)/rows;
            for(int x=0;x<=columns;x++) DoorWall("Crypt gallery wall",new Vector2((x-columns*.5f)*sx,0),sz*rows,false,9.8f,7,
                -sz*2.5f,-sz*1.5f,-sz*.5f,sz*.5f,sz*1.5f,sz*2.5f);
            for(int z=0;z<=rows;z++) DoorWall("Crypt gallery wall",new Vector2(0,(z-rows*.5f)*sz),sx*columns,true,9.8f,7,
                -sx*2,-sx,0,sx,sx*2);
            for(int x=0;x<columns;x++) for(int z=0;z<rows;z++)
            {
                Vector2 p=new Vector2((x+.5f-columns*.5f)*sx,(z+.5f-rows*.5f)*sz);
                routeCheckpoints.Add(new Vector3(p.x,0,p.y));
                Floor("Crypt floor",p,new Vector2(sx-1,sz-1),roofTile);
                foreach(int side in new[]{-1,1}) Pillar("Vault column",p+new Vector2(side*sx*.28f,side*sz*.27f),1.2f,9.8f);
                if((x+z)%3==0)
                {
                    Shape("Amber crypt lantern",PrimitiveType.Sphere,arenaRoot,new Vector3(p.x,5.3f,p.y),Vector3.one*.5f,visor,false);
                    if((x+z)%2==0) { var lamp=new GameObject("Crypt lamplight",typeof(Light)); lamp.transform.SetParent(arenaRoot,false); lamp.transform.localPosition=new Vector3(p.x,6,p.y);
                        var light=lamp.GetComponent<Light>(); light.type=LightType.Point; light.color=new Color(1,.65f,.31f); light.intensity=5; light.range=27; light.shadows=LightShadows.None; }
                }
            }
            Shape("Catacomb stone ceiling",PrimitiveType.Cube,arenaRoot,new Vector3(0,10.7f,0),new Vector3(ArenaSize.x-3,1.4f,ArenaSize.y-3),stone,true);
        }

        void BuildBazaar()
        {
            float sx=ArenaSize.x*.19f,sz=ArenaSize.y*.17f;
            for(int x=-2;x<=2;x++) for(int z=-2;z<=2;z++)
            {
                if(x==0 && Mathf.Abs(z)<=1) continue;
                Vector2 p=new Vector2(x*sx,z*sz);
                Room("Merchant house",p,new Vector2(19+(z%2==0?2:0),21),8+(x+z+4)%3);
                var awning=Shape("Market awning",PrimitiveType.Cube,arenaRoot,new Vector3(p.x,5.7f,p.y-13),new Vector3(14,.18f,5),
                    (x+z)%2==0?blue:red,false); awning.transform.localRotation=Quaternion.Euler(8,0,0);
                foreach(int side in new[]{-1,1}) Shape("Awning post",PrimitiveType.Cylinder,arenaRoot,new Vector3(p.x+side*6.5f,2.85f,p.y-15),new Vector3(.24f,2.85f,.24f),timber,false);
            }
            Pillar("Market clock spire",Vector2.zero,3,15);
            foreach(int side in new[]{-1,1})
                for(int i=-1;i<=1;i++) Cargo(new Vector2(side*9,i*22+10),new Vector2(5,6),i);
            Floor("Grand market street",Vector2.zero,new Vector2(9,ArenaSize.y-5),stone);
        }

        void BuildSanctum()
        {
            for(int ring=0;ring<3;ring++)
            {
                float x=ArenaSize.x*(.15f+ring*.12f),z=ArenaSize.y*(.15f+ring*.12f);
                foreach(int side in new[]{-1,1})
                {
                    DoorWall("Temple curtain",new Vector2(0,side*z),x*2,true,9,9,(ring%2==0?1:-1)*x*.48f);
                    DoorWall("Temple curtain",new Vector2(side*x,0),z*2,false,9,9,(ring%2==0?-1:1)*z*.48f);
                    for(int end=-1;end<=1;end+=2) Pillar("Temple column",new Vector2(side*x,end*z),1.6f,12);
                }
            }
            for(int i=-3;i<=3;i++) foreach(int side in new[]{-1,1})
                Wall("Dense thorn hedge",new Vector2(i*17,side*ArenaSize.y*.455f),new Vector2(10,4),7.4f,foliage);
            Pillar("Sanctum altar",Vector2.zero,3,10,trim);
            Shape("Sanctum crown",PrimitiveType.Sphere,arenaRoot,new Vector3(0,11,0),new Vector3(6,3,6),healer,false);
            Floor("Sacred mosaic",Vector2.zero,new Vector2(30,30),roofTile);
        }

        void Gate(Vector2 p,float width,float height,bool rotated,float phase)
        {
            var root=new GameObject("Cycling blast gate"); root.transform.SetParent(arenaRoot,false);
            root.transform.localPosition=new Vector3(p.x,0,p.y); root.transform.localRotation=Quaternion.Euler(0,rotated?90:0,0);
            var body=Shape("Moving solid gate",PrimitiveType.Cube,root.transform,new Vector3(0,height*.5f,0),new Vector3(width,height,1.2f),factoryIron,true);
            for(int side=-1;side<=1;side+=2) Shape("Gate runner",PrimitiveType.Cube,root.transform,new Vector3(side*(width*.5f+.4f),height+1,0),new Vector3(.65f,height*2+2,1.8f),armor,false);
            Shape("Gate crosshead",PrimitiveType.Cube,root.transform,new Vector3(0,height*2+1.5f,0),new Vector3(width+1.6f,1,1.8f),armor,false);
            var beacon=Shape("Gate signal",PrimitiveType.Sphere,root.transform,new Vector3(width*.5f+1,3,-1),Vector3.one*.6f,visor,false).GetComponent<Renderer>();
            var gate=root.AddComponent<BattleMovingObstacle>(); gate.Initialize(body.transform,beacon,height+1,phase);
            movingObstacles.Add(gate);
        }

        void BatchMapGeometry()
        {
            // Preserve all collision, but combine static visual pieces into a handful of material batches.
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var renderer in arenaRoot.GetComponentsInChildren<MeshRenderer>())
            {
                if(renderer.GetComponentInParent<BattleMovingObstacle>()!=null) continue;
                var filter=renderer.GetComponent<MeshFilter>(); if(filter==null || filter.sharedMesh==null) continue;
                if(!groups.TryGetValue(renderer.sharedMaterial,out var list)) groups[renderer.sharedMaterial]=list=new List<CombineInstance>();
                list.Add(new CombineInstance {mesh=filter.sharedMesh,transform=arenaRoot.worldToLocalMatrix*filter.transform.localToWorldMatrix}); renderer.enabled=false;
            }
            foreach(var group in groups)
            {
                var mesh=new Mesh {name="Batched map masonry",indexFormat=IndexFormat.UInt32}; mesh.CombineMeshes(group.Value.ToArray(),true,true); mapMeshes.Add(mesh);
                if(group.Key!=ground)
                {
                    var vertices=mesh.vertices; var normals=mesh.normals; var uv=new Vector2[vertices.Length];
                    for(int i=0;i<uv.Length;i++)
                    {
                        Vector3 p=vertices[i],n=normals[i];
                        uv[i]=(Mathf.Abs(n.y)>.65f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>Mathf.Abs(n.z)?new Vector2(p.z,p.y):new Vector2(p.x,p.y))/3f;
                    }
                    mesh.uv=uv; mesh.RecalculateTangents();
                }
                var batch=new GameObject("Map geometry / "+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer)); batch.transform.SetParent(arenaRoot,false);
                batch.GetComponent<MeshFilter>().sharedMesh=mesh; batch.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
        }
        static Mesh CreateGableRoof()
        {
            var v=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(0,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(0,.5f,.5f)};
            var order=new[]{0,2,1,3,4,5,0,3,5,0,5,2,1,2,5,1,5,4,0,1,4,0,4,3};
            var vertices=new Vector3[order.Length]; var indices=new int[order.Length];
            for(int i=0;i<order.Length;i++) { vertices[i]=v[order[i]]; indices[i]=i; }
            var mesh=new Mesh {name="Pitched masonry roof"}; mesh.vertices=vertices; mesh.triangles=indices;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh CreateCrag()
        {
            const int sides=9;
            var rings=new Vector3[sides*4];
            var radii=new[]{.53f,.55f,.43f,.26f}; var levels=new[]{0,.18f,.73f,.93f};
            for(int ring=0;ring<4;ring++) for(int s=0;s<sides;s++)
            {
                float a=s*Mathf.PI*2/sides;
                float noise=.88f+.12f*Mathf.Sin(s*7.3f+ring*.8f);
                rings[ring*sides+s]=new Vector3(Mathf.Cos(a)*radii[ring]*noise,levels[ring]+(ring==0?0:Mathf.Sin(s*4.7f)*.035f),Mathf.Sin(a)*radii[ring]*noise);
            }
            var vertices=new List<Vector3>();
            for(int ring=0;ring<3;ring++) for(int s=0;s<sides;s++)
            {
                int next=(s+1)%sides; Vector3 a=rings[ring*sides+s],b=rings[ring*sides+next],c=rings[(ring+1)*sides+s],d=rings[(ring+1)*sides+next];
                vertices.Add(a); vertices.Add(c); vertices.Add(d); vertices.Add(a); vertices.Add(d); vertices.Add(b);
            }
            for(int s=0;s<sides;s++)
            {
                int next=(s+1)%sides;
                vertices.Add(rings[3*sides+s]); vertices.Add(new Vector3(.04f,1.08f,-.03f)); vertices.Add(rings[3*sides+next]);
                vertices.Add(rings[s]); vertices.Add(rings[next]); vertices.Add(Vector3.zero);
            }
            var indices=new int[vertices.Count]; for(int i=0;i<indices.Length;i++) indices[i]=i;
            var mesh=new Mesh {name="Faceted stratified crag"}; mesh.SetVertices(vertices); mesh.triangles=indices;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        void ReleaseMapMeshes() { foreach(var mesh in mapMeshes) if(mesh!=null) Destroy(mesh); mapMeshes.Clear(); routeCheckpoints.Clear(); }
    }
}
