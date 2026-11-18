using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        void LoadTowerMap(TowerMapKind kind)
        {
            battle.GoHome(); Assert.That(battle.SelectMap(kind),Is.True); StartSiege();
        }
        void RaiseScoutTower(float height)
        {
            ((float[])typeof(TowerBattle).GetField("wallets",Private).GetValue(battle))[0]=10000;
            while (battle.BoundTower.Height < height-.01f) Assert.That(battle.BoundTower.TryBuild(BattleTeam.Blue),Is.True);
            battle.ReturnToTower(); Physics.SyncTransforms();
        }

        [UnityTest] public IEnumerator FiveBattlefieldsHaveDistinctTowerCountsValidSpawnsAndConnectedGroundRoutes()
        {
            var counts=new HashSet<int>();
            foreach (TowerMapKind kind in System.Enum.GetValues(typeof(TowerMapKind)))
            {
                LoadTowerMap(kind); yield return Simulate(.4f);
                Assert.That(battle.Map,Is.EqualTo(kind)); Assert.That(battle.MapName,Is.EqualTo(TowerMapCatalog.Name(kind)));
                Assert.That(battle.Towers.Count,Is.EqualTo(TowerMapCatalog.Spawns(kind).Count)); counts.Add(battle.Towers.Count);
                Assert.That(battle.OwnedCount(BattleTeam.Blue),Is.EqualTo(2)); Assert.That(battle.OwnedCount(BattleTeam.Red),Is.EqualTo(2));
                Assert.That(battle.BoundTower,Is.SameAs(battle.Towers[0])); Assert.That(battle.EnemyBoundTower,Is.SameAs(battle.Towers[1]));
                var positions=new HashSet<Vector3>(); var path=new NavMeshPath();
                Vector3 start=battle.BoundTower.transform.position+Vector3.back*7;
                Assert.That(NavMesh.SamplePosition(start,out var source,1,NavMesh.AllAreas),Is.True,kind+" source");
                foreach (var tower in battle.Towers)
                {
                    Assert.That(positions.Add(tower.transform.position),Is.True);
                    Bounds foundation=new Bounds(tower.transform.position+Vector3.up,new Vector3(8,2,8));
                    foreach(var wall in terrain.TowerSightBlockers) Assert.That(wall.Intersects(foundation),Is.False,tower.Title+" overlaps a high wall.");
                    Assert.That(NavMesh.SamplePosition(tower.transform.position+Vector3.back*7,out var target,1.5f,NavMesh.AllAreas),Is.True,kind+" / "+tower.Title);
                    Assert.That(NavMesh.CalculatePath(source.position,target.position,NavMesh.AllAreas,path),Is.True);
                    Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),kind+" / "+tower.Title+" must be reachable through the gates.");
                }
                Vector3 center=Vector3.zero;
                CaptureOverview("Layout-"+kind,new Vector3(110,145,-160),center);
            }
            CollectionAssert.AreEquivalent(new[]{5,7,9,11,13},counts);
        }

        [UnityTest] public IEnumerator HighWallsBlockPhysicalSightAndScoutingUntilTheTowerIsTallEnoughAndDamageRestoresTheBlindSpot()
        {
            foreach (var kind in new[]{TowerMapKind.IronDivide,TowerMapKind.SkyCitadel})
            {
                LoadTowerMap(kind); yield return Simulate(.3f);
                Vector3 target=kind==TowerMapKind.IronDivide ? new Vector3(12,0,78) : new Vector3(64,0,battle.BoundTower.transform.position.z);
                var enemy=Spawn(BattleTeam.Red,UnitRole.Vanguard,target);
                float low=kind==TowerMapKind.IronDivide ? 30 : 33; RaiseScoutTower(low);
                Assert.That(Vector3.ProjectOnPlane(enemy.EyePosition-battle.BoundTower.transform.position,Vector3.up).magnitude,Is.LessThan(battle.ScoutRadius),"This must test obstruction, not only range.");
                Assert.That(battle.IsScouted(enemy.EyePosition),Is.False,kind+" must hide enemies behind its high wall.");
                Assert.That(Physics.Linecast(battle.BoundTower.Summit,enemy.EyePosition,out var blocked,~0,QueryTriggerInteraction.Ignore),Is.True);
                Assert.That(blocked.collider.name,Is.EqualTo("High sight-blocking wall"));
                CaptureOverview(kind+"-blocked-view",player.ViewCamera.transform.position,enemy.EyePosition);
                RaiseScoutTower(36); Assert.That(battle.IsScouted(enemy.EyePosition),Is.True,kind+" must reward building a near-maximum tower.");
                Assert.That(Physics.Linecast(battle.BoundTower.Summit,enemy.EyePosition,out var clear,~0,QueryTriggerInteraction.Ignore),Is.True);
                Assert.That(clear.collider.GetComponentInParent<BattleUnit>(),Is.SameAs(enemy));
                CaptureOverview(kind+"-high-tower-view",player.ViewCamera.transform.position,enemy.EyePosition);
                battle.BoundTower.TakeDamage((36-low)*BattleTower.HealthPerMetre,BattleTeam.Red); battle.ReturnToTower();
                Assert.That(battle.IsScouted(enemy.EyePosition),Is.False,"Losing height must lose the view again.");
            }
        }

        [UnityTest] public IEnumerator HighWallsAlsoHideEnemyMinimapMarkersAndTallerTowersRevealThem()
        {
            LoadTowerMap(TowerMapKind.SkyCitadel);
            var enemy=Spawn(BattleTeam.Red,UnitRole.Vanguard,new Vector3(64,0,battle.BoundTower.transform.position.z));
            RaiseScoutTower(33); battle.BeginRoutePlanning(); hud.Refresh(); yield return null;
            int hidden;
            var populate=typeof(TowerMinimapGraphic).GetMethod("OnPopulateMesh",Private,null,new[]{typeof(VertexHelper)},null);
            using(var vertices=new VertexHelper()) { populate.Invoke(hud.Minimap,new object[]{vertices}); hidden=vertices.currentVertCount; }
            Assert.That(battle.IsScouted(enemy.EyePosition),Is.False);
            RaiseScoutTower(36); battle.BeginRoutePlanning(); hud.Refresh();
            using(var vertices=new VertexHelper())
            { populate.Invoke(hud.Minimap,new object[]{vertices}); Assert.That(vertices.currentVertCount,Is.EqualTo(hidden+4),"Only the newly visible enemy marker adds geometry; static walls stay known."); }
            Assert.That(battle.IsScouted(enemy.EyePosition),Is.True); hud.SendMessage("LateUpdate"); CaptureHud("High-wall-scouting-map");
        }

        [UnityTest] public IEnumerator LargerTowerNetworksExposeEveryTowerAndAllOwnedBuildRings()
        {
            LoadTowerMap(TowerMapKind.SkyCitadel); hud.Refresh(); yield return null; hud.Refresh(); Invoke(hud,"CancelPointer");
            Invoke(hud,"SelectCommandPage",1); yield return null; Canvas.ForceUpdateCanvases();
            var panel=hud.Canvas.transform.Find("Live command HUD/Tower network");
            Assert.That(panel.Find("Next towers").GetComponent<Button>().interactable,Is.True);
            Click(panel.Find("Next towers"));
            Assert.That((int)typeof(TowerBattleHud).GetField("towerPage",Private).GetValue(hud),Is.EqualTo(1),"Clicked: "+typeof(TowerBattleHud).GetField("hovered",Private).GetValue(hud));
            yield return null; hud.Refresh();
            for(int i=0;i<6;i++) Assert.That(panel.Find("Tower "+i).GetComponentInChildren<Text>().text,Does.Contain(battle.Towers[7+i].Title));
            Assert.That(panel.Find("Tower 6").gameObject.activeSelf,Is.False);
            Click(panel.Find("Tower 5")); var selected=battle.Towers[12]; Assert.That(battle.SelectedTower,Is.SameAs(selected));
            Spawn(BattleTeam.Blue,UnitRole.Vanguard,selected.transform.position+Vector3.right*7);
            Assert.That(selected.TryBuild(BattleTeam.Blue),Is.True); Assert.That(battle.Teleport(selected),Is.True);
            battle.ToggleMount(); Assert.That(battle.ChooseDefense(DefenseKind.Caltrops),Is.True); hud.Refresh(); hud.SendMessage("LateUpdate");
            var rings=(LineRenderer[])typeof(TowerBattleHud).GetField("defenseRings",Private).GetValue(hud);
            Assert.That(rings.Length,Is.GreaterThanOrEqualTo(13)); Assert.That(rings[12].enabled,Is.True);
            CaptureHud("Thirteen-tower-network-page-two");
            Click(panel.Find("Previous towers")); yield return null; hud.Refresh();
            Assert.That(panel.Find("Tower 0").GetComponentInChildren<Text>().text,Does.Contain(battle.Towers[0].Title));
        }

        [UnityTest] public IEnumerator HomeMapCardsPreviewLayoutsStartTheChosenMapAndCannotChangeAMatchInProgress()
        {
            hud.Refresh(); yield return null; hud.Refresh(); Invoke(hud,"CancelPointer");
            var card=hud.Canvas.transform.Find("Home and outcome veil/Skyhold briefing"); var picker=card.Find("Battlefield selection");
            Assert.That(picker.Find("Tower layout preview").GetComponent<CanvasRenderer>(),Is.Not.Null);
            foreach(TowerMapKind kind in System.Enum.GetValues(typeof(TowerMapKind)))
            {
                Click(picker.Find("Map "+kind)); yield return null; hud.Refresh(); Canvas.ForceUpdateCanvases();
                Assert.That(battle.Map,Is.EqualTo(kind)); Assert.That(battle.Towers.Count,Is.EqualTo(TowerMapCatalog.Spawns(kind).Count));
                foreach(string name in new[]{"Briefing","Briefing detail","Battlefield selection/Map description"})
                {
                    var label=card.Find(name).GetComponent<Text>();
                    Assert.That(label.preferredHeight,Is.LessThanOrEqualTo(label.rectTransform.rect.height+1),name+" must fit for "+kind);
                }
                CaptureHud("Map-selection-"+kind);
            }
            var previous=battle.Map; Click(picker.Find("Random battlefield")); yield return null; hud.Refresh();
            Assert.That(battle.Map,Is.Not.EqualTo(previous)); var chosen=battle.Map;
            Click(card.Find("Begin siege")); yield return null;
            FreezeEnemy();
            Assert.That(director.IsBattleRunning,Is.True); Assert.That(battle.Map,Is.EqualTo(chosen));
            int count=battle.Towers.Count; Assert.That(battle.SelectMap(TowerMapKind.OpenMarch),Is.False); Assert.That(battle.Towers.Count,Is.EqualTo(count));
        }

        [UnityTest] public IEnumerator RestartKeepsTheLayoutButResetsArmiesDefensesPowersRoutesAndChangingMapRemovesOldWalls()
        {
            LoadTowerMap(TowerMapKind.SkyCitadel); FundWaveUpgrades();
            battle.TryUpgradeTowerPulse(); battle.TryUpgradeTowerPulse();
            Spawn(BattleTeam.Blue,UnitRole.Vanguard,battle.BoundTower.transform.position+Vector3.forward*12);
            battle.BeginRoutePlanning(); battle.AddRouteWaypoint(battle.BoundTower.transform.position+Vector3.forward*20); Assert.That(battle.StartTroopRoute(),Is.True);
            battle.ToggleMount(); Assert.That(battle.TryPlaceDefense(DefenseKind.Caltrops,battle.BoundTower.transform.position+new Vector3(-10,0,15),0,out _),Is.True,battle.Message);
            director.RestartBattle(); FreezeEnemy();
            Assert.That(battle.Map,Is.EqualTo(TowerMapKind.SkyCitadel)); Assert.That(battle.Towers.Count,Is.EqualTo(13));
            Assert.That(battle.BlueGold,Is.EqualTo(TowerBattle.StartingGold)); Assert.That(director.Units.Count,Is.Zero); Assert.That(battle.Defenses.Count,Is.Zero);
            Assert.That(battle.BoundTower.PulseLevel,Is.EqualTo(1)); Assert.That(battle.TroopOrders.Current,Is.EqualTo(BattleOrder.Advance));
            Assert.That(terrain.TowerSightBlockers.Count,Is.EqualTo(3));
            battle.GoHome(); Assert.That(battle.SelectMap(TowerMapKind.OpenMarch),Is.True); yield return null;
            Assert.That(battle.Towers.Count,Is.EqualTo(7)); Assert.That(terrain.TowerSightBlockers.Count,Is.Zero);
            foreach(var collider in root.GetComponentsInChildren<Collider>()) Assert.That(collider.name,Is.Not.EqualTo("High sight-blocking wall"));
        }

        [UnityTest] public IEnumerator LowFlyingTroopsCanUseHighWallGatesWithoutNeedingAPlayerRoute()
        {
            LoadTowerMap(TowerMapKind.SkyCitadel); yield return Simulate(.4f);
            var flyer=Spawn(BattleTeam.Blue,UnitRole.Skywing,new Vector3(-20,6,-50)); var ai=flyer.GetComponent<BattleUnitAI>();
            Vector3 goal=new Vector3(30,0,-50); bool crossed=false; float previousX=flyer.transform.position.x;
            Time.timeScale=6; float deadline=Time.time+35;
            while(Vector3.ProjectOnPlane(flyer.transform.position-goal,Vector3.up).magnitude>2 && Time.time<deadline)
            {
                Invoke(ai,"TravelTo",goal,true); yield return null;
                if(previousX<0 && flyer.transform.position.x>=0)
                {
                    float z=flyer.transform.position.z; crossed=true;
                    Assert.That(Mathf.Abs(z+terrain.ArenaSize.y*.12f)<7.5f || Mathf.Abs(z-terrain.ArenaSize.y*.12f)<7.5f,Is.True,"A low flyer must cross at an actual gate.");
                }
                previousX=flyer.transform.position.x;
            }
            Assert.That(crossed,Is.True); Assert.That(Vector3.ProjectOnPlane(flyer.transform.position-goal,Vector3.up).magnitude,Is.LessThanOrEqualTo(2));
        }
    }
}
