using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class ChargeDoorChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string message){if(ok)return;Result="FAIL: "+message;throw new Exception(Result);}
        public static IEnumerator Wait(Func<bool> ready,string message,float seconds=35, Func<string> details=null){
            float end=Time.realtimeSinceStartup+seconds;
            while(!ready() && Time.realtimeSinceStartup<end)yield return null;
            if (!ready()) Check(false, details == null ? message : message + " (" + details() + ")");
        }
        public static void Run(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Exercise(e));
        }
        public static void PositionAtDoor(WorldEntry e, PassageEntranceDoor door, float distance, float x=0)
        {
            e.player.Teleport(door.transform.TransformPoint(new Vector3(x,.08f,distance)),door.transform.rotation);
        }
        public static IEnumerator ReachRoomThree(WorldEntry e)
        {
            var j=e.journey;
            yield return UnlockFirstPassage(e);
            if(!j.Door.GetComponentInChildren<ProgressLock>().Unlocked){
                j.SetDoor(true);
                yield return Wait(()=>j.DoorProgress==1,"unlock the rail approach");
            }
            PositionAtDoor(e,j.GarageDoor,.9f,j.PathWidth*.5f+.15f);
            yield return Wait(()=>j.GarageDoor.Progress==1,"garage senses player");
            PositionAtDoor(e,j.GarageExitDoor,-1);
            j.RailRoom.SetOn(true);
            yield return Wait(()=>j.GarageExitDoor.Progress==1,"lit bracket supplies exit lift");
            PositionAtDoor(e,j.EntranceDoor,-1);
            yield return Wait(()=>j.EntranceDoor.Progress==1,"approach opens room three");
        }
        public static IEnumerator UnlockEntrance(WorldEntry e){
            yield return ReachRoomThree(e);
            PositionAtDoor(e,e.journey.EntranceDoor,4);
            yield return Wait(()=>e.journey.EntranceDoor.Progress==0,"closed room three entrance");
        }
        // Cross-space timing fixtures keep the graph connected after physically unlocking the lifts.
        // The route checks exercise the actual proximity boundaries independently.
        internal static void ConnectTimingFixture(PassageJourney journey,bool hold){
            foreach(var lift in new[]{journey.GarageDoor,journey.GarageExitDoor,journey.EntranceDoor})
                lift.GetComponentInChildren<ProgressLock>().BindBoundary(hold?null:lift);
        }
        public static IEnumerator UnlockFirstPassage(WorldEntry e){
            var gate=UnityEngine.Object.FindFirstObjectByType<WallPassage>().GetComponentsInChildren<ProgressLock>().Single(g=>g.BoundarySource is WallPassage);
            if(gate.Unlocked)yield break;
            bool restoreOn=e.chamberLight.TargetOn;
            e.chamberLight.SetOn(false);yield return Wait(()=>gate.Unlocked,"unlock first-passage reveal before testing later rooms");
            if(restoreOn){e.chamberLight.SetOn(true);yield return Wait(()=>e.chamberLight.Brightness==1,"restore chamber lighting after progress setup");}
        }
        public static IEnumerator OpenEnding(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"unfold");
            var j=e.journey;var door=j.EndingDoor;
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"unlock white-door progress");
            j.SetDoor(false);yield return Wait(()=>j.DoorProgress==0,"close unlocked white door");
            yield return UnlockEntrance(e);
            var s=e.board.State;var p=e.player;
            e.sentenceRuntime.presentation.enabledShots=false;e.board.Panel.SetOpen(false);
            p.Teleport(door.RoomLanding,door.transform.rotation);yield return Wait(()=>j.InSecondRoom,"in far room");
            Check(!j.floorSurface.MuralAvailable && !j.floorSurface.HoleOpen,"far room floor and wall");
            Check(j.WordAt(door.Leaf.GetComponent<Collider>(),2)=="door","slab collectible");
            var wall=j.Root.Find("Room far wall").GetComponent<Renderer>();
            var reveal=door.transform.Find("Stone reveal");
            var stone=reveal.GetComponent<Renderer>();
            Check(stone.sharedMaterial.shader==wall.sharedMaterial.shader &&
                door.Leaf.GetComponent<Renderer>().sharedMaterial==stone.sharedMaterial,"door and niche use the wall shader with independent emission without wall outlines");
            Check(j.WordAt(reveal.GetComponent<MeshCollider>(),2)=="door","bevel shares the door interaction");
            var floor=j.floorSurface.Surface.transform;
            var bridge=door.transform.Find("Doorway floor");
            var roomEdge=floor.TransformPoint(new Vector3(0,.5f,.5f));
            var bridgeEdge=bridge.TransformPoint(new Vector3(0,.5f,-.5f));
            Check(Vector3.Distance(roomEdge,bridgeEdge)<.001f && bridge.GetComponent<Renderer>().sharedMaterial==j.floorSurface.Surface.sharedMaterial,"level floor meets without overlapping the threshold");
            Check(j.EntranceDoor.Progress==0 && door.Lift==0,"distant lifts stay closed");
            s.TryAcquireSentence("ending.on","positive light",out var lightOn);
            yield return Wait(()=>j.Brightness==1 && s.Find(lightOn.Id)==null,"lamps lit and light command finished");
            s.TryAcquireSentence("ending.gather","light move door",out var gather);
            Check(gather.ExecutionLocked,"gather starts after the light command");
            // Global scope also waits for tutorial spheres travelling across their room.
            yield return Wait(()=>s.Find(gather.Id)==null,"gather command retires",90);
            Check(j.Lamps.All(l=>l.Docked) && j.Lamps.Any(l=>!l.AtChargeDoor) && j.Lamps.Where(l=>l.AtChargeDoor).Sum(l=>l.ChargePower)>=j.ChargePowerRequired,"both doors receive lamps and the slab receives enough power");
            yield return Wait(()=>door.Charged,"door glow plus all docked lamps fill total energy");
            var strips=door.transform.Cast<Transform>().Where(t=>t.name=="Charge fill").ToArray();
            Check(strips.Length==3 && strips.All(t=>t.gameObject.activeSelf) && strips[0].localPosition.x<0 && strips[2].localPosition.x>0,"continuous three-sided fill reaches the right foot");
            var mark=door.Leaf.Find("Carved light").GetComponent<Renderer>().sharedMaterial;
            yield return Wait(()=>mark.GetColor("_GlyphEmission").maxColorComponent>.28f,"full charge gradually illuminates inscriptions");
            Check(mark.GetColor("_GlyphEmission").maxColorComponent<=.31f,"charged inscription emission stays restrained");
            Check(door.Lift==0,"fully charged door stays closed at a distance");
            s.TryAcquireSentence("ending.manual","positive door",out var manual);
            yield return new WaitForSeconds(.2f);
            Check(door.Lift==0 && !manual.Spoken,"manual command cannot open the charged terminal");
            p.Teleport(door.transform.TransformPoint(new Vector3(0,.08f,door.LeafZ-1)),door.transform.rotation);
            yield return Wait(()=>door.Lift==1,"full power and player proximity raise the slab");
        }
        public static void RunApproach(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Approach(e));
        }
        static IEnumerator Approach(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open world");
            var j=e.journey;var d=j.EndingDoor;var p=e.player;var board=e.board;var s=board.State;
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"unlock white door");
            j.SetDoor(false);yield return Wait(()=>j.DoorProgress==0,"close unlocked white door");
            yield return UnlockEntrance(e);
            p.Teleport(d.RoomLanding,d.transform.rotation);yield return Wait(()=>j.InSecondRoom,"far room");
            e.sentenceRuntime.presentation.enabledShots=false;
            board.Panel.SetOpen(true);yield return Wait(()=>board.Panel.FullyOpen,"visible feedback");
            s.TryAcquireSentence("reject.indoor.floor","floor move light",out var noFloorTarget);
            Check(!noFloorTarget.Spoken && !s.JudgementLocked,"dark indoor floor has no target and leaves judgement available");
            foreach(var text in new[]{"positive door","negative door"}){
                bool alreadyOpen=text=="positive door";
                j.SetDoor(alreadyOpen);
                yield return Wait(()=>j.DoorProgress==(alreadyOpen?1:0),"place other doors at the requested state before no-change feedback");
                int weak=e.feedback.WeakCount,strong=e.feedback.StrongCount;
                s.TryAcquireSentence("reject."+text,text,out var rejected);
                yield return Wait(()=>e.feedback.WeakCount>weak,"empty door gives weak feedback "+text);
                Check(e.feedback.StrongCount==strong && d.Lift==0 && j.DoorProgress==(alreadyOpen?1:0),"unpowered slab and already-set doors stay still");
                Check(!rejected.Spoken && !s.JudgementLocked,"unpowered command stays a draft");
            }
            j.SetLights(true,PassageJourney.LightSpace.FarRoom);yield return Wait(()=>j.Brightness==1,"lamps lit");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.12f,j.RoomStart+2)),j.Root.rotation);
            p.MovementLocked=true;yield return Wait(()=>j.InSecondRoom,"current room selects charging anchor");
            var show=e.sentenceRuntime.presentation;show.enabledShots=true;
            var viewBefore=p.view.rotation;
            s.TryAcquireSentence("corridor.to.gate","light move door",out var gather);
            yield return Wait(()=>show.Playing && show.CanInterrupt,"gather shot starts and locks target");
            Check(Quaternion.Angle(p.view.rotation,viewBefore)<.1f,"gather preserves the current view");
            yield return Wait(()=>s.Find(gather.Id)==null && d.Charged,"corridor command reaches gate",90);
            Check(j.Lamps.All(l=>l.Docked && d.transform.InverseTransformPoint(l.transform.position).z<0) && !j.DoorOpen,"lamps stay outside the energized threshold");
            Check(j.Lamps.Any(l=>l.AtChargeDoor) && j.Lamps.Any(l=>!l.AtChargeDoor),"gather reaches both doors");
            Result="PASS: all unpowered commands, rejected feedback, corridor camera and approach, parked lamps and independent swing door";
        }
        public static void RunEnergy(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(MeasureEnergy(e));
        }
        static IEnumerator MeasureEnergy(WorldEntry e){
            try{yield return RoomThreeChecks.Energy(e);}finally{Result=RoomThreeChecks.Result;}
        }
        public static void RunControls(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Controls(e));
        }
        static IEnumerator Controls(WorldEntry e){
            yield return OpenEnding(e);
            var j=e.journey;var d=j.EndingDoor;var p=e.player;var state=e.board.State;
            var held=d.Lift;
            var lamp=j.Lamps[0];var body=lamp.GetComponent<Rigidbody>();
            e.RequestExit();yield return Wait(()=>e.DialogReady,"off world closes");
            held=d.Lift;float energy=d.TotalEnergy;var location=body.position;
            yield return new WaitForSecondsRealtime(.3f);
            Check(held==d.Lift && energy==d.TotalEnergy && Vector3.Distance(location,body.position)>.00001f,"closed world freezes door while docked lamp hovers");
            e.CancelExit();e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"reopen world");
            e.board.Panel.SetOpen(false);
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.pathLength-2)),j.Root.rotation);
            yield return Wait(()=>j.InCorridor,"first corridor");
            j.SetLights(true,PassageJourney.LightSpace.FirstPath);
            state.TryAcquireSentence("ending.stars","light move door",out var stars);
            float start = Time.time;
            string GatherDetails()
            {
                if (state.Find(stars.Id) == null) return "command retired, needsGather=" + j.Stars.NeedsGather;
                var scope = e.sentenceRuntime.EffectScope(stars);
                var pending = e.Entities.Query<SentenceMotionOutput>(scope)
                    .Where(output => output.WouldChange(stars, scope) && output.Owns(SentenceMotion.LightToDoor,stars.Established))
                    .Select(output => e.Entities.SpacesOf(output).ToString());
                return "simulated=" + (Time.time - start) +
                    ", needsGather=" + j.Stars.NeedsGather + ", applied=" + j.Stars.MotionOutput.HasMotion(SentenceMotion.LightToDoor) +
                    ", group=" + (state.Find(stars.Id) != null) + ", pending=" + string.Join(", ", pending);
            }
            yield return Wait(() => !j.Stars.NeedsGather && state.Find(stars.Id) == null,
                "first corridor stars dock and retire", details: GatherDetails);
            Result="PASS: powered proximity lift, closed-world idle and white-door star gathering";
        }
        static IEnumerator Exercise(WorldEntry e){
            yield return OpenEnding(e);
            var d=e.journey.EndingDoor;var s=e.board.State;var p=e.player;
            p.Teleport(d.transform.TransformPoint(new Vector3(0,.08f,d.LeafZ+.14f)),d.transform.rotation);
            yield return new WaitForSeconds(.2f);
            Check(d.Lift==1 && !d.Stunned,"powered doorway stays open around its occupant");
            s.TryAcquireSentence("ending.close","negative door",out var manual);
            yield return new WaitForSeconds(.25f);
            Check(d.Lift==1 && !manual.Spoken,"manual close cannot override occupied sensor");
            p.Teleport(d.RoomLanding,d.transform.rotation);
            yield return Wait(()=>d.Lift==0,"stepping away lowers the slab");
            p.Teleport(d.transform.TransformPoint(new Vector3(0,.08f,d.LeafZ-1)),d.transform.rotation);
            yield return Wait(()=>d.Lift==1,"approaching opens it again");
            p.Teleport(d.transform.TransformPoint(new Vector3(0,.08f,d.LeafZ-.14f)),d.transform.rotation);
            p.MovementLocked=true;
            s.TryAcquireSentence("ending.drain","light move",out var drift);
            yield return Wait(()=>d.Falling,"departure drains bars and releases slab");
            var falling = JsonUtility.FromJson<ChargeDoor.LiftState>(JsonUtility.ToJson(d.CaptureState()));
            Check(d.TryRestoreState(falling) && d.Falling && d.Lift == falling.lift, "resume the same fall without restarting warning");
            var unsafeDrop = falling;
            unsafeDrop.lift = .01f;
            Check(!d.TryRestoreState(unsafeDrop) && d.Lift == falling.lift, "restoring a lower slab cannot sweep through the player");
            yield return Wait(()=>d.Stunned,"fall impact");yield return Wait(()=>!d.Stunned && d.Lift==0,"room-side respawn");
            Check(d.transform.InverseTransformPoint(p.transform.position).z<d.LeafZ && s.Find(drift.Id)==drift,"correct side and continuous sentence survives");
            Result="PASS: rejects empty door, gathering and parking, total energy, proximity lift and closure, manual exclusion, power loss, impact and room-side respawn";
        }
    }
}
