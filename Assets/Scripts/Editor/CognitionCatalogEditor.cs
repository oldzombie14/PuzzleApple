using System;
using PuzzleApple.Cognition;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.Editor
{
    [CustomEditor(typeof(CognitionCatalog))]
    public sealed class CognitionCatalogEditor : UnityEditor.Editor
    {
        public const string TutorialPath = "Assets/GameData/Cognition/Catalog/TutorialCognition.asset";
        public static CognitionCatalog LoadTutorial()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CognitionCatalog>(TutorialPath);
            if (!catalog) throw new InvalidOperationException("Missing tutorial cognition catalog: " + TutorialPath);
            catalog.ValidateOrThrow();
            return catalog;
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var errors = ((CognitionCatalog)target).Validate();
            if (errors.Count == 0) EditorGUILayout.HelpBox("Catalog valid. Rules are loaded when a new Play session starts.", MessageType.Info);
            else foreach (var error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
        [MenuItem("PuzzleApple/Cognition/Validate catalogs")]
        public static void ValidateCatalogs()
        {
            var guids = AssetDatabase.FindAssets("t:CognitionCatalog");
            if (guids.Length == 0) throw new InvalidOperationException("No cognition catalogs found.");
            foreach (var guid in guids)
                AssetDatabase.LoadAssetAtPath<CognitionCatalog>(AssetDatabase.GUIDToAssetPath(guid)).ValidateOrThrow();
            Debug.Log("PASS: " + guids.Length + " cognition catalogs validated");
        }
    }
}
