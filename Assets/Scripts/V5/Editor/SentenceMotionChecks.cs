using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using PuzzleApple.V5.Cognition;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceMotionChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string why){if(ok)return;Result="FAIL: "+why;throw new InvalidOperationException(Result);}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Exercise(e));}
        public static void RunConflicts(){
            Result="Running";
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            e.StartCoroutine(Conflicts(e));
        }
        static IEnumerator Conflicts(WorldEntry e){
            e.sentenceRuntime.presentation.enabledShots=false;
            e.OpenWorld(-1);yield return WaitFor(()=>e.room.Opened&&!e.Busy,"open Intro");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            var b=e.board;var state=b.State;var stars=e.journey.Stars;
            stars.SetOn(false);yield return WaitFor(()=>!stars.Settling(false),"stars go dark");
            b.Panel.SetOpen(true);yield return WaitFor(()=>b.Panel.FullyOpen,"open sentence board");
            int weak=e.feedback.WeakCount;
            var gather=VitruvianChecks.Say(b,"light","move","door");
            Check(!gather.Spoken && !state.JudgementLocked && e.feedback.WeakCount==weak+1,"dark stars give one weak result without locking");
            var drift=VitruvianChecks.Say(b,"light","move");
            yield return WaitFor(()=>e.chamberLight.MotionOutput.HasMotion(SentenceMotion.Light),"immediately start a different sentence");
            stars.SetOn(true);
            yield return WaitFor(()=>stars.EmissionLevel>0,"stars relight");
            weak=e.feedback.WeakCount;
            Check(!state.Speak(gather.Id) && e.feedback.WeakCount==weak+1 && state.CanPrompt(gather),"occupied output leaves an immediately retryable draft");
            Check(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Any(ps=>ps.transform.parent && ps.transform.parent.name=="Sentence grammar ripple"),
                "occupied targets give a dim ripple with stars");
            var prompt=b.Surface.GetComponentsInChildren<PeriodGlyph>(true).Single(p=>p.name=="Period prompt" && p.group==gather.Id);
            yield return null;
            Check(!prompt.gameObject.activeSelf,"the failed attempt finishes its sentence ripple first");
            yield return WaitFor(()=>prompt.gameObject.activeSelf,"period prompt relights with the same target still occupied");
            Check(!gather.Spoken && drift.Effective,"relighting a prompt does not retry the command");
            state.Unsay(drift.Id);
            int attempts=state.HistoryVersion;
            yield return WaitFor(()=>state.CanPrompt(gather),"release restores prompt");
            Check(!gather.Spoken && attempts==state.HistoryVersion,"release never starts gathering");
            var freeze=VitruvianChecks.Say(b,"negative","move");
            Check(e.sentenceRuntime.MotionFrozen && !state.Speak(gather.Id) && !state.JudgementLocked,"frozen movement cannot acquire the execution lock");
            Check(state.Unsay(freeze.Id) && state.CanPrompt(gather) && !gather.Spoken,"unfreezing restores only the prompt");
            Check(state.Speak(gather.Id) && state.JudgementLocked,"explicit retry starts actual gathering");
            var duplicate=VitruvianChecks.Say(b,"light","move","door");
            Check(!duplicate.Spoken && !state.Speak(drift.Id),"unrelated and duplicate periods are both suspended");
            yield return WaitFor(()=>state.Find(gather.Id)==null,"lit stars complete gathering");
            Check(!state.JudgementLocked && !drift.Spoken && !duplicate.Spoken,"no queued clicks after completion");
            var unsupported=VitruvianChecks.Say(b,"door","move","floor");
            Check(!unsupported.Spoken && !state.JudgementLocked,"anchored target never locks");
            Result="PASS: dark, conflicting and unsupported targets, explicit retry and global execution lock";
        }
        public static void RunSustainedWaiting(){
            Result="Running";
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            e.StartCoroutine(SustainedWaiting(e));
        }
        static IEnumerator SustainedWaiting(WorldEntry e){
            e.OpenWorld(-1);yield return WaitFor(()=>e.room.Opened&&!e.Busy,"open Intro");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            var j=e.journey;var b=e.board;var state=b.State;var show=e.sentenceRuntime.presentation;
            j.SetDoor(true);yield return WaitFor(()=>j.DoorProgress==1,"unlock platform");
            b.Panel.SetOpen(true);yield return WaitFor(()=>b.Panel.FullyOpen,"open editor");
            var owner=VitruvianChecks.Say(b,"floor","mirror","light");
            yield return null;yield return null;
            int weak=e.feedback.WeakCount,shots=show.PlayCount;
            var waiting=VitruvianChecks.Say(b,"floor","move");
            long order=waiting.Established;
            yield return WaitFor(()=>e.feedback.WeakCount>weak,"occupied floor gives one weak pulse");
            Check(waiting.Spoken && waiting.Conflicted && !waiting.Effective && !j.Platform.MotionOutput.HasMotion(SentenceMotion.Floor),
                "blocked sustained sentence keeps its period and waits");
            int attempts=state.HistoryVersion;
            state.Unsay(owner.Id);
            yield return WaitFor(()=>j.Platform.MotionOutput.HasMotion(SentenceMotion.Floor),"release starts waiting platform");
            Check(waiting.Established==order && waiting.Effective && state.HistoryVersion==attempts && show.PlayCount==shots,
                "release resumes the original utterance without another attempt or shot");
            state.ClearWorkspace();
            owner=VitruvianChecks.Say(b,"floor","mirror","light");yield return null;yield return null;
            waiting=VitruvianChecks.Say(b,"floor","move");
            Check(waiting.Conflicted && state.Detach(waiting.Id,waiting.Words[1].Id)!=null,"split while waiting");
            state.Unsay(owner.Id);yield return null;yield return null;
            Check(!j.Platform.MotionOutput.HasMotion(SentenceMotion.Floor),"removed utterance never resumes");
            state.ClearWorkspace();
            var lamp=e.chamberLight;
            lamp.SetOn(true);yield return WaitFor(()=>lamp.Brightness==1,"lit local target");
            e.journey.SetLights(false,Space.FirstPath|Space.SphereRoom|Space.ReturnPath|Space.FarRoom);
            yield return WaitFor(()=>!j.Lights(Space.FirstPath|Space.SphereRoom|Space.ReturnPath|Space.FarRoom).Any(l=>l.WouldChange(false)),"dark remote targets");
            var command=VitruvianChecks.Say(b,"negative","light");
            yield return WaitFor(()=>show.Playing,"begin local feedback lead");
            lamp.HoldCurrentState();
            Check(lamp.TryRestoreState(new EmissionState(0)),"extinguish and hold the target before dispatch");
            yield return WaitFor(()=>!state.JudgementLocked && !show.Playing,"lost target releases command");
            Check(state.Find(command.Id)==command && !command.Spoken && state.CanPrompt(command) && b.Panel.IsOpen,
                "unstarted command stays editable for manual retry");
            Check(!state.History.Any(h=>h.Effective && h.Words.SequenceEqual(command.Words.Select(w=>w.Definition))),
                "a lost target does not leave a false successful history entry");
            Result="PASS: blocked sustained period, automatic takeover, split while waiting and loss of a one-shot target before dispatch";
        }
        public static void RunChapterConflicts(){
            Result="Running";
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            e.StartCoroutine(ChapterConflicts(e));
        }
        static IEnumerator ChapterConflicts(WorldEntry e){
            yield return VitruvianChecks.Gallery(e);
            var b=e.board;var unfold=VitruvianChecks.Say(b,"positive","world");
            yield return WaitFor(()=>e.outdoor.Chapter.box.Opened && b.State.Find(unfold.Id)==null,"open gallery");
            var room=e.outdoor.Vitruvian;var puzzle=room.puzzle;
            e.player.Teleport(room.transform.TransformPoint(new Vector3(0,.1f,7)),room.transform.rotation);
            puzzle.SetOn(true);
            yield return WaitFor(()=>puzzle.ball.IsOn && puzzle.source.IsOn,"light the orbital lamp and local source");
            var body=puzzle.field.Spawn(room.transform.TransformPoint(new Vector3(2,6,4)),.22f).GetComponent<Rigidbody>();
            puzzle.field.Spawn(room.transform.TransformPoint(new Vector3(-2,6,3)),.22f);
            var created=puzzle.field.GetComponent<Outdoor.LightSentenceEffects>();
            yield return WaitFor(()=>created.Lamps.Count>0,"register created chapter lamp");
            var down=VitruvianChecks.Say(b,"light","move","floor");
            yield return WaitFor(()=>created.Motion==SentenceMotion.LightToFloor,"created lamp takes downward command");
            var cluster=VitruvianChecks.Say(b,"light","move","light");
            Check(!cluster.Spoken && !cluster.ExecutionLocked && down.ExecutionLocked,"a competing command with no free lamps stays a draft");
            var start=body.position;
            var orbit=VitruvianChecks.Say(b,"light","move");
            Check(!orbit.Spoken && down.ExecutionLocked,"global lock also covers unrelated free outputs");
            yield return WaitFor(()=>body.position.y<start.y-.05f,"created lamp descends");
            yield return WaitFor(()=>b.State.Find(down.Id)==null,"landing releases judgement");
            float angle=puzzle.OrbitAngle;
            Check(!cluster.Spoken && !orbit.Spoken && b.State.Speak(orbit.Id),"only an explicit click starts drift");
            yield return WaitFor(()=>created.Motion==SentenceMotion.Light && Mathf.Abs(Mathf.DeltaAngle(angle,puzzle.OrbitAngle))>2,"created lamp and orbital lamp drift");
            Result="PASS: landing locks new judgements across outputs and restores explicit drift";
        }
        public static void RunLuminousSurfaces(){
            Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(LuminousSurfaces(e));
        }
        static IEnumerator LuminousSurfaces(WorldEntry e){
            e.sentenceRuntime.presentation.enabledShots=false;
            e.OpenWorld(-1);yield return ChargeDoorChecks.Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            var j=e.journey;var b=e.board;var state=b.State;
            j.SetDoor(true);yield return ChargeDoorChecks.Wait(()=>j.DoorProgress==1,"unlock rail platform");
            var lightMove=VitruvianChecks.Say(b,"light","move");
            yield return ChargeDoorChecks.Wait(()=>j.Stars.MotionOutput.HasMotion(SentenceMotion.Light),"ordinary lamp motion starts");
            Check(!j.WhiteDoorMotionOutput.HasMotion(SentenceMotion.Door) && !j.Platform.MotionOutput.HasMotion(SentenceMotion.Floor),"ordinary motion does not move unlit surfaces");
            var rail=j.Platform.transform.position;
            var floor=VitruvianChecks.Say(b,"light","floor");
            yield return ChargeDoorChecks.Wait(()=>Vector3.Distance(j.Platform.transform.position,rail)>.1f,"already spoken motion moves the newly lit rail");
            var door=VitruvianChecks.Say(b,"light","door");
            yield return ChargeDoorChecks.Wait(()=>j.DoorProgress<.75f && j.DoorProgress>.15f,"lit hinge swings");
            var open=VitruvianChecks.Say(b,"positive","door");
            yield return ChargeDoorChecks.Wait(()=>!j.WhiteDoorMotionOutput.HasMotion(SentenceMotion.Door) && state.Find(open.Id)!=null,"opening command takes the hinge");
            Check(e.chamberLight.MotionOutput.HasMotion(SentenceMotion.Light),"opening the door leaves lamp drift active");
            yield return ChargeDoorChecks.Wait(()=>state.Find(open.Id)==null && j.WhiteDoorMotionOutput.HasMotion(SentenceMotion.Door) && j.DoorProgress<.9f,"lit door resumes its own swing");
            state.Unsay(door.Id);state.Unsay(floor.Id);
            yield return ChargeDoorChecks.Wait(()=>!j.WhiteDoorMotionOutput.HasMotion(SentenceMotion.Door) && !j.Platform.MotionOutput.HasMotion(SentenceMotion.Floor),"surfaces lose motion as their emission reaches zero");
            Check(lightMove.Effective && e.chamberLight.MotionOutput.HasMotion(SentenceMotion.Light),"ordinary lamp remains independently driven");
            yield return ChargeDoorChecks.Wait(()=>e.outdoor.Prepared,"outdoor scene ready");
            e.outdoor.ArriveFromAscent();yield return null;
            var world=e.outdoor.World;float height=world.ground.transform.position.y;
            VitruvianChecks.Say(b,"light","floor");var outdoorDrift=VitruvianChecks.Say(b,"light","move");
            yield return ChargeDoorChecks.Wait(()=>world.environment.WorldMoving,"luminous outdoor ground moves sky and clouds");
            Check(world.ground.transform.position.y==height,"unconstrained floor motion preserves outdoor ground height");
            b.Panel.SetOpen(true);yield return WaitFor(()=>b.Panel.FullyOpen,"open outdoor board");
            int weak=e.feedback.WeakCount;
            var native=VitruvianChecks.Say(b,"floor","move");
            yield return WaitFor(()=>world.world.MotionOutput.Owns(SentenceMotion.Floor,native.Established) && e.feedback.WeakCount>weak,"equivalent floor motion shares ownership with weak feedback");
            Check(!e.sentenceRuntime.SustainsFeedback(native),"one moving sky has one sustained feedback owner");
            state.Unsay(outdoorDrift.Id);
            yield return WaitFor(()=>e.sentenceRuntime.SustainsFeedback(native) && world.environment.WorldMoving,"remaining sentence takes over feedback and sky motion");
            var surface=world.ground.GetComponent<Renderer>();
            yield return WaitFor(()=>e.sentenceRuntime.FloorIllumination>=1,"outdoor floor reaches its base emission");
            float BaseEmission()=>surface.sharedMaterial.GetColor("_EmissionColor").maxColorComponent;
            float unboosted=BaseEmission();
            var boost=VitruvianChecks.Say(b,"light","light");
            yield return WaitFor(()=>unboosted>0 && BaseEmission()>unboosted*1.24f,"light light increases the floor material's emission");
            Check(BaseEmission()<=unboosted*1.251f,"stacked floor emission stays within its extra 25 percent");
            state.Unsay(boost.Id);
            yield return WaitFor(()=>Mathf.Abs(BaseEmission()-unboosted)<.001f,"only the added floor boost fades out");
            Result="PASS: lit platform, hinge and outdoor sky follow light move, hinge priority, fade-out and measured floor boost";
        }
        public static void RunLightThresholds(){
            Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(LightThresholds(e));
        }
        static IEnumerator WaitFor(Func<bool> predicate,string reason){
            float end=Time.realtimeSinceStartup+30;
            while(!predicate() && Time.realtimeSinceStartup<end)yield return null;
            Check(predicate(),reason);
        }
        static IEnumerator LightThresholds(WorldEntry e){
            e.sentenceRuntime.presentation.enabledShots=false;
            e.OpenWorld(-1);yield return WaitFor(()=>e.room.Opened&&!e.Busy,"open Intro");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            var j=e.journey;var b=e.board;
            e.chamberLight.SetOn(false);yield return WaitFor(()=>e.chamberLight.Brightness==0,"chamber fully dark");
            b.Panel.SetOpen(true);yield return WaitFor(()=>b.Panel.FullyOpen,"open board for feedback");
            int strong=e.feedback.StrongCount;
            var boost=VitruvianChecks.Say(b,"light","light");
            yield return WaitFor(()=>e.feedback.StrongCount>strong,"dark chamber still gets effective feedback for reachable corridor stars");
            b.State.Unsay(boost.Id);
            b.Panel.SetOpen(false);
            e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.pathLength*.5f)),j.Root.rotation);
            yield return WaitFor(()=>j.InCorridor,"enter first corridor");
            var stars=j.Stars.GetComponentsInChildren<MeshRenderer>();
            bool CanPick(){
                var eye=e.player.view.position;
                return stars.Any(r=>r.enabled && e.chamberLight.PickStar(new Ray(eye,r.bounds.center-eye),e.chamberLight.collectionDistance,out _)==r);
            }
            yield return WaitFor(CanPick,"a reachable emitting star");
            j.Stars.SetOn(false);
            yield return WaitFor(()=>j.Stars.Brightness<1 && CanPick(),"a fading star is collectible");
            yield return WaitFor(()=>!j.Stars.Settling(false),"staggered shrink finishes");
            Check(!CanPick(),"extinguished corridor stars cannot be collected");
            j.Stars.SetOn(true);
            yield return WaitFor(()=>j.Stars.Brightness<1 && CanPick(),"a brightening star is collectible");
            var tutorial=j.Tutorial;var collider=tutorial.Sphere.GetComponent<Collider>();
            tutorial.SetOn(false);yield return WaitFor(()=>!tutorial.AnyLit,"teaching lamps fully dark");
            tutorial.SetOn(true);
            Check(!tutorial.IsLit(collider),"setting the on target alone does not create light");
            yield return WaitFor(()=>tutorial.IsLit(collider) && tutorial.SphereBrightness<1,"first positive emission makes the ball light");
            tutorial.SetOn(false);Check(tutorial.IsLit(collider),"off target alone does not remove light");
            yield return WaitFor(()=>!tutorial.IsLit(collider),"ball loses light at zero");
            Result="PASS: real star pick on both fade edges, teaching ball follows emission, remote light contributes to feedback";
        }
        public static void RunControllers(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Controllers(e));}
        public static void RunDoors(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Doors(e));}
        public static void RunOutdoor(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(OutdoorControllers(e));}
        public static void RunDemonstration(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Demonstration(e));}
        static IEnumerator SampleDrift(Transform subject,bool moving,string label)
        {
            yield return new WaitForSeconds(.06f);var start=subject.position;yield return new WaitForSeconds(.2f);
            Check(moving?Vector3.Distance(start,subject.position)>.01f:Vector3.Distance(start,subject.position)<.001f,label);
        }
        static IEnumerator Demonstration(WorldEntry e)
        {
            e.OpenWorld(-1);yield return ChargeDoorChecks.Wait(()=>e.room.Opened&&!e.Busy,"open demonstration fixture");
            var j=e.journey;var state=e.board.State;
            e.sentenceRuntime.presentation.enabledShots=false;e.player.MovementLocked=true;
            Check(!state.Knows("move"),"fixture opens the tutorial before learning move");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            j.SetDoor(true);yield return ChargeDoorChecks.Wait(()=>j.DoorProgress==1,"introduce tutorial");
            e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.pathLength+3)),j.Root.rotation);
            var small=j.Root.Find("Move tutorial small sphere 1");var output=j.Tutorial.MotionOutput;
            Check(output.Introduced&&!output.IntroducedWithMove,"output remembers introduction before move was known");
            yield return SampleDrift(small,true,"small sphere demonstrates before learning");
            state.TryAcquireWord("motion.check.verb",e.board.Catalog.Word("move"));
            yield return SampleDrift(small,true,"learning the verb alone does not take over demonstration");
            VitruvianChecks.Say(e.board,"light","move");
            yield return ChargeDoorChecks.Wait(()=>output.ControlsLamps,"spoken movement takes ownership");
            state.ClearWorkspace();yield return SampleDrift(small,false,"split holds the controlled lit sphere");
            j.Tutorial.SetOn(false);yield return ChargeDoorChecks.Wait(()=>!j.Tutorial.AnyLit,"tutorial fully dark");
            yield return SampleDrift(small,true,"dark small sphere returns to its demonstration drift");
            int revision=output.LampMotionRevision;
            j.Tutorial.SetOn(true);yield return ChargeDoorChecks.Wait(()=>!j.Tutorial.WouldChange(true),"relight tutorial");
            Check(output.LampMotionRevision==revision,"brightness alone does not revise motion");
            yield return SampleDrift(small,true,"relighting preserves the ongoing demonstration drift");
            Check(output.TryCapture<MoveTutorialRoom.MotionState>(out var relitCheckpoint), "capture teaching lamp pose and takeover together");
            var json = JsonUtility.ToJson(relitCheckpoint);
            var saved = JsonUtility.FromJson<SentenceMotionOutput.Checkpoint<MoveTutorialRoom.MotionState>>(json);
            Check(saved.demonstration.controlsLamps && saved.demonstration.relitLamps.Any(value => value),
                "checkpoint stores actual relit lamps");
            var spoken=VitruvianChecks.Say(e.board,"light","move");yield return null;
            state.Unsay(spoken.Id);yield return SampleDrift(small,false,"new motion followed by removing its period ends relit drift");
            Check(output.LampMotionRevision>revision,"motion changes advance the local revision");
            var invalid = JsonUtility.FromJson<SentenceMotionOutput.Checkpoint<MoveTutorialRoom.MotionState>>(json);
            invalid.physical.lamps[0].direction = Vector3.one * 4;
            Check(!output.TryRestore(invalid) && output.LampMotionRevision > saved.demonstration.revision,
                "invalid physical data leaves takeover state intact");
            Check(output.TryRestore(saved) && !output.HasMotion(SentenceMotion.Light) && !spoken.Spoken,
                "restoring the relit demonstration preserves the removed period");
            Array.Clear(saved.demonstration.relitLamps, 0, saved.demonstration.relitLamps.Length);
            yield return SampleDrift(small,true,"relit teaching lamp resumes from the independently restored checkpoint");
            var registration=j.Tutorial.GetComponents<SentenceEntity>().Single(entity=>ReferenceEquals(entity.Endpoint,output));
            try{
                registration.enabled=false;yield return null;yield return null;
                state.ForgetWords(new[]{"move"});yield return null;
                state.TryAcquireWord("motion.check.relearn",e.board.Catalog.Word("move"));yield return null;
                Check(!output.TryRestore(relitCheckpoint) && !output.CaptureState().controlsLamps,
                    "inactive output also observes forgetting before restoring or exporting");
                registration.enabled=true;yield return null;yield return null;
                var beforeStale = small.position;
                Check(!output.ControlsLamps && !output.TryRestore(relitCheckpoint) && small.position == beforeStale,
                    "forgetting and relearning rejects the old takeover before changing a pose");
                yield return SampleDrift(small,true,"relearning only the verb restores the unclaimed demonstration");
            }finally{registration.enabled=true;}
            e.player.MovementLocked=false;
            Result="PASS: first introduction, verb-only learning, spoken takeover, split, dark drift, relit continuity, local motion revision, period removal, forgetting and disabled-output relearning";
        }
        static IEnumerator EndpointPause(WorldEntry e,Component owner,SentenceMotionOutput output,Func<Vector3> position,params string[] words)
        {
            Check(output!=null,"controller has an assigned motion output: "+owner.name);
            var registration=owner.GetComponents<SentenceEntity>().Single(entity=>ReferenceEquals(entity.Endpoint,output));
            var group=VitruvianChecks.Say(e.board,words.Length==0?new[]{"light","move"}:words);
            var requestedMotion = group.Intent.Motion;
            yield return ChargeDoorChecks.Wait(()=>output.HasMotion(requestedMotion),"receive motion: "+owner.name);
            Check(output.Owns(requestedMotion,group.Established),"endpoint retains the applied sentence identity");
            yield return new WaitForSeconds(.1f);
            var start=position();yield return new WaitForSeconds(.2f);
            Check(Vector3.Distance(start,position())>.005f,"local output drives movement: "+owner.name);
            try{
                // Keep the controller and the global sentence alive: only its registration leaves.
                registration.enabled=false;
                yield return ChargeDoorChecks.Wait(()=>!output.HasMotion(requestedMotion),"release disabled endpoint");
                Check(!output.Owns(requestedMotion,group.Established),"disabled endpoint releases sentence ownership");
                Check(e.sentenceRuntime.HasEffect(group.Signal),"global sentence remains applied");
                yield return new WaitForSeconds(.06f);
                var rest=position();yield return new WaitForSeconds(.2f);
                Check(Vector3.Distance(rest,position())<.001f,"controller holds without its output: "+owner.name);
                registration.enabled=true;
                yield return ChargeDoorChecks.Wait(()=>output.HasMotion(requestedMotion),"restore endpoint");
                yield return new WaitForSeconds(.2f);
                Check(Vector3.Distance(rest,position())>.005f,"controller resumes through its output: "+owner.name);
            }finally{registration.enabled=true;}
            e.board.State.ClearWorkspace();yield return null;yield return null;
            Check(!output.Owns(requestedMotion,group.Established),"split releases local sentence ownership");
        }
        static IEnumerator Doors(WorldEntry e)
        {
            yield return ChargeDoorChecks.OpenEnding(e);
            var j=e.journey;var d=j.EndingDoor;
            e.player.MovementLocked=true;
            e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.pathLength-2)),j.Root.rotation);
            yield return null;
            yield return EndpointPause(e,j.Door,j.WhiteDoorMotionOutput,()=>Vector3.up*j.DoorProgress,"door","move");
            foreach (var lift in new Component[]{j.GarageDoor,j.GarageExitDoor,j.EntranceDoor,d})
                Check(lift.GetComponents<SentenceEntity>().All(entity=>!entity.Outputs.Any()),"proximity lift has no sentence motion actuator");
            Result="PASS: white hinge responds to motion and all proximity lifts are observational door targets";
        }
        static IEnumerator OutdoorControllers(WorldEntry e)
        {
            e.OpenWorld(-1);
            yield return ChargeDoorChecks.Wait(()=>e.room.Opened&&!e.Busy&&e.outdoor.Prepared,"prepare outdoor output fixture");
            e.outdoor.ArriveFromAscent();yield return null;
            e.sentenceRuntime.presentation.enabledShots=false;e.player.MovementLocked=true;
            var w=e.outdoor.World;var origin=w.transform.position;
            e.player.Teleport(origin+new Vector3(0,.2f,12),Quaternion.identity);
            var lamp=w.field.Spawn(origin+new Vector3(4,12,18),.22f).GetComponent<Rigidbody>();
            var door=w.door.GetComponent<Rigidbody>();door.position=origin+new Vector3(10,10,0);
            Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            var outputs=new[]{w.lights.MotionOutput,w.doors.MotionOutput,w.world.MotionOutput};
            Check(outputs.All(o=>o!=null)&&outputs.Distinct().Count()==3,"co-located controllers have independent outputs");
            SentenceMotionBindings.BindOutdoor(w);yield return null;
            Check(outputs.SequenceEqual(new[]{w.lights.MotionOutput,w.doors.MotionOutput,w.world.MotionOutput})&&
                outputs.All(o=>e.Entities.QueryAll<SentenceMotionOutput>().Count(found=>ReferenceEquals(found,o))==1),"rebinding preserves each controller identity without duplicates");
            yield return EndpointPause(e,w.lights,w.lights.MotionOutput,()=>lamp.position,"light","move");
            yield return EndpointPause(e,w.doors,w.doors.MotionOutput,()=>door.position,"door","move");
            var floor=VitruvianChecks.Say(e.board,"floor","move");
            VitruvianChecks.Say(e.board,"light","move");VitruvianChecks.Say(e.board,"door","move");
            yield return ChargeDoorChecks.Wait(()=>w.environment.WorldMoving&&w.clouds.FollowFacing,"floor output drives sky");
            var output=w.world.MotionOutput;
            var registration=w.world.GetComponents<SentenceEntity>().Single(entity=>ReferenceEquals(entity.Endpoint,output));
            try{
                registration.enabled=false;
                yield return ChargeDoorChecks.Wait(()=>!output.HasMotion(floor.Intent.Motion)&&!w.environment.WorldMoving,"release floor output");
                Check(e.sentenceRuntime.HasEffect(floor.Signal),"floor sentence remains globally active");
                var lampStart=lamp.position;var doorStart=door.position;yield return new WaitForSeconds(.2f);
                Check(Vector3.Distance(lampStart,lamp.position)>.01f&&Vector3.Distance(doorStart,door.position)>.01f,"disabling floor output does not stop lamp or door outputs on the same object");
                registration.enabled=true;
                yield return ChargeDoorChecks.Wait(()=>w.environment.WorldMoving,"reenabled floor output resumes sky");
                SentenceEntity.Bind(e.Entities,w.world,output,()=>Space.Garden);
                yield return ChargeDoorChecks.Wait(()=>!output.HasMotion(floor.Intent.Motion)&&!w.environment.WorldMoving,"out-of-scope callback holds sky motion");
            }finally{
                registration.enabled=true;SentenceMotionBindings.BindOutdoor(w);
            }
            yield return ChargeDoorChecks.Wait(()=>w.environment.WorldMoving,"restored outdoor scope resumes sky");
            e.board.State.ClearWorkspace();yield return null;yield return null;
            Check(!w.environment.WorldMoving&&!w.door.GetComponent<Outdoor.LightMotion>().Moving&&
                !lamp.GetComponent<Outdoor.LightMotion>().Moving,"split clears all physical outdoor outputs");
            VitruvianChecks.Say(e.board,"door","move");
            var dark=VitruvianChecks.Say(e.board,"negative","door");
            yield return ChargeDoorChecks.Wait(()=>w.doors.appearance.Brightness<.7f&&w.doors.appearance.Brightness>.3f,"door dims while moving");
            Check(!e.board.State.Unsay(dark.Id),"door appearance command cannot be paused");
            var brightness=w.doors.appearance.Brightness;var position=door.position;
            yield return new WaitForSeconds(.2f);
            Check(w.doors.appearance.Brightness<brightness&&Vector3.Distance(position,door.position)>.01f,
                "door appearance and independent motion both advance");
            yield return ChargeDoorChecks.Wait(()=>e.board.State.Find(dark.Id)==null&&w.doors.appearance.Brightness<.001f,"off command completes");
            var light=VitruvianChecks.Say(e.board,"positive","door");
            yield return ChargeDoorChecks.Wait(()=>e.board.State.Find(light.Id)==null&&w.doors.appearance.Brightness>.999f,"on command completes");
            Check(w.door.GetComponent<Outdoor.LightMotion>().Moving,"appearance command retirement preserves door motion");
            yield return OutdoorStateRegistration(e,w);
            e.board.State.ClearWorkspace();yield return null;
            e.player.MovementLocked=false;
            Result="PASS: separate outdoor outputs, stable rebinding, physical disable/resume, independent coexistence, scope removal/restoration, split, appearance command lock/completion and state-registration isolation during motion";
        }
        static IEnumerator OutdoorStateRegistration(WorldEntry e,Outdoor.OutdoorWorld world)
        {
            var catalog=SentenceCapabilityCatalog.Default;
            var registration=world.door.GetComponents<SentenceEntity>().Single(entity=>ReferenceEquals(entity.Endpoint,world)&&entity.Output(catalog.emission)!=null);
            // A second in-flight door keeps the shared command alive while only this door's
            // state registration is removed. Its separate motion registration stays active.
            var peer=new GameObject("Pending peer door");
            var pending=new SentenceStateOutput(peer.transform,peer,catalog.emission,()=>1,_=>true,_=>true,_=>{},()=>{});
            SentenceEntity.Bind(e.Entities,peer.transform,peer,()=>Space.Outdoor).WithProperty(catalog.door).WithOutput(pending);
            CognitionState.Group dark=null;
            try{
                dark=VitruvianChecks.Say(e.board,"negative","door");
                yield return ChargeDoorChecks.Wait(()=>world.doors.appearance.Brightness<.7f&&world.doors.appearance.Brightness>.3f,"state output starts dimming");
                registration.enabled=false;yield return null;yield return null;
                float held=world.doors.appearance.Brightness;
                var door=world.door.GetComponent<Rigidbody>();var position=door.position;
                yield return new WaitForSeconds(.2f);
                Check(e.sentenceRuntime.HasEffect(dark.Signal)&&e.board.State.Find(dark.Id)!=null,"peer keeps the shared state command active");
                Check(Mathf.Abs(held-world.doors.appearance.Brightness)<.001f,"unregistered state output holds despite the global command");
                Check(Vector3.Distance(position,door.position)>.01f,"holding state output preserves separate motion");
                registration.enabled=true;
                yield return ChargeDoorChecks.Wait(()=>world.doors.appearance.Brightness==0,"state output rejoins and finishes dimming");
            }finally{registration.enabled=true;UnityEngine.Object.Destroy(peer);}
            yield return ChargeDoorChecks.Wait(()=>e.board.State.Find(dark.Id)==null,"shared command retires after the final peer leaves");
        }
        static IEnumerator Controllers(WorldEntry e)
        {
            e.OpenWorld(-1);yield return ChargeDoorChecks.Wait(()=>e.room.Opened&&!e.Busy,"open controller fixture");
            e.sentenceRuntime.presentation.enabledShots=false;e.player.MovementLocked=true;
            yield return EndpointPause(e,e.chamberLight,e.chamberLight.MotionOutput,()=>e.chamberLight.transform.position);
            var j=e.journey;
            foreach(var output in new[]{j.Stars.MotionOutput,j.ReturnStars.MotionOutput,j.Tutorial.MotionOutput,j.FarRoomMotionOutput,j.Ascent.Stars.MotionOutput})
                Check(output!=null&&e.Entities.QueryAll<SentenceMotionOutput>().Contains(output),"Intro controllers retain their registered output");
            e.player.MovementLocked=false;
            yield return VitruvianChecks.Gallery(e);
            VitruvianChecks.Say(e.board,"positive","world");
            yield return ChargeDoorChecks.Wait(()=>e.outdoor.Chapter.box.Opened&&!e.Busy,"open chapter");
            var garden=e.outdoor.Garden;
            e.player.Teleport(garden.roomFill.transform.position,garden.transform.rotation);e.player.MovementLocked=true;
            garden.SetLight(true);yield return ChargeDoorChecks.Wait(()=>garden.Brightness==1,"garden lit");
            garden.lamp.position=garden.roomFill.transform.position+Vector3.up*2+Vector3.right*2;Physics.SyncTransforms();
            yield return EndpointPause(e,garden,garden.MotionOutput,()=>garden.lamp.position);
            var room=e.outdoor.Vitruvian;
            e.player.Teleport(room.transform.TransformPoint(new Vector3(0,.1f,7)),room.transform.rotation);
            room.puzzle.ball.SetOn(true);yield return ChargeDoorChecks.Wait(()=>room.puzzle.ball.IsOn,"orb lit");
            yield return EndpointPause(e,room,room.MotionOutput,()=>room.puzzle.ball.transform.position);
            e.player.MovementLocked=false;
            Result="PASS: Intro bindings, chamber/garden/orb physical motion follows local output; disabling only the endpoint holds motion despite an active global sentence, reenabling resumes";
        }
        static IEnumerator Exercise(WorldEntry e)
        {
            e.OpenWorld(-1);yield return ChargeDoorChecks.Wait(()=>e.room.Opened&&!e.Busy,"open motion fixture");
            e.sentenceRuntime.presentation.enabledShots=false;
            var probe=new GameObject("Registered motion probe");var region=Space.Chamber;bool changes=true;
            var definition=SentenceCapabilityCatalog.Default.position;
            var output=SentenceMotionOutput.Bind(e.Entities,probe.transform,()=>region,definition,m=>m==SentenceMotion.Floor,(motion,g)=>changes);
            try{
                var sentence=VitruvianChecks.Say(e.board,"floor","move");yield return null;yield return null;
                Check(output.HasMotion(SentenceMotion.Floor)&&e.sentenceRuntime.SustainsFeedback(sentence),"a new endpoint participates without a dispatcher type branch");
                var entity = SentenceEntity.Bind(e.Entities, probe.transform, probe.transform, () => region);
                int stops = 0;
                SentenceStateOutput AddCommand()
                {
                    var command = new SentenceStateOutput(probe.transform, probe.transform, definition, () => 0,
                        on => true, on => true, on => {}, () => stops++, () => stops++);
                    entity.WithOutput(command);
                    return command;
                }
                var firstCommand = AddCommand();
                firstCommand.Command(true);
                Check(!output.HasMotion(SentenceMotion.Floor), "a later command registration shares the running actuator");
                var replacement = AddCommand();
                replacement.Command(false);
                firstCommand.Release();
                Check(stops == 0 && !output.HasMotion(SentenceMotion.Floor), "late release cannot stop the new owner");
                replacement.Hold();
                Check(stops == 1 && output.HasMotion(SentenceMotion.Floor), "held command gives the actuator back to the sustained sentence");
                // Simulate stale compatibility data on this Play-only group, then force a fresh delivery.
                typeof(CognitionState.Group).GetProperty("Signal").SetValue(sentence, CognitionSignal.DoorMove);
                typeof(CognitionState.Group).GetProperty("Signals").SetValue(sentence, Array.Empty<CognitionSignal>());
                changes=false;Check(!e.sentenceRuntime.SustainsFeedback(sentence),"endpoint changes its own eligibility");changes=true;
                var same=SentenceMotionOutput.Bind(e.Entities,probe.transform,()=>region,definition,m=>m==SentenceMotion.Floor,(motion,g)=>changes);
                yield return null;Check(ReferenceEquals(same,output)&&e.Entities.QueryAll<SentenceMotionOutput>().Count(o=>ReferenceEquals(o,output))==1,"rebinding keeps one output and its applied intent");
                probe.SetActive(false);yield return null;yield return null;
                Check(!output.HasMotion(SentenceMotion.Floor)&&!e.sentenceRuntime.SustainsFeedback(sentence),"disabled output releases its intent and feedback");
                probe.SetActive(true);yield return null;yield return null;
                Check(output.HasMotion(SentenceMotion.Floor) && !output.HasMotion(SentenceMotion.Door) &&
                    output.Owns(SentenceMotion.Floor, sentence.Established) && e.sentenceRuntime.SustainsFeedback(sentence),
                    "reenabled output, ownership and feedback follow the parsed motion despite stale signals");
                region=Space.Garden;SentenceMotionOutput.Bind(e.Entities,probe.transform,()=>region,definition,m=>m==SentenceMotion.Floor,(g,s)=>true);yield return null;yield return null;
                Check(!output.HasMotion(SentenceMotion.Floor),"out-of-scope output holds");
                e.board.State.ClearWorkspace();yield return null;yield return null;
                Check(e.Entities.QueryAll<SentenceMotionOutput>().All(o=>!o.HasMotion(SentenceMotion.Floor)),"split clears pushed motion across all registered regions");
                Check(e.journey.Platform.MotionOutput!=null&&e.journey.Platform.MotionOutput.Definition==definition,"platform uses the authored position output");
                Result="PASS: dynamic output admission, endpoint eligibility, deduplication, disable/reenable, scope removal, split and platform output binding";
            }finally{UnityEngine.Object.Destroy(probe);}
        }
    }
}
