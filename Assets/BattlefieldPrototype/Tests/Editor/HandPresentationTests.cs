using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [Test] public void OpticalPointerCapturesHoverBeforeFingerClosureAndRenderCannotConsumeEdges()
        {
            var latch = new HandPointerLatch();
            var hover = new Ray(Vector3.zero, Vector3.forward);
            var moved = new Ray(Vector3.right, Vector3.right);
            latch.Update(true, false, hover, 1);
            Assert.That(latch.RenderRay(moved).direction, Is.EqualTo(moved.direction));
            Assert.That(latch.Update(true, true, moved, 2).direction, Is.EqualTo(hover.direction));
            Assert.That(latch.RenderRay(moved).direction, Is.EqualTo(hover.direction));
            Assert.That(latch.Update(true, true, moved, 3).origin, Is.EqualTo(hover.origin));
            latch.Update(false, false, moved, 4);
            Assert.That(latch.RenderRay(moved).direction, Is.EqualTo(moved.direction));
        }

        [UnityTest] public IEnumerator OpticalPinchSelectsImmediatelyOnceDespiteHandMotionAndTrackingLoss()
        {
            yield return CreateBasicUi(); var hands = UseOpticalHands(); yield return null;
            var button = Ui("Home and outcome veil/Skyhold briefing/Begin siege").GetComponent<Button>();
            button.onClick.RemoveAllListeners(); int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform; Vector3 target = rect.TransformPoint(rect.rect.center);
            PointHand(hands, target, false); Invoke(hud, "ProcessInput");
            PointHand(hands, target + player.ViewCamera.transform.right * .6f, true);
            Invoke(hud, "UpdateControllerRay"); // Late tracking must not capture the displaced finger.
            Invoke(hud, "ProcessInput");
            Assert.That(clicks, Is.EqualTo(1), "Pinch closure must select the hovered button immediately.");
            for (int i = 0; i < 3; i++) { Invoke(hud, "ProcessInput"); yield return null; }
            Assert.That(clicks, Is.EqualTo(1), "Holding must neither move selection nor click again.");
            hands.tracked[1] = false; Invoke(hud, "ProcessInput");
            hands.tracked[1] = true; PointHand(hands, target, true); Invoke(hud, "ProcessInput");
            Assert.That(clicks, Is.EqualTo(1), "Tracking recovery while pinched must not select.");
            PointHand(hands, target, false); Invoke(hud, "ProcessInput"); yield return null;
            PointHand(hands, target, true); Invoke(hud, "ProcessInput");
            Assert.That(clicks, Is.EqualTo(2));
            PointHand(hands, target, false); Invoke(hud, "ProcessInput");
            Assert.That(clicks, Is.EqualTo(2), "Release must not click a second time.");
        }

        [UnityTest] public IEnumerator OpticalGauntletsRenderBothHandsOverMenuFromBothEyePositions()
        {
            yield return CreateBasicUi(); var source = UseOpticalHands(); yield return null;
            var eye = player.ViewCamera.transform;
            // Put both hands in front of an opaque menu panel; render from each eye position.
            var panel = (RectTransform)Ui("Home and outcome veil/Skyhold briefing");
            Vector3 center = panel.TransformPoint(panel.rect.center);
            Quaternion originalRotation = eye.rotation;
            eye.rotation = Quaternion.LookRotation(center - eye.position, Vector3.up);
            for (int h = 0; h < 2; h++)
            {
                Vector3 palm = Vector3.Lerp(eye.position, center, .35f) + eye.right * (h == 0 ? -.09f : .09f);
                source.Set(h == 0, palm, palm + eye.up * .09f, false);
            }
            player.enabled = true;
            var visuals = root.GetComponent<BattleOpticalHands>(); Invoke(visuals, "UpdateHands");
            var models = (Transform[])typeof(BattleOpticalHands).GetField("hands", Private).GetValue(visuals);
            foreach (var hand in models)
            {
                Assert.That(hand.gameObject.activeSelf, Is.True);
                Assert.That(hand.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(17));
                foreach (var renderer in hand.GetComponentsInChildren<MeshRenderer>())
                {
                    Assert.That(renderer.sortingOrder, Is.GreaterThan(hud.Canvas.sortingOrder));
                    Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
                    Assert.That(renderer.sharedMaterial.renderQueue, Is.GreaterThan(3500));
                }
            }
            var camera = player.ViewCamera; Vector3 original = eye.position;
            var rt = new RenderTexture(1000, 700, 24); var texture = new Texture2D(1000, 700, TextureFormat.RGB24, false);
            var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                for (int e = 0; e < 2; e++)
                {
                    eye.position = original + eye.right * (e == 0 ? -.032f : .032f);
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt });
                    RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1000, 700), 0, 0); texture.Apply();
                    string directory = System.Environment.GetEnvironmentVariable("BATTLEFIELD_PREVIEW_DIR");
                    if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "Gauntlets-eye-" + e + ".png"), texture.EncodeToPNG()); }
                    foreach (var hand in models)
                    {
                        Vector3 viewport = camera.WorldToViewportPoint(hand.Find("Palm").position);
                        Assert.That(viewport.x, Is.InRange(.05f, .95f));
                        Assert.That(viewport.y, Is.InRange(.05f, .95f));
                        Color pixel = texture.GetPixel((int)(viewport.x * 1000), (int)(viewport.y * 700));
                        Assert.That(pixel.b, Is.GreaterThan(pixel.r * 1.5f), "Both gauntlets must be visible over the menu for eye " + e + ": " + pixel);
                        Assert.That(pixel.g, Is.GreaterThan(.25f));
                    }
                }
            }
            finally
            {
                player.enabled = false;
                eye.SetPositionAndRotation(original, originalRotation); camera.targetTexture = oldTarget; RenderTexture.active = previous;
                Object.Destroy(rt); Object.Destroy(texture);
            }
        }
    }
}
