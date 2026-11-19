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
                Check(heading.sqrMagnitude>1e-6f,label+" starts moving before pause "+reason);
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
            state.ReturnToLibrary(group.Id,0,true);yield return null;
        }
        static void At(WorldEntry e,Vector3 point,Quaternion rotation){
            e.player.Teleport(point,rotation);e.player.MovementLocked=true;
            e.board.Panel.SetOpen(false);
        }
        public static void Run(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Indoor(e));
        }
        static IEnumerator Indoor(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened && !e.Busy,"open box");
            var j=e.journey;var root=j.Root;
            e.sentenceRuntime.presentation.enabledShots=false;
            At(e,Vector3.up*.1f,Quaternion.identity);yield return null;
            yield return Probe(e,"light move","Intro lamp",()=>e.chamberLight.transform.position);
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
            var slab=e.journey.EndingDoor;slab.lowerSeconds=slab.riseSeconds=6;
            yield return Probe(e,"door move","charged door",()=>Vector3.up*slab.Lift);
            e.outdoor.ArriveFromAscent();yield return null;
            var world=e.outdoor.World;var origin=world.transform.position;
            At(e,origin+Vector3.forward*12+Vector3.up*.2f,Quaternion.identity);
            var body=world.field.Spawn(origin+new Vector3(4,12,18),.22f).GetComponent<Rigidbody>();
            var other=world.field.Spawn(origin+new Vector3(-7,15,25),.22f).GetComponent<Outdoor.LightMotion>();
            var door=world.door.GetComponent<Rigidbody>();
            door.position=origin+new Vector3(10,10,0);Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            yield return Probe(e,"door move","outdoor door",()=>door.position);
            yield return Probe(e,"light move","outdoor lamp",()=>body.position);
            Check(other.Direction.sqrMagnitude>.9f && other.Direction!=body.GetComponent<Outdoor.LightMotion>().Direction,"new outdoor lamps start with individual headings");
            yield return Probe(e,"light move floor","outdoor lamp descent",()=>body.position);
            yield return Probe(e,"floor move door","outdoor ground",()=>world.ground.transform.position);
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
