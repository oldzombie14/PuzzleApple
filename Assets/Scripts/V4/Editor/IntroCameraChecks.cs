using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V4.Editor
{
    public static class IntroCameraChecks
    {
        public static string Result {get;private set;}
        static int sequence;
        static void Require(bool ok,string message){if(!ok){Result="FAIL: "+message;throw new InvalidOperationException(Result);}}
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Fresh P4 Play required");
            Result="Running";var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();flow.StartCoroutine(Check(flow));
        }
        static IEnumerator Check(OpeningFlow flow)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var hints=flow.effects.inscriptions;string key=hints.progressKey;
            hints.progressKey="PuzzleApple.Tests.Camera."+Guid.NewGuid().ToString("N");
            try
            {
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();yield return new WaitForSecondsRealtime(1.5f);
                flow.player.enabled=false;flow.lightField.enabled=false;
                foreach(var word in flow.board.Catalog.Words)flow.board.State.TryAcquireWord("camera.learn."+word.Id,word);
                var p=flow.effects.presentation;var camera=flow.player.view.GetComponent<Camera>();
                Lamps(flow,1);
                yield return Shot(flow,"negative light","light-off-unrestricted",()=>
                {
                    Require(Mathf.Abs(Mathf.DeltaAngle(Yaw(flow.player.view.forward),180))<.1f,"light off preserves yaw");
                    Horizon(flow,camera);
                },false,180);
                yield return Shot(flow,"positive light","light-on-horizon",()=>Horizon(flow,camera));
                yield return Shot(flow,"light move","light-move-near-ground",()=>
                {
                    Horizon(flow,camera);
                    Require(Mathf.Abs(Yaw(flow.player.view.forward))<.1f&&!p.TrackingTarget,"near-ground light move preserves yaw without tracking");
                });
                Lamps(flow,16);
                yield return Shot(flow,"light move","light-move-high",()=>
                {
                    Require(Mathf.Abs(Yaw(flow.player.view.forward))<.1f,"light move preserves yaw");
                    var delta=p.FocusPoint-flow.player.view.position;
                    float expected=Mathf.Atan2(delta.y,Vector3.ProjectOnPlane(delta,Vector3.up).magnitude)*Mathf.Rad2Deg;
                    Require(Mathf.Abs(Elevation(flow.player.view.forward)-expected)<.2f&&!p.TrackingTarget,"light move pitches toward light group");
                });
                yield return Shot(flow,"ground move","ground-move",()=>
                {Require(Mathf.Abs(Yaw(flow.player.view.forward))<.1f,"ground move keeps yaw");Horizon(flow,camera);});
                flow.door.Reveal(flow.player.view);
                var doorPosition=flow.player.transform.position+new Vector3(10,8,25);
                flow.door.GetComponent<Rigidbody>().position=doorPosition;flow.door.transform.position=doorPosition;Physics.SyncTransforms();
                yield return Shot(flow,"door move","door-move",()=>
                {
                    var uv=camera.WorldToViewportPoint(flow.door.transform.position+Vector3.up*3);
                    Require(uv.z>0&&Mathf.Abs(uv.x-.5f)<.05f&&Mathf.Abs(uv.y-.5f)<.05f&&p.TrackingTarget,"door focused and follow enabled");
                },true);
                Lamps(flow,1);
                yield return Shot(flow,"light ground move","light-world-low",()=>
                    Require(Mathf.Abs(Yaw(flow.player.view.forward))<.1f&&p.TrackingTarget,"low lamps track in pitch"));
                Lamps(flow,16);
                yield return Shot(flow,"light ground move","light-world-high",()=>
                {
                    var uv=camera.WorldToViewportPoint(p.FocusPoint);
                    Require(uv.z>0&&Mathf.Abs(uv.x-.5f)<.04f&&Mathf.Abs(uv.y-1f/3)<.05f,"high lamp group framed at lower third: "+uv);
                });
                Lamps(flow,1);
                yield return Shot(flow,"light door move","light-door-behind",()=>
                {
                    var uv=camera.WorldToViewportPoint(flow.door.transform.position+Vector3.up*3);
                    Require(uv.z>0&&Mathf.Abs(uv.x-.5f)<.05f&&Mathf.Abs(uv.y-.5f)<.05f,"door behind player is brought into view");
                },false,180);
                Lamps(flow,3);
                yield return Shot(flow,"light light move","light-cluster",()=>
                {
                    var uv=camera.WorldToViewportPoint(p.FocusPoint);
                    Require(uv.z>0&&Mathf.Abs(uv.x-.5f)<.05f&&Mathf.Abs(uv.y-.5f)<.05f,"lamp cluster focused");
                });
                Result="PASS: light-group pitch, horizon lower-third, door focus/follow/snap, low/high lamp-ground framing, cluster focus, every sampled entry/follow/exit frame roll-free, final orientation retained and controller handoff";
            }
            finally{hints.Progress.Clear();hints.progressKey=key;Application.runInBackground=background;}
        }
        static void Lamps(OpeningFlow flow,float height)
        {
            var lamps=flow.effects.lights.Lamps.ToArray();var origin=flow.player.transform.position;
            for(int i=0;i<lamps.Length;i++)
            {
                var lamp=lamps[i];if(!lamp)continue;
                lamp.GetComponent<LightMotion>().SetMoving(false);lamp.gameObject.SetActive(i<24);
                if(i>=24)continue;
                var point=new Vector3(origin.x+(i%6-2.5f)*3,flow.lightField.ground.bounds.max.y+height,origin.z+14+(i/6)*4);
                lamp.GetComponent<Rigidbody>().position=point;lamp.transform.position=point;
            }
            Physics.SyncTransforms();
        }
        static IEnumerator Shot(OpeningFlow flow,string sentence,string image,Action verify,bool followCheck=false,float yaw=0)
        {
            var p=flow.effects.presentation;flow.board.State.ClearWorkspace();yield return new WaitForSecondsRealtime(.25f);
            flow.player.transform.rotation=Quaternion.Euler(0,yaw,0);flow.player.view.localRotation=Quaternion.identity;
            var saved=flow.player.view.localRotation;var eye=flow.player.view.localPosition;
            flow.board.State.TryAcquireSentence("camera.shot."+(++sequence),sentence);
            float deadline=Time.realtimeSinceStartup+10;
            while((!p.Playing||p.LetterboxProgress<.999f)&&Time.realtimeSinceStartup<deadline){if(p.Playing)Level(flow);yield return null;}
            Require(p.Playing&&p.LetterboxProgress>.99f,"shot entered: "+sentence);
            yield return new WaitForSecondsRealtime(.15f);verify();
            System.IO.Directory.CreateDirectory("Temp/V4Camera");ScreenCapture.CaptureScreenshot("Temp/V4Camera/"+image+".png");yield return new WaitForEndOfFrame();
            if(followCheck)
            {
                var rotation=flow.player.view.localRotation;var body=flow.door.GetComponent<Rigidbody>();
                body.position+=Vector3.right*3;flow.door.transform.position=body.position;Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(.4f);
                float turned=Quaternion.Angle(rotation,flow.player.view.localRotation);
                Require(turned>.1f&&turned<6,"door follows gently: "+turned);
            }
            deadline=Time.realtimeSinceStartup+10;
            var last=flow.player.view.rotation;
            while(p.Playing&&Time.realtimeSinceStartup<deadline)
            {
                Level(flow);
                if(p.LetterboxProgress<.999f)Require(Quaternion.Angle(last,flow.player.view.rotation)<.1f,"exit does not turn back: "+sentence);
                last=flow.player.view.rotation;yield return null;
            }
            Require(!p.Playing&&Quaternion.Angle(last,flow.player.view.rotation)<.1f&&Vector3.Distance(eye,flow.player.view.localPosition)<.001f,"shot retains final view: "+sentence);
            flow.board.Panel.SetOpen(false);
            typeof(FirstPersonController).GetMethod("AdvanceMovement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(flow.player,new object[]{Vector2.zero,true,0f});
            Require(Quaternion.Angle(last,flow.player.view.rotation)<.1f,"controller keeps cinematic heading: "+sentence);
        }
        static float Yaw(Vector3 forward)=>Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
        static void Level(OpeningFlow flow)=>Require(Mathf.Abs(Vector3.Dot(flow.player.view.right,Vector3.up))<.0001f,"cutscene never rolls");
        static void Horizon(OpeningFlow flow,Camera camera)
        {
            var horizontal=Vector3.ProjectOnPlane(flow.player.view.forward,Vector3.up).normalized;
            var uv=camera.WorldToViewportPoint(flow.player.view.position+horizontal*10000);
            Require(Mathf.Abs(uv.y-1f/3)<.005f,"horizon at lower third: "+uv.y);
        }
        static float Elevation(Vector3 forward)=>Mathf.Asin(forward.y)*Mathf.Rad2Deg;
    }
}
