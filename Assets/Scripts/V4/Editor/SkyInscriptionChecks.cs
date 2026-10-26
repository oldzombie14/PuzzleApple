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
                Require(!new IntroHintProgress(hints.progressKey).Has(CognitionSignal.LightMove)&&!PlayerPrefs.HasKey(hints.progressKey),"completion is session-only and is not written to PlayerPrefs");
                Require(hints.Revealed&&hints.Elapsed<2,"reveals immediately on entry without time or distance threshold");
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
                hints.Record(CognitionSignal.WorldOff);
                state.ClearWorkspace();state.TryAcquireSentence("sky.check.down","light ground move");
                yield return new WaitForSecondsRealtime(1);
                Require(hints.DisplayStage==1&&hints.Visibility>.7f&&hints.VisualRoot.childCount==3,"three completed rows replaced by negative light");
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/sky-answer.png");yield return new WaitForEndOfFrame();
                state.ClearWorkspace();state.TryAcquireSentence("sky.check.off","negative light");
                yield return new WaitForSecondsRealtime(.08f);
                Require(hints.Visibility>0&&hints.environment.NightBlend<.001f,"sky stays light while inscription fades");
                yield return new WaitForSecondsRealtime(.8f);
                Require(hints.DisplayStage==2&&hints.Visibility==0&&hints.Progress.Finished&&!new IntroHintProgress(hints.progressKey).Finished,"negative light fades out and retains completion only in this session");
                Require(hints.environment.NightBlend>.1f,"night begins after letters disappear");
                Result="PASS: immediate rear reveal, session-only completion, constant size and world bearing, checklist-to-answer transition, letters fade before night";
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
