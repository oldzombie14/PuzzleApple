using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class ObservationChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string text){if(!ok){Result="FAIL: "+text;throw new Exception(Result);}}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Run(e));}
        public static void RunCorridorStars()
        {
            Result = "Running";
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            entry.StartCoroutine(RunTutorialCheck(entry));
        }

        static IEnumerator RunTutorialCheck(WorldEntry entry)
        {
            var presentation = entry.sentenceRuntime.presentation;
            bool shots = presentation.enabledShots;
            try
            {
                presentation.enabledShots = false;
                yield return CheckCorridorStars(entry);
            }
            finally { presentation.enabledShots = shots; }
        }

        static IEnumerator CheckCorridorStars(WorldEntry entry)
        {
            entry.EnterWorld();
            yield return Until(() => entry.Entered);
            var state = entry.board.State;
            state.TryAcquireSentence("tutorial.check.world", "positive world");
            yield return Until(() => entry.room.Opened && !entry.Busy);
            var journey = entry.journey;
            var observer = entry.chamberLight.observer;
            var player = entry.player;
            var sphere = journey.Tutorial.Sphere;
            var start = sphere.position;
            var collider = sphere.GetComponent<Collider>();
            entry.board.Panel.SetOpen(false);
            player.Teleport(journey.Root.TransformPoint(new Vector3(0,.08f,journey.pathLength*.5f)),journey.Root.rotation);
            yield return Until(() => journey.InCorridor);
            Cursor.lockState=CursorLockMode.Locked;
            var stars=journey.ActiveStars.GetComponentsInChildren<MeshRenderer>();
            var before=stars[0].transform.position;
            yield return new WaitForSeconds(.25f);
            Check(Vector3.Distance(before,stars[0].transform.position)>.01f,"stars drift before learning move");
            yield return Until(() =>
            {
                if(entry.chamberLight.Collecting)return true;
                foreach(var star in stars)
                {
                    player.view.LookAt(star.transform);
                    player.AdoptCurrentView();
                    observer.Advance(false,0);
                    if(observer.Target!=journey.ActiveStars.Observation)continue;
                    var aim=(star.transform.position-player.view.position).normalized;
                    if(entry.chamberLight.PickStar(new Ray(player.view.position,aim),observer.range,out _)!=star)continue;
                    observer.Advance(true,.12f);
                    player.view.rotation=Quaternion.LookRotation(Quaternion.AngleAxis(2,Vector3.up)*aim);
                    if(entry.chamberLight.PickStar(new Ray(player.view.position,player.view.forward),observer.range,out _))continue;
                    observer.Advance(true,.12f);
                    if(observer.Target!=journey.ActiveStars.Observation || observer.Progress<=.12f/journey.ActiveStars.Observation.lesson.holdDuration)continue;
                    observer.Advance(true,journey.ActiveStars.Observation.lesson.holdDuration);
                    return entry.chamberLight.Collecting;
                }
                return false;
            });
            Check(entry.chamberLight.Collecting,"hold starts observation collection");
            yield return Until(() => !entry.chamberLight.Collecting);
            Check(state.HasEffect(Cognition.CognitionSignal.LightMove) && entry.board.Panel.IsOpen,"observing a star grants the example");
            yield return new WaitForSeconds(.2f);
            Check(sphere.position==start && journey.DoorProgress==0,"closed first door isolates the sphere");
            state.TryAcquireSentence("tutorial.check.door","positive door");
            yield return Until(() => journey.DoorProgress>=1);
            var small=journey.Root.Find("Move tutorial small sphere 1");
            before=small.position;
            yield return new WaitForSeconds(.25f);
            Check(sphere.position==start && journey.Tutorial.ExitBlocked && !journey.Tutorial.IsLit(collider),
                "opening with light move leaves the dark sphere blocking the exit");
            Check(small.position==before && journey.Tutorial.IsLit(small.GetComponent<Collider>()),
                "learning move before opening suppresses the small sphere demonstration");
            var learned=state.Groups.Single(g=>g.RuleId=="light.move");
            Check((entry.sentenceRuntime.Scope(learned)&PassageJourney.LightSpace.SphereRoom)==0,"opening a door does not expand the old sentence");
            player.Teleport(journey.Root.TransformPoint(new Vector3(0,.08f,journey.pathLength+3)),journey.Root.rotation);
            yield return Until(()=>journey.InTutorial);
            var verb=state.Detach(learned.Id,learned.Words.Last().Id);state.JoinAndSpeak(verb.Id,learned.Id,false);
            yield return Until(()=>entry.sentenceRuntime.HasEffect(Cognition.CognitionSignal.LightMove,PassageJourney.LightSpace.SphereRoom));
            state.TryAcquireSentence("tutorial.check.on","positive light",out var on);
            var lampLight=sphere.GetComponentInChildren<Light>();
            float fullSphere=journey.LightStyle.LampIntensity(sphere.localScale.x);
            float smallLampFill=journey.Tutorial.Fill.Fill;
            yield return Until(() => Vector3.Distance(start,sphere.position)>.1f && !journey.Tutorial.WouldChange(true));
            Check(Mathf.Approximately(lampLight.intensity,fullSphere*journey.entry.sentenceRuntime.LampMultiplier),
                "sphere light output follows its size");
            Check(journey.Tutorial.Fill.Fill>smallLampFill+.01f,"lighting the large sphere raises the room fill");
            state.Detach(on.Id,on.Words.Last().Id);
            player.Teleport(journey.Root.TransformPoint(new Vector3(0,.08f,journey.Tutorial.ExitZ+2)),journey.Root.rotation);
            yield return Until(() => journey.InReturnPath);
            before=sphere.position;
            yield return new WaitForSeconds(.25f);
            Check(Vector3.Distance(before,sphere.position)>.01f,"sphere continues from second corridor");
            var motion=state.Groups.Single(g=>g.RuleId=="light.move");
            state.Detach(motion.Id,motion.Words.Last().Id);
            before=sphere.position;
            var smallBefore=small.position;
            yield return new WaitForSeconds(.2f);
            Check(sphere.position==before && small.position==smallBefore,"split holds lit spheres");
            player.Teleport(journey.Root.TransformPoint(new Vector3(0,.08f,journey.pathLength+3)),journey.Root.rotation);
            yield return Until(()=>journey.InTutorial);
            var glow=sphere.GetComponentInChildren<Light>();
            state.TryAcquireSentence("tutorial.check.off","negative light",out var off);
            yield return Until(()=>glow.intensity<journey.LightStyle.LampIntensity(sphere.localScale.x)*.7f);
            var token=state.Detach(off.Id,off.Words.Last().Id);
            yield return new WaitForSeconds(.15f);
            Check(glow.intensity>0 && journey.WordAt(collider,1)=="light","half-lit sphere retains light identity");
            state.JoinAndSpeak(token.Id,off.Id,false);
            yield return Until(()=>!journey.Tutorial.AnyLit);
            before=sphere.position;
            smallBefore=small.position;
            yield return new WaitForSeconds(.25f);
            Check(sphere.position==before && smallBefore==small.position,
                "dark tutorial lamps stay still after the motion sentence is split");
            journey.Tutorial.SetOn(true);
            yield return Until(()=>!journey.Tutorial.WouldChange(true));
            smallBefore=small.position;
            yield return new WaitForSeconds(.25f);
            Check(sphere.position==before && smallBefore==small.position,
                "relighting alone leaves the tutorial lamps still");
            yield return CheckBlockedApproach(entry);
            state.ClearWorkspace();
            player.Teleport(journey.Root.TransformPoint(new Vector3(0,.08f,journey.RoomStart+3)),journey.Root.rotation);
            yield return Until(()=>journey.InSecondRoom);
            yield return new WaitForFixedUpdate();
            var cube=journey.Lamps[0].GetComponent<Rigidbody>();
            before=cube.position;
            yield return new WaitForSeconds(.3f);
            Check(cube.isKinematic && cube.position==before,"entering the drifting room with move learned and the sentence dismantled holds the lamps");
            Result = "PASS";
        }
        static IEnumerator CheckBlockedApproach(WorldEntry entry)
        {
            var j = entry.journey;
            var sphere = j.Tutorial.Sphere;
            var body = sphere.GetComponent<SphereCollider>();
            var obstacles = new GameObject("Door approach check");
            obstacles.transform.SetParent(j.Root,false);
            try
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.transform.SetParent(obstacles.transform,false);
                block.transform.localPosition = new Vector3(.8f,2.5f,j.pathLength+8);
                block.transform.localScale = new Vector3(1,6,1);
                var start = new Vector3(0,2.23f,j.pathLength+12);
                sphere.localPosition = start;
                j.Tutorial.SetOn(true);
                Physics.SyncTransforms();
                entry.board.State.TryAcquireSentence("tutorial.check.approach","light move door");
                yield return Until(()=>entry.sentenceRuntime.HasEffect(Cognition.CognitionSignal.LightDoorMove));
                // The entire sphere must clear the obstacle, even if other lamps gather beyond it.
                float beyondBarrier=block.transform.localPosition.z-block.transform.localScale.z*.5f-body.bounds.extents.x-.02f;
                for (int i=0; i<700 && sphere.localPosition.z >= beyondBarrier; i++)
                {
                    yield return new WaitForFixedUpdate();
                    CheckClear(body,block.GetComponent<Collider>());
                }
                Check(sphere.localPosition.z < beyondBarrier,"sphere gets past partial barrier");
                sphere.localPosition = start;
                block.transform.localPosition = new Vector3(0,2.5f,j.pathLength+8);
                block.transform.localScale = new Vector3(9,6,1);
                foreach (int sign in new[]{-1,1})
                {
                    var side = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    side.transform.SetParent(obstacles.transform,false);
                    side.transform.localPosition = new Vector3(sign*2.69f,2.5f,j.pathLength+10);
                    side.transform.localScale = new Vector3(1,6,8);
                }
                Physics.SyncTransforms();
                yield return new WaitForSeconds(2);
                foreach (var obstacle in obstacles.GetComponentsInChildren<Collider>()) CheckClear(body,obstacle);
                Vector3 held = sphere.position;
                yield return new WaitForSeconds(.3f);
                Check(Vector3.Distance(held,sphere.position)<.001f,"sphere stops at sealed barrier");
            }
            finally { UnityEngine.Object.Destroy(obstacles); }
        }

        static void CheckClear(Collider lamp,Collider obstacle)
        {
            bool overlaps = Physics.ComputePenetration(lamp,lamp.transform.position,lamp.transform.rotation,
                obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth);
            Check(!overlaps || depth<.00001f,"sphere penetrated obstacle");
        }
        static IEnumerator Until(Func<bool> predicate)
        {float end=Time.realtimeSinceStartup+20;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),"timeout");}
        static IEnumerator Run(WorldEntry e)
        {
            var o=e.chamberLight.observer;bool background=Application.runInBackground;Application.runInBackground=true;
            bool shots=e.sentenceRuntime.presentation.enabledShots;
            // Input/ownership checks are independent of the separately tested camera timelines.
            e.sentenceRuntime.presentation.enabledShots=false;
            try
            {
                e.EnterWorld();yield return Until(()=>e.Entered);
                e.board.State.TryAcquireSentence("observe.check.world","positive world");yield return Until(()=>e.room.Opened&&!e.Busy);
                var j=e.journey;var p=e.player;
                p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart+2)),j.Root.rotation);e.board.Panel.SetOpen(false);yield return null;
                var body=j.Lamps[0].GetComponent<Rigidbody>();var before=body.position;yield return new WaitForSeconds(.2f);
                Check(!j.LightsOn&&Vector3.Distance(before,body.position)>.02f,"unlearned dark lamps keep moving");
                Check(!e.board.State.HasEffect(Cognition.CognitionSignal.LightMove)&&j.movementObservation.HasEffect(e.board.State,e.board.Catalog,Cognition.CognitionSignal.LightMove),"preset sentence drives movement before ownership transfer");
                Check(!j.movementObservation.HasEffect(e.board.State,e.board.Catalog,Cognition.CognitionSignal.LightOff),"preset evaluates its actual rule effects");
                Check(j.Lamps.All(l=>l.GetComponent<Renderer>().enabled&&l.glow.intensity==0),"dark lamp bodies remain visible without point lighting");
                Cursor.lockState=CursorLockMode.Locked;
                for(int i=0;i<j.Lamps.Count;i++)
                {
                    var lampBody=j.Lamps[i].GetComponent<Rigidbody>();
                    lampBody.interpolation=RigidbodyInterpolation.None;
                    lampBody.position=j.Root.TransformPoint(new Vector3(4,2+i*.5f,j.RoomStart+10));
                }
                Physics.SyncTransforms();
                p.view.LookAt(j.Root.Find("Room far wall"));p.AdoptCurrentView();
                Check(o.FindTarget()==null,"bare mural cannot start observation");
                body.position=j.Root.TransformPoint(new Vector3(0,3,j.RoomStart+10));Physics.SyncTransforms();
                p.view.LookAt(body.position+j.Root.right*.25f);p.AdoptCurrentView();
                Check(o.FindTarget()==j.movementObservation,"near-edge tolerance");
                p.view.LookAt(body.position);p.AdoptCurrentView();yield return null;
                Check(o.FindTarget()==j.movementObservation,"ray finds moving lamp");
                e.chamberLight.AdvancePointer(true,true,false,.03f);
                e.chamberLight.AdvancePointer(false,false,true,.03f);
                Check(!e.chamberLight.LookingAtLight()&&!e.chamberLight.ShowingKnown&&!e.chamberLight.Collecting,"short click on an unlit moving lamp cannot collect light");
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                Check(o.ring.gameObject.activeInHierarchy&&o.ring.canvasRenderer&&o.ring.canvasRenderer.GetMesh().vertexCount>0,"ring submits actual UI geometry");
                Check(o.ring.material==e.chamberLight.hoverEye.material&&o.ring.material.GetTexture("_ReticleSceneColor")==p.view.GetComponent<Camera>().targetTexture,"ring shares exact symbol contrast material");
                o.enabled=false;o.Advance(true,.3f);Check(o.Progress>0&&o.Progress<1&&!e.board.State.Knows("move"),"partial hold");
                var other=j.Lamps[1].GetComponent<Rigidbody>();
                other.position=p.view.position+p.view.forward*3+p.view.right*2;
                Physics.SyncTransforms();p.view.LookAt(other.position);p.AdoptCurrentView();o.Advance(true,.05f);
                Check(o.Progress>0&&o.Progress<.1f,"switching lamps restarts the hold: "+o.Progress);
                p.view.LookAt(body.position);o.Advance(false,.1f);Check(o.Progress==0,"release resets");
                o.Advance(true,.3f);p.view.rotation=Quaternion.LookRotation(Vector3.down,j.Root.forward);o.Advance(true,.1f);Check(o.Progress==0&&o.Target==null,"look away resets");
                p.view.LookAt(body.position);p.AdoptCurrentView();
                o.Advance(true,.3f);e.board.Panel.SetOpen(true);o.Advance(true,.1f);Check(o.Progress==0,"panel interrupts");e.board.Panel.SetOpen(false);yield return null;
                p.view.LookAt(body.position);p.AdoptCurrentView();
                int pulsesBefore=e.feedback.StrongCount;
                for(int i=0;i<47;i++)o.Advance(true,1f/60);
                Check(!e.board.State.Knows("move"),"requires full configured duration");o.Advance(true,.03f);
                Check(o.Progress==1&&e.chamberLight.Collecting&&e.chamberLight.collectionSymbol.gameObject.activeSelf,"full bar immediately shows vocabulary collection without release");
                o.Advance(true,1);
                yield return Until(()=>!e.chamberLight.Collecting);
                o.Advance(false,0);yield return null;
                var s=e.board.State;
                Check(s.Knows("move")&&!s.Knows("light")&&!s.Groups.Any(g=>g.RuleId=="light.move")&&!e.board.Panel.IsOpen,"dark observation grants only the verb");
                Check(j.LampsMoving,"demonstration continues in the dark");
                p.view.LookAt(body.position);p.AdoptCurrentView();o.Advance(false,0);o.Advance(true,1);
                Check(e.chamberLight.KnownFeedbackWord=="move"&&!e.chamberLight.Collecting,"repeat in darkness shows the known verb");
                yield return Until(()=>!e.chamberLight.ShowingKnown);o.Advance(false,0);
                s.TryAcquireSentence("observe.check.on","positive light");yield return Until(()=>j.Brightness==1&&!e.sentenceRuntime.presentation.Playing);
                Cursor.lockState=CursorLockMode.Locked;
                body.position=p.view.position+p.view.forward*3;Physics.SyncTransforms();p.view.LookAt(body.position);p.AdoptCurrentView();
                pulsesBefore=e.feedback.StrongCount;o.Advance(false,0);o.Advance(true,1);
                yield return Until(()=>!e.chamberLight.Collecting);o.Advance(false,0);
                Check(s.Knows("light")&&s.HasEffect(Cognition.CognitionSignal.LightMove)&&e.board.Panel.IsOpen,"lit observation grants the example");
                yield return Until(()=>e.feedback.StrongCount>pulsesBefore);
                Check(e.board.Panel.FullyOpen&&e.feedback.StrongCount==pulsesBefore+1,"acquired example pulses once after panel opens");
                var group=s.Groups.Single(g=>g.RuleId=="light.move");
                var acquiredRect=e.board.GroupRect(group.Id);
                Check(-acquiredRect.anchoredPosition.y>=e.board.Surface.rect.height*.12f,"acquired example stays below the top reserved area");
                Check(-acquiredRect.anchoredPosition.y+acquiredRect.rect.height<=e.board.Surface.rect.height-7,"acquired example fits in the workspace");
                if(s.RequirePeriod){
                    s.Unsay(group.Id);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                    Check(!j.LampsMoving && j.Lamps.All(l=>l.GetComponent<Rigidbody>().isKinematic),"removing observed period stops every lamp");
                    s.Speak(group.Id);yield return Until(()=>j.LampsMoving);
                }
                var detached=s.Detach(group.Id,group.Words[1].Id);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                Check(!j.LampsMoving&&j.Lamps.All(l=>l.GetComponent<Rigidbody>().isKinematic),"split stops whole group");
                before=body.position;yield return new WaitForSeconds(.15f);Check(Vector3.Distance(before,body.position)<.001f,"stopped positions stable");
                s.JoinAndSpeak(detached.Id,group.Id,false);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();Check(j.LampsMoving&&!body.isKinematic,"rejoin resumes");
                e.board.Panel.SetOpen(false);yield return null;p.view.LookAt(body.position);p.AdoptCurrentView();
                int groups=s.Groups.Count;pulsesBefore=e.feedback.StrongCount;o.Advance(false,0);o.Advance(true,2.1f);
                Check(e.chamberLight.ShowingKnown,"known full bar immediately shows vocabulary feedback");
                Check(s.Groups.Count==groups&&!e.board.Panel.IsOpen,"known observation does not duplicate sentence");
                Check(e.chamberLight.ShowingKnown&&e.chamberLight.KnownFeedbackWord=="move"&&!p.MovementLocked,"known observation displays only the learned verb");
                yield return new WaitForSecondsRealtime(1);
                Check(e.feedback.StrongCount==pulsesBefore&&!e.board.Panel.IsOpen,"known observation only shows the word");
                o.Advance(true,2.1f);Check(!e.chamberLight.ShowingKnown,"held button cannot repeatedly replay known feedback");
                o.Advance(false,0);
                // The same concrete lamp remains left-click collectible while its group is observable.
                o.enabled=true;
                body.position=p.view.position+p.view.forward*3;Physics.SyncTransforms();p.view.LookAt(body.position);p.AdoptCurrentView();
                Check(o.FindTarget()==j.movementObservation&&e.chamberLight.LookingAtLight(),"noun and behaviour coexist");
                e.chamberLight.AdvancePointer(true,true,false,.03f);
                Check(!e.chamberLight.ShowingKnown,"noun does not collect on press");
                e.chamberLight.AdvancePointer(false,false,true,.03f);
                Check(e.chamberLight.ShowingKnown&&e.chamberLight.KnownFeedbackWord=="light","short release collects noun");
                o.Advance(true,.1f);Check(o.Progress==0,"word feedback interrupts observation");
                yield return new WaitForSecondsRealtime(1);
                body.position=p.view.position+p.view.forward*3;Physics.SyncTransforms();
                e.chamberLight.AdvancePointer(true,true,false,.3f);
                e.chamberLight.AdvancePointer(false,false,true,0);
                Check(!e.chamberLight.ShowingKnown&&!e.chamberLight.Collecting,"cancelled long hold never falls through to noun");
                Check(!s.Groups.Any(g=>g.RuleId=="light.on"),"completed light command left the workspace");
                s.TryAcquireSentence("observe.check.off","negative light");yield return Until(()=>j.Brightness==0);
                body.position=p.view.position+p.view.forward*3;Physics.SyncTransforms();p.view.LookAt(body.position);p.AdoptCurrentView();
                yield return new WaitForFixedUpdate();
                before=body.position;
                yield return new WaitForSeconds(.25f);
                Check(!j.LampsMoving && body.isKinematic && Vector3.Distance(before,body.position)<.001f && s.HasEffect(Cognition.CognitionSignal.LightMove),"a spoken sentence waits while its cubes are dark");
                s.TryAcquireSentence("observe.check.relight","positive light");
                yield return Until(()=>j.Brightness==1);
                Check(j.LampsMoving&&Vector3.Distance(before,body.position)>.02f,"same movement sentence resumes after relighting");
                s.ReturnToLibrary(s.Groups.Single(g=>g.RuleId=="light.move").Id,0,true);
                yield return Until(()=>!j.LampsMoving);
                j.SetLights(false);yield return Until(()=>j.Brightness==0);
                j.SetLights(true);yield return Until(()=>j.Brightness==1);
                before=body.position;
                yield return new WaitForSeconds(.25f);
                Check(!j.LampsMoving && body.isKinematic && Vector3.Distance(before,body.position)<.001f,
                    "relighting without a motion sentence leaves controlled lamps stationary");
                Result="PASS: dark prelearning motion; group ray; hold/release/look/panel interruption; acquisition and tab; split/rejoin; known observation; noun coexistence; on/off light";
            }
            finally{o.enabled=true;e.sentenceRuntime.presentation.enabledShots=shots;Application.runInBackground=background;}
        }
    }
}
