using System;
using System.Collections;
using PuzzleApple.V4.Cognition;
using UnityEngine;

namespace PuzzleApple.V4.Editor
{
    public static class SkyInscriptionChecks
    {
        public static string Result {get;private set;}
        static void Require(bool value,string message){if(!value){Result="FAIL: "+message;throw new InvalidOperationException(Result);}}
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Fresh P4 Play required.");
            Result="Running";
            var hints=UnityEngine.Object.FindFirstObjectByType<SkyInscription>();
            hints.StartCoroutine(Check(hints));
        }
        static IEnumerator Check(SkyInscription hints)
        {
            string originalKey=hints.progressKey;
            hints.progressKey="PuzzleApple.Tests.SkyHints."+Guid.NewGuid().ToString("N");
            bool background=Application.runInBackground;Application.runInBackground=true;
            var flow=hints.opening;var player=flow.player;
            try
            {
                yield return new WaitForSecondsRealtime(.2f);
                Require(!hints.Revealed&&hints.Elapsed==0,"menu does not reveal inscriptions");
                hints.fadeDuration=.3f;
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();
                yield return new WaitForSecondsRealtime(1.4f);
                flow.effects.presentation.enabled=false;
                var state=flow.board.State;
                state.TryAcquireSentence("sky.check.move","light move");
                Require(hints.Progress.Has(CognitionSignal.LightMove),"actual applied light move saved before reveal");
                Require(new IntroHintProgress(hints.progressKey).Has(CognitionSignal.LightMove)&&PlayerPrefs.HasKey(hints.progressKey),"completion survives a new progress instance");
                Require(!hints.Revealed&&hints.Visibility==0,"entry waits for the first cube lamps");
                flow.lightField.Release();
                yield return null;yield return null;
                Require(flow.lightField.ActiveCount>0&&hints.Revealed,"reveals after cube lamps first spawn");
                Require(Vector3.Dot(hints.Bearing,Vector3.ProjectOnPlane(player.view.forward,Vector3.up).normalized)<-.99f,"appears behind initial view");
                Require(player.view.GetComponent<Camera>().WorldToViewportPoint(hints.VisualRoot.position).z<0,"initial view cannot see inscription behind player");
                yield return new WaitForSecondsRealtime(.4f);
                Require(hints.Visibility>.7f&&hints.DisplayStage==0,"checklist visible in space while player remains still");
                var strike=hints.VisualRoot.Find("LightMove completed").GetComponent<RectTransform>();
                Require(strike.rect.width>100,"earlier action already crossed out");
                System.IO.Directory.CreateDirectory("Temp/V4Presentation");
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/sky-checklist-panel.png");yield return new WaitForEndOfFrame();
                flow.board.Panel.SetOpen(false);yield return null;
                player.CinematicLocked=true;
                var camera=player.view.GetComponent<Camera>();
                Vector3 relative=hints.VisualRoot.position-player.view.position;
                Vector3 bearing=hints.Bearing;var scale=hints.VisualRoot.localScale;
                player.Teleport(player.transform.position+new Vector3(25,0,25),player.transform.rotation);
                yield return null;yield return null;
                Require(Vector3.Distance(relative,hints.VisualRoot.position-player.view.position)<.01f&&scale==hints.VisualRoot.localScale,"walking does not change viewing distance or scale");
                player.transform.rotation=Quaternion.LookRotation(-bearing);player.view.localRotation=Quaternion.identity;
                yield return null;yield return null;
                Require(camera.WorldToViewportPoint(hints.VisualRoot.position).z<0&&hints.Bearing==bearing,"turning away hides fixed world bearing");
                player.transform.rotation=Quaternion.LookRotation(bearing);player.view.localRotation=Quaternion.identity;
                yield return null;yield return null;
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/sky-checklist.png");yield return new WaitForEndOfFrame();
                state.ClearWorkspace();state.TryAcquireSentence("sky.check.earlyOff","negative light");
                yield return new WaitForSecondsRealtime(hints.environment.transitionDuration+hints.fadeDuration+.6f);
                Require(hints.environment.NightBlend==1&&hints.DisplayStage==0&&hints.Visibility>0,"unfinished checklist remains visible at night");
                var symbol=hints.VisualRoot.GetChild(0).GetComponent<UnityEngine.UI.Image>();
                Require(symbol.color.r<.3f&&hints.Visibility<.4f,"night ink and opacity remain subdued");
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/sky-night-checklist.png");yield return new WaitForEndOfFrame();
                Require(hints.TryClearFromShortcut(true)&&hints.Progress.Mask==0&&!PlayerPrefs.HasKey(hints.progressKey),"X clears memory and saved progress immediately at night");
                Require(state.HasEffect(CognitionSignal.LightOff)&&state.Knows("light"),"reset preserves sentences and vocabulary");
                state.TryAcquireSentence("sky.check.unrelated","positive world");
                Require(!hints.Progress.Has(CognitionSignal.LightOff),"unrelated edit does not rerecord a held effect after reset");
                state.ClearWorkspace();state.TryAcquireSentence("sky.check.day","positive light");
                yield return new WaitForSecondsRealtime(hints.environment.transitionDuration+hints.fadeDuration+.6f);
                Require(hints.environment.NightBlend==0&&hints.Visibility>.7f,"reset checklist returns to daytime opacity");
                state.ClearWorkspace();state.TryAcquireSentence("sky.check.exit","negative world");
                var entry=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
                Require(entry.ConfirmingExit&&new IntroHintProgress(hints.progressKey).Has(CognitionSignal.WorldOff),"exit sentence persists before either choice");
                entry.CancelExit();
                Require(!entry.ConfirmingExit&&new IntroHintProgress(hints.progressKey).Has(CognitionSignal.WorldOff),"OFF retains saved completion");
                state.TryAcquireSentence("sky.check.unrelated2","positive world");
                Require(!entry.ConfirmingExit,"unrelated edits do not reopen exit confirmation");
                state.ClearWorkspace();state.TryAcquireSentence("sky.check.moveAgain","light move");
                state.ClearWorkspace();state.TryAcquireSentence("sky.check.down","light ground move");
                yield return new WaitForSecondsRealtime(1);
                Require(hints.DisplayStage==1&&hints.Visibility>.7f&&hints.VisualRoot.childCount==2,"three completed rows replaced by negative light");
                // Simulate arriving at night with only light-off left, without recording the operation.
                hints.environment.lamp.SetOn(false);
                yield return new WaitForSecondsRealtime(hints.environment.transitionDuration+hints.fadeDuration+.6f);
                Require(hints.environment.NightBlend==1&&hints.DisplayStage==1&&hints.Visibility==0,"single light-off hint stays hidden at night");
                hints.environment.lamp.SetOn(true);
                yield return new WaitForSecondsRealtime(hints.environment.transitionDuration+hints.fadeDuration+.6f);
                Require(hints.Visibility>.7f&&hints.DisplayStage==1,"single light-off hint returns in daylight");
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/sky-answer.png");yield return new WaitForEndOfFrame();
                state.ClearWorkspace();state.TryAcquireSentence("sky.check.off","negative light");
                yield return new WaitForSecondsRealtime(.08f);
                Require(hints.Visibility>0&&hints.environment.NightBlend<.001f,"sky stays light while inscription fades");
                yield return new WaitForSecondsRealtime(.8f);
                Require(hints.DisplayStage==1&&hints.Visibility==0&&PlayerPrefs.GetInt(hints.progressKey)==7,"using the daytime prompt saves only checklist progress");
                Require(hints.environment.NightBlend>.1f,"night begins after letters disappear");
                state.ClearWorkspace();state.TryAcquireSentence("sky.check.dayAgain","positive light");
                yield return new WaitForSecondsRealtime(hints.environment.transitionDuration+hints.fadeDuration+.6f);
                Require(hints.DisplayStage==1&&hints.Visibility>.7f,"daytime prompt returns after using the off sentence");
                PlayerPrefs.SetInt(hints.progressKey,15);
                Require(new IntroHintProgress(hints.progressKey).ChecklistComplete&&PlayerPrefs.GetInt(hints.progressKey)==7,"old saves retain only checklist progress");
                hints.TryClearFromShortcut(true);
                Require(hints.DisplayStage==0&&hints.Progress.Mask==0&&!new IntroHintProgress(hints.progressKey).ChecklistComplete,"checklist progress resets immediately");
                Result="PASS: rear reveal, saved completion, night checklist, day-only final hint, exit cancellation, immediate reset, held-effect deduplication, fixed bearing and scale";
            }
            finally
            {
                player.CinematicLocked=false;
                hints.Progress.Clear();hints.progressKey=originalKey;
                Application.runInBackground=background;
            }
        }
    }
}
