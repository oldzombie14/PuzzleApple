using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V5.Editor
{
    public static class ReticleChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string message){if(!ok){Result="FAIL: "+message;throw new Exception(Result);}}
        static IEnumerator Until(Func<bool> condition)
        {float end=Time.realtimeSinceStartup+20;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(condition(),"timeout");}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Run(e));}
        static IEnumerator Run(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                var l=e.chamberLight;var p=e.player;
                Check(AssetDatabase.GetAssetPath(l.hoverEye.sprite)=="Assets/Sprites/V5/Viewer.png","actual eye binding");
                Check(System.IO.File.ReadAllBytes("Assets/Sprites/V5/Viewer.png").SequenceEqual(System.IO.File.ReadAllBytes("Assets/UI/V4/Icons/viewer.png")),"V4 eye pixels preserved");
                Check(l.hoverEye.rectTransform.sizeDelta==new Vector2(140,140)&&l.hoverEye.rectTransform.anchoredPosition==Vector2.zero,"eye layout");
                var strokes=l.reticle.GetComponentsInChildren<Image>(true);
                Check(strokes.Length==2&&strokes.All(s=>s.rectTransform.sizeDelta==new Vector2(20,3)),"native X strokes");
                var bg=l.GetComponent<ReticleBackground>();var camera=p.view.GetComponent<Camera>();
                Check(camera.targetTexture&&bg.Contrast.GetTexture("_ReticleSceneColor")==camera.targetTexture,"isolated world input for contrast");
                Check(!l.hoverEye.gameObject.activeSelf&&!l.reticle.activeSelf,"menu cursor hidden");
                e.EnterWorld();yield return Until(()=>e.Entered);
                e.board.State.TryAcquireSentence("cursor.world","positive world");yield return Until(()=>e.room.Opened&&!e.Busy);
                e.board.Panel.SetOpen(false);p.Teleport(new Vector3(4,.08f,0),Quaternion.identity);yield return null;
                Check(l.reticle.activeSelf&&!l.hoverEye.gameObject.activeSelf,"ordinary aim X");
                p.view.LookAt(l.transform);p.AdoptCurrentView();yield return null;
                Check(l.hoverEye.gameObject.activeSelf&&!l.reticle.activeSelf&&l.hoverEye.color.a==1,"unknown eye");
                l.SetOn(false);yield return Until(()=>l.Brightness==0);yield return null;
                Check(l.reticle.activeSelf&&!l.TryCollect(),"unlit target cannot collect");
                l.SetOn(true);yield return Until(()=>l.Brightness==1);yield return null;
                e.board.Panel.SetOpen(true);yield return null;
                Check(!l.reticle.activeSelf&&!l.hoverEye.gameObject.activeSelf,"panel hides aiming UI");
                e.board.Panel.SetOpen(false);yield return null;p.view.LookAt(l.transform);p.AdoptCurrentView();yield return null;
                Check(l.TryCollect()&&l.Collecting&&p.MovementLocked,"new collection locks movement");
                Check(!l.hoverEye.gameObject.activeSelf&&!l.reticle.activeSelf,"collection hides aim");
                yield return Until(()=>!l.Collecting);yield return null;
                Check(!p.MovementLocked&&Mathf.Abs(l.hoverEye.color.a-.35f)<.001f,"known eye and unlock");
                Check(l.TryCollect()&&!l.Collecting&&!p.MovementLocked&&!e.board.Panel.InputBlocked,"known feedback does not block movement or panel");
                yield return null;Check(!l.hoverEye.gameObject.activeSelf&&!l.reticle.activeSelf,"known feedback hides aim");
                Check(l.TryCollect(),"repeat click restarts known feedback");
                yield return Until(()=>!l.ShowingKnown&&l.hoverEye.gameObject.activeSelf);
                Check(l.hoverEye.gameObject.activeSelf&&!l.reticle.activeSelf,"known feedback restores eye");
                Result="PASS: V4 asset pixels, layout, world texture binding, menu/X/eye/dark/panel/new/known/repeated collection";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
