using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace BattlefieldPrototype
{
    /// <summary>Asset-backed OpenXR bindings, shared by gameplay and UI. Feature API fallback supports simulators.</summary>
    public sealed class BattleTouchInput : IDisposable
    {
        readonly InputActionAsset asset;
        public Hand Left { get; }
        public Hand Right { get; }

        public BattleTouchInput()
        {
            var source = Resources.Load<InputActionAsset>("SkyholdTouch");
            if (source == null) throw new InvalidOperationException("SkyholdTouch input asset is missing.");
            asset = UnityEngine.Object.Instantiate(source);
            Left = new Hand(asset.FindActionMap("Left", true), XRNode.LeftHand);
            Right = new Hand(asset.FindActionMap("Right", true), XRNode.RightHand);
            asset.Enable();
        }

        public void Dispose()
        {
            asset.Disable();
            if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
            else UnityEngine.Object.DestroyImmediate(asset);
        }

        public sealed class Hand
        {
            readonly InputActionMap map;
            readonly XRNode node;
            UnityEngine.XR.InputDevice Legacy => InputDevices.GetDeviceAtXRNode(node);
            bool HasActions => map["Position"].controls.Count > 0;
            public bool Tracked => HasActions ? map["Tracked"].IsPressed() :
                Legacy.isValid && (Legacy.characteristics & InputDeviceCharacteristics.HandTracking) == 0 &&
                (!Legacy.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || tracked);
            internal Hand(InputActionMap actions, XRNode hand) { map = actions; node = hand; }
            bool Button(string action, InputFeatureUsage<bool> usage)
            {
                if (!Tracked) return false;
                if (HasActions) return map[action].IsPressed();
                return Legacy.TryGetFeatureValue(usage, out bool value) && value;
            }
            float Axis(string action, InputFeatureUsage<float> usage)
            {
                if (!Tracked) return 0;
                if (HasActions) return map[action].ReadValue<float>();
                return Legacy.TryGetFeatureValue(usage, out float value) ? value : 0;
            }
            public Vector2 Move
            {
                get
                {
                    if (!Tracked) return Vector2.zero;
                    if (HasActions) return map["Stick"].ReadValue<Vector2>();
                    return Legacy.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 value) ? value : Vector2.zero;
                }
            }
            public bool Primary => Button("Primary", CommonUsages.primaryButton);
            public bool Secondary => Button("Secondary", CommonUsages.secondaryButton);
            public bool Menu => Button("Menu", CommonUsages.menuButton);
            public bool StickClick => Button("StickClick", CommonUsages.primary2DAxisClick);
            public float Grip => Axis("Grip", CommonUsages.grip);
            public bool GripHeld => Grip > .55f || Button("GripPressed", CommonUsages.gripButton);
            public bool TriggerHeld => Axis("Trigger", CommonUsages.trigger) > .55f || Button("TriggerPressed", CommonUsages.triggerButton);
            public bool TryPose(bool aim, out Vector3 position, out Quaternion rotation)
            {
                position = default; rotation = Quaternion.identity;
                if (!Tracked) return false;
                if (HasActions)
                {
                    bool pointer = aim && map["AimPosition"].controls.Count > 0;
                    position = map[pointer ? "AimPosition" : "Position"].ReadValue<Vector3>();
                    rotation = map[pointer ? "AimRotation" : "Rotation"].ReadValue<Quaternion>();
                    return true;
                }
                return Legacy.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
                    Legacy.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            }
            public void Pulse(float amplitude, float duration)
            {
                if (!Tracked) return;
                var device = map["Grip"].activeControl?.device as XRControllerWithRumble;
                if (device == null && map["Position"].controls.Count > 0)
                    device = map["Position"].controls[0].device as XRControllerWithRumble;
                if (device != null) device.SendImpulse(Mathf.Clamp01(amplitude), duration);
                else if (Legacy.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                    Legacy.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
            }
        }
    }
}
