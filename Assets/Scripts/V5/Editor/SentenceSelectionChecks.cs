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
                yield return Drag(mouse,Word(),Word()+Vector2.down*60);
                Check(Vector2.Distance(position,root.anchoredPosition)>10 && state.Groups.Count==2 &&
                    first.Effective && second.Conflicted && earlier==first.Established && later==second.Established,
                    "layout move must preserve conflict and chronology");
                var blank=Word()+Vector2.down*65;
                yield return Input(mouse,blank,1);yield return Input(mouse,blank,0);
                Check(selection.Selected<0,"blank click clears selection");
                yield return Drag(mouse,Word(),blank);
                Check(state.Groups.Count==3,"unselected word detaches");
                Result="PASS: partial/full selection, layout-only drag, conflict order, deselection and split";
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
    }
}
