using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace BattlefieldPrototype.Editor
{
    // Observe real Editor input without substituting synthetic hands or changing Play state.
    [InitializeOnLoad]
    public static class BattlefieldHandDiagnostics
    {
        static double nextSample;
        static readonly List<XRHandSubsystem> Hands = new List<XRHandSubsystem>();
        static BattlefieldHandDiagnostics() => EditorApplication.update += Sample;

        static void Sample()
        {
            if (Application.isBatchMode || EditorApplication.isCompiling ||
                EditorApplication.timeSinceStartup < nextSample) return;
            nextSample = EditorApplication.timeSinceStartup + 2;
            var report = new StringBuilder();
            report.AppendLine(DateTime.UtcNow.ToString("O"));
            report.AppendLine($"Playing={EditorApplication.isPlaying}; XRDeviceActive={XRSettings.isDeviceActive}; Focused={Application.isFocused}");
            SubsystemManager.GetSubsystems(Hands);
            report.AppendLine($"HandSubsystems={Hands.Count}");
            foreach (var subsystem in Hands)
            {
                report.AppendLine($"Subsystem={subsystem.subsystemDescriptor.id}; Running={subsystem.running}");
                if (!subsystem.running) continue;
                Describe(report, "Left", subsystem.leftHand);
                Describe(report, "Right", subsystem.rightHand);
            }
            foreach (var player in UnityEngine.Object.FindObjectsByType<PossessionController>(FindObjectsSortMode.None))
            {
                report.AppendLine($"Player={player.name}; Enabled={player.isActiveAndEnabled}; XR={player.IsUsingXR}; Optical={player.IsUsingOpticalHands}");
                var visual = player.GetComponent<BattleOpticalHands>();
                report.AppendLine($"HandRendererPresent={visual != null}; Enabled={visual != null && visual.isActiveAndEnabled}");
                foreach (var child in player.GetComponentsInChildren<Transform>(true))
                    if (child.name.EndsWith("tracked gauntlet"))
                        report.AppendLine($"{child.name}: Active={child.gameObject.activeInHierarchy}");
            }
            try
            {
                Directory.CreateDirectory("LocalValidation/LinkHandTracking");
                File.WriteAllText("LocalValidation/LinkHandTracking/LiveStatus.txt", report.ToString());
                if (EditorApplication.isPlaying)
                    File.WriteAllText("LocalValidation/LinkHandTracking/LastPlayStatus.txt", report.ToString());
            }
            catch (IOException) { /* A diagnostics reader may briefly hold the file. */ }
        }

        static void Describe(StringBuilder report, string side, XRHand hand)
        {
            Pose pose = default;
            bool palm = hand.isTracked && hand.GetJoint(XRHandJointID.Palm).TryGetPose(out pose);
            report.AppendLine($"{side}: Tracked={hand.isTracked}; Palm={palm}; PalmPosition={pose.position}");
        }
    }
}
