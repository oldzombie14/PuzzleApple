using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4.Editor
{
    public static class SessionSceneChecks
    {
        public static string Result { get; private set; }
        static void Require(bool condition, string message)
        { if (!condition) { Result = "FAIL: " + message; throw new InvalidOperationException(Result); } }
        public static void Run()
        {
            var manager = UnityEngine.Object.FindFirstObjectByType<SessionSceneManager>();
            if (!Application.isPlaying || !manager || manager.Current.intro) throw new InvalidOperationException("Start in ChapterI.");
            Result = "Running"; manager.StartCoroutine(Check(manager));
        }
        static IEnumerator Check(SessionSceneManager manager)
        {
            bool background = Application.runInBackground; Application.runInBackground = true;
            try
            {
                var player = manager.player; var board = manager.board; var state = board.State;
                state.TryAcquireSentence("session.temp", "positive door");
                state.TryAcquireWord("session.keep", board.Catalog.Word("move"));
                manager.library.Select("door");
                manager.Current.temporaryWords = new[] { "door" }; // Play-only test policy, never saved.
                Require(manager.Request("P4"), "first request accepted");
                Require(!manager.Request("P4"), "duplicate request rejected");
                bool sawCover = false, sawProgress = false;
                while (manager.Busy)
                {
                    sawCover |= manager.loading.alpha > .9f;
                    sawProgress |= manager.Progress > 0;
                    Require(player.CinematicLocked, "input locked while loading");
                    Require(board.Panel.InputBlocked, "panel locked until fade completes");
                    yield return null;
                }
                Require(sawCover && sawProgress && manager.Progress == 1, "loading cover and completion");
                Require(!state.Knows("door") && state.Knows("positive") && state.Knows("move"), "temporary scope preserves persistent vocabulary");
                Require(!state.Groups.Any(g => g.Words.Any(w => w.Meaning == "door")), "expired tokens removed");
                Require(!manager.library.IsOpen, "expired detail closed");
                Require(state.TryAcquireWord("session.temp", board.Catalog.Word("door")), "expired acquisition can be collected again");
                Require(manager.menu.opening.Ready, "returning Intro initialized");
                Require(manager.Request("ChapterI"), "return transition accepted");
                while(manager.Busy) yield return null;
                Require(manager.player == player && manager.board == board && state == board.State, "same player and state survive");
                Require(!SceneManager.GetSceneByName("P4").isLoaded, "old content unloaded");
                Require(UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None).Length == 1, "one controller");
                Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "one listener");
                Require(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1, "one event system");
                state.TryAcquireSentence("session.exit", "negative world");
                Require(manager.menu.ConfirmingExit, "exit sentence survives content unload");
                manager.menu.CancelExit();
                Require(!manager.menu.ConfirmingExit && !player.CinematicLocked && !board.Panel.InputBlocked, "cancel restores input");
                Result = "PASS: scope cleanup/reacquisition, two transitions, loader, single session, unloaded content, exit and input restoration";
            }
            finally { Application.runInBackground = background; }
        }
    }
}
