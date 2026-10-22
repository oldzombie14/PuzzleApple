using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4.Editor
{
    public static class SentenceRuntimeChecks
    {
        public static string Result { get; private set; }
        static int sequence;
        static void Require(bool condition,string message)
        {
            if(!condition) { Result="FAIL: "+message; throw new InvalidOperationException(Result); }
        }
        static void Sentence(CognitionState s,string text) => s.TryAcquireSentence("runtime."+(++sequence),text);
        public static void Run()
        {
            if(!Application.isPlaying) throw new InvalidOperationException("Fresh P4 Play required");
            Result="Running";
            var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();
            flow.StartCoroutine(Check(flow));
        }
        static IEnumerator Check(OpeningFlow flow)
        {
            bool background=Application.runInBackground; Application.runInBackground=true;
            try
            {
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();
                yield return new WaitForSecondsRealtime(2);
                flow.player.enabled=false; // Deterministic checks; no synthetic devices or InputSystem mutations.
                var s=flow.board.State; var effects=flow.effects; var field=flow.lightField;
                foreach(var word in flow.board.Catalog.Words) s.TryAcquireWord("runtime.learn."+word.Id,word);
                Require(effects && effects.lights.Lamps.Count>10,"effect bindings and pooled lights");
                s.ClearWorkspace(); Sentence(s,"positive light"); Sentence(s,"negative light move");
                Require(flow.lamp.IsOn && effects.lights.Motion==CognitionSignal.Empty && s.Groups.Last().Conflicted,"atomic world effects");
                var group=flow.board.Surface.Find("Group "+s.Groups.Last().Id);
                Require(group && group.GetComponent<CanvasGroup>().alpha<.3f,"conflicted sentence opacity");
                s.ReturnToLibrary(s.Groups.First().Id,0,true);
                yield return new WaitForSeconds(.5f);
                Require(!flow.lamp.IsOn && field.Moving,"composite takeover");
                var lamp=effects.lights.Lamps.First(l=>(l.transform.position-flow.player.transform.position).sqrMagnitude<200);
                flow.board.Panel.SetOpen(true); var before=lamp.transform.position;
                yield return new WaitForSeconds(.4f); Require(Vector3.Distance(before,lamp.transform.position)>.05f,"moving with panel open");
                flow.board.Panel.SetOpen(false); before=lamp.transform.position;
                yield return new WaitForSeconds(.4f); Require(Vector3.Distance(before,lamp.transform.position)>.05f,"moving with panel closed");
                s.ClearWorkspace(); before=lamp.transform.position;
                yield return new WaitForSeconds(.25f); Require(Vector3.Distance(before,lamp.transform.position)<.02f,"dismantling stops light");
                var door=flow.door; if(!door.Revealed) door.Reveal(flow.player.view);
                var appearance=door.GetComponent<DoorStateEffects>();
                Sentence(s,"positive door"); Require(!appearance.PassageEnabled,"airborne on does not open passage");
                s.ClearWorkspace(); Sentence(s,"negative door move");
                var fish=door.GetComponentsInChildren<ThresholdFish>(); var poses=fish.Select(f=>f.transform.localPosition).ToArray();
                var doorBefore=door.transform.position;
                yield return new WaitForSeconds(.5f);
                Require(fish.Select((f,i)=>Vector3.Distance(f.transform.localPosition,poses[i])<.0001f).All(x=>x),"off freezes fish");
                Require(Vector3.Distance(doorBefore,door.transform.position)>.1f,"off door still moves");
                Require(door.GetComponentsInChildren<Light>().All(l=>!l.enabled),"off disables door lights");
                s.ClearWorkspace(); yield return new WaitForSeconds(.3f);
                Require(!appearance.IsOff && !appearance.IsOn && !appearance.PassageEnabled,"default door state restored");
                Require(fish.Select((f,i)=>Vector3.Distance(f.transform.localPosition,poses[i])>.0001f).Any(x=>x),"fish resume without time jump");
                // A nearby, unobstructed actual descent makes this test independent of random lamp locations.
                door.GetComponent<Rigidbody>().position=new Vector3(20,4,12); door.transform.position=new Vector3(20,4,12);
                Sentence(s,"door world move");
                yield return new WaitForSeconds(4);
                Require(door.LandingCompleted,"directed door landing");
                var mist=door.GetComponentInChildren<ThresholdMist>();
                Require(!mist.GetComponentInChildren<MeshRenderer>().enabled,"landed default has no mist");
                Sentence(s,"positive door"); yield return new WaitForSeconds(.5f);
                Require(appearance.PassageEnabled && mist.GetComponentInChildren<MeshRenderer>().enabled,"landed on activates mist and passage");
                s.ClearWorkspace(); Sentence(s,"negative door"); yield return new WaitForSeconds(3);
                Require(!appearance.PassageEnabled && !mist.GetComponentInChildren<MeshRenderer>().enabled,"off shuts mist and passage");
                s.ClearWorkspace();
                lamp=effects.lights.Nearest(flow.player.transform.position);
                lamp.GetComponent<Rigidbody>().position=new Vector3(lamp.transform.position.x,3,lamp.transform.position.z);
                Sentence(s,"light world move"); yield return new WaitForSeconds(2);
                Require(lamp.GetComponent<Collider>().bounds.min.y<.12f && lamp.GetComponent<Collider>().bounds.min.y>=-.02f,"lights descend without penetrating ground");
                s.ClearWorkspace(); Sentence(s,"light door move");
                lamp=effects.lights.Nearest(flow.player.transform.position); before=lamp.transform.position;
                yield return new WaitForSeconds(1);
                Require(Vector3.Distance(before,lamp.transform.position)>.1f,"lights gather toward door");
                s.ClearWorkspace(); Sentence(s,"light light move");
                before=lamp.transform.position; yield return new WaitForSeconds(1);
                Require(Vector3.Distance(before,lamp.transform.position)>.05f,"nearest-neighbour clustering moves");
                s.ClearWorkspace(); Sentence(s,"door light move"); before=door.transform.position;
                yield return new WaitForSeconds(1);
                Require(effects.door.Target && Vector3.Distance(before,door.transform.position)>.05f,"door pursues nearest light");
                s.ClearWorkspace(); field.enabled=false;
                foreach(var l in effects.lights.Lamps) { var p=l.transform.position;p.y+=3;l.GetComponent<Rigidbody>().position=p;l.transform.position=p; }
                Physics.SyncTransforms(); float groundBefore=field.ground.bounds.max.y;
                Sentence(s,"world light move"); yield return new WaitForSeconds(2);
                Require(field.ground.bounds.max.y>groundBefore+.1f,"world rises with player");
                float stopped=field.ground.bounds.max.y; yield return new WaitForSeconds(.5f);
                Require(Mathf.Abs(field.ground.bounds.max.y-stopped)<.05f,"world waits at lowest lamp");
                foreach(var l in effects.lights.Lamps) {var p=l.transform.position+Vector3.up;l.GetComponent<Rigidbody>().position=p;l.transform.position=p;}
                // The door is a real obstruction too: keep it above the raised ground for this recovery check.
                var dp=door.transform.position+Vector3.up*4;door.GetComponent<Rigidbody>().position=dp;door.transform.position=dp;
                Physics.SyncTransforms(); yield return new WaitForSeconds(1);
                Require(field.ground.bounds.max.y>stopped+.1f,"world resumes after target rises");
                s.ClearWorkspace(); stopped=field.ground.bounds.max.y;
                yield return new WaitForSeconds(.3f); Require(Mathf.Abs(field.ground.bounds.max.y-stopped)<.005f,"world stops on dismantle");
                flow.player.enabled=true;
                Result="PASS: atomic effects/UI fade, Tab motion, door states/mist/landing, light descent/gather/cluster, door pursuit, world pause/resume";
            }
            finally { Application.runInBackground=background; }
        }
    }
}
