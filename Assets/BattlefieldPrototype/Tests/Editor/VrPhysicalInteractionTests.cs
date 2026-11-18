using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [Test] public void BowRequiresAPullAndReleaseAndCancelsLostTracking()
        {
            var draw = new BowDrawGesture();
            Assert.That(draw.Update(true, true, Vector3.zero, Vector3.back * .1f, out _), Is.False);
            Assert.That(draw.Update(true, false, Vector3.zero, Vector3.back * .11f, out _), Is.False);
            draw.Update(true, true, Vector3.zero, Vector3.back * .1f, out _);
            Assert.That(draw.Update(true, true, Vector3.zero, Vector3.back * .17f, out _), Is.False);
            Assert.That(draw.Update(true, false, Vector3.zero, Vector3.back * .17f, out float power), Is.True);
            Assert.That(power, Is.InRange(.3f, 1));
            Assert.That(draw.Update(true, false, Vector3.zero, Vector3.back * .14f, out _), Is.False);
            draw.Update(true, true, Vector3.zero, Vector3.back * .1f, out _);
            draw.Update(false, true, Vector3.zero, Vector3.back * .5f, out _);
            Assert.That(draw.Update(true, false, Vector3.zero, Vector3.back * .5f, out _), Is.False);
        }

        [UnityTest] public IEnumerator VrMenusAndBattleHudStayWhereSummoned()
        {
            UseTestHeadset(); yield return null;
            var canvas = hud.Canvas.transform; var position = canvas.position; var rotation = canvas.rotation;
            player.ViewCamera.transform.position += Vector3.right * .2f;
            player.ViewCamera.transform.Rotate(0, 15, 0);
            Invoke(hud, "Layout");
            Assert.That(Vector3.Distance(canvas.position, position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(canvas.rotation, rotation), Is.LessThan(.001f));
            StartSiege(); Invoke(hud, "Layout"); hud.Refresh(); Invoke(hud, "FinalizeVrHudAnchor", true);
            position = canvas.position; rotation = canvas.rotation;
            Assert.That(canvas.parent, Is.Null);
            player.ViewCamera.transform.position += Vector3.right;
            player.ViewCamera.transform.Rotate(0, 35, 0);
            Invoke(hud, "Layout"); hud.Refresh(); Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(canvas.position, position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(canvas.rotation, rotation), Is.LessThan(.001f));
            Invoke(hud, "ToggleVrHud"); Invoke(hud, "ToggleVrHud"); Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(canvas.position, position), Is.GreaterThan(.5f));
            Assert.That(canvas.position.y, Is.EqualTo(player.ViewCamera.transform.position.y + .08f).Within(.001f));
            position = canvas.position; player.ViewCamera.transform.position += Vector3.right;
            Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(canvas.position, position), Is.LessThan(.001f));
        }

        [UnityTest] public IEnumerator VrHiddenHudCharacterBrowserAcceptsMouseAndShowsPointerDot()
        {
            StartSiege(); var ally = Spawn(BattleTeam.Blue, UnitRole.Vanguard, FreePoint(battle.BoundTower));
            UseTestHeadset(); Invoke(hud, "ToggleVrHud"); Invoke(hud, "ToggleCharacters"); yield return null;
            hud.enabled = false; hud.Canvas.gameObject.SetActive(true); yield return null;
            Canvas.ForceUpdateCanvases();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var card = (RectTransform)Ui("Live command HUD/Character browser/Control character 0");
                Vector3 viewport = player.ViewCamera.WorldToViewportPoint(card.TransformPoint(card.rect.center));
                var position = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); InputSystem.Update();
                Invoke(hud, "ProcessInput");
                Assert.That(hud.PointerOverUI, Is.True);
                Assert.That(Ui("UI pointing dot").gameObject.activeInHierarchy, Is.True);
                CaptureHud("VR-character-pointing-dot", true);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); InputSystem.Update();
                Invoke(hud, "ProcessInput");
                Assert.That(hud.BlocksGameplayInput, Is.False);
                Invoke(player, "UpdateSwitch"); yield return new WaitForSecondsRealtime(.15f); Invoke(player, "UpdateSwitch");
                Assert.That(player.CurrentUnit, Is.EqualTo(ally));
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest] public IEnumerator VrThrownCharactersTakeImpactDamageAndLargeBodiesLaunchSlower()
        {
            StartSiege(); FreezeEnemy(); battle.ToggleMount();
            Vector3 point = FreePoint(battle.BoundTower);
            var small = Spawn(BattleTeam.Blue, UnitRole.Vanguard, point);
            var large = Spawn(BattleTeam.Blue, UnitRole.Titan, point + Vector3.right * 5);
            Assert.That((bool)Invoke(small, "BeginHandHold"), Is.True);
            Assert.That((bool)Invoke(large, "BeginHandHold"), Is.True);
            Assert.That(small.TryAttack(small.EyePosition, Vector3.forward), Is.False);
            Invoke(small, "MoveHandHeld", point + Vector3.up * 2, .25f);
            Invoke(large, "MoveHandHeld", point + Vector3.right * 5 + Vector3.up * 2, .25f);
            var velocity = new Vector3(0, 4, 8);
            Invoke(small, "ReleaseHandHold", velocity, true); Invoke(large, "ReleaseHandHold", velocity, true);
            var field = typeof(BattleUnit).GetField("throwVelocity", Private);
            Assert.That(((Vector3)field.GetValue(small)).magnitude, Is.GreaterThan(((Vector3)field.GetValue(large)).magnitude));
            float health = small.Health;
            yield return Simulate(2.4f);
            Assert.That(small.IsHandHeld, Is.False);
            Assert.That(small.Health, Is.LessThan(health), "Landing after a throw must hurt the thrown character.");
            Assert.That(small.IsThrown, Is.False);
        }

        [UnityTest] public IEnumerator VrTowerDamageBatchesGeometryChangesOncePerFrame()
        {
            StartSiege(); FreezeEnemy(); yield return null;
            var tower = battle.BoundTower;
            var shaft = (Transform)typeof(BattleTower).GetField("shaft", Private).GetValue(tower);
            TestContext.WriteLine("Tower shaft collider: " + shaft.GetComponent<Collider>().GetType().Name);
            var property = typeof(BattleTower).GetProperty("GeometryRefreshCount", Private);
            int count = (int)property.GetValue(tower); float height = tower.Height;
            for (int i = 0; i < 40; i++) tower.TakeDamage(1, BattleTeam.Red);
            Assert.That(tower.Height, Is.EqualTo(height - 40f / BattleTower.HealthPerMetre).Within(.001f));
            Assert.That((int)property.GetValue(tower), Is.EqualTo(count));
            yield return null;
            Assert.That((int)property.GetValue(tower), Is.EqualTo(count + 1));
        }

        [UnityTest] public IEnumerator VrGroundGrabRespectsWallsAndTrackingLossDropsWithoutAnImpulse()
        {
            StartSiege(); FreezeEnemy(); battle.ToggleMount(); UseTestHeadset();
            Vector3 point = FreePoint(battle.BoundTower);
            var unit = Spawn(BattleTeam.Red, UnitRole.Vanguard, point);
            player.ViewCamera.transform.position = point + new Vector3(0, 1.65f, -1.4f);
            Vector3 hand = point + Vector3.up;
            Physics.SyncTransforms();
            Assert.That(Invoke(player, "FindHandGrabTarget", hand), Is.EqualTo(unit));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform);
            wall.transform.position = point + new Vector3(0, 1, -.7f);
            wall.transform.localScale = new Vector3(2, 3, .2f);
            Physics.SyncTransforms();
            Assert.That(Invoke(player, "FindHandGrabTarget", hand), Is.Null, "A tracked hand cannot reach through scenery.");
            Object.Destroy(wall); yield return null;
            Assert.That((bool)Invoke(unit, "BeginHandHold"), Is.True);
            var held = (BattleUnit[])typeof(PossessionController).GetField("handUnits", Private).GetValue(player);
            held[0] = unit;
            Invoke(unit, "MoveHandHeld", point + Vector3.up * 2, .25f);
            Invoke(player, "ReleaseAllHandUnits");
            Assert.That(player.IsHoldingCharacter, Is.False);
            Assert.That(unit.IsHandHeld, Is.False);
            Assert.That(unit.IsThrown, Is.True, "A dropped unit must fall back to the ground.");
            Assert.That((Vector3)typeof(BattleUnit).GetField("throwVelocity", Private).GetValue(unit), Is.EqualTo(Vector3.zero));
        }

        [UnityTest] public IEnumerator VrBowDesktopHoldOnlyFiresOneArrowOnRelease()
        {
            StartSiege(); FreezeEnemy(); player.enabled = false;
            typeof(PossessionController).GetProperty("AimRay").SetValue(player, new Ray(player.ViewCamera.transform.position, Vector3.forward));
            typeof(TowerBattleHud).GetProperty("PointerOverUI").SetValue(hud, false);
            var mouse = InputSystem.AddDevice<Mouse>();
            void SetMouseHeld(bool held)
            {
                // Write the active play-mode state buffer; batch-mode editor focus
                // can route queued mouse events to the editor instead of the game.
                InputState.Change(mouse, held ? new MouseState().WithButton(MouseButton.Left) : new MouseState(), InputState.currentUpdateType);
                mouse.MakeCurrent();
            }
            try
            {
                int arrows = Object.FindObjectsByType<BattleProjectile>(FindObjectsSortMode.None).Length;
                SetMouseHeld(true);
                Invoke(player, "UpdateBowInteraction");
                Assert.That(player.IsDrawingBow, Is.True);
                Assert.That(Object.FindObjectsByType<BattleProjectile>(FindObjectsSortMode.None).Length, Is.EqualTo(arrows));
                // Advance the draw clock without handing control to the editor's
                // separate input buffer between these deterministic state transitions.
                typeof(PossessionController).GetField("desktopBowStarted", Private).SetValue(player, Time.unscaledTime - .12f);
                Assert.That(Mouse.current.leftButton.isPressed, Is.True);
                Invoke(player, "UpdateBowInteraction");
                Assert.That(Object.FindObjectsByType<BattleProjectile>(FindObjectsSortMode.None).Length, Is.EqualTo(arrows));
                SetMouseHeld(false);
                Invoke(player, "UpdateBowInteraction");
                Assert.That(Object.FindObjectsByType<BattleProjectile>(FindObjectsSortMode.None).Length, Is.EqualTo(arrows + 1));
                Invoke(player, "UpdateBowInteraction");
                Assert.That(Object.FindObjectsByType<BattleProjectile>(FindObjectsSortMode.None).Length, Is.EqualTo(arrows + 1));
            }
            finally { InputSystem.RemoveDevice(mouse); }
            yield return null;
        }
    }
}
