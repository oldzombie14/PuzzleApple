using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V4.Editor
{
    // Records actual first-step controller motion and the P4 world camera. Never creates input devices.
    public static class MoveMemoryRecorder
    {
        public static string Result {get;private set;}
        public static void Run()
        {
            if(!Application.isPlaying)throw new System.InvalidOperationException("Fresh P4 Play required.");
            Result="Recording";
            var flow=Object.FindFirstObjectByType<OpeningFlow>();flow.StartCoroutine(Record(flow));
        }
        static IEnumerator Record(OpeningFlow flow)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            Object.FindFirstObjectByType<WorldEntry>().EnterWorld();
            yield return new WaitForSecondsRealtime(1.5f);
            flow.board.Panel.SetOpen(false);
            var player=flow.player;player.enabled=false;flow.enabled=false;
            var camera=player.view.GetComponent<Camera>();
            var advance=typeof(FirstPersonController).GetMethod("AdvanceMovement",BindingFlags.Instance|BindingFlags.NonPublic);
            const int width=320,height=180,columns=8,rows=8,frames=64;
            var atlas=new Texture2D(width*columns,height*rows,TextureFormat.RGB24,false);
            var frame=new Texture2D(width,height,TextureFormat.RGB24,false);
            var target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
            int recorded=0;
            try
            {
                for(int i=0;i<frames;i++)
                {
                    advance.Invoke(player,new object[]{Vector2.up,true,1f/24});
                    yield return new WaitForEndOfFrame();
                    Graphics.Blit(camera.targetTexture,target);
                    var previous=RenderTexture.active;
                    RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,width,height),0,0);frame.Apply();RenderTexture.active=previous;
                    atlas.SetPixels(i%columns*width,(rows-1-i/columns)*height,width,height,frame.GetPixels());
                    recorded=i+1;
                    if(player.WalkedDistance>=player.learnMoveDistance)break;
                    yield return new WaitForSecondsRealtime(1f/12);
                }
                atlas.Apply();
                const string path="Assets/Textures/V4/MoveMemory.png";
                System.IO.File.WriteAllBytes(path,atlas.EncodeToPNG());AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=false;importer.maxTextureSize=4096;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
                var clip=IntroPresentationSetup.Sequence();clip.atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                clip.columns=columns;clip.rows=rows;clip.frames=recorded;clip.framesPerSecond=12;clip.aspect=(float)width/height;
                EditorUtility.SetDirty(clip);AssetDatabase.SaveAssets();
                Result="PASS: recorded actual first-person learning walk, "+player.WalkedDistance.ToString("F2")+" m";
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);Object.Destroy(atlas);Object.Destroy(frame);
                player.enabled=true;flow.enabled=true;flow.board.Panel.SetOpen(true);Application.runInBackground=background;
            }
        }
    }
}
