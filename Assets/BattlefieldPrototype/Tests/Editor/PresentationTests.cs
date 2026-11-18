using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattlefieldPrototype.Tests
{
    public sealed class PresentationTests
    {
        GameObject root;
        BattleDirector director;
        BattleDeployment deployment;
        readonly List<GameObject> previousRoots = new List<GameObject>();
        bool previousFog;
        Material previousSky;
        AmbientMode previousAmbient;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            foreach (var sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!sceneRoot.activeSelf) continue;
                previousRoots.Add(sceneRoot);
                sceneRoot.SetActive(false);
            }
            previousFog = RenderSettings.fog;
            previousSky = RenderSettings.skybox;
            previousAmbient = RenderSettings.ambientMode;
            root = new GameObject("Presentation regression");
            var bootstrap=root.AddComponent<BattlefieldBootstrap>(); bootstrap.TowerDefenseMode = false;
            bootstrap.RandomizePowerBudget=false;
            bootstrap.RandomizeBattlefield=false;
            bootstrap.ArmyPowerBudget=100;
            bootstrap.ArenaSize=new Vector2(36,44);
            yield return null;
            director = root.GetComponent<BattleDirector>();
            deployment = root.GetComponent<BattleDeployment>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            yield return null;
            foreach (var previous in previousRoots) if (previous != null) previous.SetActive(true);
            previousRoots.Clear();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EveryCharacterRendersADistinctSymbol()
        {
            var guide = new GameObject("Class symbol guide", typeof(RectTransform), typeof(Canvas));
            var canvas = guide.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = root.GetComponent<PossessionController>().ViewCamera;
            canvas.sortingOrder = 900;
            canvas.pixelPerfect = true;
            var symbols = new List<BattleUiEmblem>();
            try
            {
                var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
                background.transform.SetParent(guide.transform, false);
                var backdrop = background.GetComponent<Image>();
                backdrop.color = new Color(.035f, .044f, .052f);
                backdrop.rectTransform.anchorMin = Vector2.zero;
                backdrop.rectTransform.anchorMax = Vector2.one;
                backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;
                int index = 0;
                float cellWidth = Screen.width / 4f, cellHeight = Screen.height / Mathf.Ceil(Enum.GetValues(typeof(UnitRole)).Length/4f);
                foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
                {
                    var obj = new GameObject(role.ToString(), typeof(RectTransform), typeof(BattleUiEmblem));
                    obj.transform.SetParent(guide.transform, false);
                    var symbol = obj.GetComponent<BattleUiEmblem>();
                    symbol.Role = role;
                    symbol.color = new Color(.78f, .64f, .39f);
                    var rect = symbol.rectTransform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
                    rect.sizeDelta = Vector2.one * Mathf.Min(52, cellHeight - 40);
                    rect.anchoredPosition = new Vector2((index % 4 - 1.5f) * cellWidth, -(index / 4) * cellHeight - 14);
                    var captionObject = new GameObject("Class name", typeof(RectTransform), typeof(Text));
                    captionObject.transform.SetParent(obj.transform, false);
                    var caption = captionObject.GetComponent<Text>();
                    caption.text = role.ToString();
                    caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    caption.fontSize = 16;
                    caption.color = new Color(.89f, .89f, .83f);
                    caption.alignment = TextAnchor.MiddleCenter;
                    caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = new Vector2(.5f, 0);
                    caption.rectTransform.pivot = new Vector2(.5f, 1);
                    caption.rectTransform.anchoredPosition = new Vector2(0, -8);
                    caption.rectTransform.sizeDelta = new Vector2(cellWidth - 8, 24);
                    symbols.Add(symbol);
                    index++;
                }
                Canvas.ForceUpdateCanvases();
                var signatures = new HashSet<string>();
                foreach (var symbol in symbols)
                {
                    var mesh = symbol.canvasRenderer.GetMesh();
                    Assert.That(mesh, Is.Not.Null, symbol.Role + " must have a rendered mesh.");
                    Assert.That(mesh.vertexCount, Is.GreaterThan(0), symbol.Role + " must render a symbol.");
                    var signature = new System.Text.StringBuilder();
                    foreach (var vertex in mesh.vertices) signature.Append(vertex.ToString("F4"));
                    Assert.That(signatures.Add(signature.ToString()), Is.True, symbol.Role + " must not reuse another class's symbol.");
                }
                BattleFrameBehaviorTests.CaptureUiPreview(canvas, "Class-symbols");
            }
            finally { UnityEngine.Object.Destroy(guide); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneryPreservesDeploymentCollisionAndRestoresSceneLighting()
        {
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
            Assert.That(RenderSettings.skybox.shader.name, Is.EqualTo("Battlefield/Hazy Sky"));
            var arena = root.transform.Find("Arena - runtime NavMesh");
            Assert.That(arena.Find("Ground").GetComponent<Renderer>().sharedMaterial.mainTexture.mipmapCount, Is.GreaterThan(1));
            Assert.That(arena.Find("Cover seams and edge guards").GetComponent<Collider>(), Is.Null);
            Assert.That(arena.Find("Distant landscape").GetComponent<Collider>(), Is.Null);
            deployment.NewGame();
            int index = 0;
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                if ((int)role >= (int)UnitRole.Wisp) continue; // The original seven still fit this 100-Power lineup.
                Assert.That(deployment.TryPlace(role, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(-9 + index * 3, 0, -11))), Is.True, role.ToString());
                index++;
            }
            Assert.That(deployment.Spent, Is.EqualTo(96));
            yield return null;
            CaptureScene("Arena", new Vector3(24, 18, -32), new Vector3(0, 0, 2));
            CaptureScene("Class-lineup", new Vector3(0, 3.3f, 2), new Vector3(0, 1, -11));
            root.SetActive(false);
            Assert.That(RenderSettings.fog, Is.EqualTo(previousFog));
            Assert.That(RenderSettings.skybox, Is.SameAs(previousSky));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(previousAmbient));
        }

        [UnityTest]
        public IEnumerator WalkingAnimatesLimbsWhileCameraAndCollisionShapeRemainStable()
        {
            deployment.NewGame();
            Assert.That(deployment.TryPlace(UnitRole.Skirmisher, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(0, 0, -12)), out var unit), Is.True);
            foreach (var other in director.Units) other.GetComponent<BattleUnitAI>().enabled = false;
            var player = root.GetComponent<PossessionController>();
            player.enabled = false;
            director.BeginBattle();
            yield return new WaitForSecondsRealtime(.15f);
            unit.SetPossessed(false);
            var cameraPosition = player.ViewCamera.transform.position;
            var motor = unit.GetComponent<CharacterController>();
            var center = motor.center;
            float radius = motor.radius;
            Vector3 eyeOffset = unit.EyePosition - unit.transform.position;
            float startZ = unit.transform.position.z;
            float end = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < end) { unit.Move(Vector3.forward); yield return null; }
            var leg = unit.VisualRoot.Find("Articulated body/Left leg");
            Assert.That(Quaternion.Angle(leg.localRotation, Quaternion.identity), Is.GreaterThan(1));
            Assert.That(unit.transform.position.z, Is.GreaterThan(startZ + .1f));
            Assert.That(motor.center, Is.EqualTo(center));
            Assert.That(motor.radius, Is.EqualTo(radius));
            Assert.That(Vector3.Distance(unit.EyePosition - unit.transform.position, eyeOffset), Is.LessThan(.001f));
            Assert.That(player.ViewCamera.transform.position, Is.EqualTo(cameraPosition));
        }

        [UnityTest]
        public IEnumerator CombatEffectsStayBoundedExpireAndClearWhenTheRoundChanges()
        {
            deployment.NewGame();
            Assert.That(deployment.TryPlace(UnitRole.Engineer, BattleFrameBehaviorTests.FindFreePlacement(deployment, new Vector3(0, 0, -12)), out var unit), Is.True);
            foreach (var other in director.Units) other.GetComponent<BattleUnitAI>().enabled = false;
            director.BeginBattle();
            var effects = root.GetComponent<BattleEffects>();
            for (int i = 0; i < 200; i++) effects.Impact(unit.EyePosition, Vector3.up, Color.gray);
            effects.Pulse(unit.transform.position, 4, Color.green);
            effects.Lightning(unit.EyePosition, unit.EyePosition + Vector3.forward * 3);
            effects.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
            Assert.That(effects.ActiveCount, Is.InRange(1, BattleEffects.Capacity));
            Assert.That(effects.transform.Find("Batched combat effects").GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(effects.transform.Find("Batched combat effects").GetComponent<Collider>(), Is.Null);
            CaptureScene("Ability-effects", new Vector3(5, 3, -17), new Vector3(0, 1, -11));
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(effects.ActiveCount, Is.Zero);
            effects.Pulse(unit.transform.position, 4, Color.green);
            deployment.NewGame();
            Assert.That(effects.ActiveCount, Is.Zero);
            Assert.That(effects.transform.Find("Batched combat effects").GetComponent<Renderer>().enabled, Is.False);
        }

        void CaptureScene(string name, Vector3 position, Vector3 target)
        {
            string directory = Environment.GetEnvironmentVariable("BATTLEFIELD_PREVIEW_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            System.IO.Directory.CreateDirectory(directory);
            var obj = new GameObject("Presentation review camera", typeof(Camera));
            obj.transform.SetParent(root.transform, false);
            obj.transform.position = position;
            obj.transform.LookAt(target);
            var camera = obj.GetComponent<Camera>();
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 180;
            camera.fieldOfView = 55;
            camera.enabled = false;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var buffer = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = buffer;
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = buffer });
                else camera.Render();
                RenderTexture.active = buffer;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(buffer); UnityEngine.Object.Destroy(obj);
            }
        }
    }
}
