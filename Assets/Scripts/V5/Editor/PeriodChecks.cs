using System;
using System.Linq;
using System.Collections;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PuzzleApple.V5.Editor
{
    public static class PeriodChecks
    {
        public static string Result {get;private set;}
        internal static void JoinAndSpeak(this CognitionState state,int source,int target,bool prepend){
            state.Join(source,target,prepend);
            if(state.RequirePeriod && state.Find(target)?.Recognized==true)state.Speak(target);
        }
        internal static CognitionState.Group Draft(CognitionBoard board,params string[] words){
            var state=board.State;
            foreach(var word in words)if(!state.Knows(word))state.TryAcquireWord("period.check."+word,board.Catalog.Word(word));
            var g=state.Spawn(board.Catalog.Word(words[0]));
            for(int i=1;i<words.Length;i++)state.Join(state.Spawn(board.Catalog.Word(words[i])).Id,g.Id,false);
            return g;
        }
        internal static void Drag(CognitionBoard board,int source,Vector2 destination){
            var handle=board.Surface.GetComponentsInChildren<PeriodGlyph>().First(p=>p.raycastTarget && p.group==source);
            var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=handle.transform.position};
            ExecuteEvents.Execute(handle.gameObject,data,ExecuteEvents.beginDragHandler);
            data.position=destination;ExecuteEvents.Execute(handle.gameObject,data,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(handle.gameObject,data,ExecuteEvents.endDragHandler);
        }
        static void Check(bool pass,string message){if(!pass){Result="FAIL: "+message;throw new Exception(Result);}}
        public static void Run(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();Result="Running";e.StartCoroutine(Exercise(e));
        }
        static IEnumerator Exercise(WorldEntry e){
            var board=e.board;var state=board.State;Check(state.RequirePeriod,"enable Require Period before Play");
            board.Panel.SetOpen(true);yield return new WaitForSecondsRealtime(.4f);
            var g=Draft(board,"positive","world");yield return null;
            Check(g.Recognized && !g.Effective && state.History.Count==0 && !e.room.Animating,"draft has no history, effect or shot");
            Check(board.GroupRect(g.Id).GetComponent<CanvasGroup>().alpha<.2f,"draft uses conflict opacity");
            yield return new WaitForSecondsRealtime(board.punctuation.guidePulseSeconds+board.punctuation.guideFlightSeconds*.45f);
            Check(board.Surface.GetComponentsInChildren<PeriodGlyph>().Any(p=>p.name=="Period guide"),"first draft shows a moving guide");
            yield return new WaitForEndOfFrame();
            var folder=UnityEditor.SessionState.GetString("C2C.captureFolder","");
            if(!string.IsNullOrEmpty(folder)){
                var frame=ScreenCapture.CaptureScreenshotAsTexture();
                try{System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"period.png"),frame.EncodeToPNG());}
                finally{UnityEngine.Object.Destroy(frame);}
            }
            Drag(board,0,board.PeriodEnd(g.Id));yield return new WaitForSecondsRealtime(.5f);
            Check(g.Spoken && g.Effective && state.History.Count==1,"native period drag speaks world flow");
            while(board.Panel.InputBlocked)yield return null;
            board.Panel.SetOpen(true);yield return null;Drag(board,g.Id,Vector2.zero);yield return null;
            Check(!g.Effective && g.Words.Count==2 && !e.room.Animating,"unsay pauses fold and retains both words");
            state.ClearWorkspace();var prefix=Draft(board,"light","move");
            Check(!prefix.Effective && !e.sentenceRuntime.presentation.Playing,"prefix stays silent");
            var floor=Draft(board,"floor");state.Join(floor.Id,prefix.Id,false);
            Check(prefix.RuleId=="light.move.floor" && !prefix.Effective,"three-word draft");
            var a=Draft(board,"positive","light");var b=Draft(board,"negative","light");
            state.Speak(b.Id);state.Speak(a.Id);
            Check(b.Effective && a.Conflicted,"first spoken wins irrespective of drafting order");
            state.Unsay(b.Id);Check(a.Effective && !b.Conflicted,"draft releases conflict");
            var part=state.Detach(a.Id,a.Words[1].Id);Check(!a.Spoken,"editing ejects punctuation");
            state.Join(part.Id,a.Id,false);Check(!a.Spoken && a.Recognized,"rejoined sentence remains draft");
            var invalid=Draft(board,"mirror","floor");int count=state.HistoryVersion;
            Check(!state.Speak(invalid.Id) && state.HistoryVersion>count && !state.History[0].Effective,"invalid spoken attempt enters history");
            state.TryAcquireSentence("observed","light move",out var granted);
            Check(granted.Spoken && granted.Effective,"granted example arrives spoken");
            state.ClearWorkspace();Check(board.Surface.GetComponentsInChildren<PeriodGlyph>(true).Any(p=>p.group==0 && p.raycastTarget),"corner supply survives clear");
            board.SetPeriodMode(false);yield return null;
            Check(!board.Surface.GetComponentsInChildren<PeriodGlyph>().Any(),"legacy mode has no punctuation UI");
            board.SetPeriodMode(true);Draft(board,"mirror","move");yield return new WaitForSecondsRealtime(.95f);
            Check(!board.Surface.GetComponentsInChildren<PeriodGlyph>().Any(p=>p.name=="Period guide"),"learned guide stays dismissed across trial toggles");
            Result="PASS: actual period drags, world interruption, silent prefix, history, conflicts, edits and granted example";
        }
    }
}
