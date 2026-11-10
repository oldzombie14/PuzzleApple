using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceHistoryChecks
    {
        public static string Result { get; private set; }
        static void Check(bool condition, string message)
        { if (!condition) { Result = "FAIL: " + message; throw new Exception(Result); } }
        public static void Run()
        {
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result = "Running"; entry.StartCoroutine(RunChecks(entry));
        }
        static IEnumerator RunChecks(WorldEntry entry)
        {
            bool background = Application.runInBackground; Application.runInBackground = true;
            try
            {
                if (!entry.Entered) entry.EnterWorld();
                float deadline = Time.realtimeSinceStartup + 10;
                while (!entry.Entered && Time.realtimeSinceStartup < deadline) yield return null;
                Check(entry.Entered, "entry timeout");
                yield return new WaitForSecondsRealtime(.6f);
                var history = UnityEngine.Object.FindFirstObjectByType<SentenceHistory>();
                var board = entry.board; var state = board.State; var panel = board.Panel;
                panel.SetOpen(true); yield return new WaitForSecondsRealtime(.3f);
                Check(history.toggle.onClick.GetPersistentEventCount() == 0, "clear binding removed");
                history.toggle.onClick.Invoke(); yield return new WaitForSecondsRealtime(.15f);
                Check(history.IsOpen && board.Surface.gameObject.activeInHierarchy, "history coexists with workspace");
                state.TryAcquireSentence("history.check.valid", "positive light");
                state.TryAcquireSentence("history.check.conflict", "negative light");
                state.TryAcquireSentence("history.check.invalid", "world light");
                Check(state.History.Any(h => h.Effective) && state.History.Any(h => !h.Effective), "both evaluations recorded");
                var first = state.Groups.First(g => g.RuleId == "light.on");
                state.ReturnToLibrary(first.Id, first.Words[0].Id, true);
                Check(state.Groups.First(g => g.RuleId == "light.off").Effective, "conflict takeover");
                Check(state.History.Any(h => h.Effective && h.Words[0].Id == "negative"), "takeover recorded");
                int count = state.History.Count; state.ClearWorkspace();
                Check(state.History.Count == count, "history survives removal");
                var library = UnityEngine.Object.FindFirstObjectByType<WordLibrary>();
                yield return null;
                var tile = library.GetComponentsInChildren<LibraryDrag>(true).First(t => t.name == "Vocabulary positive");
                var origin = RectTransformUtility.WorldToScreenPoint(null, tile.transform.position);
                var point = RectTransformUtility.WorldToScreenPoint(null, board.Surface.TransformPoint(board.Surface.rect.center));
                var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = origin, pressPosition = origin };
                ExecuteEvents.Execute(tile.gameObject, data, ExecuteEvents.beginDragHandler);
                data.position = point; ExecuteEvents.Execute(tile.gameObject, data, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(tile.gameObject, data, ExecuteEvents.endDragHandler);
                Check(state.Groups.Count == 1 && history.IsOpen, "actual drag callbacks while history open");
                state.TryAcquireSentence("history.check.more1", "door light");
                state.TryAcquireSentence("history.check.more2", "floor world");
                state.TryAcquireSentence("history.check.more3", "light world");
                yield return null; yield return null;
                Check(history.content.rect.height > history.scroll.viewport.rect.height, "scroll overflow");
                history.scroll.verticalNormalizedPosition = 0; yield return null;
                Check(history.content.anchoredPosition.y > 0, "native scroll reaches older records");
                Check(history.content.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Not established"), "invalid strike-through");
                var panelRect = (RectTransform)panel.ContentRoot.parent;
                var before = panelRect.anchoredPosition;
                panel.Toggle();
                Check(!history.IsOpen && history.IsVisible, "drawer begins closing first");
                yield return null;
                Check(panelRect.anchoredPosition == before, "main panel waits for drawer");
                yield return new WaitForSecondsRealtime(.45f);
                Check(!history.IsVisible && panelRect.anchoredPosition.x < before.x, "both panels finish closing");
                panel.Toggle(); yield return new WaitForSecondsRealtime(.3f);
                Check(panel.IsOpen && !history.IsOpen && state.History.Count > 0, "reopen preserves history and resets drawer");
                Result = "PASS: evaluation history, conflict takeover, removal retention, live drag callbacks, strike-through, native scrolling, sequential close and reopen";
            }
            finally { Application.runInBackground = background; }
        }
    }
}
