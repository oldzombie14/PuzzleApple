using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V4.Editor
{
    public static class NextRoomSetup
    {
        public static void Apply()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Edit mode only");
            const string path="Assets/Scenes/V4/V4_NextRoom.unity";
            var original=SceneManager.GetActiveScene();
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
            {
                var room=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                SceneManager.SetActiveScene(room);
                var root=new GameObject("Next room - 12m square"); root.transform.position=new Vector3(2000,0,0);
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor",new Color(.48f,.5f,.51f)); material.SetFloat("_Smoothness",.1f);
                AssetDatabase.CreateAsset(material,"Assets/Materials/V4/NextRoomStone.mat");
                Action<string,Vector3,Vector3> box=(name,position,scale)=>
                {
                    var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
                    go.transform.SetParent(root.transform,false); go.transform.localPosition=position; go.transform.localScale=scale;
                    go.GetComponent<Renderer>().sharedMaterial=material;
                };
                box("Floor",new Vector3(0,-.15f,0),new Vector3(12.6f,.3f,12.6f));
                box("North wall",new Vector3(0,1.75f,6.15f),new Vector3(12.6f,3.5f,.3f));
                box("South wall",new Vector3(0,1.75f,-6.15f),new Vector3(12.6f,3.5f,.3f));
                box("East wall",new Vector3(6.15f,1.75f,0),new Vector3(.3f,3.5f,12));
                box("West wall",new Vector3(-6.15f,1.75f,0),new Vector3(.3f,3.5f,12));
                var spawn=new GameObject("Arrival").AddComponent<NextRoomSpawn>(); spawn.transform.SetParent(root.transform,false);
                spawn.transform.localPosition=new Vector3(0,1.05f,-3.5f);
                var light=new GameObject("Room light").AddComponent<Light>(); light.transform.SetParent(root.transform,false);
                light.transform.localPosition=new Vector3(0,3,0); light.type=LightType.Point; light.range=14; light.intensity=3;
                light.color=new Color(.94f,.96f,1); light.shadows=LightShadows.Soft;
                EditorSceneManager.SaveScene(room,path); EditorSceneManager.CloseScene(room,true);
                SceneManager.SetActiveScene(original);
            }
            var build=EditorBuildSettings.scenes.Where(x=>x.path!=original.path && x.path!=path).ToList();
            build.Insert(0,new EditorBuildSettingsScene(original.path,true)); build.Add(new EditorBuildSettingsScene(path,true));
            EditorBuildSettings.scenes=build.ToArray();
            var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();
            var passage=flow.door.GetComponent<DoorPassage>(); if(!passage) passage=flow.door.gameObject.AddComponent<DoorPassage>();
            passage.flow=flow; passage.effects=flow.effects;
            EditorUtility.SetDirty(passage); EditorSceneManager.MarkSceneDirty(original); EditorSceneManager.SaveScene(original);
            AssetDatabase.SaveAssets();
        }
    }
}
