using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceRegionSetup
    {
        static void WriteLock(string name,Space unlocked,Space requires,float passable=.5f){
            var path="Assets/Prefabs/V5/Resources/V5/"+name+".prefab";
            bool existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go=existing?PrefabUtility.LoadPrefabContents(path):new GameObject(name);
            try{
                var gate=go.GetComponent<ProgressLock>();if(!gate)gate=go.AddComponent<ProgressLock>();
                gate.unlocksSpaces=unlocked;gate.requiresSpaces=requires;gate.passableProgress=passable;
                PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{if(existing)PrefabUtility.UnloadPrefabContents(go);else Object.DestroyImmediate(go);}
        }
        [MenuItem("PuzzleApple/V5/Apply sentence regions")]
        public static void Apply(){
            if(Application.isPlaying)throw new System.InvalidOperationException("Apply in Edit mode.");
            WriteLock("PassageRevealProgressLock",Space.FirstPath,Space.Chamber,1);
            WriteLock("WhiteDoorProgressLock",Space.SphereRoom|Space.ReturnPath,Space.FirstPath);
            WriteLock("RoomEntranceProgressLock",Space.FarRoom,Space.ReturnPath);
            const string endingPath="Assets/Prefabs/V5/Resources/V5/IntroEnding.prefab";
            var ending=PrefabUtility.LoadPrefabContents(endingPath);
            try{
                var gate=ending.GetComponent<ProgressLock>();if(!gate)gate=ending.AddComponent<ProgressLock>();
                gate.chargeDoor=ending.GetComponent<ChargeDoor>();gate.unlocksSpaces=Space.Ascent;gate.requiresSpaces=Space.FarRoom;
                PrefabUtility.SaveAsPrefabAsset(ending,endingPath);
            }finally{PrefabUtility.UnloadPrefabContents(ending);}
            var active=SceneManager.GetActiveScene();
            foreach(var name in new[]{"ChapterI_Garden","ChapterI_Vitruvian"}){
                var path="Assets/Scenes/V5/"+name+".unity";
                if(SceneManager.GetSceneByPath(path).isLoaded)throw new System.InvalidOperationException("Close the Chapter scene first to preserve its unsaved edits.");
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try{
                    var room=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ChapterRoom>(true)).First();
                    var door=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MembraneDoor>(true)).FirstOrDefault();
                    if(!door){
                        var go=new GameObject("Membrane entrance");go.transform.SetParent(room.transform,false);
                        go.transform.position=new Vector3(room.floor.bounds.center.x,room.floor.bounds.max.y,room.floor.bounds.max.z-.12f);
                        door=go.AddComponent<MembraneDoor>();
                        var trigger=go.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(8.6f,12.21f,.3f);trigger.center=Vector3.up*6.105f;
                    }
                    door.inside=name.EndsWith("Garden")?Space.Garden:Space.Vitruvian;door.interior=room.roomFill;door.room=room;
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }finally{EditorSceneManager.CloseScene(scene,true);}
            }
            SceneManager.SetActiveScene(active);
        }
    }
}
