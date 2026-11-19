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
        static IEnumerator Wait(Func<bool> ready,string label,float seconds=40){
            float end=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(ready(),label);
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
            Check(!rt.ControlsLamps(Space.FarRoom),"dark cubes were not recorded as light-motion targets");
            s.ReturnToLibrary(detached.Id,0,true);s.ReturnToLibrary(earlier.Id,0,true);
            foreach(var target in new[]{"door","floor","light"}){
                var directed=VitruvianChecks.Say(b,"light","move",target);
                yield return SampleCube(lamp,true,"dark cube ignores light move "+target);
                s.ReturnToLibrary(directed.Id,0,true);
                yield return SampleCube(lamp,true,"removing dark directed sentence leaves autonomous motion");
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
        public static void RunGlobalDoors(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(GlobalDoors(e));}
        static IEnumerator GlobalDoors(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey;var b=e.board;var s=b.State;var p=e.player;var rt=e.sentenceRuntime;
            var entrance=j.EntranceDoor;var charge=j.EndingDoor;
            rt.presentation.enabledShots=false;b.Panel.SetOpen(false);p.MovementLocked=true;
            var locked=VitruvianChecks.Say(b,"positive","door");
            yield return Wait(()=>s.Find(locked.Id)==null,"door command before first passage unlock retires");
            Check(j.DoorProgress==0 && entrance.Progress==0 && charge.Lift==0,"locked spaces receive no door command");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"unlock room two");
            entrance.SetOpen(true);yield return Wait(()=>entrance.Progress==1,"unlock room three");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart+3)),j.Root.rotation*Quaternion.Euler(0,180,0));yield return null;
            var original=j.Door.parent.Find("Round handle").GetComponent<Renderer>();
            var handle=entrance.transform.Find("Round handle").GetComponent<Renderer>();
            var a=new MaterialPropertyBlock();var c=new MaterialPropertyBlock();original.GetPropertyBlock(a);handle.GetPropertyBlock(c);
            Check(handle.sharedMaterial==original.sharedMaterial && c.GetFloat("_RoundOutline")==a.GetFloat("_RoundOutline") && c.GetFloat("_RoundOutline")==1 && c.GetFloat("_CorridorPureWhite")==a.GetFloat("_CorridorPureWhite"),"room three handle copies room two material and runtime shading");
            rt.presentation.enabledShots=true;var entranceFocus=entrance.Leaf.position;
            var close=VitruvianChecks.Say(b,"negative","door");
            yield return Wait(()=>rt.presentation.Playing && rt.presentation.CanInterrupt,"global close presentation begins");
            Check(Vector3.Angle(p.view.forward,entranceFocus-p.view.position)<3,"room-side close camera shows the eligible entrance rather than the unpowered slab");
            yield return Wait(()=>entrance.Progress<.8f && j.DoorProgress<.8f,"room-side off door closes both eligible doors");
            Check(charge.Lift==0 && !charge.Charged,"unpowered charge door does not block the other doors");
            yield return Wait(()=>entrance.Progress==0 && j.DoorProgress==0 && s.Find(close.Id)==null,"global close waits for both doors and retires");
            rt.presentation.enabledShots=false;
            Check((j.SentenceScope()&Space.FarRoom)!=0,"closing retains unlocked room three");
            yield return new WaitForEndOfFrame();
            VitruvianChecks.Capture(p,handle.bounds.center+entrance.transform.TransformDirection(new Vector3(1,.25f,-1.6f)),handle.bounds.center,"room-three-handle");
            var open=VitruvianChecks.Say(b,"positive","door");
            yield return Wait(()=>entrance.Progress==1 && j.DoorProgress==1 && s.Find(open.Id)==null,"on door opens all eligible doors from room three");
            var cycle=VitruvianChecks.Say(b,"door","move");
            yield return Wait(()=>entrance.Progress<.7f && j.DoorProgress<.7f,"door move reaches both unlocked hinged doors");
            s.ReturnToLibrary(cycle.Id,0,true);yield return null;yield return null;
            float white=j.DoorProgress,entry=entrance.Progress;yield return new WaitForSeconds(.2f);
            Check(j.DoorProgress==white && entrance.Progress==entry,"splitting global door motion holds every door");
            var on=VitruvianChecks.Say(b,"positive","light");yield return Wait(()=>s.Find(on.Id)==null && j.Brightness==1,"light all unlocked spaces");
            var gather=VitruvianChecks.Say(b,"light","move","door");yield return null;yield return null;
            Check(rt.HasEffect(CognitionSignal.LightDoorMove,Space.FirstPath) && rt.HasEffect(CognitionSignal.LightDoorMove,Space.SphereRoom) && rt.HasEffect(CognitionSignal.LightDoorMove,Space.FarRoom),"directed light command covers every eligible space, independently of camera focus");
            yield return Wait(()=>j.Lamps.All(l=>l.Docked) && !j.Stars.NeedsGather && charge.Charged && s.Find(gather.Id)==null,"all local gathering targets finish before command retirement",90);
            open=VitruvianChecks.Say(b,"positive","door");
            yield return Wait(()=>charge.Lift==1 && entrance.Progress==1 && j.DoorProgress==1 && s.Find(open.Id)==null,"charged slab and both hinged doors all open");
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
            Check(door.Leaf.GetComponent<Renderer>().sharedMaterial.GetColor("_EmissionColor").maxColorComponent>Mathf.GammaToLinearSpace(1.1f) && door.NicheBloomWeight(p.view)<.3f,"initial door emits above bloom threshold while local bloom stays restrained");
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
            s.ClearWorkspace();d.Hold();
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
            Check((j.SentenceScope()&Space.FarRoom)!=0,"closing never relocks the room");
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
            s.JoinAndSpeak(token.Id,cycle.Id,false);yield return Wait(()=>Mathf.Abs(held-d.Progress)>.1f,"rejoin resumes entrance motion");
            var forced=VitruvianChecks.Say(b,"positive","door");yield return Wait(()=>s.Find(forced.Id)==null&&d.Progress>.97f,"open command takes priority over cycle");
            yield return Wait(()=>d.Progress<.8f,"cycle resumes after open command");
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
            Check(e.chamberLight.TryCollect(),"actual first click collects light door lesson");
            Check(b.Panel.IsOpen && !e.chamberLight.collectionSymbol.gameObject.activeSelf,"first door click opens the panel directly without a word popup");
            float minimum=1;
            while(e.chamberLight.Collecting || !b.Panel.FullyOpen){minimum=Mathf.Min(minimum,d.DoorGlow);yield return null;}
            yield return null;yield return null;
            var owned=s.Groups.Single(g=>g.RuleId=="light.door");
            Check(owned.Spoken && owned.Effective && s.Groups.Count==groups+1 && d.LessonAcquired,"acquired lesson is spoken with period");
            Check(minimum>.999f && d.DoorGlow==1 && e.feedback.StrongCount==strong+1 && e.feedback.WeakCount==weak,"ownership transfer is seamless and has one feedback");
            b.Panel.SetOpen(false);yield return new WaitForSeconds(.4f);p.view.LookAt(d.Leaf.position);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;yield return null;
            Check(e.chamberLight.TryCollect() && e.chamberLight.KnownFeedbackWord=="door" && s.Groups.Count==groups+1,"repeat click only shows known door");
            s.ReturnToLibrary(owned.Id,0,true);yield return Wait(()=>d.DoorGlow==0 && Mathf.Abs(d.TotalEnergy-d.baseEnergy)<.0001f,"removing light door fades to immutable base energy");
            var plain=VitruvianChecks.Say(b,"light","door");yield return null;yield return null;
            float before=d.DoorEnergy,time=Time.time;yield return new WaitForSeconds(.3f);float rate=(d.DoorEnergy-before)/(Time.time-time);
            Check(Mathf.Abs(rate-d.chargeRate)<.015f,"light door contributes energy at plain rate without a bonus");
            var boost=VitruvianChecks.Say(b,"light","light");yield return null;yield return null;
            before=d.DoorEnergy;time=Time.time;yield return new WaitForSeconds(.3f);float boosted=(d.DoorEnergy-before)/(Time.time-time);
            Check(Mathf.Abs(boosted/d.chargeRate-d.bonus)<.15f && d.Warmth==0,"light light accelerates without early color");
            s.ReturnToLibrary(boost.Id,0,true);yield return Wait(()=>d.DoorEnergy==d.doorCapacity,"door source reaches its cap");
            Check(d.TotalEnergy<.44f && !d.Charged,"glowing door alone cannot fill gate");
            foreach(var lamp in j.Lamps){var body=lamp.GetComponent<Rigidbody>();body.constraints=RigidbodyConstraints.FreezeAll;body.position=d.transform.TransformPoint(new Vector3(0,2,-8));}
            var first=j.Lamps[0].GetComponent<Rigidbody>();first.position=d.transform.TransformPoint(new Vector3(d.width*.5f+.3f,1.5f,-.5f));
            yield return new WaitForSeconds(.3f);Check(d.PassingEnergy==0 && d.DockedEnergy==0,"unlit lamps provide no energy");
            var denied=VitruvianChecks.Say(b,"light","move","door");yield return new WaitForSeconds(.3f);
            Check(j.Lamps.All(l=>!l.Docked) && d.DockedEnergy==0,"dark gathering leaves every lamp undocked");s.ReturnToLibrary(denied.Id,0,true);
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
            for(int i=0;i<j.Lamps.Count-1;i++){
                var lamp=j.Lamps[i];var body=lamp.GetComponent<Rigidbody>();body.constraints=RigidbodyConstraints.FreezeRotation;
                body.position=d.Slot(i,j.Lamps.Count);lamp.DockAtDoor();
            }
            var last=j.Lamps.Last();last.enabled=false;
            last.GetComponent<Rigidbody>().position=d.Slot(j.Lamps.Count-1,j.Lamps.Count)-d.transform.forward*.6f;
            yield return Wait(()=>Mathf.Abs(d.TotalEnergy-d.incompleteLimit)<.0001f,"passing last lamp holds charge at the incomplete cap");
            last.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeRotation;last.enabled=true;
            var gather=VitruvianChecks.Say(b,"light","move","door");
            float deadline=Time.time+15;bool sawFinalDock=false;
            while(!d.Charged && Time.time<deadline){
                float previous=d.TotalEnergy;yield return null;
                sawFinalDock|=j.Lamps.All(l=>l.Docked);
                Check(d.TotalEnergy-previous<=d.chargeRate*Time.deltaTime+.0001f,"last lamp docking never jumps the charge animation");
            }
            Check(sawFinalDock && d.Charged,"door plus every docked lamp smoothly reaches full");
            yield return Wait(()=>s.Find(gather.Id)==null,"gather command retires after docking");
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
            Result="PASS: actual click lesson once, seamless ownership, .15 start/.03 base, four capped sources, necessary full gate, plain door rate, 1.5x boost, delayed subtle color, passing hint, pause and closure";
        }
    }
}
