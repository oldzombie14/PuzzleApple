using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5.Editor
{
    public static class MirrorChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string label){if(!ok){Result="FAIL: "+label;throw new Exception(Result);}}
        static IEnumerator Wait(Func<bool> done,string label){
            float end=Time.realtimeSinceStartup+35;while(!done() && Time.realtimeSinceStartup<end)yield return null;Check(done(),label);
        }
        public static void Run(){var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();Result="Running";e.StartCoroutine(Route(e));}
        static IEnumerator Route(WorldEntry e){
            int weak=e.feedback.WeakCount;
            VitruvianChecks.Say(e.board,"mirror","mirror");
            yield return Wait(()=>e.feedback.WeakCount>weak,"no mirror in Intro gives weak feedback");
            e.board.State.ClearWorkspace();
            yield return VitruvianChecks.Gallery(e);
            var pass=e.outdoor;var room=pass.Chapter;var mirrors=room.mirrors;
            var board=e.board;var state=board.State;var p=e.player;var original=mirrors.originals[0];var start=original.position;
            VitruvianChecks.Say(board,"positive","world");yield return Wait(()=>room.box.Opened && !e.Busy,"open gallery");
            board.Panel.SetOpen(true);yield return new WaitForSecondsRealtime(.4f);
            var target=PeriodChecks.Draft(board,"mirror");var moving=PeriodChecks.Draft(board,"mirror");
            var rect=board.GroupRect(target.Id);
            var at=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(rect.rect.xMax+2,rect.rect.center.y)));
            board.BeginDrag(moving.Id,moving.Words[0].Id,false,at,true);board.MoveDrag(at);
            if(state.RequirePeriod)board.EndDrag(at);
            yield return new WaitForSecondsRealtime(.45f);
            var guide=new MaterialPropertyBlock();mirrors.seams[0].GetPropertyBlock(guide);
            Check(guide.GetColor("_BaseColor").a>.65f,"recognized preview brightens the symmetry line");
            Check(!mirrors.CopiesActive && mirrors.Copy(0).parent.Find("Chalk silhouette").gameObject.activeInHierarchy &&
                mirrors.Copy(0).parent.Find("Chalk silhouette").GetComponent<Renderer>().enabled,"draft or imminent snap previews actual reflection");
            if(state.RequirePeriod)PeriodChecks.Click(board,target.Id);else board.EndDrag(at);
            yield return Wait(()=>mirrors.CopiesActive,"mirror appears");
            board.Panel.SetOpen(false);
            var twin=mirrors.Copy(0);var mirror=twin.Find(room.mirror.name).GetComponent<Collider>();
            var plane=mirrors.transform.TransformPoint(mirrors.planeLocal);
            Check(Vector3.Distance(twin.position,plane+Vector3.Reflect(start-plane,mirrors.transform.right))<.025f,"far wall reflection");
            Check(room.WordAt(mirror,out _)=="mirror" && state.History.Any(h=>h.Effective && h.Words.All(w=>w.Id=="mirror")),"copy vocabulary and history");
            var off=VitruvianChecks.Say(board,"negative","light");
            yield return Wait(()=>room.Brightness==0 && state.Find(off.Id)==null,"gallery dark");
            yield return new WaitForSecondsRealtime(.45f);
            mirrors.seams[0].GetPropertyBlock(guide);var darkInk=guide.GetColor("_BaseColor");
            Check(darkInk.r>.9f && darkInk.a>.05f && darkInk.a<.3f,"symmetry remains faintly visible in darkness after activation");
            var on=VitruvianChecks.Say(board,"positive","light");
            yield return Wait(()=>room.Brightness==1 && state.Find(on.Id)==null,"gallery lit again");
            var move=VitruvianChecks.Say(board,"mirror","move");
            yield return Wait(()=>Vector3.Distance(original.position,start)>.15f,"smooth physical mirror travel");
            Check(Vector3.Distance(twin.position,plane+Vector3.Reflect(original.position-plane,mirrors.transform.right))<.04f,"moving reflection follows");
            var freeze=VitruvianChecks.Say(board,"negative","move");yield return Wait(()=>e.sentenceRuntime.MotionFrozen,"freeze");
            var stop=original.position;yield return new WaitForSecondsRealtime(.2f);
            Check(stop==original.position && mirrors.CopiesActive,"freeze holds both mirrors");
            state.ReturnToLibrary(freeze.Id,0,true);yield return Wait(()=>Vector3.Distance(original.position,stop)>.1f,"thaw resumes");
            // Put the moving original clear of the exhibit to approach its reflected opening.
            original.position+=Vector3.forward*4;yield return null;yield return null;
            var inward=Vector3.left;var centre=mirror.bounds.center;
            p.Teleport(centre-inward*1.32f-Vector3.up*p.view.localPosition.y,Quaternion.LookRotation(inward));
            board.Panel.SetOpen(false);yield return null;yield return null;
            p.GetComponent<CharacterController>().Move(inward*1.4f);
            yield return Wait(()=>pass.IsOutside && !pass.InChapter,"moving copy returns to open field");
            Check(state.Groups.Count==0,"crossing clears mirror sentences");
            yield return ChapterChecks.Cross(e,pass.World.chapterFrame);
            yield return ChapterLessonChecks.Traverse(e);
            yield return Wait(()=>pass.InChapter && !pass.Transitioning,"revisit");
            Check(Vector3.Distance(original.position,start)<.001f && !mirrors.CopiesActive,"revisit resets original and clone");
            Result="PASS: reflected position, full mirror vocabulary, moving portal, freeze and revisit";
        }
    }
}
