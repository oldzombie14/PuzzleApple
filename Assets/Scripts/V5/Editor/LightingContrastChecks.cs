using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PuzzleApple.V5.Editor
{
    public static class LightingContrastChecks
    {
        public static string Result {get;private set;}
        static void Check(bool value,string message){if(!value){Result="FAIL: "+message;throw new Exception(Result);}}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Run(e));}
        static IEnumerator Until(Func<bool> predicate)
        {float end=Time.realtimeSinceStartup+20;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),"timeout");}
        // Exercise the actual UI shader on the GPU. Pixel readback stays in memory.
        static float Ink(Material source,Texture background)
        {
            var material=new Material(source);var frame=RenderTexture.GetTemporary(32,32,0,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(32,32,TextureFormat.RGBA32,false,true);var previous=RenderTexture.active;
            try
            {
                material.SetTexture("_ReticleSceneColor",background);material.SetVector("_ReticleViewportSize",new Vector4(32,32,0,0));
                Graphics.Blit(Texture2D.whiteTexture,frame,material);
                RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();
                return pixels.GetPixel(16,16).r;
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(frame);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(material);}
        }
        static void Contrast(Material ink)
        {Check(Ink(ink,Texture2D.whiteTexture)<.05f,"white background produces dark ink");Check(Ink(ink,Texture2D.blackTexture)>.95f,"black background produces white ink");}
        static float BloomAt(Transform view)
        {
            var manager=VolumeManager.instance;var stack=manager.CreateStack();
            try{manager.Update(stack,view,~0);return stack.GetComponent<Bloom>().intensity.value;}
            finally{manager.DestroyStack(stack);}
        }
        static IEnumerator Run(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                var rig=UnityEngine.Object.FindFirstObjectByType<RoomLighting>();var l=e.chamberLight;
                e.EnterWorld();yield return Until(()=>e.Entered);yield return new WaitForEndOfFrame();
                Check(!rig.interiorFill.enabled&&RenderSettings.ambientMode==AmbientMode.Flat,"closed room uses uniform ambient without point hotspot");
                Check(Mathf.Abs(RenderSettings.ambientLight.r-.006f)<.0001f&&RenderSettings.ambientLight.r==RenderSettings.ambientLight.g&&RenderSettings.ambientLight.g==RenderSettings.ambientLight.b,"dim neutral fill applied at runtime");
                e.board.State.TryAcquireSentence("contrast.world","positive world");yield return Until(()=>e.room.Opened&&!e.Busy);
                e.board.Panel.SetOpen(false);yield return new WaitForEndOfFrame();
                Check(e.player.view.GetComponent<Camera>().targetTexture.format!=RenderTextureFormat.ARGB32,"world image retains HDR for lamp Bloom");
                Check(rig.chamberBloom.weight==1&&BloomAt(e.player.view)>.1f,"chamber uses independent soft Bloom");
                Check(Mathf.Abs(RenderSettings.ambientLight.r-(rig.openAmbient.r+rig.unfoldedAmbientLift))<.001f,"outer ambient lift balances reduced spotlight hotspot");
                e.player.Teleport(new Vector3(0,.08f,0),Quaternion.identity);e.player.view.LookAt(l.transform);e.player.AdoptCurrentView();yield return null;
                Check(l.TryCollect(),"first collection starts");yield return new WaitForSecondsRealtime(.25f);
                var contrast=l.GetComponent<ReticleBackground>().Contrast;
                Check(l.collectionSymbol.gameObject.activeSelf&&l.collectionSymbol.material==contrast,"first feedback uses live scene contrast");Contrast(l.collectionSymbol.material);
                yield return Until(()=>!l.Collecting);l.ShowKnownVocabulary("light");yield return new WaitForSecondsRealtime(.15f);
                Image known=null;foreach(var im in l.hud.GetComponentsInChildren<Image>(true))if(im.name=="Known word feedback")known=im;
                Check(known&&known.gameObject.activeSelf&&known.material==contrast,"known feedback keeps same contrast");Contrast(known.material);
                var j=e.journey;e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.pathLength+2)),j.Root.rotation);yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                Check(j.InSecondRoom&&rig.chamberBloom.weight==0&&BloomAt(e.player.view)>1f,"second-room soft Bloom restored");
                Result="PASS: neutral closed fill, HDR world image, independent soft chamber/second-room Bloom, balanced outer ambient, first/known live materials and GPU contrast on light/dark backgrounds";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
