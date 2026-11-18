using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class BattleCharacterModel
    {
        const int Ice=7,Mixture=8;
        void SiegeEquipment()
        {
            switch(role)
            {
                case UnitRole.Halberdier:
                    Rod(RightElbow,new Vector3(0,-1.03f,.16f),new Vector3(0,1.05f,.16f),.047f,Hide);
                    Part("blade",RightElbow,new Vector3(0,1.05f,.16f),new Vector3(.12f,.48f,.065f),Iron);
                    Part("shield",RightElbow,new Vector3(.17f,.75f,.16f),new Vector3(.45f,.43f,.055f),Iron,new Vector3(0,0,90));
                    Part("bevel",RightElbow,new Vector3(0,.69f,.16f),new Vector3(.11f,.18f,.12f),Brass);
                    Part("cloth",Body,new Vector3(0,1.91f,-.02f),new Vector3(.09f,.24f,.27f),Cloth,new Vector3(70,0,0));
                    break;
                case UnitRole.Duelist:
                    Sword(RightElbow,.88f);
                    Part("sphere",LeftElbow,new Vector3(0,-.12f,.23f),new Vector3(.37f,.37f,.09f),Brass);
                    Part("sphere",LeftElbow,new Vector3(0,-.12f,.28f),new Vector3(.13f,.13f,.075f),Iron);
                    Part("cloth",Body,new Vector3(.14f,1.88f,-.04f),new Vector3(.12f,.42f,.045f),Eye,new Vector3(0,0,-25));
                    Part("bevel",Body,new Vector3(0,1.18f,.225f),new Vector3(.34f,.045f,.025f),Brass,new Vector3(0,0,28));
                    break;
                case UnitRole.Frostbinder:
                    Staff(RightElbow,Ice);
                    for(int i=-1;i<=1;i++) Part("crystal",LeftElbow,new Vector3(i*.14f,-.2f,.23f),new Vector3(.08f,.3f+(.1f-Mathf.Abs(i)*.1f),.08f),Ice,new Vector3(0,0,-i*25));
                    foreach(int side in new[]{-1,1}) Part("crystal",Body,new Vector3(side*.32f,1.48f,0),new Vector3(.13f,.35f,.13f),Ice,new Vector3(0,0,-side*30));
                    break;
                case UnitRole.Alchemist:
                    Pack(true); Launcher(RightElbow,true);
                    Part("sphere",LeftElbow,new Vector3(0,-.24f,.18f),new Vector3(.23f,.28f,.23f),Mixture);
                    Part("shaft",LeftElbow,new Vector3(0,-.07f,.18f),new Vector3(.10f,.12f,.10f),Brass);
                    foreach(int side in new[]{-1,1})
                    {
                        Part("sphere",Body,new Vector3(side*.085f,1.65f,.185f),new Vector3(.13f,.105f,.045f),Mixture);
                        Part("shaft",Body,new Vector3(side*.10f,1.54f,.19f),new Vector3(.10f,.13f,.10f),Iron,new Vector3(90,0,0));
                        Part("shaft",Body,new Vector3(side*.20f,.94f,.17f),new Vector3(.09f,.23f,.09f),Mixture);
                    }
                    break;
                case UnitRole.WarDrummer:
                    Part("shaft",LeftElbow,new Vector3(.12f,-.26f,.26f),new Vector3(.50f,.33f,.50f),Hide);
                    foreach(float y in new[]{-.10f,-.42f}) Part("shaft",LeftElbow,new Vector3(.12f,y,.26f),new Vector3(.54f,.035f,.54f),Brass);
                    Part("shaft",LeftElbow,new Vector3(.12f,-.075f,.26f),new Vector3(.47f,.018f,.47f),Cloth);
                    Rod(RightElbow,new Vector3(0,-.27f,.1f),new Vector3(-.13f,.08f,.47f),.027f,Hide);
                    Part("sphere",RightElbow,new Vector3(-.13f,.08f,.47f),Vector3.one*.09f,Brass);
                    Part("bevel",Body,new Vector3(0,1.14f,.22f),new Vector3(.10f,.52f,.03f),Brass,new Vector3(0,0,25));
                    break;
                case UnitRole.Sharpshooter:
                    Launcher(RightElbow,false);
                    Rod(RightElbow,new Vector3(0,-.16f,.38f),new Vector3(0,-.16f,1.05f),.052f,Iron);
                    Part("bevel",RightElbow,new Vector3(0,-.13f,.48f),new Vector3(.13f,.10f,.39f),Hide);
                    Rod(RightElbow,new Vector3(0,-.015f,.32f),new Vector3(0,-.015f,.68f),.075f,Iron);
                    Part("sphere",RightElbow,new Vector3(0,-.015f,.69f),new Vector3(.069f,.069f,.025f),Life);
                    for(int i=0;i<5;i++) Part("shaft",Body,new Vector3(-.17f+i*.07f,1.15f,.226f),new Vector3(.03f,.14f,.03f),Brass);
                    break;
                case UnitRole.Berserker:
                    Axe(LeftElbow); Axe(RightElbow);
                    foreach(int side in new[]{-1,1})
                    {
                        Rod(Body,new Vector3(side*.14f,1.75f,0),new Vector3(side*.30f,1.97f,.03f),.07f,Brass);
                        Part("crystal",Body,new Vector3(side*.38f,1.46f,-.03f),new Vector3(.10f,.28f,.10f),Iron,new Vector3(0,0,-side*45));
                    }
                    break;
                case UnitRole.Warden:
                    Staff(RightElbow,Life,1.08f);
                    foreach(int side in new[]{-1,1})
                    {
                        Rod(Body,new Vector3(side*.12f,1.77f,0),new Vector3(side*.31f,2.02f,-.05f),.035f,Hide);
                        Rod(Body,new Vector3(side*.21f,1.90f,-.025f),new Vector3(side*.38f,1.94f,.02f),.025f,Hide);
                        Rod(RightElbow,new Vector3(0,.60f,.15f),new Vector3(side*.24f,.84f,.15f),.025f,Hide);
                    }
                    Part("shield",LeftElbow,new Vector3(0,-.13f,.24f),new Vector3(.48f,.65f,.08f),Hide);
                    Part("cloth",LeftElbow,new Vector3(0,-.10f,.30f),new Vector3(.34f,.55f,.025f),Life);
                    Rod(LeftElbow,new Vector3(0,-.43f,.33f),new Vector3(0,.15f,.33f),.025f,Brass);
                    break;
            }
        }
        void Axe(Transform hand)
        {
            Rod(hand,new Vector3(0,-.52f,.16f),new Vector3(0,.38f,.16f),.045f,Hide);
            Part("shield",hand,new Vector3(.12f,.28f,.16f),new Vector3(.45f,.40f,.06f),Iron,new Vector3(0,0,90));
            Part("bevel",hand,new Vector3(0,.24f,.16f),new Vector3(.10f,.15f,.11f),Brass);
        }
    }
}
