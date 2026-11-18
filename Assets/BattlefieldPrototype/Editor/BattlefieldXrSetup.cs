using System;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

namespace BattlefieldPrototype.Editor
{
    /// <summary>Creates the tracked camera/controller hierarchy using the installed OpenXR packages.</summary>
    public static class BattlefieldXrSetup
    {
        const string UndoLabel = "Set Up XR Origin (VR)";
        const string ActionsPath = "Assets/BattlefieldPrototype/Resources/SkyholdTouch.inputactions";

        [MenuItem("Tools/Battlefield Prototype/Set Up XR Origin (VR)")]
        public static void SetUpCurrentScene()
        {
            Selection.activeGameObject = SetUp(SceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Battlefield Prototype/Set Up XR Origin (VR)", true)]
        static bool CanSetUp() => !EditorApplication.isPlayingOrWillChangePlaymode &&
            PrefabStageUtility.GetCurrentPrefabStage() == null;

        public static GameObject SetUp(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new ArgumentException("A loaded scene is required.", nameof(scene));

            var roots = scene.GetRootGameObjects();
            // Repeated runs and scenes already using a Meta rig must not get a second player rig.
            foreach (var root in roots)
            {
                var existing = root.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(
                    component => component != null &&
                        (component is XROrigin || component.GetType().Name == "OVRCameraRig"));
                if (existing != null)
                {
                    var existingOrigin = existing as XROrigin;
                    var cameras = existing.GetComponentsInChildren<Camera>(true);
                    var existingCamera = existingOrigin != null ? existingOrigin.Camera :
                        cameras.FirstOrDefault(candidate => candidate.name == "CenterEyeAnchor") ?? cameras.FirstOrDefault();
                    if (existingCamera == null)
                        throw new InvalidOperationException("Assign the existing XR rig's head camera before running setup.");
                    var anchors = existing.GetComponentsInChildren<Transform>(true);
                    var leftAnchor = anchors.FirstOrDefault(anchor => anchor.name == "Left Controller" || anchor.name == "LeftHandAnchor");
                    var rightAnchor = anchors.FirstOrDefault(anchor => anchor.name == "Right Controller" || anchor.name == "RightControllerAnchor");
                    Undo.IncrementCurrentGroup();
                    int existingUndoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName(UndoLabel);
                    ConnectRig(roots, existing.gameObject, existingCamera, leftAnchor, rightAnchor);
                    Undo.CollapseUndoOperations(existingUndoGroup);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Debug.Log("Reused the existing XR rig and removed any standalone default camera.", existing);
                    return existing.gameObject;
                }
            }

            // Validate the shared gameplay bindings before changing the hierarchy.
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null) throw new InvalidOperationException("Missing input actions: " + ActionsPath);
            var left = actions.FindActionMap("Left", true);
            var right = actions.FindActionMap("Right", true);
            var leftPosition = left.FindAction("Position", true).Clone();
            var leftRotation = left.FindAction("Rotation", true).Clone();
            var rightPosition = right.FindAction("AimPosition", true).Clone();
            var rightRotation = right.FindAction("AimRotation", true).Clone();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoLabel);
            var rig = CreateObject("XR Origin (VR)", scene);
            var offset = CreateObject("Camera Offset", scene, rig.transform);
            var head = CreateObject("Main Camera", scene, offset.transform);
            head.tag = "MainCamera";
            var camera = Undo.AddComponent<Camera>(head);
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 500f;
            Undo.AddComponent<AudioListener>(head);
            AddPoseDriver(head,
                PoseAction("Head Position", "<XRHMD>/centerEyePosition", "Vector3"),
                PoseAction("Head Rotation", "<XRHMD>/centerEyeRotation", "Quaternion"),
                "<XRHMD>/trackingState");

            var leftController = CreateObject("Left Controller", scene, offset.transform);
            AddPoseDriver(leftController, leftPosition, leftRotation, "<XRController>{LeftHand}/trackingState");
            var rightController = CreateObject("Right Controller", scene, offset.transform);
            AddPoseDriver(rightController, rightPosition, rightRotation, "<XRController>{RightHand}/trackingState");

            var origin = Undo.AddComponent<XROrigin>(rig);
            origin.Origin = rig;
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.CameraYOffset = 1.65f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            ConnectRig(roots, rig, camera, leftController.transform, rightController.transform);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            return rig;
        }

        static void ConnectRig(GameObject[] roots, GameObject rig, Camera camera, Transform leftController, Transform rightController)
        {
            foreach (var root in roots)
            {
                // Only remove the standalone default camera, never a camera nested inside another rig.
                if (root != rig && root != camera.gameObject && root.name == "Main Camera" && root.GetComponent<Camera>() != null)
                {
                    Undo.DestroyObjectImmediate(root);
                    continue;
                }

                foreach (var bootstrap in root.GetComponentsInChildren<BattlefieldBootstrap>(true))
                {
                    Undo.RecordObject(bootstrap, UndoLabel);
                    bootstrap.ExistingRigRoot = rig.transform;
                    bootstrap.ExistingHeadCamera = camera;
                    bootstrap.ExistingLeftHandGrip = leftController;
                    bootstrap.ExistingRightHandAim = rightController;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(bootstrap);
                }
            }

        }

        static GameObject CreateObject(string name, Scene scene, Transform parent = null)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, scene);
            Undo.RegisterCreatedObjectUndo(obj, UndoLabel);
            if (parent != null) Undo.SetTransformParent(obj.transform, parent, UndoLabel);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            return obj;
        }

        static InputAction PoseAction(string name, string binding, string controlType) =>
            new InputAction(name, InputActionType.Value, binding, expectedControlType: controlType);

        static void AddPoseDriver(GameObject target, InputAction position, InputAction rotation, string trackingState)
        {
            var driver = Undo.AddComponent<TrackedPoseDriver>(target);
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            // Inline actions are enabled/disabled by the driver; no Input Action Manager is required.
            driver.positionInput = new InputActionProperty(position);
            driver.rotationInput = new InputActionProperty(rotation);
            driver.trackingStateInput = new InputActionProperty(PoseAction("Tracking State", trackingState, "Integer"));
            driver.ignoreTrackingState = false;
        }
    }
}
