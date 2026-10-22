using System;
using System.Linq;
using PuzzleApple.V4.Cognition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V4.Editor
{
    public static class OpeningSetup
    {
        const string Data = "Assets/GameData/V4/Opening";
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        static void Ref(UnityEngine.Object target, string key, UnityEngine.Object value)
        {
            var so = new SerializedObject(target); so.FindProperty(key).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Refs(UnityEngine.Object target, string key, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(key); p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/V4/P4.unity") throw new InvalidOperationException("Open P4 in Edit Mode.");
            if (UnityEngine.Object.FindFirstObjectByType<OpeningFlow>()) throw new InvalidOperationException("Opening already installed; edit its native components directly.");
            Folder(Data + "/Words"); Folder(Data + "/Rules");
            string[] ids = { "positive", "negative", "world", "move", "light" };
            string[] symbols = { "Positive", "Negative", "Apple", "Move", "red" };
            var words = new WordDefinition[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                words[i] = Asset<WordDefinition>(Data + "/Words/" + ids[i] + ".asset");
                var so = new SerializedObject(words[i]);
                so.FindProperty("id").stringValue = ids[i]; so.FindProperty("displayText").stringValue = ids[i];
                so.FindProperty("symbol").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/V3/" + symbols[i] + ".png");
                var senses = so.FindProperty("senses"); senses.arraySize = 1;
                senses.GetArrayElementAtIndex(0).FindPropertyRelative("meaning").stringValue = ids[i];
                senses.GetArrayElementAtIndex(0).FindPropertyRelative("role").enumValueIndex = i < 2 || i == 3 ? 1 : 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var rules = new CognitionRule[2];
            for (int i = 0; i < 2; i++)
            {
                rules[i] = Asset<CognitionRule>(Data + "/Rules/" + (i == 0 ? "LightOn" : "LightOff") + ".asset");
                var so = new SerializedObject(rules[i]); so.FindProperty("id").stringValue = i == 0 ? "light.on" : "light.off";
                so.FindProperty("effect").enumValueIndex = i == 0 ? 1 : 2;
                so.FindProperty("exclusionKey").stringValue = "light.state"; so.ApplyModifiedPropertiesWithoutUndo();
                Refs(rules[i], "words", new UnityEngine.Object[] { words[i], words[4] });
            }
            var catalog = Asset<CognitionCatalog>(Data + "/OpeningCatalog.asset");
            Refs(catalog, "words", words); Refs(catalog, "rules", rules); Refs(catalog, "conflicts", new UnityEngine.Object[0]);
            catalog.ValidateOrThrow();

            const string uiPath = "Assets/Prefabs/V4/UI/V4Interface.prefab";
            var contents = PrefabUtility.LoadPrefabContents(uiPath);
            try
            {
                UnityEngine.Object.DestroyImmediate(contents.GetComponent<PuzzleApple.V3.V3Presentation>());
                UnityEngine.Object.DestroyImmediate(contents.transform.Find("V3 HUD").gameObject);
                UnityEngine.Object.DestroyImmediate(contents.transform.Find("World display").gameObject);
                contents.name = "V4Interface"; contents.GetComponent<Canvas>().sortingOrder = 20;
                Ref(contents.GetComponent<CognitionBoard>(), "catalog", catalog);
                var libraryData = new SerializedObject(contents.GetComponent<WordLibrary>());
                libraryData.FindProperty("vocabularyImages").arraySize = 0; libraryData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, uiPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(uiPath));
            var board = ui.GetComponent<CognitionBoard>();
            var old = UnityEngine.Object.FindFirstObjectByType<PuzzleApple.General.BasicFirstPersonController>();
            var playerObject = old.gameObject;
            UnityEngine.Object.DestroyImmediate(old);
            var player = playerObject.AddComponent<FirstPersonController>(); player.view = player.GetComponentInChildren<Camera>().transform;
            Ref(ui.GetComponent<GameplayPanel>(), "player", player);
            var lamp = GameObject.Find("Glowing Cube").AddComponent<OpeningLight>();
            lamp.surface = lamp.GetComponent<Renderer>(); lamp.glow = lamp.GetComponentInChildren<Light>();
            var flow = ui.AddComponent<OpeningFlow>();
            flow.player = player; flow.board = board; flow.library = ui.GetComponent<WordLibrary>(); flow.lamp = lamp;
            var hud = new GameObject("Opening HUD", typeof(RectTransform)); hud.transform.SetParent(ui.transform, false);
            var rect = (RectTransform)hud.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            flow.hud = rect;
            RestoreHud(flow);
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>(); entry.player = player; entry.opening = flow;
            PrefabUtility.RecordPrefabInstancePropertyModifications(entry);
            EditorUtility.SetDirty(entry);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        public static void RestoreHud(OpeningFlow flow)
        {
            // Copy the authored HUD geometry, sprite, and contrast material exactly.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/V3/UI/V3Interface.prefab");
            var so = new SerializedObject(source.GetComponent<PuzzleApple.V3.V3Presentation>());
            for (int i = flow.hud.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(flow.hud.GetChild(i).gameObject);
            var eye = (Image)so.FindProperty("eye").objectReferenceValue;
            flow.hoverEye = UnityEngine.Object.Instantiate(eye, flow.hud, false); flow.hoverEye.name = "Collectible";
            var cross = (GameObject)so.FindProperty("cross").objectReferenceValue;
            flow.reticle = UnityEngine.Object.Instantiate(cross, flow.hud, false); flow.reticle.name = "Reticle";
            var symbol = (Image)so.FindProperty("symbol").objectReferenceValue;
            var go = new GameObject("Collected symbol", typeof(RectTransform), typeof(CenteredSymbolImage)); go.transform.SetParent(flow.hud, false);
            flow.collectionSymbol = go.GetComponent<CenteredSymbolImage>();
            flow.collectionSymbol.rectTransform.anchorMin = symbol.rectTransform.anchorMin;
            flow.collectionSymbol.rectTransform.anchorMax = symbol.rectTransform.anchorMax;
            flow.collectionSymbol.rectTransform.pivot = symbol.rectTransform.pivot;
            flow.collectionSymbol.rectTransform.sizeDelta = symbol.rectTransform.sizeDelta;
            flow.collectionSymbol.raycastTarget = false; flow.collectionSymbol.preserveAspect = true;
            flow.collectionSymbol.gameObject.SetActive(false); flow.hoverEye.gameObject.SetActive(false); flow.reticle.SetActive(false);
            EditorUtility.SetDirty(flow);
        }
    }
}
