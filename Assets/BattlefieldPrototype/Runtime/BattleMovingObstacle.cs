using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    /// <summary>Solid cycling gate with navigation carving and an occupied-doorway interlock.</summary>
    public sealed class BattleMovingObstacle : MonoBehaviour
    {
        Transform panel;
        Renderer signal;
        Vector3 closed;
        float travel,phase;
        BattleDirector director;
        NavMeshObstacle obstacle;
        bool navigationReady;
        readonly Collider[] occupants=new Collider[64];
        MaterialPropertyBlock properties;
        public float OpenAmount { get; private set; } = 1;
        public bool IsPassable => OpenAmount*travel>3.3f;
        public Bounds ClosedBounds { get; private set; }
        public Bounds CurrentBounds => panel!=null?panel.GetComponent<Collider>().bounds:ClosedBounds;
        public bool WaitingForClearance { get; private set; }

        public void Initialize(Transform body,Renderer beacon,float lift,float offset)
        {
            panel=body; signal=beacon; closed=body.localPosition; travel=lift; phase=offset;
            Physics.SyncTransforms(); ClosedBounds=body.GetComponent<Collider>().bounds;
            // Exclude the moving panel from the static bake, then carve its live closed position.
            var modifier=body.gameObject.AddComponent<NavMeshModifier>(); modifier.ignoreFromBuild=true;
            body.GetComponent<Collider>().enabled=false;
            // Carve using an unscaled, stationary ground proxy. Moving/scaling the visible
            // panel must never leave a stretched or elevated carving footprint behind.
            var proxy=new GameObject("Gate navigation blocker"); proxy.SetActive(false); proxy.transform.SetParent(transform,false);
            obstacle=proxy.AddComponent<NavMeshObstacle>(); obstacle.enabled=false; obstacle.shape=NavMeshObstacleShape.Box;
            obstacle.center=new Vector3(0,1.65f,0); obstacle.size=new Vector3(body.localScale.x,3.3f,body.localScale.z);
            obstacle.carving=true; obstacle.carveOnlyStationary=true; obstacle.carvingTimeToStationary=0;
            properties=new MaterialPropertyBlock(); panel.localPosition=closed+Vector3.up*travel;
        }
        public void ActivateNavigation()
        {
            navigationReady=true; panel.GetComponent<Collider>().enabled=true;
            BattleDirector.InvalidateSightPhysics();
            if(obstacle!=null) { obstacle.enabled=!IsPassable; obstacle.gameObject.SetActive(true); }
        }

        void Update()
        {
            if(director==null) director=GetComponentInParent<BattlefieldBootstrap>()?.GetComponent<BattleDirector>();
            if(director==null || !director.IsBattleRunning) { TrySetOpenAmount(1); return; }
            float clock=(director.BattleDuration+phase)%24;
            // Green/open for 9s, amber warning for 3s, closed for 8s; four seconds allow travel.
            bool opening=clock<12 || clock>=22;
            float next=Mathf.MoveTowards(OpenAmount,opening?1:0,Time.deltaTime*.38f);
            TrySetOpenAmount(next);
            Color tint=WaitingForClearance || clock>=9 && clock<12?new Color(1,.57f,.06f):IsPassable?new Color(.2f,1,.46f):new Color(1,.16f,.08f);
            properties.SetColor("_BaseColor",tint); properties.SetColor("_Color",tint); properties.SetColor("_EmissionColor",tint*.8f); signal.SetPropertyBlock(properties);
        }

        public bool TrySetOpenAmount(float amount)
        {
            amount=Mathf.Clamp01(amount); WaitingForClearance=false;
            if(amount<OpenAmount && DoorwayOccupied()) { WaitingForClearance=true; return false; }
            bool changed = !Mathf.Approximately(OpenAmount, amount);
            OpenAmount=amount; if(panel!=null) panel.localPosition=closed+Vector3.up*(travel*amount);
            if (changed) BattleDirector.InvalidateSightPhysics();
            // A raised panel clears even the tallest body. Removing its carve explicitly also
            // avoids a stale footprint while Unity updates a vertically moving obstacle.
            if(obstacle!=null) obstacle.enabled=navigationReady && !IsPassable;
            return true;
        }
        bool DoorwayOccupied()
        {
            var bounds=ClosedBounds; bounds.Expand(new Vector3(1.8f,.5f,1.8f));
            int count=Physics.OverlapBoxNonAlloc(bounds.center,bounds.extents,occupants,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var unit=occupants[i].GetComponentInParent<BattleUnit>();
                if(unit!=null && unit.IsAlive) return true;
            }
            var player=director!=null?director.GetComponent<PossessionController>():null;
            return player!=null && bounds.Contains(player.ViewCamera.transform.position);
        }
    }
}
