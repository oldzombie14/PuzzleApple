using System;
using System.Collections;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    public static class ConnectivityChecks
    {
        public static string Result {get;private set;}
        static void Check(bool value,string label){if(!value){Result="FAIL: "+label;throw new InvalidOperationException(Result);}}
        static IEnumerator Wait(Func<bool> done,string label){float end=Time.realtimeSinceStartup+40;while(!done()&&Time.realtimeSinceStartup<end)yield return null;Check(done(),label);}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Exercise(e));}
        static IEnumerator Exercise(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey;var p=e.player;var b=e.board;var rt=e.sentenceRuntime;
            rt.presentation.enabledShots=false;b.Panel.SetOpen(false);p.MovementLocked=true;
            const Space start=Space.Chamber|Space.FirstPath,middle=Space.SphereRoom|Space.ReturnPath;
            Check(j.SentenceScope()==Space.Chamber,"unopened progress begins at room one");
            e.chamberLight.SetOn(false);
            yield return Wait(()=>j.SentenceScope()==start,"passage reveal permanently joins the first path");
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"open first physical gate");
            j.EntranceDoor.SetOpen(true);yield return Wait(()=>j.EntranceDoor.Progress==1,"open second physical gate");
            Check(j.SentenceScope()==(start|middle|Space.FarRoom),"two open gates join unlocked regions");
            var motion=VitruvianChecks.Say(b,"light","move");yield return null;yield return null;
            Check(rt.HasEffect(CognitionSignal.LightMove,Space.FarRoom),"sustained sentence reaches connected room three");
            j.SetDoor(false);yield return Wait(()=>j.DoorProgress<.45f && j.DoorProgress>0,"closing below first-unlock threshold");
            Check(j.SentenceScope()==(start|middle|Space.FarRoom) && rt.HasEffect(CognitionSignal.LightMove,Space.FarRoom),"partly closed door still connects both sides");
            yield return Wait(()=>j.DoorProgress==0,"close white door");yield return null;
            Check(j.SentenceScope()==start && !rt.HasEffect(CognitionSignal.LightMove,Space.FarRoom),"outside side stops at closed white door");
            Check((ProgressLock.Accessible(Space.All)&Space.FarRoom)!=0,"closing preserves permanent progress");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.pathLength+4)),j.Root.rotation);yield return null;yield return null;
            Check(j.SentenceScope()==(middle|Space.FarRoom) && !rt.HasEffect(CognitionSignal.LightMove,Space.Chamber),"inside side sees only its connected component");
            j.EntranceDoor.SetOpen(false);yield return Wait(()=>j.EntranceDoor.Progress==0,"close room three entrance");yield return null;
            Check(j.SentenceScope()==middle,"two closed gates bound the middle region");
            p.Teleport(j.EndingDoor.RoomLanding,j.EndingDoor.transform.rotation);yield return null;yield return null;
            Check(j.SentenceScope()==Space.FarRoom,"room three side stops at its entrance and unopened exit");
            var open=VitruvianChecks.Say(b,"positive","door");
            yield return Wait(()=>j.EntranceDoor.Progress>0 && j.EntranceDoor.Progress<.45f,"first opening frames");
            Check(j.SentenceScope()==(middle|Space.FarRoom),"unlocked membrane reconnects before passable height");
            yield return Wait(()=>j.EntranceDoor.Progress==1 && b.State.Find(open.Id)==null,"boundary entrance can reopen from its inside");
            Check(j.DoorProgress==0 && j.SentenceScope()==(middle|Space.FarRoom),"opening command does not recruit doors across its original boundary");
            b.State.ClearWorkspace();
            j.EntranceDoor.SetOpen(false);yield return Wait(()=>j.EntranceDoor.Progress==0,"restore closed entrance");
            j.EndingDoor.GetComponent<ProgressLock>().Unlock();
            p.Teleport(j.EndingDoor.StairLanding,j.EndingDoor.transform.rotation);yield return null;
            Check(j.SentenceScope()==Space.Ascent,"unlocked closed exit isolates the staircase side");
            Check((j.EndingDoor.ControlSpaces&Space.Ascent)!=0,"exit remains controllable from the staircase");
            p.Teleport(Vector3.up*.1f,Quaternion.identity);e.chamberLight.SetOn(true);
            yield return Wait(()=>e.chamberLight.Brightness==1,"relight first room");
            Check(j.SentenceScope()==start,"doorless passage remains joined after relighting");
            Result="PASS: permanent progress, bidirectional closed-door components, chained boundaries, sustained rescope, inside reopening and doorless passage";
        }
    }
}
