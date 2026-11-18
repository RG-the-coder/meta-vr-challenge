using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattlefieldPrototype.Tests
{
    public sealed class ClassAbilityTests
    {
        [Test]
        public void EveryClassDescribesItsAvailablePowersAndCooldownsRemainIndependent()
        {
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                var definition = BattleUnitDefinition.CreateDefault(role);
                try
                {
                    Assert.That(BattleClassCatalog.Description(role).Length, Is.GreaterThan(40));
                    var names = new System.Collections.Generic.HashSet<string>();
                    for (int slot = 0; slot < definition.AbilityCount; slot++)
                    {
                        Assert.That(names.Add(BattleClassCatalog.Ability(role, slot).Name), Is.True);
                        Assert.That(BattleClassCatalog.Ability(role, slot).Description.Length, Is.GreaterThan(30));
                    }
                    Assert.That(definition.DeploymentCost, Is.GreaterThan(0));
                    Assert.That(definition.GetAbilityCooldown(1), Is.GreaterThan(0));
                }
                finally { UnityEngine.Object.DestroyImmediate(definition); }
            }
            using (var world = new TestBattleWorld())
            {
                var unit = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.zero);
                var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 12);
                world.Director.BeginBattle();
                Assert.That(unit.TryAbility(0, unit.EyePosition, Vector3.forward), Is.True);
                Assert.That(unit.TryAbility(0, unit.EyePosition, Vector3.forward), Is.False);
                Assert.That(unit.SecondaryAbilityCooldownRemaining, Is.Zero);
                Assert.That(unit.TryAbility(1, unit.EyePosition, Vector3.forward), Is.True);
                Assert.That(unit.TryAbility(1, unit.EyePosition, Vector3.forward), Is.False);
                Assert.That(unit.Shield, Is.EqualTo(40));
                float health = unit.Health;
                unit.TakeDamage(25, enemy);
                Assert.That(unit.Health, Is.EqualTo(health));
                Assert.That(unit.Shield, Is.LessThan(40));
                Assert.That(unit.TryAbility(2, unit.EyePosition, Vector3.forward), Is.False);
            }
        }

    }

    public sealed class ClassAbilityFrameTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() { yield return new EnterPlayMode(); }

        [UnityTearDown]
        public IEnumerator TearDown() { yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator SupportFieldsRespectTeamsAndStormPowersRespectCover()
        {
            using (var world = new TestBattleWorld())
            {
                var engineer = world.AddUnit(BattleTeam.Blue, UnitRole.Engineer, Vector3.zero);
                var ally = world.AddUnit(BattleTeam.Blue, UnitRole.Medic, Vector3.right * 2);
                var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 2);
                yield return new WaitForSecondsRealtime(.08f); // Allow the physics shapes to register before tracing.
                world.Director.BeginBattle();
                ally.TakeDamage(40, enemy);
                Assert.That(engineer.TryAbility(0, engineer.EyePosition, Vector3.forward), Is.True);
                Assert.That(ally.Health, Is.GreaterThan(60));
                Assert.That(ally.Shield, Is.GreaterThan(0));
                Assert.That(enemy.Shield, Is.Zero);
                Assert.That(engineer.TryAbility(1, engineer.EyePosition, Vector3.forward), Is.True);
                Assert.That(ally.IsOverdriving, Is.True);
                Assert.That(enemy.IsOverdriving, Is.False);
            }
            using (var world = new TestBattleWorld())
            {
                var caster = world.AddUnit(BattleTeam.Blue, UnitRole.Stormcaller, Vector3.zero);
                var first = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 2);
                var second = world.AddUnit(BattleTeam.Red, UnitRole.Medic, new Vector3(2, 0, 2));
                var covered = world.AddUnit(BattleTeam.Red, UnitRole.Vanguard, Vector3.forward * 5);
                caster.Definition.AbilityRadius = 6;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(world.Root.transform, false);
                wall.transform.localPosition = new Vector3(0, 1.5f, 3.7f);
                wall.transform.localScale = new Vector3(8, 3, .2f);
                yield return new WaitForSecondsRealtime(.08f);
                Physics.SyncTransforms();
                world.Director.BeginBattle();
                Assert.That(caster.TryAbility(0, caster.EyePosition, Vector3.forward), Is.True);
                Assert.That(first.Health, Is.LessThan(100));
                Assert.That(second.Health, Is.LessThan(100));
                Assert.That(covered.Health, Is.EqualTo(100));
                Assert.That(caster.TryAbility(1, caster.EyePosition, Vector3.forward), Is.True);
                Assert.That(first.IsSlowed, Is.True);
                Assert.That(covered.IsSlowed, Is.False);
            }
        }
        [UnityTest]
        public IEnumerator EveryAiClassUsesItsAvailablePowersAndPossessedUnitsKeepPlayerControl()
        {
                foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
                {
                    using (var world = new TestBattleWorld())
                    {
                        var unit = world.AddUnit(BattleTeam.Blue, role, Vector3.zero);
                        var defaults = BattleUnitDefinition.CreateDefault(role);
                        unit.Definition.RetreatDistance = defaults.RetreatDistance;
                        UnityEngine.Object.Destroy(defaults);
                        var ally = world.AddUnit(BattleTeam.Blue, UnitRole.Vanguard, Vector3.right * 1.5f);
                        var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 2, health: 1000);
                        yield return new WaitForSecondsRealtime(.08f);
                        world.Director.BeginBattle();
                        ally.TakeDamage(35, enemy);
                        unit.TakeDamage(15, enemy);
                        enemy.TakeDamage(400, unit);
                        Physics.SyncTransforms();
                        var ai = unit.gameObject.AddComponent<BattleUnitAI>();
                        ai.Initialize(unit, world.Director);
                        ai.SendMessage("Update", SendMessageOptions.RequireReceiver);
                        string diagnostic = "";
                        if (unit.AbilityCooldownRemaining <= 0)
                        {
                            var aim = (enemy.EyePosition - Vector3.up * .35f - unit.EyePosition).normalized;
                            diagnostic = " visible=" + unit.HasLineOfSight(enemy) + " hits=" + string.Join(", ", Array.ConvertAll(
                                Physics.SphereCastAll(unit.EyePosition, .22f, aim, 12), hit => hit.collider.name + ":" + hit.distance));
                        }
                        for (int slot = 0; slot < unit.Definition.AbilityCount; slot++)
                            Assert.That(unit.GetAbilityCooldownRemaining(slot), Is.GreaterThan(0), role + " must use power " + (slot + 1) + diagnostic);
                        if (unit.Definition.AbilityCount == 0)
                            Assert.That(unit.AttackCooldownRemaining, Is.GreaterThan(0), role + " must still use its basic attack.");
                        Assert.That(unit.TryAbility(unit.Definition.AbilityCount, unit.EyePosition, Vector3.forward), Is.False);
                    }
                }
                using (var world = new TestBattleWorld())
                {
                    var unit = world.AddUnit(BattleTeam.Blue, UnitRole.Engineer, Vector3.zero);
                    world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 2);
                    world.Director.BeginBattle();
                    unit.SetPossessed(true);
                    var ai = unit.gameObject.AddComponent<BattleUnitAI>();
                    ai.Initialize(unit, world.Director);
                    ai.SendMessage("Update", SendMessageOptions.RequireReceiver);
                    Assert.That(unit.AbilityCooldownRemaining, Is.Zero);
                    Assert.That(unit.SecondaryAbilityCooldownRemaining, Is.Zero);
                }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FireRespectsTeamsAndCoverAndPounceOnlyHitsEachEnemyOnce()
        {
            using (var world = new TestBattleWorld())
            {
                var caster = world.AddUnit(BattleTeam.Blue, UnitRole.Pyromancer, Vector3.zero);
                var ally = world.AddUnit(BattleTeam.Blue, UnitRole.Militia, Vector3.right * 2);
                var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 2, health: 1000);
                var covered = world.AddUnit(BattleTeam.Red, UnitRole.Medic, Vector3.forward * 6, health: 1000);
                caster.Definition.AbilityRadius = 7;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(world.Root.transform, false);
                wall.transform.localPosition = new Vector3(0, 1.5f, 4.5f);
                wall.transform.localScale = new Vector3(8, 3, .2f);
                yield return new WaitForSecondsRealtime(.08f);
                world.Director.BeginBattle();
                Assert.That(caster.TryAbility(1, caster.EyePosition, Vector3.forward), Is.True);
                Assert.That(enemy.IsBurning, Is.True);
                Assert.That(ally.Health, Is.EqualTo(100));
                Assert.That(covered.IsBurning, Is.False);
                Assert.That(covered.Health, Is.EqualTo(1000));
                float health = enemy.Health;
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(enemy.Health, Is.LessThan(health));
            }
            using (var world = new TestBattleWorld())
            {
                var hound = world.AddUnit(BattleTeam.Blue, UnitRole.Hound, Vector3.zero);
                var enemy = world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 2, health: 1000);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.SetParent(world.Root.transform, false);
                floor.transform.localPosition = Vector3.down * .25f;
                floor.transform.localScale = new Vector3(20, .5f, 20);
                yield return new WaitForSecondsRealtime(.08f);
                world.Director.BeginBattle();
                Assert.That(hound.TryAbility(0, hound.EyePosition, Vector3.forward), Is.True);
                // Offscreen shader/portrait rendering can consume wall-clock time
                // between player frames. Wait for the motor to finish the pounce
                // before checking its contact contract, with a bounded timeout.
                float pounceDeadline = Time.realtimeSinceStartup + 3f;
                while (hound.IsDashing && Time.realtimeSinceStartup < pounceDeadline) yield return null;
                Assert.That(hound.IsDashing, Is.False, "The pounce motor must finish within the test timeout.");
                yield return null;
                yield return null;
                Assert.That(enemy.Health, Is.EqualTo(980).Within(.01), "Contact damage occurs once per target, not every frame.");
            }
        }

        [UnityTest]
        public IEnumerator SmallMeleeCreaturesCanHitAGiantsLegs()
        {
            using (var world = new TestBattleWorld())
            {
                var hound = world.AddUnit(BattleTeam.Blue, UnitRole.Hound, Vector3.zero);
                var titan = world.AddUnit(BattleTeam.Red, UnitRole.Titan, Vector3.forward, health: 1000);
                hound.Definition.BodyScale = .7f;
                hound.Definition.AttackRange = 1.5f;
                hound.Configure(world.Director, hound.Definition, BattleTeam.Blue);
                titan.Definition.BodyScale = 1.6f;
                titan.Configure(world.Director, titan.Definition, BattleTeam.Red);
                yield return new WaitForSecondsRealtime(.08f);
                world.Director.BeginBattle();
                var ai = hound.gameObject.AddComponent<BattleUnitAI>();
                ai.Initialize(hound, world.Director);
                ai.SendMessage("Update", SendMessageOptions.RequireReceiver);
                Assert.That(titan.Health, Is.LessThan(1000), "Short melee reach must still connect with a tall target's body.");
            }
        }

        [UnityTest]
        public IEnumerator DashMovesThroughMotorAndCannotCrossAWall()
        {
            using (var world = new TestBattleWorld())
            {
                var unit = world.AddUnit(BattleTeam.Blue, UnitRole.Skirmisher, Vector3.zero);
                world.AddUnit(BattleTeam.Red, UnitRole.Brute, Vector3.forward * 12);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.SetParent(world.Root.transform, false);
                floor.transform.localPosition = Vector3.down * .25f;
                floor.transform.localScale = new Vector3(20, .5f, 20);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(world.Root.transform, false);
                wall.transform.localPosition = new Vector3(0, 1.5f, 1.8f);
                wall.transform.localScale = new Vector3(8, 3, .2f);
                world.Director.BeginBattle();
                Physics.SyncTransforms();
                Assert.That(unit.TryAbility(1, unit.EyePosition, Vector3.forward), Is.True);
                float deadline = Time.realtimeSinceStartup + .55f;
                while (Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(unit.transform.localPosition.z, Is.GreaterThan(.2f));
                Assert.That(unit.transform.position.z + .42f, Is.LessThanOrEqualTo(wall.GetComponent<Collider>().bounds.min.z + .1f));
                Assert.That(unit.IsDashing, Is.False);
            }
            yield return null;
        }
    }
}
