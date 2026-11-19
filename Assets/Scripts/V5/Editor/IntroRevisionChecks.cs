using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Wait=PuzzleApple.V5.Editor.ChargeDoorChecks;

namespace PuzzleApple.V5.Editor
{
    public static class IntroRevisionChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string message){if(ok)return;Result="FAIL: "+message;throw new Exception(Result);}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Exercise(e));}
        static IEnumerator Exercise(WorldEntry e){
            e.OpenWorld(-1);yield return Wait.Wait(()=>e.room.Opened&&!e.Busy,"open world");
            var j=e.journey;var p=e.player;var d=j.EndingDoor;var a=j.Ascent;
            e.board.Panel.SetOpen(false);e.sentenceRuntime.presentation.enabledShots=false;
            Check(!j.LightsOn && j.Brightness==0 && d.DoorGlow==1 && Mathf.Abs(d.TotalEnergy-.15f)<.001f,"dark far room begins with a glowing door and .15 energy");
            Check(!d.Charged && d.TotalEnergy<1,"initial lamps do not charge all door rails");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.pathLength-5)),j.Root.rotation);
            j.SetDoor(true);yield return Wait.Wait(()=>j.DoorProgress==1,"white door opens");
            var sphere=j.Tutorial.Sphere;var before=sphere.position;
            j.Tutorial.SetOn(true);yield return new WaitForSeconds(1);
            Check(Vector3.Distance(sphere.position,before)>.1f,"lit exit sphere joins unlearned demonstration");
            j.Tutorial.SetOn(false);yield return Wait.Wait(()=>!j.Tutorial.Settling(false),"sphere extinguished");
            before=sphere.position;yield return new WaitForSeconds(.3f);
            Check(Vector3.Distance(sphere.position,before)<.001f,"extinguished exit sphere pauses");
            j.SetDoor(false);yield return Wait.Wait(()=>j.DoorProgress==0,"white door closes");
            yield return Wait.OpenEnding(e);
            var advance=typeof(FirstPersonController).GetMethod("AdvanceMovement",BindingFlags.Instance|BindingFlags.NonPublic);
            p.enabled=false;p.MovementLocked=false;
            foreach(float u in new[]{.1f,.5f,.9f,1f}){
                var origin=a.transform.TransformPoint(new Vector3(0,a.totalRise*u+.08f,2+(a.TopZ-2)*u+(u==1?1:0)));
                foreach(int side in new[]{-1,1}){
                    p.Teleport(origin,a.transform.rotation);Physics.SyncTransforms();
                    for(int n=0;n<220;n++)advance.Invoke(p,new object[]{Vector2.right*side,true,1f/120,n==20});
                    Check(Mathf.Abs(a.transform.InverseTransformPoint(p.transform.position).x)<d.MouthWidth*.5f,"native walk and jump retained on stair/landing "+u);
                }
            }
            p.enabled=true;
            var vocabulary=new[]{"light","move","door","world","positive","negative","floor"};
            var words=vocabulary.Where(e.board.State.Knows).ToArray();float brightness=j.Brightness;
            p.Teleport(a.transform.TransformPoint(new Vector3(5,-a.fallRecoveryDepth-1,10)),a.transform.rotation);
            yield return Wait.Wait(()=>d.Stunned,"fall recovery begins");
            yield return Wait.Wait(()=>!d.Stunned,"fall recovery ends");
            Check(Vector3.Distance(p.transform.position,d.RoomLanding)<.3f,"fall returns before charging door");
            Check(vocabulary.Where(e.board.State.Knows).SequenceEqual(words) && j.Brightness==brightness,"recovery preserves vocabulary and lighting");
            Check(Vector3.Dot(p.view.forward,d.transform.forward)>.99f,"recovery faces door");
            Result="PASS: initial lighting, noncharged door, lit demonstration sphere, extinguished pause, native jump/edge barriers and state-preserving fall recovery";
        }
    }
}
