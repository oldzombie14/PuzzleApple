using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace PuzzleApple.V4.Editor
{
    public static class PlaytestPolishChecks
    {
        public static string Result { get; private set; }
        static void Require(bool ok,string message) { if(!ok) { Result="FAIL: "+message; throw new InvalidOperationException(Result); } }
        public static void Run(bool floorFirst) { Result="Running"; var f=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();f.StartCoroutine(Check(f,floorFirst)); }
        static IEnumerator Walk(OpeningFlow f,float distance)
        {
            var method=typeof(FirstPersonController).GetMethod("AdvanceMovement",BindingFlags.NonPublic|BindingFlags.Instance);
            float start=f.player.WalkedDistance;
            var origin=f.player.transform.position;
            for(int i=0;i<600 && f.player.WalkedDistance-start<distance;i++)
            {
                method.Invoke(f.player,new object[]{Vector2.up,true,1f/60});
                Require(Mathf.Abs(f.player.transform.position.x-origin.x)<.001f,"straight walk has no lateral drift");
                Require(Mathf.Abs(f.player.view.localEulerAngles.y)<.001f,"straight walk has no camera yaw sway");
                yield return null;
            }
            Require(f.player.WalkedDistance-start>=distance,"walk advances real controller distance");
        }
        static IEnumerator Floor(OpeningFlow f)
        {
            f.player.enabled=true;f.player.view.localRotation=Quaternion.Euler(75,0,0);
            typeof(FirstPersonController).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(f.player,75f);
            Cursor.lockState=CursorLockMode.Locked;Physics.SyncTransforms();
            Require(f.TryCollectGround(),"real floor ray starts collection");
            Require(f.lightField.ActiveCount==0,"no lamps during floor collection");
            yield return new WaitForSecondsRealtime(f.wordDisplayDuration+.2f);
            Require(f.board.State.Knows("ground"),"floor granted at flight end");
            f.player.enabled=false;f.player.view.localRotation=Quaternion.identity;
            typeof(FirstPersonController).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(f.player,0f);
        }
        static IEnumerator Check(OpeningFlow f,bool floorFirst)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                Require(f.lightField.ActiveCount==0&&!f.lightField.Released,"no initial lamps");
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();yield return new WaitForSecondsRealtime(2);
                foreach(var id in new[]{"positive","negative","world"}) {f.library.Select(id);Require(f.library.SnapshotImage.texture,"entry memory "+id);}
                f.library.Show(false);f.board.Panel.SetOpen(false);yield return null;
                f.player.Teleport(new Vector3(0,1,0),Quaternion.identity);f.player.enabled=false;
                if(floorFirst)yield return Floor(f);
                yield return Walk(f,f.player.learnMoveDistance+.1f);
                Require(f.Collecting&&!f.board.State.Knows("move")&&f.lightField.ActiveCount==0,"no lamps during Move flight");
                yield return new WaitForSecondsRealtime(f.wordDisplayDuration+.2f);
                Require(f.board.State.Knows("move"),"Move collection completed");
                if(!floorFirst)
                {
                    Require(!f.lightField.Released,"Move alone does not release lamps");
                    yield return Floor(f);
                    yield return new WaitForSecondsRealtime(.3f);
                    Require(!f.lightField.Released,"stationary after Floor does not release lamps");
                    yield return Walk(f,.4f);Require(!f.lightField.Released,"short partial walk does not release lamps");
                    yield return Walk(f,1f);
                }
                yield return new WaitForSecondsRealtime(.4f);
                Require(f.lightField.Released&&f.lightField.ActiveCount>0,"lamps released after correct sequence");
                Require(f.lightField.ActiveLamps.All(l=>Vector3.ProjectOnPlane(l.transform.position-f.player.transform.position,Vector3.up).magnitude>18),"first lamps appear in distance");
                f.library.Select("ground");var photo=(Texture2D)f.library.SnapshotImage.texture;
                System.IO.Directory.CreateDirectory("Temp/V4Polish");System.IO.File.WriteAllBytes("Temp/V4Polish/floor.png",photo.EncodeToPNG());f.library.Show(false);
                f.door.Reveal(f.player.view);yield return new WaitForSecondsRealtime(4);
                f.RememberDoor();f.board.State.TryAcquireWord("check.door",f.board.Catalog.Word("door"));f.library.Select("door");
                System.IO.File.WriteAllBytes("Temp/V4Polish/door.png",((Texture2D)f.library.SnapshotImage.texture).EncodeToPNG());
                Result="PASS: "+(floorFirst?"Floor then Move":"Move then Floor then walk")+", distance gate, straight movement, distant lamps and memories";
            }
            finally {Application.runInBackground=background;}
        }
    }
}
