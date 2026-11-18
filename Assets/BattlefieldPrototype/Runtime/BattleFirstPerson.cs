using UnityEngine;
using UnityEngine.Rendering;

namespace BattlefieldPrototype
{
    /// <summary>Class-specific held equipment. Visual recoil never moves the aim ray or headset.</summary>
    [DefaultExecutionOrder(180)]
    public sealed class BattleFirstPerson : MonoBehaviour
    {
        BattlefieldBootstrap bootstrap;
        PossessionController player;
        BattleDirector director;
        BattleUnit shown;
        BattleCharacterModel model;
        Renderer[] leftRenderers;
        Vector3 leftRest;
        float strike, art;
        public BattleCharacterModel Model => model;
        public void Initialize(BattlefieldBootstrap owner,PossessionController controller)
        {
            bootstrap=owner; player=controller; director=owner.GetComponent<BattleDirector>();
            director.UnitAttacked+=Attacked; director.AbilityActivated+=Activated;
        }
        void Attacked(BattleUnit unit) { if(unit==shown) strike=1; }
        void Activated(BattleUnit unit,int slot) { if(unit==shown) art=1; }
        void Rebuild(BattleUnit unit)
        {
            if(model!=null) { model.gameObject.SetActive(false); Destroy(model.gameObject); }
            shown=unit; strike=art=0; leftRenderers=null;
            if(unit==null || !unit.IsAlive) { model=null; return; }
            model=bootstrap.CreatePresentationModel(unit.Definition.Role,unit.Team,transform);
            model.name="First-person "+unit.Definition.DisplayName;
            Transform left=model.LeftElbow!=null?model.LeftElbow:model.LeftArm;
            Transform right=model.RightElbow!=null?model.RightElbow:model.RightArm;
            var colors=new MaterialPropertyBlock();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                colors.Clear(); colors.SetColor("_BaseColor",renderer.sharedMaterial.color); colors.SetColor("_Color",renderer.sharedMaterial.color);
                renderer.SetPropertyBlock(colors);
                bool arm=left!=null && renderer.transform.IsChildOf(left) || right!=null && renderer.transform.IsChildOf(right);
                renderer.enabled=arm || model.IsSpirit;
                renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            }
            leftRenderers=left!=null?left.GetComponentsInChildren<Renderer>():null;
            if(model.LeftArm!=null) leftRest=model.LeftArm.localPosition;
        }
        void LateUpdate()
        {
            if(player==null) return;
            if(shown!=player.CurrentUnit || shown!=null && !shown.IsAlive) Rebuild(player.CurrentUnit!=null && player.CurrentUnit.IsAlive?player.CurrentUnit:null);
            if(model==null) return;
            bool visible=director.IsBattleRunning && !player.IsMenuActive;
            model.gameObject.SetActive(visible);
            if(!visible) return;
            strike=Mathf.MoveTowards(strike,0,Time.deltaTime*5); art=Mathf.MoveTowards(art,0,Time.deltaTime*3);
            bool tracked=player.IsUsingXR && !player.IsDesktopInputActive && player.HasTrackedAim;
            if(leftRenderers!=null) foreach(var renderer in leftRenderers) renderer.enabled=!tracked || player.HasTrackedLeftHand;
            if(tracked)
            {
                var hand=player.RightHandAim;
                model.transform.SetParent(transform,false); model.transform.localScale=Vector3.one;
                model.transform.rotation=hand.rotation;
                Vector3 grip=model.RightElbow!=null?model.RightElbow.TransformPoint(new Vector3(0,-.23f,.1f)):
                    model.transform.TransformPoint(new Vector3(0,1.35f,0));
                model.transform.position+=hand.position-grip;
                if(model.LeftArm!=null && player.HasTrackedLeftHand)
                {
                    model.LeftArm.rotation=player.LeftHandAim.rotation;
                    Vector3 leftGrip=model.LeftElbow!=null?model.LeftElbow.TransformPoint(new Vector3(0,-.23f,.1f)):
                        model.LeftArm.TransformPoint(new Vector3(0,-.4f,.1f));
                    model.LeftArm.position+=player.LeftHandAim.position-leftGrip;
                }
            }
            else
            {
                model.transform.SetParent(player.ViewCamera.transform,false);
                float sway=Mathf.Sin(Time.time*1.8f)*.004f;
                Vector3 home=model.IsSpirit?new Vector3(.22f,-.8f,.68f):model.IsQuadruped?new Vector3(.04f,-.42f,.52f):new Vector3(.10f,-.72f,.60f);
                model.transform.localScale=Vector3.one*(model.IsSpirit? .38f:model.IsQuadruped? .7f:.6f);
                model.transform.localPosition=home+new Vector3(sway,Mathf.Abs(sway)-strike*.035f,-strike*.08f+art*.035f);
                model.transform.localRotation=Quaternion.Euler(strike*9-art*4,-6+strike*5,art*4);
                if(model.LeftArm!=null) model.LeftArm.localPosition=leftRest;
            }
            if(model.RightArm!=null) model.RightArm.localRotation=Quaternion.Euler(-strike*(shown.IsMelee?48:9)-art*18,0,5);
            if(model.LeftArm!=null && !tracked) model.LeftArm.localRotation=Quaternion.Euler(-strike*12-art*12,0,-5);
        }
        void OnDisable() { if(model!=null) model.gameObject.SetActive(false); }
        void OnDestroy()
        {
            if(director!=null) { director.UnitAttacked-=Attacked; director.AbilityActivated-=Activated; }
            if(model!=null) Destroy(model.gameObject);
        }
    }
}
