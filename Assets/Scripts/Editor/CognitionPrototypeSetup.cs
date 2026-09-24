using System;
using PuzzleApple.Cognition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleApple.Editor
{
    public static class CognitionPrototypeSetup
    {
        [MenuItem("PuzzleApple/Cognition/Configure TutorialLevel")]
        public static void Configure()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/TutorialLevel.unity" || Application.isPlaying)
                throw new InvalidOperationException("Open TutorialLevel in Edit Mode first.");
            var panel = UnityEngine.Object.FindFirstObjectByType<GameplayPanel>();
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            var appleObject = GameObject.Find("AppleLowPoly");
            if (!panel || !player || !appleObject) throw new InvalidOperationException("Expected panel/player/apple are missing.");
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/UI/Icons/viewer.png");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            var board = Ensure<CognitionBoard>(panel.gameObject);
            Assign(board,"panel",panel);
            Assign(board,"catalog",CognitionCatalogEditor.LoadTutorial());
            Assign(board,"chalkFont",AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Handodle-rg38A.ttf"));
            var apple = Ensure<CognitionApple>(appleObject); Assign(apple,"board",board);
            ApplePuzzleSetup.ConfigurePhysics(apple);
            var interaction = Ensure<CognitionWorldInteraction>(panel.gameObject);
            Assign(interaction,"board",board); Assign(interaction,"player",player); Assign(interaction,"view",Camera.main);
            Assign(interaction,"eyeSprite",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Icons/viewer.png"));
            ConfigureHoverIcons();
            ConfigureContrast();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        [MenuItem("PuzzleApple/Cognition/Configure reticle contrast")]
        public static void ConfigureContrast()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Configure reticle contrast in Edit Mode.");
            if (!AssetDatabase.IsValidFolder("Assets/Materials/UI")) AssetDatabase.CreateFolder("Assets/Materials","UI");
            const string materialPath = "Assets/Materials/UI/ReticleContrast.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/ReticleContrast.shader");
            if (!shader) throw new InvalidOperationException("Reticle contrast shader missing.");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material,materialPath); }
            var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/SacredSpaceV2_Renderer.asset");
            bool exists = false;
            foreach (var feature in renderer.rendererFeatures) if (feature is ReticleSceneColorFeature) exists = true;
            if (!exists)
            {
                var feature = ScriptableObject.CreateInstance<ReticleSceneColorFeature>();
                feature.name = "Reticle background contrast";
                AssetDatabase.AddObjectToAsset(feature,renderer);
                renderer.rendererFeatures.Add(feature);
                feature.Create(); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
            }
            var interaction = UnityEngine.Object.FindFirstObjectByType<CognitionWorldInteraction>();
            Assign(interaction,"reticleContrastMaterial",material);
            var settings = new SerializedObject(interaction);
            settings.FindProperty("reticleThickness").floatValue = 6;
            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(interaction.gameObject.scene);
            EditorSceneManager.SaveScene(interaction.gameObject.scene);
        }
        public static void ConfigureHoverIcons()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Configure icons in Edit Mode.");
            const string path = "Assets/UI/Icons/forbidden.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            var interaction = UnityEngine.Object.FindFirstObjectByType<CognitionWorldInteraction>();
            Assign(interaction,"forbiddenSprite",AssetDatabase.LoadAssetAtPath<Sprite>(path));
            var settings = new SerializedObject(interaction);
            settings.FindProperty("reticleSize").floatValue = 38;
            settings.FindProperty("reticleThickness").floatValue = 6;
            settings.FindProperty("eyeImageSize").floatValue = 140;
            settings.FindProperty("forbiddenImageSize").floatValue = 38;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(interaction.gameObject.scene);
            EditorSceneManager.SaveScene(interaction.gameObject.scene);
        }
        static T Ensure<T>(GameObject go) where T : Component => go.GetComponent<T>() ? go.GetComponent<T>() : Undo.AddComponent<T>(go);
        static void Assign(UnityEngine.Object target,string property,UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedProperties();
        }
    }
}
