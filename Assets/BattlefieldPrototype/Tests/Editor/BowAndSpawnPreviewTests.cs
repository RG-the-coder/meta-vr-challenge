using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [Test] public void BowDrawUsesHandSeparationCurrentTensionAndFreshNocking()
        {
            var draw = new BowDrawGesture();
            draw.Update(true, true, Vector3.zero, Vector3.forward * .5f, out _);
            Assert.That(draw.Drawing, Is.False, "Reach close to the bow before nocking.");
            draw.Update(true, false, Vector3.zero, Vector3.back * .12f, out _);
            draw.Update(true, true, Vector3.zero, Vector3.back * .12f, out _);
            draw.Update(true, true, Vector3.zero, Vector3.back * .52f, out _);
            Assert.That(draw.Pull, Is.EqualTo(.4f).Within(.001f));
            draw.Update(true, true, Vector3.zero, Vector3.back * .13f, out _);
            Assert.That(draw.Update(true, false, Vector3.zero, Vector3.back * .13f, out _), Is.False,
                "Letting the string down must remove its stored power.");
            draw.Update(true, true, Vector3.zero, Vector3.back * .12f, out _);
            draw.Update(true, true, Vector3.zero, Vector3.back * 1.3f, out _);
            Assert.That(draw.Drawing, Is.False, "An implausible tracking jump must cancel the draw.");
            draw.Update(true, false, Vector3.zero, Vector3.back * .12f, out _);
            draw.Update(true, true, Vector3.zero, Vector3.back * .12f, out _);
            Assert.That(draw.Update(true, false, Vector3.zero, Vector3.back * .52f, out float power), Is.True);
            Assert.That(power, Is.EqualTo(1).Within(.001f));
            Assert.That(draw.Update(true, false, Vector3.zero, Vector3.back * .52f, out _), Is.False);
            Vector3 grip = new Vector3(2, 1, 3);
            draw.Update(true, true, grip, grip - Vector3.right * .12f, Vector3.right, out _);
            Assert.That(draw.Update(true, false, grip, grip - Vector3.right * .52f, Vector3.right, out power), Is.True);
            Assert.That(power, Is.EqualTo(1).Within(.001f), "A rotated bow must draw without relying on wrist orientation.");
        }

        [UnityTest] public IEnumerator SpawnPreviewMatchesPlacementMarksBlockedAndUnaffordableAndSupportsMinimap()
        {
            StartSiege(); battle.ToggleMount(); battle.ChooseRecruit(UnitRole.Vanguard);
            player.enabled = false; hud.enabled = false; hud.Canvas.gameObject.SetActive(true);
            Vector3 point = FreePoint(battle.BoundTower);
            var preview = (LineRenderer)typeof(TowerBattleHud).GetField("recruitmentPreview", Private).GetValue(hud);
            void PreviewAt(Vector3 target, bool forceRefresh = true)
            {
                typeof(PossessionController).GetProperty("AimRay").SetValue(player, new Ray(target + Vector3.up * 8, Vector3.down));
                if (forceRefresh) typeof(TowerBattleHud).GetField("nextRecruitmentPreviewAt", Private).SetValue(hud, 0f);
                Invoke(hud, "UpdateRecruitmentPreview");
            }
            PreviewAt(point);
            Assert.That(preview.enabled, Is.True);
            Assert.That(preview.startColor.g, Is.GreaterThan(preview.startColor.r));
            Vector3 center = Vector3.zero;
            for (int i = 0; i < preview.positionCount; i++) center += preview.GetPosition(i) / preview.positionCount;
            Assert.That(Vector3.Distance(center, point + Vector3.up * .12f), Is.LessThan(.05f));
            player.ViewCamera.transform.SetPositionAndRotation(point + new Vector3(0, 4, -5), Quaternion.LookRotation(new Vector3(0, -4, 5)));
            hud.Canvas.enabled = false;
            CaptureHud("Recruitment-spawn-circle");
            hud.Canvas.enabled = true;
            Assert.That(battle.TryDeploy(UnitRole.Vanguard, point, out var unit), Is.True);
            Assert.That(Vector3.Distance(unit.transform.position, center - Vector3.up * .12f), Is.LessThan(.05f));
            PreviewAt(point); Assert.That(preview.startColor.r, Is.GreaterThan(preview.startColor.g));

            point = FreePoint(battle.BoundTower);
            ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[0] = 0;
            PreviewAt(point, false); Assert.That(preview.startColor.r, Is.GreaterThan(preview.startColor.g));
            center = Vector3.zero;
            for (int i = 0; i < preview.positionCount; i++) center += preview.GetPosition(i) / preview.positionCount;
            Assert.That(Vector3.Distance(center, point + Vector3.up * .12f), Is.LessThan(.1f), "Moving the aim must move the ring immediately.");
            ((float[])typeof(TowerBattle).GetField("wallets", Private).GetValue(battle))[0] = 180;
            hud.Refresh(); yield return null; Canvas.ForceUpdateCanvases();
            Invoke(hud, "SelectCommandPage", 1); yield return null; Canvas.ForceUpdateCanvases();
            Invoke(hud.Minimap, "RefreshProjection");
            var local = (Vector2)Invoke(hud.Minimap, "Project", point);
            Invoke(hud, "DispatchWorld", hud.Minimap.rectTransform.TransformPoint(local), false);
            typeof(TowerBattleHud).GetProperty("ControllerOverUI").SetValue(hud, true);
            typeof(TowerBattleHud).GetField("nextRecruitmentPreviewAt", Private).SetValue(hud, 0f);
            Invoke(hud, "UpdateRecruitmentPreview");
            Assert.That(preview.enabled, Is.True);
            center = Vector3.zero;
            for (int i = 0; i < preview.positionCount; i++) center += preview.GetPosition(i) / preview.positionCount;
            Assert.That(Vector3.Distance(center, point + Vector3.up * .12f), Is.LessThan(.1f));
            battle.ChooseRecruit(null); Invoke(hud, "UpdateRecruitmentPreview");
            Assert.That(preview.enabled, Is.False);
        }

        static void SpawnTestBowArrow(BattleDirector director, Vector3 origin, Vector3 direction, float power)
            => typeof(BattleProjectile).GetMethod("SpawnSummitArrow", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { director, origin, direction, power, Color.yellow });

        [UnityTest] public IEnumerator FullDrawBowOneShotsMostClassesAndProtectsTankRoles()
        {
            StartSiege(); player.enabled = false;
            Vector3 point = FreePoint(battle.BoundTower); int oneHit = 0;
            foreach (var definition in battle.Definitions)
            {
                var unit = Spawn(BattleTeam.Red, definition.Role, point); unit.enabled = false;
                SpawnTestBowArrow(director, unit.AimPosition - Vector3.forward, Vector3.forward, 1);
                yield return Simulate(.06f);
                if (!unit.IsAlive) oneHit++;
                if (definition.Role == UnitRole.Titan || definition.Role == UnitRole.Sentinel || definition.Role == UnitRole.Paladin)
                    Assert.That(unit.IsAlive, Is.True, definition.DisplayName + " must retain its tank role.");
                Object.Destroy(unit.gameObject); yield return null;
            }
            TestContext.WriteLine($"One full-draw hit defeats {oneHit}/{battle.Definitions.Count} unshielded classes.");
            Assert.That(oneHit, Is.GreaterThanOrEqualTo(22));
        }

        [UnityTest] public IEnumerator BowArrowsDropWithGravityShortDrawsAreWeakerAndLimbsFlex()
        {
            StartSiege(); player.enabled = false;
            Vector3 origin = FreePoint(battle.BoundTower) + Vector3.up * 30;
            SpawnTestBowArrow(director, origin, Vector3.right, 1);
            var arrow = root.GetComponentInChildren<BattleProjectile>();
            yield return Simulate(.25f);
            Assert.That(arrow.transform.position.y, Is.LessThan(origin.y - .15f));
            Assert.That(arrow.Direction.y, Is.LessThan(0));
            Object.Destroy(arrow.gameObject);
            var unit = Spawn(BattleTeam.Red, UnitRole.Vanguard, FreePoint(battle.BoundTower)); unit.enabled = false;
            float initial = unit.Health;
            SpawnTestBowArrow(director, unit.AimPosition - Vector3.forward, Vector3.forward, .25f);
            yield return Simulate(.08f);
            Assert.That(initial - unit.Health, Is.InRange(1, 60));
            typeof(PossessionController).GetField("desktopBowHeld", Private).SetValue(player, true);
            typeof(PossessionController).GetField("desktopBowStarted", Private).SetValue(player, Time.unscaledTime - .8f);
            Invoke(hud, "UpdateBowPresentation");
            var bowString = (LineRenderer)typeof(TowerBattleHud).GetField("bowString", Private).GetValue(hud);
            Assert.That(bowString.GetPosition(1).z, Is.EqualTo(-.52f).Within(.001f));
            Assert.That(bowString.GetPosition(0).z, Is.LessThan(-.2f));
            CaptureHud("Bow-full-draw");
        }
    }
}
