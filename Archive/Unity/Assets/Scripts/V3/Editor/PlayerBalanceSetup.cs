using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PuzzleApple.V3.Cognition;
using Object=UnityEngine.Object;

namespace PuzzleApple.V3.Editor
{
    public static class PlayerBalanceSetup
    {
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play first");
            var room=Object.FindFirstObjectByType<OpeningRoom>();var catalog=room.board.Catalog;
            const string path="Assets/GameData/V3/Opening/Rules/i-equal-equal.asset";
            var rule=AssetDatabase.LoadAssetAtPath<CognitionRule>(path);
            if(!rule){rule=ScriptableObject.CreateInstance<CognitionRule>();AssetDatabase.CreateAsset(rule,path);}
            var data=new SerializedObject(rule);data.FindProperty("id").stringValue="opening-i-equal-equal";
            data.FindProperty("effect").enumValueIndex=(int)CognitionSignal.EqualBalance;data.FindProperty("exclusionKey").stringValue="form:i";
            var words=data.FindProperty("words");words.arraySize=3;
            for(int i=0;i<3;i++)words.GetArrayElementAtIndex(i).objectReferenceValue=catalog.Word(i==0?"i":"equal");
            data.ApplyModifiedPropertiesWithoutUndo();
            var c=new SerializedObject(catalog);var rules=c.FindProperty("rules");
            if(!catalog.Rules.Contains(rule))rules.GetArrayElementAtIndex(rules.arraySize++).objectReferenceValue=rule;
            c.ApplyModifiedPropertiesWithoutUndo();catalog.ValidateOrThrow();
            var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
            try{Configure(root.GetComponent<OpeningRoom>());PrefabUtility.SaveAsPrefabAsset(root,OpeningSetup.PrefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(room.gameObject.scene);
            return "Player balance identity, model, physical pans and sensors authored.";
        }
        static void Configure(OpeningRoom room)
        {
            if(room.playerBalance)return;
            var mobile=room.player.gameObject.AddComponent<PlayerBalance>();room.playerBalance=mobile;mobile.room=room;
            var visual=Object.Instantiate(room.mainRoute.balance.gameObject,room.player.transform);visual.name="Player balance appearance";
            visual.transform.localPosition=new Vector3(0,.16f,0);visual.transform.localRotation=Quaternion.Euler(0,90,0);visual.transform.localScale=Vector3.one;
            foreach(var script in visual.GetComponentsInChildren<MonoBehaviour>(true))if(!(script is LocalAmbientProbe))Object.DestroyImmediate(script);
            mobile.visual=visual;mobile.slots=visual.AddComponent<DestinationSlots>();
            mobile.slots.places=new[]{visual.transform.Find("Tray slot 0"),visual.transform.Find("Tray slot 1")};
            mobile.slots.sensors=mobile.slots.places.Select(t=>t.GetComponentInChildren<BoxCollider>()).ToArray();
            var axis=new GameObject("Player balance reflection plane").transform;axis.SetParent(visual.transform,false);axis.localRotation=Quaternion.identity;mobile.axis=axis;
            mobile.hideInFirstPerson=visual.GetComponentsInChildren<Renderer>().Where(r=>!r.name.StartsWith("Pan")).ToArray();
            visual.SetActive(false);EditorUtility.SetDirty(room);
        }
    }
}
