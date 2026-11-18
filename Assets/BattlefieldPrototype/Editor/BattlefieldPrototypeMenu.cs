using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattlefieldPrototype.Editor
{
    public static class BattlefieldPrototypeMenu
    {
        const string ScenePath = "Assets/BattlefieldPrototype/Scenes/BattlefieldPrototype.unity";

        [MenuItem("Tools/Battlefield Prototype/Open Demo Scene")]
        public static void OpenDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Battlefield Prototype/Add To Current Scene")]
        public static void AddToCurrentScene()
        {
            if (Object.FindAnyObjectByType<BattlefieldBootstrap>() != null)
            {
                Selection.activeObject = Object.FindAnyObjectByType<BattlefieldBootstrap>().gameObject;
                return;
            }
            GameObject root = new GameObject("Battlefield Prototype");
            Undo.RegisterCreatedObjectUndo(root, "Add Battlefield Prototype");
            BattlefieldBootstrap bootstrap = root.AddComponent<BattlefieldBootstrap>();
            bootstrap.Vanguard = AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>("Assets/BattlefieldPrototype/Definitions/Vanguard.asset");
            bootstrap.Ranger = AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>("Assets/BattlefieldPrototype/Definitions/Ranger.asset");
            bootstrap.Medic = AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>("Assets/BattlefieldPrototype/Definitions/Medic.asset");
            bootstrap.Skirmisher = AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>("Assets/BattlefieldPrototype/Definitions/Skirmisher.asset");
            bootstrap.Brute = AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>("Assets/BattlefieldPrototype/Definitions/Brute.asset");
            bootstrap.Engineer = AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>("Assets/BattlefieldPrototype/Definitions/Engineer.asset");
            bootstrap.Stormcaller = AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>("Assets/BattlefieldPrototype/Definitions/Stormcaller.asset");
            var additional = new List<BattleUnitDefinition>();
            for (int role = (int)UnitRole.Wisp; role <= (int)UnitRole.Crownwing; role++)
                additional.Add(AssetDatabase.LoadAssetAtPath<BattleUnitDefinition>("Assets/BattlefieldPrototype/Definitions/" + (UnitRole)role + ".asset"));
            bootstrap.AdditionalCharacters = additional.ToArray();
            bootstrap.PrimitiveMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/BattlefieldPrototype/Materials/Primitive.mat");
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        [MenuItem("Tools/Battlefield Prototype/Use Demo For Build")]
        public static void UseDemoForBuild()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.path != ScenePath) scenes.Add(new EditorBuildSettingsScene(scene.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("Battlefield Prototype is now the enabled startup scene. Other scene entries are retained.");
        }
    }
}
