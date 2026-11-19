using System;
using System.Collections;
using UnityEngine;
using Wait=PuzzleApple.V5.Editor.ChargeDoorChecks;

namespace PuzzleApple.V5.Editor
{
    public static class LightingTransitionChecks
    {
        public static string Result {get;private set;}
        public static float MaximumFrameDelta {get;private set;}
        public static float threshold=.15f;
        static int samples;
        public static void Run(){Result="Running";MaximumFrameDelta=0;samples=0;var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Intro(e));}
        public static void RunChapter(){Result="Running";MaximumFrameDelta=0;samples=0;var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Chapter(e));}
        public static void RunDeparture(){Result="Running";MaximumFrameDelta=0;samples=0;var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Departure(e));}
        static IEnumerator Departure(WorldEntry e){
            yield return Wait.OpenEnding(e);
            var a=e.journey.Ascent;var p=e.player;e.board.Panel.SetOpen(false);
            p.Teleport(a.Landing.position+Vector3.up*.36f-a.transform.forward*.75f,a.transform.rotation);yield return null;
            p.view.LookAt(a.Symbol.position);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;yield return null;
            var frame=new RenderTexture(Screen.width,Screen.height,0);var small=new RenderTexture(32,18,0);
            var pixels=new Texture2D(32,18,TextureFormat.RGB24,false,true);frame.Create();small.Create();
            float previous=-1,baseline=-1,minimum=1,end=Time.realtimeSinceStartup+35;bool clicked=false;
            try{
                do{
                    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshotIntoRenderTexture(frame);Graphics.Blit(frame,small);
                    var active=RenderTexture.active;RenderTexture.active=small;pixels.ReadPixels(new Rect(0,0,32,18),0,0);RenderTexture.active=active;
                    float value=0;foreach(var color in pixels.GetPixels())value+=color.grayscale;value/=32*18;
                    if(baseline<0)baseline=value;
                    if(!e.outdoor.IsOutside)minimum=Mathf.Min(minimum,value);
                    if(previous>=0)MaximumFrameDelta=Mathf.Max(MaximumFrameDelta,Mathf.Abs(value-previous));
                    previous=value;samples++;
                    if(!clicked){e.chamberLight.AdvancePointer(true,true,false,.02f);e.chamberLight.AdvancePointer(false,false,true,.03f);clicked=true;}
                }while((a.Playing||!e.outdoor.IsOutside)&&Time.realtimeSinceStartup<end);
                if(!a.Completed||a.Playing||MaximumFrameDelta>threshold||minimum<baseline-.1f){Result="FAIL: altar departure delta "+MaximumFrameDelta+", baseline/min "+baseline+"/"+minimum;throw new Exception(Result);}
                Result="PASS: actual altar click, no preliminary dark dip and white-flash/outdoor transition; "+samples+" frames, max delta "+MaximumFrameDelta.ToString("F3");
            }finally{frame.Release();small.Release();UnityEngine.Object.Destroy(frame);UnityEngine.Object.Destroy(small);UnityEngine.Object.Destroy(pixels);}
        }
        static IEnumerator Chapter(WorldEntry e){
            yield return VitruvianChecks.Gallery(e);
            var pass=e.outdoor;var c=pass.Chapter;
            VitruvianChecks.Say(e.board,"positive","world");
            yield return Wait.Wait(()=>c.box.Opened && !e.Busy,"gallery opens");
            var basis=new GameObject("Lighting route basis").transform;
            foreach(var room in new[]{pass.Garden,pass.Vitruvian}){
                Vector3 gate=room==pass.Garden?c.gardenThreshold.position:room.transform.position;
                basis.SetPositionAndRotation(new Vector3(gate.x,room.floor.bounds.max.y+.1f,gate.z),Quaternion.Euler(0,180,0));
                yield return Traverse(e,basis,new Vector3(0,0,-3),new Vector3(0,0,3),"gallery / "+room.name);
            }
            UnityEngine.Object.Destroy(basis.gameObject);e.player.MovementLocked=false;
            // Include the actual fade canvas when sampling the chapter portal in both directions.
            var mirror=c.mirror.bounds.center;mirror.y=c.floor.bounds.max.y+.1f;
            yield return Portal(e,mirror-Vector3.right*1.3f,Quaternion.LookRotation(Vector3.right),Vector3.right*1.5f,()=>pass.IsOutside&&!pass.Transitioning,"Chapter to outdoor");
            var frame=pass.World.chapterFrame;
            yield return Portal(e,frame.TransformPoint(new Vector3(0,.24f,-.65f)),frame.rotation,frame.forward*1.3f,()=>pass.InChapter&&!pass.Transitioning,"outdoor to Chapter");
            Result="PASS: both side-room thresholds and chapter portals, bidirectional with return views; "+samples+" frames, max delta "+MaximumFrameDelta.ToString("F3");
        }
        static IEnumerator Portal(WorldEntry e,Vector3 start,Quaternion facing,Vector3 travel,Func<bool> arrived,string label){
            var p=e.player;p.Teleport(start,facing);p.MovementLocked=true;
            yield return new WaitForSecondsRealtime(1);
            var frame=new RenderTexture(Screen.width,Screen.height,0);
            var small=new RenderTexture(32,18,0);var pixels=new Texture2D(32,18,TextureFormat.RGB24,false,true);
            frame.Create();small.Create();float previous=-1,end=Time.realtimeSinceStartup+45;bool moved=false;
            try{
                do{
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(frame);Graphics.Blit(frame,small);
                    var active=RenderTexture.active;RenderTexture.active=small;pixels.ReadPixels(new Rect(0,0,32,18),0,0);RenderTexture.active=active;
                    float value=0;foreach(var color in pixels.GetPixels())value+=color.grayscale;value/=32*18;
                    if(previous>=0){var delta=Mathf.Abs(value-previous);MaximumFrameDelta=Mathf.Max(MaximumFrameDelta,delta);
                        if(delta>threshold){Result="FAIL: "+label+" frame delta "+delta.ToString("F3");throw new Exception(Result);}}
                    previous=value;samples++;
                    if(!moved){p.GetComponent<CharacterController>().Move(travel);moved=true;}
                }while((!arrived() || samples<3) && Time.realtimeSinceStartup<end);
                if(!arrived()){Result="FAIL: "+label+" did not cross";throw new Exception(Result);}
            }finally{frame.Release();small.Release();UnityEngine.Object.Destroy(frame);UnityEngine.Object.Destroy(small);UnityEngine.Object.Destroy(pixels);p.MovementLocked=false;}
        }
        static IEnumerator Traverse(WorldEntry e,Transform basis,Vector3 from,Vector3 to,string label){
            var p=e.player;var camera=p.view.GetComponent<Camera>();
            for(int backwards=0;backwards<2;backwards++){
                Vector3 start=backwards==0?from:to,end=backwards==0?to:from;
                float distance=Vector3.Distance(start,end),travel=0;
                p.Teleport(basis.TransformPoint(start),basis.rotation*Quaternion.Euler(0,backwards*180,0));
                p.MovementLocked=true;yield return new WaitForSecondsRealtime(1);
                float previous=-1;
                while(travel<distance){
                    travel=Mathf.Min(distance,travel+3.5f*Mathf.Min(Time.unscaledDeltaTime,.05f));
                    p.transform.position=basis.TransformPoint(Vector3.Lerp(start,end,travel/distance));
                    // Smoothly look back and then forward while crossing, in each direction.
                    p.view.localRotation=Quaternion.Euler(0,180*Mathf.Sin(Mathf.PI*travel/distance),0);
                    yield return new WaitForEndOfFrame();
                    float value=SentenceRuntimeChecks.FrameLuminance(camera);
                    if(previous>=0){
                        float delta=Mathf.Abs(value-previous);MaximumFrameDelta=Mathf.Max(MaximumFrameDelta,delta);
                        if(delta>threshold){Result="FAIL: "+label+" frame delta "+delta.ToString("F3")+" at "+travel.ToString("F2");throw new Exception(Result);}
                    }
                    previous=value;samples++;
                }
            }
        }
        static IEnumerator Intro(WorldEntry e){
            yield return Wait.OpenEnding(e);
            var j=e.journey;var p=e.player;var a=j.Ascent;
            p.MovementLocked=true;
            // Sample both sides while the new physical entrance opens and closes.
            foreach(int side in new[]{-1,1}){
                p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart+side*4)),j.Root.rotation*Quaternion.Euler(0,side>0?180:0,0));
                yield return new WaitForSecondsRealtime(1);
                foreach(bool open in new[]{true,false}){
                    j.EntranceDoor.SetOpen(open);float previous=-1;
                    do{
                        yield return new WaitForEndOfFrame();float value=SentenceRuntimeChecks.FrameLuminance(p.view.GetComponent<Camera>());
                        if(previous>=0){float delta=Mathf.Abs(value-previous);MaximumFrameDelta=Mathf.Max(MaximumFrameDelta,delta);
                            if(delta>threshold){Result="FAIL: room entrance swing frame delta "+delta;throw new Exception(Result);}}
                        previous=value;samples++;
                    }while(j.EntranceDoor.Driving);
                }
            }
            j.EntranceDoor.SetOpen(true);yield return Wait.Wait(()=>j.EntranceDoor.Progress==1,"entrance ready for bidirectional crossing");
            j.SetDoor(true);yield return Wait.Wait(()=>j.DoorProgress==1,"open white door");
            j.SetLights(false,PassageJourney.LightSpace.Chamber);
            yield return Wait.Wait(()=>e.chamberLight.Brightness==0,"reveal playable chamber entrance");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.Tutorial.ExitZ-5)),j.Root.rotation);yield return null;
            j.Tutorial.SetOn(true);
            var motion=VitruvianChecks.Say(e.board,"light","move");
            yield return Wait.Wait(()=>!j.Tutorial.ExitBlocked,"sphere clears the second corridor");
            // Leave a capsule-width margin before pausing at the newly cleared exit.
            yield return new WaitForSeconds(2);
            e.board.State.ReturnToLibrary(motion.Id,0,true);
            foreach(float z in new[]{0,j.pathLength,j.Tutorial.ExitZ,j.RoomStart})
                yield return Traverse(e,j.Root,new Vector3(0,.1f,z-2),new Vector3(0,.1f,z+2),"Intro threshold "+z);
            // The tutorial motion test released the docked lamps. Restore the actual walkable route.
            p.Teleport(j.EndingDoor.RoomLanding,j.EndingDoor.transform.rotation);yield return null;
            var gather=VitruvianChecks.Say(e.board,"light","move","door");
            yield return Wait.Wait(()=>e.board.State.Find(gather.Id)==null && j.EndingDoor.Charged,"recharge before sampling the stair doorway");
            j.EndingDoor.SetOpen(true);yield return Wait.Wait(()=>j.EndingDoor.Lift==1,"open the real stair route");
            yield return Traverse(e,a.transform,new Vector3(0,.1f,-2),new Vector3(0,1.8f,5),"charge door / stair");
            yield return Traverse(e,a.transform,new Vector3(0,a.totalRise*.3f+.1f,2+(a.TopZ-2)*.3f),new Vector3(0,a.totalRise*.7f+.1f,2+(a.TopZ-2)*.7f),"stair black-white blend");
            var folder=UnityEditor.SessionState.GetString("C2C.captureFolder","");
            if(!string.IsNullOrEmpty(folder)){
                p.Teleport(a.transform.TransformPoint(new Vector3(0,1,4)),a.transform.rotation);yield return new WaitForEndOfFrame();
                VitruvianChecks.Capture(p,p.view.position,a.transform.TransformPoint(new Vector3(0,4,12)),"intro-star-glow");
            }
            p.MovementLocked=false;
            Result="PASS: bidirectional Intro thresholds, return views and stair blend; "+samples+" rendered frames, max delta "+MaximumFrameDelta.ToString("F3")+" / "+threshold;
        }
    }
}
