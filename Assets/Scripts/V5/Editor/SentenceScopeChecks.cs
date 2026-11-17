using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceScopeChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string what){if(ok)return;Result="FAIL: "+what;throw new InvalidOperationException(Result);}
        static IEnumerator Wait(Func<bool> done,string what){
            float end=Time.realtimeSinceStartup+30;
            while(!done() && Time.realtimeSinceStartup<end)yield return null;
            Check(done(),what);
        }
        public static void Run(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Exercise(e));
        }
        static IEnumerator Exercise(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open box");
            var j=e.journey;var p=e.player;var state=e.board.State;var runtime=e.sentenceRuntime;
            runtime.presentation.enabledShots=false;e.board.Panel.SetOpen(false);
            j.SetDoor(true);j.Tutorial.Sphere.localPosition+=Vector3.back*8;
            yield return Wait(()=>j.DoorProgress==1,"open passage");
            Vector3 point=j.Root.TransformPoint(new Vector3(0,.12f,j.RoomStart-3));
            p.Teleport(point,j.Root.rotation);p.MovementLocked=true;yield return null;
            Check((j.SentenceScope()&Space.FarRoom)!=0 && (j.SentenceScope()&Space.SphereRoom)==0,"toward far room");
            p.Teleport(point,j.Root.rotation*Quaternion.Euler(0,90,0));yield return null;
            Check((j.SentenceScope()&Space.FarRoom)!=0,"sideways chooses near end");
            p.Teleport(point,j.Root.rotation*Quaternion.Euler(0,180,0));yield return null;
            Check((j.SentenceScope()&Space.SphereRoom)!=0 && (j.SentenceScope()&Space.FarRoom)==0,"toward tutorial");
            float distance=j.maxScopeDistance;j.maxScopeDistance=1;
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.12f,j.RoomStart+6)),j.Root.rotation*Quaternion.Euler(0,180,0));yield return null;
            Check(j.SentenceScope()==Space.FarRoom,"distant connected rooms excluded");
            j.maxScopeDistance=distance;
            j.SetLights(true,Space.FarRoom);yield return Wait(()=>j.Brightness==1,"far lamps on");
            state.TryAcquireSentence("scope.sustain","light move",out var drift);
            yield return Wait(()=>runtime.HasEffect(CognitionSignal.LightMove,Space.FarRoom),"commit movement");
            var locked=runtime.Scope(drift);
            p.Teleport(Vector3.zero,Quaternion.identity);yield return null;
            Check(runtime.Scope(drift)==locked && !runtime.SustainsFeedback(drift) && !e.chamberLight.Moving,"walking away preserves scope and silences feedback");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.12f,j.RoomStart+5)),j.Root.rotation);yield return null;
            Check(runtime.SustainsFeedback(drift),"feedback resumes on return");
            if(state.RequirePeriod){
                state.Unsay(drift.Id);p.Teleport(Vector3.zero,Quaternion.identity);yield return null;
                state.Speak(drift.Id);Check(runtime.Scope(drift)==locked,"resuming the same sentence outside preserves its scope");
                p.Teleport(j.Root.TransformPoint(new Vector3(0,.12f,j.RoomStart+5)),j.Root.rotation);yield return null;
            }
            var part=state.Detach(drift.Id,drift.Words[1].Id);
            state.JoinAndSpeak(part.Id,drift.Id,false);
            Check(runtime.Scope(drift)==locked,"rejoining within original scope preserves its rooms");
            part=state.Detach(drift.Id,drift.Words[1].Id);
            p.Teleport(Vector3.zero,Quaternion.identity);yield return null;
            state.JoinAndSpeak(part.Id,drift.Id,false);
            Check((runtime.Scope(drift)&Space.Chamber)!=0 && runtime.Scope(drift)!=locked,"rejoining outside locks a new scope");
            state.ClearWorkspace();
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.12f,j.RoomStart+5)),j.Root.rotation);yield return null;
            var low=j.Lamps[0].GetComponent<Rigidbody>();var high=j.Lamps[1].GetComponent<Rigidbody>();
            low.position=j.Root.TransformPoint(new Vector3(-4,1,j.RoomStart+6));high.position=low.position+Vector3.up*2;
            Physics.SyncTransforms();
            state.TryAcquireSentence("scope.floor","light move floor",out var floor);
            yield return Wait(()=>state.Find(floor.Id)==null,"floor command retires");
            var body=j.Lamps[0].GetComponent<Rigidbody>();var held=body.position;
            j.SetLights(false,Space.FarRoom);yield return Wait(()=>j.Brightness==0,"switch parked lamps off");
            yield return new WaitForSeconds(.3f);
            Check(Vector3.Distance(held,body.position)<.02f,"parked dark lamp holds");
            state.ClearWorkspace();
            p.Teleport(point,j.Root.rotation);yield return null;
            state.TryAcquireSentence("scope.duplicate.path","light move",out var first);
            var pathScope=runtime.Scope(first);
            Check((pathScope&Space.ReturnPath)!=0 && (pathScope&Space.FarRoom)!=0,"first motion spans the path and far room");
            p.Teleport(Vector3.zero,Quaternion.identity);yield return null;
            state.TryAcquireSentence("scope.duplicate.chamber","light move",out var second);
            var firstWord=state.Detach(first.Id,first.Words[1].Id);
            state.Detach(second.Id,second.Words[1].Id);
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.12f,j.RoomStart+6)),j.Root.rotation);yield return null;
            state.JoinAndSpeak(firstWord.Id,first.Id,false);
            Check(runtime.Scope(first)==pathScope,"two equal paused rules retain their own scopes");
            Result="PASS: both corridor directions, fallback, distance, locked scope, feedback, command retirement and dark parking";
        }
    }
}
