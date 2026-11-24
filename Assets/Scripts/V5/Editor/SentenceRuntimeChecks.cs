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
        static IEnumerator Until(Func<bool> predicate,float seconds=25,string label="timeout")
        {float end=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),label);}
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Run only in a fresh P5 Play session.");
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            if(!e||e.room.Progress>0)throw new InvalidOperationException("Start in the closed P5 box.");
            Result="Running";e.StartCoroutine(Exercise(e));
        }
        public static void RunCommandFinish(){
            Result="Running command completion";
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            e.StartCoroutine(CommandFinish(e));
        }
        static IEnumerator CommandFinish(WorldEntry e){
            var j=e.journey;var b=e.board;var s=b.State;
            e.OpenWorld(-1);yield return Until(()=>e.room.Opened && !e.Busy);
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            j.SetDoor(true);yield return Until(()=>j.DoorProgress==1);
            yield return ChargeDoorChecks.ReachRoomThree(e);
            e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart+3)),j.Root.rotation);
            yield return Until(()=>j.InSecondRoom);
            e.sentenceRuntime.presentation.enabledShots=false;
            e.player.MovementLocked=true;b.Panel.SetOpen(true);
            j.SetLights(true,PassageJourney.LightSpace.All);
            yield return Until(()=>j.Brightness==1 && e.chamberLight.Brightness==1);
            float fade=e.chamberLight.fadeDuration,doorDuration=j.doorDuration;
            try{
                ChargeDoorChecks.ConnectTimingFixture(j,true);
                e.chamberLight.fadeDuration=10;
                foreach(bool on in new[]{false,true}){
                    float target=on?1:0;
                    var command=VitruvianChecks.Say(b,on?"positive":"negative","light");
                    yield return Until(()=>j.Brightness==target);
                    double localDone=Time.unscaledTimeAsDouble;
                    Check(command.ExecutionLocked && Mathf.Abs(e.chamberLight.Brightness-target)>.4f,"remote lamp still fading at normal speed when the local fade completes");
                    yield return Until(()=>s.Find(command.Id)==null);
                    double tail=Time.unscaledTimeAsDouble-localDone;
                    Check(tail>=.5-Time.unscaledDeltaTime-.02 && tail<.8 && e.chamberLight.Brightness==target &&
                        !j.Stars.Settling(on) && !j.Tutorial.Settling(on),"remote light and stars finish with the local half-second window: "+tail);
                }
                j.SetDoor(false);yield return Until(()=>j.DoorProgress<.65f);
                j.WhiteDoor.Hold();j.doorDuration=10;
                var open=VitruvianChecks.Say(b,"positive","door");
                double started=Time.unscaledTimeAsDouble;
                yield return Until(()=>s.Find(open.Id)==null);
                Check(j.DoorProgress==1 && Time.unscaledTimeAsDouble-started<1,"remote ten-second hinge completes within the finishing window");
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.pathLength-2)),j.Root.rotation);
                yield return Until(()=>j.InCorridor);
                var lamps=j.CaptureState();
                for(int i=0;i<lamps.lamps.Length;i++){
                    lamps.lamps[i].docked=false;lamps.lamps[i].pose.parked=true;
                    lamps.lamps[i].pose.position=j.Root.TransformPoint(new Vector3(-5+i%5*2.4f,5,j.RoomStart+4+i/5*3));
                }
                Check(j.TryRestoreState(lamps),"set unobstructed remote lamp approaches");
                Physics.SyncTransforms();
                var lower=VitruvianChecks.Say(b,"light","move","floor");
                double movingAt=Time.timeAsDouble;
                yield return Until(()=>s.Find(lower.Id)==null);
                Check(Time.timeAsDouble-movingAt<1 && j.Lamps.All(j.floorMotion.IsArrived),"distant lamps actually reach the floor before judgement unlocks");
                // Place independent pairs within sight of each other but away from the other pairs.
                for(int i=0;i<lamps.lamps.Length;i++){
                    int pair=i/2;
                    float x=(pair%2==0?-3.5f:3.5f)+(i%2==0?-1.45f:1.45f);
                    lamps.lamps[i].pose.position=j.Root.TransformPoint(new Vector3(x,3,j.RoomStart+2.5f+pair/2*4));
                }
                Check(j.TryRestoreState(lamps),"set separate remote lamp pairs");
                Physics.SyncTransforms();
                Check(j.FarRoomMotion.CanMove(SentenceMotion.LightToLight),"lamp pairs have room to approach");
                var cluster=VitruvianChecks.Say(b,"light","move","light");
                movingAt=Time.timeAsDouble;
                yield return Until(()=>s.Find(cluster.Id)==null);
                Check(Time.timeAsDouble-movingAt<1 && !j.FarRoomMotion.CanMove(SentenceMotion.LightToLight),"distant lamp pairs reach spacing before judgement unlocks");
                Result="PASS: on/off light, star fades, hinge opening, directed descent and clustering finish in the local half-second window";
            }finally{ChargeDoorChecks.ConnectTimingFixture(j,false);e.chamberLight.fadeDuration=fade;j.doorDuration=doorDuration;}
        }
        public static void RunFeedback(){
            Result="Running";
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            e.StartCoroutine(CheckFeedback(e));
        }
        public static void RunOutputStates()
        {
            Result = "Running";
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            entry.StartCoroutine(OutputStates(entry));
        }
        static IEnumerator OutputStates(WorldEntry entry)
        {
            entry.OpenWorld(-1);
            yield return Until(() => entry.room.Opened && !entry.Busy);
            var lamp = entry.chamberLight;
            var emission = lamp.GetComponents<SentenceEntity>().SelectMany(entity => entity.Outputs)
                .Single(output => output.Source == lamp);
            var dimmer = emission.StateSource<EmissionState>();
            Check(dimmer != null && emission.StateSource<DriftPose>() == null &&
                !lamp.MotionOutput.TryCapture<EmissionState>(out _),
                "the same lamp exposes separate physical state types for emission and motion");
            emission.Command(false);
            yield return Until(() => emission.Value < .8f && emission.Value > .2f);
            emission.Hold();
            var dim = JsonUtility.FromJson<EmissionState>(JsonUtility.ToJson(dimmer.CaptureState()));
            var color = lamp.surface.material.GetColor("_BaseColor");
            emission.Command(true);
            yield return Until(() => emission.Value == 1);
            emission.Hold();
            Check(dimmer.TryRestoreState(dim) && emission.Value == dim.brightness &&
                lamp.surface.material.GetColor("_BaseColor") == color, "restore intermediate brightness and material");
            yield return null;
            Check(emission.Value == dim.brightness && !lamp.TransitionEnabled, "restore preserves hold");
            Check(!dimmer.TryRestoreState(new EmissionState { brightness = float.NaN }) &&
                emission.Value == dim.brightness, "invalid brightness leaves lamp intact");
            emission.Command(true);
            yield return Until(() => emission.Value == 1);
            Check(dimmer.TryRestoreState(dim) && lamp.TargetOn, "restore leaves current lamp command in charge");
            yield return Until(() => emission.Value == 1);

            var spoken = VitruvianChecks.Say(entry.board, "negative", "light");
            yield return Until(() => entry.board.State.Find(spoken.Id) == null && emission.Value == 0);
            Check(dimmer.TryRestoreState(dim), "restore after the spoken off command retired");
            yield return null;
            Check(emission.Value == dim.brightness && !lamp.TransitionEnabled, "retired off command cannot restart a fade");

            var journey = entry.journey;
            var door = journey.Door;
            var hinge = door.GetComponents<SentenceEntity>().SelectMany(entity => entity.Outputs)
                .Single(output => output.Source == journey.WhiteDoor && output.Definition == SentenceCapabilityCatalog.Default.hinge);
            var swing = hinge.StateSource<HingeState>();
            Check(swing != null, "registered entrance hinge supports restore");
            hinge.Command(true);
            yield return Until(() => hinge.Value > .25f && hinge.Value < .75f);
            hinge.Hold();
            var partial = JsonUtility.FromJson<HingeState>(JsonUtility.ToJson(swing.CaptureState()));
            var pose = door.parent.localRotation;
            hinge.Command(true);
            yield return Until(() => hinge.Value == 1);
            var motor = entry.player.GetComponent<CharacterController>();
            var clearPosition = entry.player.transform.position;
            var clearRotation = entry.player.transform.rotation;
            entry.player.Teleport(journey.Root.TransformPoint(new Vector3(0, .1f,
                journey.pathLength)), journey.Root.rotation);
            Physics.SyncTransforms();
            Check(!swing.TryRestoreState(partial) && hinge.Value == 1, "restored closing path respects doorway occupancy");
            entry.player.Teleport(clearPosition, clearRotation);
            Physics.SyncTransforms();
            Check(swing.TryRestoreState(partial) && hinge.Value == partial.progress &&
                Quaternion.Angle(pose, door.parent.localRotation) < .01f, "restore actual hinge angle");
            yield return null;
            Check(hinge.Value == partial.progress && !journey.DoorTransitionEnabled &&
                !Physics.GetIgnoreCollision(door.GetComponent<Collider>(), motor), "restored hinge holds and collides");
            Check(!swing.TryRestoreState(new HingeState { progress = 2 }) &&
                hinge.Value == partial.progress, "invalid hinge state leaves angle intact");
            hinge.Command(false);
            Check(swing.TryRestoreState(partial) && journey.DoorTransitionEnabled && !journey.DoorOpen, "restore keeps live closing command");
            yield return Until(() => hinge.Value == 0);
            yield return OtherIntroStates(journey);
            Result = "PASS: Intro hinge, grouped lamps, corridor and ascent star states, live commands, holds and safe collision";
        }

        static IEnumerator OtherIntroStates(PassageJourney journey)
        {
            var white = journey.WhiteDoorMotionOutput.StateSource<HingeState>();
            Check(white != null && ReferenceEquals(white, journey.WhiteDoor), "both white door bindings use the same hinge");
            journey.WhiteDoor.SetOpen(true);
            yield return Until(() => journey.DoorProgress > .2f && journey.DoorProgress < .8f);
            journey.WhiteDoor.Hold();
            var mid = RoundTrip(white.CaptureState());
            journey.WhiteDoor.SetOpen(true);
            yield return Until(() => journey.DoorProgress == 1);
            Check(white.TryRestoreState(mid) && journey.DoorProgress == mid.progress, "white door restores intermediate opening");
            yield return null;
            Check(journey.DoorProgress == mid.progress, "completed command stays released");

            var tutorial = journey.Tutorial;
            tutorial.SetOn(false);
            yield return Until(() => tutorial.SphereBrightness < .9f && tutorial.SphereBrightness > .05f);
            tutorial.Hold();
            var spheres = RoundTrip(tutorial.CaptureState());
            tutorial.SetOn(true);
            yield return Until(() => !tutorial.Settling(true));
            tutorial.Hold();
            Check(tutorial.TryRestoreState(spheres), "restore independent sphere and room levels");
            yield return null;
            Check(JsonUtility.ToJson(tutorial.CaptureState()) == JsonUtility.ToJson(spheres), "held sphere levels and lit flags survive");
            spheres.lamps[spheres.lamps.Length-1].brightness = -1;
            Check(!tutorial.TryRestoreState(spheres), "invalid last sphere rejects the group");

            var far = (ISentenceOutputState<EmissionState>)journey.FarRoom;
            journey.FarRoom.Hold();
            Check(far.TryRestoreState(new EmissionState(.37f)) && journey.Brightness == .37f, "far room shared lamp level");
            var ascent = journey.Ascent.Stars;
            ascent.Hold();
            Check(ascent.TryRestoreState(new EmissionState(.42f)), "restore ascent stars");
            yield return null;
            Check(ascent.Brightness == .42f, "held ascent stars stay at restored brightness");
            var floatPhase = ascent.MotionOutput.StateSource<AscentStars.MotionState>();
            var phase = RoundTrip(floatPhase.CaptureState());
            Check(floatPhase.TryRestoreState(new AscentStars.MotionState { time = 5 }) && floatPhase.CaptureState().time == 5 &&
                floatPhase.TryRestoreState(phase), "restore ascent motion phase independently of brightness");
            foreach (var stars in new[] { journey.Stars, journey.ReturnStars })
            {
                var motion = stars.MotionOutput.StateSource<CorridorStars.FlightState>();
                var savedFlight = RoundTrip(motion.CaptureState());
                var gathered = RoundTrip(savedFlight);
                gathered.parked = true;
                gathered.stars[0].position += Vector3.up;
                Check(motion.TryRestoreState(gathered) && motion.CaptureState().stars[0].position == gathered.stars[0].position &&
                    motion.TryRestoreState(savedFlight) && JsonUtility.ToJson(motion.CaptureState()) == JsonUtility.ToJson(savedFlight),
                    "star positions, launch origins, travel, fade and independent directions round-trip");
                gathered.stars[gathered.stars.Length-1].direction = Vector3.zero;
                Check(!motion.TryRestoreState(gathered) && !motion.CaptureState().parked, "invalid last flight leaves the full group intact");
                stars.SetOn(false);
                yield return Until(() => stars.Brightness < .8f && stars.Brightness > 0);
                stars.Hold();
                var saved = RoundTrip(stars.CaptureState());
                stars.SetOn(true);
                yield return Until(() => !stars.Settling(true));
                stars.Hold();
                Check(stars.TryRestoreState(saved) && JsonUtility.ToJson(stars.CaptureState()) == JsonUtility.ToJson(saved),
                    "path and individual star fade phases restored together");
                var unchanged = stars.Brightness;
                saved.path.brightness = .1f;
                saved.stars[saved.stars.Length-1].period = 0;
                Check(!stars.TryRestoreState(saved) && stars.Brightness == unchanged, "invalid star rejects the whole restore");
                stars.SetOn(true);
                yield return Until(() => !stars.Settling(true));
            }
        }
        static T RoundTrip<T>(T state) where T : struct => JsonUtility.FromJson<T>(JsonUtility.ToJson(state));


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
                Check(!UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Any(ps=>ps.transform.parent && ps.transform.parent.name=="Sentence grammar ripple"),
                    "no matching target emits no stars: "+words);
                yield return new WaitForSecondsRealtime(fade);
                Check(!e.sentenceRuntime.SustainsFeedback(group) && !Rippling(),"unavailable "+words+" never loops");
                Check(!state.JudgementLocked && (words=="light move" || !group.Spoken),"unavailable command leaves judgement free: "+words);
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
            yield return Until(()=>state.Find(command.Id)==null,65,"gather completes before mixed-space feedback sample");
            state.ClearWorkspace();
            j.SetLights(true,PassageJourney.LightSpace.ReturnPath);
            yield return Until(()=>j.ReturnStars.Brightness==1);
            state.TryAcquireSentence("feedback.multi","light move",out var multi);
            j.SetLights(false,PassageJourney.LightSpace.FarRoom);yield return Until(()=>j.Brightness==0);
            yield return new WaitForSecondsRealtime(fade);
            Check(!e.sentenceRuntime.SustainsFeedback(multi) && !Rippling(),"a closed entrance excludes the lit return path");
            try{
                ChargeDoorChecks.ConnectTimingFixture(j,true);
                yield return Until(()=>e.sentenceRuntime.SustainsFeedback(multi) && GameObject.Find("Sentence effect ripple"));
                Check(j.ReturnStars.Brightness>0 && j.PlayerSpace==PassageJourney.LightSpace.FarRoom,"reconnecting restores remote feedback while the player stays in the dark room");
            }finally{ChargeDoorChecks.ConnectTimingFixture(j,false);}
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
                Check(!redundant.Spoken && !state.JudgementLocked,"already lit produces an editable draft");
                state.ReturnToLibrary(redundant.Id,0,true);
                state.TryAcquireSentence("check.off","negative light");
                yield return new WaitForSeconds(.2f);
                float brightness=e.chamberLight.Brightness;
                Check(brightness>0&&brightness<1,"light starts gradual off");
                var off=state.Groups.First(g=>g.RuleId=="light.off");
                Check(!e.sentenceRuntime.SustainsFeedback(off),"active light command plays its initial feedback only");
                Check(state.Detach(off.Id,off.Words.Last().Id)==null,"running light command rejects splitting");
                state.TryAcquireSentence("check.on","positive light",out var on);
                Check(off.ExecutionLocked && !on.Spoken && !on.Effective,"opposite light command waits for a fresh period");
                state.ReturnToLibrary(off.Id,0,true);
                yield return Until(()=>state.Find(off.Id)==null);
                Check(e.chamberLight.Brightness==0 && !on.Effective,"off completes without automatically relighting");
                e.board.Panel.SetOpen(true);presentation.enabledShots=true;
                int shotsBefore=presentation.PlayCount;
                Check(state.Speak(on.Id),"manually speak ON after OFF completes");
                yield return Until(()=>presentation.Playing);
                yield return Until(()=>!presentation.Playing);
                Check(presentation.PlayCount==shotsBefore+1 && e.chamberLight.Brightness==1 && j.Brightness==0,"manual reversal brightens the chamber");
                presentation.enabledShots=false;
                yield return Until(()=>state.Find(on.Id)==null);
                // Teleporting into the passage does not bypass its reveal progress lock.
                yield return ChargeDoorChecks.UnlockFirstPassage(e);
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.pathLength-4)),j.Root.rotation);
                yield return Until(()=>j.InCorridor);
                var doorPosition=j.Door.position;
                Check(j.Door.parent.Find("Round handle"),"hinge carries a round handle");
                Check(!j.Root.Cast<Transform>().Any(t=>t.name.Contains("reveal")),"no wide reveal-plane artifacts");
                state.TryAcquireSentence("check.door.on","positive door",out var doorOn);yield return new WaitForSeconds(.25f);
                Check(state.Find(doorOn.Id)!=null,"unlocked white door keeps its opening command until complete");
                float progress=j.DoorProgress;Check(progress>0&&progress<1,"door starts gradual opening");
                Check(state.Detach(doorOn.Id,doorOn.Words.Last().Id)==null,"partial door command rejects splitting");
                yield return new WaitForSeconds(.2f);
                Check(j.DoorProgress>progress,"door continues opening");
                var surface=new MaterialPropertyBlock();j.Door.GetComponent<Renderer>().GetPropertyBlock(surface);
                Check(surface.GetFloat("_Dissolve")==0,"swinging leaf stays opaque");
                Check(Mathf.Abs(j.Door.position.y-doorPosition.y)<.001f,"swing stays level");
                yield return Until(()=>j.DoorProgress>=1);
                Check(j.Door.GetComponent<Renderer>().enabled&&j.Door.GetComponent<Collider>().enabled,"open leaf remains solid and visible");
                Check(Mathf.Abs(Vector3.Angle(j.Root.forward,j.Door.forward)-j.openAngle)<.05f,"fully open leaf reaches its configured swing angle");
                yield return Until(()=>state.Find(doorOn.Id)==null);
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.05f,j.pathLength)),j.Root.rotation);
                state.TryAcquireSentence("check.door.off","negative door",out var doorOff);
                yield return new WaitForSeconds(1.5f);
                Check(j.DoorProgress==1,"closing waits while player occupies doorway");
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.05f,j.RoomStart+4)),j.Root.rotation);yield return Until(()=>j.DoorProgress<=0);
                Check(j.DoorProgress==0,"off door closes after doorway clears");
                Check(j.Door.GetComponent<Renderer>().enabled&&j.Door.GetComponent<Collider>().enabled&&j.Door.position==doorPosition,"off restores stationary white leaf and collision");
                Check(!j.Root.GetComponentsInChildren<Renderer>().Any(r=>r.name.Contains("door")&&r.name.Contains("filaments")&&r.transform.localPosition.z>j.pathLength&&r.enabled),"closed door conceals the room-side outline");
                Check(j.WordAt(j.Door.GetComponent<Collider>(),1)==null,"closed rear door does not reveal itself through collection hint");
                yield return Until(()=>state.Find(doorOff.Id)==null);
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
                Check(off.ExecutionLocked && j.Brightness<brightness,"editing after shot interruption leaves the fade running");
                Check(Mathf.Abs(Vector3.Dot(e.player.view.right,Vector3.up))<.0001f,"camera roll stays zero");
                Result="PASS: light/door priority, execution lock, manual reversal, safe door closure, deferred effect commit, shot interruption and input restoration";
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
                    // Direct timeline requests without a mechanism keep the current view.
                    e.board.Panel.SetOpen(true);
                    string rule=pass==1?"light.on":pass==2?"light.move":"light.move.floor";
                    var position=p.transform.position;var camera=p.view.position;
                    var rotation=p.view.rotation;var roll=p.view.eulerAngles.z;bool applied=false;
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
                                Check(Quaternion.Angle(rotation,p.view.rotation)<.01f,"no focus request keeps the current view");
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
                Result="PASS: full-width bars, fixed player/camera positions and unchanged view without a focus request, normal completion, interruption, movement and editor restoration";
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
                var gap=j.Root.TransformPoint(new Vector3(0,1,j.RoomStart-RailChargeRoom.LandingDepth-RailChargeRoom.JumpGap*.5f));
                Check(!Physics.Raycast(gap,-j.Root.up,2,~0,QueryTriggerInteraction.Ignore),"jump gap before the fixed room-three doorstep");
                state.ClearWorkspace();state.TryAcquireSentence("check.box.off","negative world");
                yield return Until(()=>e.room.Progress<.8f);
                var close=state.Groups.First(g=>g.RuleId=="world.off");
                Check(state.Detach(close.Id,close.Words.Last().Id)==null && !state.Unsay(close.Id),"closing world refuses edits");
                yield return Until(()=>e.DialogReady);e.CancelExit();
                yield return Until(()=>state.Find(close.Id)==null);
                state.TryAcquireSentence("check.box.on","positive world",out var open);yield return Until(()=>e.room.Progress>.15f);
                Check(state.Detach(open.Id,open.Words.Last().Id)==null && !state.Unsay(open.Id),"opening world refuses edits");
                yield return Until(()=>e.room.Opened&&!e.Busy);
                Check(!e.player.MovementLocked&&!e.portableClosure.Isolated,"reopening releases player and restores room");
                Result="PASS: actual wall hit/reflection, lit/dark walls, no decorative frame, no wide reveal planes, no floor overlap, uninterrupted box closing and opening, exit cancellation and portable restoration";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
