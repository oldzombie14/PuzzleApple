using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class JourneyChecks
    {
        public static string Result {get;private set;}
        static void Require(bool ok,string text){if(!ok){Result="FAIL: "+text;throw new Exception(Result);}}
        static readonly MethodInfo Step=typeof(FirstPersonController).GetMethod("AdvanceMovement",BindingFlags.Instance|BindingFlags.NonPublic);
        static void Walk(FirstPersonController p,int frames,bool jump=false,Vector2? input=null)
        {for(int i=0;i<frames;i++)Step.Invoke(p,new object[]{input??Vector2.up,true,1f/120,jump&&i==0});}
        static IEnumerator Until(Func<bool> condition)
        {float end=Time.realtimeSinceStartup+25;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Require(condition(),"transition timeout");}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Check(e));}
        static IEnumerator Check(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                e.EnterWorld();yield return Until(()=>e.Entered);
                Require(!e.board.State.Knows("door")&&!e.board.State.Knows("light"),"initial words");
                e.board.State.TryAcquireSentence("journey.world","positive world");yield return Until(()=>e.room.Opened&&!e.Busy);
                e.board.Panel.SetOpen(false);yield return null;
                var p=e.player;var j=e.journey;var hole=UnityEngine.Object.FindFirstObjectByType<WallPassage>();
                p.Teleport(new Vector3(0,.02f,0),Quaternion.identity);p.view.LookAt(e.chamberLight.transform);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;yield return null;
                Require(e.chamberLight.TryCollect(),"collect initial light");yield return Until(()=>!e.chamberLight.Collecting);
                p.enabled=false;p.Teleport(hole.Entrance.TransformPoint(new Vector3(0,-hole.sillHeight+.02f,-1.3f)),hole.Entrance.rotation);
                Walk(p,20,false,Vector2.zero);Walk(p,110,true);yield return null;
                Require(j.InCorridor&&hole.ReachedEntrance,"jump reaches corridor");
                // Try to leave the path sideways; native rail and explicit corridor constraint agree.
                Walk(p,180,false,Vector2.right);yield return null;
                Require(Mathf.Abs(j.Root.InverseTransformPoint(p.transform.position).x)<1,"path edge retains player");
                p.enabled=true;e.RequestExit();yield return Until(()=>e.DialogReady);
                Require(e.portableClosure.Isolated&&e.room.PortablePlacement,"portable closure in corridor");
                var frozen=p.transform.position;Walk(p,30,true,Vector2.one);Require(p.transform.position==frozen,"closure freezes XYZ");
                e.CancelExit();Require(!p.MovementLocked,"cancel unlocks");e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);
                Require(!e.portableClosure.Isolated,"reopen restores environment");
                j.allowCorridorClosure=false;e.RequestExit();yield return Until(()=>e.DialogReady);
                Require(e.room.Opened&&!e.portableClosure.Isolated,"optional corridor policy retains exit without folding");
                e.CancelExit();j.allowCorridorClosure=true;
                p.enabled=false;p.Teleport(j.Root.TransformPoint(new Vector3(0,.02f,2)),j.Root.rotation);yield return null;
                Walk(p,Mathf.CeilToInt((j.pathLength-7)/p.moveSpeed*120));yield return null;p.enabled=true;
                p.view.LookAt(j.Door.position);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;yield return null;
                Require(e.chamberLight.TryCollect(),"click door collection");yield return Until(()=>!e.chamberLight.Collecting);
                Require(e.board.State.Knows("door"),"door word acquired");
                p.enabled=false;Walk(p,220);yield return null;
                Require(j.Root.InverseTransformPoint(p.transform.position).z<j.pathLength,"closed door blocks passage");
                e.board.State.TryAcquireSentence("journey.door","positive door");yield return Until(()=>j.DoorProgress>=1);
                Walk(p,120);yield return null;p.enabled=true;
                Require(j.InSecondRoom&&!j.LightsOn&&j.Brightness==0,"new room starts dark");
                var first=j.Lamps[0];var before=first.transform.position;yield return new WaitForSecondsRealtime(.6f);
                Require(Vector3.Distance(before,first.transform.position)>.1f,"lamps move in darkness");
                Require(j.Lamps.All(l=>Physics.GetIgnoreCollision(l.GetComponent<Collider>(),p.GetComponent<CharacterController>())),"lamps ignore player");
                e.board.State.TryAcquireSentence("journey.light","positive light");yield return null;
                Require(j.LightsOn&&j.Brightness<1,"room lights fade in");yield return Until(()=>j.Brightness>=1);
                before=first.transform.position;yield return new WaitForSecondsRealtime(.5f);Require(Vector3.Distance(before,first.transform.position)>.1f,"lamps move when lit");
                var body=first.GetComponent<Rigidbody>();body.position=j.Root.TransformPoint(new Vector3(j.roomSize.x*.5f-.3f,4,j.pathLength+8));first.direction=j.Root.right;int bounced=first.BounceCount;
                yield return new WaitForSecondsRealtime(.3f);Require(first.BounceCount>bounced,"wall bounce");
                var second=j.Lamps[1];body.position=j.Root.TransformPoint(new Vector3(-.4f,4,j.pathLength+8));second.GetComponent<Rigidbody>().position=j.Root.TransformPoint(new Vector3(.4f,4,j.pathLength+8));first.direction=j.Root.right;second.direction=-j.Root.right;bounced=first.BounceCount;
                yield return new WaitForSecondsRealtime(.4f);Require(first.BounceCount>bounced,"lamp pair collision");
                e.RequestExit();yield return Until(()=>e.DialogReady);Require(e.portableClosure.Isolated,"second room closure");
                e.CancelExit();e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);
                Require(j.LightsOn&&j.Brightness==1&&!e.portableClosure.Isolated,"second room restores lit state");
                e.board.State.TryAcquireSentence("journey.off","negative light");yield return Until(()=>j.Brightness==0);
                Require(j.InSecondRoom&&!j.LightsOn,"off light affects current room");
                Result="PASS: corridor jump and bounds, portable closure/cancel/reopen, door collection/block/open, dark-room entry, continuous dark/lit motion, wall and lamp collision, player exclusion, second-room closure and local light commands";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
