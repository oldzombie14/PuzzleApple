using System;
using System.Collections;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    public static class ConnectivityChecks
    {
        public static string Result {get;private set;}
        static void Check(bool value,string label){if(!value){Result="FAIL: "+label;throw new InvalidOperationException(Result);}}
        static IEnumerator Wait(Func<bool> done,string label){float end=Time.realtimeSinceStartup+40;while(!done()&&Time.realtimeSinceStartup<end)yield return null;Check(done(),label);}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Exercise(e));}
        internal static IEnumerator Exercise(WorldEntry e)
        {
            e.OpenWorld(-1);
            yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey; var p=e.player; var b=e.board;
            b.Panel.SetOpen(false); e.sentenceRuntime.presentation.enabledShots=false;
            Check(j.SentenceScope()==Space.Chamber,"start in chamber only");
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            var white=VitruvianChecks.Say(b,"positive","door");
            yield return Wait(()=>j.DoorProgress==1 && b.State.Find(white.Id)==null,"manual white door opens");
            Check((ProgressLock.Accessible(Space.All)&Space.RailRoom)==0,"rail room remains locked");
            Check(j.GarageDoor.Energy==1 && j.GarageExitDoor.Energy<1 && j.RailRoom.Brightness==0,"full entrance and incomplete exit");

            var platform=j.Platform;
            p.Teleport(platform.transform.position+Vector3.up*.23f,j.Root.rotation);
            yield return Wait(()=>p.StandingOn(platform.Deck),"board the stationary floor");
            var move=VitruvianChecks.Say(b,"floor","move");
            yield return Wait(()=>j.PlayerSpace==Space.RailRoom && p.StandingOn(platform.Deck),"ride through opening entrance");
            Check(j.GarageDoor.GetComponentInChildren<ProgressLock>().Unlocked && j.GarageDoor.Clearance>=p.GetComponent<CharacterController>().height,"passable lift unlocks rail room");
            p.Teleport(j.RailRoom.SafeLanding,j.Root.rotation);
            yield return Wait(()=>j.GarageDoor.Progress==0,"walk clear to collect from inside");
            Check(j.SentenceScope()==Space.RailRoom,"closed garage cuts off outer rail");
            yield return Wait(()=>j.Root.InverseTransformPoint(platform.transform.position).z<j.GarageStart-2,"empty board crosses closed gate");
            Check(j.GarageDoor.Progress==0,"empty board never trips sensor");

            p.view.LookAt(j.GarageDoor.Leaf.position); p.AdoptCurrentView(); Cursor.lockState=CursorLockMode.Locked;
            yield return null;
            Check(e.chamberLight.TryCollect(),"collect from the inner bank");
            yield return Wait(()=>j.EndingDoor.LessonAcquired && b.Panel.FullyOpen,"shared live light door sentence");
            Check(j.GarageDoor.Energy==1 && j.GarageExitDoor.Energy>0,"ownership transfers both doors");
            var lesson=System.Linq.Enumerable.First(b.State.Groups,g=>g.Intent?.Is(SentenceAction.Illuminate,SentenceCapabilityCatalog.Default.door)==true);
            b.State.ReturnToLibrary(lesson.Id,0,true);
            yield return Wait(()=>j.GarageDoor.Energy==0 && j.GarageExitDoor.Energy==0,"removing light door extinguishes both frames");

            // Board the returning platform just inside an unpowered entrance.
            yield return Wait(()=>j.Root.InverseTransformPoint(platform.transform.position).z>j.GarageStart+2.1f &&
                Vector3.Dot(platform.TravelDirection,j.Root.forward)<0,"board returning inside");
            p.Teleport(platform.transform.position+Vector3.up*.23f,j.Root.rotation);
            yield return Wait(()=>p.StandingOn(platform.Deck),"return rider grounded");
            yield return Wait(()=>Vector3.Dot(platform.TravelDirection,j.Root.forward)>0,"blocked rider and floor reverse together");
            Check(p.StandingOn(platform.Deck) && j.PlayerSpace==Space.RailRoom && j.GarageDoor.Progress==0,"rider stays aboard inside");
            b.Panel.SetOpen(false);
            p.Teleport(j.Root.TransformPoint(new Vector3(0,-2.5f,j.GarageStart+1)),j.Root.rotation);
            yield return Wait(()=>platform.Respawning,"fall in room trench");
            yield return Wait(()=>!platform.Respawning,"respawn indoors");
            Check(Vector3.Distance(p.transform.position,j.RailRoom.SafeLanding)<.35f,"fixed bank respawn");
            b.Panel.SetOpen(false);
            VitruvianChecks.Say(b,"light","door");
            ChargeDoorChecks.PositionAtDoor(e,j.GarageExitDoor,-1);
            yield return null; yield return null;
            Check(j.GarageExitDoor.Energy<1 && j.GarageExitDoor.Progress==0,"near an incomplete door");
            ChargeDoorChecks.PositionAtDoor(e,j.GarageExitDoor,-4);
            yield return null;
            var on=VitruvianChecks.Say(b,"positive","light");
            yield return Wait(()=>b.State.Find(on.Id)==null && j.GarageExitDoor.Energy==1,"fixed cube fills the exit");
            Check(j.GarageExitDoor.Progress==0 && j.Brightness==0,"charging at a distance leaves both rooms separated");
            ChargeDoorChecks.PositionAtDoor(e,j.GarageExitDoor,-1);
            yield return Wait(()=>j.GarageExitDoor.Progress==1,"player opens charged exit");
            Check((j.SentenceScope()&Space.ApproachPath)!=0 && (j.SentenceScope()&Space.FarRoom)==0,"only walkway joins");
            float edge=j.RoomStart-RailChargeRoom.LandingDepth-RailChargeRoom.JumpGap;
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,edge-.3f)),j.Root.rotation);
            yield return Wait(()=>p.GetComponent<CharacterController>().isGrounded,"grounded at approach walkway edge");
            var advance=typeof(FirstPersonController).GetMethod("AdvanceMovement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int frame=0;frame<95;frame++){
                advance.Invoke(p,new object[]{frame<65?Vector2.up:Vector2.zero,true,1f/120,frame==0});
            }
            yield return null;
            Check(j.PlayerSpace==Space.ApproachPath && p.GetComponent<CharacterController>().isGrounded &&
                j.Root.InverseTransformPoint(p.transform.position).z>j.RoomStart-RailChargeRoom.LandingDepth,"ordinary jump lands on fixed doorstep");
            yield return Wait(()=>j.EntranceDoor.Progress==1,"doorstep opens far room");
            ChargeDoorChecks.PositionAtDoor(e,j.EntranceDoor,4);
            yield return Wait(()=>j.EntranceDoor.Progress==0,"leaving closes far entrance");
            yield return null;
            Check(j.SentenceScope()==Space.FarRoom && j.Brightness==0,"far room remains dark and isolates");
            Check((ProgressLock.Accessible(Space.All)&Space.ApproachPath)!=0,"closing retains previous unlocks");
            foreach(var words in new[]{new[]{"positive","door"},new[]{"negative","door"},new[]{"door","move"}}){
                VitruvianChecks.Say(b,words); yield return null; yield return null;
                Check(j.EntranceDoor.Progress==0 && j.EndingDoor.Lift==0,"manual sentence cannot drive a sensor lift");
            }
            Result="PASS: occupied approach, empty rail, onboard reversal, two-sided lesson, power loss, fixed respawn, local lamp, chained unlocks, closed scopes and manual door exclusion";
        }
    }
}
