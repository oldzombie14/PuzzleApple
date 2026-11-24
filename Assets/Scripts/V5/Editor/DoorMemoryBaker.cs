using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple.V5.Editor
{
    public static class DoorMemoryBaker
    {
        public const string OutputPath="Assets/Textures/V5/DoorMemory.png";

        public static void Bake()
        {
            var entry=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            if(!Application.isPlaying || !entry || !entry.journey.Door || entry.journey.DoorProgress!=0)
                throw new InvalidOperationException("Capture the closed white door in a running P5 scene.");
            var journey=entry.journey;
            var source=entry.player.view.GetComponent<Camera>();
            var go=new GameObject("Door memory capture",typeof(Camera));
            var camera=go.GetComponent<Camera>();camera.CopyFrom(source);
            var data=go.AddComponent<UniversalAdditionalCameraData>();
            var original=source.GetComponent<UniversalAdditionalCameraData>();
            if(original){data.renderPostProcessing=original.renderPostProcessing;data.volumeLayerMask=original.volumeLayerMask;}
            camera.orthographic=true;camera.orthographicSize=3.2f;camera.aspect=2f/3;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.cullingMask=1<<journey.Door.gameObject.layer;
            camera.nearClipPlane=.1f;camera.farClipPlane=8;
            camera.transform.SetPositionAndRotation(journey.Door.GetComponent<Renderer>().bounds.center-
                journey.Root.forward*6-journey.Root.right*.06f,journey.Root.rotation);
            var frame=RenderTexture.GetTemporary(512,768,24,RenderTextureFormat.DefaultHDR);
            var active=RenderTexture.active;
            var image=new Texture2D(512,768,TextureFormat.RGB24,false);
            try{
                // Render the actual mesh, submesh materials and runtime property blocks together.
                camera.targetTexture=frame;camera.Render();RenderTexture.active=frame;
                image.ReadPixels(new Rect(0,0,512,768),0,0);image.Apply();
                System.IO.File.WriteAllBytes(OutputPath,image.EncodeToPNG());
            }finally{
                RenderTexture.active=active;camera.targetTexture=null;
                RenderTexture.ReleaseTemporary(frame);
                UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.ImportAsset(OutputPath,ImportAssetOptions.ForceUpdate);
        }
    }
}
