using System;
using System.Collections;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class ChamberChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string message){if(!ok){Result="FAIL: "+message;throw new Exception(Result);}}
        public static void Run(){var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();Result="Running";e.StartCoroutine(RunChecks(e));}
        static IEnumerator Until(Func<bool> condition)
        {float end=Time.realtimeSinceStartup+20;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Check(condition(),"transition timeout");}
        static IEnumerator RunChecks(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                e.EnterWorld();yield return Until(()=>e.Entered);
                Check(!e.board.State.Knows("light"),"light initially locked");
                e.board.State.TryAcquireSentence("test.world","positive world");yield return Until(()=>e.room.Opened&&!e.Busy);
                e.board.Panel.SetOpen(false);yield return null;
                var light=e.chamberLight;var p=e.player;
                p.view.LookAt(light.transform);p.AdoptCurrentView();
                Cursor.lockState=CursorLockMode.Locked;yield return null;
                Check(light.LookingAtLight(),"floor lamp collectible from opening spawn");
                light.AdvancePointer(true,true,false,.3f);
                Check(light.Collecting,"ordinary left press collects immediately even on a long frame");
                light.AdvancePointer(false,false,true,0);yield return Until(()=>!light.Collecting);
                Check(e.board.State.Knows("light"),"light acquired");
                e.board.State.TryAcquireSentence("test.off","negative light");
                yield return Until(()=>!light.TargetOn);yield return null;
                Check(light.Brightness>0,"gradual fade");yield return Until(()=>light.Brightness==0);
                Check(!light.TryCollect(),"off light cannot collect");
                var hole=UnityEngine.Object.FindFirstObjectByType<WallPassage>();int chosen=hole.SelectedWall;
                Check(!hole.walls[chosen].GetComponent<Renderer>().enabled,"dark opening reveals the passage");
                e.board.State.TryAcquireSentence("test.on","positive light");
                var off=System.Linq.Enumerable.First(e.board.State.Groups,g=>g.RuleId=="light.off");
                e.board.State.ReturnToLibrary(off.Id,0,true);
                yield return Until(()=>light.Brightness==1);
                Check(hole.walls[chosen].GetComponent<Renderer>().enabled,"lit wall visually whole");
                e.board.State.ClearWorkspace();yield return null;Check(light.TargetOn,"splitting preserves lamp state");
                // Native CharacterController trajectory through the concealed opening.
                p.enabled=false;var origin=hole.Entrance.TransformPoint(new Vector3(0,-hole.sillHeight+.02f,-1.3f));
                p.Teleport(origin,hole.Entrance.rotation);
                var step=p.GetType().GetMethod("AdvanceMovement",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                for(int i=0;i<20;i++)step.Invoke(p,new object[]{Vector2.zero,true,1f/120,false});
                for(int i=0;i<110;i++)step.Invoke(p,new object[]{new Vector2(0,1),true,1f/120,i==0});
                yield return null;
                Check(hole.ReachedEntrance,"jump crosses concealed aperture and reaches landing");
                Check(hole.SelectedWall==chosen,"random wall stable during play");
                p.Teleport(new Vector3(0,.02f,0),Quaternion.identity);p.enabled=true;
                e.RequestExit();yield return Until(()=>e.DialogReady);e.CancelExit();
                Check(!p.MovementLocked&&!e.ConfirmingExit,"world close/cancel with light available");
                Check(hole.SelectedWall==chosen,"random wall stable after world close");
                Result="PASS: acquisition, light fade and re-enable, blocked dark collection, coexistence and split, concealed-opening jump, world close/cancel";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
