using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    // These tests exercise combat, controls, and match contracts. Frame-dependent
    // tests enter Play Mode for projectiles, possession, input, and HUD layout.
    sealed class TestBattleWorld : IDisposable
    {
        public readonly GameObject Root;
        public readonly BattleDirector Director;
        readonly List<BattleUnitDefinition> definitions = new List<BattleUnitDefinition>();

        public TestBattleWorld()
        {
            Root = new GameObject("Battle behavior test world");
            Root.transform.position = new Vector3(1000f, 0f, 1000f);
            Director = Root.AddComponent<BattleDirector>();
        }

        public BattleUnit AddUnit(BattleTeam team, UnitRole role, Vector3 position,
            float armor = 0f, float health = 100f)
        {
            var definition = ScriptableObject.CreateInstance<BattleUnitDefinition>();
            definition.hideFlags = HideFlags.DontSave;
            definition.Role = role;
            definition.MaxHealth = health;
            definition.ArmorFraction = armor;
            definition.Damage = 20f;
            definition.AbilityPower = 30f;
            definition.AbilityRadius = 3f;
            definition.AbilityCooldown = 5f;
            definition.AttackCooldown = 1f;
            definition.AttackRange = role == UnitRole.Vanguard ? 2.2f : 10f;
            definition.ProjectileSpeed = 20f;
            definitions.Add(definition);
            var root = new GameObject(team + " " + role);
            root.transform.SetParent(Root.transform, false);
            root.transform.localPosition = position;
            var motor = root.AddComponent<CharacterController>();
            motor.height = 1.8f;
            motor.radius = 0.42f;
            motor.center = new Vector3(0f, 0.9f, 0f);
            var unit = root.AddComponent<BattleUnit>();
            unit.Configure(Director, definition, team);
            return unit;
        }

        public void Dispose()
        {
            if (Application.isPlaying)
            {
                if (Root != null)
                {
                    Root.SetActive(false); // Deferred destruction must not leak colliders into the next test world.
                    UnityEngine.Object.Destroy(Root);
                }
                foreach (var definition in definitions)
                    if (definition != null) UnityEngine.Object.Destroy(definition);
            }
            else
            {
                if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
                foreach (var definition in definitions)
                    if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            }
            definitions.Clear();
        }
    }

    public sealed class BattleCombatAndMatchTests
    {
        TestBattleWorld world;

        [SetUp] public void SetUp() => world = new TestBattleWorld();
        [TearDown] public void TearDown() => world.Dispose();

        [Test]
        public void ArmorReducesEnemyDamageAndFriendlyFireDoesNotHurt()
        {
            var victim = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero, 0.25f);
            var ally = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.left * 10f);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.forward * 10f);
            world.Director.BeginBattle();

            victim.TakeDamage(40f, ally);
            Assert.That(victim.Health, Is.EqualTo(100f));
            victim.TakeDamage(40f, enemy);
            Assert.That(victim.Health, Is.EqualTo(70f).Within(0.001f));
        }

        [Test]
        public void VanguardGuardReducesIncomingDamageDuringItsAbility()
        {
            var vanguard = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero, 0.25f);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.forward * 10f);
            world.Director.BeginBattle();

            Assert.That(vanguard.TryAbility(vanguard.EyePosition, Vector3.forward), Is.True);
            vanguard.TakeDamage(40f, enemy);
            Assert.That(vanguard.IsGuarding, Is.True);
            Assert.That(vanguard.Health, Is.EqualTo(88f).Within(0.001f));
        }

        [Test]
        public void VanguardAbilityHitsForwardEnemyButNotRearEnemyOrAlly()
        {
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var front = world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.forward * 2f);
            var rear = world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.back * 2f);
            var ally = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.right * 2f);
            world.Director.BeginBattle();
            Physics.SyncTransforms();

            actor.TryAbility(actor.EyePosition, Vector3.forward);
            Assert.That(front.Health, Is.EqualTo(70f).Within(0.001f));
            Assert.That(rear.Health, Is.EqualTo(100f));
            Assert.That(ally.Health, Is.EqualTo(100f));
        }

        [Test]
        public void SolidCoverBlocksMeleeAndVanguardAreaDamage()
        {
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.forward * 2f);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.transform.SetParent(world.Root.transform, false);
            cover.transform.localPosition = new Vector3(0f, 1.5f, 1f);
            cover.transform.localScale = new Vector3(3f, 3f, 0.25f);
            world.Director.BeginBattle();
            Physics.SyncTransforms();

            Assert.That(actor.HasLineOfSight(enemy), Is.False);
            actor.TryAttack(actor.EyePosition, Vector3.forward);
            actor.TryAbility(actor.EyePosition, Vector3.forward);
            Assert.That(enemy.Health, Is.EqualTo(100f));
        }

        [Test]
        public void MedicPulseHealsNearbyAlliesAndSelfButNotEnemiesOrDistantAllies()
        {
            var medic = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.zero);
            var nearby = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.right * 2f);
            var distant = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.left * 8f);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 2f);
            world.Director.BeginBattle();
            medic.TakeDamage(50f, enemy);
            nearby.TakeDamage(50f, enemy);
            distant.TakeDamage(50f, enemy);
            enemy.TakeDamage(50f, medic);

            Assert.That(medic.TryAbility(medic.EyePosition, Vector3.forward), Is.True);
            Assert.That(medic.Health, Is.EqualTo(80f));
            Assert.That(nearby.Health, Is.EqualTo(80f));
            Assert.That(distant.Health, Is.EqualTo(50f));
            Assert.That(enemy.Health, Is.EqualTo(50f));
        }

        [Test]
        public void HealingCapsAtMaximumAndDoesNotReviveDeadUnits()
        {
            var survivor = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.zero);
            var victim = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.right * 2f);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 10f);
            world.Director.BeginBattle();
            survivor.TakeDamage(20f, enemy);
            survivor.Heal(1000f);
            victim.TakeDamage(1000f, enemy);
            victim.Heal(1000f);

            Assert.That(survivor.Health, Is.EqualTo(survivor.MaxHealth));
            Assert.That(victim.Health, Is.Zero);
            Assert.That(victim.IsAlive, Is.False);
            Assert.That(world.Director.IsBattleRunning, Is.True);
        }

        [Test]
        public void CombatWaitsForStartAndCooldownPreventsRepeatedActions()
        {
            var actor = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 10f);
            Assert.That(actor.TryAttack(actor.EyePosition, Vector3.forward), Is.False);
            Assert.That(actor.TryAbility(actor.EyePosition, Vector3.forward), Is.False);
            Assert.That(actor.AttackCooldownRemaining, Is.Zero);
            world.Director.BeginBattle();

            Assert.That(actor.TryAttack(actor.EyePosition, Vector3.zero), Is.False);
            Assert.That(actor.TryAttack(actor.EyePosition, Vector3.forward), Is.True);
            Assert.That(actor.TryAttack(actor.EyePosition, Vector3.forward), Is.False);
            Assert.That(actor.TryAbility(actor.EyePosition, Vector3.forward), Is.True);
            Assert.That(actor.TryAbility(actor.EyePosition, Vector3.forward), Is.False);
        }

        [Test]
        public void BattleCannotStartWithoutBothLivingArmies()
        {
            world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            LogAssert.Expect(LogType.Warning, "Both armies need living units before the battle can start.");
            world.Director.BeginBattle();
            Assert.That(world.Director.Phase, Is.EqualTo(BattlePhase.Setup));
            world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 10f);
            world.Director.BeginBattle();
            Assert.That(world.Director.IsBattleRunning, Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-10f)]
        public void InvalidDamageAndHealingCannotCorruptHealth(float amount)
        {
            var victim = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 10f);
            world.Director.BeginBattle();
            victim.TakeDamage(20f, enemy);
            victim.TakeDamage(amount, enemy);
            victim.Heal(amount);
            Assert.That(victim.Health, Is.EqualTo(80f));
        }

        [TestCase(BattleTeam.Red, BattlePhase.Victory)]
        [TestCase(BattleTeam.Blue, BattlePhase.Defeat)]
        public void LastUnitDeathEndsTheRoundAndFreezesFurtherCombat(BattleTeam losingTeam, BattlePhase expected)
        {
            var blue = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var red = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 10f);
            var loser = losingTeam == BattleTeam.Blue ? blue : red;
            var winner = losingTeam == BattleTeam.Blue ? red : blue;
            BattlePhase phaseDuringDeathEvent = BattlePhase.Setup;
            world.Director.UnitDied += unit => phaseDuringDeathEvent = world.Director.Phase;
            world.Director.BeginBattle();
            loser.TakeDamage(1000f, winner);

            Assert.That(world.Director.Phase, Is.EqualTo(expected));
            Assert.That(phaseDuringDeathEvent, Is.EqualTo(expected));
            Assert.That(world.Director.IsBattleRunning, Is.False);
            winner.TakeDamage(1000f, loser);
            Assert.That(winner.IsAlive, Is.True);
            Assert.That(winner.TryAttack(winner.EyePosition, Vector3.forward), Is.False);
        }

        [Test]
        public void RestartSpawnsFreshArmiesBeforeResetListenersAndCanStartAgain()
        {
            var blue = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var red = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 10f);
            world.Director.BeginBattle();
            red.TakeDamage(1000f, blue);
            blue.SetPossessed(true);
            int spawns = 0;
            bool resetSawFreshRound = false;
            world.Director.Initialize(() =>
            {
                spawns++;
                world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.left * 10f);
                world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.right * 10f);
            });
            world.Director.RoundReset += () => resetSawFreshRound =
                world.Director.Phase == BattlePhase.Setup && world.Director.Units.Count == 2;

            world.Director.RestartBattle();
            Assert.That(spawns, Is.EqualTo(1));
            Assert.That(resetSawFreshRound, Is.True);
            Assert.That(blue.IsPossessed, Is.False);
            Assert.That(world.Director.Units, Has.No.Member(blue));
            Assert.That(world.Director.AliveBlue, Is.EqualTo(1));
            Assert.That(world.Director.AliveRed, Is.EqualTo(1));
            world.Director.BeginBattle();
            Assert.That(world.Director.IsBattleRunning, Is.True);
        }

        [Test]
        public void AssigningVisualsBeforeConfigurationStillProducesVisibleLivingUnits()
        {
            var unitObject = new GameObject("Visual initialization scenario");
            unitObject.transform.SetParent(world.Root.transform);
            var unit = unitObject.AddComponent<BattleUnit>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(unitObject.transform);
            var renderer = body.GetComponent<Renderer>();
            unit.SetVisuals(new[] { renderer });
            var configured = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            unit.Configure(world.Director, configured.Definition, BattleTeam.Blue);

            Assert.That(renderer.enabled, Is.True);
            unit.SetPossessed(true);
            Assert.That(renderer.enabled, Is.False);
            unit.SetPossessed(false);
            Assert.That(renderer.enabled, Is.True);
        }
    }

    public sealed class BattleNewRoleTests
    {
        [Test]
        public void NewRoleDefaultsGiveSkirmisherSpeedAndBruteDurability()
        {
            var ranger = BattleUnitDefinition.CreateDefault(UnitRole.Ranger);
            var skirmisher = BattleUnitDefinition.CreateDefault(UnitRole.Skirmisher);
            var brute = BattleUnitDefinition.CreateDefault(UnitRole.Brute);
            try
            {
                Assert.That(skirmisher.DisplayName, Is.EqualTo("Skirmisher"));
                Assert.That(skirmisher.AttackCooldown, Is.LessThan(ranger.AttackCooldown));
                Assert.That(skirmisher.MoveSpeed, Is.GreaterThan(ranger.MoveSpeed));
                Assert.That(brute.DisplayName, Is.EqualTo("Brute"));
                Assert.That(brute.MaxHealth, Is.GreaterThan(skirmisher.MaxHealth));
                Assert.That(brute.ArmorFraction, Is.GreaterThan(skirmisher.ArmorFraction));
                Assert.That(brute.AttackRange, Is.LessThan(skirmisher.AttackRange));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ranger);
                UnityEngine.Object.DestroyImmediate(skirmisher);
                UnityEngine.Object.DestroyImmediate(brute);
            }
        }

        [Test]
        public void BruteSlamHitsEnemiesAllAroundButRespectsCoverAndAllies()
        {
            using (var world = new TestBattleWorld())
            {
                var brute = world.AddUnit(BattleTeam.Blue, UnitRole.Brute, Vector3.zero);
                var front = world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.forward * 2f);
                var rear = world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.back * 2f);
                var covered = world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.right * 2f);
                var ally = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.left * 2f);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(world.Root.transform, false);
                wall.transform.localPosition = new Vector3(1f, 1.5f, 0f);
                wall.transform.localScale = new Vector3(0.2f, 3f, 3f);
                world.Director.BeginBattle();
                Physics.SyncTransforms();

                Assert.That(brute.IsMelee, Is.True);
                Assert.That(brute.TryAbility(brute.EyePosition, Vector3.forward), Is.True);
                Assert.That(front.Health, Is.EqualTo(70f));
                Assert.That(rear.Health, Is.EqualTo(70f));
                Assert.That(covered.Health, Is.EqualTo(100f));
                Assert.That(ally.Health, Is.EqualTo(100f));
            }
        }

        [Test]
        public void SkirmisherOverdriveEnablesBuffWithoutBypassingAbilityCooldown()
        {
            using (var world = new TestBattleWorld())
            {
                var skirmisher = world.AddUnit(BattleTeam.Blue, UnitRole.Skirmisher, Vector3.zero);
                world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 10f);
                world.Director.BeginBattle();
                Assert.That(skirmisher.IsMelee, Is.False);
                Assert.That(skirmisher.TryAbility(skirmisher.EyePosition, Vector3.forward), Is.True);
                Assert.That(skirmisher.IsOverdriving, Is.True);
                Assert.That(skirmisher.AbilityCooldownRemaining, Is.GreaterThan(0f));
                Assert.That(skirmisher.TryAbility(skirmisher.EyePosition, Vector3.forward), Is.False);
            }
        }

        [Test]
        public void HandBeyondCoverCannotFireOrSpendRangerCooldowns()
        {
            using (var world = new TestBattleWorld())
            {
                var ranger = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.zero);
                world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 6f);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(world.Root.transform, false);
                wall.transform.localPosition = new Vector3(0f, 1.5f, 0.5f);
                wall.transform.localScale = new Vector3(4f, 3f, 0.1f);
                world.Director.BeginBattle();
                Physics.SyncTransforms();
                Vector3 handBeyondWall = ranger.EyePosition + Vector3.forward;
                Assert.That(ranger.TryAttack(handBeyondWall, Vector3.forward), Is.False);
                Assert.That(ranger.TryAbility(handBeyondWall, Vector3.forward), Is.False);
                Assert.That(ranger.AttackCooldownRemaining, Is.Zero);
                Assert.That(ranger.AbilityCooldownRemaining, Is.Zero);
                Assert.That(world.Root.GetComponentsInChildren<BattleProjectile>(), Is.Empty);
            }
        }
    }

    public sealed class BattleFrameBehaviorTests
    {
        TestBattleWorld world;
        readonly List<AudioListener> disabledListeners = new List<AudioListener>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            // The active Editor scene may already contain its own camera listener.
            // Keep its state for teardown while this fixture owns the player rig.
            foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>())
            {
                if (!listener.enabled) continue;
                listener.enabled = false;
                disabledListeners.Add(listener);
            }
            world = new TestBattleWorld();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Isolated test floor";
            floor.transform.SetParent(world.Root.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(100f, 0.5f, 100f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            world?.Dispose();
            yield return null;
            foreach (var listener in disabledListeners) if (listener != null) listener.enabled = true;
            disabledListeners.Clear();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RangerBurstShootsThroughAlliesAndCooldownBlocksASecondBurst()
        {
            Vector3 origin = Vector3.zero;
            var ranger = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, origin);
            var ally = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, origin + Vector3.forward * 2f);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, origin + Vector3.forward * 4f);
            world.Director.BeginBattle();
            Physics.SyncTransforms();
            Assert.That(ranger.TryAbility(ranger.EyePosition, Vector3.forward), Is.True);
            Assert.That(world.Root.GetComponentsInChildren<BattleProjectile>().Length, Is.EqualTo(3));
            Assert.That(ranger.TryAbility(ranger.EyePosition, Vector3.forward), Is.False);

            float deadline = Time.realtimeSinceStartup + 1f;
            while (enemy.Health >= 100f && world.Root.GetComponentInChildren<BattleProjectile>() != null &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ally.Health, Is.EqualTo(100f));
            var remaining = world.Root.GetComponentsInChildren<BattleProjectile>();
            string projectilePositions = string.Empty;
            foreach (var projectile in remaining) projectilePositions += projectile.transform.position + " ";
            Assert.That(enemy.Health, Is.LessThan(100f),
                $"Phase {world.Director.Phase}; ranger {ranger.transform.position}; enemy {enemy.transform.position}; remaining {projectilePositions}");
            Assert.That(ranger.AbilityCooldownRemaining, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator PossessedUnitDeathHandsControlToAnotherLivingAlly()
        {
            Vector3 origin = Vector3.zero;
            var first = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, origin);
            var replacement = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, origin + Vector3.right * 5f);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Ranger, origin + Vector3.forward * 20f);
            var controller = world.Root.AddComponent<PossessionController>();
            controller.Initialize(world.Director);
            world.Director.BeginBattle();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.SameAs(first));
            Assert.That(first.IsPossessed, Is.True);

            first.TakeDamage(1000f, enemy);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.SameAs(replacement));
            Assert.That(replacement.IsPossessed, Is.True);
            Assert.That(first.IsPossessed, Is.False);
        }

        [UnityTest]
        public IEnumerator PossessionMovesNestedRigToUnitEyesAndRoomscaleCannotCrossCover()
        {
            world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var target = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, new Vector3(7f, 0f, 4f));
            world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.forward * 20f);
            var rig = new GameObject("Offset external rig").transform;
            rig.SetParent(world.Root.transform, false);
            rig.localPosition = new Vector3(-3f, 0.4f, -5f);
            rig.localRotation = Quaternion.Euler(0f, 37f, 0f);
            var trackingSpace = new GameObject("Nested tracking origin").transform;
            trackingSpace.SetParent(rig, false);
            trackingSpace.localPosition = new Vector3(0.6f, -0.7f, 0.4f);
            var head = new GameObject("Offset tracked head", typeof(Camera)).GetComponent<Camera>();
            head.transform.SetParent(trackingSpace, false);
            head.transform.localPosition = new Vector3(0.3f, 2.2f, -0.25f);
            head.nearClipPlane = 0.05f;
            var controller = world.Root.AddComponent<PossessionController>();
            controller.AssignRig(rig, head, null);
            controller.Initialize(world.Director);
            yield return new WaitForSecondsRealtime(0.2f);
            controller.enabled = false;
            typeof(PossessionController).GetProperty(nameof(PossessionController.IsUsingXR)).SetValue(controller, true);
            Vector3 targetPosition = target.transform.position;
            Assert.That(controller.Possess(target), Is.True);
            yield return new WaitForSecondsRealtime(0.1f);
            controller.SendMessage("UpdateSwitch", SendMessageOptions.RequireReceiver);
            controller.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Assert.That(controller.CurrentUnit, Is.SameAs(target));
            Assert.That(Vector3.Distance(head.transform.position, target.EyePosition), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(target.transform.position, targetPosition), Is.LessThan(0.001f));

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(world.Root.transform, false);
            wall.transform.localPosition = new Vector3(8f, 2.5f, 4f);
            wall.transform.localScale = new Vector3(0.2f, 5f, 20f);
            Physics.SyncTransforms();
            Vector3 originalHeadLocal = head.transform.localPosition;
            Vector3 originalRigPosition = rig.position;
            head.transform.position += Vector3.right * 6f; // Simulate a large raw tracked-head step.
            controller.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            float nearSide = wall.GetComponent<Collider>().bounds.min.x;
            Assert.That(head.transform.position.x, Is.LessThan(nearSide - 0.1f));
            Assert.That(Vector3.Distance(target.transform.position, targetPosition), Is.LessThan(0.001f));
            controller.SendMessage("UpdateTrackedPoses", SendMessageOptions.RequireReceiver);
            Assert.That(head.transform.position.x, Is.LessThan(nearSide - 0.1f));
            head.transform.localPosition = originalHeadLocal;
            controller.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Assert.That(Vector3.Distance(head.transform.position, target.EyePosition), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(rig.position, originalRigPosition), Is.LessThan(0.001f), "Leaning back must restore the tracking origin.");

            controller.ReleaseControl();
            yield return new WaitForSecondsRealtime(0.1f);
            controller.SendMessage("UpdateSwitch", SendMessageOptions.RequireReceiver);
            controller.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Assert.That(controller.CurrentUnit, Is.Null);
            controller.SendMessage("MoveSpectator", Vector3.right * 20f, SendMessageOptions.RequireReceiver);
            Assert.That(head.transform.position.x, Is.LessThan(nearSide - 0.1f), "Spectator movement must sweep the full step.");
            Vector3 spectatorHeadLocal = head.transform.localPosition;
            head.transform.position += Vector3.right * 3f;
            controller.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            controller.SendMessage("TurnRig", 0f, SendMessageOptions.RequireReceiver);
            controller.SendMessage("MoveSpectator", Vector3.right * 2f, SendMessageOptions.RequireReceiver);
            Assert.That(head.transform.position.x, Is.LessThan(nearSide - 0.1f), "Zero mouse yaw while leaning into a wall must preserve the safe sweep origin.");
            head.transform.localPosition = spectatorHeadLocal;
            controller.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            controller.SendMessage("MoveSpectator", Vector3.left, SendMessageOptions.RequireReceiver);
            Assert.That(head.transform.position.x, Is.LessThan(nearSide - 0.7f), "Backing away must respond immediately after a blocked step.");

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.SetParent(world.Root.transform, true);
            ceiling.transform.position = head.transform.position + Vector3.up;
            ceiling.transform.localScale = new Vector3(30f, 0.2f, 30f);
            Physics.SyncTransforms();
            controller.SendMessage("MoveSpectator", Vector3.up * 20f, SendMessageOptions.RequireReceiver);
            Assert.That(head.transform.position.y, Is.LessThan(ceiling.GetComponent<Collider>().bounds.min.y - 0.1f));
            controller.SendMessage("MoveSpectator", Vector3.down * 20f, SendMessageOptions.RequireReceiver);
            Assert.That(head.transform.position.y, Is.GreaterThan(world.Root.transform.position.y + 0.1f));
        }

        [UnityTest]
        public IEnumerator ProjectileFollowsOffsetCursorAndCloseCoverCannotBeSkippedByItsMuzzle()
        {
            var ranger = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.zero);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 5f);
            var controller = world.Root.AddComponent<PossessionController>();
            controller.Initialize(world.Director);
            yield return new WaitForSecondsRealtime(0.2f);
            controller.enabled = false;
            typeof(PossessionController).GetProperty(nameof(PossessionController.IsUsingXR)).SetValue(controller, false);
            controller.ViewCamera.usePhysicalProperties = true;
            controller.ViewCamera.lensShift = new Vector2(0.12f, -0.08f);
            controller.ViewCamera.rect = new Rect(.13f, .09f, .76f, .81f);
            var previousMouse = Mouse.current;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                Rect viewport = controller.ViewCamera.pixelRect;
                Vector2 pointer = new Vector2(viewport.xMin + viewport.width * 0.76f,
                    viewport.yMin + viewport.height * 0.33f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                InputSystem.Update();
                controller.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                world.Director.BeginBattle();
                Assert.That(Vector2.Distance(controller.AimScreenPosition, pointer), Is.LessThan(0.01f));
                foreach (float distance in new[] { 1f, 10f, 30f })
                {
                    Vector3 visibleAim = controller.ViewCamera.WorldToScreenPoint(controller.AimRay.GetPoint(distance));
                    Assert.That(Vector2.Distance(new Vector2(visibleAim.x, visibleAim.y), pointer), Is.LessThan(.1f),
                        $"At {distance}m the shot must remain under the cursor, including with an offset physical camera.");
                }
                Assert.That(ranger.TryAttack(controller.AimRay.origin, controller.AimRay.direction), Is.True);
                var projectile = world.Root.GetComponentInChildren<BattleProjectile>();
                Assert.That(projectile, Is.Not.Null);
                Assert.That(Vector3.Distance(projectile.SpawnOrigin, controller.ViewCamera.transform.position), Is.LessThan(0.001f));
                Assert.That(Vector3.Angle(projectile.Direction, controller.AimRay.direction), Is.LessThan(0.01f));
                Assert.That(projectile.GetComponent<Renderer>().enabled, Is.False,
                    "The initial visual is hidden without shifting the physical trajectory.");
                UnityEngine.Object.Destroy(projectile.gameObject);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                previousMouse?.MakeCurrent();
            }

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(world.Root.transform, false);
            wall.transform.localPosition = new Vector3(0f, 1.5f, 0.18f);
            wall.transform.localScale = new Vector3(4f, 3f, 0.08f);
            Physics.SyncTransforms();
            BattleProjectile.Spawn(world.Director, ranger, ranger.EyePosition, Vector3.forward,
                50f, 2000f, 10f, Color.yellow);
            float deadline = Time.realtimeSinceStartup + 1f;
            while (world.Root.GetComponentInChildren<BattleProjectile>() != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(enemy.Health, Is.EqualTo(100f));
            Assert.That(world.Root.GetComponentsInChildren<BattleProjectile>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator HomeDeploymentSupportsBudgetedEditingAndLocksTheRosterDuringBattle()
        {
            var disabledRoots = new List<GameObject>();
            foreach (var sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (sceneRoot == world.Root || !sceneRoot.activeSelf) continue;
                sceneRoot.SetActive(false);
                disabledRoots.Add(sceneRoot);
            }
            var previousRandom = UnityEngine.Random.state;
            var root = new GameObject("Home and deployment regression");
            try
            {
                UnityEngine.Random.InitState(74123);
                var bootstrap = root.AddComponent<BattlefieldBootstrap>(); bootstrap.TowerDefenseMode = false;
                bootstrap.RandomizePowerBudget=false;
                bootstrap.RandomizeBattlefield=false;
                bootstrap.ArmyPowerBudget=100;
                bootstrap.ArenaSize=new Vector2(36,44);
                bootstrap.RandomizeArmies = true;
                yield return null; // Build the arena and its runtime navigation surface.
                var director = root.GetComponent<BattleDirector>();
                var deployment = root.GetComponent<BattleDeployment>();
                Assert.That(director, Is.Not.Null);
                Assert.That(deployment, Is.Not.Null);
                Assert.That(director.Phase, Is.EqualTo(BattlePhase.Home));
                Assert.That(director.Units, Is.Empty);
                Assert.That(deployment.Definitions.Count, Is.EqualTo(24));
                Assert.That(deployment.CanStart, Is.False);

                var player = root.GetComponent<PossessionController>();
                Canvas menu = null;
                foreach (var candidate in UnityEngine.Object.FindObjectsByType<Canvas>())
                    if (candidate.name == "Battle Menu" && candidate.worldCamera == player.ViewCamera) menu = candidate;
                Assert.That(menu, Is.Not.Null);
                Assert.That(menu.transform.Find("Home").gameObject.activeInHierarchy, Is.True);
                CaptureUiPreview(menu, "Home-command");
                ClickMenu(menu, menu.transform.Find("Home/New Game"));
                yield return null; // Let newly visible deployment graphics enter the canvas hit-test registry.
                Assert.That(director.Phase, Is.EqualTo(BattlePhase.Setup));
                Assert.That(director.AliveBlue, Is.Zero);
                Assert.That(director.AliveRed, Is.GreaterThan(0));
                Assert.That(deployment.EnemySpent, Is.EqualTo(deployment.Budget));
                Assert.That(deployment.Remaining, Is.EqualTo(deployment.Budget));
                Assert.That(deployment.CanStart, Is.False);
                CaptureUiPreview(menu, "Army-forge");
                var initialRedPositions = new List<Vector3>();
                foreach (var unit in director.Units) initialRedPositions.Add(unit.transform.position);
                AssertClearDeployment(director);

                // Exercise the actual chooser and map coordinate conversion, then
                // select, replace, and remove through the same controls as the player.
                Canvas.ForceUpdateCanvases();
                var board = menu.transform.Find("Deployment/Deployment Board").GetComponent<RectTransform>();
                Vector3 free = FindFreePlacement(deployment);
                Vector2 normalized = new Vector2(free.x / deployment.ArenaSize.x + .5f,
                    free.z / deployment.ArenaSize.y + .5f);
                Vector3 boardWorld = board.TransformPoint(new Vector3(normalized.x * board.rect.width,
                    (normalized.y - 1) * board.rect.height, 0));
                var click = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, boardWorld),
                    button = PointerEventData.InputButton.Left
                };
                ClickMenuAt(menu, click.position);
                Assert.That(deployment.Count, Is.EqualTo(1), "A click in the blue map zone must place a unit.");
                Assert.That(deployment.SelectedUnit, Is.Null, "Repeated map clicks remain in placement mode.");
                ClickMenuAt(menu, click.position);
                Assert.That(deployment.SelectedUnit, Is.Not.Null, "Clicking an existing map unit selects it.");
                var nameField=menu.transform.Find("Deployment/Recruitment panel/Army name").GetComponent<InputField>();
                var previousNameKeyboard=Keyboard.current;
                var nameKeyboard=InputSystem.AddDevice<Keyboard>();
                var previousNameRouting=InputSystem.settings.editorInputBehaviorInPlayMode;
                var previousNameBackground=InputSystem.settings.backgroundBehavior;
                try
                {
                    InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    nameField.Select(); nameField.ActivateInputField();
                    yield return null;
                    Assert.That(nameField.isFocused,Is.True);
                    foreach(var key in new[]{Key.Space,Key.X,Key.Backspace})
                    {
                        InputSystem.QueueStateEvent(nameKeyboard,new KeyboardState(key)); InputSystem.Update();
                        root.GetComponent<BattleDeploymentMenu>().SendMessage("LateUpdate",SendMessageOptions.RequireReceiver);
                        Assert.That(director.Phase,Is.EqualTo(BattlePhase.Setup),"Naming an army must not start combat.");
                        Assert.That(deployment.Count,Is.EqualTo(1),"Typing must not remove a selected unit.");
                        InputSystem.QueueStateEvent(nameKeyboard,new KeyboardState()); InputSystem.Update();
                    }
                }
                finally
                {
                    nameField.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
                    InputSystem.settings.editorInputBehaviorInPlayMode=previousNameRouting;
                    InputSystem.settings.backgroundBehavior=previousNameBackground;
                    InputSystem.RemoveDevice(nameKeyboard); previousNameKeyboard?.MakeCurrent();
                }
                var selectedMarker = deployment.SelectedUnit;
                Vector3 originalPosition = selectedMarker.transform.position;
                Vector3 emptyGround = FindFreePlacement(deployment);
                Vector2 emptyNormalized = new Vector2(emptyGround.x / deployment.ArenaSize.x + .5f,
                    emptyGround.z / deployment.ArenaSize.y + .5f);
                Vector2 emptyScreen = RectTransformUtility.WorldToScreenPoint(null, board.TransformPoint(new Vector3(
                    emptyNormalized.x * board.rect.width, (emptyNormalized.y - 1) * board.rect.height, 0)));
                ClickMenuAt(menu, click.position);
                Assert.That(deployment.SelectedUnit, Is.Null, "Clicking the selected marker again must deselect it.");
                ClickMenuAt(menu, emptyScreen);
                Assert.That(deployment.Count, Is.EqualTo(1), "Empty ground must not place units after deselection.");
                Assert.That(selectedMarker.transform.position, Is.EqualTo(originalPosition));
                ClickMenuAt(menu, click.position);
                ClickMenu(menu, menu.transform.Find("Deployment/Deselect"));
                Assert.That(deployment.SelectedUnit, Is.Null);
                ClickMenuAt(menu, emptyScreen);
                Assert.That(deployment.Count, Is.EqualTo(1));
                Assert.That(selectedMarker.transform.position, Is.EqualTo(originalPosition));
                ClickMenu(menu, menu.transform.Find("Deployment/Add New"));
                ClickMenuAt(menu, emptyScreen);
                Assert.That(deployment.Count, Is.EqualTo(2), "Add new must explicitly resume placement.");
                ClickMenuAt(menu, emptyScreen);
                ClickMenu(menu, menu.transform.Find("Deployment/Remove"));
                Assert.That(deployment.Count, Is.EqualTo(1));
                ClickMenuAt(menu, click.position);
                ClickMenuAt(menu, emptyScreen);
                Assert.That(Vector3.Distance(selectedMarker.transform.position, emptyGround), Is.LessThan(.1f));
                Assert.That(deployment.SelectedUnit, Is.SameAs(selectedMarker), "A completed move must keep the character selected.");
                ClickMenuAt(menu, click.position);
                Assert.That(deployment.Count, Is.EqualTo(1), "Finishing a move must not silently enable placement.");
                Assert.That(Vector3.Distance(selectedMarker.transform.position, originalPosition), Is.LessThan(.1f), "The same selection supports successive moves.");
                Assert.That(deployment.SelectedUnit, Is.SameAs(selectedMarker));
                ClickMenu(menu, menu.transform.Find("Deployment/Recruitment panel/Choose Ranger"));
                Assert.That(menu.transform.Find("Deployment/Character Details").gameObject.activeInHierarchy, Is.False,
                    "Choosing a class must leave the map available for immediate deployment.");
                ClickMenu(menu, menu.transform.Find("Deployment/Selected class dossier/Info"));
                Assert.That(menu.transform.Find("Deployment/Character Details").gameObject.activeInHierarchy, Is.True);
                Assert.That(menu.transform.Find("Deployment/Character Details/Ability 1/Name").GetComponent<Text>().text, Does.Contain("Power Shot"));
                yield return null;
                ClickMenu(menu, menu.transform.Find("Deployment/Character Details/Use Class"));
                ClickMenu(menu, menu.transform.Find("Deployment/Replace"));
                Assert.That(deployment.SelectedUnit.Definition.Role, Is.EqualTo(UnitRole.Ranger));
                ClickMenu(menu, menu.transform.Find("Deployment/Remove"));
                Assert.That(deployment.Count, Is.Zero);
                Assert.That(deployment.Spent, Is.Zero);

                foreach (var definition in deployment.Definitions)
                {
                    if (menu.transform.Find("Deployment/Recruitment panel/Choose " + definition.Role) == null)
                    {
                        ClickMenu(menu, menu.transform.Find("Deployment/Recruitment panel/Class Next"));
                        yield return null;
                    }
                    ClickMenu(menu, menu.transform.Find("Deployment/Recruitment panel/Choose " + definition.Role));
                    yield return null;
                    var details = menu.transform.Find("Deployment/Character Details");
                    Assert.That(details.gameObject.activeInHierarchy, Is.False, "Class switching must not open the dossier.");
                    ClickMenu(menu, menu.transform.Find("Deployment/Selected class dossier/Info"));
                    yield return null;
                    Assert.That(details.gameObject.activeInHierarchy, Is.True);
                    Assert.That(details.Find("Class Name").GetComponent<Text>().text, Is.EqualTo(definition.DisplayName.ToUpperInvariant()));
                    for (int slot = 0; slot < Mathf.Min(2, definition.AbilityCount); slot++)
                        Assert.That(details.Find("Ability " + slot + "/Name").GetComponent<Text>().text,
                            Does.Contain(BattleClassCatalog.Ability(definition.Role, slot).Name));
                    foreach (var label in details.GetComponentsInChildren<Text>())
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1),
                            definition.Role + "/" + label.name + " must fit its description page.");
                    if (definition.Role == UnitRole.Engineer) CaptureUiPreview(menu, "Engineer-description");
                    if (definition.AbilityCount == 0)
                        Assert.That(details.Find("No Powers").gameObject.activeInHierarchy, Is.True);
                    if (definition.AbilityCount > 2)
                    {
                        ClickMenu(menu, details.Find("Power Next"));
                        yield return null;
                        for (int slot = 2; slot < definition.AbilityCount; slot++)
                            Assert.That(details.Find("Ability " + slot + "/Name").GetComponent<Text>().text,
                                Does.Contain(BattleClassCatalog.Ability(definition.Role, slot).Name));
                        if (definition.Role == UnitRole.Paladin) CaptureUiPreview(menu, "Paladin-powers");
                    }
                    ClickMenu(menu, details.Find("Use Class"));
                    Assert.That(details.gameObject.activeInHierarchy, Is.False);
                }
                while(menu.transform.Find("Deployment/Recruitment panel/Choose Vanguard")==null)
                {
                    ClickMenu(menu,menu.transform.Find("Deployment/Recruitment panel/Class Previous"));
                    yield return null;
                }

                // The simulator still needs mouse UI while its headset mode is active.
                player.enabled = false;
                var xrMode = typeof(PossessionController).GetProperty(nameof(PossessionController.IsUsingXR));
                xrMode.SetValue(player, true);
                root.GetComponent<BattleDeploymentMenu>().SendMessage("RebuildLayout", SendMessageOptions.RequireReceiver);
                yield return null;
                Assert.That(menu.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay),
                    "Editor mouse menus must use display coordinates even with a tracked simulator.");
                // A script reload can restore the live canvas while losing its
                // nonserialized input action, especially with fast Play Mode.
                var menuOwner = root.GetComponent<BattleDeploymentMenu>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                menuOwner.enabled = false;
                var actionField = typeof(BattleDeploymentMenu).GetField("mouseClick", flags);
                ((InputAction)actionField.GetValue(menuOwner)).Dispose();
                actionField.SetValue(menuOwner, null);
                menuOwner.enabled = true;
                yield return null; // Rebuilt controls need their first canvas update.
                var previousMouse = Mouse.current;
                var mouse = InputSystem.AddDevice<Mouse>();
                var eyeTexture = new RenderTexture(1680, 1760, 0);
                var previousEditorRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
                var previousBackground = InputSystem.settings.backgroundBehavior;
                try
                {
                    // The EditMode runner executes this iterator in an Editor
                    // update. Deliver synthetic events to the player state as a
                    // focused Game view would, retaining Application.isFocused=false.
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    // Reproduce a headset render size different from the Game view.
                    player.ViewCamera.targetTexture = eyeTexture;
                    Assert.That(player.ViewCamera.pixelWidth, Is.EqualTo(1680));
                    Canvas.ForceUpdateCanvases();
                    var choice = menu.transform.Find("Deployment/Recruitment panel/Choose Brute").GetComponent<RectTransform>();
                    Vector2 position = RectTransformUtility.WorldToScreenPoint(null, choice.TransformPoint(choice.rect.center));
                    // Queue both edges before the player samples input, as happens
                    // when a short click occurs between slow XR frames. This test
                    // runs even when batch mode reports Application.isFocused=false.
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 0 });
                    typeof(InputSystem).GetMethod("Update", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                        null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Dynamic });
                    Assert.That(mouse.leftButton.isPressed, Is.False, "The complete click has ended before the next player frame.");
                    root.GetComponent<BattleDeploymentMenu>().SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                    Assert.That(menu.transform.Find("Deployment/Selected class dossier/Selection").GetComponent<Text>().text, Does.Contain("Brute"),
                        "Raw mouse input must activate a choice without a focus override or direct click invocation.");
                }
                finally
                {
                    InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorRouting;
                    InputSystem.settings.backgroundBehavior = previousBackground;
                    player.ViewCamera.targetTexture = null;
                    UnityEngine.Object.Destroy(eyeTexture);
                    InputSystem.RemoveDevice(mouse);
                    previousMouse?.MakeCurrent();
                }
                xrMode.SetValue(player, false);
                root.GetComponent<BattleDeploymentMenu>().SendMessage("RebuildLayout", SendMessageOptions.RequireReceiver);
                yield return null;
                player.enabled = true;

                Assert.That(deployment.TryPlace(UnitRole.Vanguard, FindFreePlacement(deployment), out var first), Is.True);
                Assert.That(deployment.Spent, Is.EqualTo(BattleDeployment.GetCost(UnitRole.Vanguard)));
                Assert.That(deployment.SelectUnit(first), Is.True);
                Assert.That(deployment.SelectedUnit, Is.SameAs(first));
                Assert.That(deployment.TryPlace(UnitRole.Ranger, first.transform.position), Is.False,
                    "A second unit may not occupy the same deployment slot.");
                Assert.That(deployment.TryPlace(UnitRole.Ranger, deployment.DeploymentBounds.max + Vector3.right * 10f), Is.False);
                Assert.That(deployment.Count, Is.EqualTo(1));

                Vector3 moved = FindFreePlacement(deployment);
                Assert.That(deployment.TryMove(first, moved), Is.True);
                Assert.That(Vector3.Distance(first.transform.position, moved), Is.LessThan(0.1f));
                Assert.That(deployment.TryReplace(first, UnitRole.Ranger), Is.True);
                first = deployment.SelectedUnit;
                Assert.That(first, Is.Not.Null);
                Assert.That(first.Definition.Role, Is.EqualTo(UnitRole.Ranger));
                Assert.That(deployment.Spent, Is.EqualTo(BattleDeployment.GetCost(UnitRole.Ranger)));
                Assert.That(Vector3.Distance(first.transform.position, moved), Is.LessThan(0.1f));
                for (int i = 0; i < 4; i++)
                    Assert.That(deployment.TryPlace(UnitRole.Brute, FindFreePlacement(deployment)), Is.True);
                Assert.That(deployment.TryPlace(UnitRole.Medic, FindFreePlacement(deployment), out var medic), Is.True);
                int expectedSpend = BattleDeployment.GetCost(UnitRole.Ranger) + 4 * BattleDeployment.GetCost(UnitRole.Brute)
                    + BattleDeployment.GetCost(UnitRole.Medic);
                Assert.That(deployment.Spent, Is.EqualTo(expectedSpend));
                Assert.That(deployment.TryPlace(UnitRole.Brute, FindFreePlacement(deployment)), Is.False,
                    "The roster must never spend beyond its command budget.");
                Assert.That(deployment.Spent, Is.EqualTo(expectedSpend));
                Assert.That(deployment.Count, Is.EqualTo(6));
                Assert.That(deployment.Remove(medic), Is.True);
                Assert.That(deployment.Spent, Is.EqualTo(expectedSpend - BattleDeployment.GetCost(UnitRole.Medic)));
                Assert.That(deployment.TryReplace(first, UnitRole.Brute), Is.True);
                Assert.That(deployment.Remaining, Is.EqualTo(deployment.Budget - 5 * BattleDeployment.GetCost(UnitRole.Brute)));
                AssertClearDeployment(director);

                deployment.Clear();
                Assert.That(deployment.Count, Is.Zero);
                Assert.That(deployment.Spent, Is.Zero);
                Assert.That(deployment.CanStart, Is.False);
                Assert.That(deployment.RandomizeBlue(), Is.True);
                Assert.That(deployment.Count, Is.GreaterThan(0));
                Assert.That(deployment.Spent, Is.LessThanOrEqualTo(deployment.Budget));
                Assert.That(deployment.CanStart, Is.True);
                AssertClearDeployment(director);
                BattleUnit editable = null;
                foreach (var unit in director.Units)
                    if (unit.Team == BattleTeam.Blue) { editable = unit; break; }
                int countBeforeStart = deployment.Count;
                director.BeginBattle();
                Assert.That(director.IsBattleRunning, Is.True);
                Assert.That(deployment.TryMove(editable, editable.transform.position), Is.False);
                Assert.That(deployment.TryReplace(editable, UnitRole.Medic), Is.False);
                Assert.That(deployment.Remove(editable), Is.False);
                Assert.That(deployment.TryPlace(UnitRole.Vanguard, deployment.DeploymentBounds.center), Is.False);
                deployment.Clear();
                Assert.That(deployment.Count, Is.EqualTo(countBeforeStart));

                var reds = new List<BattleUnit>();
                foreach (var unit in director.Units) if (unit.Team == BattleTeam.Red) reds.Add(unit);
                foreach (var unit in reds) unit.TakeDamage(100000, editable);
                yield return null;
                Assert.That(director.Phase, Is.EqualTo(BattlePhase.Victory));
                Assert.That(menu.transform.Find("Battle Results").gameObject.activeInHierarchy, Is.True);
                Assert.That(menu.transform.Find("Battle Results/Title").GetComponent<Text>().text, Is.EqualTo("VICTORY"));
                Assert.That(menu.transform.Find("Battle Results/Title").GetComponent<Text>().fontSize, Is.GreaterThanOrEqualTo(60));
                Assert.That(player.IsMenuActive, Is.True);
                CaptureUiPreview(menu, "Victory");
                ClickMenu(menu, menu.transform.Find("Battle Results/New Army"));
                Physics.SyncTransforms();
                Assert.That(director.Phase, Is.EqualTo(BattlePhase.Setup));
                Assert.That(deployment.Count, Is.Zero);
                Assert.That(deployment.Remaining, Is.EqualTo(deployment.Budget));
                Assert.That(director.AliveRed, Is.GreaterThan(0));
                AssertClearDeployment(director);
                bool changed = initialRedPositions.Count != director.Units.Count;
                for (int i = 0; i < Mathf.Min(initialRedPositions.Count, director.Units.Count); i++)
                    changed |= Vector3.Distance(initialRedPositions[i], director.Units[i].transform.position) > 0.1f;
                Assert.That(changed, Is.True, "A new deployment must reroll the enemy army.");
                Assert.That(deployment.RandomizeBlue(), Is.True);
                director.BeginBattle();
                var blues = new List<BattleUnit>();
                foreach (var unit in director.Units) if (unit.Team == BattleTeam.Blue) blues.Add(unit);
                foreach (var unit in blues) unit.TakeDamage(100000, null);
                yield return null;
                Assert.That(director.Phase, Is.EqualTo(BattlePhase.Defeat));
                Assert.That(menu.transform.Find("Battle Results/Title").GetComponent<Text>().text, Is.EqualTo("DEFEAT"));
                ClickMenu(menu, menu.transform.Find("Battle Results/Home"));
                Assert.That(director.Phase, Is.EqualTo(BattlePhase.Home));
                Assert.That(director.Units, Is.Empty);
                Assert.That(deployment.TryPlace(UnitRole.Vanguard, deployment.DeploymentBounds.center), Is.False);
            }
            finally
            {
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
                UnityEngine.Random.state = previousRandom;
                foreach (var sceneRoot in disabledRoots) if (sceneRoot != null) sceneRoot.SetActive(true);
            }
        }

        internal static Vector3 FindFreePlacement(BattleDeployment deployment, Vector3? preferred=null)
        {
            if(preferred.HasValue && deployment.TryGetPlacement(preferred.Value,null,out var nearby,out _)) return nearby;
            Bounds bounds = deployment.DeploymentBounds;
            for (float z = bounds.min.z + 0.6f; z < bounds.max.z - 0.6f; z += 1.8f)
                for (float x = bounds.min.x + 0.6f; x < bounds.max.x - 0.6f; x += 1.8f)
                    if (deployment.TryGetPlacement(new Vector3(x, 0, z), null, out var point, out _))
                        return point;
            Assert.Fail("The arena must have room for a legal deployment.");
            return Vector3.zero;
        }

        internal static void CaptureUiPreview(Canvas canvas, string name)
        {
            string directory = Environment.GetEnvironmentVariable("BATTLEFIELD_PREVIEW_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            System.IO.Directory.CreateDirectory(directory);
            var camera = canvas.worldCamera;
            var previousTarget = camera.targetTexture;
            var previousMode = canvas.renderMode;
            float previousDistance = canvas.planeDistance;
            var target = new RenderTexture(Screen.width, Screen.height, 24);
            var pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.planeDistance = .5f;
                Canvas.ForceUpdateCanvases();
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                        new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousMode;
                canvas.planeDistance = previousDistance;
                RenderTexture.active = previousActive;
                UnityEngine.Object.Destroy(pixels);
                UnityEngine.Object.Destroy(target);
                Canvas.ForceUpdateCanvases();
            }
        }

        internal static void ClickMenu(Canvas menu, Transform control)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)control;
            Camera camera = menu.renderMode == RenderMode.ScreenSpaceOverlay ? null : menu.worldCamera;
            ClickMenuAt(menu, RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)));
        }

        internal static void ClickMenuAt(Canvas menu, Vector2 position)
        {
            Canvas.ForceUpdateCanvases();
            var owner = menu.worldCamera.GetComponentInParent<PossessionController>();
            // The generated rig is nested under the component's bootstrap object.
            Assert.That(owner, Is.Not.Null);
            var controller = owner.GetComponent<BattleDeploymentMenu>();
            var process = typeof(BattleDeploymentMenu).GetMethod("ProcessPointer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(process, Is.Not.Null);
            process.Invoke(controller, new object[] { position, true });
            process.Invoke(controller, new object[] { position, false });
        }

        static void AssertClearDeployment(BattleDirector director)
        {
            for (int i = 0; i < director.Units.Count; i++)
            {
                var unit = director.Units[i];
                Assert.That(NavMesh.SamplePosition(unit.transform.position, out var hit, 0.2f, NavMesh.AllAreas), Is.True);
                Assert.That(Vector3.Distance(unit.transform.position, hit.position), Is.LessThan(0.2f));
                foreach (var collider in Physics.OverlapCapsule(unit.transform.position + Vector3.up * 0.5f,
                    unit.transform.position + Vector3.up * 1.35f, 0.44f, ~0, QueryTriggerInteraction.Ignore))
                    Assert.That(collider.GetComponentInParent<BattleUnit>(), Is.Not.Null,
                        unit.name + " spawned intersecting " + collider.name);
                for (int j = 0; j < i; j++)
                    Assert.That(Vector3.Distance(unit.transform.position, director.Units[j].transform.position), Is.GreaterThan(1.2f));
            }
        }

        [UnityTest]
        public IEnumerator SwitchingBlinksBrieflyAndCompletesWhileGameTimeIsPaused()
        {
            var first = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var next = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.right * 5f);
            world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 20f);
            var controller = world.Root.AddComponent<PossessionController>();
            controller.Initialize(world.Director);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.SameAs(first));
            int blinks = 0;
            int changes = 0;
            controller.BlinkStarted += () => blinks++;
            controller.PossessionChanged += () => changes++;
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                Assert.That(PossessionController.SwitchBlinkDuration, Is.EqualTo(0.1f));
                Assert.That(controller.Possess(next), Is.True);
                Assert.That(blinks, Is.EqualTo(1));
                Assert.That(controller.CurrentUnit, Is.SameAs(first), "The character changes during the blink.");
                float largestOpacity = 0f;
                float deadline = Time.realtimeSinceStartup + 0.25f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    largestOpacity = Mathf.Max(largestOpacity, controller.BlinkOpacity);
                    yield return null;
                }
                Assert.That(largestOpacity, Is.GreaterThan(0.1f), "Switching must produce a visible blink.");
                Assert.That(controller.BlinkOpacity, Is.Zero);
                Assert.That(controller.CurrentUnit, Is.SameAs(next));
                Assert.That(first.IsPossessed, Is.False);
                Assert.That(changes, Is.EqualTo(1));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator HitsPushEnemiesWithoutCrossingWallsAndShotsRecoilThenRecover()
        {
            var shooter = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.zero);
            var victim = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 3f);
            var ally = world.AddUnit(BattleTeam.Red, UnitRole.Medic, Vector3.right * 5f);
            var visual = new GameObject("Recoil visual").transform;
            visual.SetParent(shooter.transform, false);
            visual.localPosition = Vector3.up * 0.25f;
            visual.localRotation = Quaternion.Euler(0f, 15f, 0f);
            shooter.VisualRoot = visual;
            Vector3 restPosition = visual.localPosition;
            Quaternion restRotation = visual.localRotation;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(world.Root.transform, false);
            wall.transform.localPosition = new Vector3(0f, 1.5f, 4.1f);
            wall.transform.localScale = new Vector3(5f, 3f, 0.2f);
            world.Director.BeginBattle();
            Physics.SyncTransforms();

            victim.TakeDamage(5f, ally, Vector3.back, 8f);
            Assert.That(victim.Health, Is.EqualTo(100f));
            Assert.That(victim.KnockbackVelocity, Is.EqualTo(Vector3.zero));
            Vector3 victimStart = victim.transform.position;
            victim.TakeDamage(5f, shooter, Vector3.forward, 8f);
            Assert.That(victim.Health, Is.EqualTo(95f));
            Assert.That(victim.KnockbackVelocity.z, Is.GreaterThan(0f));
            Assert.That(victim.KnockbackVelocity.y, Is.Zero);
            int shots = 0;
            shooter.ShotFired += () => shots++;
            Assert.That(shooter.TryAttack(shooter.EyePosition, Vector3.forward), Is.True);
            Assert.That(shooter.RecoilAmount, Is.GreaterThan(0f));
            Assert.That(shooter.TryAttack(shooter.EyePosition, Vector3.forward), Is.False);
            Assert.That(shots, Is.EqualTo(1));
            Assert.That(Vector3.Distance(restPosition, visual.localPosition) + Quaternion.Angle(restRotation, visual.localRotation),
                Is.GreaterThan(0.001f), "A successful shot must visibly kick the unit's body.");
            float deadline = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(victim.transform.position.z, Is.GreaterThan(victimStart.z + 0.05f));
            Assert.That(victim.transform.position.z + victim.GetComponent<CharacterController>().radius,
                Is.LessThanOrEqualTo(wall.GetComponent<Collider>().bounds.min.z + 0.1f));
            Assert.That(shooter.RecoilAmount, Is.Zero);
            Assert.That(Vector3.Distance(restPosition, visual.localPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(restRotation, visual.localRotation), Is.LessThan(0.001f));
            Assert.That(victim.KnockbackVelocity.magnitude, Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator RapidCyclingSkipsEnemiesAndDeadAlliesAndReleaseStaysInSpectator()
        {
            var first = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.forward * 20f);
            var deadAlly = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.right * 3f);
            var second = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.right * 6f);
            var third = world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.right * 9f);
            var controller = world.Root.AddComponent<PossessionController>();
            controller.Initialize(world.Director);
            world.Director.BeginBattle();
            deadAlly.TakeDamage(1000f, enemy);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.SameAs(first));

            // Repeated Tab/A/button presses during the blink must advance the pending
            // selection instead of repeatedly selecting the same next ally.
            controller.CycleUnit();
            controller.CycleUnit();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.SameAs(third));
            Assert.That(first.IsPossessed, Is.False);
            Assert.That(second.IsPossessed, Is.False);
            Assert.That(third.IsPossessed, Is.True);

            controller.ReleaseControl();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(controller.CurrentUnit, Is.Null);
            Assert.That(third.IsPossessed, Is.False);

            controller.CycleUnit();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.SameAs(first));
            Assert.That(first.IsPossessed, Is.True);
        }

        [UnityTest]
        public IEnumerator MouseLookStartsWithoutRightClickAndEscapeKeepsMenusUsable()
        {
            world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 20);
            var controller = world.Root.AddComponent<PossessionController>();
            controller.Initialize(world.Director);
            controller.enabled = false;
            world.Director.BeginBattle();
            var mode = typeof(PossessionController).GetProperty(nameof(PossessionController.IsUsingXR));
            mode.SetValue(controller, true); // Exercise simulator mouse input even without batch-mode window focus.
            typeof(PossessionController).GetField("desktopPointerActive", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(controller, true);
            var previousMouse = Mouse.current;
            var previousKeyboard = Keyboard.current;
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var previousRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            try
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Vector2 center = controller.ViewCamera.pixelRect.center;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                InputSystem.Update();
                controller.SendMessage("UpdatePlayerInput", SendMessageOptions.RequireReceiver);
                bool lookRequested = (bool)typeof(PossessionController).GetField("mouseLookActive",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controller);
                Assert.That(lookRequested, Is.True, "Gameplay must request mouse look without a button press.");
                if (!controller.IsMouseLookCaptured)
                    Assert.Ignore("Native cursor locking requires a focused interactive Game view; batch mode rejected the lock.");

                Quaternion before = controller.ViewCamera.transform.rotation;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center, delta = new Vector2(40, -20) });
                InputSystem.Update();
                controller.SendMessage("UpdatePlayerInput", SendMessageOptions.RequireReceiver);
                Assert.That(Quaternion.Angle(before, controller.ViewCamera.transform.rotation), Is.GreaterThan(1));
                Assert.That(Vector2.Distance(controller.AimScreenPosition, center), Is.LessThan(.01f));
                foreach (ushort buttons in new ushort[] { 2, 0 })
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = center, buttons = buttons });
                    InputSystem.Update();
                    controller.SendMessage("UpdatePlayerInput", SendMessageOptions.RequireReceiver);
                    Assert.That(controller.IsMouseLookCaptured, Is.True, "Releasing RMB must not stop mouse look.");
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                controller.SendMessage("UpdatePlayerInput", SendMessageOptions.RequireReceiver);
                Assert.That(controller.IsMouseLookCaptured, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center, delta = Vector2.right * 20 });
                InputSystem.Update();
                before = controller.ViewCamera.transform.rotation;
                controller.SendMessage("UpdatePlayerInput", SendMessageOptions.RequireReceiver);
                Assert.That(controller.IsMouseLookCaptured, Is.False, "Escape must keep the cursor free across frames.");
                Assert.That(Quaternion.Angle(before, controller.ViewCamera.transform.rotation), Is.LessThan(.01f));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center, buttons = 1 });
                InputSystem.Update();
                controller.SendMessage("UpdatePlayerInput", SendMessageOptions.RequireReceiver);
                Assert.That(controller.IsMouseLookCaptured, Is.True, "A game-view click resumes automatic looking.");

                world.Root.AddComponent<BattleDeployment>();
                world.Director.ReturnHome();
                controller.SendMessage("UpdatePlayerInput", SendMessageOptions.RequireReceiver);
                Assert.That(controller.IsMouseLookCaptured, Is.False, "Army menus must retain a free cursor.");
                Assert.That(Cursor.visible, Is.True);
            }
            finally
            {
                mode.SetValue(controller, false);
                controller.OpenMenuCursor();
                InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                previousMouse?.MakeCurrent();
                previousKeyboard?.MakeCurrent();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator KeyboardMatchAndPossessionActionsRemainAvailableInXRMode()
        {
            if (!Application.isFocused)
                Assert.Ignore("Keyboard controls require the Unity player to have focus.");

            var first = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var second = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.right * 5f);
            world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.forward * 20f);
            var controller = world.Root.AddComponent<PossessionController>();
            controller.Initialize(world.Director);
            world.Director.Initialize(() =>
            {
                world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.left * 5f);
                world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 20f);
            });
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.SameAs(first));

            var previousKeyboard = Keyboard.current;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                PressKeyboardInXRMode(controller, keyboard, Key.Space);
                Assert.That(world.Director.IsBattleRunning, Is.True);

                PressKeyboardInXRMode(controller, keyboard, Key.Tab);
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(controller.CurrentUnit, Is.SameAs(second));

                PressKeyboardInXRMode(controller, keyboard, Key.Q);
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(controller.CurrentUnit, Is.Null);

                PressKeyboardInXRMode(controller, keyboard, Key.R);
                Assert.That(world.Director.Phase, Is.EqualTo(BattlePhase.Setup));
                Assert.That(world.Director.RoundId, Is.EqualTo(2));
                PressKeyboardInXRMode(controller, keyboard, Key.X);
                Assert.That(world.Director.IsBattleRunning, Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                previousKeyboard?.MakeCurrent();
            }
        }

        static void PressKeyboardInXRMode(PossessionController controller, Keyboard keyboard, Key key)
        {
            // Inject only the tracked-head mode: no headset or simulator is needed.
            // Invoke the shared input step immediately so the synthetic key cannot
            // also trigger other controllers in the scene on a later player frame.
            var mode = typeof(PossessionController).GetProperty(nameof(PossessionController.IsUsingXR));
            bool previousXR = controller.IsUsingXR;
            mode.SetValue(controller, true);
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                InputSystem.Update();
                controller.SendMessage("UpdatePlayerInput", SendMessageOptions.RequireReceiver);
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                mode.SetValue(controller, previousXR);
            }
        }

        [UnityTest]
        public IEnumerator DesktopHudHasReadableTextAndButtonsControlTheMatchAndPossession()
        {
            var first = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
            var second = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.right * 5f);
            world.AddUnit(BattleTeam.Red, UnitRole.Ranger, Vector3.forward * 20f);
            var controller = world.Root.AddComponent<PossessionController>();
            controller.Initialize(world.Director);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.SameAs(first));

            // Freeze automatic device selection to inspect the desktop layout even
            // when these tests are run with an XR simulator active in the Editor.
            controller.enabled = false;
            var mode = typeof(PossessionController).GetProperty(nameof(PossessionController.IsUsingXR));
            mode.SetValue(controller, false);
            var hud = world.Root.AddComponent<BattleHud>();
            hud.Initialize(world.Director, controller);
            yield return null; // Let CanvasScaler apply the actual Game view dimensions.
            Canvas canvas = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<Canvas>())
                if (candidate.name == "Battle HUD" && candidate.worldCamera == controller.ViewCamera) canvas = candidate;
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.sortingOrder, Is.GreaterThanOrEqualTo(500));
            var instructionsPanel = canvas.transform.Find("Panel").GetComponent<RectTransform>();
            Assert.That(instructionsPanel.anchorMin, Is.EqualTo(new Vector2(0, 1)));
            Assert.That(instructionsPanel.pivot, Is.EqualTo(new Vector2(0, 1)));
            Assert.That(instructionsPanel.GetComponent<Image>().color.a, Is.EqualTo(.7f).Within(.001f));
            Assert.That(instructionsPanel.GetComponent<Image>().material.shader.name, Is.EqualTo("Battlefield/UI Always Visible"));
            Assert.That(canvas.transform.Find("Switch Blink").GetComponent<Canvas>().sortingOrder, Is.GreaterThan(canvas.sortingOrder));
            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            var controls = canvas.transform.Find("Panel/Controls").GetComponent<Text>();
            var status = canvas.transform.Find("Status Panel/Status").GetComponent<Text>();
            Assert.That(controls.fontSize, Is.GreaterThanOrEqualTo(22));
            Assert.That(status.fontSize, Is.GreaterThanOrEqualTo(26));
            // Keep desktop pixel-size guarantees at full size while allowing
            // the complete HUD to fit a small docked editor viewport.
            float viewportScale = Mathf.Min(1, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            Assert.That(controls.fontSize * canvas.scaleFactor, Is.GreaterThanOrEqualTo(17.99f * viewportScale));
            Assert.That(status.fontSize * canvas.scaleFactor, Is.GreaterThanOrEqualTo(21f * viewportScale));
            var corners = new Vector3[4]; instructionsPanel.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corner);
                Assert.That(point.x, Is.InRange(-1f, Screen.width + 1f), "Instructions must fit the actual viewport width.");
                Assert.That(point.y, Is.InRange(-1f, Screen.height + 1f), "Instructions must fit the actual viewport height.");
            }
            Assert.That(controls.resizeTextForBestFit, Is.False);
            Assert.That(controls.preferredHeight, Is.LessThanOrEqualTo(controls.rectTransform.rect.height + 1f));
            Assert.That(status.preferredHeight, Is.LessThanOrEqualTo(status.rectTransform.rect.height + 1f));

            mode.SetValue(controller, true);
            hud.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(scaler.dynamicPixelsPerUnit, Is.GreaterThanOrEqualTo(3f));
            Assert.That(controls.fontSize, Is.GreaterThanOrEqualTo(26));
            Assert.That(status.fontSize, Is.GreaterThanOrEqualTo(30));
            Assert.That(controls.preferredHeight, Is.LessThanOrEqualTo(controls.rectTransform.rect.height + 1f));
            Assert.That(status.preferredHeight, Is.LessThanOrEqualTo(status.rectTransform.rect.height + 1f));
            var headsetRect = (RectTransform)canvas.transform;
            float apparentWidth = headsetRect.rect.width * headsetRect.localScale.x / headsetRect.localPosition.z;
            Assert.That(apparentWidth, Is.GreaterThanOrEqualTo(0.9f), "The headset panel must occupy a readable field of view.");
            mode.SetValue(controller, false);
            hud.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);

            var buttons = canvas.transform.Find("Panel/Desktop Buttons");
            Assert.That(buttons.gameObject.activeInHierarchy, Is.True);
            Assert.That(canvas.GetComponent<GraphicRaycaster>().enabled, Is.True);
            buttons.Find("Start [Space]").GetComponent<Button>().onClick.Invoke();
            Assert.That(world.Director.IsBattleRunning, Is.True);

            Assert.That(first.TryAbility(0, first.EyePosition, Vector3.forward), Is.True);
            Assert.That(first.TryAbility(1, first.EyePosition, Vector3.forward), Is.True);
            yield return new WaitForSecondsRealtime(.12f);
            hud.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Assert.That(canvas.transform.Find("Panel/Ability 0/Name").GetComponent<Text>().text, Does.Contain("Shield Bash"));
            Assert.That(canvas.transform.Find("Panel/Ability 1/Name").GetComponent<Text>().text, Does.Contain("Bulwark"));
            Assert.That(canvas.transform.Find("Panel/Ability 1/Cooldown").GetComponent<Text>().text, Does.EndWith("s"));
            Assert.That(canvas.transform.Find("Status Panel/Health Value").GetComponent<Text>().text, Does.Contain("shield"));
            CaptureUiPreview(canvas, "Vanguard-HUD");

            buttons.Find("Next Ally [Tab]").GetComponent<Button>().onClick.Invoke();
            controller.enabled = true;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.SameAs(second));
            hud.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Assert.That(canvas.transform.Find("Panel/Ability 0/Name").GetComponent<Text>().text, Does.Contain("Healing Pulse"));
            Assert.That(canvas.transform.Find("Panel/Ability 1/Name").GetComponent<Text>().text, Does.Contain("Protective Ward"));
            Assert.That(canvas.transform.Find("Panel/Ability 1/Cooldown").GetComponent<Text>().text, Is.EqualTo("READY"));
            buttons.Find("Spectator [Q]").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.Null);

            world.Director.Initialize(() =>
            {
                world.AddUnit(BattleTeam.Blue, UnitRole.Ranger, Vector3.left * 5f);
                world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 20f);
            });
            buttons.Find("Restart [R]").GetComponent<Button>().onClick.Invoke();
            Assert.That(world.Director.RoundId, Is.EqualTo(2));
            Assert.That(world.Director.Phase, Is.EqualTo(BattlePhase.Setup));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(controller.CurrentUnit, Is.Not.Null);
            Assert.That(controller.CurrentUnit, Is.Not.SameAs(first));
            Assert.That(controller.CurrentUnit, Is.Not.SameAs(second));
        }
    }
}
