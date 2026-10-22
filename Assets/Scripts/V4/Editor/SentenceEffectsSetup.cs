using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4.Editor
{
    // Incremental upgrade: preserves scene geometry, visuals, existing rule GUIDs and tuning.
    public static class SentenceEffectsSetup
    {
        const string Data="Assets/GameData/V4/Opening/";
        public static void Apply()
        {
            if(Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/V4/P4.unity")
                throw new InvalidOperationException("Open P4 in Edit mode.");
            var catalog=AssetDatabase.LoadAssetAtPath<CognitionCatalog>(Data+"OpeningCatalog.asset");
            var rules=catalog.Rules.ToList();
            Action<string,CognitionSignal[]> add=(sentence,effects)=>
            {
                var tokens=sentence.Split(' ');
                var rule=rules.FirstOrDefault(r=>string.Join(" ",r.Words.Select(w=>w.Id))==sentence);
                if(!rule)
                {
                    rule=ScriptableObject.CreateInstance<CognitionRule>();
                    AssetDatabase.CreateAsset(rule,Data+"Rules/"+sentence.Replace(' ', '_')+".asset");
                    rules.Add(rule);
                }
                var so=new SerializedObject(rule);
                if(string.IsNullOrEmpty(rule.Id)) so.FindProperty("id").stringValue=sentence.Replace(' ','.');
                var words=so.FindProperty("words"); words.arraySize=tokens.Length;
                for(int i=0;i<tokens.Length;i++) words.GetArrayElementAtIndex(i).objectReferenceValue=catalog.Word(tokens[i]);
                so.FindProperty("effect").intValue=(int)(effects.Length>0?effects[0]:CognitionSignal.Empty);
                var extra=so.FindProperty("additionalEffects"); extra.arraySize=Math.Max(0,effects.Length-1);
                for(int i=1;i<effects.Length;i++) extra.GetArrayElementAtIndex(i-1).intValue=(int)effects[i];
                // Built-in per-effect channels allow shared identical effects across composite rules.
                so.FindProperty("exclusionKey").stringValue="";
                so.ApplyModifiedPropertiesWithoutUndo();
            };
            add("positive door",new[]{CognitionSignal.DoorOn});
            add("negative door",new[]{CognitionSignal.DoorOff});
            var moves=new Dictionary<string,CognitionSignal>
            {
                {"light move",CognitionSignal.LightMove},{"door move",CognitionSignal.DoorMove},
                {"world move",CognitionSignal.WorldMove},{"door world move",CognitionSignal.DoorWorldMove},
                {"light world move",CognitionSignal.LightWorldMove},{"light door move",CognitionSignal.LightDoorMove},
                {"door light move",CognitionSignal.DoorLightMove},{"world light move",CognitionSignal.WorldLightMove},
                {"world door move",CognitionSignal.WorldDoorMove},{"light light move",CognitionSignal.LightLightMove}
            };
            foreach(var pair in moves)
            {
                add(pair.Key,new[]{pair.Value});
                string subject=pair.Key.Split(' ')[0];
                add("positive "+pair.Key,subject=="world"?new[]{pair.Value}:new[]{subject=="light"?CognitionSignal.LightOn:CognitionSignal.DoorOn,pair.Value});
                add("negative "+pair.Key,subject=="world"?new[]{CognitionSignal.WorldOff}:new[]{subject=="light"?CognitionSignal.LightOff:CognitionSignal.DoorOff,pair.Value});
            }
            var cs=new SerializedObject(catalog); var entries=cs.FindProperty("rules"); entries.arraySize=rules.Count;
            for(int i=0;i<rules.Count;i++) entries.GetArrayElementAtIndex(i).objectReferenceValue=rules[i];
            cs.ApplyModifiedPropertiesWithoutUndo(); catalog.ValidateOrThrow();
            var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();
            var manager=UnityEngine.Object.FindFirstObjectByType<SentenceEffectManager>();
            if(!manager) manager=new GameObject("Sentence Effects").AddComponent<SentenceEffectManager>();
            var light=Get<LightSentenceEffects>(manager.gameObject);
            var door=Get<DoorSentenceEffects>(manager.gameObject);
            var world=Get<WorldSentenceEffects>(manager.gameObject);
            light.field=flow.lightField; light.stateSource=flow.lamp; light.observer=flow.player.transform;
            light.ground=flow.lightField.ground; light.door=flow.door;
            door.door=flow.door; door.appearance=Get<DoorStateEffects>(flow.door.gameObject); door.lights=light; door.ground=light.ground;
            world.environment=UnityEngine.Object.FindFirstObjectByType<NightEnvironment>(); world.ground=light.ground;
            world.player=flow.player; world.door=flow.door; world.lights=light;
            manager.board=flow.board; manager.lights=light; manager.door=door; manager.world=world; flow.effects=manager;
            foreach(var obj in new UnityEngine.Object[]{flow,manager,light,door,world,door.appearance}) EditorUtility.SetDirty(obj);
            var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/V4/FishThreshold.prefab");
            Get<DoorStateEffects>(root); PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/V4/FishThreshold.prefab"); PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        static T Get<T>(GameObject go) where T:Component => go.GetComponent<T>() ?? go.AddComponent<T>();
    }
}
