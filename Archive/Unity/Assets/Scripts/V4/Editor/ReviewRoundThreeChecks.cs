using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4.Editor
{
    public static class ReviewRoundThreeChecks
    {
        public static string Result {get;private set;}
        static int sequence;
        static void Require(bool ok,string message){if(!ok){Result="FAIL: "+message;throw new InvalidOperationException(Result);}}
        static void Sentence(CognitionState s,string text)=>s.TryAcquireSentence("round3."+(++sequence),text);
        public static void Run(){Result="Running";var f=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();f.StartCoroutine(Check(f));}
        static IEnumerator Check(OpeningFlow f)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();yield return new WaitForSecondsRealtime(2);
                f.player.enabled=false;f.board.Panel.SetOpen(false);f.effects.presentation.enabled=false;f.effects.inscriptions.enabled=false;
                var p=f.effects.presentation;var s=f.board.State;var d=f.door;
                foreach(var word in f.board.Catalog.Words)s.TryAcquireWord("round3.learn."+word.Id,word);
                var frame = d.GetComponentInChildren<WeaveFrameOpening>(true).GetComponent<MeshRenderer>();
                var finish = frame.sharedMaterial;
                d.Reveal(f.player.view);
                f.player.view.rotation=Quaternion.LookRotation(d.transform.TransformPoint(new Vector3(0,3,0))-f.player.view.position);
                yield return new WaitForSecondsRealtime(.7f);
                var assembly=d.GetComponent<DoorAssembly>();
                Require(d.Appearing && assembly.Progress > 0 && frame.sharedMaterial != finish &&
                    frame.sharedMaterial.shader == finish.shader && frame.sharedMaterial.GetFloat("_SolidProgress") < 1,
                    "gallery frame reveals with its own finish");
                System.IO.Directory.CreateDirectory("Temp/V4Review");
                ScreenCapture.CaptureScreenshot("Temp/V4Review/door-assembly.png");yield return new WaitForEndOfFrame();
                float assemblyDeadline=Time.realtimeSinceStartup+12;
                while(d.Appearing&&Time.realtimeSinceStartup<assemblyDeadline)yield return null;
                yield return null;
                Require(!d.Appearing && assembly.Progress == 1 && frame.sharedMaterial == finish,
                    "assembly finishes and restores the original gallery frame material");
                f.RememberDoor();f.library.Select("door");
                var photo=f.library.SnapshotImage.texture as Texture2D;
                Require(photo&&photo.width==640&&photo.height==640,"orthographic door memory captured");
                System.IO.File.WriteAllBytes("Temp/V4Review/door-memory.png",photo.EncodeToPNG());f.library.Show(false);
                // A fresh region during free motion starts newly seen lamps at ground level.
                f.lightField.enabled=false;f.player.Teleport(new Vector3(200,1,200),Quaternion.identity);
                f.lightField.SetMoving(true);f.lightField.RefreshField();Physics.SyncTransforms();
                var fresh=f.lightField.ActiveLamps.ToArray();
                Require(fresh.Length>0&&fresh.All(l=>Mathf.Abs(l.GetComponent<Collider>().bounds.min.y-f.lightField.ground.bounds.max.y-.025f)<.02f&&l.GetComponent<LightMotion>().Direction.y>0),"newly spawned moving lamps start just above ground and move upward");
                f.lightField.SetMoving(false);f.player.Teleport(new Vector3(0,1,0),Quaternion.identity);f.lightField.RefreshField();
                f.player.view.localRotation=Quaternion.identity;
                // Fully close the source, then insert movement before reopening it.
                Sentence(s,"negative light");yield return new WaitForSecondsRealtime(3.2f);Require(f.lamp.IsOff,"off setup");s.ClearWorkspace();
                f.board.Panel.SetOpen(true);p.enabled=true;Sentence(s,"light move");yield return new WaitForSecondsRealtime(.3f);
                int strong=p.feedback.StrongCount,shots=p.PlayCount;
                Sentence(s,"positive light");yield return new WaitForSecondsRealtime(.3f);
                Require(p.feedback.StrongCount==strong+1&&!p.Playing,"on sentence gets first strong feedback");
                float deadline=Time.realtimeSinceStartup+12;
                while(!p.CanInterrupt&&Time.realtimeSinceStartup<deadline)yield return null;
                Require(p.feedback.StrongCount==strong+2&&p.PlayCount==shots+1&&p.LastShotRule=="light.move","waiting move gets second strong feedback and owns the only shot");
                yield return new WaitForSecondsRealtime(1);
                Require(f.lamp.Brightness>0&&f.lightField.Moving,"on and movement effects both run");
                p.Cancel();p.enabled=false;s.ClearWorkspace();
                var pos=new Vector3(20,d.groundHeight,15);d.GetComponent<Rigidbody>().position=pos;d.transform.position=pos;Physics.SyncTransforms();
                for(int i=0;i<12;i++){d.SetMotion(false,false);d.SetMotion(true,false);Require(d.GetComponent<LightMotion>().Direction.y>0,"grounded door initial direction is never downward");}
                d.SetMotion(false,false);yield return new WaitForFixedUpdate();
                f.board.Panel.SetOpen(true);p.enabled=true;strong=p.feedback.StrongCount;int weak=p.feedback.WeakCount;shots=p.PlayCount;
                Sentence(s,"door ground move");yield return new WaitForSecondsRealtime(.35f);
                Require(p.feedback.StrongCount==strong&&p.feedback.WeakCount==weak+1&&p.PlayCount==shots,"grounded door descent gives weak-only feedback");
                p.enabled=false;s.ClearWorkspace();
                var env=f.effects.world.environment;var clouds=env.clouds;
                foreach(float elevation in new[]{15f,35f,65f})
                {
                    f.player.view.rotation=Quaternion.Euler(-elevation,20,0);
                    var ray=f.player.view.forward;float height=Mathf.Abs(clouds.cloudPlane.transform.position.y-f.player.view.position.y);
                    float cloudSpeed=Vector3.Cross(ray,clouds.DriftDirection*clouds.ActualDriftSpeed).magnitude/(height/Mathf.Abs(ray.y));
                    var axis=Vector3.Cross(Vector3.up,clouds.DriftDirection).normalized;
                    float starSpeed=env.MatchedStarSpeed()*Mathf.Deg2Rad*Vector3.Cross(axis,ray).magnitude;
                    Require(Mathf.Abs(cloudSpeed-starSpeed)<.0001f,"cloud/star apparent angular speeds match at "+elevation);
                }
                Result="PASS: woven assembly and material restoration, front orthographic memory, new moving lamps spawn at ground, on-to-motion feedback/camera handoff, grounded door upward start and weak descent feedback, matched sky speeds";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
