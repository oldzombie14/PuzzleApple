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
                Check(e.chamberLight.TryCollect(),"floor uses normal collection path");yield return Until(()=>!e.chamberLight.Collecting);
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
                state.TryAcquireSentence("observe.light.move","light move");
                state.TryAcquireSentence("check.floor","light floor move");
                var moving=state.Groups.First(g=>g.RuleId=="light.move");
                var descending=state.Groups.First(g=>g.RuleId=="light.floor.move");
                Check(moving.Effective&&descending.Conflicted&&!j.floorMotion.Active,"earlier drift blocks descent");
                state.ReturnToLibrary(moving.Id,0,true);
                yield return Until(()=>j.floorMotion.Active);
                Check(descending.Effective&&j.floorMotion.Active,"removing earlier motion activates descent");
                state.TryAcquireSentence("check.later.drift","light move");
                Check(state.Groups.First(g=>g.RuleId=="light.move").Conflicted,"earlier descent blocks later drift");
                state.ReturnToLibrary(state.Groups.First(g=>g.RuleId=="light.move").Id,0,true);
                if(lightFirst)j.SetLights(true);
                for(int i=0;i<j.Lamps.Count;i++)
                    j.Lamps[i].GetComponent<Rigidbody>().position=j.Root.TransformPoint(new Vector3(-5+(i%4)*3,4,j.pathLength+3+(i/4)*3));
                Physics.SyncTransforms();yield return new WaitForFixedUpdate();
                var before=body.position;yield return new WaitForSeconds(.2f);
                Check(body.position.y<before.y-.2f&&Mathf.Abs(body.position.x-before.x)<.001f&&Mathf.Abs(body.position.z-before.z)<.001f,"dark descent preserves horizontal coordinates");
                var detached=state.Detach(descending.Id,descending.Words.Last().Id);
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                before=body.position;yield return new WaitForSeconds(.25f);
                Check(Vector3.Distance(body.position,before)<.001f,"split pauses descent");
                Check(!floor.HoleOpen,"lights alone cannot open hole before arrival");
                state.Join(detached.Id,descending.Id,false);
                yield return Until(()=>j.floorMotion.ArrivedCount==j.Lamps.Count);
                if(!lightFirst)Check(!floor.HoleOpen,"all arrived in darkness does not open hole");
                j.SetLights(true);yield return Until(()=>floor.HoleOpen);Physics.SyncTransforms();
                var centre=j.Root.TransformPoint(new Vector3(0,.2f,j.pathLength+j.roomSize.z*.5f));
                Check(!Physics.Raycast(centre,Vector3.down,2,~0,QueryTriggerInteraction.Ignore),"centre has no hidden collision floor");
                Check(Physics.Raycast(centre+j.Root.right*(floor.holeSize*.5f+1),Vector3.down,2,~0,QueryTriggerInteraction.Ignore),"rim remains solid");
                e.player.Teleport(centre,j.Root.rotation);yield return new WaitForSecondsRealtime(1.2f);
                Check(e.player.transform.position.y<centre.y-3||e.outdoor&&(e.outdoor.Transitioning||e.outdoor.IsOutside),"native player falls through centre opening into the outdoor passage");
                Result="PASS (light first="+lightFirst+"): floor collection and native impacts, particles, both motion priorities, vertical trajectory, split pause/rejoin, all-arrived plus light gate, solid rim and native player fall";
            }
            finally{presentation.enabledShots=shots;Application.runInBackground=background;}
        }
    }
}
