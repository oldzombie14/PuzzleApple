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
        public static IEnumerator OpenEnding(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"unfold");
            var j=e.journey;var door=j.EndingDoor;
            var s=e.board.State;var p=e.player;
            e.sentenceRuntime.presentation.enabledShots=false;e.board.Panel.SetOpen(false);
            p.Teleport(door.RoomLanding,door.transform.rotation);yield return Wait(()=>j.InSecondRoom,"in far room");
            Check(!j.floorSurface.MuralAvailable && !j.floorSurface.HoleOpen,"far room floor and wall");
            Check(j.WordAt(door.Leaf.GetComponent<Collider>(),2)=="door","slab collectible");
            var wall=j.Root.Find("Room far wall").GetComponent<Renderer>();
            var reveal=door.transform.Find("Stone reveal");
            var stone=reveal.GetComponent<Renderer>();
            Check(stone.sharedMaterial==wall.sharedMaterial && stone.gameObject.layer==wall.gameObject.layer &&
                door.Leaf.GetComponent<Renderer>().sharedMaterial==wall.sharedMaterial,"single bevel and leaf share the wall lighting");
            Check(j.WordAt(reveal.GetComponent<MeshCollider>(),2)=="door","bevel shares the door interaction");
            var floor=j.floorSurface.Surface.transform;
            var bridge=door.transform.Find("Doorway floor");
            var roomEdge=floor.TransformPoint(new Vector3(0,.5f,.5f));
            var bridgeEdge=bridge.TransformPoint(new Vector3(0,.5f,-.5f));
            Check(Vector3.Distance(roomEdge,bridgeEdge)<.001f && bridge.GetComponent<Renderer>().sharedMaterial==j.floorSurface.Surface.sharedMaterial,"level floor meets without overlapping the threshold");
            s.TryAcquireSentence("ending.reject","positive door",out var denied);
            yield return Wait(()=>s.Find(denied.Id)==null,"rejected command retires");
            Check(!j.DoorOpen && door.Lift==0,"neither door opens before charging");
            s.TryAcquireSentence("ending.on","positive light");yield return Wait(()=>j.Brightness==1,"lamps lit");
            s.TryAcquireSentence("ending.gather","light move door",out var gather);
            yield return Wait(()=>s.Find(gather.Id)==null,"gather command retires");
            Check(j.Lamps.All(l=>l.Docked),"all lamps dock outside the door");
            yield return Wait(()=>door.Charged,"three charge bars full");
            var strips=door.transform.Cast<Transform>().Where(t=>t.name=="Charge fill").ToArray();
            Check(strips.Length==3 && strips.All(t=>t.gameObject.activeSelf) && strips[0].localPosition.x<0 && strips[2].localPosition.x>0,"continuous three-sided fill reaches the right foot");
            var mark=door.Leaf.Find("Carved light").GetComponent<Renderer>().sharedMaterial;
            yield return Wait(()=>mark.GetColor("_GlyphEmission").maxColorComponent>2.8f,"full charge gradually illuminates inscriptions");
            Check(door.Lift==0,"full charge does not open");
            s.TryAcquireSentence("ending.open","positive door",out var opened);
            yield return Wait(()=>door.Lift==1 && s.Find(opened.Id)==null,"rise and retire");
            Check(!j.DoorOpen,"white swing door stays closed");
        }
        public static void RunApproach(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Approach(e));
        }
        static IEnumerator Approach(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open world");
            var j=e.journey;var d=j.EndingDoor;var p=e.player;var board=e.board;var s=board.State;
            p.Teleport(d.RoomLanding,d.transform.rotation);yield return Wait(()=>j.InSecondRoom,"far room");
            e.sentenceRuntime.presentation.enabledShots=false;
            board.Panel.SetOpen(true);yield return Wait(()=>board.Panel.FullyOpen,"visible feedback");
            s.TryAcquireSentence("reject.indoor.floor","floor move light",out var noFloorTarget);
            yield return Wait(()=>s.Find(noFloorTarget.Id)==null,"dark indoor floor has no target and retires");
            foreach(var text in new[]{"positive door","negative door","door move"}){
                int weak=e.feedback.WeakCount,strong=e.feedback.StrongCount;
                s.TryAcquireSentence("reject."+text,text,out var rejected);
                yield return Wait(()=>e.feedback.WeakCount>weak,"empty door gives weak feedback "+text);
                Check(e.feedback.StrongCount==strong && d.Lift==0 && !j.DoorOpen,"empty doors stay still");
                if(text=="door move")s.ReturnToLibrary(rejected.Id,0,true);
                else yield return Wait(()=>s.Find(rejected.Id)==null,"unpowered command retires");
            }
            j.SetLights(true,PassageJourney.LightSpace.FarRoom);yield return Wait(()=>j.Brightness==1,"lamps lit");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.12f,j.RoomStart-2)),j.Root.rotation);
            p.MovementLocked=true;yield return Wait(()=>j.InReturnPath,"on return path");
            var show=e.sentenceRuntime.presentation;show.enabledShots=true;
            s.TryAcquireSentence("corridor.to.gate","light move door",out var gather);
            yield return Wait(()=>show.Playing && show.CanInterrupt,"gather shot starts and locks target");
            Check(Vector3.Angle(p.view.forward,(d.Leaf.position-p.view.position).normalized)<3,"shot faces recessed door from corridor");
            yield return Wait(()=>s.Find(gather.Id)==null && d.Charged,"corridor command reaches gate");
            Check(j.Lamps.All(l=>l.Docked && d.transform.InverseTransformPoint(l.transform.position).z<0) && !j.DoorOpen,"lamps stay outside the energized threshold");
            var shot=new SentencePresentation.Shot("light.move",SentencePresentation.Focus.LightGroup);
            var target=typeof(SentencePresentation).GetMethod("Target",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Vector3 Aim()=> (Vector3)target.Invoke(show,new object[]{shot,true});
            var mouth=j.Root.TransformPoint(new Vector3(0,1.5f,j.RoomStart));
            p.Teleport(mouth+j.Root.forward*7-Vector3.up*1.4f,Quaternion.LookRotation(-j.Root.forward));
            yield return Wait(()=>j.InSecondRoom,"camera samples the far room");
            Check(j.Lamps.Any(l=>Vector3.Distance(l.transform.position,Aim())<.05f),"lit room prioritizes its lamps even facing away from them");
            p.Teleport(mouth+j.Root.forward*1.5f-Vector3.up*1.4f,Quaternion.LookRotation(-j.Root.forward));
            yield return null;
            Check(j.Root.InverseTransformPoint(Aim()).z<j.RoomStart,"facing the nearby doorway selects a return-path star");
            p.Teleport(mouth+j.Root.forward*8-Vector3.up*1.4f,j.Root.rotation);
            j.SetLights(false,PassageJourney.LightSpace.FarRoom);
            yield return Wait(()=>!j.LampsLit,"dark far room");
            Check(j.Root.InverseTransformPoint(Aim()).z<j.RoomStart,"dark room light move can focus the illuminated stars");
            Result="PASS: all unpowered commands, rejected feedback, corridor camera and approach, parked lamps and independent swing door";
        }
        public static void RunEnergy(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(MeasureEnergy(e));
        }
        static IEnumerator MeasureEnergy(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"opened box");
            var j=e.journey;var d=j.EndingDoor;var state=e.board.State;
            e.board.Panel.SetOpen(false);e.sentenceRuntime.presentation.enabledShots=false;
            e.player.Teleport(d.RoomLanding,d.transform.rotation);yield return Wait(()=>j.InSecondRoom,"far room");
            foreach(var lamp in j.Lamps){
                var body=lamp.GetComponent<Rigidbody>();body.constraints=RigidbodyConstraints.FreezeAll;
                body.position=d.transform.TransformPoint(new Vector3(0,2,-8));
            }
            j.SetLights(true,PassageJourney.LightSpace.FarRoom);yield return Wait(()=>j.Brightness==1,"lit lamps");
            var test=j.Lamps[0].GetComponent<Rigidbody>();
            test.position=d.transform.TransformPoint(new Vector3(d.width*.5f+.28f,1.5f,-.5f));
            yield return Wait(()=>d.Energy(1)>.06f,"right-side lamp contributes without a sentence");
            Check(d.Energy(0)==0 && d.Energy(2)==0,"distant rails stay empty");
            var strips=d.transform.Cast<Transform>().Where(t=>t.name=="Charge fill").ToArray();
            Check(strips.Length==3 && strips[0].gameObject.activeSelf && strips[0].localPosition.x<0 &&
                !strips[1].gameObject.activeSelf && !strips[2].gameObject.activeSelf,"even right-side energy starts at the left foot");
            float before=d.Energy(1),time=Time.time;yield return new WaitForSeconds(.23f);
            float plain=(d.Energy(1)-before)/(Time.time-time);
            state.TryAcquireSentence("energy.light","light light");yield return null;yield return null;
            before=d.Energy(1);time=Time.time;yield return new WaitForSeconds(.23f);
            float warm=(d.Energy(1)-before)/(Time.time-time);
            Check(warm>plain*1.3f,"light light accelerates charge");
            state.TryAcquireSentence("energy.door","light door");yield return null;yield return null;
            before=d.Energy(1);time=Time.time;yield return new WaitForSeconds(.23f);
            float combined=(d.Energy(1)-before)/(Time.time-time);
            Check(combined>warm*1.3f,"light door stacks with light light");
            var fill=d.transform.Find("Charge fill").GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
            Check(fill.r>fill.g*2,"active bonus warms the fill");
            state.TryAcquireSentence("energy.freeze","negative move",out var freeze);
            before=d.Energy(1);yield return new WaitForSecondsRealtime(.28f);
            Check(before==d.Energy(1),"partial charge freezes");state.ReturnToLibrary(freeze.Id,0,true);
            e.RequestExit();yield return Wait(()=>e.DialogReady,"closed world");
            before=d.Energy(1);yield return new WaitForSecondsRealtime(.23f);Check(before==d.Energy(1),"partial charge held while world is closed");
            e.CancelExit();e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open world");
            test.position=d.transform.TransformPoint(new Vector3(0,2,-8));
            float visualBefore=strips[0].localScale.y;
            before=d.Energy(1);yield return new WaitForSeconds(.3f);
            Check(d.Energy(1)<before && strips[0].localScale.y<visualBefore && !strips[2].gameObject.activeSelf,"discharge retracts the same illuminated length");
            Result="PASS: independent proximity, passive charge and decay, both bonuses, warm material, freeze and closed-world partial energy";
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
            yield return Wait(()=>d.Lift<.72f,"cycle resumes after command");
            state.ReturnToLibrary(cycle.Id,0,true);yield return null;yield return null;
            held=d.Lift;yield return new WaitForSecondsRealtime(.25f);
            Check(Mathf.Abs(held-d.Lift)<.001f,"splitting cycle holds height");
            var lamp=j.Lamps[0];var body=lamp.GetComponent<Rigidbody>();
            e.RequestExit();yield return Wait(()=>e.DialogReady,"off world closes");
            held=d.Lift;float energy=d.Energy(1);var location=body.position;
            yield return new WaitForSecondsRealtime(.3f);
            Check(held==d.Lift && energy==d.Energy(1) && Vector3.Distance(location,body.position)>.00001f,"closed world freezes door while docked lamp hovers");
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
            Result="PASS: rejects empty door, gathering and parking, three bars, controlled lift and closure, power loss, impacts and both respawn sides";
        }
    }
}
