#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PuzzleApple.V5.Editor
{
    public static class OutdoorChecks
    {
        public static string Result {get;private set;}
        static void Check(bool value,string message){if(!value){Result="FAIL: "+message;throw new Exception(Result);}}
        public static string Grammar()
        {
            var v4=AssetDatabase.LoadAssetAtPath<PuzzleApple.V4.Cognition.CognitionCatalog>("Assets/GameData/V4/Opening/OpeningCatalog.asset");
            var v5=AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V5/Opening/OpeningCatalog.asset");v5.ValidateOrThrow();
            Check(v5.Rules.Select(r=>r.Id).Distinct().Count()==v5.Rules.Count,"unique rule IDs after integration");
            var frozen=new CognitionState(v5);
            frozen.TryAcquireSentence("freeze","negative move");
            Check(frozen.Groups.Single().Effective&&frozen.HasEffect(CognitionSignal.FreezeMotion),"off move retains global freeze");
            frozen.TryAcquireSentence("boost","light light");
            Check(frozen.HasEffect(CognitionSignal.FreezeMotion)&&frozen.HasEffect(CognitionSignal.IlluminateLight),"freeze and lamp boost have separate signals");
            frozen.ReturnToLibrary(frozen.Groups.First(g=>g.RuleId=="move.off").Id,0,true);
            Check(!frozen.HasEffect(CognitionSignal.FreezeMotion)&&frozen.HasEffect(CognitionSignal.IlluminateLight),"unfreeze preserves lamp boost");
            frozen.TryAcquireSentence("move.on","positive move");
            Check(frozen.Groups.Last().Recognized,"on move remains valid");
            var lit=new CognitionState(v5);
            foreach(var target in new[]{"world","door","floor","light"})
            {
                lit.TryAcquireSentence("illuminate."+target,"light "+target);
                Check(lit.Groups.Last().Effective,"valid illumination target "+target);
            }
            Check(lit.Groups.All(g=>g.Effective)&&lit.HasEffect(CognitionSignal.IlluminateLight),"illumination sentences coexist and light light boosts lamps");
            lit.TryAcquireSentence("duplicate","light floor");
            lit.ReturnToLibrary(lit.Groups.Last().Id,0,true);
            Check(lit.HasEffect(CognitionSignal.IlluminateFloor),"remaining copy sustains floor emission");
            var floor=lit.Groups.First(g=>g.RuleId=="light.floor");
            lit.Detach(floor.Id,floor.Words[0].Id);
            Check(!lit.HasEffect(CognitionSignal.IlluminateFloor)&&lit.HasEffect(CognitionSignal.IlluminateDoor),"split removes only its illumination");
            string Text(PuzzleApple.V4.Cognition.CognitionRule r)=>string.Join(" ",r.Words.Select(w=>w.Id));
            foreach(var a in v4.Rules)foreach(var b in v4.Rules)
            {
                var old=new PuzzleApple.V4.Cognition.CognitionState(v4);var current=new CognitionState(v5);
                old.TryAcquireSentence("a",Text(a));old.TryAcquireSentence("b",Text(b));
                current.TryAcquireSentence("a",string.Join(" ",OutdoorSetup.V5Words(a)));current.TryAcquireSentence("b",string.Join(" ",OutdoorSetup.V5Words(b)));
                Check(old.Groups.Select(g=>g.Effective).SequenceEqual(current.Groups.Select(g=>g.Effective)),"priority parity "+a.Id+" / "+b.Id);
                current.ReturnToLibrary(current.Groups[0].Id,0,true);old.ReturnToLibrary(old.Groups[0].Id,0,true);
                Check(old.Groups.Select(g=>g.Effective).SequenceEqual(current.Groups.Select(g=>g.Effective)),"split takeover parity");
            }
            foreach(var text in new[]{"mirror mirror","mirror move"}){
                var state=new CognitionState(v5);state.TryAcquireSentence("mirror",text);
                Check(state.Groups.Single().Effective && state.Groups.Single().Rule.Kind==SentenceKind.Sustained,text);
            }
            foreach(var invalid in new[]{"mirror light","mirror door","mirror floor","mirror world","light floor move","world move","light world move","door door move","floor floor move","light door move","light light move","door floor move","door light move","floor door move","floor light move","light positive","positive light move","negative light move floor","light move move","light door floor"})
            {var state=new CognitionState(v5);state.TryAcquireSentence("invalid",invalid);Check(!state.Groups.Single().Recognized,"invalid syntax "+invalid);}
            var profiles=AssetDatabase.LoadAssetAtPath<SentencePresentationSettings>("Assets/GameData/V5/Resources/V5/SentencePresentation.asset");
            foreach(var rule in v5.Rules.Where(r=>r.Words.Count==3 && r.Words[1].Id=="move")){
                var state=new CognitionState(v5);string words=string.Join(" ",rule.Words.Select(w=>w.Id));
                state.TryAcquireSentence("grammar.history",words);
                Check(rule.Kind==SentenceKind.Command && state.History.Any(h=>h.Effective && string.Join(" ",h.Words.Select(w=>w.Id))==words),"directional history "+words);
                Check(profiles.shots.Any(shot=>shot.rule==rule.Id),"serialized shot "+rule.Id);
            }
            return "PASS: four illumination sentences, duplicate and split behavior, all 16 V4 sentences, all ordered rule pairs, conflict takeover and invalid combinations (ground maps to floor).";
        }
        static IEnumerator Until(Func<bool> predicate,string label,float timeout=30)
        {float end=Time.realtimeSinceStartup+timeout;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),"timeout: "+label);}
        public static void Run()
        {
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            if(!Application.isPlaying||!e||!e.Entered||e.room.Opened)throw new InvalidOperationException("Fresh P5 Play required.");
            Result="Running";e.StartCoroutine(Exercise(e));
        }
        static IEnumerator Exercise(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            bool shots=e.sentenceRuntime.presentation.enabledShots;e.sentenceRuntime.presentation.enabledShots=false;
            try
            {
                Check(e.Entered && !e.enterButton.gameObject.activeSelf,"starts indoors");
                e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy,"unfold");
                var player=e.player;var state=e.board.State;var j=e.journey;
                e.board.Panel.SetOpen(false);player.MovementLocked=true;
                player.view.LookAt(e.chamberLight.surface.bounds.center);player.AdoptCurrentView();yield return null;
                e.chamberLight.SetOn(false);yield return Until(()=>e.chamberLight.Brightness==0,"ceiling dark");
                player.view.LookAt(e.chamberLight.surface.bounds.center);player.AdoptCurrentView();
                Check(!e.chamberLight.LookingAtLight()&&!e.chamberLight.TryCollect(),"dark ceiling cannot supply light");
                e.chamberLight.SetOn(true);yield return Until(()=>e.chamberLight.Brightness==1,"ceiling bright");
                player.view.LookAt(e.chamberLight.surface.bounds.center);player.AdoptCurrentView();
                Check(e.chamberLight.TryCollect(),"lit ceiling collection");yield return Until(()=>!e.chamberLight.Collecting,"collection completion");
                Check(!e.outdoor.TryWorldDrop(e.board,"light",new Vector2(Screen.width*.8f,Screen.height*.5f)),"world drop prohibited indoors");
                player.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart+3)),j.Root.rotation);yield return null;
                var lamp=j.Lamps[0];lamp.GetComponent<Rigidbody>().position=j.Root.TransformPoint(new Vector3(0,2,j.RoomStart+6));
                lamp.direction=Vector3.zero;Physics.SyncTransforms();player.view.LookAt(lamp.transform.position);player.AdoptCurrentView();yield return null;
                Check(!e.chamberLight.LookingAtLight()&&!e.chamberLight.TryCollect(),"dark room lamp cannot supply known light");
                j.SetLights(true);yield return Until(()=>j.Brightness==1,"room bright");
                player.view.LookAt(lamp.transform.position);player.AdoptCurrentView();
                Check(e.chamberLight.TryCollect(),"lit room lamp repeat collection");
                foreach(var id in new[]{"floor","door","move"})state.TryAcquireWord("outdoor.check."+id,e.board.Catalog.Word(id));
                yield return WhiteAscentChecks.Leave(e);
                var w=e.outdoor.World;
                Check(e.outdoor.TransitionCount==1&&w.field.ActiveCount==e.outdoor.TransferredLamps,"single transition retains the transferred lamps");
                Check(w.door.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"exit frame visible without V4 reveal event");
                Check(player.transform.position.x>1000&&w.door.transform.position.x>1000,"player and physical frame are in the outdoor scene space");
                Check(e.journey.Ascent.Completed && e.outdoor.LastExitVelocity==Vector3.zero,"ascent arrives standing outdoors");
                Check(!player.CinematicLocked&&!player.MovementLocked&&!e.board.Panel.InputBlocked,"control restored");
                Check(state.Knows("floor")&&state.Knows("light"),"vocabulary preserved");
                Check(w.Audio&&w.Audio.GetComponents<AudioSource>().Any(a=>a.isPlaying&&a.loop&&a.clip==w.music.clip&&Mathf.Abs(a.volume-.45f)<.001f),"V4 BGM actually playing");
                yield return new WaitForSeconds(2);
                Check(player.GetComponent<CharacterController>().isGrounded,"gravity returns player to ground");
                player.MovementLocked=true;player.Launch(Vector3.zero);
                player.Teleport(w.exit.position+Vector3.forward*9,Quaternion.identity);player.view.localRotation=Quaternion.Euler(8,0,0);player.AdoptCurrentView();
                e.board.Panel.SetOpen(true);yield return new WaitForSecondsRealtime(.4f);
                var screen=new Vector2(Screen.width*.78f,Screen.height*.48f);
                var library=UnityEngine.Object.FindFirstObjectByType<WordLibrary>();
                var tile=library.GetComponentsInChildren<LibraryDrag>(true).First(t=>t.name=="Vocabulary light");
                Drag(tile.gameObject,screen);Check(e.outdoor.Picker.Open,"library release opens picker");e.outdoor.Picker.Select(InstanceShape.Cube);
                Check(w.field.ActiveCount==e.outdoor.TransferredLamps+1,"library drag creates lamp");
                var first=w.field.ActiveLamps.First(l=>l.RevealAmount<1);Check(first.RevealAmount<1,"lamp starts faded");
                yield return Until(()=>first.RevealAmount>=1,"lamp fades in at V4 configured duration");
                state.TryAcquireSentence("outdoor.drag.sentence","light move");yield return null;yield return null;
                var group=state.Groups.First(g=>g.RuleId=="light.move");
                var handle=e.board.GroupRect(group.Id).GetComponentsInChildren<CognitionDragHandle>().First(h=>h.WordId==group.Words[0].Id);
                Drag(handle.gameObject,new Vector2(Screen.width*.68f,Screen.height*.48f),true);Check(e.outdoor.Picker.Open,"detached token opens picker");e.outdoor.Picker.Select(InstanceShape.Cube);
                Check(w.field.ActiveCount==e.outdoor.TransferredLamps+2,"sentence token drag creates lamp");
                Check(!state.HasEffect(CognitionSignal.LightMove),"drag detaches sentence token");
                state.ClearWorkspace();state.TryAcquireSentence("outdoor.motion","light move");yield return null;yield return null;
                var before=w.field.ActiveLamps.ToDictionary(l=>l,l=>l.transform.position);yield return new WaitForSeconds(.35f);
                Check(before.All(p=>Vector3.Distance(p.Key.transform.position,p.Value)>.1f),"all created lamps move");
                Drag(tile.gameObject,new Vector2(Screen.width*.87f,Screen.height*.45f));e.outdoor.Picker.Select(InstanceShape.Cube);
                Check(w.field.ActiveCount==e.outdoor.TransferredLamps+3,"new lamp while moving");
                var newest=w.field.ActiveLamps.First(l=>!before.ContainsKey(l));var newPosition=newest.transform.position;yield return new WaitForSeconds(.3f);
                Check(Vector3.Distance(newest.transform.position,newPosition)>.08f,"new lamp inherits movement");
                state.ClearWorkspace();yield return null;yield return null;
                before=w.field.ActiveLamps.ToDictionary(l=>l,l=>l.transform.position);yield return new WaitForSeconds(.3f);
                Check(before.All(p=>Vector3.Distance(p.Key.transform.position,p.Value)<.005f),"split suspends lamps");
                var directions=w.field.ActiveLamps.Select(l=>l.GetComponent<Outdoor.LightMotion>()).ToDictionary(m=>m,m=>m.Direction);
                state.TryAcquireSentence("outdoor.resume","light move",out var resume);yield return null;
                Check(directions.All(d=>d.Key.Moving && Vector3.Dot(d.Key.Direction,d.Value)>.999f),"outdoor lamps resume their original headings");
                if(state.RequirePeriod){
                    state.Unsay(resume.Id);yield return null;
                    Check(directions.All(d=>!d.Key.Moving),"unsay holds every lamp");
                    state.Speak(resume.Id);yield return null;
                    Check(directions.All(d=>d.Key.Moving && Vector3.Dot(d.Key.Direction,d.Value)>.999f),"respeaking keeps every heading");
                }
                state.ClearWorkspace();yield return null;
                state.TryAcquireSentence("outdoor.night","negative light");yield return Until(()=>w.stateSource.IsOff&&w.environment.NightBlend>.99f,"night");
                Check(w.GetComponentsInChildren<Outdoor.ThresholdDoor>().Length==1,"night creates no progression door");
                var dark=w.field.ActiveLamps.First();
                Check(w.LookedAtWord(new Ray(player.view.position,(dark.transform.position-player.view.position).normalized))!="light","dark outdoor lamp unavailable");
                Check(w.LookedAtWord(new Ray(player.view.position,-w.sun.transform.forward))!="light","night sun unavailable");
                state.ClearWorkspace();yield return null;state.TryAcquireSentence("outdoor.day","positive light");
                yield return Until(()=>w.stateSource.IsOn&&w.environment.NightBlend<.01f,"day");
                Check(w.LookedAtWord(new Ray(player.view.position,-w.sun.transform.forward))=="light","day sun offers known light");
                e.sentenceRuntime.presentation.enabledShots=true;state.ClearWorkspace();yield return null;
                state.TryAcquireSentence("outdoor.cluster","light move light");
                yield return Until(()=>e.sentenceRuntime.presentation.Playing,"cluster cutscene");
                yield return Until(()=>e.sentenceRuntime.presentation.CanInterrupt,"deferred effect");
                Check(e.sentenceRuntime.HasEffect(CognitionSignal.LightLightMove),"cluster begins at cinematic application");
                Check(e.sentenceRuntime.presentation.InterruptForEditing(),"cutscene Tab interruption path");
                Check(e.board.Panel.IsOpen&&!player.CinematicLocked,"cutscene restores editing");
                e.sentenceRuntime.presentation.enabledShots=false;
                yield return MotionAndPlacement(e);
                Result="PASS: indoor collection, white ascent and grounded arrival, retained vocabulary and music, picker from both real drag sources, all/new lamps move, split, day/night, scoped outdoor motion and placement.";
            }
            finally{e.sentenceRuntime.presentation.enabledShots=shots;Application.runInBackground=background;}
        }
        static IEnumerator MotionAndPlacement(WorldEntry e)
        {
            var w=e.outdoor.World;var s=e.board.State;var p=e.player;var lamps=w.field.ActiveLamps.ToArray();
            s.ClearWorkspace();yield return null;yield return null;
            var origin=w.transform.position;
            for(int i=0;i<lamps.Length;i++){var b=lamps[i].GetComponent<Rigidbody>();b.position=origin+new Vector3(i<2?-5+i*5:12+i*5,4,22);}
            Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            float distance=Vector3.Distance(lamps[0].transform.position,lamps[1].transform.position);
            s.TryAcquireSentence("physical.cluster","light move light");yield return new WaitForSeconds(.5f);
            Check(Vector3.Distance(lamps[0].transform.position,lamps[1].transform.position)<distance-.2f,"nearest-neighbour cluster physically approaches");
            s.ClearWorkspace();yield return null;yield return null;
            float away=Vector3.Distance(lamps[0].transform.position,w.door.transform.position);
            s.TryAcquireSentence("physical.gather","light move door");yield return new WaitForSeconds(.5f);
            Check(Vector3.Distance(lamps[0].transform.position,w.door.transform.position)<away-.2f,"lamps gather toward existing frame");
            s.ClearWorkspace();yield return null;yield return null;
            var doorBody=w.door.GetComponent<Rigidbody>();var doorBefore=doorBody.position;
            s.TryAcquireSentence("physical.pursue","door move light");yield return new WaitForSeconds(.5f);
            Check(Vector3.Distance(doorBefore,doorBody.position)<.01f,"anchored door ignores light target");
            s.ClearWorkspace();yield return null;yield return null;
            doorBody.position=origin+new Vector3(10,8,0);Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            float doorY=doorBody.position.y;
            s.TryAcquireSentence("physical.door.floor","door move floor");yield return new WaitForSeconds(.5f);
            Check(Mathf.Abs(doorBody.position.y-doorY)<.01f,"anchored door ignores floor target");
            s.ClearWorkspace();yield return null;yield return null;
            doorBefore=doorBody.position;s.TryAcquireSentence("physical.door.free","door move");yield return new WaitForSeconds(.4f);
            Check(Vector3.Distance(doorBefore,doorBody.position)>.5f,"door free movement");
            s.ClearWorkspace();yield return null;yield return null;
            float lampY=lamps[0].transform.position.y;s.TryAcquireSentence("physical.light.floor","light move floor");yield return new WaitForSeconds(.4f);
            Check(lamps[0].transform.position.y<lampY-.3f,"created lights descend toward ground");
            s.ClearWorkspace();yield return null;yield return null;
            s.TryAcquireSentence("physical.sky.move","floor move");yield return null;yield return null;yield return null;
            Check(w.environment.WorldMoving&&w.clouds.FollowFacing&&w.clouds.speedMultiplier>1,"ground move drives sky motion");
            s.ClearWorkspace();yield return null;yield return null;
            doorBody.position=origin+new Vector3(10,12,0);Physics.SyncTransforms();
            p.Teleport(origin+new Vector3(0,.05f,10),Quaternion.identity);p.view.localRotation=Quaternion.Euler(-12,0,0);p.AdoptCurrentView();
            float groundY=w.ground.bounds.max.y,playerY=p.transform.position.y;
            s.TryAcquireSentence("physical.floor.rise","floor move light");yield return new WaitForSeconds(.5f);
            Check(w.ground.bounds.max.y>groundY+.1f&&p.transform.position.y>playerY+.1f,"floor and player rise toward visible light");
            s.ClearWorkspace();yield return null;yield return null;groundY=w.ground.bounds.max.y;
            yield return new WaitForSeconds(.3f);Check(Mathf.Abs(w.ground.bounds.max.y-groundY)<.005f,"split holds raised floor");
            groundY=w.ground.bounds.max.y;s.TryAcquireSentence("physical.floor.door","floor move door");yield return new WaitForSeconds(.3f);
            Check(w.ground.bounds.max.y>groundY+.1f,"floor rises toward door");
            s.ClearWorkspace();yield return null;yield return null;
            s.TryAcquireSentence("physical.dark.motion","light move");s.TryAcquireSentence("physical.dark.off","negative light");
            yield return Until(()=>w.stateSource.IsOff,"dark movement pause");
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            var held=lamps.ToDictionary(l=>l,l=>l.transform.position);yield return new WaitForSeconds(.3f);
            Check(held.All(x=>Vector3.Distance(x.Key.transform.position,x.Value)<.005f),"outdoor V4 darkness suspends movement");
            yield return Until(()=>!s.Groups.Any(g=>g.RuleId=="light.off"),"off command retires");s.TryAcquireSentence("physical.dark.on","positive light");
            yield return Until(()=>w.stateSource.IsOn,"light restores movement");
            Check(held.Any(x=>Vector3.Distance(x.Key.transform.position,x.Value)>.1f),"on light resumes retained motion");
            s.ClearWorkspace();yield return null;yield return null;
            e.board.Panel.SetOpen(true);p.view.localRotation=Quaternion.Euler(10,0,0);p.AdoptCurrentView();
            yield return new WaitForSecondsRealtime(.4f);
            var screen=new Vector2(Screen.width*.75f,Screen.height*.55f);int count=w.field.ActiveCount;
            bool made=w.TrySpawnInView(screen);Check(made,"release-ray spawn succeeds");
            var madeLamp=w.field.ActiveLamps.Last(l=>!lamps.Contains(l));
            var firstPosition=madeLamp.transform.position;
            bool nearby=w.TrySpawnInView(screen);Check(nearby,"overlap uses nearby fallback");
            var created=w.field.ActiveLamps.Where(l=>!lamps.Contains(l)).ToArray();
            Check(created.Length==2&&Vector3.Distance(created[0].transform.position,created[1].transform.position)>=w.minimumLampSpacing-.001f,"spawn minimum spacing");
            Check(Vector3.Distance(created[1].transform.position,firstPosition)<w.spawnSearchRadius+1,"fallback remains near release");
            float radius=w.spawnSearchRadius;int attempts=w.spawnAttempts;w.spawnSearchRadius=0;w.spawnAttempts=1;
            count=w.field.ActiveCount;Check(!w.TrySpawnInView(screen)&&w.field.ActiveCount==count,"no safe position creates nothing");
            w.spawnSearchRadius=radius;w.spawnAttempts=attempts;
        }
        public static void RunCompletion(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Completion(e));
        }
        static IEnumerator Completion(WorldEntry e){
            e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy,"unfold");
            yield return WhiteAscentChecks.Leave(e);
            var w=e.outdoor.World;var s=e.board.State;
            e.sentenceRuntime.presentation.enabledShots=false;
            var origin=w.transform.position;
            var a=w.field.Spawn(origin+new Vector3(0,2,12),.3f);var b=w.field.Spawn(origin+new Vector3(5,2,12),.3f);
            e.player.Teleport(origin+new Vector3(0,.1f,5),Quaternion.identity);
            e.board.Panel.SetOpen(true);yield return Until(()=>e.board.Panel.FullyOpen,"board visible for ripple checks");
            foreach(var syntax in new[]{"light move light","light move door","light move floor","door move light","door move floor","floor move light","floor move door"}){
                if(syntax.StartsWith("floor move")){
                    float top=w.ground.bounds.max.y;
                    a.GetComponent<Rigidbody>().position=origin+new Vector3(0,top-origin.y+2,12);
                    b.GetComponent<Rigidbody>().position=origin+new Vector3(5,top-origin.y+3,12);
                    var door=w.door.GetComponent<Rigidbody>();
                    door.position=new Vector3(door.position.x,top+3,door.position.z);
                    Physics.SyncTransforms();yield return new WaitForFixedUpdate();
                }
                int weak=e.feedback.WeakCount,strong=e.feedback.StrongCount;
                s.TryAcquireSentence("done."+syntax,syntax,out var group);
                yield return Until(()=>s.Find(group.Id)==null,"retires "+syntax,40);
                if(syntax.StartsWith("door move"))Check(e.feedback.WeakCount>weak && e.feedback.StrongCount==strong,"anchored door gives weak ripple "+syntax);
                var held=a.transform.position;var floorHeight=w.ground.bounds.max.y;
                yield return new WaitForSeconds(.22f);
                Check(Vector3.Distance(a.transform.position,held)<.02f && Mathf.Abs(floorHeight-w.ground.bounds.max.y)<.01f,"end position retained "+syntax);
            }
            Result="PASS: all seven directional sentences retire, anchored door feedback is weak, end positions retained";
        }
        static void Drag(GameObject source,Vector2 target,bool pointerDown=false)
        {
            var start=RectTransformUtility.WorldToScreenPoint(null,source.transform.position);
            var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=start,pressPosition=start};
            if(pointerDown)ExecuteEvents.Execute(source,e,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(source,e,ExecuteEvents.beginDragHandler);e.position=target;
            ExecuteEvents.Execute(source,e,ExecuteEvents.dragHandler);ExecuteEvents.Execute(source,e,ExecuteEvents.endDragHandler);
        }
    }
}
#endif
