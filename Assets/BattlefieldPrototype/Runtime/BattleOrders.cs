using System.Collections.Generic;
using UnityEngine;

namespace BattlefieldPrototype
{
    public enum BattleOrder { Advance, Hold, Protect, Route }
    public sealed partial class BattleOrders : MonoBehaviour
    {
        BattleDirector director;
        readonly Dictionary<BattleUnit,Vector3> heldPositions=new Dictionary<BattleUnit,Vector3>();
        public BattleOrder Current { get; private set; }
        public BattleUnit ProtectedUnit { get; private set; }
        public Vector3 Anchor { get; private set; }
        public string Description => Current==BattleOrder.Route ? "FOLLOW ROUTE / " + waypoints.Count + " POINTS" : Current==BattleOrder.Hold ? "HOLD FORMATION" : Current==BattleOrder.Protect && ProtectedUnit!=null
            ? "PROTECT "+ProtectedUnit.Definition.DisplayName.ToUpperInvariant() : "ADVANCE";
        public void Initialize(BattleDirector owner) { director=owner; owner.RoundReset+=ResetOrders; }
        void ResetOrders() { Current=BattleOrder.Advance; ProtectedUnit=null; Anchor=Vector3.zero; heldPositions.Clear(); ClearRouteData(); Revision++; }
        public bool Issue(BattleOrder order, BattleUnit target=null)
        {
            if (director==null || !director.IsBattleRunning) return false;
            if ((int)order < 0 || (int)order > (int)BattleOrder.Protect) return false;
            if (order==BattleOrder.Protect && (target==null || !target.IsAlive || target.Team!=BattleTeam.Blue)) return false;
            Current=order; ProtectedUnit=order==BattleOrder.Protect?target:null;
            ClearRouteData(); Revision++; director.Towers?.CancelRoutePlanning();
            heldPositions.Clear(); Anchor=Vector3.zero; int count=0;
            foreach(var unit in director.Units)
                if(unit!=null && unit.IsAlive && unit.Team==BattleTeam.Blue)
                { heldPositions[unit]=unit.transform.position; Anchor+=unit.transform.position; count++; }
            if(count>0) Anchor/=count;
            return true;
        }
        public bool IssueForPlayer(BattleOrder order,PossessionController player)
        {
            BattleUnit target=player.AimedUnit!=null && player.AimedUnit.Team==BattleTeam.Blue?player.AimedUnit:player.CurrentUnit;
            if(order==BattleOrder.Protect && target==null)
                foreach(var unit in director.Units) if(unit!=null && unit.IsAlive && unit.Team==BattleTeam.Blue &&
                    (target==null || unit.Definition.DeploymentCost>target.Definition.DeploymentCost)) target=unit;
            return Issue(order,target);
        }
        public Vector3 Destination(BattleUnit unit)
        {
            if (Current == BattleOrder.Route) return RouteDestination(unit);
            if(Current==BattleOrder.Hold && heldPositions.TryGetValue(unit,out var position)) return position;
            if(Current==BattleOrder.Protect && ProtectedUnit!=null && ProtectedUnit.IsAlive)
            {
                int seed=Mathf.Abs(unit.transform.GetSiblingIndex());
                float angle=(seed%16)*Mathf.PI/8;
                float radius=unit.IsMelee?3.2f:5.8f;
                return ProtectedUnit.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
            }
            return Anchor;
        }
        void Update()
        {
            if (Current == BattleOrder.Route) FinishRouteIfComplete();
            if(Current==BattleOrder.Protect && (ProtectedUnit==null || !ProtectedUnit.IsAlive))
            { Current=BattleOrder.Hold; ProtectedUnit=null; }
        }
        void OnDestroy() { if(director!=null) director.RoundReset-=ResetOrders; }
    }
}
