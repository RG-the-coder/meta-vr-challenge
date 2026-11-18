using System;
using System.Linq;
using BattlefieldPrototype.Editor;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

namespace BattlefieldPrototype.Tests
{
    public sealed class XrSetupTests
    {
        Scene scene;
        string savedPath;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (savedPath != null) AssetDatabase.DeleteAsset(savedPath);
            savedPath = null;
        }

        [Test]
        public void SavedSceneRetainsHeadTrackingHandBindingsAndGameplayReferences()
        {
            new GameObject("Battlefield").AddComponent<BattlefieldBootstrap>();
            new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            BattlefieldXrSetup.SetUp(scene);
            savedPath = "Assets/XrSetupTest_" + Guid.NewGuid().ToString("N") + ".unity";
            Assert.That(EditorSceneManager.SaveScene(scene, savedPath), Is.True);
            scene = EditorSceneManager.OpenScene(savedPath, OpenSceneMode.Single);
            var origin = scene.GetRootGameObjects().Select(root => root.GetComponent<XROrigin>()).First(value => value != null);
            var bootstrap = scene.GetRootGameObjects().Select(root => root.GetComponent<BattlefieldBootstrap>()).First(value => value != null);
            Assert.That(bootstrap.ExistingRigRoot, Is.EqualTo(origin.transform));
            Assert.That(bootstrap.ExistingHeadCamera, Is.EqualTo(origin.Camera));
            Assert.That(origin.CameraFloorOffsetObject.transform.parent, Is.EqualTo(origin.transform));
            Assert.That(origin.RequestedTrackingOriginMode, Is.EqualTo(XROrigin.TrackingOriginMode.Floor));
            Assert.That(scene.GetRootGameObjects().Count(root => root.name == "Main Camera"), Is.Zero);
            Assert.That(origin.GetComponentsInChildren<Camera>().Length, Is.EqualTo(1));
            Assert.That(origin.GetComponentsInChildren<AudioListener>().Length, Is.EqualTo(1));
            var head = origin.Camera.GetComponent<TrackedPoseDriver>();
            Assert.That(head.positionInput.action.bindings.Single().path, Is.EqualTo("<XRHMD>/centerEyePosition"));
            Assert.That(head.rotationInput.action.bindings.Single().path, Is.EqualTo("<XRHMD>/centerEyeRotation"));
            AssertHand(bootstrap.ExistingLeftHandGrip, "LeftHand", "devicePosition", "deviceRotation");
            AssertHand(bootstrap.ExistingRightHandAim, "RightHand", "pointerPosition", "pointerRotation");
        }

        static void AssertHand(Transform hand, string usage, string position, string rotation)
        {
            var driver = hand.GetComponent<TrackedPoseDriver>();
            Assert.That(driver.positionInput.action.bindings.Count, Is.EqualTo(2));
            Assert.That(driver.positionInput.action.bindings.All(binding => binding.path.EndsWith("{" + usage + "}/" + position)), Is.True);
            Assert.That(driver.rotationInput.action.bindings.All(binding => binding.path.EndsWith("{" + usage + "}/" + rotation)), Is.True);
            Assert.That(driver.trackingStateInput.action.bindings.Single().path, Is.EqualTo("<XRController>{" + usage + "}/trackingState"));
            Assert.That(driver.updateType, Is.EqualTo(TrackedPoseDriver.UpdateType.UpdateAndBeforeRender));
            Assert.That(driver.ignoreTrackingState, Is.False);
            Assert.That(driver.positionInput.reference, Is.Null, "Inline actions are owned and enabled by the driver.");
        }

        [Test]
        public void UndoRestoresDefaultCameraAndGameplayReferencesAndRedoRestoresRig()
        {
            var bootstrap = new GameObject("Battlefield").AddComponent<BattlefieldBootstrap>();
            new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            BattlefieldXrSetup.SetUp(scene);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(scene.GetRootGameObjects().Any(root => root.name == "Main Camera" && root.GetComponent<Camera>() != null), Is.True);
            Assert.That(scene.GetRootGameObjects().Any(root => root.GetComponent<XROrigin>() != null), Is.False);
            Assert.That(bootstrap.ExistingRigRoot, Is.Null);
            Assert.That(bootstrap.ExistingHeadCamera, Is.Null);
            Undo.PerformRedo();
            Assert.That(bootstrap.ExistingHeadCamera, Is.Not.Null);
            Assert.That(bootstrap.ExistingLeftHandGrip.GetComponent<TrackedPoseDriver>(), Is.Not.Null);
            Assert.That(bootstrap.ExistingRightHandAim.GetComponent<TrackedPoseDriver>(), Is.Not.Null);
        }

        [Test]
        public void RepeatedSetupReusesInactiveRigAndLeavesOtherScenesAndCamerasAlone()
        {
            var otherCamera = new GameObject("Spectator Camera", typeof(Camera));
            savedPath = "Assets/XrSetupTest_" + Guid.NewGuid().ToString("N") + ".unity";
            Assert.That(EditorSceneManager.SaveScene(scene, savedPath), Is.True);
            var otherScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(otherScene);
            var otherDefault = new GameObject("Main Camera", typeof(Camera));
            try
            {
                SceneManager.SetActiveScene(scene);
                var rig = BattlefieldXrSetup.SetUp(scene);
                rig.SetActive(false);
                var extraDefault = new GameObject("Main Camera", typeof(Camera));
                Assert.That(BattlefieldXrSetup.SetUp(scene), Is.SameAs(rig));
                Assert.That(extraDefault == null, Is.True);
                Assert.That(scene.GetRootGameObjects().Count(root => root.GetComponent<XROrigin>() != null), Is.EqualTo(1));
                Assert.That(otherCamera != null, Is.True);
                Assert.That(otherDefault != null, Is.True);
            }
            finally { EditorSceneManager.CloseScene(otherScene, true); }
        }
    }
}
