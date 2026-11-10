using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5.Editor
{
    public static class V5Checks
    {
        public static string Result {get;private set;}
        static void Require(bool value,string message)
        {if(!value){Result="FAIL: "+message;throw new InvalidOperationException(Result);}}
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Fresh P5 Play required.");
            var entry=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";entry.StartCoroutine(Check(entry));
        }
        static IEnumerator Until(Func<bool> ready)
        {
            float end=Time.realtimeSinceStartup+20;
            while(!ready()&&Time.realtimeSinceStartup<end)yield return null;
            Require(ready(),"timed out waiting for transition");
        }
        static void Drag(WorldEntry e,string word,RectTransform target=null)
        {
            var library=UnityEngine.Object.FindFirstObjectByType<WordLibrary>();
            var tile=library.GetComponentsInChildren<LibraryDrag>(true).First(x=>x.name=="Vocabulary "+word);
            var origin=RectTransformUtility.WorldToScreenPoint(null,tile.transform.position);
            var point=target?RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(new Vector3(target.rect.xMax+3,target.rect.center.y,0))):
                RectTransformUtility.WorldToScreenPoint(null,e.board.Surface.TransformPoint(e.board.Surface.rect.center));
            var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=origin,pressPosition=origin};
            ExecuteEvents.Execute(tile.gameObject,data,ExecuteEvents.beginDragHandler);
            data.position=point;ExecuteEvents.Execute(tile.gameObject,data,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(tile.gameObject,data,ExecuteEvents.endDragHandler);
        }
        static IEnumerator Check(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {

                
                yield return new WaitForEndOfFrame();
                e.enterButton.onClick.Invoke();yield return Until(()=>e.Entered);yield return new WaitForSecondsRealtime(.6f);
                var b=e.board;var s=b.State;
                Require(e.Entered&&b.Panel.IsOpen&&!b.Panel.InputBlocked,"entry and automatic panel");
                Require(new[]{"positive","negative","world"}.All(s.Knows)&&!s.Knows("light"),"three initial symbols, light requires collection");
                Require(!UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a=>a.loop),"no BGM");
                yield return new WaitForEndOfFrame();
                Drag(e,"world");yield return null;
                Drag(e,"positive",b.GroupRect(s.Groups.Single().Id));yield return new WaitForSecondsRealtime(.2f);
                var on=s.Groups.Single();
                Require(!on.Recognized&&e.room.Progress==0&&e.feedback.StrongCount==0,"invalid order remains inert");
                s.Swap(on.Id,on.Words[0].Id,on.Words[1].Id);
                yield return Until(()=>e.room.Opened&&!e.Busy);
                Require(e.room.Opened&&!e.Busy&&e.feedback.StrongCount==1,"on world opens via UI token state");
                Require(e.room.movingColliders.All(c=>!c.enabled),"open panels cannot obstruct movement");
                yield return new WaitForEndOfFrame();
                // Move to a different location before OFF, keeping the earlier ON sentence.
                e.player.Teleport(new Vector3(9,.02f,7),Quaternion.Euler(0,35,0));
                s.TryAcquireSentence("check.off","negative world");
                var frozen=e.player.transform.position;
                yield return Until(()=>e.room.Animating&&!b.Panel.IsOpen);
                Require(e.player.MovementLocked&&!e.player.CinematicLocked&&!b.Panel.IsOpen,"only position locked while folding");
                e.player.view.localRotation=Quaternion.Euler(-23,0,0);e.player.transform.rotation=Quaternion.Euler(0,72,0);e.player.AdoptCurrentView();
                var rotation=e.player.view.rotation;
                var advance=typeof(FirstPersonController).GetMethod("AdvanceMovement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                advance.Invoke(e.player,new object[]{Vector2.one,true,.25f,true});
                Require(Vector3.Distance(frozen,e.player.transform.position)<.0001f,"XYZ movement and gravity locked");
                Require(Quaternion.Angle(rotation,e.player.view.rotation)<.01f,"look retained while position frozen");
                yield return Until(()=>!e.room.Animating&&e.room.Progress==0);
                Require(!e.ConfirmingExit&&e.player.MovementLocked,"closed-room pause precedes dialog");
                float closedAt=Time.realtimeSinceStartup;
                yield return Until(()=>e.ConfirmingExit);
                Require(Time.realtimeSinceStartup-closedAt>=e.closedPauseDuration-.12f,"pause lasts configured duration");
                var dialogInk=e.dialog.GetComponent<CanvasGroup>();
                Require(dialogInk&&dialogInk.alpha<1&&!e.DialogReady,"dialog starts transparent and cannot be clicked early");
                bool intermediate=false;
                while(!e.DialogReady)
                {
                    intermediate|=dialogInk.alpha>0&&dialogInk.alpha<1;
                    Require(Vector3.Distance(frozen,e.player.transform.position)<.0001f,"position remains locked during dialog fade");
                    yield return null;
                }
                Require(intermediate&&dialogInk.alpha==1&&dialogInk.interactable,"dialog visibly fades to interactive");
                Require(e.ConfirmingExit&&e.room.Progress==0&&e.room.movingColliders.All(c=>c.enabled),"close restores walls before confirmation");
                Require(Vector3.Distance(frozen,e.player.transform.position)<.0001f,"closing never teleports player");
                yield return new WaitForEndOfFrame();
                e.cancelButton.onClick.Invoke();yield return null;
                Require(!e.ConfirmingExit&&!e.player.MovementLocked&&e.room.Progress==0,"OFF cancels and leaves room closed");
                s.Spawn(b.Catalog.Word("world"));yield return new WaitForSecondsRealtime(.2f);
                Require(!e.ConfirmingExit&&!e.Busy&&e.room.Progress==0,"unrelated edit and old ON do not retrigger");
                var oldOn=s.Groups.First(g=>g.RuleId=="world.on");s.ReturnToLibrary(oldOn.Id,0,true);
                s.TryAcquireSentence("check.on.again","positive world");
                yield return Until(()=>e.room.Opened&&!e.Busy);
                Require(e.room.Opened,"reformed ON opens with old OFF still present");
                b.Panel.SetOpen(false);yield return null;b.Panel.SetOpen(true);yield return null;
                Require(!e.ConfirmingExit&&e.room.Opened,"panel changes do not replay commands");
                s.ClearWorkspace();yield return null;
                Require(e.room.Opened,"dismantling preserves completed state");
                // At the edge, fit the new enclosure within the outer box without moving the player.
                e.player.Teleport(new Vector3(e.room.centreMax.x+2.6f,.02f,e.room.centreMax.y+2.6f),Quaternion.identity);
                s.TryAcquireSentence("check.edge.off","negative world");
                yield return Until(()=>e.DialogReady);
                Require(e.ConfirmingExit&&Mathf.Abs(e.room.transform.position.x-e.room.centreMax.x)<.01f&&Mathf.Abs(e.room.transform.position.z-e.room.centreMax.y)<.01f,"edge enclosure fits outer walls");
                e.CancelExit();
                Result="PASS: native UI drag, entry, invalid order, unfold, ON/OFF coexistence, exact position lock with view retained, reverse fold, cancel, re-form, panel toggles, dismantle, edge fit, no BGM.";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
