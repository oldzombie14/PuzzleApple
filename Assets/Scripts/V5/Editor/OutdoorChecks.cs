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
                current.TryAcquireSentence("a",Text(a).Replace("ground","floor"));current.TryAcquireSentence("b",Text(b).Replace("ground","floor"));
                Check(old.Groups.Select(g=>g.Effective).SequenceEqual(current.Groups.Select(g=>g.Effective)),"priority parity "+a.Id+" / "+b.Id);
                current.ReturnToLibrary(current.Groups[0].Id,0,true);old.ReturnToLibrary(old.Groups[0].Id,0,true);
                Check(old.Groups.Select(g=>g.Effective).SequenceEqual(current.Groups.Select(g=>g.Effective)),"split takeover parity");
            }
            foreach(var invalid in new[]{"world move","light world move","door door move","floor floor move","door move floor","light positive","positive light move","negative light floor move","light move move","light door floor"})
            {var state=new CognitionState(v5);state.TryAcquireSentence("invalid",invalid);Check(!state.Groups.Single().Recognized,"invalid syntax "+invalid);}
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
                player.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.pathLength+3)),j.Root.rotation);yield return null;
                var lamp=j.Lamps[0];lamp.GetComponent<Rigidbody>().position=j.Root.TransformPoint(new Vector3(0,2,j.pathLength+6));
                lamp.direction=Vector3.zero;Physics.SyncTransforms();player.view.LookAt(lamp.transform.position);player.AdoptCurrentView();yield return null;
                Check(!e.chamberLight.LookingAtLight()&&!e.chamberLight.TryCollect(),"dark room lamp cannot supply known light");
                j.SetLights(true);yield return Until(()=>j.Brightness==1,"room bright");
                player.view.LookAt(lamp.transform.position);player.AdoptCurrentView();
                Check(e.chamberLight.TryCollect(),"lit room lamp repeat collection");
                foreach(var id in new[]{"floor","door","move"})state.TryAcquireWord("outdoor.check."+id,e.board.Catalog.Word(id));
                // Solving the floor puzzle is covered by FloorImpactChecks; here exercise its exit.
                var fallingLamp=j.Lamps[0];var body=fallingLamp.GetComponent<Rigidbody>();
                body.position=j.Root.TransformPoint(new Vector3(0,.45f,j.floorSurface.HoleCentreZ));
                fallingLamp.direction=Vector3.zero;
                player.MovementLocked=true;j.floorSurface.OpenHole();
                yield return Until(()=>e.outdoor.TransferredLamps>0,"a lamp falls and exits before the player");
                Check(e.outdoor.RenderingWarmed&&!e.outdoor.IsOutside,"outdoor window rendered before travel");
                player.MovementLocked=false;
                var centre=j.Root.TransformPoint(new Vector3(0,.1f,j.pathLength+j.roomSize.z*.5f));
                player.Teleport(centre,j.Root.rotation);player.view.rotation=Quaternion.LookRotation(Vector3.down,j.Root.forward);player.AdoptCurrentView();
                yield return Until(()=>e.outdoor.Transitioning,"native fall activates passage");
                Check(e.board.Panel.InputBlocked&&player.CinematicLocked,"passage owns input");
                var overlay=(RectTransform)e.outdoor.transform.Find("Passage cinematic");
                var upper=(RectTransform)overlay.Find("Top");
                var lower=(RectTransform)overlay.Find("Bottom");
                yield return Until(()=>upper.rect.height>1,"letterbox visible during fall");
                var screenRect=overlay.GetComponent<Canvas>().rootCanvas.pixelRect;
                var corners=new Vector3[4];
                upper.GetWorldCorners(corners);
                Check(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,corners[1]),new Vector2(screenRect.xMin,screenRect.yMax))<1 &&
                    Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,corners[2]),screenRect.max)<1,"top bar spans the screen's upper edge");
                lower.GetWorldCorners(corners);
                Check(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,corners[0]),screenRect.min)<1 &&
                    Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null,corners[3]),new Vector2(screenRect.xMax,screenRect.yMin))<1,"bottom bar spans the screen's lower edge");
                yield return Until(()=>e.outdoor.IsOutside&&!e.outdoor.Transitioning,"horizontal exit");
                Check(!upper.gameObject.activeSelf&&!lower.gameObject.activeSelf&&!overlay.Find("Transition").gameObject.activeSelf,"cinematic overlay clears after exit");
                var w=e.outdoor.World;
                Check(e.outdoor.TransitionCount==1&&w.field.ActiveCount==e.outdoor.TransferredLamps,"single transition retains the transferred lamps");
                Check(w.door.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"exit frame visible without V4 reveal event");
                Check(player.transform.position.x>1000&&w.door.transform.position.x>1000,"player and physical frame are in the outdoor scene space");
                Check(e.outdoor.LastExitVelocity.z>0&&e.outdoor.LastExitVelocity.z<=e.outdoor.exitSpeed&&Mathf.Abs(player.view.forward.y)<.01f,"horizontal launch and level view");
                Check(!player.CinematicLocked&&!player.MovementLocked&&!e.board.Panel.InputBlocked,"control restored");
                Check(state.Knows("floor")&&state.Knows("light"),"vocabulary preserved");
                Check(w.Audio&&w.GetComponents<AudioSource>().Any(a=>a.isPlaying&&a.loop&&a.clip==w.music.clip&&Mathf.Abs(a.volume-.45f)<.001f),"V4 BGM actually playing");
                yield return new WaitForSeconds(2);
                Check(player.GetComponent<CharacterController>().isGrounded,"gravity returns player to ground");
                player.MovementLocked=true;player.Launch(Vector3.zero);
                player.Teleport(w.exit.position+Vector3.forward*9,Quaternion.identity);player.view.localRotation=Quaternion.Euler(8,0,0);player.AdoptCurrentView();
                e.board.Panel.SetOpen(true);yield return new WaitForSecondsRealtime(.4f);
                var screen=new Vector2(Screen.width*.78f,Screen.height*.48f);
                var library=UnityEngine.Object.FindFirstObjectByType<WordLibrary>();
                var tile=library.GetComponentsInChildren<LibraryDrag>(true).First(t=>t.name=="Vocabulary light");
                Drag(tile.gameObject,screen);
                Check(w.field.ActiveCount==e.outdoor.TransferredLamps+1,"library drag creates lamp");
                var first=w.field.ActiveLamps.First(l=>l.RevealAmount<1);Check(first.RevealAmount<1,"lamp starts faded");
                yield return Until(()=>first.RevealAmount>=1,"lamp fades in at V4 configured duration");
                state.TryAcquireSentence("outdoor.drag.sentence","light move");yield return null;yield return null;
                var group=state.Groups.First(g=>g.RuleId=="light.move");
                var handle=e.board.GroupRect(group.Id).GetComponentsInChildren<CognitionDragHandle>().First(h=>h.WordId==group.Words[0].Id);
                Drag(handle.gameObject,new Vector2(Screen.width*.68f,Screen.height*.48f),true);
                Check(w.field.ActiveCount==e.outdoor.TransferredLamps+2,"sentence token drag creates lamp");
                Check(!state.HasEffect(CognitionSignal.LightMove),"drag detaches sentence token");
                state.ClearWorkspace();state.TryAcquireSentence("outdoor.motion","light move");yield return null;yield return null;
                var before=w.field.ActiveLamps.ToDictionary(l=>l,l=>l.transform.position);yield return new WaitForSeconds(.35f);
                Check(before.All(p=>Vector3.Distance(p.Key.transform.position,p.Value)>.1f),"all created lamps move");
                Drag(tile.gameObject,new Vector2(Screen.width*.87f,Screen.height*.45f));
                Check(w.field.ActiveCount==e.outdoor.TransferredLamps+3,"new lamp while moving");
                var newest=w.field.ActiveLamps.First(l=>!before.ContainsKey(l));var newPosition=newest.transform.position;yield return new WaitForSeconds(.3f);
                Check(Vector3.Distance(newest.transform.position,newPosition)>.08f,"new lamp inherits movement");
                state.ClearWorkspace();yield return null;yield return null;
                before=w.field.ActiveLamps.ToDictionary(l=>l,l=>l.transform.position);yield return new WaitForSeconds(.3f);
                Check(before.All(p=>Vector3.Distance(p.Key.transform.position,p.Value)<.005f),"split suspends lamps");
                state.TryAcquireSentence("outdoor.night","negative light");yield return Until(()=>w.stateSource.IsOff&&w.environment.NightBlend>.99f,"night");
                Check(w.GetComponentsInChildren<Outdoor.ThresholdDoor>().Length==1,"night creates no progression door");
                var dark=w.field.ActiveLamps.First();
                Check(w.LookedAtWord(new Ray(player.view.position,(dark.transform.position-player.view.position).normalized))!="light","dark outdoor lamp unavailable");
                Check(w.LookedAtWord(new Ray(player.view.position,-w.sun.transform.forward))!="light","night sun unavailable");
                state.ClearWorkspace();yield return null;state.TryAcquireSentence("outdoor.day","positive light");
                yield return Until(()=>w.stateSource.IsOn&&w.environment.NightBlend<.01f,"day");
                Check(w.LookedAtWord(new Ray(player.view.position,-w.sun.transform.forward))=="light","day sun offers known light");
                e.sentenceRuntime.presentation.enabledShots=true;state.ClearWorkspace();yield return null;
                state.TryAcquireSentence("outdoor.cluster","light light move");
                yield return Until(()=>e.sentenceRuntime.presentation.Playing,"cluster cutscene");
                yield return Until(()=>e.sentenceRuntime.presentation.CanInterrupt,"deferred effect");
                Check(e.sentenceRuntime.HasEffect(CognitionSignal.LightLightMove),"cluster begins at cinematic application");
                Check(e.sentenceRuntime.presentation.InterruptForEditing(),"cutscene Tab interruption path");
                Check(e.board.Panel.IsOpen&&!player.CinematicLocked,"cutscene restores editing");
                e.sentenceRuntime.presentation.enabledShots=false;
                yield return MotionAndPlacement(e);
                Result="PASS: direct indoor entry; live outdoor window and lamp transfer; dark/lit collection in all V5 spaces; real fall, cinematic, horizontal impulse and landing; retained vocabulary and playing V4 BGM; both UI drag sources; fade; all/new lamps move; split; day/night without V4 progression; cutscene application/interruption.";
            }
            finally{e.sentenceRuntime.presentation.enabledShots=shots;Application.runInBackground=background;}
        }
        static IEnumerator MotionAndPlacement(WorldEntry e)
        {
            var w=e.outdoor.World;var s=e.board.State;var p=e.player;var lamps=w.field.ActiveLamps.ToArray();
            s.ClearWorkspace();yield return null;yield return null;
            var origin=w.transform.position;
            for(int i=0;i<lamps.Length;i++){var b=lamps[i].GetComponent<Rigidbody>();b.position=origin+new Vector3(-5+i*5,4,22);}
            Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            float distance=Vector3.Distance(lamps[0].transform.position,lamps[1].transform.position);
            s.TryAcquireSentence("physical.cluster","light light move");yield return new WaitForSeconds(.5f);
            Check(Vector3.Distance(lamps[0].transform.position,lamps[1].transform.position)<distance-.2f,"nearest-neighbour cluster physically approaches");
            s.ClearWorkspace();yield return null;yield return null;
            float away=Vector3.Distance(lamps[0].transform.position,w.door.transform.position);
            s.TryAcquireSentence("physical.gather","light door move");yield return new WaitForSeconds(.5f);
            Check(Vector3.Distance(lamps[0].transform.position,w.door.transform.position)<away-.2f,"lamps gather toward existing frame");
            s.ClearWorkspace();yield return null;yield return null;
            var doorBody=w.door.GetComponent<Rigidbody>();var doorBefore=doorBody.position;
            s.TryAcquireSentence("physical.pursue","door light move");yield return new WaitForSeconds(.5f);
            Check(Vector3.Distance(doorBefore,doorBody.position)>.3f,"door pursues nearest light");
            s.ClearWorkspace();yield return null;yield return null;
            doorBody.position=origin+new Vector3(10,8,0);Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            float doorY=doorBody.position.y;
            s.TryAcquireSentence("physical.door.floor","door floor move");yield return new WaitForSeconds(.5f);
            Check(doorBody.position.y<doorY-.2f,"door descends toward floor");
            s.ClearWorkspace();yield return null;yield return null;
            doorBefore=doorBody.position;s.TryAcquireSentence("physical.door.free","door move");yield return new WaitForSeconds(.4f);
            Check(Vector3.Distance(doorBefore,doorBody.position)>.5f,"door free movement");
            s.ClearWorkspace();yield return null;yield return null;
            float lampY=lamps[0].transform.position.y;s.TryAcquireSentence("physical.light.floor","light floor move");yield return new WaitForSeconds(.4f);
            Check(lamps[0].transform.position.y<lampY-.3f,"created lights descend toward ground");
            s.ClearWorkspace();yield return null;yield return null;
            s.TryAcquireSentence("physical.sky.move","floor move");yield return null;yield return null;yield return null;
            Check(w.environment.WorldMoving&&w.clouds.FollowFacing&&w.clouds.speedMultiplier>1,"ground move drives sky motion");
            s.ClearWorkspace();yield return null;yield return null;
            doorBody.position=origin+new Vector3(10,12,0);Physics.SyncTransforms();
            p.Teleport(origin+new Vector3(0,.05f,10),Quaternion.identity);p.view.localRotation=Quaternion.Euler(-12,0,0);p.AdoptCurrentView();
            float groundY=w.ground.bounds.max.y,playerY=p.transform.position.y;
            s.TryAcquireSentence("physical.floor.rise","floor light move");yield return new WaitForSeconds(.5f);
            Check(w.ground.bounds.max.y>groundY+.1f&&p.transform.position.y>playerY+.1f,"floor and player rise toward visible light");
            s.ClearWorkspace();yield return null;yield return null;groundY=w.ground.bounds.max.y;
            yield return new WaitForSeconds(.3f);Check(Mathf.Abs(w.ground.bounds.max.y-groundY)<.005f,"split holds raised floor");
            groundY=w.ground.bounds.max.y;s.TryAcquireSentence("physical.floor.door","floor door move");yield return new WaitForSeconds(.3f);
            Check(w.ground.bounds.max.y>groundY+.1f,"floor rises toward door");
            s.ClearWorkspace();yield return null;yield return null;
            s.TryAcquireSentence("physical.dark.motion","light move");s.TryAcquireSentence("physical.dark.off","negative light");
            yield return Until(()=>w.stateSource.IsOff,"dark movement pause");
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            var held=lamps.ToDictionary(l=>l,l=>l.transform.position);yield return new WaitForSeconds(.3f);
            Check(held.All(x=>Vector3.Distance(x.Key.transform.position,x.Value)<.005f),"outdoor V4 darkness suspends movement");
            var off=s.Groups.First(g=>g.RuleId=="light.off");s.ReturnToLibrary(off.Id,0,true);s.TryAcquireSentence("physical.dark.on","positive light");
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
