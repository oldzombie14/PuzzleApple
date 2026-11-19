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
        public static IEnumerator Wait(Func<bool> ready,string message,float seconds=35){
            float end=Time.realtimeSinceStartup+seconds;
            while(!ready() && Time.realtimeSinceStartup<end)yield return null;
            Check(ready(),message);
        }
        public static void Run(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Exercise(e));
        }
        public static IEnumerator UnlockEntrance(WorldEntry e){
            yield return UnlockFirstPassage(e);
            var door=e.journey.EntranceDoor;door.SetOpen(true);
            yield return Wait(()=>door.Progress==1,"unlock room entrance progress");
            door.SetOpen(false);yield return Wait(()=>door.Progress==0,"close unlocked room entrance");
        }
        public static IEnumerator UnlockFirstPassage(WorldEntry e){
            var gate=UnityEngine.Object.FindFirstObjectByType<WallPassage>().GetComponentsInChildren<ProgressLock>().Single(g=>g.passageReveal);
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
            Check(stone.sharedMaterial.shader==j.lampMaterial.shader &&
                door.Leaf.GetComponent<Renderer>().sharedMaterial==stone.sharedMaterial,"door and niche use plain lamp HDR material without wall outlines");
            Check(j.WordAt(reveal.GetComponent<MeshCollider>(),2)=="door","bevel shares the door interaction");
            var floor=j.floorSurface.Surface.transform;
            var bridge=door.transform.Find("Doorway floor");
            var roomEdge=floor.TransformPoint(new Vector3(0,.5f,.5f));
            var bridgeEdge=bridge.TransformPoint(new Vector3(0,.5f,-.5f));
            Check(Vector3.Distance(roomEdge,bridgeEdge)<.001f && bridge.GetComponent<Renderer>().sharedMaterial==j.floorSurface.Surface.sharedMaterial,"level floor meets without overlapping the threshold");
            s.TryAcquireSentence("ending.reject","positive door",out var denied);
            yield return Wait(()=>s.Find(denied.Id)==null,"rejected command retires");
            Check(j.DoorProgress==1 && j.EntranceDoor.Progress==1 && door.Lift==0,"unpowered slab stays closed while other eligible doors open");
            s.TryAcquireSentence("ending.on","positive light");yield return Wait(()=>j.Brightness==1,"lamps lit");
            s.TryAcquireSentence("ending.gather","light move door",out var gather);
            // Global scope also waits for tutorial spheres travelling across their room.
            yield return Wait(()=>s.Find(gather.Id)==null,"gather command retires",90);
            Check(j.Lamps.All(l=>l.Docked),"all lamps dock outside the door");
            yield return Wait(()=>door.Charged,"door glow plus all docked lamps fill total energy");
            var strips=door.transform.Cast<Transform>().Where(t=>t.name=="Charge fill").ToArray();
            Check(strips.Length==3 && strips.All(t=>t.gameObject.activeSelf) && strips[0].localPosition.x<0 && strips[2].localPosition.x>0,"continuous three-sided fill reaches the right foot");
            var mark=door.Leaf.Find("Carved light").GetComponent<Renderer>().sharedMaterial;
            yield return Wait(()=>mark.GetColor("_GlyphEmission").maxColorComponent>.28f,"full charge gradually illuminates inscriptions");
            Check(mark.GetColor("_GlyphEmission").maxColorComponent<=.31f,"charged inscription emission stays restrained");
            Check(door.Lift==0,"full charge does not open");
            s.TryAcquireSentence("ending.open","positive door",out var opened);
            yield return Wait(()=>door.Lift==1 && s.Find(opened.Id)==null,"rise and retire");
            Check(j.DoorProgress==1 && j.EntranceDoor.Progress==1,"earlier doors share the global open command");
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
            yield return Wait(()=>s.Find(noFloorTarget.Id)==null,"dark indoor floor has no target and retires");
            foreach(var text in new[]{"positive door","negative door"}){
                bool alreadyOpen=text=="positive door";
                j.SetDoor(alreadyOpen);j.EntranceDoor.SetOpen(alreadyOpen);
                yield return Wait(()=>j.DoorProgress==(alreadyOpen?1:0) && j.EntranceDoor.Progress==(alreadyOpen?1:0),"place other doors at the requested state before no-change feedback");
                int weak=e.feedback.WeakCount,strong=e.feedback.StrongCount;
                s.TryAcquireSentence("reject."+text,text,out var rejected);
                yield return Wait(()=>e.feedback.WeakCount>weak,"empty door gives weak feedback "+text);
                Check(e.feedback.StrongCount==strong && d.Lift==0 && j.DoorProgress==(alreadyOpen?1:0),"unpowered slab and already-set doors stay still");
                if(text=="door move")s.ReturnToLibrary(rejected.Id,0,true);
                else yield return Wait(()=>s.Find(rejected.Id)==null,"unpowered command retires");
            }
            j.SetLights(true,PassageJourney.LightSpace.FarRoom);yield return Wait(()=>j.Brightness==1,"lamps lit");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.12f,j.RoomStart+2)),j.Root.rotation);
            p.MovementLocked=true;yield return Wait(()=>j.InSecondRoom,"current room selects charging anchor");
            var show=e.sentenceRuntime.presentation;show.enabledShots=true;
            s.TryAcquireSentence("corridor.to.gate","light move door",out var gather);
            yield return Wait(()=>show.Playing && show.CanInterrupt,"gather shot starts and locks target");
            Check(Vector3.Angle(p.view.forward,(d.Leaf.position-p.view.position).normalized)<3,"shot faces the selected recessed door");
            yield return Wait(()=>s.Find(gather.Id)==null && d.Charged,"corridor command reaches gate",90);
            Check(j.Lamps.All(l=>l.Docked && d.transform.InverseTransformPoint(l.transform.position).z<0) && !j.DoorOpen,"lamps stay outside the energized threshold");
            var shot=new SentencePresentation.Shot("light.move",SentencePresentation.Focus.LightGroup);
            var target=typeof(SentencePresentation).GetMethod("Target",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Vector3 Aim()=> (Vector3)target.Invoke(show,new object[]{shot,true});
            var mouth=j.Root.TransformPoint(new Vector3(0,1.5f,j.RoomStart));
            p.Teleport(mouth+j.Root.forward*7-Vector3.up*1.4f,Quaternion.LookRotation(-j.Root.forward));
            yield return Wait(()=>j.InSecondRoom,"camera samples the far room");
            Check(j.Root.InverseTransformPoint(Aim()).z>=j.RoomStart,"lit room keeps its local motion focus even facing away");
            p.Teleport(mouth+j.Root.forward*1.5f-Vector3.up*1.4f,Quaternion.LookRotation(-j.Root.forward));
            yield return null;
            Check(j.Lamps.Any(l=>Vector3.Distance(l.transform.position,Aim())<.05f),"a lit room keeps its lamp as the subject near the door");
            p.Teleport(mouth+j.Root.forward*8-Vector3.up*1.4f,j.Root.rotation);
            j.SetLights(false,PassageJourney.LightSpace.FarRoom);
            yield return Wait(()=>!j.LampsLit,"dark far room");
            Check(j.Root.InverseTransformPoint(Aim()).z<j.RoomStart && j.ReturnStars.Brightness>0,"dark room motion focuses the still-lit stars in its unlocked scope");
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
            state.TryAcquireSentence("ending.cycle","door move",out var cycle);
            yield return Wait(()=>d.Lift>.3f && d.Lift<.8f,"charged door moves");
            state.TryAcquireSentence("ending.freeze","negative move",out var freeze);
            float held=d.Lift;yield return new WaitForSecondsRealtime(.26f);
            Check(d.Lift==held,"no move freezes door");
            state.ReturnToLibrary(freeze.Id,0,true);
            state.TryAcquireSentence("ending.takeover","positive door",out var open);
            yield return Wait(()=>state.Find(open.Id)==null && d.Lift>.97f,"on completes during cycle");
            Check(j.DoorProgress>.97f && j.EntranceDoor.Progress>.97f,"on door takes priority over every door cycle");
            yield return Wait(()=>d.Lift<.72f,"cycle resumes after command");
            state.ReturnToLibrary(cycle.Id,0,true);yield return null;yield return null;
            held=d.Lift;yield return new WaitForSecondsRealtime(.25f);
            Check(Mathf.Abs(held-d.Lift)<.001f,"splitting cycle holds height");
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
            yield return Wait(()=>!j.Stars.NeedsGather && state.Find(stars.Id)==null,"first corridor stars dock and retire");
            Result="PASS: charged cycle, command priority and resumption, split, freeze, closed-world idle, white-door star gathering";
        }
        static IEnumerator Exercise(WorldEntry e){
            yield return OpenEnding(e);
            var d=e.journey.EndingDoor;var s=e.board.State;var p=e.player;
            p.Teleport(d.transform.TransformPoint(new Vector3(0,.08f,d.LeafZ+.14f)),d.transform.rotation);
            s.TryAcquireSentence("ending.lower","negative door",out var close);
            yield return Wait(()=>d.Stunned,"slab stuns while closing");
            yield return Wait(()=>!d.Stunned && s.Find(close.Id)==null,"stair-side respawn and command retirement");
            Check(d.transform.InverseTransformPoint(p.transform.position).z>d.LeafZ,"respawn stays on stair side");
            p.Teleport(d.RoomLanding,d.transform.rotation);yield return null;
            s.TryAcquireSentence("ending.reopen","positive door",out var open);
            yield return Wait(()=>d.Lift==1 && s.Find(open.Id)==null,"reopen");
            p.Teleport(d.transform.TransformPoint(new Vector3(0,.08f,d.LeafZ-.14f)),d.transform.rotation);
            p.MovementLocked=true;
            s.TryAcquireSentence("ending.drain","light move",out var drift);
            yield return Wait(()=>d.Falling,"departure drains bars and releases slab");
            yield return Wait(()=>d.Stunned,"fall impact");yield return Wait(()=>!d.Stunned && d.Lift==0,"room-side respawn");
            Check(d.transform.InverseTransformPoint(p.transform.position).z<d.LeafZ && s.Find(drift.Id)==drift,"correct side and continuous sentence survives");
            Result="PASS: rejects empty door, gathering and parking, total energy, controlled lift and closure, power loss, impacts and both respawn sides";
        }
    }
}
