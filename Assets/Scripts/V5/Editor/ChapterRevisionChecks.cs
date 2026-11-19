using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class ChapterRevisionChecks
    {
        public static string Result {get;private set;}
        static void Check(bool value,string label){if(!value){Result="FAIL: "+label;throw new Exception(Result);}}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Exercise(e));}
        static void Walk(FirstPersonController p,Vector2 direction,int count){
            var method=typeof(FirstPersonController).GetMethod("AdvanceMovement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int i=0;i<count;i++)method.Invoke(p,new object[]{direction,true,1f/120,false});
        }
        static void Bump(FirstPersonController player,Collider obstacle,string label){
            var motor=player.GetComponent<CharacterController>();var b=obstacle.bounds;
            bool found=false;RaycastHit hit=new RaycastHit();Vector3 origin=Vector3.zero;
            for(int i=0;i<9&&!found;i++){
                origin=new Vector3(Mathf.Lerp(b.min.x,b.max.x,(i+.5f)/9),b.min.y+Mathf.Min(b.size.y*.5f,motor.height*.5f),b.min.z-2);
                found=obstacle.Raycast(new Ray(origin,Vector3.forward),out hit,b.size.z+4);
            }
            Check(found,"contact surface available: "+label);
            player.Teleport(origin-Vector3.up*motor.center.y,Quaternion.identity);Physics.SyncTransforms();
            var flags=CollisionFlags.None;
            for(int i=0;i<80;i++)flags|=motor.Move(Vector3.forward*.05f);
            Check((flags&CollisionFlags.Sides)!=0 && player.transform.position.z<hit.point.z+.1f,"native character collision: "+label);
            Check(!Physics.ComputePenetration(motor,player.transform.position,player.transform.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth)||depth<=motor.skinWidth+.01f,"no penetration: "+label);
        }
        static IEnumerator Exercise(WorldEntry e){
            yield return VitruvianChecks.Gallery(e);
            var pass=e.outdoor;var c=pass.Chapter;var g=pass.Garden;var v=pass.Vitruvian;var p=e.player;
            foreach(var room in new[]{c,g,v}){
                var objects=room.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                Check(!objects.Any(t=>t.name.Contains("entrance tread")||t.name.StartsWith("Vitruvian tread")||t.name=="Threshold blossoms"||t.name=="PortraitBouquet"||t.name=="Right painting"||t.name.StartsWith("Blank painting")||t.name=="frame" && t.parent.name=="Corridor"),"removed art and steps in "+room.name);
                Check(Mathf.Abs(room.transform.lossyScale.x-.8f)<.001f,"uniform chapter scale");
            }
            Check(Mathf.Abs(g.floor.bounds.max.y-c.floor.bounds.max.y-(p.jumpHeight-.15f))<.002f,"garden sill above actual floor");
            Check(Mathf.Abs(v.floor.bounds.max.y-g.floor.bounds.max.y)<.002f && Mathf.Abs(v.floor.bounds.size.z-14.4f)<.002f,"room depth and flush floors");
            Check(Vector3.Distance(Vector3.Scale(c.boxFill.interior.size,c.boxFill.transform.lossyScale),Vector3.one*6)<.001f,"arrival box remains six metres");
            Check(Mathf.Abs(p.WalkMultiplier-1)<.001f,"arrival walking speed unchanged");
            VitruvianChecks.Say(e.board,"positive","world");
            while(!c.box.Opened){
                foreach(var surface in c.box.GetComponentsInChildren<MeshRenderer>()){
                    var b=surface.bounds;
                    Check(b.min.z>=c.floor.bounds.min.z-.01f && b.max.z<=c.floor.bounds.max.z+.01f,"fold stays inside corridor: "+surface.name);
                    Check(b.min.x>=c.floor.bounds.min.x-.15f && b.max.x<=c.floor.bounds.max.x+.15f,"fold stays inside end walls: "+surface.name);
                }
                yield return null;
            }
            yield return ChargeDoorChecks.Wait(()=>!e.Busy,"opening finishes");
            p.Teleport(c.arrival.position+Vector3.left*8,Quaternion.identity);p.MovementLocked=false;
            Walk(p,Vector2.zero,1);Check(p.WalkMultiplier>1 && p.WalkMultiplier<1.3f,"speed changes smoothly");
            Walk(p,Vector2.zero,70);Check(Mathf.Abs(p.WalkMultiplier-1.3f)<.001f,"gallery walking multiplier");
            p.Teleport(c.arrival.position,Quaternion.identity);Walk(p,Vector2.zero,1);
            Check(p.WalkMultiplier>1 && p.WalkMultiplier<1.3f,"box speed eases back");
            Walk(p,Vector2.zero,70);Check(Mathf.Abs(p.WalkMultiplier-1)<.001f,"box restores walking speed");
            foreach(var room in new[]{c,g,v}){
                var b=room.floor.bounds;
                p.Teleport(new Vector3(b.center.x,b.max.y+.08f,b.min.z+1.2f),Quaternion.LookRotation(Vector3.back));
                Walk(p,Vector2.up,150);
                Check(p.transform.position.z>b.min.z-.1f && p.transform.position.y>b.max.y-.1f,"wall and floor collision in "+room.name);
                Check(Mathf.Abs(p.WalkMultiplier-1.3f)<.001f,"side room walking multiplier");
            }
            var exhibit=c.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="Enlarged balance and showcase");
            var cases=exhibit.GetComponentsInChildren<Collider>().Where(x=>x.enabled).ToArray();
            Check(cases.Length>0 && cases.All(x=>x.bounds.size.sqrMagnitude>0),"showcase retains scaled colliders");
            Bump(p,cases.OrderByDescending(x=>x.bounds.size.sqrMagnitude).First(),"showcase");
            var sculpture=v.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Collider>(true)).Where(x=>x.name.Contains("Vitruvian")||x.transform.parent && x.transform.parent.name.Contains("Vitruvian_Body")).ToArray();
            Check(sculpture.Length>0,"sculpture retains collision");
            Bump(p,sculpture.First(x=>x.name=="Vitruvian_Body_15k_Relaxed"),"sculpture");
            Bump(p,v.puzzle.upper.GetComponentInChildren<Collider>(),"corner mirror mechanism");
            Check(Mathf.Abs(Vector3.Distance(v.puzzle.ball.transform.position,v.puzzle.pivot.position)-v.puzzle.height*v.puzzle.navelRatio*.8f)<.002f,"orb radius follows scale");
            VitruvianChecks.Capture(p,c.arrival.position+Vector3.up*1.55f,c.floor.bounds.center+Vector3.up*4,"chapter-scaled-gallery");
            VitruvianChecks.Capture(p,v.transform.TransformPoint(new Vector3(0,2,1)),v.puzzle.transform.TransformPoint(Vector3.up*5),"chapter-scaled-vitruvian");
            Result="PASS: removed art, uniform scale, flush jump sills, six metre box, folding bounds, smooth speed, room collisions and scaled orbit";
        }
    }
}
