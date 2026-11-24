using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using PuzzleApple.V5.Cognition;
using PuzzleApple.V5.Outdoor;

namespace PuzzleApple.V5.Editor
{
    public static class ChapterLessonChecks
    {
        public static string Result {get;private set;}
        static void Check(bool pass,string label)
        {
            if(!pass){Result="FAIL: "+label;throw new InvalidOperationException(Result);}
        }
        static IEnumerator Until(Func<bool> condition,string label)
        {
            Result=label;
            float deadline=Time.realtimeSinceStartup+45;
            while(!condition() && Time.realtimeSinceStartup<deadline)yield return null;
            Check(condition(),label);
        }
        public static void Run()
        {
            var entry=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            entry.StartCoroutine(Route(entry));
        }
        static IEnumerator Route(WorldEntry e)
        {
            e.OpenWorld(-1);
            yield return Until(()=>e.room.Opened && !e.Busy && e.outdoor.Prepared,"prepare outdoor frame");
            e.board.Panel.SetOpen(false);
            yield return WhiteAscentChecks.Leave(e);
            var world=e.outdoor.World;
            var region=world.GetComponent<WorldDropRegion>();
            var outdoorLamp=world.field.Spawn(world.exit.position+Vector3.up,0,InstanceShape.Cube,region);
            yield return ChapterChecks.Cross(e,world.chapterFrame);
            yield return Traverse(e,true);
            yield return ChapterChecks.WalkToMirror(e,e.outdoor.Chapter);
            yield return Until(()=>e.outdoor.IsOutside,"gallery mirror returns to the open field");
            Check(PlayerInstance.Remaining==3 && outdoorLamp,"outdoor instance survives both spaces");
            Result="PASS: frame, mirror takeover, global budget, charge, slot recovery, failed ride, respawn, ascent and gallery return";
        }
        public static IEnumerator Traverse(WorldEntry e,bool exerciseFailure=false)
        {
            var pass=e.outdoor;
            yield return Until(()=>pass.InLesson && !pass.Transitioning,"enter mirror lesson");
            var lesson=pass.Lesson;var state=e.board.State;var player=e.player;
            if(!state.HasAcquired(lesson.mirrorLesson.acquisitionId)){
                var mirror=lesson.transform.Find("Mirror chamber/Reflected mirror");
                player.view.LookAt(mirror.position);player.AdoptCurrentView();
                e.board.Panel.SetOpen(false);Cursor.lockState=CursorLockMode.Locked;
                yield return null;
                Check(e.chamberLight.TryCollect(),"click the reflected mirror");
                yield return Until(()=>state.Groups.Any(g=>g.Effective && g.RuleId=="mirror.mirror"),"world mirror sentence taken over");
                var group=state.Groups.First(g=>g.RuleId=="mirror.mirror");
                state.ReturnToLibrary(group.Id,0,true);
                yield return Until(()=>!mirror.gameObject.activeSelf,"removing the sentence fades out the image");
                e.board.Panel.SetOpen(false);
            }
            CognitionState.Group Say(params string[] words)=>VitruvianChecks.Say(e.board,words);
            Say("mirror","light");
            yield return Until(()=>lesson.Energy==2,"reflect the original cube");
            if(exerciseFailure){
                var field=lesson.Field;
                var capacity=PlayerInstance.Remaining;
                Check(capacity==3,"outdoor lamp still owns its slot");
                yield return Until(()=>!e.sentenceRuntime.presentation.Playing && !e.board.Panel.InputBlocked,
                    "mirror presentation releases editing");
                player.Teleport(lesson.transform.TransformPoint(new Vector3(0,.08f,1.4f)),Quaternion.Euler(0,104,0));
                e.board.Panel.SetOpen(true);
                yield return Until(()=>e.board.Panel.FullyOpen && !e.board.Panel.InputBlocked,"open library for a world drop");
                var screen=new Vector2(.76f*Screen.width,.5f*Screen.height);
                Check(pass.TryWorldDrop(e.board,"light",screen),"drop light from library into the chamber");
                pass.Picker.Select(InstanceShape.Cube);
                var created=field.ActiveLamps.FirstOrDefault(l=>l.GetComponent<PlayerInstance>());
                Check(created,"shape choice created a lamp in the box");
                e.board.Panel.SetOpen(false);
                yield return Until(()=>lesson.Energy==4,"newly placed lamp and image charge the door");
                var far=field.transform.TransformPoint(new Vector3(-2,1,-1.5f));
                var second=field.Spawn(far,0,InstanceShape.Sphere,lesson.Region);
                var third=field.Spawn(far+Vector3.forward,0,InstanceShape.Octahedron,lesson.Region);
                Check(second && third && !field.Spawn(far,0,InstanceShape.Cube,lesson.Region),"four shared slots including outdoor");
                Check(lesson.Energy==4,"out-of-range lamps do not charge");
                var gather=Say("light","move","door");
                yield return Until(()=>state.Find(gather.Id)==null && lesson.Energy>=6,"known directed command gathers remote lamps");
                foreach(var lamp in new[]{created,second,third})lamp.GetComponent<PlayerInstance>().Recover(0);
                yield return Until(()=>PlayerInstance.Remaining==capacity && lesson.Energy==2,"recovering originals releases slots and images");
            }
            Say("light","door");
            yield return Until(()=>lesson.Energy==4 && lesson.Charge==1,"two lamps and illuminated door fill charge");
            var open=Say("positive","door");
            yield return Until(()=>lesson.Openness==1 && state.Find(open.Id)==null,"on door opens charged leaf");
            e.board.Panel.SetOpen(false);
            var deck=lesson.GetComponentsInChildren<BoxCollider>().First(c=>lesson.lift.IsDeck(c));
            player.Teleport(deck.bounds.center+Vector3.up*.26f,Quaternion.Euler(0,90,0));
            yield return Until(()=>player.StandingOn(deck),"board the floor");
            var star=lesson.GetComponentsInChildren<MeshFilter>().First(s=>s.name=="Rising star" &&
                Vector3.Distance(s.transform.position,player.view.position)<28 && s.transform.position.x>player.view.position.x+2 &&
                s.transform.position.y>player.view.position.y+.5f && s.transform.position.y<lesson.lift.whiteLayerHeight);
            var ray=new Ray(player.view.position,(star.transform.position-player.view.position).normalized);
            Check(lesson.WordAt(ray,40,out _,out _)=="light" && !star.GetComponentInParent<ObservableBehaviour>(),
                "rising star offers light without the move lesson");
            if(exerciseFailure){
                var drift=Say("floor","move");
                yield return Until(()=>pass.Transitioning && pass.InLesson && player.transform.position.y<lesson.lift.whiteLayerHeight,
                    "random ride dissolves below the veil and the fall fades to black");
                yield return Until(()=>!pass.Transitioning && player.transform.position.y>-.1f &&
                    Vector3.Distance(player.transform.position,lesson.transform.TransformPoint(new Vector3(4.95f,0,0)))<1.1f,"recover at replacement board");
                state.ReturnToLibrary(drift.Id,0,true);
            }
            Say("floor","mirror","light");
            if(exerciseFailure){
                yield return Until(()=>player.transform.position.y>1 && !pass.Transitioning,"rise with the replacement floor");
                var ridden=lesson.GetComponentsInChildren<BoxCollider>().First(c=>lesson.lift.IsDeck(c) && player.StandingOn(c));
                float height=ridden.bounds.max.y;
                float offset=player.view.position.y-height;
                yield return Until(()=>{
                    Check(player.StandingOn(ridden) && Mathf.Abs(player.view.position.y-ridden.bounds.max.y-offset)<.003f,
                        "rising floor holds the same distance from the rider's camera");
                    return ridden.bounds.max.y>=height+1;
                },"sample one metre of uninterrupted ascent");
            }
            yield return Until(()=>pass.InChapter && !pass.InLesson && !pass.Transitioning,"ride through white into gallery");
            Check(Vector3.Distance(e.player.transform.position,pass.Chapter.arrival.position)<1,"arrive at the original mirror spawn");
            state.ClearWorkspace();
        }
    }
}
