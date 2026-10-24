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
            try
            {
                var entry=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();entry.enterButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(2);
                var board=flow.board;var s=board.State;var presentation=flow.effects.presentation;var feedback=presentation.feedback;
                Require(flow.Ready&&board.Panel.IsOpen&&entry.exitButton.isActiveAndEnabled,"entry opens Tab and retains exit");
                foreach(var id in new[]{"positive","negative","world"})
                {Require(s.Knows(id),"entry vocabulary "+id);flow.library.Select(id);Require(!flow.library.SnapshotImage.enabled,"no image for "+id);}
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
                Sentence(s,"negative light move");yield return new WaitForSecondsRealtime(.25f);
                var suppressed=s.Groups.Last();Require(suppressed.Conflicted,"whole composite conflict");
                Require(board.GroupRect(suppressed.Id).GetComponent<CanvasGroup>().alpha<.3f,"conflict faded");
                int pulses=feedback.StrongCount+feedback.WeakCount;
                s.ReturnToLibrary(s.Groups.First().Id,0,true);yield return new WaitForSecondsRealtime(.4f);
                Require(!flow.lamp.IsOn&&flow.lightField.Moving,"split lets suppressed composite take over");
                Require(presentation.PlayCount==before&&!presentation.Playing,"split takeover never cuts");
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
                Require(Quaternion.Angle(eyeRotation,flow.player.view.localRotation)<=presentation.maxCorrectionAngle+.1f,"correction stays subtle");
                ScreenCapture.CaptureScreenshot("Temp/V4Presentation/cutscene.png");
                yield return new WaitForSecondsRealtime(presentation.holdDuration+presentation.blendDuration+.3f);
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
                Sentence(s,"door world move");yield return new WaitForSecondsRealtime(.3f);
                Require(presentation.PlayCount==before,"already grounded has no cinematic");
                Result="PASS: entry, snapshots, pointer drag join, invalid/weak/strong/conflict feedback, duplicate effects, split takeover, freeze and restore, completed motion";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
