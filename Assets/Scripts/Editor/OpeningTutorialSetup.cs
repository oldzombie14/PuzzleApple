using System;
using PuzzleApple.Cognition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleApple.Editor
{
    public static class OpeningTutorialSetup
    {
        [MenuItem("PuzzleApple/Tutorial/Configure opening")]
        public static void Configure()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/TutorialLevel.unity")
                throw new InvalidOperationException("Open TutorialLevel in Edit Mode.");
            if (UnityEngine.Object.FindFirstObjectByType<OpeningTutorial>())
                throw new InvalidOperationException("Opening already configured; edit its Inspector settings.");
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            var board = UnityEngine.Object.FindFirstObjectByType<CognitionBoard>();
            var root = new GameObject("Opening Tutorial");
            Undo.RegisterCreatedObjectUndo(root, "Configure opening tutorial");
            var mirror = new GameObject("Mirror"); mirror.transform.SetParent(root.transform);
            mirror.transform.position = new Vector3(9.0f, 1.22f, -1.54f);
            var target = AuthoredInteractableSetup.ConfigureMirror(mirror.transform);
            var end = new GameObject("Walking recovery endpoint"); end.transform.SetParent(root.transform);
            end.transform.position = new Vector3(3.35f, .08f, 0);
            var tutorial = Undo.AddComponent<OpeningTutorial>(root);
            var data = new SerializedObject(tutorial);
            Assign(data,"player",player); Assign(data,"view",Camera.main); Assign(data,"board",board);
            Assign(data,"presentation",board.GetComponent<CognitionWorldInteraction>());
            Assign(data,"mirrorTarget",target); Assign(data,"recoveryEnd",end.transform);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        static void Assign(SerializedObject data, string name, UnityEngine.Object value) => data.FindProperty(name).objectReferenceValue = value;
    }
}

