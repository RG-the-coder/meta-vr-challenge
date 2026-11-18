using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace BattlefieldPrototype.Tests
{
    public sealed class SightAndTacticsTests
    {
        TestBattleWorld world;
        NavMeshData navigation;
        NavMeshDataInstance navigationInstance;
        [SetUp] public void SetUp() => world = new TestBattleWorld();
        [TearDown] public void TearDown()
        {
            if (navigationInstance.valid) navigationInstance.Remove();
            if (navigation != null) UnityEngine.Object.DestroyImmediate(navigation);
            world.Dispose();
        }
        internal static void Set(object obj, string field, object value) => obj.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        internal static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(obj, args);
        BattleUnitAI Brain(BattleUnit unit)
        {
            var ai = unit.gameObject.AddComponent<BattleUnitAI>(); ai.Initialize(unit, world.Director); return ai;
        }
        void BakeGround()
        {
            var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource {
                shape = NavMeshBuildSourceShape.Box, size = new Vector3(154, .5f, 186),
                transform = Matrix4x4.TRS(world.Root.transform.position + Vector3.down * .25f, Quaternion.identity, Vector3.one), area = 0 } };
            navigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
                new Bounds(world.Root.transform.position, new Vector3(160, 10, 192)), Vector3.zero, Quaternion.identity);
            Assert.That(navigation, Is.Not.Null);
            navigationInstance = NavMesh.AddNavMeshData(navigation);
        }
        GameObject Wall(Vector3 position)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(world.Root.transform, false); wall.transform.localPosition = position;
            wall.transform.localScale = new Vector3(10, 6, .5f); Physics.SyncTransforms(); return wall;
        }

        [Test]
        public void SightRequiresRangeFacingAndUnobstructedCover()
        {
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var front = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 20);
            var rear = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.back * 2);
            var side = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.right * 5);
            var far = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 70);
            Physics.SyncTransforms();
            Assert.That(actor.CanSee(front), Is.True);
            Assert.That(actor.CanSee(rear), Is.False);
            Assert.That(actor.CanSee(side), Is.False);
            Assert.That(actor.CanSee(far), Is.False);
            var wall = Wall(new Vector3(0, 3, 10));
            Assert.That(actor.CanSee(front), Is.False);
            wall.SetActive(false); actor.Face(Vector3.back); Physics.SyncTransforms();
            Assert.That(actor.CanSee(rear), Is.True);
            Assert.That(actor.CanSee(front), Is.False);
        }

        [Test]
        public void EyesInsideCoverCannotSeeOutThroughTheWall()
        {
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var target = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 20);
            Wall(new Vector3(0, 3, 0));
            Assert.That(actor.CanSee(target), Is.False, "A ray that starts inside a wall must still be blocked.");
            Assert.That(actor.HasLineOfSight(target), Is.False);
            actor.transform.localPosition = Vector3.forward;
            Physics.SyncTransforms();
            Assert.That(actor.CanSee(target), Is.True, "Moving completely clear of the wall restores sight.");
        }

        [Test]
        public void MeshWallsBlockSightFromBothDirectionsRegardlessOfTriangleFacing()
        {
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var target = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 20);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wall.transform.SetParent(world.Root.transform, false);
            wall.transform.localPosition = new Vector3(0, 2, 10);
            wall.transform.localScale = new Vector3(10, 8, 1);
            for (int side = 0; side < 2; side++)
            {
                wall.transform.localRotation = Quaternion.Euler(0, side * 180, 0); Physics.SyncTransforms();
                Assert.That(actor.CanSee(target), Is.False);
                target.Face(Vector3.back); Assert.That(target.CanSee(actor), Is.False);
            }
        }

        [Test]
        public void EveryRoleHasAtLeastOneThirdMapSightAndScoutsSeeFarther()
        {
            var ranges = new HashSet<float>();
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                var unit = world.AddUnit(BattleTeam.Blue, role, Vector3.zero);
                Assert.That(unit.SightRange, Is.GreaterThanOrEqualTo(192f / 3 - .001f));
                ranges.Add(unit.SightRange);
            }
            var tank = world.AddUnit(BattleTeam.Blue, UnitRole.Titan, Vector3.zero);
            var scout = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.zero);
            var sniper = world.AddUnit(BattleTeam.Blue, UnitRole.Sharpshooter, Vector3.zero);
            Assert.That(ranges.Count, Is.EqualTo(4));
            Assert.That(sniper.SightRange, Is.GreaterThan(scout.SightRange));
            Assert.That(scout.SightRange, Is.GreaterThan(tank.SightRange));
        }

        [Test]
        public void TargetSelectionCannotDiscoverHiddenOrRearEnemies()
        {
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Assassin, Vector3.zero);
            world.AddUnit(BattleTeam.Red, UnitRole.Medic, Vector3.back * 2);
            var seen = world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 12);
            Physics.SyncTransforms();
            Assert.That(world.Director.FindPriorityEnemy(actor), Is.SameAs(seen));
            Assert.That(world.Director.FindNearestEnemy(actor), Is.SameAs(seen));
            Wall(new Vector3(0, 3, 6));
            Assert.That(world.Director.FindPriorityEnemy(actor), Is.Null);
            Assert.That(world.Director.FindNearestEnemy(actor), Is.Null);
        }

        [Test]
        public void IncomingDamageTurnsAnUnawareUnitTowardTheImpactWithoutSeeingThroughCover()
        {
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var attacker = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.back * 18);
            var ai = Brain(actor); world.Director.BeginBattle(); Physics.SyncTransforms();
            Assert.That(actor.CanSee(attacker), Is.False);
            actor.TakeDamage(10, attacker); Call(ai, "Update");
            Assert.That(actor.CanSee(attacker), Is.True);
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Pursuing));
            Wall(new Vector3(0, 3, -9));
            Set(ai, "targetRefresh", 0f); Call(ai, "Update");
            Assert.That(actor.CanSee(attacker), Is.False);
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Investigating));
        }

        [Test]
        public void LostContactUsesLastSeenPositionAndThenExpires()
        {
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var target = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 20);
            var ai = Brain(actor); world.Director.BeginBattle(); Physics.SyncTransforms();
            Call(ai, "Update");
            Vector3 known = target.transform.position;
            target.transform.position += Vector3.back * 50; Physics.SyncTransforms();
            Set(ai, "targetRefresh", 0f); Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Investigating));
            Assert.That(ai.LastSeenPosition, Is.EqualTo(known));
            Assert.That(ai.HasEnemyMemory, Is.True);
            Set(ai, "lastSeenAge", BattleUnitAI.EnemyMemorySeconds + 1); Call(ai, "Update");
            Assert.That(ai.HasEnemyMemory, Is.False);
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Searching));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PursuitNeedsThreeContinuousVisibleSecondsAndOwnEnemyOverridesFollowing(bool playerLed)
        {
            var observer = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var leader = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.forward * 12);
            world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 74);
            var observerAi = Brain(observer); var leaderAi = Brain(leader);
            world.Director.BeginBattle(); Physics.SyncTransforms(); Call(leaderAi, "Update");
            Assert.That(leaderAi.IsPursuing, Is.True);
            if (playerLed) leader.SetPossessed(true);
            Set(leader, "<PlanarVelocity>k__BackingField", Vector3.forward * 4);
            for (int i = 0; i < 12; i++) Call(observerAi, "RefreshAwareness", .25f);
            Assert.That(observerAi.FollowedUnit, Is.Null, "An initial glance does not count as three seconds.");
            observer.Face(Vector3.back); Call(observerAi, "RefreshAwareness", .25f);
            observer.Face(Vector3.forward);
            for (int i = 0; i < 12; i++) Call(observerAi, "RefreshAwareness", .25f);
            Assert.That(observerAi.FollowedUnit, Is.Null, "Losing visual contact resets the observation.");
            Call(observerAi, "RefreshAwareness", .25f);
            Assert.That(observerAi.FollowedUnit, Is.SameAs(leader));
            Assert.That(observerAi.HasEnemyMemory, Is.False);
            Assert.That(world.Director.Tactics.Heat(observer, world.Root.transform.position + Vector3.forward * 74), Is.Zero,
                "Recognizing a pursuer must not transfer their enemy knowledge.");
            Call(observerAi, "ActWithoutEnemy");
            Assert.That(observerAi.State, Is.EqualTo(BattleTacticalState.Following));
            world.AddUnit(BattleTeam.Red, UnitRole.Militia, new Vector3(4, 0, 22)); Physics.SyncTransforms();
            Call(observerAi, "RefreshAwareness", .25f);
            Assert.That(observerAi.FollowedUnit, Is.Null);
            Call(observerAi, "Update"); Assert.That(observerAi.State, Is.EqualTo(BattleTacticalState.Pursuing));
        }

        [Test]
        public void HeatBelongsOnlyToTheCharacterWhoSawTheEnemyAndRoundResetClearsIt()
        {
            var observer = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var target = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 20);
            var uninformedAlly = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.back * 60);
            Physics.SyncTransforms();
            Vector3 point = target.transform.position;
            Assert.That(world.Director.Tactics.Heat(observer, point), Is.Zero);
            observer.Face(Vector3.back); world.Director.Tactics.RecordContact(observer, target);
            Assert.That(world.Director.Tactics.Heat(observer, point), Is.Zero);
            observer.Face(Vector3.forward); world.Director.Tactics.RecordContact(observer, target);
            Assert.That(world.Director.Tactics.Heat(observer, point), Is.EqualTo(1).Within(.01f));
            Assert.That(world.Director.Tactics.Heat(uninformedAlly, point), Is.Zero);
            Assert.That(world.Director.Tactics.Heat(target, point), Is.Zero);
            world.Director.RestartBattle();
            Assert.That(world.Director.Tactics.Heat(observer, point), Is.Zero);
        }

        [Test]
        public void PatrolDestinationDoesNotChangeWhenAnUnseenEnemyMovesOrAnAllySpotsThem()
        {
            BakeGround();
            var observer = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var scout = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.forward * 20);
            var target = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 80);
            var ai = Brain(observer); world.Director.BeginBattle(); Physics.SyncTransforms();
            Assert.That(observer.CanSee(target), Is.False);
            Call(ai, "Update");
            var goalField = typeof(BattleUnitAI).GetField("tacticalGoal", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector3 firstGoal = (Vector3)goalField.GetValue(ai);
            world.Director.Tactics.Clear();
            world.Director.Tactics.RecordContact(scout, target);
            Assert.That(world.Director.Tactics.Heat(scout, target.transform.position), Is.GreaterThan(0));
            Assert.That(world.Director.Tactics.Heat(observer, target.transform.position), Is.Zero);
            observer.Face(Vector3.forward); ai.Initialize(observer, world.Director); Call(ai, "Update");
            Assert.That((Vector3)goalField.GetValue(ai), Is.EqualTo(firstGoal));
            Assert.That(ai.HasEnemyMemory, Is.False);
            world.Director.Tactics.Clear();
            target.transform.localPosition = new Vector3(50, 0, -50); Physics.SyncTransforms();
            observer.Face(Vector3.forward); ai.Initialize(observer, world.Director); Call(ai, "Update");
            Assert.That((Vector3)goalField.GetValue(ai), Is.EqualTo(firstGoal));
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Searching));
            Assert.That(ai.HasEnemyMemory, Is.False);
        }

        [Test]
        public void LocalSteeringDoesNotReactToAnEnemyOutsideTheSightCone()
        {
            BakeGround();
            var observer = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var hidden = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.right * 1.05f);
            var ai = Brain(observer); world.Director.BeginBattle(); Physics.SyncTransforms();
            Assert.That(observer.CanSee(hidden), Is.False);
            Call(ai, "TravelTo", observer.transform.position + Vector3.forward * 10, true);
            var move = (Vector3)typeof(BattleUnit).GetField("requestedMove", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(observer);
            Assert.That(move.x, Is.EqualTo(0).Within(.001f), "An unseen enemy must not push the AI's steering away from its patrol route.");
            Assert.That(move.z, Is.GreaterThan(.9f));
        }

        [Test]
        public void WoundedAlliesAndProtectorsMustBeSeenBeforeTheyCanBeChosen()
        {
            var healer = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.zero);
            var ally = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.forward * 8);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 20);
            world.Director.BeginBattle(); ally.TakeDamage(50, enemy); Physics.SyncTransforms();
            Assert.That(world.Director.FindWoundedAlly(healer, 12), Is.SameAs(ally));
            Assert.That(world.Director.FindSupportAnchor(healer, null), Is.SameAs(ally));
            Wall(new Vector3(0, 3, 4));
            Assert.That(world.Director.FindWoundedAlly(healer, 12), Is.Null);
            Assert.That(world.Director.FindSupportAnchor(healer, null), Is.Null);
        }

        [Test]
        public void CriticallyWoundedFighterSeeksReachableHealerAndResumesWhenHealed()
        {
            BakeGround();
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var healer = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, new Vector3(3, 0, 7));
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 16);
            var ai = Brain(actor); world.Director.BeginBattle(); actor.TakeDamage(80, enemy); Physics.SyncTransforms();
            Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.SeekingHealer));
            var goal = (Vector3)typeof(BattleUnitAI).GetField("tacticalGoal", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ai);
            Assert.That(Vector3.Distance(goal, healer.transform.position), Is.LessThan(healer.Definition.AbilityRadius));
            actor.Heal(80); Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Pursuing));
        }

        [Test]
        public void SearchSelectsReachableGroundAndHoldKeepsTheFormation()
        {
            BakeGround();
            var orders = world.Root.AddComponent<BattleOrders>(); orders.Initialize(world.Director);
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.back * 70);
            var ai = Brain(actor); world.Director.BeginBattle(); Physics.SyncTransforms();
            Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Searching));
            var goal = (Vector3)typeof(BattleUnitAI).GetField("tacticalGoal", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ai);
            Assert.That(Vector3.Distance(goal, actor.transform.position), Is.GreaterThan(10));
            Assert.That(NavMesh.SamplePosition(goal, out _, 1, NavMesh.AllAreas), Is.True);
            orders.Issue(BattleOrder.Hold); Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Holding));
            Assert.That(orders.Destination(actor), Is.EqualTo(actor.transform.position));
        }

        [Test]
        public void NewlySpottedEnemyReplacesTheOldPatrolGoalWithCombatPositioning()
        {
            BakeGround();
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.zero);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.back * 70);
            Set(actor, "<AttackCooldownRemaining>k__BackingField", 5f);
            Set(actor, "<AbilityCooldownRemaining>k__BackingField", 5f);
            Set(actor, "<SecondaryAbilityCooldownRemaining>k__BackingField", 5f);
            var ai = Brain(actor); world.Director.BeginBattle(); Physics.SyncTransforms(); Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Searching));
            actor.Face(Vector3.forward); enemy.transform.localPosition = Vector3.forward * 7;
            Physics.SyncTransforms(); Set(ai, "targetRefresh", 0f); Call(ai, "Update");
            Assert.That(ai.State, Is.EqualTo(BattleTacticalState.Fighting));
            var goal = (Vector3)typeof(BattleUnitAI).GetField("tacticalGoal", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ai);
            Assert.That(Vector3.Distance(actor.transform.position, goal), Is.LessThan(8), "Combat repositioning must discard a distant patrol waypoint.");
        }

        [Test]
        public void CombatTempoAppliesToEveryRoleAndActualAttackAndPowerCooldowns()
        {
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                var definition = BattleUnitDefinition.CreateDefault(role);
                try
                {
                    Assert.That(definition.CombatMoveSpeed, Is.EqualTo(definition.MoveSpeed * 1.4f).Within(.001f));
                    Assert.That(definition.CombatAttackCooldown, Is.EqualTo(definition.AttackCooldown * .65f).Within(.001f));
                    Assert.That(definition.CombatProjectileSpeed, Is.EqualTo(definition.ProjectileSpeed * 1.5f).Within(.001f));
                }
                finally { UnityEngine.Object.DestroyImmediate(definition); }
            }
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 30);
            world.Director.BeginBattle(); actor.TryAttack(actor.EyePosition, Vector3.forward);
            actor.TryAbility(1, actor.EyePosition, Vector3.forward);
            Assert.That(actor.AttackCooldownRemaining, Is.EqualTo(.65f).Within(.001f));
            Assert.That(actor.SecondaryAbilityCooldownRemaining, Is.EqualTo(actor.Definition.SecondaryAbilityCooldown * .75f).Within(.001f));
        }
    }

    public sealed class PlayerSightFrameTests
    {
        [UnityTest]
        public IEnumerator RealBattlePatrolsAndSightRestoreTheOriginalSceneAtmosphere()
        {
            yield return new EnterPlayMode();
            var previous = new List<GameObject>();
            foreach (var item in SceneManager.GetActiveScene().GetRootGameObjects())
                if (item.activeSelf) { previous.Add(item); item.SetActive(false); }
            bool fog = RenderSettings.fog;
            float fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
            var root = new GameObject("Sight integration battlefield");
            try
            {
                var bootstrap = root.AddComponent<BattlefieldBootstrap>(); bootstrap.TowerDefenseMode = false;
                bootstrap.RandomizeBattlefield = bootstrap.RandomizePowerBudget = false;
                bootstrap.ArmyPowerBudget = 200;
                yield return null;
                var director = root.GetComponent<BattleDirector>();
                var deployment = root.GetComponent<BattleDeployment>(); deployment.NewGame();
                Assert.That(deployment.TryPlace(UnitRole.Ranger, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(0, 0, -38)), out var ranger), Is.True);
                Assert.That(deployment.TryPlace(UnitRole.Medic, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(-3, 0, -34))), Is.True);
                Assert.That(deployment.TryPlace(UnitRole.Vanguard, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(3, 0, -30))), Is.True);
                foreach (var unit in director.Units) unit.GetComponent<BattleUnitAI>().enabled = false;
                var player = root.GetComponent<PossessionController>();
                director.BeginBattle(); player.Possess(ranger);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(player.ViewCamera.farClipPlane, Is.EqualTo(ranger.SightRange));
                var canvas = root.GetComponent<BattleHud>().TacticalHud.GetComponent<Canvas>();
                BattleFrameBehaviorTests.CaptureUiPreview(canvas, "Sight-Possessed");
                root.SetActive(false);
                Assert.That(RenderSettings.fog, Is.EqualTo(fog));
                Assert.That(RenderSettings.fogStartDistance, Is.EqualTo(fogStart).Within(.001f));
                Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(fogEnd).Within(.001f));
                root.SetActive(true); yield return null;
                player.ReleaseControl(); yield return new WaitForSecondsRealtime(.15f);
                var startingPositions = new Dictionary<BattleUnit, Vector3>();
                foreach (var unit in director.Units)
                { startingPositions[unit] = unit.transform.position; unit.GetComponent<BattleUnitAI>().enabled = true; }
                yield return new WaitForSecondsRealtime(3);
                int moved = 0;
                foreach (var pair in startingPositions)
                    if (pair.Key != null && Vector3.Distance(pair.Value, pair.Key.transform.position) > 2) moved++;
                Assert.That(moved, Is.GreaterThan(3), "Autonomous fighters should patrol or pursue instead of waiting in place.");
                Assert.That(player.HasLimitedSight, Is.False);
                player.enabled = false;
                player.ViewCamera.transform.SetPositionAndRotation(new Vector3(0, 42, -70), Quaternion.LookRotation(new Vector3(0, -35, 70)));
                yield return null;
                BattleFrameBehaviorTests.CaptureUiPreview(canvas, "Sight-Spectator");
            }
            finally
            {
                root.SetActive(false); UnityEngine.Object.Destroy(root);
                foreach (var item in previous) if (item != null) item.SetActive(true);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        static int EnemyMarkerVertices(BattleMinimapGraphic map)
        {
            using (var vertices = new VertexHelper())
            {
                SightAndTacticsTests.Call(map, "OnPopulateMesh", vertices);
                var stream = new List<UIVertex>(); vertices.GetUIVertexStream(stream);
                Color32 enemy = new Color(1, .3f, .2f);
                return stream.FindAll(vertex => vertex.color.Equals(enemy)).Count;
            }
        }
        [UnityTest]
        public IEnumerator PossessionFiltersWorldAndMinimapVisibilityAndSpectatorRestoresThem()
        {
            yield return new EnterPlayMode();
            using (var world = new TestBattleWorld())
            {
                var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
                var near = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 12);
                var far = world.AddUnit(BattleTeam.Red, UnitRole.Militia, Vector3.forward * 80);
                var rear = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.back * 12);
                var meshes = new List<Renderer>();
                foreach (var other in new[] { near, far, rear })
                {
                    var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.transform.SetParent(other.transform, false);
                    body.transform.localPosition = Vector3.up; UnityEngine.Object.Destroy(body.GetComponent<Collider>());
                    var renderer = body.GetComponent<Renderer>(); other.SetVisuals(new[] { renderer }); meshes.Add(renderer);
                }
                var rig = new GameObject("Sight test rig"); rig.transform.SetParent(world.Root.transform, false);
                var cameraObject = new GameObject("Sight camera", typeof(Camera)); cameraObject.transform.SetParent(rig.transform, false);
                var camera = cameraObject.GetComponent<Camera>(); camera.farClipPlane = 500;
                var player = world.Root.AddComponent<PossessionController>();
                player.AssignRig(rig.transform, camera, null); player.Initialize(world.Director);
                world.Director.BeginBattle(); player.Possess(actor);
                var mapObject = new GameObject("Sight test minimap", typeof(RectTransform), typeof(BattleMinimapGraphic));
                mapObject.transform.SetParent(world.Root.transform, false);
                var map = mapObject.GetComponent<BattleMinimapGraphic>(); map.Director = world.Director; map.Player = player;
                map.rectTransform.sizeDelta = new Vector2(240, 280);
                yield return new WaitForSecondsRealtime(.2f);
                Physics.SyncTransforms(); player.SendMessage("LateUpdate");
                Assert.That(player.CurrentUnit, Is.SameAs(actor));
                Assert.That(camera.farClipPlane, Is.EqualTo(actor.SightRange));
                Assert.That(player.CanSeeUnit(near), Is.True); Assert.That(meshes[0].enabled, Is.True);
                Assert.That(player.CanSeeUnit(far), Is.False); Assert.That(meshes[1].enabled, Is.False);
                Assert.That(player.CanSeeUnit(rear), Is.False); Assert.That(meshes[2].enabled, Is.True, "The camera frustum handles bodies behind the player.");
                Assert.That(player.CanSeeMapPoint(far.transform.position), Is.False);
                Assert.That(EnemyMarkerVertices(map), Is.EqualTo(6), "Only the visible enemy has a map marker.");
                Assert.That(map.LastVisibilityRayCount, Is.InRange(1, BattleMinimapGraphic.SightRayBudget));
                int refreshed = map.VisibilityRefreshCount;
                EnemyMarkerVertices(map);
                Assert.That(map.VisibilityRefreshCount, Is.EqualTo(refreshed), "A repeated canvas rebuild must reuse its raycast fan.");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(world.Root.transform, false);
                wall.transform.localPosition = new Vector3(0, 1, 6);
                wall.transform.localScale = new Vector3(12, 12, .5f);
                yield return null;
                Assert.That(player.CanSeeUnit(near), Is.False);
                Assert.That(player.CanSeeMapPoint(near.transform.position), Is.False, "Minimap sight must stop at the wall too.");
                Assert.That(EnemyMarkerVertices(map), Is.Zero);
                Assert.That(meshes[0].enabled, Is.True, "Walls hide bodies in the depth buffer, without disabling whole models.");
                wall.SetActive(false); yield return null;
                Assert.That(player.CanSeeUnit(near), Is.True);
                player.Possess(rear); yield return new WaitForSecondsRealtime(.15f);
                Assert.That(player.CurrentUnit, Is.SameAs(rear));
                Assert.That(meshes[2].enabled, Is.False, "The newly controlled body stays hidden.");
                player.ReleaseControl(); yield return new WaitForSecondsRealtime(.15f);
                Assert.That(player.CurrentUnit, Is.Null);
                Assert.That(player.HasLimitedSight, Is.False); Assert.That(camera.farClipPlane, Is.EqualTo(500));
                foreach (var renderer in meshes) Assert.That(renderer.enabled, Is.True);
                Assert.That(player.CanSeeUnit(far), Is.True); Assert.That(player.CanSeeMapPoint(far.transform.position), Is.True);
                Assert.That(EnemyMarkerVertices(map), Is.EqualTo(12), "Spectator can see both enemy markers.");
            }
            yield return null;
            yield return new ExitPlayMode();
        }
    }
}
