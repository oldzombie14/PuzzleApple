using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using PuzzleApple.V5.Cognition;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    public static class RoomThreeChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string message){if(ok)return;Result="FAIL: "+message;throw new Exception(Result);}
        static IEnumerator Wait(Func<bool> ready,string label,float seconds=40,Func<string> details=null){
            float end=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;
            if(!ready())Check(false,details==null?label:label+" ("+details()+")");
        }
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Entrance(e));}
        public static void RunLightEligibility(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(LightEligibility(e));}
        static IEnumerator SampleCube(DriftingLight lamp,bool moving,string label){
            var body=lamp.GetComponent<Rigidbody>();
            body.position=lamp.journey.Root.TransformPoint(new Vector3(0,5,lamp.journey.RoomStart+6));
            Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.06f);
            var start=body.position;yield return new WaitForSecondsRealtime(.2f);
            Check(moving?Vector3.Distance(start,body.position)>.02f:Vector3.Distance(start,body.position)<.001f,label);
        }
        static IEnumerator LightEligibility(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey;var b=e.board;var s=b.State;var rt=e.sentenceRuntime;
            rt.presentation.enabledShots=false;b.Panel.SetOpen(false);e.player.MovementLocked=true;
            // Learn and use move before Room 3 is unlocked, as in the normal route.
            var earlier=VitruvianChecks.Say(b,"light","move");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"unlock tutorial");
            yield return ChargeDoorChecks.UnlockEntrance(e);
            e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart+3)),j.Root.rotation);yield return null;
            var lamp=j.Lamps[0];Check(j.Brightness==0,"Room 3 stays dark");
            yield return SampleCube(lamp,true,"dark cube keeps its own motion while global light move exists");
            var detached=s.Detach(earlier.Id,earlier.Words.Last().Id);
            yield return SampleCube(lamp,true,"splitting light move does not stop a dark cube");
            Check(!j.FarRoomMotionOutput.ControlsLamps,"dark cubes were not recorded as light-motion targets");
            s.ReturnToLibrary(detached.Id,0,true);s.ReturnToLibrary(earlier.Id,0,true);
            j.SetLights(false,PassageJourney.LightSpace.All);
            yield return Wait(()=>!j.Lights(PassageJourney.LightSpace.All).Any(l=>l.WouldChange(false)),"all reachable lights off");
            var freeLamps=j.CaptureState();
            foreach(var target in new[]{"door","floor","light"}){
                var directed=VitruvianChecks.Say(b,"light","move",target);
                yield return SampleCube(lamp,true,"dark cube ignores light move "+target);
                if(target=="door")Check(j.Lamps.All(l=>!l.Docked) && j.EndingDoor.DockedEnergy==0,"waiting for light leaves dark lamps undocked and supplies no power");
                Check(!directed.Spoken && !s.JudgementLocked,"no lit subject leaves a draft");
                var relight=VitruvianChecks.Say(b,"positive","light");
                yield return Wait(()=>s.Find(relight.Id)==null && s.CanPrompt(directed),"relight restores the prompt");
                Check(!directed.Spoken && s.Speak(directed.Id),"explicit retry after relighting");
                yield return Wait(()=>s.Find(directed.Id)==null,"relit directed movement completes: "+target,65);
                var darken=VitruvianChecks.Say(b,"negative","light");
                yield return Wait(()=>s.Find(darken.Id)==null,"darken before next sample");
                Check(j.TryRestoreState(freeLamps),"restore free cubes for an independent dark sample");
            }
            var freeze=VitruvianChecks.Say(b,"negative","move");
            yield return SampleCube(lamp,false,"no move still freezes dark cubes");s.ReturnToLibrary(freeze.Id,0,true);
            yield return SampleCube(lamp,true,"dark cube resumes after no move");
            var move=VitruvianChecks.Say(b,"light","move");
            var on=VitruvianChecks.Say(b,"positive","light");yield return Wait(()=>j.Brightness==1 && s.Find(on.Id)==null,"light cubes with active move");
            yield return SampleCube(lamp,true,"lit cube follows light move");
            detached=s.Detach(move.Id,move.Words.Last().Id);
            yield return SampleCube(lamp,false,"splitting light move pauses a lit cube");
            var off=VitruvianChecks.Say(b,"negative","light");yield return Wait(()=>j.Brightness==0 && s.Find(off.Id)==null,"extinguish controlled cubes");
            yield return SampleCube(lamp,true,"dark cube is no longer held by the removed light move");
            on=VitruvianChecks.Say(b,"positive","light");yield return Wait(()=>j.Brightness==1 && s.Find(on.Id)==null,"relight controlled cubes");
            yield return SampleCube(lamp,false,"relighting preserves the paused light-motion state");
            var heading=lamp.direction;s.JoinAndSpeak(detached.Id,move.Id,false);
            yield return SampleCube(lamp,true,"rejoined light move resumes the lit cube");
            Check(Vector3.Dot(heading,lamp.direction)>.99f,"resume preserves cube direction");
            Result="PASS: dark cubes ignore light motion and splitting, directed dark commands do not park them, no move still freezes, lit split/relight/resume preserves state";
        }
        public static void RunFirstReveal(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(FirstReveal(e));}
        static IEnumerator FirstReveal(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey;var b=e.board;var s=b.State;var p=e.player;var d=j.EndingDoor;
            e.sentenceRuntime.presentation.enabledShots=false;b.Panel.SetOpen(false);p.MovementLocked=true;
            Check(j.Brightness==0 && d.LightHeld && d.DoorGlow==1 && !d.LessonAcquired,"uncollected door starts lit in a dark room");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            var first=VitruvianChecks.Say(b,"positive","door");
            yield return Wait(()=>s.Find(first.Id)==null && j.DoorProgress==1,"first on door finishes");
            Check(j.EntranceDoor.Progress==0,"first on door must not chain into the newly unlocked entrance");
            var on=VitruvianChecks.Say(b,"positive","light");yield return Wait(()=>s.Find(on.Id)==null,"light unlocked early rooms");
            Check(j.Brightness==0,"room three remains dark behind its entrance lock");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart-3)),j.Root.rotation);yield return null;
            e.sentenceRuntime.presentation.enabledShots=true;
            var entrance=VitruvianChecks.Say(b,"positive","door");
            yield return Wait(()=>j.EntranceDoor.Progress==1 && s.Find(entrance.Id)==null && !e.sentenceRuntime.presentation.Playing,"open room three through the actual sentence presentation");
            Check(j.Brightness==0 && d.LightHeld && d.DoorGlow==1 && !d.LessonAcquired,"first reveal has dark lamps and the world-held glowing door");
            yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
            VitruvianChecks.Capture(p,p.view.position,d.Leaf.position,"first-entrance-reveal");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart+2)),j.Root.rotation);p.view.LookAt(d.Leaf.position);p.AdoptCurrentView();
            yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
            VitruvianChecks.Capture(p,p.view.position,d.Leaf.position,"first-dark-room");
            p.Teleport(d.transform.TransformPoint(new Vector3(0,.1f,-6)),d.transform.rotation);p.view.LookAt(d.Leaf.position);p.AdoptCurrentView();
            yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
            VitruvianChecks.Capture(p,p.view.position,d.Leaf.position,"first-door-near");
            e.sentenceRuntime.presentation.enabledShots=false;
            p.Teleport(d.RoomLanding,d.transform.rotation);p.view.LookAt(d.Leaf.position);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;yield return null;
            Check(e.chamberLight.TryCollect() && b.Panel.IsOpen && !e.chamberLight.collectionSymbol.gameObject.activeSelf,"first click grants the active lesson directly");
            yield return Wait(()=>b.Panel.FullyOpen && d.LessonAcquired,"lesson acquired");
            Check(d.LightHeld && d.DoorGlow==1 && j.Brightness==0,"lesson ownership preserves the initial light and dark room");
            b.Panel.SetOpen(false);yield return new WaitForSecondsRealtime(.5f);
            p.Teleport(d.transform.TransformPoint(new Vector3(0,.1f,-6)),d.transform.rotation);p.view.LookAt(d.Leaf.position);p.AdoptCurrentView();
            yield return new WaitForSecondsRealtime(1.5f);yield return new WaitForEndOfFrame();
            VitruvianChecks.Capture(p,p.view.position,d.Leaf.position,"door-after-lesson");
            on=VitruvianChecks.Say(b,"positive","light");yield return Wait(()=>s.Find(on.Id)==null && j.Brightness==1,"new command lights the now-unlocked room");
            yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
            VitruvianChecks.Capture(p,p.view.position,d.Leaf.position,"door-after-on-light");
            Result="PASS: staged opening without chained unlocks, dark initial room with glowing door, seamless first-click lesson, newly issued light command reaches unlocked room";
        }
        public static void RunCrowdedGather(){
            Result="Running";
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            e.StartCoroutine(CrowdedGather(e));
        }

        static IEnumerator CrowdedGather(WorldEntry e){
            yield return Wait(()=>e.Entered,"enter P5");
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            var j=e.journey;
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"connect tutorial room");
            j.SetDoor(false);yield return Wait(()=>j.DoorProgress<=.7f,"partly close the white door");j.WhiteDoor.Hold();
            j.Stars.SetOn(false);yield return Wait(()=>!j.Stars.Settling(false),"isolate tutorial lights");
            e.board.Panel.SetOpen(false);
            e.sentenceRuntime.presentation.enabledShots=false;
            j.SetLights(true,Space.SphereRoom);yield return Wait(()=>j.Tutorial.SphereBrightness==1,"light tutorial room");

            // Local offsets from the white doorway in a P5 run that timed out with only SphereRoom pending.
            var offsets=new[]{
                new Vector3(-0.4198571f, 2.3301756f, 4.4272861f),
                new Vector3(0.8799707f, 1.1822770f, 1.1479168f),
                new Vector3(-0.7646734f, 0.7014748f, 1.7187748f),
                new Vector3(-0.5999470f, 1.4231985f, 0.9875507f),
                new Vector3(0.0570030f, 0.5003150f, 2.0876751f),
                new Vector3(0.1617737f, 0.6470161f, 0.9212971f),
                new Vector3(-0.9081346f, 0.5322835f, 0.6224155f),
                new Vector3(1.0595796f, 0.4135857f, 0.5379925f),
                new Vector3(-1.8931544f, 0.7094813f, 1.2204494f),
                new Vector3(-0.0614929f, 1.5819073f, 1.8327045f),
                new Vector3(0.6454774f, 0.9160789f, 1.8009510f),
            };
            var source=(ISentenceOutputState<MoveTutorialRoom.MotionState>)j.Tutorial;
            var crowded=source.CaptureState();
            Check(crowded.lamps.Length==offsets.Length,"P5 tutorial lamp layout");
            for(int i=0;i<offsets.Length;i++){
                crowded.lamps[i].position=Vector3.forward*j.pathLength+offsets[i];
                crowded.lamps[i].parked=true;
            }
            Check(j.Tutorial.TryRestoreState(crowded),"restore crowded door approach");
            Physics.SyncTransforms();
            var board=e.board;var gather=VitruvianChecks.Say(board,"light","move","door");
            Check(gather.ExecutionLocked,"crowded gather starts");
            yield return Wait(()=>board.State.Find(gather.Id)==null,"blocked spheres stop holding the judgement",10);
            Check(!board.State.JudgementLocked,"release judgement after the remaining spheres are obstructed");

            // Free one approach and verify the next command can still move a sphere.
            crowded=source.CaptureState();
            int last=crowded.lamps.Length-1;
            var start=crowded.lamps[last].position+new Vector3(3,2,3);
            crowded.lamps[last].position=start;
            Check(j.Tutorial.TryRestoreState(crowded),"restore free approach");
            Physics.SyncTransforms();
            var retry=VitruvianChecks.Say(board,"light","move","door");
            Check(retry.ExecutionLocked,"a fresh period starts gathering again");
            yield return Wait(()=>Vector3.Distance(start,source.CaptureState().lamps[last].position)>.5f,"unobstructed lamp advances on retry");
            yield return Wait(()=>board.State.Find(retry.Id)==null,"retry finishes",15);
            Result="PASS: crowded tutorial lamps release judgement, a fresh command advances a free lamp and retires";
        }

        static IEnumerator BlockedSlotAndOpen(WorldEntry e){
            var j=e.journey;var board=e.board;var state=board.State;
            var snapshot=j.CaptureState();
            int index=Array.FindIndex(snapshot.lamps,l=>l.targetsEntrance);
            Check(index>=0,"an entrance lamp can be obstructed without removing power from the slab");
            var lamp=snapshot.lamps[index];
            var obstacle=new GameObject("Occupied docking slot",typeof(BoxCollider));
            try{
                obstacle.transform.SetPositionAndRotation(lamp.doorTarget,j.Root.rotation);
                obstacle.transform.localScale=Vector3.one*1.4f;
                lamp.docked=false;lamp.pose.parked=true;
                lamp.pose.position=lamp.doorTarget+j.Root.forward*2.5f;
                snapshot.lamps[index]=lamp;
                Check(j.TryRestoreState(snapshot),"place the last undocked lamp on its approach");
                Physics.SyncTransforms();
                board.Panel.SetOpen(true);
                yield return Wait(()=>board.Panel.FullyOpen,"open period tray");
                var gather=VitruvianChecks.Say(board,"light","move","door");
                var draft=PeriodChecks.Draft(board,"positive","door");
                Check(gather.ExecutionLocked && !state.CanPrompt(draft),"active gathering suppresses the open-door prompt");
                yield return Wait(()=>state.Find(gather.Id)==null,"obstructed approach releases the judgement lock",12);
                var cube=j.Lamps.Single(l=>l.SlotIndex==lamp.slot);
                Check(!cube.Docked && j.EndingDoor.Charged,"the obstructed lamp stays undocked while the slab retains its actual charge");
                yield return Wait(()=>board.Surface.GetComponentsInChildren<PeriodGlyph>().Any(p=>p.group==draft.Id && p.ring),"the existing draft regains its visible period prompt");
                PeriodChecks.Click(board,draft.Id);
                yield return Wait(()=>j.EndingDoor.Lift==1 && state.Find(draft.Id)==null,"clicking the original draft opens the charged door");
                obstacle.SetActive(false);
                Physics.SyncTransforms();
                var stars=((ISentenceOutputState<CorridorStars.FlightState>)j.Stars).CaptureState();
                stars.stars[0].position.z=0;
                Check(j.Stars.TryRestoreState(stars) && j.Stars.NeedsGather,"remote star has a full corridor to travel");
                var retry=VitruvianChecks.Say(board,"light","move","door");
                Check(retry.ExecutionLocked,"a new gather retries the freed slot");
                var fading=board.GroupRect(retry.Id).GetComponent<CanvasGroup>();
                yield return Wait(()=>cube.Docked,"local lamp docks after clearance");
                Check(j.Stars.NeedsGather && state.JudgementLocked,"remote star travelled at normal speed during the local approach");
                double dockedAt=Time.timeAsDouble;
                yield return Wait(()=>state.Find(retry.Id)==null,"remote gathering window finishes");
                double tail=Time.timeAsDouble-dockedAt;
                Check(tail>=.5-Time.deltaTime && tail<.5+3*Time.maximumDeltaTime && !j.Stars.NeedsGather,
                    "remote star reaches its slot in the half-second window, elapsed="+tail);
                Check(!state.JudgementLocked && fading && !fading.blocksRaycasts,"visual fade releases judgement and pointer input");
                yield return Wait(()=>fading && fading.alpha<1 && fading.alpha>0,"completed sentence fades visibly");
            }finally{
                board.Panel.SetOpen(false);
                UnityEngine.Object.Destroy(obstacle);
            }
        }

        public static void RunGlobalDoors(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(GlobalDoors(e));}
        static IEnumerator GlobalDoors(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey;var b=e.board;var s=b.State;var p=e.player;var rt=e.sentenceRuntime;
            var entrance=j.EntranceDoor;var charge=j.EndingDoor;
            rt.presentation.enabledShots=false;b.Panel.SetOpen(false);p.MovementLocked=true;
            var locked=VitruvianChecks.Say(b,"positive","door");
            Check(s.Find(locked.Id)==locked && !locked.Spoken && !s.JudgementLocked,"unreachable doors leave an editable draft");
            Check(j.DoorProgress==0 && entrance.Progress==0 && charge.Lift==0,"locked spaces receive no door command");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"unlock room two");
            entrance.SetOpen(true);yield return Wait(()=>entrance.Progress==1,"unlock room three");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart+3)),j.Root.rotation*Quaternion.Euler(0,180,0));yield return null;
            var original=j.Door.parent.Find("Round handle").GetComponent<Renderer>();
            var handle=entrance.transform.Find("Round handle").GetComponent<Renderer>();
            var a=new MaterialPropertyBlock();var c=new MaterialPropertyBlock();original.GetPropertyBlock(a);handle.GetPropertyBlock(c);
            Check(handle.sharedMaterial==original.sharedMaterial && c.GetFloat("_RoundOutline")==a.GetFloat("_RoundOutline") && c.GetFloat("_RoundOutline")==1 && c.GetFloat("_CorridorPureWhite")==a.GetFloat("_CorridorPureWhite"),"room three handle copies room two material and runtime shading");
            rt.presentation.enabledShots=true;var beforeClose=p.view.rotation;
            var close=VitruvianChecks.Say(b,"negative","door");
            yield return Wait(()=>rt.presentation.Playing && rt.presentation.CanInterrupt,"global close presentation begins");
            Check(Quaternion.Angle(p.view.rotation,beforeClose)<.1f,"off door preserves the view");
            yield return Wait(()=>entrance.Progress<.8f && j.DoorProgress<.8f,"room-side off door closes both eligible doors");
            Check(charge.Lift==0 && !charge.Charged,"unpowered charge door does not block the other doors");
            yield return Wait(()=>entrance.Progress==0 && j.DoorProgress==0 && s.Find(close.Id)==null,"global close waits for both doors and retires");
            rt.presentation.enabledShots=false;
            Check((j.SentenceScope()&Space.FarRoom)!=0,"closing retains unlocked room three");
            yield return new WaitForEndOfFrame();
            VitruvianChecks.Capture(p,handle.bounds.center+entrance.transform.TransformDirection(new Vector3(1,.25f,-1.6f)),handle.bounds.center,"room-three-handle");
            var open=VitruvianChecks.Say(b,"positive","door");
            yield return Wait(()=>entrance.Progress==1 && s.Find(open.Id)==null,"on door opens the boundary from room three");
            Check(j.DoorProgress==0,"the closed entrance excludes the white door at command start");
            open=VitruvianChecks.Say(b,"positive","door");
            yield return Wait(()=>j.DoorProgress==1 && s.Find(open.Id)==null,"a new command reaches the reconnected white door");
            var cycle=VitruvianChecks.Say(b,"door","move");
            yield return Wait(()=>entrance.Progress<.7f && j.DoorProgress<.7f,"door move reaches both unlocked hinged doors");
            s.ReturnToLibrary(cycle.Id,0,true);yield return null;yield return null;
            float white=j.DoorProgress,entry=entrance.Progress;yield return new WaitForSeconds(.2f);
            Check(j.DoorProgress==white && entrance.Progress==entry,"splitting global door motion holds every door");
            var on=VitruvianChecks.Say(b,"positive","light");yield return Wait(()=>s.Find(on.Id)==null && j.Brightness==1,"light all unlocked spaces");
            var gather=VitruvianChecks.Say(b,"light","move","door");yield return null;yield return null;
            Check(rt.HasEffect(CognitionSignal.LightDoorMove,Space.FirstPath) && rt.HasEffect(CognitionSignal.LightDoorMove,Space.SphereRoom) && rt.HasEffect(CognitionSignal.LightDoorMove,Space.FarRoom),"directed light command covers every eligible space, independently of camera focus");
            float started=Time.time;
            string PendingGather(){
                var scope=rt.EffectScope(gather);
                var outputs=e.Entities.Query<SentenceMotionOutput>(scope).Where(o=>o.WouldChange(gather,scope) && o.Owns(SentenceMotion.LightToDoor,gather.Established));
                var remaining=j.CaptureState().lamps.Where(l=>!l.docked).Select(l=>l.slot+" at "+j.Root.InverseTransformPoint(l.pose.position)+" to "+j.Root.InverseTransformPoint(l.doorTarget));
                return "simulated="+(Time.time-started)+", command active="+(s.Find(gather.Id)!=null)+", charge="+charge.TotalEnergy+
                    ", stars pending="+j.Stars.NeedsGather+", outputs="+string.Join(", ",outputs.Select(o=>e.Entities.SpacesOf(o)))+
                    ", undocked="+string.Join(", ",remaining);
            }
            yield return Wait(()=>j.Lamps.All(l=>l.Docked) && !j.Stars.NeedsGather && charge.Charged && s.Find(gather.Id)==null,"all local gathering targets finish before command retirement",90,PendingGather);
            yield return BlockedSlotAndOpen(e);
            Check(charge.Lift==1 && entrance.Progress==1 && j.DoorProgress==1,"charged slab and both hinged doors all open");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart-.23f)),j.Root.rotation);yield return null;
            close=VitruvianChecks.Say(b,"negative","door");
            yield return Wait(()=>charge.Lift==0 && j.DoorProgress==0,"other doors finish while entrance is occupied");
            Check(entrance.Progress==1 && s.Find(close.Id)!=null,"occupied entrance keeps the shared command alive");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart+3)),j.Root.rotation*Quaternion.Euler(0,180,0));
            yield return Wait(()=>entrance.Progress==0 && s.Find(close.Id)==null,"entrance closes from room side after doorway clears");
            cycle=VitruvianChecks.Say(b,"door","move");
            yield return Wait(()=>charge.Lift>.2f && entrance.Progress>.2f && j.DoorProgress>.2f,"all three powered doors cycle together");
            s.ReturnToLibrary(cycle.Id,0,true);yield return null;yield return null;
            white=j.DoorProgress;entry=entrance.Progress;float slab=charge.Lift;yield return new WaitForSeconds(.2f);
            Check(white==j.DoorProgress && entry==entrance.Progress && slab==charge.Lift,"splitting holds all three doors");
            Result="PASS: global door on/off/move, room-side entrance closure, locks retained, unpowered exclusion, multi-target gathering/retirement, doorway guard and matching handle material";
        }
        public static void RunAppearance(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Appearance(e));}
        static IEnumerator Appearance(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey;var p=e.player;var door=j.EndingDoor;
            e.sentenceRuntime.presentation.enabledShots=false;e.board.Panel.SetOpen(false);p.MovementLocked=true;
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"unlock white door");
            yield return ChargeDoorChecks.UnlockEntrance(e);
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart+2)),j.Root.rotation);
            p.view.LookAt(door.Leaf.position);p.AdoptCurrentView();yield return new WaitForSecondsRealtime(1.5f);
            var doorInk=new MaterialPropertyBlock();door.Leaf.GetComponent<Renderer>().GetPropertyBlock(doorInk);
            Check(doorInk.GetFloat("_ChargeDoorEmission")>Mathf.GammaToLinearSpace(1.1f) && door.NicheBloomWeight(p.view)<.3f,"initial door emits above bloom threshold while local bloom stays restrained");
            yield return new WaitForEndOfFrame();VitruvianChecks.Capture(p,p.view.position,door.Leaf.position,"door-dark-entry");
            p.Teleport(door.transform.TransformPoint(new Vector3(0,.1f,-6)),door.transform.rotation);
            p.view.LookAt(door.Leaf.position);p.AdoptCurrentView();yield return new WaitForSecondsRealtime(1);
            yield return new WaitForEndOfFrame();VitruvianChecks.Capture(p,p.view.position,door.Leaf.position,"door-dark-near");
            j.SetLights(true,Space.FarRoom);yield return Wait(()=>j.Brightness==1,"room lamps on");
            yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();
            VitruvianChecks.Capture(p,p.view.position,door.Leaf.position,"door-lit");
            VitruvianChecks.Say(e.board,"light","light");
            VitruvianChecks.Say(e.board,"light","move","door");
            yield return Wait(()=>door.Charged,"fully charge door for inscription review");
            yield return new WaitForSecondsRealtime(1.7f);yield return new WaitForEndOfFrame();
            VitruvianChecks.Capture(p,p.view.position,door.Leaf.position,"door-charged");
            e.board.State.ClearWorkspace();
            var rails=door.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Charge fill").ToArray();
            Check(rails.Any(t=>Mathf.Abs(t.localPosition.x)-t.localScale.x*.5f>=door.MouthWidth*.5f+.17f),"charge rail retains visible separation from frame");
            yield return Wait(()=>e.outdoor.Prepared,"outdoor prepared");e.outdoor.ArriveFromAscent();
            yield return Wait(()=>e.outdoor.IsOutside&&!e.outdoor.Transitioning,"outdoor arrival");
            var world=e.outdoor.World;var rotation=world.door.transform.rotation;
            Check(Vector3.Angle(p.view.forward,Vector3.ProjectOnPlane(world.chapterFrame.position-p.transform.position,Vector3.up))>20,"arrival does not centre the next chapter door");
            float low=world.IntroWord.transform.localPosition.y,high=low;
            for(float t=0;t<6.5f;t+=Time.deltaTime){
                float y=world.IntroWord.transform.localPosition.y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);yield return null;
            }
            Check(high-low>.2f && high-low<.25f,"doorway symbol gently floats within a small vertical range");
            world.IntroWord.SetCompleted(true);
            var target=world.IntroWord.GetComponent<Renderer>().bounds.center;
            foreach(float side in new[]{-8f,8f}){
                p.Teleport(world.door.transform.TransformPoint(new Vector3(side,.1f,-8)),world.door.transform.rotation);
                yield return new WaitForSecondsRealtime(.3f);
                Check(Quaternion.Angle(rotation,world.door.transform.rotation)<.01f,"outdoor door orientation stays fixed as player moves");
            }
            p.Teleport(world.door.transform.TransformPoint(new Vector3(0,.1f,-9)),world.door.transform.rotation);
            p.view.LookAt(target);p.AdoptCurrentView();yield return new WaitForSecondsRealtime(2);
            Check(world.IntroWord.emission<=1,"outdoor core word emission stays readable");
            yield return new WaitForEndOfFrame();VitruvianChecks.Capture(p,p.view.position,target,"outdoor-door-symbol");
            Result="PASS: fixed outdoor orientation, restrained symbol emission, dark/lit charge-door views and separated charge rails";
        }
        static IEnumerator Entrance(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey;var d=j.EntranceDoor;var p=e.player;var b=e.board;var s=b.State;var rt=e.sentenceRuntime;
            b.Panel.SetOpen(false);rt.presentation.enabledShots=false;p.MovementLocked=true;
            Check(d && d.Progress==0 && d.transform.localPosition.x>0 && d.Leaf.localPosition.x<0 && d.transform.localScale.x==1,"right hinge mirrors original door with positive collider scale");
            Check(!j.LightsOn && j.Brightness==0 && j.EndingDoor.DoorGlow==1,"dark room has only the charge door glow");
            Check(Mathf.Abs(j.EndingDoor.TotalEnergy-.15f)<.001f,"initial energy is .15 before entrance unlock");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"white door unlocks room two");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart-5)),j.Root.rotation);yield return null;
            Check(j.InReturnPath && (j.SentenceScope()&Space.FarRoom)==0,"closed entrance excludes room three");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart-1)),j.Root.rotation);Physics.SyncTransforms();
            p.GetComponent<CharacterController>().Move(j.Root.forward*2);
            Check(j.Root.InverseTransformPoint(p.transform.position).z<j.RoomStart-.2f,"native character collision blocks the closed entrance");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart-5)),j.Root.rotation);yield return null;
            var on=VitruvianChecks.Say(b,"positive","light");var move=VitruvianChecks.Say(b,"light","move");
            yield return Wait(()=>s.Find(on.Id)==null,"light command finishes before entrance");
            Check(j.Brightness==0 && !rt.HasEffect(CognitionSignal.LightMove,Space.FarRoom),"closed gate blocks light commands and motion");
            s.ClearWorkspace();
            p.Teleport(j.Tutorial.Fill.transform.position-Vector3.up*4,j.Root.rotation);yield return null;
            var nearest=VitruvianChecks.Say(b,"positive","door");
            Check((rt.EffectScope(nearest)&(Space.FirstPath|Space.ReturnPath))==(Space.FirstPath|Space.ReturnPath),"room two controls both unlocked hinged doors");
            yield return Wait(()=>s.Find(nearest.Id)==null,"room-two opening finishes before testing the next opening");
            d.SetOpen(false);yield return Wait(()=>d.Progress==0,"reset entrance angle for camera check");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart-5)),j.Root.rotation);yield return null;
            rt.presentation.enabledShots=true;var position=p.transform.position;
            var open=VitruvianChecks.Say(b,"positive","door");
            yield return Wait(()=>rt.presentation.Playing,"entrance camera begins");
            yield return Wait(()=>!rt.presentation.Playing && d.Progress==1 && s.Find(open.Id)==null,"entrance shot finishes after opening");
            Check(Vector3.Distance(position,p.transform.position)<.05f,"entrance cutscene preserves player position");
            Check(j.Brightness==0 && j.EndingDoor.DoorGlow==1 && (j.SentenceScope()&Space.FarRoom)!=0,"opening reveals dark room and unlocks room three");
            Check(Mathf.DeltaAngle(0,d.transform.localEulerAngles.y)<-134,"entrance rotates opposite the first white door");
            if(!string.IsNullOrEmpty(UnityEditor.SessionState.GetString("C2C.captureFolder","")))
                VitruvianChecks.Capture(p,p.view.position,j.EndingDoor.Leaf.position,"room-three-first-reveal");
            rt.presentation.enabledShots=false;
            var close=VitruvianChecks.Say(b,"negative","door");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart-.23f)),j.Root.rotation);yield return null;
            float held=d.Progress;yield return new WaitForSeconds(.4f);
            Check(Mathf.Abs(d.Progress-held)<.001f,"entrance closing waits for an occupied doorway");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart-5)),j.Root.rotation);
            yield return Wait(()=>d.Progress==0 && s.Find(close.Id)==null,"closing resumes after player leaves");
            var gate=d.GetComponentsInChildren<ProgressLock>().Single(boundary=>boundary.BoundarySource==d);
            Check(gate.Unlocked && !gate.Connected && (j.SentenceScope()&Space.FarRoom)==0,
                "closed entrance retains progress and separates room three from the corridor");
            d.SetOpen(true);yield return Wait(()=>d.Progress>.3f,"partial entrance opening");d.Hold();held=d.Progress;
            p.Teleport(d.Leaf.position-Vector3.up*(d.Leaf.localScale.y*.5f-.08f),j.Root.rotation);yield return null;yield return null;
            Check(Physics.GetIgnoreCollision(d.Leaf.GetComponent<Collider>(),p.GetComponent<CharacterController>()) && d.Progress==held,"paused overlapping leaf waits before restoring collision");
            p.Teleport(j.Root.TransformPoint(new Vector3(.5f,.08f,j.RoomStart-1.6f)),j.Root.rotation);yield return null;
            position=p.transform.position;d.SetOpen(true);yield return Wait(()=>d.Progress==1,"opening passes through the player");
            Check(Vector3.Distance(position,p.transform.position)<.05f,"opening leaf does not push the player");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart-5)),j.Root.rotation);yield return null;yield return null;
            Check(!Physics.GetIgnoreCollision(d.Leaf.GetComponent<Collider>(),p.GetComponent<CharacterController>()),"door collision restores once clear");
            var cycle=VitruvianChecks.Say(b,"door","move");yield return Wait(()=>d.Progress>.3f&&d.Progress<.7f,"entrance cycle begins");
            var token=s.Detach(cycle.Id,cycle.Words.Last().Id);held=d.Progress;yield return new WaitForSeconds(.3f);
            Check(Mathf.Abs(held-d.Progress)<.001f,"split entrance cycle holds its angle");
            var hinge=d.MotionOutput.StateSource<HingeState>();
            Check(hinge!=null,"hinge motion exposes its physical state");
            var saved=JsonUtility.FromJson<HingeState>(JsonUtility.ToJson(hinge.CaptureState()));
            var opposite=saved;
            opposite.cycleOpening=!saved.cycleOpening;
            Check(hinge.TryRestoreState(opposite)&&hinge.TryRestoreState(saved) &&
                hinge.CaptureState().cycleOpening==saved.cycleOpening,"restore held swing direction");
            s.JoinAndSpeak(token.Id,cycle.Id,false);yield return Wait(()=>Mathf.Abs(held-d.Progress)>.1f,"rejoin resumes entrance motion");
            Check((d.Progress>held)==saved.cycleOpening,"rejoined motion follows the restored direction");
            var forced = VitruvianChecks.Say(b,"positive","door");
            yield return Wait(() => d.Driving && !d.MotionOutput.HasMotion(SentenceMotion.Door), "command takes the shared hinge");
            float previous = d.Progress;
            float peak = previous;
            bool released = false;
            float deadline = Time.realtimeSinceStartup + 35;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                float current = d.Progress;
                Check(Mathf.Abs(current - previous) <= Time.deltaTime / Mathf.Max(.01f, j.doorDuration) + .002f,
                    "handoff never jumps back to the angle before the command");
                peak = Mathf.Max(peak, current);
                released |= s.Find(forced.Id) == null && d.MotionOutput.HasMotion(SentenceMotion.Door);
                if (released && current < previous && current < .8f) break;
                previous = current;
            }
            Check(released && peak > .997f && d.Progress < .8f, "cycle closes from the fully opened hinge");

            yield return Wait(() => d.Progress < .5f && d.Progress > .22f, "closing leg before second command");
            float closing = d.Progress;
            var secondOpen = VitruvianChecks.Say(b, "positive", "door");
            yield return Wait(() => d.Driving && d.Progress > closing + .08f && d.Progress < .98f, "command owns partly opened door");
            Check(!s.Unsay(secondOpen.Id) && s.Detach(secondOpen.Id,secondOpen.Words.Last().Id)==null, "partial opening refuses period removal and split");
            b.RemoveToken(secondOpen.Id,secondOpen.Words[0].Id);
            b.RemoveToken(secondOpen.Id,0,true);
            Check(secondOpen.ExecutionLocked && s.Find(secondOpen.Id)==secondOpen && secondOpen.Words.Count==2,"right-click leaves the command intact");
            yield return Wait(() => d.Progress==1, "command reaches full opening despite edit attempts");
            yield return Wait(() => s.Find(secondOpen.Id)==null && d.MotionOutput.HasMotion(SentenceMotion.Door) && d.Progress<.8f,
                "sustained cycle resumes closing after command retires");
            s.ClearWorkspace();held=d.Progress;
            p.Teleport(j.EndingDoor.RoomLanding,j.EndingDoor.transform.rotation);yield return null;
            var far=VitruvianChecks.Say(b,"positive","door");yield return Wait(()=>s.Find(far.Id)==null,"all eligible doors finish despite unpowered charge door");
            Check(d.Progress==1 && j.DoorProgress==1 && j.EndingDoor.Lift==0 && rt.EffectScope(far)==rt.Scope(far),"room three also opens earlier doors without bypassing charge requirements");held=d.Progress;
            e.RequestExit();yield return Wait(()=>e.DialogReady,"close world preserves entrance");
            Check(d.Progress==held,"entrance angle survives closure");e.CancelExit();e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"restore world");
            Check(d.Progress==held && (j.SentenceScope()&Space.FarRoom)!=0,"angle and unlock survive return");
            Result="PASS: right mirrored entrance, global door scope, fixed camera dark reveal, collision guards, split/resume, command priority and retained state";
        }
        public static IEnumerator Energy(WorldEntry e){
            Result="Running";e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey;var d=j.EndingDoor;var p=e.player;var b=e.board;var s=b.State;var rt=e.sentenceRuntime;
            Check(!j.LightsOn && j.Brightness==0 && Mathf.Abs(d.TotalEnergy-.15f)<.001f,"initial dark room and .15 energy");
            rt.presentation.enabledShots=false;b.Panel.SetOpen(false);
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"white progress");yield return ChargeDoorChecks.UnlockEntrance(e);
            p.Teleport(d.RoomLanding,d.transform.rotation);yield return null;
            p.view.LookAt(d.Leaf.position);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;yield return null;
            int strong=e.feedback.StrongCount,weak=e.feedback.WeakCount,groups=s.Groups.Count;
            void ClickDoor(){
                e.chamberLight.AdvancePointer(true,true,false,.02f);
                e.chamberLight.AdvancePointer(false,false,true,.02f);
            }
            ClickDoor();
            Check(b.Panel.IsOpen && !e.chamberLight.collectionSymbol.gameObject.activeSelf,"first door click opens the panel directly without a word popup");
            float minimum=1;
            while(e.chamberLight.Collecting || !b.Panel.FullyOpen){minimum=Mathf.Min(minimum,d.DoorGlow);yield return null;}
            yield return null;yield return null;
            var owned=s.Groups.Single(g=>g.RuleId=="light.door");
            Check(owned.Spoken && owned.Effective && s.Groups.Count==groups+1 && d.LessonAcquired,"acquired lesson is spoken with period");
            Check(minimum>.999f && d.DoorGlow==1 && e.feedback.StrongCount==strong+1 && e.feedback.WeakCount==weak,"ownership transfer is seamless and has one feedback");
            b.Panel.SetOpen(false);yield return new WaitForSeconds(.4f);p.view.LookAt(d.Leaf.position);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;yield return null;
            ClickDoor();
            Check(e.chamberLight.KnownFeedbackWord=="light door" && s.Groups.Count==groups+1,"lit repeat click shows the two words without granting another lesson");
            var prefix=e.chamberLight.hud.GetComponentsInChildren<UnityEngine.UI.Image>().Single(i=>i.name=="Known phrase prefix");
            var noun=prefix.transform.parent.GetComponent<UnityEngine.UI.Image>();
            Check(prefix.sprite==b.Catalog.Word("light").Symbol && noun.sprite==b.Catalog.Word("door").Symbol && prefix.rectTransform.position.x<noun.rectTransform.position.x,"known phrase renders light to the left of door");
            yield return Wait(()=>!e.chamberLight.ShowingKnown,"lit feedback completes");
            s.ReturnToLibrary(owned.Id,0,true);
            yield return Wait(()=>d.DoorGlow>0 && d.DoorGlow<1,"charge door starts dimming");
            ClickDoor();
            Check(e.chamberLight.KnownFeedbackWord=="light door","fading slab still shows the pair");
            yield return Wait(()=>d.DoorGlow==0 && Mathf.Abs(d.TotalEnergy-d.baseEnergy)<.0001f,"removing light door fades to immutable base energy");
            ClickDoor();
            Check(e.chamberLight.KnownFeedbackWord=="door" && s.Groups.Count==groups,"dark repeat click shows only door");
            var plain=VitruvianChecks.Say(b,"light","door");yield return null;yield return null;
            float before=d.DoorEnergy,time=Time.time;yield return new WaitForSeconds(.3f);float rate=(d.DoorEnergy-before)/(Time.time-time);
            Check(Mathf.Abs(rate-d.chargeRate)<.015f,"light door contributes energy at plain rate without a bonus");
            var boost=VitruvianChecks.Say(b,"light","light");yield return null;yield return null;
            before=d.DoorEnergy;time=Time.time;yield return new WaitForSeconds(.3f);float boosted=(d.DoorEnergy-before)/(Time.time-time);
            Check(Mathf.Abs(boosted/d.chargeRate-d.bonus)<.15f && d.Warmth==0,"light light accelerates without early color");
            yield return Wait(()=>rt.LampBoost(Space.FarRoom)>=2 && d.DoorGlow==1,"full door surface boost");
            yield return new WaitForEndOfFrame();
            var slabInk=new MaterialPropertyBlock();d.Leaf.GetComponent<Renderer>().GetPropertyBlock(slabInk);
            Check(Mathf.Abs(slabInk.GetFloat("_ChargeDoorEmission")-1.25f*d.doorEmission)<.001f,"light light boosts the actual slab emission by 25 percent");
            s.ReturnToLibrary(boost.Id,0,true);yield return Wait(()=>d.DoorEnergy==d.doorCapacity,"door source reaches its cap");
            Check(d.TotalEnergy<.44f && !d.Charged,"glowing door alone cannot fill gate");
            foreach(var lamp in j.Lamps){var body=lamp.GetComponent<Rigidbody>();body.constraints=RigidbodyConstraints.FreezeAll;body.position=d.transform.TransformPoint(new Vector3(0,2,-8));}
            var first=j.Lamps[0].GetComponent<Rigidbody>();first.position=d.transform.TransformPoint(new Vector3(d.width*.5f+.3f,1.5f,-.5f));
            yield return new WaitForSeconds(.3f);Check(d.PassingEnergy==0 && d.DockedEnergy==0,"unlit lamps provide no energy");
            j.SetLights(true,Space.FarRoom);yield return Wait(()=>j.Brightness==1 && d.PassingEnergy>.015f && d.PassingHint>.1f,"passing lit lamp contributes with a local hint");
            Check(d.PassingEnergy<=d.passingCapacity && !d.Charged,"passing contribution stays capped");
            var hint=new MaterialPropertyBlock();bool visibleHint=false;
            foreach(Transform child in d.transform)if(child.name=="Empty charge groove"){
                var renderer=child.GetComponent<Renderer>();renderer.GetPropertyBlock(hint);
                visibleHint|=renderer.sharedMaterial.HasProperty("_BaseColor") && hint.GetColor("_BaseColor").maxColorComponent>.01f;
            }
            Check(visibleHint,"nearest groove uses a visible native material property");
            before=d.PassingEnergy;first.position=d.transform.TransformPoint(new Vector3(0,2,-8));yield return new WaitForSeconds(.4f);
            Check(d.PassingEnergy<before,"passing energy and hint fade after departure");
            float supply=0;
            DriftingLight last=null;
            var sorted=j.Lamps.OrderByDescending(l=>l.ChargePower).ToArray();
            for(int i=0;i<sorted.Length;i++){
                var lamp=sorted[i];var body=lamp.GetComponent<Rigidbody>();
                body.constraints=RigidbodyConstraints.FreezeRotation;
                bool atSlab=supply<j.ChargePowerRequired;
                body.position=atSlab?d.Slot(i,sorted.Length):j.Root.TransformPoint(new Vector3(3,1,j.RoomStart+1));
                if(atSlab){
                    supply+=lamp.ChargePower;
                    if(supply>=j.ChargePowerRequired){last=lamp;continue;}
                }
                lamp.DockAtDoor(atSlab);
            }
            Check(last && sorted.Any(l=>l.Docked&&!l.AtChargeDoor),"a subset of larger lamps can supply the slab");
            float expected=d.dockedCapacity*(supply-last.ChargePower)/j.ChargePowerRequired;
            yield return Wait(()=>Mathf.Abs(d.DockedEnergy-expected)<.0001f,"energy follows the size of bright lamps docked at the charge door");
            Check(!d.Charged,"underpowered docks and passing lights do not unlock the slab");
            last.DockAtDoor();
            float deadline=Time.time+15;
            while(!d.Charged && Time.time<deadline){
                float previous=d.TotalEnergy;yield return null;
                Check(d.TotalEnergy-previous<=d.chargeRate*Time.deltaTime+.0001f,"last lamp docking never jumps the charge animation");
            }
            Check(d.Charged,"sufficient local lamp power smoothly reaches full");
            Check(d.DoorEnergy==d.doorCapacity && Mathf.Abs(d.DockedEnergy-d.dockedCapacity)<.001f && d.Warmth==0,"full white charge has authored capacities");
            boost=VitruvianChecks.Say(b,"light","light");yield return null;yield return null;yield return new WaitForSeconds(.2f);
            Check(d.Warmth>0 && d.Warmth<.5f,"full boost begins a slow color transition");yield return Wait(()=>d.Warmth==1,"warmth settles after full charge");
            var color=d.transform.Find("Charge fill").GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");Color.RGBToHSV(color,out var hue,out var saturation,out var value);
            Check(saturation<=.2f && hue>.025f && hue<.09f && value==1,"full color is subtle low-saturation orange red");
            s.ReturnToLibrary(boost.Id,0,true);yield return Wait(()=>d.Warmth==0,"removing boost returns neutral white");
            s.ReturnToLibrary(plain.Id,0,true);yield return null;yield return null;
            Check(!d.Charged && d.TotalEnergy<=d.incompleteLimit,"removing door glow closes full gate immediately");
            yield return Wait(()=>d.DoorEnergy==0,"door energy decays while lamps remain docked");Check(d.TotalEnergy<.61f,"all docked lamps alone cannot fill gate");
            plain=VitruvianChecks.Say(b,"light","door");yield return Wait(()=>d.Charged,"restoring door glow restores full charge");
            var freeze=VitruvianChecks.Say(b,"negative","move");yield return null;yield return null;before=d.TotalEnergy;float glow=d.DoorGlow;
            yield return new WaitForSecondsRealtime(.3f);Check(d.TotalEnergy==before && d.DoorGlow==glow,"global pause freezes energy");
            s.ReturnToLibrary(plain.Id,0,true);yield return Wait(()=>d.DoorGlow==0,"removing a light sentence still fades while motion is frozen");Check(d.TotalEnergy==before,"light editing does not advance frozen energy");
            plain=VitruvianChecks.Say(b,"light","door");yield return Wait(()=>d.DoorGlow==1,"restoring light while motion is frozen");s.ReturnToLibrary(freeze.Id,0,true);
            e.RequestExit();yield return Wait(()=>e.DialogReady,"close world");before=d.TotalEnergy;yield return new WaitForSecondsRealtime(.3f);Check(d.TotalEnergy==before,"closed world retains energy");
            e.CancelExit();e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"reopen world");
            Result="PASS: lesson ownership, dark base, weighted docked power, boost, warm color, passing hint, pause and closure";
        }
    }
}
