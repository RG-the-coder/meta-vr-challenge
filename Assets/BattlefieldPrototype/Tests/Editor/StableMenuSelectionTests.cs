using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator SelectedMenuDoesNotJumpWhenTrackingArrivesOrHelpOpensAfterTurning()
        {
            yield return CreateBasicUi(); StartSiege(); var hands = UseOpticalHands();
            Invoke(hud, "FinalizeVrHudAnchor", true);
            var position = hud.Canvas.transform.position;
            var rotation = hud.Canvas.transform.rotation;
            typeof(TowerBattleHud).GetField("hudAnchorWaitingForTracking", Private).SetValue(hud, true);
            // A selection accepts the currently visible anchor before the render callback.
            player.ViewCamera.transform.position += Vector3.right * .15f;
            yield return HandPage(hands, "RECRUIT");
            Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(position, hud.Canvas.transform.position), Is.LessThan(.001f));
            player.ViewCamera.transform.Rotate(0, 85, 0);
            hud.OpenGuide(); Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(position, hud.Canvas.transform.position), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(rotation, hud.Canvas.transform.rotation), Is.LessThan(.001f));
        }

        [UnityTest] public IEnumerator OpeningAlreadyVisibleMenuPreservesItsAnchor()
        {
            yield return CreateBasicUi(); StartSiege(); UseOpticalHands();
            Invoke(hud, "FinalizeVrHudAnchor", true);
            var position = hud.Canvas.transform.position;
            player.ViewCamera.transform.position += Vector3.right * .2f;
            Invoke(hud, "OpenCommandMenu"); Invoke(hud, "FinalizeVrHudAnchor", true);
            Assert.That(Vector3.Distance(position, hud.Canvas.transform.position), Is.LessThan(.001f));
        }
    }
}
