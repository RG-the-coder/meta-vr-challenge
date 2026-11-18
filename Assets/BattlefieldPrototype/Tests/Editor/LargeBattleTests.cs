using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed class LargeArmyPlannerTests
    {
        [Test]
        public void BudgetsVaryWithinSharedRangeAndStrategicArmiesSpendAllPower()
        {
            var definitions=new List<BattleUnitDefinition>();
            var previous=UnityEngine.Random.state;
            try
            {
                foreach(UnitRole role in Enum.GetValues(typeof(UnitRole))) definitions.Add(BattleUnitDefinition.CreateDefault(role));
                UnityEngine.Random.InitState(8305);
                int budget=600;
                var budgets=new HashSet<int>();
                for(int round=0;round<80;round++)
                {
                    int next=BattleDeployment.RollBudget(400,1000,budget);
                    Assert.That(next,Is.InRange(400,1000)); Assert.That(next,Is.Not.EqualTo(budget));
                    budgets.Add(next); budget=next;
                    for(int doctrine=0;doctrine<4;doctrine++)
                    {
                        Assert.That(BattleArmyPlanner.TryBuildStrategic(definitions,budget,2000,doctrine,out var roster),Is.True);
                        int spent=0; foreach(var entry in roster) spent+=entry.DeploymentCost;
                        Assert.That(spent,Is.EqualTo(budget));
                        Assert.That(roster.Count,Is.InRange(1,budget));
                    }
                }
                Assert.That(budgets.Count,Is.GreaterThan(8));
                var wisp=definitions.Find(d=>d.Role==UnitRole.Wisp);
                Assert.That(BattleArmyPlanner.TryBuildStrategic(new[]{wisp},150,150,0,out var wisps),Is.True);
                Assert.That(wisps.Count,Is.EqualTo(150));
            }
            finally { UnityEngine.Random.state=previous; foreach(var definition in definitions) UnityEngine.Object.DestroyImmediate(definition); }
        }
    }

    public sealed class LargeBattleFrameTests
    {
        readonly List<GameObject> previousRoots=new List<GameObject>();
        GameObject root;
        BattlefieldBootstrap bootstrap;
        BattleDirector director;
        BattleDeployment deployment;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            foreach(var item in SceneManager.GetActiveScene().GetRootGameObjects()) if(item.activeSelf) { previousRoots.Add(item); item.SetActive(false); }
            root=new GameObject("Large battle regression"); bootstrap=root.AddComponent<BattlefieldBootstrap>(); bootstrap.TowerDefenseMode = false;
            bootstrap.RandomizeBattlefield=false;
            yield return null;
            director=root.GetComponent<BattleDirector>(); deployment=root.GetComponent<BattleDeployment>();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(root!=null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            yield return null;
            foreach(var item in previousRoots) if(item!=null) item.SetActive(true);
            previousRoots.Clear();
            yield return new ExitPlayMode();
        }
        Vector3 FreePoint(Func<Vector3,bool> accept=null)
        {
            Bounds bounds=deployment.DeploymentBounds;
            for(float z=bounds.min.z+1;z<bounds.max.z-1;z+=2.2f) for(float x=bounds.min.x+1;x<bounds.max.x-1;x+=2.2f)
            {
                var desired=new Vector3(x,0,z);
                if(accept!=null && !accept(desired)) continue;
                if(deployment.TryGetPlacement(desired,null,out var position,out _)) return position;
            }
            Assert.Fail("The full battlefield must have a legal placement."); return default;
        }
        [UnityTest]
        public IEnumerator EveryLargeLayoutSupportsEqualVariableBudgetsAndMoreThanTwentyUnits()
        {
            Assert.That(bootstrap.ArenaSize.x*bootstrap.ArenaSize.y,Is.GreaterThan(36*44*5));
            int previous=deployment.Budget;
            for(int layout=0;layout<3;layout++)
            {
                bootstrap.MapKind=(BattleMapKind)layout;
                deployment.NewGame();
                Assert.That(deployment.Budget,Is.Not.EqualTo(previous)); previous=deployment.Budget;
                Assert.That(deployment.EnemySpent,Is.EqualTo(deployment.Budget));
                Assert.That(bootstrap.CurrentMapKind,Is.EqualTo((BattleMapKind)layout));
                Assert.That(deployment.TryPlace(UnitRole.Wisp,FreePoint(p=>p.z>20)),Is.True,"Blue units can use the northern half.");
                Assert.That(deployment.TryPlace(UnitRole.Wisp,FreePoint(p=>p.z< -20)),Is.True);
                for(int i=2;i<64;i++) Assert.That(deployment.TryPlace(UnitRole.Wisp,FreePoint()),Is.True);
                Assert.That(deployment.Count,Is.EqualTo(64)); Assert.That(deployment.Spent,Is.EqualTo(64));
                yield return null;
                Canvas menu=null;
                foreach(var candidate in UnityEngine.Object.FindObjectsByType<Canvas>()) if(candidate.name=="Battle Menu") menu=candidate;
                Assert.That(menu,Is.Not.Null);
                BattleFrameBehaviorTests.ClickMenu(menu,menu.transform.Find("Deployment/Deselect"));
                BattleFrameBehaviorTests.ClickMenu(menu,menu.transform.Find("Deployment/Recruitment panel/Choose Vanguard"));
                BattleFrameBehaviorTests.ClickMenu(menu,menu.transform.Find("Deployment/Zoom In"));
                BattleFrameBehaviorTests.ClickMenu(menu,menu.transform.Find("Deployment/Zoom In"));
                BattleFrameBehaviorTests.ClickMenu(menu,menu.transform.Find("Deployment/Pan 1"));
                Vector3 requested=FreePoint(p=>p.x>0 && p.x<20 && Mathf.Abs(p.z)<20);
                var content=menu.transform.Find("Deployment/Deployment Board/Map content").GetComponent<RectTransform>();
                Vector3 mapPoint=content.TransformPoint(new Vector3(requested.x/deployment.ArenaSize.x*content.rect.width,
                    requested.z/deployment.ArenaSize.y*content.rect.height,0));
                BattleFrameBehaviorTests.ClickMenuAt(menu,RectTransformUtility.WorldToScreenPoint(null,mapPoint));
                Assert.That(deployment.Count,Is.EqualTo(65),"Zoomed and panned map clicks must deploy at the requested world position.");
                bool placedAtRequest=false;
                foreach(var unit in director.Units) if(unit.Team==BattleTeam.Blue && unit.Definition.Role==UnitRole.Vanguard &&
                    Vector3.Distance(unit.transform.position,requested)<.1f) placedAtRequest=true;
                Assert.That(placedAtRequest,Is.True);
                BattleFrameBehaviorTests.ClickMenu(menu,menu.transform.Find("Deployment/Reset Map"));
                Assert.That(content.localScale, Is.EqualTo(Vector3.one));
                Assert.That(content.anchoredPosition, Is.EqualTo(Vector2.zero));
                BattleFrameBehaviorTests.CaptureUiPreview(menu,"Creator-"+bootstrap.CurrentMapKind);
                CaptureWorld("Large-"+bootstrap.CurrentMapKind);
                foreach(var unit in director.Units)
                {
                    Assert.That(Mathf.Abs(unit.transform.position.x),Is.LessThan(bootstrap.ArenaSize.x*.5f));
                    Assert.That(Mathf.Abs(unit.transform.position.z),Is.LessThan(bootstrap.ArenaSize.y*.5f));
                }
            }
        }
        [UnityTest]
        public IEnumerator AutoFillCopiesEnemyClassesAndCountsAcrossEveryMapWithoutRerolling()
        {
            bootstrap.RandomizePowerBudget = false;
            int layout = 0;
            foreach (BattleMapKind map in Enum.GetValues(typeof(BattleMapKind)))
            {
                bootstrap.MapKind = map;
                bootstrap.ArmyPowerBudget = new[] { 10, 200, 600, 1000 }[layout++ % 4];
                deployment.NewGame();
                var enemies = new List<BattleUnit>(director.Units);
                var positions = enemies.ConvertAll(unit => unit.transform.position);
                string doctrine = deployment.EnemyDoctrine;
                Assert.That(deployment.TryPlace(UnitRole.Wisp, FreePoint()), Is.True);
                BattleUnit previous = deployment.SelectedUnit;
                if (layout == 1)
                {
                    yield return null;
                    Canvas menu = null;
                    foreach (var candidate in UnityEngine.Object.FindObjectsByType<Canvas>())
                        if (candidate.name == "Battle Menu") menu = candidate;
                    Assert.That(menu, Is.Not.Null);
                    BattleFrameBehaviorTests.ClickMenu(menu, menu.transform.Find("Deployment/Randomize"));
                }
                else Assert.That(deployment.MirrorEnemyArmy(), Is.True, map + ": " + deployment.LastMessage);
                Assert.That(previous == null || !previous.gameObject.activeSelf, Is.True, "Auto-fill replaces the edited army.");
                AssertMirroredRoster(enemies);
                Assert.That(deployment.EnemyDoctrine, Is.EqualTo(doctrine));
                Assert.That(deployment.LastMessage, Does.Contain("Enemy army copied"));
                for (int index = 0; index < enemies.Count; index++)
                {
                    Assert.That(enemies[index].IsAlive, Is.True);
                    Assert.That(enemies[index].transform.position, Is.EqualTo(positions[index]), "Auto-fill must leave the enemy untouched.");
                }
                if (layout % 3 == 0)
                {
                    var first = deployment.CapturePreset("First copy");
                    Assert.That(deployment.RandomizeBlue(), Is.True, "Existing callers must also mirror the enemy.");
                    AssertMirroredRoster(enemies);
                    var second = deployment.CapturePreset("Second copy");
                    Assert.That(second.units, Is.EqualTo(first.units), "Repeated auto-fill must not reroll a stronger composition or formation.");
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AutoFillSearchesBeyondBlockedMirrorAndKeepsCurrentArmyWhenNoRoomRemains()
        {
            bootstrap.RandomizePowerBudget = false; bootstrap.ArmyPowerBudget = 600;
            deployment.NewGame();
            var enemies = new List<BattleUnit>(director.Units);
            Vector3 mirror = -enemies[0].transform.position;
            var obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstruction.transform.SetParent(root.transform);
            obstruction.transform.position = new Vector3(mirror.x, 3, mirror.z);
            obstruction.transform.localScale = new Vector3(30, 6, 30);
            Physics.SyncTransforms();
            Assert.That(deployment.MirrorEnemyArmy(), Is.True, deployment.LastMessage);
            AssertMirroredRoster(enemies);
            BattleUnit first = null;
            foreach (var unit in director.Units) if (unit.Team == BattleTeam.Blue) { first = unit; break; }
            Assert.That(Vector3.Distance(first.transform.position, mirror), Is.GreaterThan(8), "A blocked mirror needs a search beyond the nearby preset offsets.");
            Assert.That(deployment.SelectUnit(first), Is.True);
            var army = new List<BattleUnit>(director.Units);
            obstruction.transform.position = Vector3.up * 3;
            obstruction.transform.localScale = new Vector3(bootstrap.ArenaSize.x, 6, bootstrap.ArenaSize.y);
            Physics.SyncTransforms();
            Assert.That(deployment.MirrorEnemyArmy(), Is.False);
            Assert.That(deployment.SelectedUnit, Is.SameAs(first));
            Assert.That(director.Units, Is.EqualTo(army), "Failed auto-fill must preserve the existing army.");
            obstruction.SetActive(false);
            director.BeginBattle();
            Assert.That(deployment.RandomizeBlue(), Is.False, "Auto-fill is locked during combat.");
            Assert.That(director.Units, Is.EqualTo(army));
            yield return null;
        }

        void AssertMirroredRoster(List<BattleUnit> enemies)
        {
            var allies = new List<BattleUnit>();
            foreach (var unit in director.Units) if (unit.Team == BattleTeam.Blue && unit.IsAlive) allies.Add(unit);
            Assert.That(allies.Count, Is.EqualTo(enemies.Count));
            Assert.That(deployment.Spent, Is.EqualTo(deployment.EnemySpent));
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
                Assert.That(allies.FindAll(unit => unit.Definition.Role == role).Count,
                    Is.EqualTo(enemies.FindAll(unit => unit.Definition.Role == role).Count), role.ToString());
            foreach (var ally in allies)
            {
                Assert.That(enemies.Exists(enemy => enemy.Definition == ally.Definition), Is.True, "Copies use the same character statistics.");
                Assert.That(deployment.TryGetPlacement(ally.transform.position, ally, out _, out var reason), Is.True, reason);
            }
        }

        [UnityTest]
        public IEnumerator SavedFormationsRoundTripRelocateAndRejectUnaffordableArmiesWithoutDataLoss()
        {
            bootstrap.RandomizePowerBudget=false; bootstrap.ArmyPowerBudget=600; deployment.NewGame();
            foreach(var role in new[]{UnitRole.Vanguard,UnitRole.Ranger,UnitRole.Medic,UnitRole.Assassin,UnitRole.Paladin})
                Assert.That(deployment.TryPlace(role,FreePoint()),Is.True);
            int originalCost=deployment.Spent;
            var preset=JsonUtility.FromJson<BattleArmyPreset>(JsonUtility.ToJson(deployment.CapturePreset("Veterans")));
            deployment.Clear(); Assert.That(deployment.TryApplyPreset(preset),Is.True); Assert.That(deployment.Spent,Is.EqualTo(originalCost));
            var broken=JsonUtility.FromJson<BattleArmyPreset>(JsonUtility.ToJson(preset)); var entry=broken.units[0]; entry.role=(UnitRole)999; broken.units[0]=entry;
            Assert.That(deployment.TryApplyPreset(broken),Is.False); Assert.That(deployment.Count,Is.EqualTo(5));
            bootstrap.ArmyPowerBudget=50; deployment.NewGame();
            Assert.That(deployment.TryApplyPreset(preset),Is.False); Assert.That(deployment.Count,Is.Zero);
            bootstrap.ArmyPowerBudget=600; bootstrap.MapKind=BattleMapKind.RiverCrossing; deployment.NewGame();
            Assert.That(deployment.TryApplyPreset(preset),Is.True); Assert.That(deployment.Count,Is.EqualTo(5));
            var adjusted=new BattleArmyPreset {name="Terrain adaptation"};
            adjusted.units.Add(new BattlePresetUnit {role=UnitRole.Wisp,x=.5f-8/bootstrap.ArenaSize.x,z=.5f});
            Assert.That(deployment.TryApplyPreset(adjusted),Is.True);
            BattleUnit wisp=null; foreach(var unit in director.Units) if(unit.Team==BattleTeam.Blue) wisp=unit;
            Assert.That(Vector3.Distance(wisp.transform.position,new Vector3(-8,0,0)),Is.GreaterThan(.8f));
            yield return null;
        }
        [UnityTest]
        public IEnumerator OrdersMinimapWeaponsAndAudioWorkAcrossAllCharacters()
        {
            bootstrap.RandomizePowerBudget=false; bootstrap.ArmyPowerBudget=600; deployment.NewGame();
            var allies=new List<BattleUnit>();
            foreach(var definition in deployment.Definitions)
            { Assert.That(deployment.TryPlace(definition.Role,FreePoint(),out var unit),Is.True); allies.Add(unit); }
            foreach(var unit in director.Units) unit.GetComponent<BattleUnitAI>().enabled=false;
            director.BeginBattle();
            var player=root.GetComponent<PossessionController>();
            var firstPerson=root.GetComponent<BattleFirstPerson>();
            foreach(var ally in allies)
            {
                player.Possess(ally);
                float deadline=Time.realtimeSinceStartup+3;
                while((player.CurrentUnit!=ally || player.BlinkOpacity>0) && Time.realtimeSinceStartup<deadline) yield return null;
                yield return null;
                Assert.That(player.CurrentUnit,Is.SameAs(ally)); Assert.That(firstPerson.Model,Is.Not.Null);
                bool visible=false; foreach(var renderer in firstPerson.Model.GetComponentsInChildren<Renderer>()) visible|=renderer.enabled;
                Assert.That(visible,Is.True,ally.Definition.Role+" needs visible held equipment.");
                Assert.That(firstPerson.Model.GetComponentsInChildren<Collider>().Length,Is.Zero);
                Assert.That(ally.TryAttack(ally.EyePosition,Vector3.up),Is.True);
                if(ally.Definition.Role==UnitRole.Ranger || ally.Definition.Role==UnitRole.Paladin)
                    BattleFrameBehaviorTests.CaptureUiPreview(root.GetComponent<BattleHud>().TacticalHud.GetComponent<Canvas>(),"First-person-"+ally.Definition.Role);
            }
            var orders=root.GetComponent<BattleOrders>();
            Assert.That(orders.Issue(BattleOrder.Hold),Is.True); Vector3 anchor=orders.Destination(allies[0]);
            allies[0].GetComponent<CharacterController>().enabled=false; allies[0].transform.position+=Vector3.right*3;
            Assert.That(orders.Destination(allies[0]),Is.EqualTo(anchor));
            Assert.That(orders.Issue(BattleOrder.Protect,allies[0]),Is.True);
            Assert.That(Vector3.Distance(orders.Destination(allies[1]),allies[0].transform.position),Is.InRange(2,7));
            Assert.That(orders.Issue(BattleOrder.Advance),Is.True);
            // Full-roster markers belong to spectator mode; possession now has fog of war.
            player.ReleaseControl(); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(player.CurrentUnit,Is.Null);
            var map=root.GetComponent<BattleHud>().TacticalHud.Minimap;
            map.SetVerticesDirty(); Canvas.ForceUpdateCanvases(); int vertices=map.canvasRenderer.GetMesh().vertexCount;
            BattleUnit enemy=null; foreach(var unit in director.Units) if(unit.Team==BattleTeam.Red) { enemy=unit; break; }
            Vector2 before=map.Project(enemy.transform.position); enemy.GetComponent<CharacterController>().enabled=false;
            enemy.transform.position+=Vector3.right*8; Assert.That(Vector2.Distance(before,map.Project(enemy.transform.position)),Is.GreaterThan(2));
            enemy.TakeDamage(10000,allies[0]); map.SetVerticesDirty(); Canvas.ForceUpdateCanvases();
            Assert.That(map.canvasRenderer.GetMesh().vertexCount,Is.EqualTo(vertices-4));
            Assert.That(root.GetComponent<BattleAudio>().VoiceCount,Is.EqualTo(BattleAudio.VoiceCapacity));
            Assert.That(root.GetComponentsInChildren<AudioSource>().Length,Is.EqualTo(BattleAudio.VoiceCapacity));
            var sound=root.GetComponent<BattleAudio>(); sound.ToggleMute(); Assert.That(sound.Muted,Is.True); sound.ToggleMute();
            Assert.That(orders.Issue(BattleOrder.Protect,allies[0]),Is.True);
            BattleUnit attacker=null; foreach(var unit in director.Units) if(unit!=null && unit.IsAlive && unit.Team==BattleTeam.Red) { attacker=unit; break; }
            allies[0].TakeDamage(10000,attacker); orders.SendMessage("Update",SendMessageOptions.RequireReceiver);
            Assert.That(orders.Current,Is.EqualTo(BattleOrder.Hold));
            deployment.NewGame(); Assert.That(orders.Current,Is.EqualTo(BattleOrder.Advance));
            yield return null;
        }
        void CaptureWorld(string name)
        {
            string directory=Environment.GetEnvironmentVariable("BATTLEFIELD_PREVIEW_DIR"); if(string.IsNullOrEmpty(directory)) return;
            System.IO.Directory.CreateDirectory(directory);
            var obj=new GameObject("Large terrain review",typeof(Camera)); obj.transform.position=new Vector3(100,100,-140); obj.transform.LookAt(Vector3.zero);
            var camera=obj.GetComponent<Camera>(); camera.enabled=false; camera.farClipPlane=600; camera.fieldOfView=55;
            obj.transform.position=new Vector3(83,78,-105); obj.transform.LookAt(Vector3.zero);
            var target=new RenderTexture(1280,720,24); var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false); var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target;
                if(GraphicsSettings.currentRenderPipeline!=null) RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target}); else camera.Render();
                RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(obj); }
        }
    }
}
