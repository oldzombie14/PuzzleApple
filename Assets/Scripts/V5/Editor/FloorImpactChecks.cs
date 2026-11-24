using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class FloorImpactChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string message){if(!ok){Result="FAIL: "+message;throw new Exception(Result);}}
        public static void Run(bool lightFirst=false){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Run(e,lightFirst));}
        static IEnumerator Until(Func<bool> predicate,[System.Runtime.CompilerServices.CallerLineNumber]int line=0)
        {float end=Time.realtimeSinceStartup+20;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),"timeout at "+line);}

        public static void RunPlatform()
        {
            Result="Running";
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            e.StartCoroutine(CheckPlatform(e));
        }
        static IEnumerator CheckPlatform(WorldEntry e)
        {
            e.OpenWorld(-1);
            yield return Until(()=>e.room.Opened&&!e.Busy);
            yield return ChargeDoorChecks.UnlockFirstPassage(e);
            var j=e.journey;
            var platform=j.Platform;
            var p=e.player;
            var root=j.Root;
            var state=e.board.State;
            e.board.Panel.SetOpen(false);
            j.SetDoor(true);yield return Until(()=>j.DoorProgress==1);
            yield return ChargeDoorChecks.ReachRoomThree(e);
            // Leave the tutorial exit clear while exercising the rail puzzle in isolation.
            j.Tutorial.Sphere.localPosition+=Vector3.back*6;
            Physics.SyncTransforms();
            float home=platform.transform.localPosition.z;
            Check(Mathf.Approximately(platform.transform.localScale.z,j.PathWidth),"square deck");
            yield return FallFrom(j,j.RoomStart-2,false);
            Check(platform.transform.localPosition.z==home,"platform waits before collecting floor");
            p.Teleport(root.TransformPoint(new Vector3(0,.12f,j.Tutorial.ExitZ-.4f)),root.rotation);
            yield return Until(()=>p.GetComponent<CharacterController>().isGrounded);
            p.view.LookAt(platform.Deck.bounds.center+Vector3.up*platform.Deck.bounds.extents.y);p.AdoptCurrentView();
            Cursor.lockState=CursorLockMode.Locked;
            yield return null;
            var eyeRay=new Ray(p.view.position,p.view.forward);
            e.chamberLight.PickStar(eyeRay,40,out var aimed);
            Check(aimed.collider==platform.Deck,"platform sightline first hits "+(aimed.collider?aimed.collider.name:"empty"));
            e.chamberLight.AdvancePointer(true,true,false,0);e.chamberLight.AdvancePointer(false,false,true,.03f);
            yield return Until(()=>state.Knows("floor")&&!e.chamberLight.Collecting);
            Check(platform.transform.localPosition.z==home,"collecting floor leaves platform parked");
            e.board.Panel.SetOpen(false);
            p.transform.rotation=root.rotation;
            Walk(p,Vector2.up,true);
            Check(p.StandingOn(platform.Deck),"normal jump boards the square");yield return null;

            state.TryAcquireWord("platform.test.move",e.board.Catalog.Word("move"));
            var floor=state.Spawn(e.board.Catalog.Word("floor"));
            var move=state.Spawn(e.board.Catalog.Word("move"));
            state.JoinAndSpeak(move.Id,floor.Id,false);
            yield return Until(()=>platform.transform.localPosition.z>home+1);
            yield return new WaitForEndOfFrame();
            var cameraOffset=p.view.position-platform.transform.position;
            float maxJitter=0;
            Vector3 maximumOffset=Vector3.zero;
            for(int frame=0;frame<90;frame++){
                yield return new WaitForEndOfFrame();
                var offset=p.view.position-platform.transform.position-cameraOffset;
                if(offset.magnitude>maxJitter){maxJitter=offset.magnitude;maximumOffset=offset;}
            }
            Check(maxJitter<.003f,"rendered rider stays aligned, maximum offset "+maxJitter+" vector "+maximumOffset);
            var localRider=platform.transform.InverseTransformPoint(p.transform.position);
            yield return Until(()=>platform.transform.localPosition.z>home+2);
            Check(Vector3.Distance(localRider,platform.transform.InverseTransformPoint(p.transform.position))<.05f,"grounded rider follows platform");
            var detached=state.Detach(floor.Id,floor.Words.Last().Id);
            float paused=platform.transform.localPosition.z;
            var source = platform.MotionOutput.StateSource<PassagePlatform.RailState>();
            Check(source != null, "registered rail output exposes its state");
            var outward = source.CaptureState();
            yield return new WaitForSeconds(.25f);
            Check(platform.transform.localPosition.z==paused && source.CaptureState().phase == outward.phase,"split holds exact rail position and phase");
            p.transform.rotation=root.rotation;
            Walk(p,Vector2.right,false);
            Check(root.InverseTransformPoint(p.transform.position).y<-.3f,"walk sideways off the deck into the abyss");
            yield return Until(()=>j.InTutorial);
            p.Teleport(platform.Deck.bounds.center+Vector3.up*.3f,root.rotation);
            yield return Until(()=>p.StandingOn(platform.Deck));
            state.JoinAndSpeak(detached.Id,floor.Id,false);
            yield return Until(()=>platform.transform.localPosition.z>paused+.2f);
            yield return Until(()=>Vector3.Dot(platform.TravelDirection,root.forward)<0);
            Check(p.StandingOn(platform.Deck),"rider reaches far dock through reversal");
            var returning = source.CaptureState();
            var returnPosition = platform.transform.position;
            p.transform.rotation=root.rotation;
            Walk(p,Vector2.up,true);
            yield return Until(()=>j.InSecondRoom);
            yield return new WaitForEndOfFrame();
            yield return FallFrom(j,j.RoomStart-3,true);
            yield return FallFrom(j,j.Tutorial.ExitZ+3,false);
            state.ClearWorkspace();
            yield return null;
            yield return null;
            Check(source.TryRestoreState(outward) && Mathf.Abs(platform.transform.localPosition.z-paused)<.001f &&
                Vector3.Dot(platform.TravelDirection, root.forward) > .99f, "restore paused outbound rail state");
            var saved = JsonUtility.ToJson(returning);
            Check(source.TryRestoreState(JsonUtility.FromJson<PassagePlatform.RailState>(saved)) &&
                Vector3.Distance(platform.transform.position,returnPosition)<.001f &&
                Vector3.Distance(platform.Deck.bounds.center,returnPosition)<.001f &&
                Vector3.Dot(platform.TravelDirection,root.forward)<-.99f, "serialized return leg restores pose and heading");
            Check(!source.TryRestoreState(new PassagePlatform.RailState { phase = float.NaN }) &&
                source.CaptureState().phase == returning.phase, "invalid rail state leaves the restored pose intact");
            yield return null;
            Check(source.CaptureState().phase == returning.phase, "restoring a pose does not start a sentence");
            Result="PASS: floor click, rider alignment, boarding, reversal, split/resume, abyss returns and rail state round trip";
        }
        static void Walk(FirstPersonController player,Vector2 input,bool jump)
        {
            var advance=typeof(FirstPersonController).GetMethod("AdvanceMovement",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            for(int i=0;i<126;i++)advance.Invoke(player,new object[]{input,true,1f/120,jump&&i==0});
        }
        static IEnumerator FallFrom(PassageJourney j,float z,bool expectFar)
        {
            var p=j.entry.player;
            p.Teleport(j.Root.TransformPoint(new Vector3(j.PathWidth*.5f+1,.12f,z)),j.Root.rotation);
            yield return Until(()=>j.Root.InverseTransformPoint(p.transform.position).y<-1);
            yield return Until(()=>j.Root.InverseTransformPoint(p.transform.position).y>=0 && !j.Platform.Respawning);
            float landing=j.Root.InverseTransformPoint(p.transform.position).z;
            Check(expectFar?landing>j.RoomStart:landing<j.Tutorial.ExitZ,"abyss return at "+z);
        }
        static IEnumerator Run(WorldEntry e,bool lightFirst)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var presentation=e.sentenceRuntime.presentation;bool shots=presentation.enabledShots;presentation.enabledShots=false;
            try
            {
                e.EnterWorld();yield return Until(()=>e.Entered);e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);
                var j=e.journey;var floor=j.floorSurface;
                j.SetDoor(true);yield return Until(()=>j.DoorProgress==1);yield return ChargeDoorChecks.UnlockEntrance(e);
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart+3)),j.Root.rotation);e.board.Panel.SetOpen(false);yield return null;
                j.SetLights(true);yield return Until(()=>j.Brightness==1);yield return new WaitForEndOfFrame();
                Check(floor.Surface.sharedMaterial.shader==j.whitePath.shader&&floor.Surface.sharedMaterial.GetFloat("_SurfaceMode")==2&&floor.Surface.sharedMaterial.GetFloat("_Brightness")==1,"shared surface shader preserves white lit floor effects");
                e.player.view.rotation=Quaternion.LookRotation(Vector3.down,j.Root.forward);e.player.AdoptCurrentView();yield return null;
                Cursor.lockState=CursorLockMode.Locked;Check(!e.chamberLight.TryCollect(),"ordinary floor cannot teach floor");
                e.player.Teleport(j.Platform.Deck.bounds.center+Vector3.up*.32f,j.Root.rotation);
                yield return null;e.player.view.LookAt(j.Platform.Deck.bounds.center);e.player.AdoptCurrentView();
                Check(e.chamberLight.TryCollect(),"rail platform teaches floor");yield return Until(()=>!e.chamberLight.Collecting);
                Check(e.board.State.Knows("floor"),"floor vocabulary learned");
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.RoomStart+3)),j.Root.rotation);yield return null;
                var lamp=j.Lamps[0];var body=lamp.GetComponent<Rigidbody>();
                body.position=j.Root.TransformPoint(new Vector3(-4,.7f,j.RoomStart+8));lamp.direction=Vector3.down;Physics.SyncTransforms();
                int impacts=floor.ImpactCount;yield return Until(()=>floor.ImpactCount>impacts&&lamp.direction.y>0);
                Check(lamp.direction.y>0,"native floor collision reflects lamp upward");
                var squares=j.Root.Find("Floor impact squares").GetComponent<ParticleSystem>();
                Check(squares.particleCount>0&&squares.GetComponent<ParticleSystemRenderer>().renderMode==ParticleSystemRenderMode.Mesh,"few mesh squares emitted on impact");
                j.SetLights(false);yield return Until(()=>j.Brightness==0);yield return new WaitForEndOfFrame();
                Check(floor.Surface.sharedMaterial.GetFloat("_Brightness")==1,"floor retains dim fill");
                var state=e.board.State;
                e.board.Panel.SetOpen(true);
                yield return Until(()=>e.board.Panel.FullyOpen);
                body.position=j.Root.TransformPoint(new Vector3(-4,5,j.RoomStart+8));Physics.SyncTransforms();
                state.TryAcquireSentence("check.drift","light move");
                yield return Until(()=>e.sentenceRuntime.HasEffect(CognitionSignal.LightMove));
                var group=state.Groups.First(g=>g.RuleId=="light.move");
                var target=state.Spawn(e.board.Catalog.Word("floor"));
                int weak=e.feedback.WeakCount,strong=e.feedback.StrongCount;
                state.JoinAndSpeak(target.Id,group.Id,false);
                yield return Until(()=>e.feedback.WeakCount>weak);
                var before=body.position;
                yield return new WaitForSeconds(.25f);
                Check(group.Recognized&&!j.floorMotion.Active&&e.feedback.StrongCount==strong&&Vector3.Distance(body.position,before)>.01f && group.ExecutionLocked,"dark cube drifts independently while the directed command waits");
                j.SetLights(true);
                yield return Until(()=>j.floorMotion.Active);
                yield return new WaitForFixedUpdate();
                Check(state.Detach(group.Id,group.Words.Last().Id)==null && !state.Unsay(group.Id),"directed descent rejects splitting and period removal");
                before=body.position;yield return new WaitForSeconds(.2f);
                Check(body.position.y<before.y-.2f,"lit lamp descends");
                if(!lightFirst)
                {
                    j.SetLights(false);yield return Until(()=>j.Brightness==0);
                    yield return new WaitForFixedUpdate();
                    before=body.position;yield return new WaitForSeconds(.2f);
                    Check(!j.floorMotion.Active&&Vector3.Distance(body.position,before)<.015f,"extinguishing pauses directed movement");
                    Check(state.Detach(group.Id,group.Words.Last().Id)==null && group.ExecutionLocked,"dark pause preserves command lock");
                }
                e.board.Panel.SetOpen(false);
                j.SetLights(true);
                yield return Until(()=>j.floorMotion.Completed);
                yield return Until(()=>state.Find(group.Id)==null);Physics.SyncTransforms();
                var centre=j.Root.TransformPoint(new Vector3(0,.2f,j.RoomStart+j.roomSize.z*.5f));
                Check(!floor.HoleOpen && !floor.MuralAvailable,"no hole or wall mural after grounding");
                Check(Physics.Raycast(centre,Vector3.down,2,~0,QueryTriggerInteraction.Ignore),"solid centre");
                Check(Physics.Raycast(centre+j.Root.right*(floor.holeSize*.5f+1),Vector3.down,2,~0,QueryTriggerInteraction.Ignore),"solid rim");
                e.player.Teleport(centre,j.Root.rotation);yield return new WaitForSecondsRealtime(1.2f);
                Check(e.player.GetComponent<CharacterController>().isGrounded && !e.outdoor.IsOutside,"standing on the centre stays indoors");
                Result="PASS: floor collection, impacts, directed movement, dark pause, retirement, solid centre and no mural";
            }
            finally{presentation.enabledShots=shots;Application.runInBackground=background;}
        }
    }
}
