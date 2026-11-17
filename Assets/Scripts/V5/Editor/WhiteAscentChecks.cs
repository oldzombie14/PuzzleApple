using System.Collections;
using System.Linq;
using UnityEngine;
using Wait=PuzzleApple.V5.Editor.ChargeDoorChecks;

namespace PuzzleApple.V5.Editor
{
    public static class WhiteAscentChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string message){if(ok)return;Result="FAIL: "+message;throw new System.Exception(Result);}
        public static void Run(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(Route(e));
        }
        public static IEnumerator Leave(WorldEntry e){
            // Route through the actual altar click, including transition and input restoration.
            var j=e.journey;var a=j.Ascent;var p=e.player;
            p.MovementLocked=false;e.board.Panel.SetOpen(false);
            var top=a.Landing.position+Vector3.up*.36f-a.transform.forward*.75f;
            p.Teleport(top,a.transform.rotation);yield return null;
            p.view.LookAt(a.Symbol.position);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;
            yield return null;Check(a.WhiteAmount>.99f && a.LookingAtSymbol(),"white altar is visible and interactive");
            Check(a.Symbol.GetComponentsInChildren<ParticleSystem>().All(ps=>!ps.isPlaying && ps.particleCount==0),"quiet altar before clicking");
            var aim=p.view.rotation;
            e.chamberLight.AdvancePointer(true,true,false,.02f);e.chamberLight.AdvancePointer(false,false,true,.03f);
            yield return new WaitForSecondsRealtime(.32f);
            Check(Quaternion.Angle(aim,p.view.rotation)<.2f,"altar keeps the player's aim");
            var rings=a.GetComponentsInChildren<LineRenderer>().Where(r=>r.name=="Altar ripple").ToArray();
            Check(rings.Length==2 && rings.All(r=>r.enabled && Vector3.Distance(r.transform.position,a.Symbol.position)<.01f),"both ripples expand from the symbol");
            var particles=a.Symbol;
            Check(particles.Find("Rising motes").GetComponent<ParticleSystem>().particleCount>0,"white motes at ignition");
            Check(particles.Find("Ripple stars").GetComponent<ParticleSystem>().particleCount==16,"both rings release black stars");
            yield return new WaitForSecondsRealtime(1);
            Check(Quaternion.Angle(aim,p.view.rotation)<.2f,"steady view through ignition");
            yield return Wait.Wait(()=>e.outdoor.IsOutside,"outdoor activation");
            Check(Vector3.Angle(p.view.forward,p.transform.forward)<1,"arrives level under the flash");
            var overlay=e.blackBackground.transform.parent.Find("Ascent white flash").GetComponent<UnityEngine.UI.Image>();
            Check(overlay.gameObject.activeInHierarchy && overlay.color.r>.99f && overlay.color.a>.98f && p.CinematicLocked,"full white covers the actual scene switch");
            float switched=Time.realtimeSinceStartup;
            yield return Wait.Wait(()=>!a.Playing,"white flash recovery");
            Check(Time.realtimeSinceStartup-switched>=2.7f && Vector3.Angle(p.view.forward,p.transform.forward)<1,"three-second recovery and level view");
            Check(a.Completed && e.board.State.Groups.Count==0 && !p.CinematicLocked,"completion, cleared workspace and restored control");
        }
        public static IEnumerator Revisit(WorldEntry e){
            var a=e.journey.Ascent;
            e.player.Teleport(a.Landing.position+Vector3.up*.4f,a.transform.rotation);
            yield return Wait.Wait(()=>e.outdoor.IsOutside && !a.Playing,"ordinary upper crossing");
            Check(a.WhiteAmount==0 && !a.Symbol.gameObject.activeSelf,"no second altar");
        }
        public static void RunStars(){
            var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Result="Running";e.StartCoroutine(StarCollection(e));
        }
        static IEnumerator StarCollection(WorldEntry e){
            e.OpenWorld(-1);yield return Wait.Wait(()=>e.room.Opened&&!e.Busy,"open world");
            var a=e.journey.Ascent;var p=e.player;var light=e.chamberLight;
            e.board.Panel.SetOpen(false);p.MovementLocked=true;
            Vector3 lower=a.transform.TransformPoint(new Vector3(0,1.8f,5));
            p.Teleport(lower,a.transform.rotation);Cursor.lockState=CursorLockMode.Locked;
            yield return null;yield return new WaitForEndOfFrame();
            var stars=a.Stars.GetComponentsInChildren<MeshRenderer>();
            Check(stars.Length==a.starCount && a.WhiteAmount==0,"lower flight on black");
            var target=stars.OrderBy(r=>Vector3.Distance(p.view.position,r.transform.position)).First(r=>{
                var ray=new Ray(p.view.position,r.transform.position-p.view.position);
                return light.PickStar(ray,light.collectionDistance,out _)==r;
            });
            var anchor=target.transform.localPosition;
            p.view.LookAt(target.transform);p.AdoptCurrentView();
            Check(!e.board.State.Knows("light") && light.TryCollect(),"white star teaches the light noun");
            yield return Wait.Wait(()=>e.board.State.Knows("light")&&!light.Collecting,"star acquisition");
            var bob=target.transform.localPosition-anchor;
            Check(Mathf.Abs(bob.x)+Mathf.Abs(bob.z)<.0001f && Mathf.Abs(bob.y)<=.25f,"bounded bob about a fixed location");
            e.board.Panel.SetOpen(false);
            p.Teleport(a.Landing.position+Vector3.up*.4f,a.transform.rotation);yield return null;yield return new WaitForEndOfFrame();
            var dark=stars.OrderBy(r=>Vector3.Distance(p.view.position,r.transform.position)).First();
            p.view.LookAt(dark.transform);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;
            var rayDark=new Ray(p.view.position,p.view.forward);
            Check(a.WhiteAmount>.99f && dark.enabled && dark.sharedMaterial.GetColor("_EmissionColor").maxColorComponent<.01f,"black star remains visible on white");
            Check(!a.Stars.Pick(rayDark,light.collectionDistance) && !light.TryCollect(),"black star cannot supply light");
            p.Teleport(lower,a.transform.rotation);yield return null;yield return new WaitForEndOfFrame();
            p.view.LookAt(target.transform);p.AdoptCurrentView();Cursor.lockState=CursorLockMode.Locked;
            Check(a.WhiteAmount==0 && light.TryCollect() && light.KnownFeedbackWord=="light","descending restores white-star interaction");
            Result="PASS: anchored stars, first noun acquisition, dark non-collectible stars and known feedback on return";
        }
        static IEnumerator Route(WorldEntry e){
            yield return ChargeDoorChecks.OpenEnding(e);
            var j=e.journey;var d=j.EndingDoor;var a=j.Ascent;var p=e.player;
            var midpoint=a.transform.TransformPoint(new Vector3(0,a.totalRise*.5f+.1f,a.TopZ-32*a.stepRun));
            p.Teleport(midpoint,a.transform.rotation);yield return new WaitForEndOfFrame();
            Check(a.transform.Cast<Transform>().Count(t=>t.name=="White tread")==64 && Mathf.Abs(a.WhiteAmount-.5f)<.07f,"64 steps and half-white at the physical midpoint");
            var advance=typeof(FirstPersonController).GetMethod("AdvanceMovement",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            p.Teleport(d.RoomLanding,d.transform.rotation);
            for(int i=0;i<2500 && a.WhiteAmount<.999f;i++){
                advance.Invoke(p,new object[]{Vector2.up,true,1f/120,false});
                if(i%12==0)yield return null;
            }
            Check(a.WhiteAmount>=.999f,"native walking through raised door and up continuous slope");
            Check(a.transform.Find("Fading lower world").GetComponent<Renderer>().enabled,"white veil hides room exteriors viewed from above");
            for(int i=0;i<1800 && a.WhiteAmount>0;i++){
                advance.Invoke(p,new object[]{Vector2.down,true,1f/120,false});
                if(i%12==0)yield return null;
            }
            Check(a.WhiteAmount==0,"walking down restores indoor view");
            e.board.State.TryAcquireSentence("ascent.world","light world");
            yield return Leave(e);
            var frame=e.outdoor.World.door.transform;
            e.player.Teleport(frame.TransformPoint(new Vector3(0,.12f,-.55f)),frame.rotation);yield return null;yield return null;
            p.GetComponent<CharacterController>().Move(frame.forward*1.2f);
            yield return Wait.Wait(()=>!e.outdoor.IsOutside&&!e.outdoor.Transitioning,"white door returns indoors");
            Check(Vector3.Distance(p.transform.position,d.RoomLanding)<1 && !a.Symbol.gameObject.activeSelf,"room-side return and retained completion");
            yield return Revisit(e);
            Result="PASS: charged doorway, physical ascent and reversible whiteness, altar click, full white transition, level view, return and ordinary revisit; outdoor activation "+e.outdoor.ActivationMilliseconds.ToString("F1")+" ms";
        }
    }
}
