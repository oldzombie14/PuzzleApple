using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4.Editor
{
    public static class ReviewRoundTwoChecks
    {
        public static string Result {get;private set;}
        static int sequence;
        static void Require(bool ok,string message){if(!ok){Result="FAIL: "+message;throw new InvalidOperationException(Result);}}
        static void Sentence(CognitionState s,string text)=>s.TryAcquireSentence("round2."+(++sequence),text);
        static void Place(Component c,Vector3 p){c.GetComponent<Rigidbody>().position=p;c.transform.position=p;Physics.SyncTransforms();}
        public static void Run(){Result="Running";var f=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();f.StartCoroutine(Check(f));}
        static IEnumerator Weak(OpeningFlow f,string text)
        {
            var p=f.effects.presentation;f.board.State.ClearWorkspace();f.board.Panel.SetOpen(true);p.enabled=true;
            int weak=p.feedback.WeakCount,strong=p.feedback.StrongCount,shots=p.PlayCount;
            Sentence(f.board.State,text);yield return new WaitForSecondsRealtime(.35f);
            Require(p.feedback.WeakCount==weak+1&&p.feedback.StrongCount==strong&&p.PlayCount==shots&&!p.Playing,"weak-only feedback: "+text);
            p.enabled=false;
        }
        static IEnumerator Check(OpeningFlow f)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();yield return new WaitForSecondsRealtime(2);
                f.player.enabled=false;f.lightField.enabled=false;f.effects.inscriptions.enabled=false;
                f.board.Panel.SetOpen(false);f.player.Teleport(new Vector3(0,1,0),Quaternion.identity);f.player.view.localRotation=Quaternion.identity;
                var s=f.board.State;var e=f.effects;var p=e.presentation;var lamp=f.lamp;var env=e.world.environment;
                p.enabled=false;foreach(var word in f.board.Catalog.Words)s.TryAcquireWord("round2.learn."+word.Id,word);
                var lamps=e.lights.Lamps.ToArray();foreach(var l in lamps.Skip(2))l.gameObject.SetActive(false);
                var a=lamps[0];var b=lamps[1];Place(a,new Vector3(-3,7,15));Place(b,new Vector3(3,7,15));
                Sentence(s,"negative light");yield return new WaitForSecondsRealtime(.15f);
                Require(env.NightBlend>0&&lamp.Brightness>.999f,"sky starts before lamps dim");
                yield return new WaitForSecondsRealtime(.7f);
                Require(lamp.Brightness>.5f&&lamp.Brightness<1&&env.NightBlend>1-lamp.Brightness,"sky leads overlapping slow lamp fade");
                s.ClearWorkspace();float held=lamp.Brightness,sky=env.NightBlend;
                yield return new WaitForSecondsRealtime(.3f);
                Require(Mathf.Abs(lamp.Brightness-held)<.0001f&&Mathf.Abs(env.NightBlend-sky)<.0001f,"split holds both transition values");
                Sentence(s,"negative light");yield return new WaitForSecondsRealtime(3);
                Require(lamp.IsOff&&!e.lights.CanOperate&&f.door.Revealed,"full blackout disables light operations and reveals door");
                foreach(var text in new[]{"light move","light ground move","light door move","light light move","door light move","ground light move"})
                {
                    var before=a.transform.position;var doorBefore=f.door.transform.position;float groundBefore=f.lightField.ground.bounds.max.y;
                    yield return Weak(f,text);
                    Require(Vector3.Distance(before,a.transform.position)<.01f&&Vector3.Distance(doorBefore,f.door.transform.position)<.01f&&Mathf.Abs(groundBefore-f.lightField.ground.bounds.max.y)<.001f,"off gate stops motion: "+text);
                }
                s.ClearWorkspace();Sentence(s,"light move");Sentence(s,"positive light");
                yield return new WaitForSecondsRealtime(.15f);Require(lamp.IsOff&&env.NightBlend<1,"day leads lamp turn-on symmetrically");
                yield return new WaitForSecondsRealtime(.7f);Require(lamp.Brightness>0&&lamp.Brightness<.5f&&f.lightField.Moving,"turn-on restores eligible motion continuously");
                yield return new WaitForSecondsRealtime(2.3f);Require(lamp.IsOn,"lamp finishes natural turn-on");s.ClearWorkspace();
                var d=f.door;var appearance=d.GetComponent<DoorStateEffects>();
                Sentence(s,"negative door");Require(appearance.Brightness>.999f,"door light does not snap off");
                yield return new WaitForSecondsRealtime(.4f);
                Require(appearance.Brightness>0&&appearance.Brightness<1&&d.GetComponentsInChildren<ThresholdFish>().All(x=>x.Paused),"door dims while fish pause");
                s.ClearWorkspace();held=appearance.Brightness;yield return new WaitForSecondsRealtime(.3f);
                Require(Mathf.Abs(held-appearance.Brightness)<.0001f,"split holds intermediate door brightness");
                Sentence(s,"negative door");yield return new WaitForSecondsRealtime(2);Require(appearance.Brightness==0&&d.GetComponentsInChildren<Light>().All(x=>!x.enabled),"door fade completes");s.ClearWorkspace();
                // Every observer quadrant must keep the physical frame upright and facing the observer.
                Place(d,new Vector3(0,8,15));
                foreach(var offset in new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left})
                {
                    f.player.Teleport(d.transform.position+offset*12,Quaternion.identity);yield return new WaitForSecondsRealtime(.1f);
                    var bearing=Vector3.ProjectOnPlane(d.transform.position-f.player.view.position,Vector3.up).normalized;
                    Require(Vector3.Dot(d.transform.up,Vector3.up)>.9999f&&Vector3.Dot(d.transform.forward,bearing)>.999f,"upright door tracks player around frame");
                }
                f.player.Teleport(new Vector3(0,1,0),Quaternion.identity);f.player.view.localRotation=Quaternion.identity;
                Place(d,new Vector3(10,d.groundHeight+.4f,15));Sentence(s,"door move");d.GetComponent<LightMotion>().RestoreDirection(Vector3.down);
                yield return new WaitForSecondsRealtime(.5f);
                Require(!d.LandingCompleted&&d.GetComponent<LightMotion>().Direction.y>0,"free ground contact bounces without committing landing");
                Place(d,new Vector3(10,d.groundHeight+.25f,15));s.ClearWorkspace();
                Require(d.LandingCompleted&&Mathf.Abs(d.transform.position.y-d.groundHeight)<.01f,"near-ground split snaps to surface and commits");
                Place(d,new Vector3(0,4,15));Place(a,new Vector3(0,2,15));b.gameObject.SetActive(false);
                Sentence(s,"door ground move");yield return new WaitForSecondsRealtime(3);
                Require(d.Landed,"directed descent passes lamp under bottom frame");s.ClearWorkspace();
                Place(d,new Vector3(0,8,15));Place(a,new Vector3(0,11,15));
                yield return Weak(f,"door light move");
                Place(a,new Vector3(0,.5f,15));yield return Weak(f,"ground light move");yield return Weak(f,"ground door move");s.ClearWorkspace();
                // Actual pitch tracking while the selected light group descends, at low and high heights.
                foreach(float height in new[]{3f,12f})
                {
                    Place(a,new Vector3(0,height,12));f.player.transform.rotation=Quaternion.identity;f.player.view.localRotation=Quaternion.identity;
                    f.board.Panel.SetOpen(true);p.enabled=true;Sentence(s,"light ground move");
                    float deadline=Time.realtimeSinceStartup+10;
                    while(!p.CanInterrupt&&Time.realtimeSinceStartup<deadline)yield return null;
                    Require(p.CanInterrupt&&p.TrackingTarget,"descending light shot tracks at height "+height);
                    float elevation=f.player.view.forward.y;yield return new WaitForSecondsRealtime(.65f);
                    Require(f.player.view.forward.y<elevation-.01f&&Mathf.Abs(f.player.view.forward.x)<.001f&&Mathf.Abs(f.player.view.right.y)<.001f,"descending light camera follows downward in pitch only");
                    var rotation=f.player.view.rotation;Require(p.InterruptForEditing(),"shot interrupt available");
                    Require(Quaternion.Angle(rotation,f.player.view.rotation)<.1f,"interrupt retains current orientation");
                    p.enabled=false;s.ClearWorkspace();
                }
                var on=p.shots.First(x=>x.rule.Id=="light.on");var off=p.shots.First(x=>x.rule.Id=="light.off");
                Require(on.target==off.target&&on.aim==off.aim&&on.targetViewportY==off.targetViewportY&&on.enterDuration==off.enterDuration&&!on.heightBasedPitchSpeed,"on/off horizon shots are symmetric");
                Result="PASS: sky-led symmetric fades/held values, six closed-light motion gates and weak feedback, gradual door light and hold, upright continuous facing, free bounce/split landing, descent through lamps, proximity feedback, low/high pitch follow and retained interruption";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
