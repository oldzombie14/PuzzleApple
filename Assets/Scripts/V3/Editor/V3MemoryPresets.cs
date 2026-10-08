using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleApple.V3.Editor
{
    public static class V3MemoryPresets
    {
        public const string Folder="Assets/Textures/V3/Archive/Vocabulary";
        [MenuItem("PuzzleApple/V3/Bake preset memories")]
        public static void Bake()
        {
            var w=UnityEngine.Object.FindFirstObjectByType<V3World>();
            if(EditorApplication.isPlaying||!w||w.gameObject.scene.path!="Assets/Scenes/V3/P3.unity")
                throw new InvalidOperationException("Open P3 in Edit mode before baking memories.");
            Directory.CreateDirectory(Folder);
            var go=new GameObject("Memory capture (temporary)");
            var camera=go.AddComponent<Camera>();camera.CopyFrom(w.presentation.view);camera.enabled=false;
            camera.aspect=.75f;camera.nearClipPlane=.03f;camera.fieldOfView=52;
            var target=new RenderTexture(600,800,24);target.Create();camera.targetTexture=target;
            var oldActive=RenderTexture.active;
            var oldLamp=w.indicator.sharedMaterial;
            var lamp=new Material(oldLamp);w.indicator.sharedMaterial=lamp;
            var presets=new List<WordLibrary.VocabularyImage>();
            try
            {
                Capture(camera,"mirror",new Vector3(9,1.3f,.9f),w.originalMirror.Bounds.center,presets);
                Capture(camera,"move",new Vector3(8,1.3f,0),new Vector3(4,0,0),presets);
                Capture(camera,"door",new Vector3(7,1.85f,0),new Vector3(2,1.85f,0),presets);
                foreach(bool lit in new[]{true,false})
                {
                    lamp.EnableKeyword("_EMISSION");lamp.SetColor("_BaseColor",lit?Color.white:new Color(.015f,.015f,.015f));
                    lamp.SetColor("_EmissionColor",lit?Color.white*.8f:Color.black);
                    Capture(camera,lit?"positive":"negative",new Vector3(3.1f,3.7f,.2f),w.indicator.bounds.center,presets);
                }
                Capture(camera,"equal",new Vector3(-4.4f,2.8f,2.4f),new Vector3(-8,1.95f,0),presets);
                Capture(camera,"apple",new Vector3(-5.1f,1.1f,.15f),w.originalApple.Bounds.center,presets);
                AssetDatabase.Refresh();
                for(int i=0;i<presets.Count;i++)
                {
                    var p=presets[i];string path=Folder+"/"+p.wordId+".png";
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                    importer.npotScale=TextureImporterNPOTScale.None;
                    importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
                    p.image=AssetDatabase.LoadAssetAtPath<Texture2D>(path);presets[i]=p;
                }
                Undo.RecordObject(w.library,"Assign preset memories");w.library.vocabularyImages=presets.ToArray();
                EditorUtility.SetDirty(w.library);EditorSceneManager.MarkSceneDirty(w.gameObject.scene);
            }
            finally
            {
                w.indicator.sharedMaterial=oldLamp;UnityEngine.Object.DestroyImmediate(lamp);
                camera.targetTexture=null;RenderTexture.active=oldActive;target.Release();
                UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);
            }
        }
        static void Capture(Camera camera,string id,Vector3 position,Vector3 focus,List<WordLibrary.VocabularyImage> presets)
        {
            camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(focus-position));
            camera.Render();RenderTexture.active=camera.targetTexture;
            var image=new Texture2D(600,800,TextureFormat.RGB24,false);
            try
            {
                image.ReadPixels(new Rect(0,0,600,800),0,0);image.Apply();
                File.WriteAllBytes(Folder+"/"+id+".png",image.EncodeToPNG());
                presets.Add(new WordLibrary.VocabularyImage{wordId=id});
            }
            finally{UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
