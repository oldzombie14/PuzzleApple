using System;
using System.Collections;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class PresentationCoverageChecks
    {
        public static string Result { get; private set; }
        static void Check(bool ok,string why){if(ok)return;Result="FAIL: "+why;throw new InvalidOperationException(Result);}
        static IEnumerator Wait(Func<bool> ready,string label){float end=Time.realtimeSinceStartup+30;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(ready(),label);}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Exercise(e));}
        static IEnumerator Shot(WorldEntry e,bool interrupt,params string[] words)
        {
            var board=e.board;var show=e.sentenceRuntime.presentation;
            board.Panel.SetOpen(true);yield return Wait(()=>board.Panel.FullyOpen,"open editor");
            e.player.view.rotation=Quaternion.Euler(8,37,2);e.player.AdoptCurrentView();
            int count=show.PlayCount;
            var group=VitruvianChecks.Say(board,words);
            yield return Wait(()=>show.Playing,"start "+group.RuleId);
            // The open editor may sway for the frame before presentation takes the camera lock.
            var rotation=e.player.view.rotation;var position=e.player.view.position;
            bool applied=false;float end=Time.realtimeSinceStartup+20;
            while(show.Playing&&Time.realtimeSinceStartup<end){
                Check(Quaternion.Angle(rotation,e.player.view.rotation)<.01f,"preserve current angle: "+group.RuleId);
                Check(Vector3.Distance(position,e.player.view.position)<.001f,"preserve camera position");
                if(show.CanInterrupt){
                    Check(e.sentenceRuntime.HasEffect(group.Signal),"effect commits during shot");applied=true;
                    if(interrupt)Check(show.InterruptForEditing(),"Tab interruption accepted");
                }
                yield return null;
            }
            Check(!show.Playing&&applied&&show.PlayCount==count+1,"exactly one complete shot");
            Check(!board.Panel.InputBlocked&&!e.player.CinematicLocked,"restore controls");
            Check(board.Panel.IsOpen==interrupt,"restore editor/gameplay destination");
        }
        static IEnumerator Illuminate(WorldEntry e)
        {
            foreach(var noun in new[]{"world","light","door","floor"}){
                e.board.State.ClearWorkspace();yield return new WaitForSecondsRealtime(1.6f);
                yield return Shot(e,noun=="floor","light",noun);
                int count=e.sentenceRuntime.presentation.PlayCount;
                VitruvianChecks.Say(e.board,"light",noun);yield return new WaitForSecondsRealtime(.2f);
                Check(e.sentenceRuntime.presentation.PlayCount==count,"duplicate illumination has no extra shot");
            }
            e.board.State.ClearWorkspace();
        }
        static IEnumerator Exercise(WorldEntry e)
        {
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            e.player.MovementLocked=true;
            yield return Illuminate(e);
            e.sentenceRuntime.presentation.enabledShots=false;
            VitruvianChecks.Say(e.board,"negative","move");yield return null;
            e.sentenceRuntime.presentation.enabledShots=true;
            yield return Shot(e,true,"light","world");
            Check(e.sentenceRuntime.MotionFrozen,"non-motion presentation preserves freeze");
            e.board.State.ClearWorkspace();e.player.MovementLocked=false;
            yield return Wait(()=>e.outdoor.Prepared,"outdoors prepared");
            e.outdoor.ArriveFromAscent();yield return null;
            e.player.MovementLocked=true;
            e.board.Panel.SetOpen(true);yield return Wait(()=>e.board.Panel.FullyOpen,"open outdoor editor");
            var outdoor=e.outdoor.World;float duration=outdoor.environment.transitionDuration;
            outdoor.environment.transitionDuration=20;
            outdoor.stateSource.SetOn(false);
            yield return Wait(()=>outdoor.environment.NightBlend>.04f,"sky begins changing before lamps");
            Check(outdoor.stateSource.Brightness==1,"lamps still fully lit at twilight reversal");
            yield return Shot(e,false,"positive","light");
            yield return Wait(()=>outdoor.environment.NightBlend==0,"reverse twilight to full day");
            outdoor.environment.transitionDuration=duration;
            e.board.State.ClearWorkspace();e.player.MovementLocked=false;
            yield return VitruvianChecks.Gallery(e);
            VitruvianChecks.Say(e.board,"positive","world");yield return Wait(()=>e.outdoor.Chapter.box.Opened&&!e.Busy,"open gallery");
            e.outdoor.Chapter.SetLight(true);e.player.MovementLocked=true;
            yield return new WaitForSecondsRealtime(1.6f);
            e.sentenceRuntime.presentation.enabledShots=true;
            yield return Illuminate(e);
            yield return Shot(e,true,"negative","light");
            yield return Wait(()=>e.outdoor.Chapter.Brightness==0,"chapter lamps extinguish after interruption");
            yield return Shot(e,false,"positive","light");
            e.player.MovementLocked=false;
            Result="PASS: illumination shots in Intro/Chapter, outdoor twilight reversal, unchanged view, deferred effects, normal completion, interruption, duplicate suppression and frozen-world illumination";
        }
    }
}
