using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator VrStartupAnchorUsesFinalEyeHeightAndFirstTrackedPoseOnce()
        {
            UseTestHeadset();
            var eye = player.ViewCamera.transform; var panel = (RectTransform)hud.Canvas.transform;
            eye.SetPositionAndRotation(new Vector3(2, 1.2f, -3), Quaternion.Euler(36, 40, 0));
            Invoke(hud, "RequestVrHudAnchor");
            eye.position += Vector3.up * .6f;
            Invoke(hud, "FinalizeVrHudAnchor", false);
            Assert.That(panel.position.y, Is.EqualTo(eye.position.y + .08f).Within(.001f));
            Assert.That(panel.pivot, Is.EqualTo(Vector2.one * .5f));
            Assert.That(Mathf.Abs(panel.forward.y), Is.LessThan(.001f), "The starting camera pitch must not lower the menu.");
            eye.position += Vector3.up * .4f;
            Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(panel.position.y, Is.EqualTo(eye.position.y + .08f).Within(.001f), "First valid tracking must replace the provisional height.");
            Vector3 anchored = panel.position;
            eye.position += new Vector3(1, .5f, 0); eye.Rotate(0, 60, 0);
            Invoke(hud, "FinalizeVrHudAnchor", true); Invoke(hud, "Layout");
            Assert.That(Vector3.Distance(panel.position, anchored), Is.LessThan(.001f));
            yield return null;
        }

        [UnityTest] public IEnumerator VrCharacterAndGuideSummonsAnchorAtTheCurrentPlayerLocation()
        {
            StartSiege(); battle.ToggleMount(); UseTestHeadset(); Invoke(hud, "FinalizeVrHudAnchor", true);
            Invoke(hud, "ToggleVrHud");
            var panel = hud.Canvas.transform; var eye = player.ViewCamera.transform;
            Vector3 initial = panel.position;
            eye.position += Vector3.right * 3; eye.Rotate(0, 50, 0);
            Invoke(hud, "ToggleCharacters"); Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(panel.position, initial), Is.GreaterThan(1));
            Assert.That(panel.position.y, Is.EqualTo(eye.position.y + .08f).Within(.001f));
            Vector3 characters = panel.position;
            eye.position += Vector3.right; hud.Refresh();
            Assert.That(Vector3.Distance(panel.position, characters), Is.LessThan(.001f));
            hud.OpenGuide(); Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(panel.position, characters), Is.LessThan(.001f), "Switching visible menu pages must preserve the anchor.");
            Invoke(hud, "CloseGuide");
            hud.OpenGuide(); Invoke(hud, "FinalizeVrHudAnchor", true);
            var facing = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            Assert.That(Vector3.Distance(panel.position, eye.position + facing * 1.65f + Vector3.up * .08f), Is.LessThan(.001f));
            Vector3 guide = panel.position;
            eye.position += Vector3.right; Invoke(hud, "TurnGuide", 1);
            Assert.That(Vector3.Distance(panel.position, guide), Is.LessThan(.001f));
            yield return null;
        }

        [UnityTest] public IEnumerator VrAimAndComfortEffectsRemainInViewWhenLookingAwayFromFixedHud()
        {
            StartSiege(); UseTestHeadset(); Invoke(hud, "FinalizeVrHudAnchor", true);
            var eye = player.ViewCamera.transform;
            eye.Rotate(0, 180, 0);
            typeof(PossessionController).GetProperty("AimScreenPosition").SetValue(player, new Vector2(Screen.width * .5f, Screen.height * .5f));
            Invoke(hud, "LateUpdate");
            var reticle = (RectTransform)typeof(TowerBattleHud).GetField("reticle", Private).GetValue(hud);
            Assert.That(hud.Canvas.transform.parent, Is.Null);
            Assert.That(reticle.parent.parent, Is.EqualTo(eye));
            Assert.That(reticle.gameObject.activeInHierarchy, Is.True);
            Vector3 viewport = player.ViewCamera.WorldToViewportPoint(reticle.position);
            Assert.That(viewport.x, Is.EqualTo(.5f).Within(.01f));
            Assert.That(viewport.y, Is.EqualTo(.5f).Within(.01f));
            var effects = reticle.parent.gameObject;
            typeof(PossessionController).GetProperty("IsUsingXR").SetValue(player, false);
            Invoke(hud, "Layout");
            Assert.That(reticle.parent, Is.EqualTo(Ui("Live command HUD")));
            Assert.That(effects.activeSelf, Is.False);
            yield return null;
        }
    }
}
