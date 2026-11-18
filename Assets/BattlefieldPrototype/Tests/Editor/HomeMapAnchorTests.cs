using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator HomeMapPinchesPreserveRoundCameraAndMainScreen()
        {
            yield return CreateBasicUi(); battle.GoHome(); hud.Refresh();
            var hands = UseOpticalHands();
            Invoke(hud, "FinalizeVrHudAnchor", true);
            var panelPosition = hud.Canvas.transform.position;
            var panelRotation = hud.Canvas.transform.rotation;
            int round = battle.Director.RoundId;
            var eye = player.ViewCamera.transform;
            eye.position += Vector3.right * .15f;
            eye.Rotate(0, 10, 0);
            var eyePosition = eye.position; var eyeRotation = eye.rotation;
            var choices = (UnityEngine.UI.Button[])typeof(TowerBattleHud).GetField("mapChoices", Private).GetValue(hud);
            for (int i = 0; i < choices.Length; i++)
            {
                yield return PinchButton(hands, choices[i].transform);
                Invoke(hud, "FinalizeVrHudAnchor", true);
                Assert.That(battle.Map, Is.EqualTo((TowerMapKind)i), "The actual pinch must select the map.");
                Assert.That(battle.Towers.Count, Is.EqualTo(TowerMapCatalog.Spawns((TowerMapKind)i).Count));
                Assert.That(battle.Director.RoundId, Is.EqualTo(round), "Map browsing is not a new round.");
                Assert.That(Vector3.Distance(eye.position, eyePosition), Is.LessThan(.001f), "Map selection must not teleport the head.");
                Assert.That(Quaternion.Angle(eye.rotation, eyeRotation), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(hud.Canvas.transform.position, panelPosition), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(hud.Canvas.transform.rotation, panelRotation), Is.LessThan(.001f));
            }
            battle.NewGame();
            Assert.That(battle.Director.IsBattleRunning, Is.True);
            Assert.That(battle.Director.RoundId, Is.GreaterThan(round), "Starting a game still resets the round.");
        }
    }
}
