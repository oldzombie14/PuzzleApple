using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class MotionPauseChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string what){
            if(ok)return;
            Result="FAIL: "+what;throw new InvalidOperationException(Result);
        }
        static IEnumerator Wait(Func<bool> ready,string label){
            float deadline=Time.realtimeSinceStartup+35;
            while(!ready() && Time.realtimeSinceStartup<deadline)yield return null;
            Check(ready(),label);
        }
        // Sample the real moving object before and after each interruption. Freeze stops scaled time.
        static IEnumerator Probe(WorldEntry entry,string words,string label,Func<Vector3> position)
        {
            var board=entry.board;var state=board.State;
            var group=VitruvianChecks.Say(board,words.Split(' '));
            for(int reason=0;reason<(state.RequirePeriod?3:2);reason++){
                yield return new WaitForSeconds(.09f);
                Vector3 start=position();yield return new WaitForSeconds(.12f);
                Vector3 heading=position()-start;
                Check(heading.sqrMagnitude>1e-6f,label+" starts moving before pause "+reason+
                    " (effective="+group.Effective+", scope="+entry.sentenceRuntime.EffectScope(group)+
                    ", applied="+entry.sentenceRuntime.HasEffect(group.Signal)+", charge="+entry.journey.EndingDoor.Charged+
                    ", lift="+entry.journey.EndingDoor.Lift+")");
                if (group.ExecutionLocked && reason != 1) {
                    Check(state.Detach(group.Id,group.Words[1].Id)==null && !state.Unsay(group.Id),label+" rejects edits during travel");
                    continue;
                }
                CognitionState.Group detached=null,tail=null,freeze=null;
                if(reason==0){
                    int tailWord=group.Words.Last().Id;
                    detached=state.Detach(group.Id,group.Words[1].Id);
                    if(detached.Words[0].Id!=tailWord)tail=state.Owning(tailWord);
                }
                else if(reason==1)freeze=VitruvianChecks.Say(board,"negative","move");
                else state.Unsay(group.Id);
                yield return new WaitForSecondsRealtime(.06f);
                Vector3 rest=position();yield return new WaitForSecondsRealtime(.14f);
                Check((position()-rest).sqrMagnitude<1e-5f,label+" holds position during pause "+reason);
                if(detached!=null){
                    if(tail!=null)state.Join(tail.Id,detached.Id,false);
                    state.JoinAndSpeak(detached.Id,group.Id,false);
                }
                else if(freeze!=null)state.ReturnToLibrary(freeze.Id,0,true);
                else state.Speak(group.Id);
                yield return new WaitForSeconds(.12f);
                var resumed=position()-rest;
                Check(resumed.sqrMagnitude>1e-6f && Vector3.Dot(heading.normalized,resumed.normalized)>.65f,
                    label+" retains heading after pause "+reason);
            }
            if(group.ExecutionLocked)yield return Wait(()=>state.Find(group.Id)==null,label+" completes travel");
            else state.ReturnToLibrary(group.Id,0,true);
            yield return null;
        }
        static void At(WorldEntry e,Vector3 point,Quaternion rotation){
            e.player.Teleport(point,rotation);e.player.MovementLocked=true;
            e.board.Panel.SetOpen(false);
        }
        public static void Run(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Indoor(e));
        }
        static T Copy<T>(T state) where T : struct => JsonUtility.FromJson<T>(JsonUtility.ToJson(state));

        static IEnumerator Indoor(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened && !e.Busy,"open box");
            var j=e.journey;var root=j.Root;
            e.sentenceRuntime.presentation.enabledShots=false;
            At(e,Vector3.up*.1f,Quaternion.identity);yield return null;
            var chamber = e.chamberLight.MotionOutput.StateSource<DriftPose>();
            var chamberPose = Copy(chamber.CaptureState());
            var moved = chamberPose;
            moved.position += Vector3.up * .3f;
            moved.direction = -moved.direction;
            Check(chamber.TryRestoreState(moved) && e.chamberLight.transform.position == moved.position &&
                chamber.CaptureState().direction == moved.direction && chamber.TryRestoreState(chamberPose), "chamber position and heading");
            yield return Probe(e,"light move","Intro lamp",()=>e.chamberLight.transform.position);
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            At(e,root.TransformPoint(new Vector3(0,.1f,j.pathLength*.5f)),root.rotation);yield return null;
            j.doorDuration=8;
            yield return Probe(e,"door move","white door",()=>Vector3.up*j.DoorProgress);
            var centre=new Vector3(0,5,j.pathLength*.5f);
            var star=j.Stars.GetComponentsInChildren<MeshRenderer>().OrderBy(r=>(r.transform.localPosition-centre).sqrMagnitude).First().transform;
            yield return Probe(e,"light move","corridor star",()=>star.position);
            j.doorDuration=1;j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"unlock the tutorial before testing its motion");
            At(e,j.Tutorial.Fill.transform.position,root.rotation);yield return null;
            j.Tutorial.SetOn(true);yield return Wait(()=>!j.Tutorial.Settling(true),"tutorial lamps lit");
            var small=root.Find("Move tutorial small sphere 1");
            small.position=j.Tutorial.Fill.transform.position+root.right*2;
            var tutorial = j.Tutorial.MotionOutput.StateSource<MoveTutorialRoom.MotionState>();
            var spheres = Copy(tutorial.CaptureState());
            var altered = Copy(spheres);
            altered.lamps[0].position += Vector3.up * .3f;
            altered.lamps[0].parked = !altered.lamps[0].parked;
            altered.lamps[altered.lamps.Length - 1].direction.x = float.NaN;
            Check(!tutorial.TryRestoreState(altered) && tutorial.CaptureState().lamps[0].position == spheres.lamps[0].position,
                "invalid final sphere leaves the first sphere in place");
            altered.lamps[altered.lamps.Length - 1].direction = spheres.lamps[spheres.lamps.Length - 1].direction;
            Check(tutorial.TryRestoreState(altered) && j.Tutorial.Sphere.localPosition == altered.lamps[0].position &&
                tutorial.CaptureState().lamps[0].parked == altered.lamps[0].parked && tutorial.TryRestoreState(spheres), "tutorial lamp poses");
            yield return Probe(e,"light move","tutorial sphere",()=>small.position);
            At(e,root.TransformPoint(new Vector3(0,.1f,j.Tutorial.ExitZ+4)),root.rotation);yield return null;
            yield return Probe(e,"floor move","rail platform",()=>j.Platform.transform.position);
            yield return ChargeDoorChecks.UnlockEntrance(e);
            At(e,root.TransformPoint(new Vector3(0,.1f,j.RoomStart+6)),root.rotation);yield return null;
            j.SetLights(true,PassageJourney.LightSpace.FarRoom);
            yield return Wait(()=>j.Brightness==1,"far room lit");
            var lamp=j.Lamps.First();var body=lamp.GetComponent<Rigidbody>();
            Check(j.Lamps.All(l=>l.direction.sqrMagnitude>.9f) && j.Lamps.Any(l=>l.direction!=lamp.direction),"room lamps begin with individual random headings");
            body.position=root.TransformPoint(new Vector3(0,5,j.RoomStart+6));
            Physics.SyncTransforms();
            yield return Probe(e,"light move","drifting lamp",()=>body.position);
            body.position=root.TransformPoint(new Vector3(0,6,j.RoomStart+6));Physics.SyncTransforms();
            yield return Probe(e,"light move floor","directed descent",()=>body.position);
            Result="PASS: chamber lamp, white door, corridor star, tutorial sphere, rail platform, drifting lamp and directed descent sampled across each pause reason";
        }
        public static void RunOutdoor(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(OutdoorRoute(e));
        }
        static IEnumerator OutdoorRoute(WorldEntry e){
            yield return ChargeDoorChecks.OpenEnding(e);
            var roomLights = e.journey.FarRoomMotionOutput.StateSource<PassageJourney.LampMotionState>();
            var docked = Copy(roomLights.CaptureState());
            var first = e.journey.Lamps.First(l => l.AtChargeDoor);
            var anchor = docked.lamps.Single(l => l.slot == first.SlotIndex);
            Check(anchor.docked && anchor.hasDoorTarget && docked.lamps.Any(l => l.docked && !l.chargeDoor), "both docking destinations captured");
            var undocked = Copy(docked);
            Array.Reverse(undocked.lamps);
            for (int i = 0; i < undocked.lamps.Length; i++) undocked.lamps[i].docked = false;
            Check(roomLights.TryRestoreState(undocked) && !first.Docked && roomLights.TryRestoreState(docked) &&
                first.Docked && first.AtChargeDoor && first.GetComponent<Rigidbody>().position == anchor.pose.position,
                "restore docked lamps by slot with their hover anchor and destination");
            yield return new WaitForSeconds(.15f);
            var hovered = roomLights.CaptureState().lamps.Single(l => l.slot == anchor.slot);
            Check(hovered.dockTime > anchor.dockTime && hovered.dockPosition == anchor.dockPosition, "hover advances from saved phase");
            var slab=e.journey.EndingDoor;
            var freeze=VitruvianChecks.Say(e.board,"negative","move");
            float held=slab.Lift;
            yield return new WaitForSecondsRealtime(.2f);
            Check(slab.Lift==held,"sensor lift remains at its current height while frozen");
            e.board.State.ReturnToLibrary(freeze.Id,0,true);
            e.outdoor.ArriveFromAscent();yield return null;
            var world=e.outdoor.World;var origin=world.transform.position;
            At(e,origin+Vector3.forward*12+Vector3.up*.2f,Quaternion.identity);
            var body=world.field.Spawn(origin+new Vector3(4,12,18),.22f).GetComponent<Rigidbody>();
            var other=world.field.Spawn(origin+new Vector3(-7,15,25),.22f).GetComponent<Outdoor.LightMotion>();
            var door=world.door.GetComponent<Rigidbody>();
            door.position=origin+new Vector3(10,10,0);Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            var frame = world.doors.MotionOutput.StateSource<Outdoor.ThresholdDoor.MotionState>();
            var frameState = Copy(frame.CaptureState());
            var shifted = frameState;
            shifted.drift.pose.position += Vector3.up * .2f;
            Check(frame.TryRestoreState(shifted) && door.position == shifted.drift.pose.position && frame.TryRestoreState(frameState), "outdoor frame pose");
            yield return Probe(e,"door move","outdoor door",()=>door.position);
            yield return Probe(e,"light move","outdoor lamp",()=>body.position);
            Check(other.Direction.sqrMagnitude>.9f && other.Direction!=body.GetComponent<Outdoor.LightMotion>().Direction,"new outdoor lamps start with individual headings");
            yield return Probe(e,"light move floor","outdoor lamp descent",()=>body.position);
            // The completed descent leaves both lamps on the ground, blocking the next ascent.
            body.position=origin+new Vector3(4,18,18);
            other.GetComponent<Rigidbody>().position=origin+new Vector3(-7,20,25);
            Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            yield return Probe(e,"floor move door","outdoor ground",()=>world.ground.transform.position);
            e.player.MovementLocked = false;
            e.player.Teleport(new Vector3(origin.x, world.ground.bounds.max.y + .1f, origin.z + 12), Quaternion.identity);
            yield return Wait(() => e.player.StandingOn(world.ground), "player stands on the moving ground");
            var groundOutput = world.world.MotionOutput.StateSource<Outdoor.WorldSentenceEffects.MotionState>();
            var originalGround = Copy(groundOutput.CaptureState());
            var lower = Copy(originalGround);
            lower.groundY -= .15f;
            var rider = e.player.transform.position;
            Check(groundOutput.TryRestoreState(lower) && Mathf.Abs(e.player.transform.position.y - rider.y + .15f) < .001f,
                "restoring the ground carries its rider");
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = new Vector3(origin.x + 8, world.ground.bounds.max.y + .54f, origin.z + 12);
            Physics.SyncTransforms();
            originalGround.clouds.travel += Vector3.up * 3;
            var beforeBlock = groundOutput.CaptureState();
            Check(!groundOutput.TryRestoreState(originalGround) && world.ground.transform.position.y == lower.groundY &&
                groundOutput.CaptureState().clouds.travel == beforeBlock.clouds.travel, "blocked ground restore leaves ground and sky untouched");
            UnityEngine.Object.Destroy(blocker);
            Result="PASS: charged slab, outdoor free door and lamp, lamp descent and rising ground retain direction and progress for each pause reason";
        }
        public static void RunChapter(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Chapter(e));
        }
        static IEnumerator Chapter(WorldEntry e){
            yield return VitruvianChecks.Gallery(e);
            var pass=e.outdoor;var board=e.board;
            VitruvianChecks.Say(board,"positive","world");
            yield return Wait(()=>pass.Chapter.box.Opened && !e.Busy,"folded gallery opens");
            var mirror=pass.Chapter.mirrors.originals[0];
            yield return Probe(e,"mirror move","gallery mirror",()=>mirror.position);
            var garden=pass.Garden;
            At(e,garden.roomFill.transform.position,garden.transform.rotation);yield return null;
            garden.lamp.position=garden.roomFill.transform.position+Vector3.up*2+Vector3.right*2;Physics.SyncTransforms();
            yield return Probe(e,"light move","garden lamp",()=>garden.lamp.position);
            var room=pass.Vitruvian;var puzzle=room.puzzle;
            At(e,room.transform.TransformPoint(new Vector3(0,.1f,7)),room.transform.rotation);yield return null;
            puzzle.ball.SetOn(true);yield return Wait(()=>puzzle.ball.IsOn,"orb lit");
            yield return Probe(e,"light move","orb",()=>puzzle.ball.transform.position);
            yield return Probe(e,"mirror move","corner mirror",()=>puzzle.upper.right);
            Result="PASS: entrance and corner mirrors, garden lamp and orbiting ball hold then continue for every pause reason; removed picture fixtures excluded";
        }
    }
}
