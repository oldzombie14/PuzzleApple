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
            if(!e||e.room.Progress>0)throw new InvalidOperationException("Start in the closed P5 box.");
            Result="Running";e.StartCoroutine(Exercise(e));
        }
        public static void RunFeedback(){
            Result="Running";
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            e.StartCoroutine(CheckFeedback(e));
        }
        static IEnumerator CheckFeedback(WorldEntry e){
            e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);
            var j=e.journey;var state=e.board.State;
            j.SetDoor(true);yield return Until(()=>j.DoorProgress==1);yield return ChargeDoorChecks.UnlockEntrance(e);
            j.SetLights(false,PassageJourney.LightSpace.All);yield return Until(()=>!j.Lights(PassageJourney.LightSpace.All).Any(l=>l.WouldChange(false)));
            e.sentenceRuntime.presentation.enabledShots=false;
            e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.12f,j.RoomStart+5)),j.Root.rotation);
            yield return Until(()=>j.InSecondRoom);
            j.SetLights(false,PassageJourney.LightSpace.FarRoom);
            yield return Until(()=>j.Brightness==0);
            e.board.Panel.SetOpen(true);yield return new WaitForSecondsRealtime(.4f);
            bool Rippling()=>GameObject.Find("Sentence effect ripple") || GameObject.Find("Sentence grammar ripple");
            float fade=e.feedback.duration*1.2f+.3f;
            foreach(var words in new[]{"light move","light move door","light move light","light move floor","door move light"}){
                state.ClearWorkspace();yield return new WaitForSecondsRealtime(fade);
                int weak=e.feedback.WeakCount;
                state.TryAcquireSentence("feedback."+words,words,out var group);
                yield return null;yield return null;
                Check(e.feedback.WeakCount==weak+1,"one weak feedback on unavailable "+words);
                yield return new WaitForSecondsRealtime(fade);
                Check(!e.sentenceRuntime.SustainsFeedback(group) && !Rippling(),"unavailable "+words+" never loops");
            }
            state.ClearWorkspace();j.SetLights(true,PassageJourney.LightSpace.FarRoom);
            yield return Until(()=>j.Brightness==1);
            state.TryAcquireSentence("feedback.active","light move",out var moving);
            yield return new WaitForSecondsRealtime(fade);
            Check(e.sentenceRuntime.SustainsFeedback(moving) && GameObject.Find("Sentence effect ripple"),"actual local motion sustains feedback");
            j.SetLights(false,PassageJourney.LightSpace.FarRoom);yield return Until(()=>j.Brightness==0);
            yield return new WaitForSecondsRealtime(fade);
            Check(!e.sentenceRuntime.SustainsFeedback(moving) && !Rippling(),"darkening stops the sustained feedback");
            state.ClearWorkspace();j.SetLights(true,PassageJourney.LightSpace.FarRoom);
            yield return Until(()=>j.Brightness==1);
            state.TryAcquireSentence("feedback.command","light move door",out var command);
            yield return new WaitForSecondsRealtime(fade);
            Check(!e.sentenceRuntime.SustainsFeedback(command) && !Rippling(),"executing command has only its initial pulse");
            state.ClearWorkspace();
            j.SetLights(true,PassageJourney.LightSpace.ReturnPath);
            yield return Until(()=>j.ReturnStars.Brightness==1);
            state.TryAcquireSentence("feedback.multi","light move",out var multi);
            j.SetLights(false,PassageJourney.LightSpace.FarRoom);yield return Until(()=>j.Brightness==0);
            yield return new WaitForSecondsRealtime(fade);
            Check(j.ReturnStars.Brightness>0 && !e.sentenceRuntime.SustainsFeedback(multi) && !Rippling(),"lit distant stars do not sustain a dark room's feedback");
            Result="PASS: drafts, unavailable effects, actual sustained motion, darkness, one-shot commands and mixed-space feedback";
        }
        public static void RunDoorSwing()
        {
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result = "Running";
            entry.StartCoroutine(CheckDoorSwing(entry));
        }

        static IEnumerator CheckDoorSwing(WorldEntry entry)
        {
            entry.EnterWorld();
            yield return Until(() => entry.Entered);
            entry.OpenWorld(-1);
            yield return Until(() => entry.room.Opened && !entry.Busy);
            var journey = entry.journey;
            var state = entry.board.State;
            var player = entry.player;
            var presentation = entry.sentenceRuntime.presentation;
            bool shots = presentation.enabledShots;
            presentation.enabledShots = false;
            try
            {
                player.Teleport(journey.Root.TransformPoint(new Vector3(0,.05f,journey.pathLength-9)),journey.Root.rotation);
                yield return null;
                player.view.LookAt(journey.Door);
                player.AdoptCurrentView();
                Cursor.lockState = CursorLockMode.Locked;
                Check(entry.chamberLight.TryCollect(),"can collect from nine metres");
                yield return Until(() => !entry.chamberLight.Collecting);
                Check(state.Knows("door"),"collected door from nine metres");
                player.Teleport(journey.Root.TransformPoint(new Vector3(0,.05f,journey.pathLength-1)),journey.Root.rotation);
                state.TryAcquireSentence("check.swing.open","positive door");
                yield return Until(() => journey.DoorProgress > .2f);
                var motor = player.GetComponent<CharacterController>();
                foreach (var part in journey.Door.parent.GetComponentsInChildren<Collider>())
                    Check(Physics.GetIgnoreCollision(part,motor),"opening leaf and handle pass through player");
                yield return Until(() => journey.DoorProgress == 1);
                yield return null;
                Check(!Physics.GetIgnoreCollision(journey.Door.GetComponent<Collider>(),motor),"open door regains collision");
                yield return Until(() => !state.Groups.Any(g => g.RuleId == "door.on"));
                player.Teleport(journey.Root.TransformPoint(new Vector3(0,.05f,journey.pathLength-9)),journey.Root.rotation);
                state.TryAcquireSentence("check.swing.cycle","door move",out var cycle);
                yield return Until(() => journey.DoorProgress == 0);
                yield return Until(() => journey.DoorProgress > .3f);
                var detached = state.Detach(cycle.Id,cycle.Words.Last().Id);
                float held = journey.DoorProgress;
                yield return new WaitForSeconds(.25f);
                Check(held == journey.DoorProgress,"splitting door move holds angle");
                state.JoinAndSpeak(detached.Id,cycle.Id,false);
                yield return Until(() => journey.DoorProgress == 1);
                yield return Until(() => journey.DoorProgress < .8f);
                Result = "PASS";
            }
            finally { presentation.enabledShots = shots; }
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
                e.board.Panel.SetOpen(true);
                int strongBefore=e.feedback.StrongCount,weakBefore=e.feedback.WeakCount;
                Check(e.chamberLight.Brightness==1&&j.Brightness==0,"chamber lit and locked far room dark");
                state.TryAcquireSentence("check.already.on","positive light");
                yield return Until(()=>e.feedback.WeakCount>weakBefore);
                var redundant=state.Groups.First(g=>g.RuleId=="light.on");
                Check(e.feedback.StrongCount==strongBefore&&!e.sentenceRuntime.SustainsFeedback(redundant),"remote room change gives no local stars");
                yield return Until(()=>state.Find(redundant.Id)==null);
                state.TryAcquireSentence("check.off","negative light");
                yield return new WaitForSeconds(.2f);
                float brightness=e.chamberLight.Brightness;
                Check(brightness>0&&brightness<1,"light starts gradual off");
                var off=state.Groups.First(g=>g.RuleId=="light.off");
                Check(!e.sentenceRuntime.SustainsFeedback(off),"active light command plays its initial feedback only");
                var word=state.Detach(off.Id,off.Words.Last().Id);
                yield return new WaitForSeconds(.2f);
                Check(Mathf.Abs(brightness-e.chamberLight.Brightness)<.0001f,"split holds partial brightness");
                state.JoinAndSpeak(word.Id,off.Id,false);
                state.TryAcquireSentence("check.on","positive light");
                var on=state.Groups.First(g=>g.RuleId=="light.on");
                Check(off.Effective&&on.Conflicted,"opposite light state uses earlier sentence");
                e.board.Panel.SetOpen(true);presentation.enabledShots=true;
                int pulses=e.feedback.StrongCount,shotsBefore=presentation.PlayCount;
                float held=e.chamberLight.Brightness;
                state.ReturnToLibrary(off.Id,0,true);
                yield return Until(()=>presentation.Playing);
                Check(e.feedback.StrongCount==pulses+1&&presentation.LastRule=="light.on","newly effective ON receives strong ripple and its own shot");
                Check(e.chamberLight.Brightness==held&&!presentation.CanInterrupt,"old fade stops immediately before takeover shot");
                yield return Until(()=>!presentation.Playing);
                Check(presentation.PlayCount==shotsBefore+1&&e.chamberLight.Brightness==1&&j.Brightness==0,"takeover brightens chamber and leaves locked room unchanged");
                presentation.enabledShots=false;
                yield return Until(()=>state.Find(on.Id)==null);
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.pathLength-4)),j.Root.rotation);
                yield return Until(()=>j.InCorridor);
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
                state.JoinAndSpeak(token.Id,doorOn.Id,false);yield return Until(()=>j.DoorProgress>=1);
                Check(j.Door.GetComponent<Renderer>().enabled&&j.Door.GetComponent<Collider>().enabled,"open leaf remains solid and visible");
                Check(Mathf.Abs(Vector3.Angle(j.Root.forward,j.Door.forward)-j.openAngle)<.05f,"fully open leaf reaches its configured swing angle");
                yield return Until(()=>state.Find(doorOn.Id)==null);
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.05f,j.pathLength)),j.Root.rotation);
                state.TryAcquireSentence("check.door.off","negative door");
                yield return new WaitForSeconds(1.5f);
                Check(j.DoorProgress==1,"closing waits while player occupies doorway");
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.05f,j.RoomStart+4)),j.Root.rotation);yield return Until(()=>j.DoorProgress<=0);
                Check(j.DoorProgress==0,"off door closes after doorway clears");
                Check(j.Door.GetComponent<Renderer>().enabled&&j.Door.GetComponent<Collider>().enabled&&j.Door.position==doorPosition,"off restores stationary white leaf and collision");
                Check(!j.Root.GetComponentsInChildren<Renderer>().Any(r=>r.name.Contains("door")&&r.name.Contains("filaments")&&r.transform.localPosition.z>j.pathLength&&r.enabled),"closed door conceals the room-side outline");
                Check(j.WordAt(j.Door.GetComponent<Collider>(),1)==null,"closed rear door does not reveal itself through collection hint");
                yield return ChargeDoorChecks.UnlockEntrance(e);state.ClearWorkspace();j.SetLights(true);yield return Until(()=>j.Brightness>=1);
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
        public static void RunCorridorLights()
        {
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running corridor lights";
            e.StartCoroutine(CorridorLights(e));
        }
        internal static float FrameLuminance(Camera camera)
        {
            var small=RenderTexture.GetTemporary(32,18,0,RenderTextureFormat.ARGB32);
            var old=RenderTexture.active;
            var pixels=new Texture2D(32,18,TextureFormat.RGB24,false,true);
            try
            {
                Graphics.Blit(camera.targetTexture,small);
                RenderTexture.active=small;
                pixels.ReadPixels(new Rect(0,0,32,18),0,0);
                return pixels.GetPixels().Average(c=>c.grayscale);
            }
            finally
            {
                RenderTexture.active=old;
                RenderTexture.ReleaseTemporary(small);
                UnityEngine.Object.Destroy(pixels);
            }
        }
        static IEnumerator CorridorLights(WorldEntry e)
        {
            var j=e.journey;var show=e.sentenceRuntime.presentation;var p=e.player;
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);
                j.SetLights(true,PassageJourney.LightSpace.All);yield return Until(()=>j.Brightness==1);
                var camera=p.view.GetComponent<Camera>();
                for(int pass=0;pass<3;pass++)
                {
                    bool on=pass>0;
                    if(on){j.SetDoor(true);yield return Until(()=>j.DoorProgress==1);yield return ChargeDoorChecks.UnlockEntrance(e);}
                    if(pass==2)
                    {
                        j.SetLights(false,PassageJourney.LightSpace.All);
                        yield return Until(()=>e.chamberLight.Brightness==0&&j.Brightness==0);
                    }
                    var position=j.Root.TransformPoint(new Vector3(0,.05f,on?j.pathLength-4:4));
                    p.Teleport(position,j.Root.rotation);
                    e.board.Panel.SetOpen(true);
                    yield return Until(()=>j.InCorridor);
                    var local=p.view.localPosition;
                    var cameraPosition=p.view.position;
                    Quaternion? fixedRotation=on?(Quaternion?)null:p.view.rotation;
                    e.board.State.TryAcquireSentence("corridor."+pass,on?"positive light":"negative light");
                    yield return FixedCameraUntil(()=>show.CanInterrupt,p,cameraPosition,fixedRotation);
                    yield return new WaitForEndOfFrame();
                    float before=FrameLuminance(camera);
                    yield return FixedCameraUntil(()=>j.Stars.Brightness==(on?1:0),p,cameraPosition,fixedRotation);
                    yield return new WaitForEndOfFrame();
                    float after=FrameLuminance(camera);
                    Check(show.Playing&&(on?after-before:before-after)>.01f,"visible light change in held shot: "+before+" to "+after);
                    if(pass==2)Check(show.InterruptForEditing(),"interrupt stationary corridor shot");
                    yield return FixedCameraUntil(()=>!show.Playing,p,cameraPosition,fixedRotation);
                    Check((p.view.localPosition-local).sqrMagnitude<.0001f&&j.InCorridor,"camera returned to corridor");
                    e.board.State.ClearWorkspace();
                }
                j.SetDoor(true);yield return Until(()=>j.DoorProgress==1);yield return ChargeDoorChecks.UnlockEntrance(e);
                yield return new WaitForEndOfFrame();
                j.SetLights(true,PassageJourney.LightSpace.FarRoom);yield return Until(()=>j.Brightness==1);
                float outside=RenderSettings.ambientLight.r;
                p.Teleport(j.Root.TransformPoint(new Vector3(0,.05f,j.RoomStart+3)),j.Root.rotation);
                yield return Until(()=>j.InSecondRoom);yield return new WaitForEndOfFrame();
                float inside=RenderSettings.ambientLight.r;
                Check(outside>=j.Tutorial.Fill.Fill*.85f && Mathf.Abs(inside-j.FarRoomFill.Fill)<.025f,"each space uses its own lit room fill");
                Result="PASS: corridor camera stays fixed through entry, lighting, completion and interruption; visible room changes; OFF keeps current view. Ambient outside/inside "+outside+"/"+inside;
            }
            finally{Application.runInBackground=background;}
        }
        static IEnumerator FixedCameraUntil(Func<bool> ready,FirstPersonController player,Vector3 position,Quaternion? fixedRotation)
        {
            float end=Time.realtimeSinceStartup+25;
            while(!ready()&&Time.realtimeSinceStartup<end)
            {
                Check(Vector3.Distance(player.view.position,position)<.0001f,"corridor camera never translates");
                if(fixedRotation.HasValue)Check(Quaternion.Angle(player.view.rotation,fixedRotation.Value)<.05f,"OFF preserves view direction");
                yield return null;
            }
            Check(ready(),"corridor lighting timeout");
            Check(Vector3.Distance(player.view.position,position)<.0001f,"corridor camera remains at original position");
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
                if(!e.Entered){e.EnterWorld();yield return Until(()=>e.Entered);}
                if(!e.room.Opened){e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);}
                e.board.Panel.SetOpen(false);
                e.journey.SetDoor(true);yield return Until(()=>e.journey.DoorProgress==1);yield return ChargeDoorChecks.UnlockEntrance(e);
                for(int pass=0;pass<4;pass++)
                {
                    var j=e.journey;
                    var centre=pass==2?j.Tutorial.Fill.transform.position:pass==3?j.FarRoomFill.transform.position:e.chamberLight.RoomCentre;
                    if(pass>=2)
                    {
                        p.Teleport(new Vector3(centre.x, j.Root.position.y+.1f, centre.z)+j.Root.right*3,j.Root.rotation);
                        yield return null;
                    }
                    // Include follow as well as static focus, normal completion and interruption.
                    e.board.Panel.SetOpen(true);
                    string rule=pass==1?"light.on":pass==2?"light.move":"light.move.floor";
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
                            else
                            {
                                var heading=Vector3.ProjectOnPlane(centre-p.view.position,Vector3.up);
                                Check(Mathf.Abs(p.view.forward.y)<.001f && Vector3.Angle(heading,p.view.forward)<.1f,"level shot towards room centre, pass "+pass+", space "+j.PlayerSpace+", forward "+p.view.forward+", expected "+heading.normalized);
                            }
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
                    else if(pass==1)Check(Cursor.lockState==CursorLockMode.None,"editing pointer is free");
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
                if(!e.Entered){e.EnterWorld();yield return Until(()=>e.Entered);}
                if(!e.room.Opened){e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);}
                var j=e.journey;var f=j.floorSurface;var state=e.board.State;
                j.SetDoor(true);yield return Until(()=>j.DoorProgress==1);yield return ChargeDoorChecks.UnlockEntrance(e);
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart+4)),j.Root.rotation);
                yield return Until(()=>j.InSecondRoom);j.SetLights(true);yield return Until(()=>j.Brightness>=1);yield return null;
                var wall=j.Root.Find("Room side wall").GetComponent<Renderer>();
                Check(wall.sharedMaterial.shader==f.Surface.sharedMaterial.shader&&wall.sharedMaterial.GetFloat("_Brightness")==1,"wall uses lit white ripple surface");
                var lamp=j.Lamps[0];var body=lamp.GetComponent<Rigidbody>();
                body.position=j.Root.TransformPoint(new Vector3(j.roomSize.x*.5f-.6f,3,j.RoomStart+6));lamp.direction=j.Root.right;Physics.SyncTransforms();
                int impacts=f.WallImpactCount;yield return Until(()=>f.WallImpactCount>impacts&&Vector3.Dot(lamp.direction,j.Root.right)<0);
                Check(f.WallImpactCount>impacts,"actual wall collision emits ripple");
                j.SetLights(false);yield return Until(()=>j.Brightness<=0);yield return null;
                Check(wall.sharedMaterial.GetFloat("_Brightness")==1,"wall retains dim fill");
                Check(!j.Root.Cast<Transform>().Any(t=>t.name.StartsWith("Door frame")),"fixed decorative frame absent");
                Check(j.Root.Cast<Transform>().Count(t=>t.name.Contains("opening")&&t.name.Contains("reveal"))==0,"no widened filament reveal planes");
                var floor=f.Surface.transform;
                float dock=floor.localPosition.z-floor.localScale.z*.5f;
                var gap=j.Root.TransformPoint(new Vector3(0,1,dock-j.platformDockGap*.5f));
                Check(!Physics.Raycast(gap,-j.Root.up,2,~0,QueryTriggerInteraction.Ignore),"platform leaves an open gap at the far room");
                state.ClearWorkspace();state.TryAcquireSentence("check.box.off","negative world");
                yield return Until(()=>e.room.Progress<.8f);
                var close=state.Groups.First(g=>g.RuleId=="world.off");var part=state.Detach(close.Id,close.Words.Last().Id);
                float pose=e.room.Progress;yield return new WaitForSeconds(.25f);
                Check(Mathf.Abs(e.room.Progress-pose)<.0001f&&!e.Busy&&e.board.Panel.IsOpen&&!e.board.Panel.InputBlocked,"split pauses closing and allows editing");
                state.JoinAndSpeak(part.Id,close.Id,false);yield return Until(()=>e.DialogReady);e.CancelExit();
                state.ClearWorkspace();state.TryAcquireSentence("check.box.on","positive world");yield return Until(()=>e.room.Progress>.15f);
                var open=state.Groups.First(g=>g.RuleId=="world.on");part=state.Detach(open.Id,open.Words.Last().Id);
                pose=e.room.Progress;yield return new WaitForSeconds(.25f);
                Check(Mathf.Abs(e.room.Progress-pose)<.0001f,"split pauses opening");
                state.JoinAndSpeak(part.Id,open.Id,false);yield return Until(()=>e.room.Opened&&!e.Busy);
                Check(!e.player.MovementLocked&&!e.portableClosure.Isolated,"reopening releases player and restores room");
                Result="PASS: actual wall hit/reflection, lit/dark walls, no decorative frame, no wide reveal planes, no floor overlap, box close/open split and resume, exit cancellation and portable restoration";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
