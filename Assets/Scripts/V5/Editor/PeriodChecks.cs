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
            var group=state.Spawn(board.Catalog.Word(words[0]));
            for(int i=1;i<words.Length;i++)state.Join(state.Spawn(board.Catalog.Word(words[i])).Id,group.Id,false);
            return group;
        }
        static PeriodGlyph Glyph(CognitionBoard board,int group)=>board.Surface.GetComponentsInChildren<PeriodGlyph>(true).First(p=>p.group==group && p.name=="Sentence period");
        static PointerEventData Pointer(PeriodGlyph handle,bool right=false)=>new PointerEventData(EventSystem.current){button=right?PointerEventData.InputButton.Right:PointerEventData.InputButton.Left,position=handle.transform.position};
        internal static void Click(CognitionBoard board,int group,bool right=false,bool checkHit=false){
            var handle=Glyph(board,group);var data=Pointer(handle,right);
            if(checkHit){
                data.position+=Vector2.right*25*board.Surface.lossyScale.x;
                var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                Check(hits.Count>0 && hits[0].gameObject==handle.gameObject,"period hit area reaches near the dot: "+string.Join(", ",hits.Select(hit=>hit.gameObject.name)));
            }
            ExecuteEvents.Execute(handle.gameObject,data,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(handle.gameObject,data,ExecuteEvents.pointerUpHandler);
        }
        internal static void Drag(CognitionBoard board,int source,Vector2 destination,bool checkHitArea=false){
            var handle=Glyph(board,source);var data=Pointer(handle);var target=handle.gameObject;
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target,data,ExecuteEvents.initializePotentialDrag);
            Check(data.useDragThreshold,"period uses the normal drag threshold");
            data.position+=Vector2.right*20;ExecuteEvents.Execute(target,data,ExecuteEvents.beginDragHandler);
            data.position=destination;ExecuteEvents.Execute(target,data,ExecuteEvents.dragHandler);
            Check(!GameObject.Find("Dragging period ripple"),"drag has no pointer ripple");
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target,data,ExecuteEvents.endDragHandler);
        }
        static void Check(bool pass,string message){if(!pass){Result="FAIL: "+message;throw new Exception(Result);}}
        static void RemoveByRightClick(CognitionBoard board,CognitionState.Group group,bool whole=false,int index=0){
            var ids=group.Words.Select(w=>w.Id).ToArray();
            GameObject target;Vector2 position;
            if(whole){
                var selection=board.Surface.GetComponent<SentenceSelection>();
                var root=board.GroupRect(group.Id);var bounds=board.GroupContentBounds(group.Id);
                var start=(Vector2)root.TransformPoint(bounds.min)-Vector2.one*8;
                var end=(Vector2)root.TransformPoint(bounds.max)+Vector2.one*8;
                var box=new PointerEventData(EventSystem.current){position=start,pressPosition=start,button=PointerEventData.InputButton.Left};
                selection.OnPointerDown(box);selection.OnBeginDrag(box);box.position=end;selection.OnDrag(box);selection.OnEndDrag(box);
                Check(selection.Selected==group.Id,"select whole sentence before removing it");
                var frame=(RectTransform)board.Surface.Find("Sentence selection");
                position=frame.TransformPoint(new Vector2(frame.rect.xMin+1,frame.rect.center.y));target=board.Surface.gameObject;
            }else{
                var handle=board.GroupRect(group.Id).GetComponentsInChildren<CognitionDragHandle>().First(h=>h.WordId==ids[index]);
                target=handle.gameObject;position=handle.transform.position;
            }
            int before=board.Surface.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.name=="Dissolving word");
            var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right,position=position};
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerDownHandler);
            if(group.ExecutionLocked){
                Check(ids.All(id=>board.State.Owning(id)==group),"right-click preserves a locked sentence");
                Check(board.Surface.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.name=="Dissolving word")==before,"locked words do not dissolve");
                return;
            }
            var removed=whole?ids:new[]{ids[index]};
            Check(removed.All(id=>board.State.Owning(id)==null),"right-click removes the selected word or whole sentence immediately");
            Check(ids.Except(removed).All(id=>board.State.Owning(id)!=null),"right-click keeps unselected words");
            var ghosts=board.Surface.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i=>i.name=="Dissolving word").ToArray();
            Check(ghosts.Length==before+removed.Length && ghosts.All(i=>!i.raycastTarget),"removed words dissolve without intercepting input");
        }
        public static void Run(){var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();Result="Running";e.StartCoroutine(Exercise(e));}
        static IEnumerator Exercise(WorldEntry e){
            var board=e.board;var state=board.State;Check(state.RequirePeriod,"enable Require Period before Play");
            board.Panel.SetOpen(true);yield return new WaitForSecondsRealtime(.4f);
            Check(!GameObject.Find("Period supply"),"no corner supply");
            Check(!e.blackBackground.raycastTarget,"opening fade passes pointer input to the board");
            var one=Draft(board,"light");yield return null;
            Check(!board.Surface.GetComponentsInChildren<PeriodGlyph>(true).Any(p=>p.group==one.Id),"single word has no period");
            RemoveByRightClick(board,one);
            state.ClearWorkspace();
            var g=Draft(board,"positive","world");yield return null;
            var token=Glyph(board,g.Id);var pointer=Pointer(token);
            Check(!g.Effective && state.History.Count==0 && !e.room.Animating,"draft has no effect or history");
            Check(board.GroupRect(g.Id).GetComponent<CanvasGroup>().alpha<.2f,"draft words remain dim");
            var second=Draft(board,"light","move");
            board.PlaceAcquiredSentence(second.Id);
            yield return new WaitForEndOfFrame();
            bool Prompt(int id)=>Glyph(board,id).GetComponentsInChildren<PeriodGlyph>().Any(p=>p.ring);
            Check(Prompt(g.Id) && Prompt(second.Id),"both recognized drafts show independent prompts");
            float low=1,high=0;
            for(float t=0;t<board.punctuation.breathSeconds;t+=Time.unscaledDeltaTime){
                low=Mathf.Min(low,token.canvasRenderer.GetAlpha());high=Mathf.Max(high,token.canvasRenderer.GetAlpha());yield return null;
            }
            Check(low<.25f && high>.9f && token.rectTransform.localScale==Vector3.one,"period breathes without scaling");
            ExecuteEvents.Execute(token.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.18f);Check(token.canvasRenderer.GetAlpha()>.99f,"hover restores opacity");
            ExecuteEvents.Execute(token.gameObject,pointer,ExecuteEvents.pointerExitHandler);
            ExecuteEvents.Execute(token.gameObject,pointer,ExecuteEvents.pointerDownHandler);pointer.position=Vector2.zero;
            ExecuteEvents.Execute(token.gameObject,pointer,ExecuteEvents.pointerUpHandler);Check(!g.Spoken,"release outside is not a click");
            Drag(board,g.Id,Vector2.zero);Check(!board.IsDragging && !g.Spoken,"draft cannot drag or speak by dragging");
            Click(board,g.Id,false,true);yield return null;
            Check(g.Spoken && g.Effective && state.History.Count==1 && !state.CanPrompt(second),"executing world command suspends other prompts");
            var row=board.GroupRect(g.Id);
            var period=row.InverseTransformPoint(board.PeriodEnd(g.Id));
            float inkScale=row.GetComponentInChildren<CenteredSymbolImage>().rectTransform.lossyScale.x/row.lossyScale.x;
            Check(Mathf.Abs(board.GroupContentBounds(g.Id).xMax-period.x-board.punctuation.diameter*.5f*inkScale)<.01f,"ripple padding begins at the circular ink edge");
            while(board.Panel.InputBlocked)yield return null;
            board.Panel.SetOpen(true);while(!board.Panel.FullyOpen)yield return null;
            Drag(board,g.Id,Vector2.zero,true);Click(board,g.Id,true);
            RemoveByRightClick(board,g,false,1);RemoveByRightClick(board,g,true);
            var word=g.Words.Last();
            board.BeginDrag(g.Id,word.Id,false,board.GroupRect(g.Id).position);
            Check(!board.IsDragging && g.Spoken && g.ExecutionLocked && g.Words.Count==2 && Glyph(board,g.Id).gameObject.activeSelf,
                "active world command ignores period, single-word and selection edits");
            int attempts=state.HistoryVersion;
            Click(board,second.Id);
            state.TryAcquireSentence("during.world.open","light floor",out var example);
            Check(!example.Spoken && !second.Spoken && state.HistoryVersion==attempts,"collect example and ignore other periods during execution");
            state.Detach(second.Id,second.Words.Last().Id);
            Check(second.Independent,"other words can still be detached");
            while(state.Find(g.Id)!=null)yield return null;
            Check(state.CanPrompt(example) && !example.Spoken,"collected example waits for its own click");
            Check(e.room.Opened,"world command finishes and returns to library");
            board.Panel.SetOpen(true);while(!board.Panel.FullyOpen)yield return null;
            foreach (var subject in new[]{"light","world","door"}){
                var noop=Draft(board,"positive",subject);
                Click(board,noop.Id);
                Check(!noop.Spoken && !state.JudgementLocked,"no change to "+subject+" leaves judgement available");
                state.ReturnToLibrary(noop.Id,0,true);
            }
            var draft=Draft(board,"light","move","floor");RemoveByRightClick(board,draft,false,1);
            Check(draft.Words.Count==1 && state.Groups.Any(x=>x.Independent&&x.Words[0].Meaning=="floor"),"removing draft middle word preserves separate left and right runs");
            RemoveByRightClick(board,Draft(board,"mirror","move"),true);
            yield return new WaitForSecondsRealtime(.7f);
            Check(!board.Surface.GetComponentsInChildren<UnityEngine.UI.Image>().Any(i=>i.name=="Dissolving word"),"dissolve remnants are cleaned up");
            state.ClearWorkspace();e.sentenceRuntime.presentation.enabledShots=false;
            var wholeSpoken=Draft(board,"mirror","move");Click(board,wholeSpoken.Id);
            RemoveByRightClick(board,wholeSpoken,true);
            var periodDraft=Draft(board,"light","move");Click(board,periodDraft.Id,true);
            Check(!periodDraft.Spoken && periodDraft.Words.Count==2 && !Glyph(board,periodDraft.Id).gameObject.activeSelf,"right-click draft period dissolves only punctuation");
            yield return new WaitForSecondsRealtime(.7f);
            Check(Glyph(board,periodDraft.Id).gameObject.activeSelf,"draft period returns after dissolving");
            state.ClearWorkspace();
            var prefix=Draft(board,"light","move");Check(!prefix.Effective,"two-word prefix stays silent");
            state.Join(Draft(board,"floor").Id,prefix.Id,false);Check(prefix.RuleId=="light.move.floor"&&!prefix.Spoken,"three-word draft stays silent");
            var a=Draft(board,"positive","light");var b=Draft(board,"negative","light");Click(board,b.Id);int weak=e.feedback.WeakCount;Click(board,a.Id);
            Check(b.ExecutionLocked && !a.Spoken && !a.Effective && a.RejectedAttempts==0 && e.feedback.WeakCount==weak,
                "locked click performs no judgement or feedback");
            Click(board,b.Id,true);Check(b.Spoken,"right-click keeps command period");
            while(state.Find(b.Id)!=null)yield return null;
            Check(!a.Spoken && !a.Effective,"reverse command needs another click after completion");
            Click(board,a.Id);Check(a.ExecutionLocked,"fresh period starts the opposite action");
            while(state.Find(a.Id)!=null)yield return null;
            b=Draft(board,"light","move");Click(board,b.Id);Click(board,b.Id,true);
            Check(!b.Spoken && b.Words.Count==2,"right-click period pauses sustained motion");
            var dissolvingDot=board.Surface.GetComponentsInChildren<PeriodGlyph>().Single(p=>p.name=="Dissolving period");
            Check(!dissolvingDot.raycastTarget && dissolvingDot.material.shader.name=="PuzzleApple/V5/SymbolDissolve","period uses non-interactive dissolve material");
            Check(!Glyph(board,b.Id).gameObject.activeSelf,"period stays hidden during dissolve");
            yield return new WaitForSecondsRealtime(.3f);
            Check(dissolvingDot && dissolvingDot.material.GetFloat("_Progress")>0 && dissolvingDot.material.GetFloat("_Progress")<1,"period dissolution progresses over time");
            yield return new WaitForSecondsRealtime(.4f);
            Check(!dissolvingDot && Glyph(board,b.Id).gameObject.activeSelf,"dissolved period cleans up and returns as draft");
            a=b;Click(board,a.Id);var part=state.Detach(a.Id,a.Words.Last().Id);Check(!a.Spoken,"split pauses");
            Check(!board.Surface.GetComponentsInChildren<PeriodGlyph>().Any(p=>p.group==a.Id),"split to one word removes period");
            state.Join(part.Id,a.Id,false);Check(!a.Spoken,"rejoin stays draft");
            var invalid=Draft(board,"world","world");yield return null;int count=state.HistoryVersion;
            Check(Glyph(board,invalid.Id) && !board.GroupRect(invalid.Id).Find("Chalk reveal/Scratch 0"),"invalid draft has a period but no scratch");
            Click(board,invalid.Id,false,true);
            Check(invalid.Spoken&&!invalid.Effective&&state.HistoryVersion>count&&board.GroupRect(invalid.Id).Find("Chalk reveal/Scratch 0"),"invalid click marks and records failure");
            Click(board,invalid.Id,true);Check(!board.GroupRect(invalid.Id).Find("Chalk reveal/Scratch 0"),"pause clears rejection");
            yield return new WaitForSecondsRealtime(.7f);
            Click(board,invalid.Id);state.Swap(invalid.Id,invalid.Words[0].Id,invalid.Words[1].Id);
            Check(!board.GroupRect(invalid.Id).Find("Chalk reveal/Scratch 0"),"editing clears rejection");
            state.TryAcquireSentence("observed","light move",out var granted);Check(granted.Spoken&&granted.Effective,"observation example arrives spoken");
            state.ClearWorkspace();board.SetPeriodMode(false);yield return null;
            Check(!board.Surface.GetComponentsInChildren<PeriodGlyph>().Any(),"legacy mode removes punctuation UI");
            var legacy=Draft(board,"mirror","move");Check(legacy.Effective,"legacy mode activates on assembly");
            RemoveByRightClick(board,legacy);Check(state.Groups.All(x=>!x.Effective),"legacy right-click retains removal behavior");
            RemoveByRightClick(board,Draft(board,"light"));
            RemoveByRightClick(board,Draft(board,"mirror","move"),true);
            state.ClearWorkspace();board.SetPeriodMode(true);Draft(board,"mirror","move");yield return new WaitForSecondsRealtime(1);
            Check(GameObject.Find("Period prompt"),"eligible prompts resume after mode toggle");
            var folder=UnityEditor.SessionState.GetString("C2C.captureFolder","");
            if(!string.IsNullOrEmpty(folder)){yield return new WaitForEndOfFrame();var frame=ScreenCapture.CaptureScreenshotAsTexture();System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"period-click.png"),frame.EncodeToPNG());UnityEngine.Object.Destroy(frame);}
            Result="PASS: automatic periods, breathing, hover, click/drag, right-click word/sentence dissolution in both modes, period-only dissolution and return, progress, history, conflicts and cleanup";
        }
    }
}
