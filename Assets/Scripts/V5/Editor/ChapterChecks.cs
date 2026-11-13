using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class ChapterChecks
    {
        public static string Result {get;private set;}
        static void Check(bool passed,string label)
        {
            if(passed)return;
            Result="FAIL: "+label;
            throw new InvalidOperationException(Result);
        }
        static IEnumerator Until(Func<bool> done,string label)
        {
            float end=Time.realtimeSinceStartup+45;
            while(!done()&&Time.realtimeSinceStartup<end)yield return null;
            Check(done(),label);
        }
        public static void Run(bool mirrorFirst=false)
        {
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Check(Application.isPlaying&&e&&!e.room.Opened,"fresh P5 Play");
            Result="Running";
            e.StartCoroutine(Route(e,mirrorFirst));
        }
        static IEnumerator Cross(WorldEntry e,Transform frame)
        {
            e.player.Teleport(frame.TransformPoint(new Vector3(0,.24f,-.65f)),frame.rotation);
            yield return null;
            yield return null;
            e.player.GetComponent<CharacterController>().Move(frame.forward*1.3f);
            yield return null;
        }
        static IEnumerator Route(WorldEntry e,bool mirrorFirst)
        {
            var passage=e.outdoor;
            var state=e.board.State;
            e.OpenWorld(-1);
            yield return Until(()=>e.room.Opened&&!e.Busy,"initial open");
            e.board.Panel.SetOpen(false);
            var j=e.journey;
            j.floorSurface.OpenHole();
            yield return Until(()=>j.floorSurface.HoleOpen,"floor open");
            var hole=j.Root.TransformPoint(new Vector3(0,.2f,j.floorSurface.HoleCentreZ));
            e.player.Teleport(hole,j.Root.rotation);
            yield return Until(()=>passage.IsOutside&&!passage.Transitioning,"first fall");
            yield return Cross(e,passage.World.door.transform);
            yield return Until(()=>!passage.IsOutside,"original door returns indoors");
            Check(j.enabled&&!passage.InChapter,"indoor restored");
            e.player.Teleport(hole,j.Root.rotation);
            yield return Until(()=>passage.IsOutside&&!passage.Transitioning,"second fall");
            var frame=passage.World.chapterFrame;
            yield return Cross(e,frame);
            yield return Until(()=>passage.InChapter&&!passage.Transitioning,"picture frame arrival");
            var chapter=passage.Chapter;
            Check(!chapter.box.Opened&&!state.Knows("mirror"),"starts closed");
            bool outdoorLight=passage.World.stateSource.TargetOn;
            state.TryAcquireSentence("chapter.check.off","negative light");
            yield return Until(()=>chapter.bulb.intensity<.001f&&!state.Groups.Any(g=>g.RuleId=="light.off"),"off retires");
            Check(RenderSettings.ambientLight.maxColorComponent>.05f,"dark visibility");
            Check(passage.World.stateSource.TargetOn==outdoorLight,"light command stops at portal");
            if(mirrorFirst)
            {
                yield return CollectMirror(e,chapter);
                Check(chapter.box.Progress==0,"collecting mirror leaves box closed");
                yield return WalkToMirror(e,chapter);
                yield return Until(()=>passage.IsOutside,"mirror returns from closed box");
                yield return Cross(e,frame);
                yield return Until(()=>passage.InChapter&&!passage.Transitioning,"return to closed box");
            }
            state.TryAcquireSentence("chapter.check.move","light move");
            Check(!chapter.lamp&&!chapter.emitter,"chapter has no cube lamp");
            state.TryAcquireSentence("chapter.check.open","positive world");
            yield return Until(()=>chapter.box.Progress>.1f,"chapter opening");
            Check(chapter.box.movingColliders.All(c=>c.enabled),"live folding collision");
            yield return Until(()=>chapter.box.Opened&&!e.Busy,"unfold without mirror");
            yield return WalkToMirror(e,chapter);
            yield return Until(()=>passage.IsOutside,"return to outdoor frame");
            yield return Cross(e,frame);
            yield return Until(()=>passage.InChapter,"revisit chapter");
            Check(chapter.box.Opened&&chapter.bulb.intensity<.001f,"revisit preserves chapter");
            if(!mirrorFirst)yield return CollectMirror(e,chapter);
            var motion=state.Groups.First(g=>g.RuleId=="light.move");
            state.ReturnToLibrary(motion.Id,0,true);
            yield return new WaitForFixedUpdate();
            state.TryAcquireSentence("chapter.check.on","positive light");
            yield return Until(()=>chapter.bulb.intensity>0,"light can switch back on");
            e.RequestExit();
            yield return Until(()=>e.DialogReady,"world exit confirmation");
            e.CancelExit();e.OpenWorld(-1);
            yield return Until(()=>e.room.Opened&&!e.Busy,"portable box reopens");
            Check(chapter.box.Opened&&state.Knows("mirror"),"chapter retained after cancelling exit");
            yield return CheckGarden(e);
            Result="PASS";
        }
        static IEnumerator CheckGarden(WorldEntry e)
        {
            var passage=e.outdoor;
            var chapter=passage.Chapter;
            var garden=passage.Garden;
            var gate=chapter.gardenThreshold;
            var p=e.player;
            var state=e.board.State;
            yield return Until(()=>!state.Groups.Any(g=>g.RuleId=="light.on"),"previous light command retired");
            var lens=chapter.paintingLens.bounds.center;
            p.Teleport(lens+Vector3.down*13+Vector3.back*4,Quaternion.identity);
            p.view.LookAt(lens);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;
            yield return null;
            Check(e.chamberLight.TryCollect()&&e.chamberLight.KnownFeedbackWord=="light","painting lamp light interaction");
            state.TryAcquireSentence("garden.gallery.off","negative light");
            yield return Until(()=>chapter.paintingSpot.intensity==0,"painting spotlight fades out");
            Check(!e.chamberLight.LookingAtLight()&&garden.bulb.intensity>0,"unlit painting lamp and independent garden");
            p.Teleport(new Vector3(gate.position.x,.1f,gate.position.z+4.2f),Quaternion.LookRotation(-gate.forward));
            var walk=typeof(FirstPersonController).GetMethod("AdvanceMovement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int i=0;i<330;i++)walk.Invoke(p,new object[]{Vector2.up,true,1f/120,false});
            Check(passage.CurrentRoom==garden,"walk into garden without jumping");
            var start=garden.lamp.position;
            yield return Until(()=>Mathf.Abs(garden.lamp.position.y-start.y)>.04f,"garden cube floats");
            state.TryAcquireSentence("garden.cube.off","negative light");
            yield return Until(()=>garden.bulb.intensity==0,"garden light switches off");
            state.TryAcquireSentence("garden.cube.on","positive light");
            yield return Until(()=>garden.bulb.intensity>0,"garden light switches on");
            Check(chapter.paintingSpot.intensity==0,"garden command stays in garden");
            for(int i=0;i<330;i++)walk.Invoke(p,new object[]{Vector2.down,true,1f/120,false});
            Check(passage.CurrentRoom==chapter,"walk back into gallery");
            yield return Until(()=>!state.Groups.Any(g=>g.RuleId=="light.on"),"garden command retired");
            state.TryAcquireSentence("garden.gallery.on","positive light");
            yield return Until(()=>chapter.paintingSpot.intensity>0,"painting light switches on");
        }
        static IEnumerator WalkToMirror(WorldEntry e,ChapterRoom chapter)
        {
            var towardMirror=Vector3.right;
            var approach=chapter.mirror.bounds.center-towardMirror*1.4f;
            approach.y=chapter.floor.bounds.max.y+.08f;
            e.player.Teleport(approach,Quaternion.LookRotation(towardMirror));
            yield return null;yield return null;
            e.player.GetComponent<CharacterController>().Move(towardMirror*1.4f);
            yield return null;
        }
        static IEnumerator CollectMirror(WorldEntry e,ChapterRoom chapter)
        {
            e.player.Teleport(chapter.mirror.bounds.center+Vector3.left*2-Vector3.up,Quaternion.LookRotation(Vector3.right));
            e.player.view.LookAt(chapter.mirror.bounds.center);e.player.AdoptCurrentView();
            Cursor.lockState=CursorLockMode.Locked;
            yield return null;
            Check(e.chamberLight.TryCollect(),"mirror raycast in dark");
            yield return Until(()=>e.board.State.Knows("mirror"),"mirror enters library");
        }
    }
}
