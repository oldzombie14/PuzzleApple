using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class SurfaceArtChecks
    {
        public static string Result {get;private set;}
        static void Check(bool value,string message){if(!value){Result="FAIL: "+message;throw new Exception(Result);}}
        static IEnumerator Until(Func<bool> ready)
        {float end=Time.realtimeSinceStartup+20;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(ready(),"transition timeout");}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Run(e));}
        static IEnumerator Run(WorldEntry e)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                e.EnterWorld();yield return Until(()=>e.Entered);e.OpenWorld(-1);yield return Until(()=>e.room.Opened&&!e.Busy);
                var j=e.journey;var p=e.player;var path=j.Root.Find("White path");
                p.Teleport(j.Root.TransformPoint(new Vector3(0,.05f,7)),j.Root.rotation);e.board.Panel.SetOpen(false);
                yield return new WaitForSeconds(.8f);
                var properties=new MaterialPropertyBlock();path.GetComponent<Renderer>().GetPropertyBlock(properties);
                Check(properties.GetFloat("_CorridorPureWhite")==1,"static pure white path");
                Check(!j.Root.GetComponentsInChildren<ParticleSystem>(true).Any(ps=>ps.name.Contains("bridge")),"no bridge particle systems created");
                Check(path.GetComponent<Collider>().enabled&&Physics.Raycast(j.Root.TransformPoint(new Vector3(0,1,1)),-j.Root.up,2),"continuous path collision retained");
                float before=j.Root.InverseTransformPoint(p.transform.position).z;
                var advance=typeof(FirstPersonController).GetMethod("AdvanceMovement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                for(int i=0;i<20;i++){advance.Invoke(p,new object[]{Vector2.down,true,.026f,false});yield return null;}
                Check(j.Root.InverseTransformPoint(p.transform.position).z<before-.8f,"S backward movement retained");
                p.transform.rotation=j.Root.rotation*Quaternion.Euler(0,180,0);
                before=j.Root.InverseTransformPoint(p.transform.position).z;
                for(int i=0;i<20;i++){advance.Invoke(p,new object[]{Vector2.up,true,.026f,false});yield return null;}
                Check(j.Root.InverseTransformPoint(p.transform.position).z<before-.8f,"turned W return retained");
                p.Teleport(j.Root.TransformPoint(new Vector3(0,.05f,j.pathLength+4)),j.Root.rotation);j.SetLights(true);
                yield return Until(()=>j.Brightness>=1);yield return null;
                var f=j.floorSurface;var ceiling=j.Root.Find("Room ceiling").GetComponent<Renderer>();
                var lamp=j.Lamps[0];var body=lamp.GetComponent<Rigidbody>();body.position=j.Root.TransformPoint(new Vector3(0,j.roomSize.y-.5f,j.pathLength+7));
                lamp.direction=j.Root.up;Physics.SyncTransforms();int count=f.WallImpactCount;
                yield return Until(()=>f.WallImpactCount>count&&Vector3.Dot(lamp.direction,j.Root.up)<0);
                Check(f.WallImpactCount>count,"native ceiling contact creates ripple and reflects");
                var wall=j.Root.Cast<Transform>().First(t=>t.name=="Room side wall").GetComponent<Renderer>();
                var point=j.Root.TransformPoint(new Vector3(-j.roomSize.x*.5f+.15f,j.roomSize.y-.2f,j.pathLength+7));
                Check(f.ImpactWall(wall.GetComponent<Collider>(),point,j.Root.right),"wall impact accepted");yield return null;
                var shared=f.Surface.sharedMaterial.GetVectorArray("_Impacts");
                Check(shared.SequenceEqual(wall.sharedMaterial.GetVectorArray("_Impacts"))&&shared.SequenceEqual(ceiling.sharedMaterial.GetVectorArray("_Impacts")),"wall floor ceiling share timed spatial wave sources");
                Check(wall.sharedMaterial.GetVector("_RippleTaper").z<1&&wall.sharedMaterial.GetFloat("_LightStepBlend")==0,"distance thinning and smooth room lighting configured");
                Check(!f.steppedLighting,"room lighting defaults to smooth shading");
                f.contactParticles=false;var particles=j.Root.Find("Floor impact squares").GetComponent<ParticleSystem>();particles.Clear();
                f.ImpactWall(wall.GetComponent<Collider>(),point,j.Root.right);Check(particles.particleCount==0,"contact particle trial can be disabled");f.contactParticles=true;
                j.SetLights(false);yield return Until(()=>j.Brightness==0);yield return null;
                Check(wall.sharedMaterial.GetFloat("_Brightness")==j.SurfaceBrightness,"walls retain dim fill");
                e.sentenceRuntime.presentation.enabledShots=false;
                e.board.State.ClearWorkspace();
                p.Teleport(Vector3.up*.05f,Quaternion.identity);yield return null;yield return null;
                Check(!j.BeyondChamber,"returned to original room");
                var original=e.chamberLight;var origin=original.transform.position;
                e.board.State.TryAcquireSentence("check.original.move","light move");
                yield return new WaitForSeconds(.5f);
                Check(original.Moving&&Vector3.Distance(origin,original.transform.position)>.2f,"original lamp moves after using light move on return");
                var command=e.board.State.Groups.First(g=>g.RuleId=="light.move");
                var moveWord=e.board.State.Detach(command.Id,command.Words.Last().Id);
                yield return new WaitForFixedUpdate();origin=original.transform.position;
                yield return new WaitForSeconds(.25f);
                Check(!original.Moving&&Vector3.Distance(origin,original.transform.position)<.01f,"split stops original lamp");
                e.board.State.Join(moveWord.Id,command.Id,false);original.SetOn(false);
                yield return new WaitForSeconds(.5f);
                Check(original.Moving&&Vector3.Distance(origin,original.transform.position)>.2f,"rejoin resumes original lamp independently of off light");
                e.sentenceRuntime.presentation.enabledShots=true;
                Result="PASS: static white path without bridge particles, backward movement, room surface regression, returned chamber light move, split pause and rejoin resume";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
