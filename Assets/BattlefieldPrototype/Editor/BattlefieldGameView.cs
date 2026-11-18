using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BattlefieldPrototype.Editor
{
    /// <summary>Keep the demo's complete render visible when Unity restores a zoomed Game view.</summary>
    [InitializeOnLoad]
    public static class BattlefieldGameView
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static BattlefieldGameView()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (!Application.isBatchMode) EditorApplication.delayCall += FitDemoView;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!Application.isBatchMode && state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += FitDemoView;
        }

        static void FitDemoView()
        {
            if (Object.FindAnyObjectByType<BattlefieldBootstrap>() != null) FitGameView();
        }

        [MenuItem("Tools/Battlefield Prototype/Fit Game View")]
        public static void FitGameView()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var type = window.GetType();
                if (type.FullName != "UnityEditor.GameView") continue;
                var zoomField = type.GetField("m_ZoomArea", Instance);
                var defaultField = type.GetField("m_defaultScale", Instance);
                var update = type.GetMethod("UpdateZoomAreaAndParent", Instance);
                var zoom = zoomField?.GetValue(window);
                var scale = zoom?.GetType().GetProperty("scale", Instance);
                if (zoom == null || scale == null || defaultField == null || update == null) continue;
                // Let Unity calculate its own fit scale (including monitor DPI) and
                // recenter, using the same path as a default-scale viewport resize.
                defaultField.SetValue(window, ((Vector2)scale.GetValue(zoom)).y);
                update.Invoke(window, null);
                window.Repaint();
            }
        }
    }
}
