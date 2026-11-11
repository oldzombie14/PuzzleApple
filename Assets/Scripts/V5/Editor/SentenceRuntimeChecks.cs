using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    // Explicitly invoked in a fresh P5 Play session. Never opens/saves scenes or writes images.
    public static class SentenceRuntimeChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string message){if(!ok){Result="FAIL: "+message;throw new Exception(Result);}}
        static IEnumerator Until(Func<bool> predicate)
        {float end=Time.realtimeSinceStartup+25;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),"timeout");}
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Run only in a fresh P5 Play session.");
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            if(!e||e.Entered)throw new InvalidOperationException("A fresh P5 menu is required; no scene is reloaded by this test.");
            Result="Running";e.StartCoroutine(Exercise(e));
        }
        static IEnumerator Exercise(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var presentation=e.sentenceRuntime.presentation;bool shots=presentation.enabledShots;
            try
            {
                presentation.enabledShots=false;
                e.EnterWorld();yield return Until(()=>e.Entered);e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);
                var state=e.board.State;var j=e.journey;
                state.TryAcquireSentence("check.off","negative light");
                yield return new WaitForSeconds(.2f);
                float brightness=e.chamberLight.Brightness;
                Check(brightness>0&&brightness<1,"light starts gradual off");
                var off=state.Groups.First(g=>g.RuleId=="light.off");
                var word=state.Detach(off.Id,off.Words.Last().Id);
                yield return new WaitForSeconds(.2f);
                Check(Mathf.Abs(brightness-e.chamberLight.Brightness)<.0001f,"split holds partial brightness");
                state.Join(word.Id,off.Id,false);
                state.TryAcquireSentence("check.on","positive light");
                var on=state.Groups.First(g=>g.RuleId=="light.on");
                Check(off.Effective&&on.Conflicted,"opposite light state uses earlier sentence");
                state.ReturnToLibrary(off.Id,0,true);yield return Until(()=>e.chamberLight.Brightness>=1);
                Check(on.Effective,"removing off hands control to on");
                var doorPosition=j.Door.position;
                Check(j.Door.parent.Find("Round handle"),"hinge carries a round handle");
                Check(!j.Root.Cast<Transform>().Any(t=>t.name.Contains("reveal")),"no wide reveal-plane artifacts");
                state.TryAcquireSentence("check.door.on","positive door");yield return new WaitForSeconds(.25f);
                var doorOn=state.Groups.First(g=>g.RuleId=="door.on");
                float progress=j.DoorProgress;Check(progress>0&&progress<1,"door starts gradual opening");
                var token=state.Detach(doorOn.Id,doorOn.Words.Last().Id);yield return new WaitForSeconds(.2f);
                Check(Mathf.Abs(progress-j.DoorProgress)<.0001f,"split holds door angle");
                var surface=new MaterialPropertyBlock();j.Door.GetComponent<Renderer>().GetPropertyBlock(surface);
                Check(surface.GetFloat("_Dissolve")==0,"swinging leaf stays opaque");
                Check(Mathf.Abs(j.Door.position.y-doorPosition.y)<.001f,"swing stays level");
                state.Join(token.Id,doorOn.Id,false);yield return Until(()=>j.DoorProgress>=1);
                Check(j.Door.GetComponent<Renderer>().enabled&&j.Door.GetComponent<Collider>().enabled,"open leaf remains solid and visible");
                Check(Mathf.Abs(Vector3.Dot(j.Door.forward,j.Root.forward))<.001f,"fully open leaf is perpendicular to doorway");
                state.TryAcquireSentence("check.door.off","negative door");
                var doorOff=state.Groups.First(g=>g.RuleId=="door.off");Check(doorOff.Conflicted,"opposite door state conflict");
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.05f,j.pathLength)),j.Root.rotation);
                state.ReturnToLibrary(doorOn.Id,0,true);yield return new WaitForSeconds(1.5f);
                Check(j.DoorProgress==1,"closing waits while player occupies doorway");
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.05f,j.pathLength+4)),j.Root.rotation);yield return Until(()=>j.DoorProgress<=0);
                Check(j.DoorProgress==0,"off door closes after doorway clears");
                Check(j.Door.GetComponent<Renderer>().enabled&&j.Door.GetComponent<Collider>().enabled&&j.Door.position==doorPosition,"off restores stationary white leaf and collision");
                Check(!j.Root.GetComponentsInChildren<Renderer>().Any(r=>r.name.Contains("door")&&r.name.Contains("filaments")&&r.enabled),"closed room side hides all doorway outlines");
                Check(j.WordAt(j.Door.GetComponent<Collider>(),1)==null,"closed rear door does not reveal itself through collection hint");
                state.ClearWorkspace();j.SetLights(true);yield return Until(()=>j.Brightness>=1);
                yield return null;j.Door.GetComponent<Renderer>().GetPropertyBlock(surface);
                Check(surface.GetFloat("_DoorBackBrightness")==1,"closed door back follows lit room wall");
                presentation.enabledShots=true;
                state.TryAcquireSentence("check.shot.off","negative light");
                yield return Until(()=>presentation.Playing);
                Check(j.Brightness==1&&!presentation.CanInterrupt,"new effect held during feedback and black bars");
                yield return Until(()=>presentation.CanInterrupt);yield return new WaitForSeconds(.2f);
                Check(j.Brightness<1&&j.Brightness>0,"effect begins inside shot");
                Check(presentation.InterruptForEditing(),"Tab-equivalent interrupts shot");
                Check(!presentation.Playing&&e.board.Panel.IsOpen&&!e.player.CinematicLocked,"editing and view control restored");
                off=state.Groups.First(g=>g.RuleId=="light.off");state.Detach(off.Id,off.Words.Last().Id);
                brightness=j.Brightness;yield return new WaitForSeconds(.2f);
                Check(Mathf.Abs(brightness-j.Brightness)<.0001f,"split after shot interruption holds room and wall brightness");
                Check(Mathf.Abs(Vector3.Dot(e.player.view.right,Vector3.up))<.0001f,"camera roll stays zero");
                Result="PASS: light/door priority, partial transition retention, takeover, safe door closure, deferred effect commit, shot interruption and input restoration";
            }
            finally{presentation.enabledShots=shots;Application.runInBackground=background;}
        }
        public static void RunCameraContract()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play required.");
            Result="Running camera contract";
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(CameraContract(e));
        }
        static IEnumerator CameraContract(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var p=e.player;var presentation=e.sentenceRuntime.presentation;
            try
            {
                if(!e.Entered){e.EnterWorld();yield return Until(()=>e.Entered);e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);}
                e.board.Panel.SetOpen(false);
                for(int pass=0;pass<2;pass++)
                {
                    // Include follow as well as static focus, normal completion and interruption.
                    e.board.Panel.SetOpen(true);
                    string rule=pass==0?"light.floor.move":"light.on";
                    var position=p.transform.position;var camera=p.view.position;
                    var roll=p.view.eulerAngles.z;bool applied=false;
                    e.StartCoroutine(presentation.Play(rule,()=>applied=true));
                    float end=Time.realtimeSinceStartup+20;int frames=0;bool barsChecked=false;
                    while(presentation.Playing&&Time.realtimeSinceStartup<end)
                    {
                        Check(p.CinematicLocked,"player locked throughout shot");
                        Check(Vector3.Distance(position,p.transform.position)<.0001f,"player position fixed");
                        Check(Vector3.Distance(camera,p.view.position)<.0001f,"camera position fixed");
                        Check(Mathf.Abs(Mathf.DeltaAngle(roll,p.view.eulerAngles.z))<.001f,"roll unchanged");
                        if(presentation.CanInterrupt&&!barsChecked)
                        {
                            var canvas=presentation.transform.Find("V5 sentence letterbox").GetComponent<RectTransform>();
                            var parent=canvas.parent as RectTransform;
                            Check(parent&&Mathf.Abs(canvas.rect.width-parent.rect.width)<.1f&&Mathf.Abs(canvas.rect.height-parent.rect.height)<.1f,"letterbox canvas fills parent");
                            var top=canvas.Find("Top").GetComponent<RectTransform>();
                            Check(top.gameObject.activeSelf&&Mathf.Abs(top.rect.width-canvas.rect.width)<.1f&&top.rect.height>1,"black bar spans full width");
                            barsChecked=true;
                            if(pass==1)Check(presentation.InterruptForEditing(),"interrupt accepted");
                        }
                        frames++;yield return null;
                    }
                    Check(!presentation.Playing&&applied&&barsChecked&&frames>2,"shot completes with effect and full-width bars");
                    Check(!p.CinematicLocked,"movement released after shot");
                    Check(!presentation.transform.Find("V5 sentence letterbox/Top").gameObject.activeSelf,"bars hidden after shot");
                    if(pass==0)Check(!e.board.Panel.IsOpen&&!e.board.Panel.InputBlocked&&!p.PanelOpen,"normal completion returns to gameplay without reopening Tab");
                    if(pass==1)Check(e.board.Panel.IsOpen&&!e.board.Panel.InputBlocked,"interrupt restores editing");
                    yield return null;
                    if(pass==0)Check(Cursor.lockState==CursorLockMode.Locked&&!Cursor.visible&&p.CanInteractWithWorld,"gameplay aim resumes without clicking");
                    else Check(Cursor.lockState==CursorLockMode.None,"editing pointer is free");
                }
                Result="PASS: full-width bars, fixed player/camera positions and unchanged roll throughout static/follow shots, normal completion, interruption, movement and editor restoration";
            }
            finally{presentation.Cancel();Application.runInBackground=background;}
        }
        public static void RunSurfacesAndBox()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play required.");
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();Result="Running surfaces and box";e.StartCoroutine(SurfacesAndBox(e));
        }
        static IEnumerator SurfacesAndBox(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                if(!e.Entered){e.EnterWorld();yield return Until(()=>e.Entered);e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);}
                var j=e.journey;var f=j.floorSurface;var state=e.board.State;
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.pathLength+4)),j.Root.rotation);
                j.SetLights(true);yield return Until(()=>j.Brightness>=1);yield return null;
                var wall=j.Root.Find("Room side wall").GetComponent<Renderer>();
                Check(wall.sharedMaterial.shader==f.Surface.sharedMaterial.shader&&wall.sharedMaterial.GetFloat("_Brightness")==1,"wall uses lit white ripple surface");
                var lamp=j.Lamps[0];var body=lamp.GetComponent<Rigidbody>();
                body.position=j.Root.TransformPoint(new Vector3(j.roomSize.x*.5f-.6f,3,j.pathLength+6));lamp.direction=j.Root.right;Physics.SyncTransforms();
                int impacts=f.WallImpactCount;yield return Until(()=>f.WallImpactCount>impacts&&Vector3.Dot(lamp.direction,j.Root.right)<0);
                Check(f.WallImpactCount>impacts,"actual wall collision emits ripple");
                j.SetLights(false);yield return Until(()=>j.Brightness<=0);yield return null;
                Check(wall.sharedMaterial.GetFloat("_Brightness")==j.SurfaceBrightness,"wall retains dim fill");
                Check(!j.Root.Cast<Transform>().Any(t=>t.name.StartsWith("Door frame")),"fixed decorative frame absent");
                Check(j.Root.Cast<Transform>().Count(t=>t.name.Contains("opening")&&t.name.Contains("reveal"))==0,"no widened filament reveal planes");
                var path=j.Root.Find("White path");var floor=f.Surface.transform;
                Check(Mathf.Abs(path.localPosition.z+path.localScale.z*.5f-(floor.localPosition.z-floor.localScale.z*.5f))<.001f,"path and room floor meet without overlap");
                state.ClearWorkspace();state.TryAcquireSentence("check.box.off","negative world");
                yield return Until(()=>e.room.Progress<.8f);
                var close=state.Groups.First(g=>g.RuleId=="world.off");var part=state.Detach(close.Id,close.Words.Last().Id);
                float pose=e.room.Progress;yield return new WaitForSeconds(.25f);
                Check(Mathf.Abs(e.room.Progress-pose)<.0001f&&!e.Busy&&e.board.Panel.IsOpen&&!e.board.Panel.InputBlocked,"split pauses closing and allows editing");
                state.Join(part.Id,close.Id,false);yield return Until(()=>e.DialogReady);e.CancelExit();
                state.ClearWorkspace();state.TryAcquireSentence("check.box.on","positive world");yield return Until(()=>e.room.Progress>.15f);
                var open=state.Groups.First(g=>g.RuleId=="world.on");part=state.Detach(open.Id,open.Words.Last().Id);
                pose=e.room.Progress;yield return new WaitForSeconds(.25f);
                Check(Mathf.Abs(e.room.Progress-pose)<.0001f,"split pauses opening");
                state.Join(part.Id,open.Id,false);yield return Until(()=>e.room.Opened&&!e.Busy);
                Check(!e.player.MovementLocked&&!e.portableClosure.Isolated,"reopening releases player and restores room");
                Result="PASS: actual wall hit/reflection, lit/dark walls, no decorative frame, no wide reveal planes, no floor overlap, box close/open split and resume, exit cancellation and portable restoration";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
