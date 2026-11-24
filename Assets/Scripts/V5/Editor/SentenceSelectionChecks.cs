using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PuzzleApple.V5.Editor
{
    // Run in a fresh P5 Play with either punctuation mode, using the real UI input module.
    public static class SentenceSelectionChecks
    {
        public static string Result { get; private set; }
        public static void Run()
        {
            Result="Running";
            var entry=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            entry.StartCoroutine(Exercise(entry));
        }

        static void Check(bool ok,string message)
        {
            if(ok)return;
            Result="FAIL: "+message;
            throw new InvalidOperationException(Result);
        }

        static IEnumerator Input(Mouse mouse,Vector2 position,ushort buttons)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=buttons});
            yield return null;
            yield return null;
        }

        static IEnumerator Drag(Mouse mouse,Vector2 start,Vector2 end)
        {
            yield return Input(mouse,start,0);
            yield return Input(mouse,start,1);
            for(int step=1;step<=5;step++)yield return Input(mouse,Vector2.Lerp(start,end,step/5f),1);
            yield return Input(mouse,end,0);
        }

        static Vector2 ScreenPoint(Transform root,Vector2 point)=>
            RectTransformUtility.WorldToScreenPoint(null,root.TransformPoint(point));

        static IEnumerator Exercise(WorldEntry entry)
        {
            var board=entry.board;
            var state=board.State;
            entry.sentenceRuntime.presentation.enabledShots=false;
            board.Panel.SetOpen(true);
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                mouse.MakeCurrent();
                yield return new WaitUntil(()=>board.Panel.FullyOpen);
                state.TryAcquireSentence("selection.first","light move",out var first);
                state.TryAcquireSentence("selection.second","light move floor",out var second);
                yield return new WaitForSecondsRealtime(.4f);
                var selection=board.Surface.GetComponent<SentenceSelection>();
                var root=board.GroupRect(second.Id);
                var ink=board.GroupContentBounds(second.Id);
                var start=ScreenPoint(root,ink.min)-new Vector2(12,6);
                var end=ScreenPoint(root,ink.max)+new Vector2(10,5);
                var word=root.GetComponentsInChildren<CognitionDragHandle>().First(h=>h.WordId>0);
                var rect=(RectTransform)word.transform;
                Vector2 Word()=>ScreenPoint(rect,rect.rect.center);
                var position=root.anchoredPosition;
                long earlier=first.Established,later=second.Established;
                yield return Drag(mouse,start,new Vector2((start.x+end.x)/2,end.y));
                Check(selection.Selected<0,"partial sentence selected");
                yield return Drag(mouse,start,end);
                Check(selection.Selected==second.Id,"whole sentence selection");
                Check(!board.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Sentence handle"||t.name=="Preview sentence bullet"),"legacy sentence bullets removed");
                var frame=(RectTransform)board.Surface.Find("Sentence selection");
                // Compare UI frame edges to the rendered line from a real pulse in its held phase.
                var stage=entry.feedback.transform.Find("Sentence feedback rendering");
                entry.feedback.Play(second.Id,false);
                var line=stage.GetChild(stage.childCount-1).GetComponentInChildren<LineRenderer>();
                var camera=stage.GetComponentInChildren<Camera>();
                yield return new WaitForSecondsRealtime(entry.feedback.duration*1.2f*.43f);
                yield return new WaitForEndOfFrame();
                Vector2 Edge(int vertex)=>camera.WorldToScreenPoint(line.transform.TransformPoint(line.GetPosition(vertex)));
                Vector2 lowError=Edge(0)-ScreenPoint(frame,frame.rect.min);
                Vector2 highError=Edge(2)-ScreenPoint(frame,frame.rect.max);
                string margins=$"ripple vs selection in pixels: left {lowError.x:F2}, right {highError.x:F2}, top {highError.y:F2}, bottom {lowError.y:F2}";
                Check(new[]{lowError.x,lowError.y,highError.x,highError.y}.All(error=>Mathf.Abs(error)<1f),margins);
                string folder=System.IO.Path.GetFullPath("Temp/SentenceSelectionChecks");System.IO.Directory.CreateDirectory(folder);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,state.RequirePeriod?"period-on.png":"period-off.png"));
                yield return null;yield return null;
                yield return Drag(mouse,Word(),Word()+Vector2.down*60);
                Check(Vector2.Distance(position,root.anchoredPosition)>10 && state.Groups.Count==2 &&
                    first.Effective && !second.Spoken && earlier==first.Established && later==second.Established,
                    "layout move must preserve conflict and chronology");
                var blank=ScreenPoint(frame,new Vector2(frame.rect.center.x,frame.rect.yMin))-Vector2.up*30;
                yield return Input(mouse,blank,1);yield return Input(mouse,blank,0);
                Check(selection.Selected<0,"blank click clears selection");
                yield return Drag(mouse,Word(),blank);
                Check(state.Groups.Count==3,"unselected word detaches");
                state.ClearWorkspace();
                foreach(var part in new[]{"word","period","padding"}){
                    var draft=PeriodChecks.Draft(board,"light","move");
                    yield return new WaitForSecondsRealtime(.4f);
                    var row=board.GroupRect(draft.Id);
                    var bounds=board.GroupContentBounds(draft.Id);
                    yield return Drag(mouse,ScreenPoint(row,bounds.min)-new Vector2(12,6),ScreenPoint(row,bounds.max)+new Vector2(10,5));
                    Check(selection.Selected==draft.Id,"select before right-clicking "+part);
                    var firstWord=(RectTransform)row.GetComponentInChildren<CognitionDragHandle>().transform;
                    Vector2 at=part=="word" ? ScreenPoint(firstWord,firstWord.rect.center)
                        : part=="period" ? (Vector2)board.PeriodEnd(draft.Id)
                        : ScreenPoint(frame,new Vector2(frame.rect.xMin+1,frame.rect.center.y));
                    yield return Input(mouse,at,2);yield return Input(mouse,at,0);
                    Check(selection.Selected<0 && state.Groups.Count==0,"right-clicking "+part+" removes the entire selection");
                }
                Result="PASS: "+margins+"; drag, split and right-click on word, period and padding";
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
    }
}
