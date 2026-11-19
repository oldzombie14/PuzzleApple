using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceScopeChecks
    {
        public static string Result {get;private set;}
        static void Check(bool value,string label){if(!value){Result="FAIL: "+label;throw new Exception(Result);}}
        static IEnumerator Wait(Func<bool> done,string label){float end=Time.realtimeSinceStartup+40;while(!done()&&Time.realtimeSinceStartup<end)yield return null;Check(done(),label);}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Exercise(e));}
        public static void RunFirstPassage(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(FirstPassage(e));}
        static IEnumerator FirstPassage(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey;var b=e.board;var state=b.State;var rt=e.sentenceRuntime;
            var passage=UnityEngine.Object.FindFirstObjectByType<WallPassage>();
            var gate=passage.GetComponentsInChildren<ProgressLock>().Single(g=>g.passageReveal==passage);
            rt.presentation.enabledShots=false;e.player.MovementLocked=true;b.Panel.SetOpen(true);
            yield return Wait(()=>b.Panel.FullyOpen,"open panel");
            Check(!gate.Unlocked && j.SentenceScope()==Space.Chamber,"first path starts behind its reveal lock");
            var motion=VitruvianChecks.Say(b,"light","move");yield return null;
            var off=VitruvianChecks.Say(b,"negative","light");
            yield return Wait(()=>e.chamberLight.Brightness<.7f,"partial chamber fade");
            Check(!gate.Unlocked && j.Stars.Brightness==1 && !rt.HasEffect(CognitionSignal.LightOff,Space.FirstPath),"opening off command cannot reach corridor stars");
            state.ReturnToLibrary(off.Id,0,true);float held=e.chamberLight.Brightness;
            yield return new WaitForSecondsRealtime(.2f);
            Check(e.chamberLight.Brightness==held && !gate.Unlocked,"interrupted reveal does not unlock");
            off=VitruvianChecks.Say(b,"negative","light");
            yield return Wait(()=>gate.Unlocked,"first complete reveal unlocks passage");
            yield return null;yield return null;
            Check(j.SentenceScope()==(Space.Chamber|Space.FirstPath) && rt.HasEffect(CognitionSignal.LightMove,Space.FirstPath),"reveal expands an existing sustained sentence");
            Check(j.Stars.Brightness==1 && !rt.HasEffect(CognitionSignal.LightOff,Space.FirstPath),"unlock does not extend the triggering light command");
            yield return Wait(()=>state.Find(off.Id)==null,"opening command retires");
            Check(j.Stars.Brightness==1 && !j.Stars.WouldChange(true),"stars stay lit after opening command completes");
            var on=VitruvianChecks.Say(b,"positive","light");yield return Wait(()=>state.Find(on.Id)==null && e.chamberLight.Brightness==1,"relight chamber");
            Check(gate.Unlocked && passage.Revealed && passage.Reveal==1 && (j.SentenceScope()&Space.FirstPath)!=0,"relighting preserves both passage visibility and unlocked progress");
            Check(!passage.walls[passage.SelectedWall].GetComponent<Renderer>().enabled,"relighting never restores the aperture cover");
            var stairs=(GameObject)typeof(WallPassage).GetField("stairs",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(passage);
            Check(stairs.activeInHierarchy && stairs.GetComponentsInChildren<Collider>().All(c=>c.enabled),"revealed entrance stairs remain usable when relit");
            var laterOff=VitruvianChecks.Say(b,"negative","light");
            yield return Wait(()=>state.Find(laterOff.Id)==null && j.Stars.Brightness==0,"a new light command controls the unlocked corridor");
            state.ClearWorkspace();
            Result="PASS: first passage reveal lock, interrupted reveal, opening off command preserves stars, sustained expansion, no relock, later light commands";
        }
        static IEnumerator Exercise(WorldEntry e){
            e.OpenWorld(-1);yield return Wait(()=>e.room.Opened&&!e.Busy,"open Intro");
            var j=e.journey;var p=e.player;var b=e.board;var state=b.State;var rt=e.sentenceRuntime;
            rt.presentation.enabledShots=false;b.Panel.SetOpen(false);p.MovementLocked=true;
            const Space first=Space.Chamber;
            Check(j.SentenceScope()==first,"initial range is only the chamber");
            var white=j.Door.GetComponentInChildren<ProgressLock>();var entrance=j.EntranceDoor.GetComponentInChildren<ProgressLock>();var charge=j.EndingDoor.GetComponent<ProgressLock>();
            var reveal=UnityEngine.Object.FindFirstObjectByType<WallPassage>().GetComponentsInChildren<ProgressLock>().Single(g=>g.passageReveal);
            Check(reveal && white && entrance && charge && !reveal.Unlocked && !white.Unlocked && !entrance.Unlocked && !charge.Unlocked,"four authored progress locks start closed");
            var motion=VitruvianChecks.Say(b,"light","move");yield return null;yield return null;
            foreach(float yaw in new[]{0f,90f,180f}){
                p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.RoomStart+5)),j.Root.rotation*Quaternion.Euler(0,yaw,0));yield return null;
                Check(rt.Scope(motion)==first && !rt.HasEffect(CognitionSignal.LightMove,Space.FarRoom),"view, distance and teleport do not bypass progress");
            }
            p.Teleport(Vector3.up*.1f,Quaternion.identity);e.chamberLight.SetOn(false);
            yield return Wait(()=>reveal.Unlocked && rt.HasEffect(CognitionSignal.LightMove,Space.FirstPath),"revealing first passage expands sustained motion");
            e.chamberLight.SetOn(true);yield return Wait(()=>e.chamberLight.Brightness==1,"relight chamber for return-motion feedback");
            p.Teleport(j.Root.TransformPoint(new Vector3(0,.1f,j.pathLength-3)),j.Root.rotation);yield return null;
            j.SetDoor(true);yield return Wait(()=>white.Unlocked,"white door becomes passable");
            yield return Wait(()=>rt.HasEffect(CognitionSignal.LightMove,Space.ReturnPath),"first unlock expands existing motion to room two and return path");
            Check(!rt.HasEffect(CognitionSignal.LightMove,Space.FarRoom),"entrance keeps room three outside the scope");
            j.EntranceDoor.SetOpen(true);
            yield return Wait(()=>rt.HasEffect(CognitionSignal.LightMove,Space.FarRoom),"second unlock expands existing motion to room three");
            Check(rt.Scope(motion)==(Space.All&~Space.Ascent) && !charge.Unlocked,"second layer includes room three but excludes ascent");
            p.Teleport(Vector3.up*.1f,Quaternion.identity);
            j.SetDoor(false);yield return Wait(()=>j.DoorProgress==0,"white door closes again");
            Check(white.Unlocked && rt.Scope(motion)==(Space.Chamber|Space.FirstPath),"closing never relocks progress");
            p.Teleport(Vector3.up*.1f,Quaternion.identity);yield return null;
            Check(rt.HasEffect(CognitionSignal.LightMove,Space.Chamber) && rt.SustainsFeedback(motion),"earlier rooms remain controlled on return");
            var tail=state.Detach(motion.Id,motion.Words.Last().Id);state.JoinAndSpeak(tail.Id,motion.Id,false);
            Check(rt.Scope(motion)==(Space.Chamber|Space.FirstPath),"reassembly respects the closed membrane");
            j.SetDoor(true);yield return Wait(()=>j.DoorProgress==1,"reconnect white door");state.ClearWorkspace();p.Teleport(j.EndingDoor.RoomLanding,j.EndingDoor.transform.rotation);yield return null;
            j.SetLights(true,Space.FarRoom);yield return Wait(()=>j.Brightness==1,"light room three before gathering");
            var boost=VitruvianChecks.Say(b,"light","light");
            var gather=VitruvianChecks.Say(b,"light","move","door");
            Check(rt.EffectScope(gather)==rt.Scope(gather),"one-shot motion includes all unlocked spaces");
            var target=typeof(SentencePresentation).GetMethod("LocalTarget",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var aim=target.Invoke(rt.presentation,new object[]{new SentencePresentation.Shot("light.move.door",SentencePresentation.Focus.Door),gather});
            Check(aim==null,"gather holds the camera without narrowing effect scope");
            yield return Wait(()=>state.Find(gather.Id)==null && j.EndingDoor.Charged,"charge from the selected room");
            var open=VitruvianChecks.Say(b,"positive","door");
            yield return null;yield return null;
            Check(rt.HasEffect(CognitionSignal.DoorOn,Space.FarRoom) && rt.HasEffect(CognitionSignal.DoorOn,Space.FirstPath|Space.ReturnPath),"one-shot door signal reaches all unlocked door spaces");
            yield return Wait(()=>charge.Unlocked && rt.HasEffect(CognitionSignal.IlluminateLight,Space.Ascent),"third lock expands an existing sustained effect");
            Check(j.SentenceScope()==Space.All,"all Intro progress is now accessible");
            yield return Wait(()=>j.EndingDoor.Lift==1 && state.Find(open.Id)==null,"charge door finishes opening");
            j.EndingDoor.SetOpen(false);yield return Wait(()=>j.EndingDoor.Lift==0,"charge door closes");
            Check(charge.Unlocked && (j.SentenceScope()&Space.Ascent)==0,"closed exit isolates the unlocked stairs");
            var stop=VitruvianChecks.Say(b,"negative","move");
            p.Teleport(j.EndingDoor.StairLanding,j.EndingDoor.transform.rotation);yield return null;yield return null;var dark=VitruvianChecks.Say(b,"negative","light");
            yield return Wait(()=>j.Ascent.Stars.Brightness==0 && state.Find(dark.Id)==null,"light commands still finish in ascent while movement is frozen");
            state.ReturnToLibrary(stop.Id,0,true);
            yield return VitruvianChecks.Gallery(e);
            Check(state.Groups.Count==0,"chapter crossing clears sentences");
            var pass=e.outdoor;var gallery=pass.Chapter;var garden=pass.Garden;var room=pass.Vitruvian;
            VitruvianChecks.Say(b,"positive","world");yield return Wait(()=>gallery.box.Opened&&!e.Busy,"open gallery");
            var mirror=gallery.mirrors.originals[0];var mirrorMotion=VitruvianChecks.Say(b,"mirror","move");var start=mirror.position;
            yield return Wait(()=>Vector3.Distance(start,mirror.position)>.08f,"gallery mirror moves");
            p.Teleport(new Vector3(garden.floor.bounds.center.x,garden.floor.bounds.max.y+.08f,garden.floor.bounds.center.z),Quaternion.identity);yield return null;yield return null;
            Check(pass.CurrentRoom==garden && rt.Scope(mirrorMotion)==Space.Garden,"crossing membrane retargets the spoken sentence");
            var held=mirror.position;yield return new WaitForSeconds(.2f);Check(Vector3.Distance(held,mirror.position)<.001f,"gallery mirror pauses outside the active region");
            var lampMotion=VitruvianChecks.Say(b,"light","move");yield return new WaitForSeconds(.1f);
            var lampStart=garden.lamp.position;yield return new WaitForSeconds(.1f);var heading=garden.lamp.position-lampStart;
            Check(heading.sqrMagnitude>1e-6f,"garden motion begins");
            p.Teleport(gallery.arrival.position+Vector3.left*8,Quaternion.identity);yield return null;yield return null;
            var lampHeld=garden.lamp.position;yield return new WaitForSeconds(.2f);
            Check(Vector3.Distance(lampHeld,garden.lamp.position)<.001f && Vector3.Distance(held,mirror.position)>.03f,"exit pauses inside and resumes outside");
            p.Teleport(room.transform.TransformPoint(new Vector3(0,.1f,5)),room.transform.rotation);yield return null;yield return null;
            Check(pass.CurrentRoom==room && rt.Scope(lampMotion)==Space.Vitruvian,"second membrane selects its own region");
            var offset=room.puzzle.UpperOffset;yield return new WaitForSeconds(.2f);Check(Mathf.Abs(offset-room.puzzle.UpperOffset)>.1f,"existing mirror motion transfers to the puzzle");
            var freeze=VitruvianChecks.Say(b,"negative","move");yield return null;yield return null;
            Check(rt.MotionFrozen && Time.timeScale==0,"no move remains global");
            offset=room.puzzle.UpperOffset;
            p.Teleport(gallery.arrival.position+Vector3.left*8,Quaternion.identity);yield return null;yield return null;
            Check(rt.MotionFrozen && rt.Scope(mirrorMotion)==Space.Gallery,"membrane updates also work while frozen");
            state.ReturnToLibrary(freeze.Id,0,true);yield return new WaitForSeconds(.2f);
            Check(Mathf.Abs(room.puzzle.UpperOffset-offset)<.001f,"puzzle phase stays paused outside");
            var copy=VitruvianChecks.Say(b,"mirror","mirror");yield return Wait(()=>gallery.mirrors.CopiesActive,"copy appears in its owner region");
            var ownership=gallery.mirrors.Copy(0).GetComponent<SentenceOwnership>();
            Check(ownership && ownership.source && ownership.Space==Space.Gallery,"copy expresses source ownership");
            gallery.mirrors.Copy(0).position=room.roomFill.transform.position;
            Check(ownership.Space==Space.Gallery,"physical placement does not rewrite ownership");
            var world=VitruvianChecks.Say(b,"light","world");yield return new WaitForSeconds(.2f);float exposure=rt.WorldExposure;
            p.Teleport(new Vector3(garden.floor.bounds.center.x,garden.floor.bounds.max.y+.08f,garden.floor.bounds.center.z),Quaternion.identity);yield return null;yield return null;
            var resumed=garden.lamp.position;yield return new WaitForSeconds(.12f);
            Check(Vector3.Dot(heading.normalized,(garden.lamp.position-resumed).normalized)>.65f,"garden resumes its stored direction");
            Check(rt.HasEffect(CognitionSignal.IlluminateWorld) && rt.WorldExposure>=exposure,"world illumination survives membrane crossings");
            Check(!gallery.mirrors.CopiesActive && state.Find(copy.Id)!=null,"sustained sentence stays spoken as outside copy pauses");
            Result="PASS: layered sticky locks, global multi-target effects, independent camera focus, membrane retargeting, paused direction/phase and source ownership";
        }
    }
}
