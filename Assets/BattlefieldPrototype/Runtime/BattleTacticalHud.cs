using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed class BattleTacticalHud : MonoBehaviour
    {
        BattleDirector director;
        PossessionController player;
        Canvas canvas;
        Material material;
        BattleOrders orders;
        BattleAudio audio;
        RectTransform panel,markerLayer,hitMarker,targetFrame;
        RectTransform healthPanel,controlsPanel;
        BattleMinimapGraphic map;
        Text title,legend,orderText;
        readonly Button[] commandButtons=new Button[5];
        readonly List<Badge> badges=new List<Badge>();
        readonly List<BattleUnit> visible=new List<BattleUnit>();
        float nextRefresh,hitUntil;
        bool lastWorld,triggerHeld;
        sealed class Badge { public RectTransform Root; public BattleUiEmblem Symbol; public Image Health; public RectTransform Track; public BattleUnit Unit; }
        public BattleMinimapGraphic Minimap=>map;
        public void Initialize(BattleDirector battle,PossessionController controller,Canvas owner,Material overlay)
        {
            director=battle; player=controller; canvas=owner; material=overlay;
            orders=battle.GetComponent<BattleOrders>(); audio=battle.GetComponent<BattleAudio>();
            panel=BattleUiStyle.Box(owner.transform,"Live tactical map",new Color(.035f,.055f,.065f,.9f),material).rectTransform;
            BattleUiStyle.Frame(panel,material);
            title=BattleUiStyle.Label(panel,"Battlefield name","",16,material);
            var obj=new GameObject("All combatants",typeof(RectTransform),typeof(BattleMinimapGraphic)); obj.transform.SetParent(panel,false);
            map=obj.GetComponent<BattleMinimapGraphic>(); map.Director=director; map.Terrain=battle.GetComponent<BattlefieldBootstrap>(); map.Player=player;
            map.material=material; map.raycastTarget=false;
            legend=BattleUiStyle.Label(panel,"Team counts","",14,material,TextAnchor.MiddleCenter);
            orderText=BattleUiStyle.Label(panel,"Current order","",15,material,TextAnchor.MiddleCenter);
            commandButtons[0]=BattleUiStyle.Button(panel,"Advance","ADV Z",material,()=>Issue(BattleOrder.Advance));
            commandButtons[1]=BattleUiStyle.Button(panel,"Hold","HOLD X",material,()=>Issue(BattleOrder.Hold));
            commandButtons[2]=BattleUiStyle.Button(panel,"Protect","GUARD V",material,()=>Issue(BattleOrder.Protect));
            commandButtons[3]=BattleUiStyle.Button(panel,"Sound","SOUND ON [M]",material,()=>audio?.ToggleMute());
            commandButtons[4]=BattleUiStyle.Button(panel,"Flare","FLARE AT YOUR FEET [B] · 1",material,()=>player.TryUseFlare());
            markerLayer=new GameObject("World class markers",typeof(RectTransform)).GetComponent<RectTransform>(); markerLayer.SetParent(owner.transform,false); Stretch(markerLayer);
            markerLayer.SetAsFirstSibling();
            healthPanel=owner.transform.Find("Status Panel") as RectTransform;
            controlsPanel=owner.transform.Find("Panel") as RectTransform;
            hitMarker=new GameObject("Hit confirmation",typeof(RectTransform)).GetComponent<RectTransform>(); hitMarker.SetParent(owner.transform,false);
            hitMarker.anchorMin=hitMarker.anchorMax=hitMarker.pivot=Vector2.one*.5f; hitMarker.sizeDelta=Vector2.one*30;
            for(int i=0;i<4;i++)
            {
                var stroke=BattleUiStyle.Box(hitMarker,"Hit stroke",BattleUiStyle.Accent,material).rectTransform;
                stroke.anchorMin=stroke.anchorMax=stroke.pivot=Vector2.one*.5f; stroke.sizeDelta=new Vector2(9,2);
                float angle=45+i*90; stroke.localRotation=Quaternion.Euler(0,0,angle);
                stroke.anchoredPosition=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*9;
            }
            var bracket=new GameObject("Aim target highlight",typeof(RectTransform),typeof(BattleTargetBracket)); bracket.transform.SetParent(markerLayer,false);
            targetFrame=(RectTransform)bracket.transform; targetFrame.anchorMin=targetFrame.anchorMax=targetFrame.pivot=Vector2.one*.5f;
            var graphic=bracket.GetComponent<BattleTargetBracket>(); graphic.color=BattleUiStyle.Accent; graphic.material=material; graphic.raycastTarget=false;
            director.UnitDamaged+=OnDamage;
            RebuildLayout(owner.renderMode==RenderMode.WorldSpace);
        }
        void Issue(BattleOrder order)
        {
            if(orders==null) return;
            orders.IssueForPlayer(order,player);
        }
        public void RebuildLayout(bool world)
        {
            if(panel==null) return;
            lastWorld=world; float w=world?300:250, h=world?455:397;
            panel.anchorMin=panel.anchorMax=panel.pivot=Vector2.one; panel.anchoredPosition=new Vector2(-12,-12); panel.sizeDelta=new Vector2(w,h);
            BattleUiStyle.Place(title.rectTransform,12,9,w-24,25); title.fontSize=world?20:16;
            BattleUiStyle.Place(map.rectTransform,10,37,w-20,world?240:200);
            float y=world?280:240;
            BattleUiStyle.Place(legend.rectTransform,10,y,w-20,25); legend.fontSize=world?18:14;
            BattleUiStyle.Place(orderText.rectTransform,10,y+29,w-20,25); orderText.color=BattleUiStyle.Accent; orderText.fontSize=world?20:15;
            float width=(w-36)/3;
            for(int i=0;i<3;i++)
            { BattleUiStyle.Place((RectTransform)commandButtons[i].transform,10+i*(width+8),y+61,width,world?35:28); commandButtons[i].GetComponentInChildren<Text>().fontSize=world?18:14; }
            BattleUiStyle.Place((RectTransform)commandButtons[4].transform,10,y+(world?105:95),w-20,world?32:28);
            commandButtons[4].GetComponentInChildren<Text>().fontSize=world?17:12;
            BattleUiStyle.Place((RectTransform)commandButtons[3].transform,10,y+(world?145:131),w-20,24);
            commandButtons[3].GetComponentInChildren<Text>().fontSize=world?18:14;
        }
        void OnDamage(BattleUnit target,BattleUnit source,float amount)
        { if(source!=null && source==player.CurrentUnit && target.Team!=source.Team) hitUntil=Time.unscaledTime+.14f; }
        void LateUpdate()
        {
            if(director==null || player==null) return;
            bool active=director.IsBattleRunning;
            panel.gameObject.SetActive(active); markerLayer.gameObject.SetActive(active); hitMarker.gameObject.SetActive(active && Time.unscaledTime<hitUntil);
            if(!active) return;
            bool world=canvas.renderMode==RenderMode.WorldSpace;
            if(world!=lastWorld) RebuildLayout(world);
            if(hitMarker.gameObject.activeSelf) PositionAtScreen(hitMarker,player.AimScreenPosition);
            foreach(var badge in badges) if(badge.Root.gameObject.activeSelf && badge.Unit!=null && badge.Unit.IsAlive) PositionBadge(badge);
            map.SetVerticesDirty();
            if(Time.unscaledTime<nextRefresh) return;
            nextRefresh=Time.unscaledTime+.1f;
            title.text=map.Terrain!=null?map.Terrain.MapName:"TACTICAL MAP";
            if (player.HasLimitedSight)
            {
                int allies = 0, foes = 0;
                foreach (var other in director.Units) if (player.CanSeeUnit(other)) { if (other.Team == player.CurrentUnit.Team) allies++; else foes++; }
                legend.text=$"SIGHT {player.ViewSightRange:0}m   /   SEEN {allies} ALLY · {foes} FOE";
            }
            else legend.text=$"<color=#4CC8FF>BLUE {director.AliveBlue}</color>   /   <color=#FF6040>RED {director.AliveRed}</color>   /   N ↑";
            orderText.text=orders!=null?orders.Description:"ADVANCE";
            commandButtons[3].GetComponentInChildren<Text>().text=audio!=null && audio.Muted?"SOUND OFF [M]":"SOUND ON [M]";
            bool flareReady = director.Flares.HasCharge(BattleTeam.Blue);
            commandButtons[4].GetComponentInChildren<Text>().text=flareReady?"FLARE AT YOUR FEET [B] · 1":"FLARE USED · 0";
            commandButtons[4].interactable=flareReady && player.CurrentUnit!=null && player.CurrentUnit.IsAlive && player.CurrentUnit.Team==BattleTeam.Blue;
            for(int i=0;i<3;i++) commandButtons[i].image.color=orders!=null && (int)orders.Current==i?new Color(.33f,.27f,.15f):new Color(.12f,.15f,.16f);
            UpdateBadges();
        }
        void UpdateBadges()
        {
            visible.Clear(); var camera=player.ViewCamera; Vector3 eye=camera.transform.position;
            foreach(var unit in director.Units)
            {
                if(unit==null || !unit.IsAlive || unit==player.CurrentUnit || !player.CanSeeUnit(unit) ||
                    (unit.transform.position-eye).sqrMagnitude>Mathf.Min(80,player.ViewSightRange)*Mathf.Min(80,player.ViewSightRange)) continue;
                Vector3 head=unit.transform.position+Vector3.up*(unit.Definition.BodyHeight+.25f), viewport=camera.WorldToViewportPoint(head);
                if(viewport.z<=0 || viewport.x<0 || viewport.x>1 || viewport.y<0 || viewport.y>1) continue;
                Vector2 screen=camera.WorldToScreenPoint(head);
                Camera eventCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:camera;
                if(RectTransformUtility.RectangleContainsScreenPoint(panel,screen,eventCamera) ||
                    healthPanel!=null && RectTransformUtility.RectangleContainsScreenPoint(healthPanel,screen,eventCamera) ||
                    controlsPanel!=null && RectTransformUtility.RectangleContainsScreenPoint(controlsPanel,screen,eventCamera)) continue;
                if(Physics.Linecast(eye,head,out var hit,~0,QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<BattleUnit>()!=unit) continue;
                visible.Add(unit);
            }
            visible.Sort((a,b)=>(a.transform.position-eye).sqrMagnitude.CompareTo((b.transform.position-eye).sqrMagnitude));
            int count=Mathf.Min(40,visible.Count);
            while(badges.Count<count) badges.Add(CreateBadge());
            for(int i=0;i<badges.Count;i++)
            {
                var badge=badges[i]; badge.Root.gameObject.SetActive(i<count); if(i>=count) continue;
                var unit=visible[i]; badge.Symbol.Role=unit.Definition.Role;
                badge.Unit=unit;
                badge.Symbol.color=unit==player.AimedUnit?BattleUiStyle.Accent:unit.Team==BattleTeam.Blue?BattleUiStyle.Friendly:BattleUiStyle.Enemy;
                badge.Symbol.SetVerticesDirty();
                PositionBadge(badge);
                badge.Track.gameObject.SetActive(unit.HealthFraction<.999f || Time.time-unit.LastDamagedAt<4 || unit==player.AimedUnit);
                badge.Health.color=unit.Team==BattleTeam.Blue?BattleUiStyle.Friendly:BattleUiStyle.Enemy;
                badge.Health.rectTransform.sizeDelta=new Vector2(34*unit.HealthFraction,4);
            }
            var aimed=player.AimedUnit;
            bool showTarget = aimed!=null && aimed.IsAlive && player.CanSeeUnit(aimed);
            targetFrame.gameObject.SetActive(showTarget);
            if(showTarget)
            {
                if(canvas.renderMode==RenderMode.WorldSpace)
                {
                    targetFrame.position=aimed.transform.position+Vector3.up*aimed.Definition.BodyHeight*.5f;
                    targetFrame.rotation=camera.transform.rotation;
                    targetFrame.localScale=Vector3.one/Mathf.Max(.0001f,markerLayer.lossyScale.x);
                    targetFrame.sizeDelta=new Vector2(aimed.Definition.BodyRadius*2+.2f,aimed.Definition.BodyHeight+.2f);
                    targetFrame.SetAsLastSibling(); return;
                }
                targetFrame.localScale=Vector3.one; targetFrame.localRotation=Quaternion.identity;
                Vector3 top=camera.WorldToScreenPoint(aimed.transform.position+Vector3.up*aimed.Definition.BodyHeight), bottom=camera.WorldToScreenPoint(aimed.transform.position);
                if(top.z>0 && bottom.z>0 && PositionAtScreen(targetFrame,(top+bottom)*.5f))
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,top,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:camera,out var a);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,bottom,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:camera,out var b);
                    float height=Mathf.Abs(a.y-b.y); targetFrame.sizeDelta=new Vector2(Mathf.Max(25,height*aimed.Definition.BodyRadius*2/aimed.Definition.BodyHeight+12),height+12);
                }
                else targetFrame.gameObject.SetActive(false);
            }
            targetFrame.SetAsLastSibling();
        }
        void PositionBadge(Badge badge)
        {
            Vector3 head=badge.Unit.transform.position+Vector3.up*(badge.Unit.Definition.BodyHeight+.25f);
            var camera=player.ViewCamera;
            if(canvas.renderMode==RenderMode.WorldSpace)
            {
                badge.Root.position=head; badge.Root.rotation=camera.transform.rotation;
                float scale=Mathf.Clamp(Vector3.Distance(head,camera.transform.position)*.0008f,.004f,.03f);
                badge.Root.localScale=Vector3.one*scale/Mathf.Max(.0001f,markerLayer.lossyScale.x);
            }
            else
            {
                badge.Root.localScale=Vector3.one; badge.Root.localRotation=Quaternion.identity;
                PositionAtScreen(badge.Root,camera.WorldToScreenPoint(head));
            }
        }
        Badge CreateBadge()
        {
            var root=BattleUiStyle.Box(markerLayer,"Unit symbol",new Color(.02f,.03f,.04f,.6f),material).rectTransform;
            root.anchorMin=root.anchorMax=Vector2.one*.5f; root.pivot=new Vector2(.5f,0); root.sizeDelta=new Vector2(36,34);
            var emblem=BattleUiStyle.Emblem(root,"Class",UnitRole.Vanguard,Color.white,material); BattleUiStyle.Place(emblem,8,2,20,20);
            var track=BattleUiStyle.Box(root,"Health track",new Color(.08f,.1f,.1f),material); BattleUiStyle.Place(track.rectTransform,1,26,34,4);
            var health=BattleUiStyle.Box(track.transform,"Health",Color.white,material); BattleUiStyle.Place(health.rectTransform,0,0,34,4);
            return new Badge {Root=root,Symbol=emblem.GetComponent<BattleUiEmblem>(),Health=health,Track=track.rectTransform};
        }
        bool PositionAtScreen(RectTransform rect,Vector2 screen)
        {
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,screen,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:player.ViewCamera,out var local)) return false;
            rect.anchoredPosition=local; return true;
        }
        public bool TryControllerPress(Ray ray,bool held)
        {
            bool wasHeld=triggerHeld; triggerHeld=held;
            if(!director.IsBattleRunning || canvas.renderMode!=RenderMode.WorldSpace || !held) return false;
            var plane=new Plane(canvas.transform.forward,canvas.transform.position);
            if(!plane.Raycast(ray,out float distance) || distance<0) return false;
            Vector3 point=ray.GetPoint(distance);
            foreach(var button in commandButtons)
            {
                var rect=(RectTransform)button.transform;
                if(!rect.rect.Contains(rect.InverseTransformPoint(point))) continue;
                if(!wasHeld && button.interactable) button.onClick.Invoke();
                return true;
            }
            return false;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
        void OnDestroy() { if(director!=null) director.UnitDamaged-=OnDamage; }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleTargetBracket : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r=GetPixelAdjustedRect();
            float length=Mathf.Min(8,Mathf.Min(r.width,r.height)*.24f), width=Mathf.Min(1.6f,Mathf.Min(r.width,r.height)*.035f);
            for(int x=0;x<2;x++) for(int y=0;y<2;y++)
            {
                Vector2 p=new Vector2(x==0?r.xMin:r.xMax,y==0?r.yMin:r.yMax);
                BattleMinimapGraphic.Line(vh,p,p+Vector2.right*(x==0?length:-length),width,color);
                BattleMinimapGraphic.Line(vh,p,p+Vector2.up*(y==0?length:-length),width,color);
            }
        }
    }
}
