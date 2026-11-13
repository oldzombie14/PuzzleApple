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
        static IEnumerator Until(Func<bool> predicate)
        {float end=Time.realtimeSinceStartup+20;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),"timeout");}
        static IEnumerator Run(WorldEntry e,bool lightFirst)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var presentation=e.sentenceRuntime.presentation;bool shots=presentation.enabledShots;presentation.enabledShots=false;
            try
            {
                e.EnterWorld();yield return Until(()=>e.Entered);e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);
                var j=e.journey;var floor=j.floorSurface;
                e.player.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.pathLength+3)),j.Root.rotation);e.board.Panel.SetOpen(false);yield return null;
                j.SetLights(true);yield return Until(()=>j.Brightness==1);yield return new WaitForEndOfFrame();
                Check(floor.Surface.sharedMaterial.shader==j.whitePath.shader&&floor.Surface.sharedMaterial.GetFloat("_SurfaceMode")==2&&floor.Surface.sharedMaterial.GetFloat("_Brightness")==1,"shared surface shader preserves white lit floor effects");
                e.player.view.rotation=Quaternion.LookRotation(Vector3.down,j.Root.forward);e.player.AdoptCurrentView();yield return null;
                Cursor.lockState=CursorLockMode.Locked;Check(e.chamberLight.TryCollect(),"floor uses normal collection path");yield return Until(()=>!e.chamberLight.Collecting);
                Check(e.board.State.Knows("floor"),"floor vocabulary learned");
                var lamp=j.Lamps[0];var body=lamp.GetComponent<Rigidbody>();
                body.position=j.Root.TransformPoint(new Vector3(-4,.7f,j.pathLength+8));lamp.direction=Vector3.down;Physics.SyncTransforms();
                int impacts=floor.ImpactCount;yield return Until(()=>floor.ImpactCount>impacts&&lamp.direction.y>0);
                Check(lamp.direction.y>0,"native floor collision reflects lamp upward");
                var squares=j.Root.Find("Floor impact squares").GetComponent<ParticleSystem>();
                Check(squares.particleCount>0&&squares.GetComponent<ParticleSystemRenderer>().renderMode==ParticleSystemRenderMode.Mesh,"few mesh squares emitted on impact");
                j.SetLights(false);yield return Until(()=>j.Brightness==0);yield return new WaitForEndOfFrame();
                Check(floor.Surface.sharedMaterial.GetFloat("_Brightness")==1,"floor retains dim fill");
                var state=e.board.State;
                e.board.Panel.SetOpen(true);
                yield return Until(()=>e.board.Panel.FullyOpen);
                state.TryAcquireSentence("check.drift","light move");
                yield return Until(()=>e.sentenceRuntime.HasEffect(CognitionSignal.LightMove));
                var group=state.Groups.First(g=>g.RuleId=="light.move");
                var target=state.Spawn(e.board.Catalog.Word("floor"));
                int weak=e.feedback.WeakCount,strong=e.feedback.StrongCount;
                state.Join(target.Id,group.Id,false);
                yield return Until(()=>e.feedback.WeakCount>weak);
                var before=body.position;
                yield return new WaitForSeconds(.25f);
                Check(group.Recognized&&!j.floorMotion.Active&&e.feedback.StrongCount==strong&&Vector3.Distance(body.position,before)>.02f,"dark sentence gives grammar feedback while cube drifts");
                j.SetLights(true);
                yield return Until(()=>j.floorMotion.Active);
                yield return new WaitForFixedUpdate();
                var detached=state.Detach(group.Id,group.Words.Last().Id);
                yield return Until(()=>j.LampsMoving);
                body.position=j.Root.TransformPoint(new Vector3(-4,4,j.pathLength+8));
                strong=e.feedback.StrongCount;
                state.Join(detached.Id,group.Id,false);
                yield return Until(()=>j.floorMotion.Active);
                Check(e.feedback.StrongCount==strong+1&&e.feedback.GetComponentsInChildren<ParticleSystem>().Any(ps=>ps.isPlaying),"extending lit drift to floor plays a starred ripple");
                before=body.position;yield return new WaitForSeconds(.2f);
                Check(body.position.y<before.y-.2f,"lit lamp descends");
                if(!lightFirst)
                {
                    j.SetLights(false);yield return Until(()=>j.Brightness==0);
                    yield return new WaitForFixedUpdate();
                    before=body.position;yield return new WaitForSeconds(.2f);
                    Check(!j.floorMotion.Active&&Vector3.Distance(body.position,before)>.02f,"extinguishing restores autonomous drift");
                }
                if(!lightFirst)
                {
                    var floorWord=state.Detach(group.Id,group.Words.Last().Id);
                    var moveWord=state.Detach(group.Id,group.Words.Last().Id);
                    before=body.position;
                    yield return new WaitForSeconds(.25f);
                    Check(!e.sentenceRuntime.HasEffect(CognitionSignal.LightMove)&&!body.isKinematic&&Vector3.Distance(body.position,before)>.02f,"fully dismantled sentence preserves dark drift");
                    state.Join(moveWord.Id,group.Id,false);
                    state.Join(floorWord.Id,group.Id,false);
                }
                e.board.Panel.SetOpen(false);
                j.SetLights(true);yield return Until(()=>floor.HoleOpen);Physics.SyncTransforms();
                var centre=j.Root.TransformPoint(new Vector3(0,.2f,j.pathLength+j.roomSize.z*.5f));
                Check(!Physics.Raycast(centre,Vector3.down,2,~0,QueryTriggerInteraction.Ignore),"centre has no hidden collision floor");
                Check(Physics.Raycast(centre+j.Root.right*(floor.holeSize*.5f+1),Vector3.down,2,~0,QueryTriggerInteraction.Ignore),"rim remains solid");
                e.player.Teleport(centre,j.Root.rotation);yield return new WaitForSecondsRealtime(1.2f);
                Check(e.player.transform.position.y<centre.y-3||e.outdoor&&(e.outdoor.Transitioning||e.outdoor.IsOutside),"native player falls through centre opening into the outdoor passage");
                Result="PASS (light first="+lightFirst+"): floor collection and native impacts, particles, lit and dark extension feedback, vertical trajectory, relight resumes, solid rim and native player fall";
            }
            finally{presentation.enabledShots=shots;Application.runInBackground=background;}
        }
    }
}
