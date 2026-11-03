using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V4.Cognition;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PuzzleApple.V4.Editor
{
    public static class IntroPresentationChecks
    {
        public static string Result {get;private set;}
        static int sequence;
        static void Require(bool value,string message){if(!value){Result="FAIL: "+message;throw new InvalidOperationException(Result);}}
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Fresh P4 Play required.");
            Result="Running";
            var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();flow.StartCoroutine(Check(flow));
        }
        public static void RunPalette()
        {
            Result="Running palette";
            var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();flow.StartCoroutine(Palette(flow));
        }
        static IEnumerator Palette(OpeningFlow flow)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var entry=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            try
            {
                Require(entry.enterButton.colors.normalColor==Color.white&&entry.exitButton.colors.normalColor==Color.white,"menu defaults pure white");
                Require(entry.enterButton.colors.highlightedColor.r<.8f&&entry.exitButton.colors.highlightedColor.r<.8f,"menu hover grey");
                System.IO.Directory.CreateDirectory("Temp/V4Presentation");ScreenCapture.CaptureScreenshot("Temp/V4Presentation/menu.png");
                yield return null;
                entry.enterButton.onClick.Invoke();yield return new WaitForSecondsRealtime(2);
                flow.player.CinematicLocked=true;flow.board.Panel.SetOpen(false);
                flow.player.view.localRotation=Quaternion.identity;yield return new WaitForSecondsRealtime(.6f);
                var day=entry.exitButton.colors.normalColor;
                Require(day.g>day.r&&day.g>day.b&&Mathf.Max(day.r,day.g,day.b)-Mathf.Min(day.r,day.g,day.b)<.1f,"day muted green");
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/day-exit.png");
                yield return new WaitForEndOfFrame();
                yield return null;
                flow.player.view.localRotation=Quaternion.Euler(55,0,0);yield return new WaitForSecondsRealtime(.8f);
                var down=entry.exitButton.colors.normalColor;Require(down.grayscale>.9f&&down.g>down.r,"look down pale green");
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/ground-exit.png");
                yield return new WaitForEndOfFrame();
                yield return null;
                flow.player.view.localRotation=Quaternion.identity;yield return new WaitForSecondsRealtime(.8f);
                var pointer=new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(entry.exitButton.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.4f);
                var ink=entry.exitButton.GetComponentsInChildren<UnityEngine.UI.Image>().First(i=>i.sprite).canvasRenderer.GetColor();
                Require(ink.grayscale>.9f,"native hover renders near white");
                ExecuteEvents.Execute(entry.exitButton.gameObject,pointer,ExecuteEvents.pointerExitHandler);
                flow.lamp.SetOn(false);yield return new WaitForSecondsRealtime(2);
                var night=entry.exitButton.colors.normalColor;
                Require(entry.environment.NightBlend>.99f&&night.b>night.r&&night!=day,"night follows cool environment hue");
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/night-exit.png");
                Result="PASS: white/grey menu, muted green day, pale green down/hover, cool night palette";
            }
            finally{flow.player.CinematicLocked=false;flow.board.Panel.SetOpen(true);Application.runInBackground=background;}
        }
        static void Sentence(CognitionState state,string text)=>state.TryAcquireSentence("presentation.check."+(++sequence),text);
        static void DragWord(OpeningFlow flow,string word,RectTransform target=null)
        {
            var entry=flow.library.GetComponentsInChildren<LibraryDrag>(true).First(x=>x.name=="Vocabulary "+word);
            var origin=RectTransformUtility.WorldToScreenPoint(null,entry.transform.position);
            var point=target?RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(new Vector3(target.rect.xMax+3,target.rect.center.y,0))):
                RectTransformUtility.WorldToScreenPoint(null,flow.board.Surface.TransformPoint(flow.board.Surface.rect.center));
            var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=origin,pressPosition=origin};
            ExecuteEvents.Execute(entry.gameObject,data,ExecuteEvents.beginDragHandler);
            data.position=point;ExecuteEvents.Execute(entry.gameObject,data,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(entry.gameObject,data,ExecuteEvents.endDragHandler);
        }
        static IEnumerator Check(OpeningFlow flow)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var hints=flow.effects.inscriptions;
            string originalKey=hints?hints.progressKey:null;
            if(hints)hints.progressKey="PuzzleApple.Tests.Presentation."+Guid.NewGuid().ToString("N");
            try
            {
                var entry=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();entry.enterButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(2);
                var board=flow.board;var s=board.State;var presentation=flow.effects.presentation;var feedback=presentation.feedback;
                Require(flow.Ready&&board.Panel.IsOpen&&entry.exitButton.isActiveAndEnabled,"entry opens Tab and retains exit");
                foreach(var id in new[]{"positive","negative","world"})
                {Require(s.Knows(id),"entry vocabulary "+id);flow.library.Select(id);Require(flow.library.SnapshotImage.enabled,"entry image for "+id);}
                flow.library.Show(false);board.Panel.SetOpen(false);
                yield return new WaitForSecondsRealtime(.1f);
                var lamp=flow.effects.lights.Nearest(flow.player.transform.position);
                Require(lamp,"nearest lamp exists");
                // Exercise the same aim/raycast collection entry point used by a mouse click.
                var motor=flow.player.GetComponent<CharacterController>();motor.enabled=false;
                flow.player.transform.position=lamp.transform.position+new Vector3(0,0,-2);motor.enabled=true;
                Cursor.lockState=CursorLockMode.Locked;flow.player.view.LookAt(lamp.transform.position);
                Physics.SyncTransforms();
                Physics.Raycast(flow.player.view.position,flow.player.view.forward,out var aimed,65,~0,QueryTriggerInteraction.Collide);
                Require(flow.TryCollectLight(),"aimed lamp collection: can="+flow.CanInteract+", pending="+flow.MovementDiscoveryPending+", cursor="+Cursor.lockState+", hit="+(aimed.collider?aimed.collider.name:"none")+", distance="+aimed.distance);
                yield return new WaitForSecondsRealtime(flow.wordDisplayDuration+.2f);
                Aim(flow,lamp.transform.position);
                Require(flow.TryCollectLight(),"known lamp remains interactable");
                yield return new WaitForSecondsRealtime(.2f);
                var known=flow.collectionSymbol.transform.parent.Find("Known word feedback").GetComponent<UnityEngine.UI.Image>();
                Require(!flow.Collecting&&!flow.player.MovementLocked&&known.gameObject.activeSelf,"repeat feedback does not collect or freeze");
                Require(flow.hoverEye.color.a<.5f&&!flow.hoverEye.gameObject.activeSelf&&known.color.a>.7f,"known symbol readable without eye overlap");
                Require(known.rectTransform.anchoredPosition==Vector2.zero&&known.rectTransform.localScale.x>flow.knownSymbolScale.x,"known symbol centered and expanding");
                System.IO.Directory.CreateDirectory("Temp/V4Presentation");
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/known-symbol.png");
                yield return new WaitForEndOfFrame();
                Aim(flow,lamp.transform.position);
                Require(flow.TryCollectLight(),"repeat click restarts feedback");
                yield return new WaitForSecondsRealtime(flow.knownSymbolDuration+.1f);
                Require(!known.gameObject.activeSelf,"repeat symbol fades out");
                board.Panel.SetOpen(true);flow.library.Select("light");
                Require(flow.library.SnapshotImage.texture,"lamp first-acquisition photo");
                var photo=flow.library.SnapshotImage.texture;flow.library.Remember("light");Require(photo==flow.library.SnapshotImage.texture,"photo not overwritten");
                flow.library.Show(false);
                s.TryAcquireWord("check.move",board.Catalog.Word("move"));
                flow.library.Select("move");var uv=flow.library.SnapshotImage.uvRect;
                yield return new WaitForSecondsRealtime(.15f);
                Require(flow.library.SnapshotImage.texture&&flow.library.SnapshotImage.uvRect!=uv,"prerecorded move playback");
                flow.library.Show(false);
                // Actual LibraryDrag / EventSystem path, including an invalid intermediate run.
                DragWord(flow,"world");yield return null;
                var independent=s.Groups.Single();
                DragWord(flow,"positive",board.GroupRect(independent.Id));yield return new WaitForSecondsRealtime(.2f);
                Require(!s.Groups.Single().Recognized,"world positive is invalid");
                Require(board.GroupRect(independent.Id).GetComponentInChildren<CognitionChalkStroke>(),"invalid sentence strikeout");
                var words=s.Groups.Single().Words;s.Swap(independent.Id,words[0].Id,words[1].Id);
                yield return new WaitForSecondsRealtime(.25f);
                Require(s.Groups.Single().Effective&&feedback.WeakCount>0&&!presentation.Playing,"on world weak ripple only");
                Require(board.GroupRect(independent.Id).Find("Chalk reveal").GetComponent<RectTransform>().rect.width>0,"no erase/rewrite mask");
                s.ClearWorkspace();yield return new WaitForSecondsRealtime(.2f);
                int before=presentation.PlayCount;
                Sentence(s,"positive light");yield return new WaitForSecondsRealtime(.3f);
                Require(presentation.PlayCount==before,"already lit has no cinematic");
                Sentence(s,"negative light");yield return new WaitForSecondsRealtime(.25f);
                var suppressed=s.Groups.Last();Require(suppressed.Conflicted,"opposite state conflict");
                Require(board.GroupRect(suppressed.Id).GetComponent<CanvasGroup>().alpha<.3f,"conflict faded");
                int pulses=feedback.StrongCount+feedback.WeakCount;
                s.ReturnToLibrary(s.Groups.First().Id,0,true);yield return new WaitForSecondsRealtime(.4f);
                Require(!flow.lamp.IsOn&&!flow.lightField.Moving,"split lets suppressed state take over without enabling movement");
                Require(presentation.PlayCount==before&&!presentation.Playing,"split takeover never cuts");
                yield return new WaitForSecondsRealtime(flow.lamp.transitionDuration);
                board.Panel.SetOpen(false);yield return new WaitForSecondsRealtime(.1f);
                Cursor.lockState=CursorLockMode.Locked;flow.player.view.LookAt(flow.door.transform.position+Vector3.up*3);
                Physics.SyncTransforms();Require(flow.TryCollectDoor(),"aimed revealed door collection");
                yield return new WaitForSecondsRealtime(flow.wordDisplayDuration+.2f);
                board.Panel.SetOpen(true);flow.library.Select("door");Require(flow.library.SnapshotImage.texture,"door first-acquisition photo");
                flow.library.Show(false);
                s.ClearWorkspace();yield return new WaitForSecondsRealtime(.3f);
                // A normal pointer join produces an actual moving effect and one cinematic.
                DragWord(flow,"light");yield return null;
                var eyePosition=flow.player.view.localPosition;
                var eyeRotation=flow.player.view.localRotation;
                DragWord(flow,"move",board.GroupRect(s.Groups.Single().Id));
                yield return new WaitForSecondsRealtime(.23f);
                Require(presentation.Playing&&feedback.StrongCount>0,"drag join strong ripple and cinematic");
                Require(!flow.lightField.Moving,"new motion stays off during ripple");
                System.IO.Directory.CreateDirectory("Temp/V4Presentation");ScreenCapture.CaptureScreenshot("Temp/V4Presentation/strong.png");
                var position=flow.player.transform.position;
                yield return new WaitForSecondsRealtime(.6f);
                Require(presentation.LetterboxProgress==0&&board.Panel.IsOpen&&Quaternion.Angle(eyeRotation,flow.player.view.localRotation)<.1f,"feedback completes before panel closes or camera corrects");
                Require(!flow.lightField.Moving,"new motion still off after feedback");
                yield return new WaitForSecondsRealtime(.55f);
                Require(presentation.LetterboxProgress>0&&presentation.LetterboxProgress<1&&!flow.lightField.Moving,"black bars animate before new motion begins");
                yield return new WaitForSecondsRealtime(1.1f);
                Require(flow.lightField.Moving,"new motion begins after black bars fully arrive");
                Require(presentation.LetterboxProgress>.99f&&!board.Panel.IsOpen&&board.Panel.InputBlocked&&flow.player.CinematicLocked,"cinematic input lock and panel hidden");
                Require((flow.player.transform.position-position).sqrMagnitude<.0001f,"player frozen");
                Require((flow.player.view.localPosition-eyePosition).sqrMagnitude<.000001f,"cinematic never translates camera");
                Require(Mathf.Abs(Quaternion.Angle(eyeRotation,flow.player.view.localRotation)-5f)<.1f,"light move applies its configured five-degree pitch");
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/cutscene.png");
                var lightShot=presentation.shots.First(p=>p.rule.Id=="light.move");
                yield return new WaitForSecondsRealtime(lightShot.holdDuration+lightShot.exitDuration+.3f);
                Require(presentation.LetterboxProgress==0&&!presentation.Playing&&board.Panel.IsOpen&&!board.Panel.InputBlocked&&!flow.player.CinematicLocked,"camera and controls restored");
                before=presentation.PlayCount;
                Sentence(s,"light move");yield return new WaitForSecondsRealtime(.3f);
                Require(presentation.PlayCount==before,"duplicate motion no replay");
                s.ClearWorkspace();yield return new WaitForSecondsRealtime(.3f);
                Require(presentation.PlayCount==before,"clear no cut");
                // A completed directional motion is recognized, but cannot generate a new visible movement.
                flow.door.Reveal(flow.player.view);s.TryAcquireWord("check.door",board.Catalog.Word("door"));
                flow.door.GetComponent<Rigidbody>().position=new Vector3(0,flow.door.groundHeight,10);
                flow.door.transform.position=new Vector3(0,flow.door.groundHeight,10);Physics.SyncTransforms();
                Sentence(s,"door ground move");yield return new WaitForSecondsRealtime(.3f);
                Require(presentation.PlayCount==before,"already grounded has no cinematic");
                s.ClearWorkspace();yield return new WaitForSecondsRealtime(.3f);
                int visualRules=board.Catalog.Rules.Count(r=>r.Effects.Any()&&!r.Effects.Contains(CognitionSignal.WorldOff));
                Require(presentation.shots.Length==visualRules&&presentation.shots.All(p=>p&&p.rule)&&presentation.shots.Select(p=>p.rule.Id).Distinct().Count()==visualRules,"only visual complete sentences have independent profiles");
                foreach(var light in UnityEngine.Object.FindObjectsByType<OpeningLight>(FindObjectsSortMode.None))light.SetOn(false);
                DragWord(flow,"positive");yield return null;
                DragWord(flow,"light",board.GroupRect(s.Groups.Single().Id));
                yield return new WaitForSecondsRealtime(.25f);
                Require(presentation.Playing&&presentation.LastShotRule=="light.on","state assembled first uses its sentence shot");
                yield return AwaitShot(presentation);
                DragWord(flow,"move",board.GroupRect(s.Groups.Single().Id));
                yield return new WaitForSecondsRealtime(.25f);
                Require(!presentation.Playing&&!s.Groups.Single().Recognized,"appending move to a state sentence is invalid");
                s.ClearWorkspace();yield return new WaitForSecondsRealtime(.3f);
                Sentence(s,"negative light move");yield return new WaitForSecondsRealtime(.25f);
                Require(!presentation.Playing&&!s.Groups.Single().Recognized,"negative state-modified move is invalid");
                s.ClearWorkspace();yield return new WaitForSecondsRealtime(.3f);
                Sentence(s,"positive light");yield return new WaitForSecondsRealtime(.3f);
                Sentence(s,"light ground move");yield return new WaitForSecondsRealtime(.25f);
                Require(presentation.Playing&&presentation.LastShotRule=="light.ground.move","separate directed motion uses its own shot");
                yield return AwaitShot(presentation);
                Require(flow.lamp.IsOn&&flow.effects.lights.Motion==CognitionSignal.LightWorldMove,"separate state and directed motion coexist");
                s.ClearWorkspace();yield return new WaitForSecondsRealtime(.3f);
                Sentence(s,"ground move");yield return new WaitForSecondsRealtime(.25f);
                Require(presentation.LastShotRule=="ground.move","ground move has its own shot");
                yield return AwaitShot(presentation);
                var raised=flow.effects.lights.Nearest(flow.player.transform.position);
                var raisedPosition=raised.transform.position+Vector3.up*2;
                raised.GetComponent<Rigidbody>().position=raisedPosition;raised.transform.position=raisedPosition;Physics.SyncTransforms();
                var movingWorld=s.Groups.Single();var lightWord=s.Spawn(board.Catalog.Word("light"));
                s.Join(lightWord.Id,movingWorld.Id,true);yield return new WaitForSecondsRealtime(.25f);
                Require(presentation.Playing&&presentation.LastShotRule=="light.ground.move","prepending light switches to light ground move shot");
                yield return AwaitShot(presentation);
                Result="PASS: entry, snapshots, repeated interaction, pointer joins, complete-sentence profiles, prepend light to ground move, feedback, split takeover, freeze and restore";
            }
            finally
            {
                Application.runInBackground=background;
                if(hints){hints.Progress.Clear();hints.progressKey=originalKey;}
            }
        }
        static IEnumerator AwaitShot(IntroPresentation presentation)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(presentation.Playing&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(!presentation.Playing,"shot finishes within timeout");
        }
        static void Aim(OpeningFlow flow,Vector3 target)
        {
            var direction=(target-flow.player.view.position).normalized;
            flow.player.transform.rotation=Quaternion.Euler(0,Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,0);
            float pitch=-Mathf.Asin(direction.y)*Mathf.Rad2Deg;
            typeof(FirstPersonController).GetField("pitch",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(flow.player,pitch);
            flow.player.view.localRotation=Quaternion.Euler(pitch,0,0);
            Cursor.lockState=CursorLockMode.Locked;Physics.SyncTransforms();
        }
    }
}
