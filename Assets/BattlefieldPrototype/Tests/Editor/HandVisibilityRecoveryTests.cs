using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator TrackedPalmsSurviveMissingKnucklesAndPointerSurvivesMenuMiss()
        {
            yield return CreateBasicUi(); var source = UseOpticalHands(); yield return null;
            source.palmOnly = true; player.enabled = true;
            var visuals = root.GetComponent<BattleOpticalHands>(); Invoke(visuals, "UpdateHands");
            var models = (Transform[])typeof(BattleOpticalHands).GetField("hands", Private).GetValue(visuals);
            foreach (var model in models) Assert.That(model.gameObject.activeSelf, Is.True);
            var eye = player.ViewCamera.transform;
            source.SetAim(false, new Pose(eye.position + eye.forward * .3f, Quaternion.LookRotation(eye.right)), 0);
            var line = (LineRenderer)typeof(TowerBattleHud).GetField("pointerLine", Private).GetValue(hud);
            line.enabled = true; Invoke(hud, "UpdateControllerRay");
            Assert.That(line.enabled, Is.True);
            Assert.That(Vector3.Distance(line.GetPosition(0), line.GetPosition(1)), Is.GreaterThan(.1f));
            source.tracked[0] = source.tracked[1] = false; Invoke(visuals, "UpdateHands");
            foreach (var model in models) Assert.That(model.gameObject.activeSelf, Is.False, "Do not leave phantom hands after actual tracking loss.");
        }
    }
}
