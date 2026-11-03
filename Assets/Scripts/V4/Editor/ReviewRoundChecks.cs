using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4.Editor
{
    public static class ReviewRoundChecks
    {
        public static string Result {get;private set;}
        static int sequence;
        static void Require(bool ok,string message){if(!ok){Result="FAIL: "+message;throw new InvalidOperationException(Result);}}
        static void Sentence(CognitionState s,string text)=>s.TryAcquireSentence("review."+(++sequence),text);
        static void Place(Component item,Vector3 p){item.GetComponent<Rigidbody>().position=p;item.transform.position=p;Physics.SyncTransforms();}
        public static void Run(){Result="Running";var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();flow.StartCoroutine(Check(flow));}
        static IEnumerator Check(OpeningFlow flow)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try
            {
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();yield return new WaitForSecondsRealtime(2);
                flow.player.enabled=false;flow.lightField.enabled=false;
                var s=flow.board.State;var e=flow.effects;var p=e.presentation;
                foreach(var word in flow.board.Catalog.Words)s.TryAcquireWord("review.learn."+word.Id,word);
                p.enabled=false;flow.board.Panel.SetOpen(true);
                var all=e.lights.Lamps.ToArray();foreach(var lamp in all.Skip(2))lamp.gameObject.SetActive(false);
                var a=all[0];var b=all[1];var origin=flow.player.transform.position;
                flow.player.transform.rotation=Quaternion.identity;flow.player.view.localRotation=Quaternion.identity;
                Place(a,new Vector3(origin.x,6,origin.z+15));Place(b,new Vector3(origin.x,2,origin.z-15));
                Require(Mathf.Abs(e.lights.LowestBottom(true)-a.GetComponent<Collider>().bounds.min.y)<.001f,"visible lowest excludes lower lamp behind player");
                var door=flow.door;door.Reveal(flow.player.view);
                var appearance=door.GetComponent<DoorStateEffects>();
                Require(appearance.IsOn&&!appearance.IsOff&&!appearance.PassageEnabled,"initial door is on but airborne cannot pass");
                Place(door,new Vector3(origin.x+15,door.groundHeight,origin.z+15));door.SetDescending(true);
                yield return new WaitForFixedUpdate();door.SetDescending(false);
                Require(door.LandingCompleted&&appearance.PassageEnabled,"initial on door enables passage at landing without another on sentence");
                float top=flow.lightField.ground.bounds.max.y;
                Sentence(s,"ground light move");yield return new WaitForSecondsRealtime(.6f);
                Require(Mathf.Abs(flow.lightField.ground.bounds.max.y-top)<.01f,"landed door blocks rising ground");
                s.ClearWorkspace();Place(door,new Vector3(origin.x+15,9,origin.z+15));
                b.gameObject.SetActive(false);Place(a,new Vector3(origin.x,3,origin.z+15));
                Sentence(s,"ground door move");yield return new WaitForSecondsRealtime(2);
                float stopped=flow.lightField.ground.bounds.max.y;
                Require(stopped>top+.1f&&stopped<=a.GetComponent<Collider>().bounds.min.y&&e.world.Waiting,"intervening lamp blocks ascent toward door");
                s.ClearWorkspace();yield return new WaitForSecondsRealtime(.2f);
                Require(Mathf.Abs(flow.lightField.ground.bounds.max.y-stopped)<.001f,"split holds ground height");
                // Place both targets far enough apart to measure unconstrained clustering speed.
                b.gameObject.SetActive(true);origin=flow.player.transform.position;
                Place(a,origin+new Vector3(-5,4,15));Place(b,origin+new Vector3(5,4,15));
                Sentence(s,"light light move");yield return new WaitForFixedUpdate();
                var before=a.transform.position;float start=Time.fixedTime;
                yield return new WaitForSeconds(.5f);
                float speed=Vector3.Distance(before,a.transform.position)/(Time.fixedTime-start);
                Require(speed>.9f&&speed<=e.lights.clusterSpeed+.05f,"cluster uses slower speed: "+speed);
                s.ClearWorkspace();
                // Start each winning rule without a cinematic, then add the conflicting sentence with feedback enabled.
                var pairs=new[]{new[]{"light move","light ground move"},new[]{"door move","door ground move"},new[]{"ground move","ground door move"}};
                foreach(var pair in pairs)
                {
                    p.enabled=false;s.ClearWorkspace();Sentence(s,pair[0]);p.enabled=true;
                    int weak=p.feedback.WeakCount,strong=p.feedback.StrongCount,shots=p.PlayCount;
                    Sentence(s,pair[1]);yield return new WaitForSecondsRealtime(.1f);
                    var group=s.Groups.Last();
                    Require(group.Conflicted&&flow.board.GroupRect(group.Id).GetComponent<CanvasGroup>().alpha<.3f,"conflict dims: "+pair[1]);
                    Require(p.feedback.WeakCount==weak+1&&p.feedback.StrongCount==strong&&p.PlayCount==shots&&!p.Playing,"conflict weak ripple without stars or shot: "+pair[1]);
                    Require(GameObject.Find("Sentence grammar ripple"),"conflict ripple remains rendered");
                    yield return new WaitForSecondsRealtime(1);
                }
                p.enabled=false;s.ClearWorkspace();
                var environment=e.world.environment;top=flow.lightField.ground.bounds.max.y;
                Sentence(s,"ground move");yield return new WaitForSecondsRealtime(.3f);
                Require(environment.WorldMoving&&environment.clouds.FollowFacing&&environment.clouds.speedMultiplier>1&&Mathf.Abs(flow.lightField.ground.bounds.max.y-top)<.001f,"day ground move accelerates clouds without translating ground");
                s.ClearWorkspace();Require(!environment.WorldMoving,"split stops environment movement rule");
                string key="PuzzleApple.Tests.Review."+Guid.NewGuid().ToString("N");
                var progress=new IntroHintProgress(key);progress.Record(CognitionSignal.LightMove);
                try { Require(progress.Has(CognitionSignal.LightMove)&&PlayerPrefs.HasKey(key)&&new IntroHintProgress(key).Has(CognitionSignal.LightMove),"hint progress persists"); }
                finally { progress.Clear(); }
                Result="PASS: visible lowest, binary initial door and landing, door/lamp ascent blockers, held height, cluster speed, all motion conflict ripples/dimming/no shots, day ground movement, saved hints";
            }
            finally{Application.runInBackground=background;}
        }
    }
}
