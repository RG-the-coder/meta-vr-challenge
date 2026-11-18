using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed class ComplexMapFrameTests
    {
        readonly List<GameObject> previousRoots=new List<GameObject>();
        GameObject root;
        BattlefieldBootstrap terrain;
        BattleDeployment deployment;
        BattleDirector director;
        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            foreach(var previous in SceneManager.GetActiveScene().GetRootGameObjects()) if(previous.activeSelf) { previousRoots.Add(previous); previous.SetActive(false); }
            root=new GameObject("Complex battlefield regression"); terrain=root.AddComponent<BattlefieldBootstrap>(); terrain.TowerDefenseMode = false;
            terrain.RandomizeBattlefield=false; terrain.RandomizePowerBudget=false; terrain.ArmyPowerBudget=600;
            terrain.MapKind=BattleMapKind.IronCitadel;
            yield return null;
            deployment=root.GetComponent<BattleDeployment>(); director=root.GetComponent<BattleDirector>();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(root!=null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            yield return null;
            foreach(var previous in previousRoots) if(previous!=null) previous.SetActive(true);
            previousRoots.Clear(); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GeneratedWallsBlockActualUnitVisionOnEveryMap()
        {
            for (int index = 0; index < BattleMapNames.Count; index++)
            {
                terrain.MapKind = (BattleMapKind)index; deployment.NewGame();
                yield return null;
                using (var probes = new TestBattleWorld())
                {
                    var first = probes.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
                    var second = probes.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 5);
                    first.GetComponent<CharacterController>().enabled = second.GetComponent<CharacterController>().enabled = false;
                    int checkedWalls = 0;
                    foreach (var cover in terrain.CoverFootprints)
                    {
                        if (cover.min.y > .1f || cover.max.y < 1.7f) continue;
                        Vector3 a = cover.center, b = cover.center;
                        a.y = b.y = 0;
                        if (cover.size.x > cover.size.z) { a.z = cover.min.z - 2; b.z = cover.max.z + 2; }
                        else { a.x = cover.min.x - 2; b.x = cover.max.x + 2; }
                        first.transform.position = a; second.transform.position = b;
                        first.Face(b - a); second.Face(a - b); Physics.SyncTransforms();
                        Assert.That(first.CanSee(second), Is.False, terrain.MapName + " wall at " + cover.center);
                        Assert.That(second.CanSee(first), Is.False, terrain.MapName + " reverse sight at " + cover.center);
                        Assert.That(probes.Director.FindPriorityEnemy(first), Is.Null);
                        checkedWalls++;
                    }
                    Assert.That(checkedWalls, Is.GreaterThan(0), terrain.MapName);
                }
            }
        }

        [UnityTest]
        public IEnumerator ClosingGatesImmediatelyBlockSightAndOpeningRestoresIt()
        {
            terrain.MapKind = BattleMapKind.Ironworks; deployment.NewGame();
            yield return null; yield return null;
            foreach (var moving in terrain.MovingObstacles) moving.enabled = false;
            foreach (var unit in new List<BattleUnit>(director.Units))
            { director.UnregisterUnit(unit); unit.gameObject.SetActive(false); UnityEngine.Object.Destroy(unit.gameObject); }
            using (var probes = new TestBattleWorld())
            {
                var first = probes.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
                var second = probes.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 5);
                first.GetComponent<CharacterController>().enabled = second.GetComponent<CharacterController>().enabled = false;
                foreach (var gate in terrain.MovingObstacles)
                {
                    Vector3 center = gate.ClosedBounds.center; center.y = 0;
                    first.transform.position = center + Vector3.right + Vector3.back * 2;
                    second.transform.position = center + Vector3.right + Vector3.forward * 2;
                    first.Face(Vector3.forward); second.Face(Vector3.back); Physics.SyncTransforms();
                    Assert.That(first.CanSee(second), Is.True);
                    Assert.That(gate.TrySetOpenAmount(0), Is.True);
                    Assert.That(first.CanSee(second), Is.False, "A closed gate must block sight in the same frame.");
                    Assert.That(gate.TrySetOpenAmount(1), Is.True);
                    Assert.That(first.CanSee(second), Is.True);
                }
            }
        }

        [UnityTest]
        public IEnumerator AllComplexMapsHaveTallCoverConnectedRoutesAndFullEnemyArmies()
        {
            Assert.That(terrain.ArenaSize,Is.EqualTo(new Vector2(160,192)));
            var titan=BattleUnitDefinition.CreateDefault(UnitRole.Titan);
            float eyeHeight=titan.EyeHeight; UnityEngine.Object.Destroy(titan);
            var path=new NavMeshPath();
            for(int index=3;index<BattleMapNames.Count;index++)
            {
                terrain.MapKind=(BattleMapKind)index; deployment.NewGame();
                yield return null; yield return null;
                Assert.That(deployment.EnemySpent,Is.EqualTo(600),terrain.MapName);
                Assert.That(terrain.CoverFootprints.Count,Is.GreaterThan(20),terrain.MapName+" needs substantial geometry.");
                foreach(var cover in terrain.CoverFootprints)
                    Assert.That(cover.max.y,Is.GreaterThan(eyeHeight+3),terrain.MapName+" must block even Titan-height sightlines.");
                var slots=new List<Vector3>();
                Bounds bounds=deployment.DeploymentBounds;
                for(float z=bounds.min.z+1;z<bounds.max.z;z+=6) for(float x=bounds.min.x+1;x<bounds.max.x;x+=6)
                    if(deployment.TryGetPlacement(new Vector3(x,0,z),null,out var clear,out _)) slots.Add(clear);
                Assert.That(slots.Count,Is.GreaterThan(150),terrain.MapName+" needs room for large formations.");
                Assert.That(NavMesh.SamplePosition(slots[0],out var start,1,NavMesh.AllAreas),Is.True);
                var checkpoints=new List<Vector3>(terrain.RouteCheckpoints);
                for(int i=0;i<slots.Count;i+=Mathf.Max(1,slots.Count/24)) checkpoints.Add(slots[i]);
                foreach(var point in checkpoints)
                {
                    Assert.That(NavMesh.SamplePosition(point,out var end,1,NavMesh.AllAreas),Is.True,terrain.MapName+" checkpoint "+point);
                    Assert.That(NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path),Is.True,terrain.MapName+" path to "+point);
                    Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),terrain.MapName+" isolates a chamber at "+point);
                }
                var coverTest=terrain.CoverFootprints[0];
                Vector3 a=coverTest.center,b=coverTest.center;
                a.y=b.y=eyeHeight;
                if(coverTest.size.x>coverTest.size.z) { a.z=coverTest.min.z-2; b.z=coverTest.max.z+2; }
                else { a.x=coverTest.min.x-2; b.x=coverTest.max.x+2; }
                Assert.That(Physics.Linecast(a,b,~0,QueryTriggerInteraction.Ignore),Is.True,"Titan cannot look over the cover.");
                var canvas=FindMenu();
                BattleFrameBehaviorTests.CaptureUiPreview(canvas,"Map-plan-"+terrain.CurrentMapKind);
                if(terrain.CurrentMapKind==BattleMapKind.SunlessCatacombs)
                    Capture("Interior-Catacombs",terrain.RouteCheckpoints[0]+Vector3.up*eyeHeight,terrain.RouteCheckpoints[0]+Vector3.forward*20+Vector3.up*eyeHeight);
                else Capture("Complex-"+terrain.CurrentMapKind,new Vector3(135,155,-167),Vector3.zero);
                if(index==3 || index==4 || index==5)
                {
                    Vector3 point=slots[slots.Count/2]+Vector3.up*eyeHeight;
                    Capture("Titan-view-"+terrain.CurrentMapKind,point,point+Vector3.forward*25);
                }
            }
        }

        [UnityTest]
        public IEnumerator MovingGatesCarveRoutesAndWaitForOccupantsInsteadOfCrushingThem()
        {
            terrain.MapKind=BattleMapKind.Ironworks; deployment.NewGame();
            yield return null; yield return null;
            Assert.That(terrain.MovingObstacles.Count,Is.EqualTo(2));
            foreach(var moving in terrain.MovingObstacles) moving.enabled=false;
            var gate=terrain.MovingObstacles[0];
            foreach(var unit in new List<BattleUnit>(director.Units))
            { director.UnregisterUnit(unit); unit.gameObject.SetActive(false); UnityEngine.Object.Destroy(unit.gameObject); }
            Physics.SyncTransforms();
            var position=gate.ClosedBounds.center; position.y=0;
            // Stay inside one navigation tile: a ray exactly along the x=0 tile seam
            // can report the seam itself instead of the gate's footprint.
            Assert.That(NavMesh.SamplePosition(position+Vector3.right+Vector3.back*2,out var firstSide,1,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.SamplePosition(position+Vector3.right+Vector3.forward*2,out var secondSide,1,NavMesh.AllAreas),Is.True);
            Vector3 a=firstSide.position,b=secondSide.position;
            Assert.That(NavMesh.Raycast(a,b,out var initialHit,NavMesh.AllAreas),Is.False,"Initially raised gate must have a direct route; hit="+initialHit.position);
            Assert.That(deployment.TryPlace(UnitRole.Titan,position,out var titan),Is.True);
            Physics.SyncTransforms();
            Assert.That(gate.TrySetOpenAmount(0),Is.False);
            Assert.That(gate.WaitingForClearance,Is.True); Assert.That(gate.OpenAmount,Is.EqualTo(1));
            titan.GetComponent<CharacterController>().enabled=false;
            titan.transform.position=position+Vector3.right*14; Physics.SyncTransforms();
            Assert.That(gate.TrySetOpenAmount(0),Is.True);
            Physics.SyncTransforms(); yield return null; yield return null;
            Assert.That(gate.IsPassable,Is.False); Assert.That(gate.CurrentBounds.min.y,Is.EqualTo(0).Within(.1f));
            Assert.That(NavMesh.Raycast(a,b,out _,NavMesh.AllAreas),Is.True,"Closed gate must block the direct navigation route.");
            var path=new NavMeshPath(); Assert.That(NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path),Is.True);
            Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),"Side gateways keep an alternate route available.");
            Assert.That(gate.TrySetOpenAmount(1),Is.True); Physics.SyncTransforms();
            yield return null; yield return null; yield return new WaitForSeconds(.15f);
            Assert.That(NavMesh.Raycast(a,b,out var openedHit,NavMesh.AllAreas),Is.False,"Raised gate must restore the direct route; hit="+openedHit.position+" endpoints="+a+" / "+b);
        }

        [UnityTest]
        public IEnumerator HomePreviewMatchesChosenAndShuffledBattlefieldsAndControlsFit()
        {
            var menu = FindMenu();
            Assert.That(menu.transform.Find("Home/Champion showcase"), Is.Null);
            for (int i = 0; i < 3; i++)
            {
                if (i > 0)
                {
                    BattleFrameBehaviorTests.ClickMenu(menu, menu.transform.Find(i == 1 ? "Home/Map Next" : "Home/Shuffle Map"));
                    yield return null;
                }
                Canvas.ForceUpdateCanvases();
                var preview = menu.transform.Find("Home/Battlefield preview/Course minimap").GetComponent<BattleMinimapGraphic>();
                Assert.That(preview.Terrain, Is.SameAs(terrain));
                Assert.That(menu.transform.Find("Home/Map Choice").GetComponent<Text>().text, Is.EqualTo(terrain.MapName));
                Assert.That(terrain.MapKind, Is.EqualTo(terrain.CurrentMapKind));
                var homeRect = (RectTransform)menu.transform.Find("Home");
                var corners = new Vector3[4];
                foreach (var text in homeRect.GetComponentsInChildren<Text>())
                {
                    text.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                        Assert.That(homeRect.rect.Contains(homeRect.InverseTransformPoint(corner)), Is.True, text.name + " must fit on the home screen.");
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name + " must not truncate.");
                }
                BattleFrameBehaviorTests.CaptureUiPreview(menu, "Home-preview-" + i);
                var expected = terrain.CurrentMapKind;
                int footprintCount = terrain.CoverFootprints.Count;
                BattleFrameBehaviorTests.ClickMenu(menu, menu.transform.Find("Home/New Game"));
                yield return null;
                Assert.That(terrain.CurrentMapKind, Is.EqualTo(expected), "Start must honor the preview, including shuffled maps.");
                Assert.That(terrain.CoverFootprints.Count, Is.EqualTo(footprintCount));
                Assert.That(deployment.EnemySpent, Is.EqualTo(deployment.Budget));
                director.ReturnHome();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HomeMapChooserAndThirdCharacterPageWorkWithRealButtons()
        {
            var menu=FindMenu();
            BattleFrameBehaviorTests.ClickMenu(menu,menu.transform.Find("Home/Map Next"));
            Assert.That(terrain.RandomizeBattlefield,Is.False);
            Assert.That(terrain.MapKind,Is.EqualTo(BattleMapKind.BlackstoneMaze));
            yield return null;
            BattleFrameBehaviorTests.ClickMenu(menu,menu.transform.Find("Home/New Game")); yield return null;
            Assert.That(terrain.CurrentMapKind,Is.EqualTo(BattleMapKind.BlackstoneMaze));
            Assert.That(deployment.Definitions.Count,Is.EqualTo(24));
            for(int i=0;i<2;i++) { BattleFrameBehaviorTests.ClickMenu(menu,menu.transform.Find("Deployment/Recruitment panel/Class Next")); yield return null; }
            for(int role=16;role<24;role++)
            {
                var button=menu.transform.Find("Deployment/Recruitment panel/Choose "+(UnitRole)role); Assert.That(button,Is.Not.Null);
                BattleFrameBehaviorTests.ClickMenu(menu,button); yield return null;
                Assert.That(menu.transform.Find("Deployment/Character Details").gameObject.activeSelf,Is.False);
                BattleFrameBehaviorTests.CaptureUiPreview(menu,"New-class-"+(UnitRole)role);
            }
            BattleFrameBehaviorTests.ClickMenu(menu,menu.transform.Find("Deployment/Selected class dossier/Info")); yield return null;
            BattleFrameBehaviorTests.CaptureUiPreview(menu,"Warden-dossier");
            Assert.That(menu.transform.Find("Deployment/Character Details/Class Name").GetComponent<Text>().text,Is.EqualTo("WARDEN"));
        }
        Canvas FindMenu()
        {
            foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>()) if(canvas.name=="Battle Menu") return canvas;
            Assert.Fail("Battle menu missing"); return null;
        }
        void Capture(string name,Vector3 position,Vector3 lookAt)
        {
            string directory=Environment.GetEnvironmentVariable("BATTLEFIELD_PREVIEW_DIR"); if(string.IsNullOrEmpty(directory)) return;
            System.IO.Directory.CreateDirectory(directory);
            var obj=new GameObject("Complex terrain review",typeof(Camera)); obj.transform.position=position; obj.transform.LookAt(lookAt);
            var camera=obj.GetComponent<Camera>(); camera.enabled=false; camera.farClipPlane=800; camera.nearClipPlane=.08f; camera.fieldOfView=60;
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

    public sealed class SiegeAbilityFrameTests
    {
        [UnitySetUp] public IEnumerator SetUp() { yield return new EnterPlayMode(); }
        [UnityTearDown] public IEnumerator TearDown() { yield return new ExitPlayMode(); }
        [UnityTest]
        public IEnumerator IceAndNaturePowersRespectWallsTeamsAndIndependentCooldowns()
        {
            using(var world=new TestBattleWorld())
            {
                var frost=world.AddUnit(BattleTeam.Blue,UnitRole.Frostbinder,Vector3.zero);
                var close=world.AddUnit(BattleTeam.Red,UnitRole.Vanguard,Vector3.forward*2);
                var distant=world.AddUnit(BattleTeam.Red,UnitRole.Vanguard,Vector3.forward*5);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(world.Root.transform,false);
                wall.transform.localPosition=new Vector3(0,4,3.5f); wall.transform.localScale=new Vector3(12,8,.5f);
                yield return null; Physics.SyncTransforms(); world.Director.BeginBattle();
                Assert.That(frost.TryAbility(0,frost.EyePosition,close.AimPosition-frost.EyePosition),Is.True);
                Assert.That(close.IsSlowed,Is.True); Assert.That(distant.Health,Is.EqualTo(100));
                Assert.That(frost.TryAbility(1,frost.EyePosition,Vector3.forward),Is.True); Assert.That(distant.IsSlowed,Is.False);
                Assert.That(frost.TryAbility(2,frost.EyePosition,Vector3.forward),Is.True); Assert.That(frost.Shield,Is.EqualTo(45));
                Assert.That(frost.TryAbility(0,frost.EyePosition,Vector3.forward),Is.False);
            }
            using(var world=new TestBattleWorld())
            {
                var warden=world.AddUnit(BattleTeam.Blue,UnitRole.Warden,Vector3.zero);
                var nearby=world.AddUnit(BattleTeam.Blue,UnitRole.Medic,Vector3.right*2);
                var covered=world.AddUnit(BattleTeam.Blue,UnitRole.Medic,Vector3.forward*2);
                var enemy=world.AddUnit(BattleTeam.Red,UnitRole.Brute,Vector3.left*2);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(world.Root.transform,false);
                wall.transform.localPosition=new Vector3(0,4,1); wall.transform.localScale=new Vector3(4,8,.25f);
                yield return null; Physics.SyncTransforms(); world.Director.BeginBattle(); nearby.TakeDamage(60,enemy); covered.TakeDamage(60,enemy);
                Assert.That(warden.TryAbility(1,warden.EyePosition,Vector3.right),Is.True);
                Assert.That(nearby.Shield,Is.EqualTo(45)); Assert.That(covered.Shield,Is.Zero); Assert.That(enemy.Shield,Is.Zero);
                Assert.That(warden.TryAbility(2,warden.EyePosition,Vector3.right),Is.True);
                Assert.That(nearby.Health,Is.EqualTo(85)); Assert.That(covered.Health,Is.EqualTo(40));
            }
        }
    }
}
