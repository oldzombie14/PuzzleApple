using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4.Editor
{
    public static class StateRetentionChecks
    {
        public static string Result { get; private set; }
        static int sequence;
        static void Require(bool ok,string message)
        { if(!ok){Result="FAIL: "+message;throw new InvalidOperationException(Result);} }
        static void Sentence(CognitionState state,string text)=>state.TryAcquireSentence("retention."+(++sequence),text);
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Fresh P4 Play required");
            Result="Running";
            var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();
            flow.StartCoroutine(Check(flow));
        }
        static IEnumerator Check(OpeningFlow flow)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var hint=flow.effects.inscriptions;string key=hint.progressKey;
            hint.progressKey="PuzzleApple.Tests.Retention."+Guid.NewGuid().ToString("N");
            var lamp=flow.lamp;float duration=lamp.transitionDuration;
            var presentation=flow.effects.presentation;
            try
            {
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();
                yield return new WaitForSecondsRealtime(2);
                var s=flow.board.State;
                foreach(var word in flow.board.Catalog.Words)s.TryAcquireWord("retention.learn."+word.Id,word);
                lamp.transitionDuration=3;
                Sentence(s,"negative light");
                float deadline=Time.realtimeSinceStartup+10;
                while(!presentation.CanInterrupt && Time.realtimeSinceStartup<deadline)yield return null;
                Require(presentation.CanInterrupt,"cinematic allows editing once effect begins");
                yield return new WaitForSecondsRealtime(.3f);
                Require(presentation.InterruptForEditing() && flow.board.Panel.IsOpen && !flow.board.Panel.InputBlocked,"Tab interrupt path reopens editable panel");
                Require(lamp.CurrentState==LightLevelState.InBetween,"actual intermediate brightness is distinct from off");
                s.ClearWorkspace();
                float held=lamp.Brightness, sky=flow.effects.world.environment.NightBlend;
                var color=lamp.surface.material.GetColor("_BaseColor");
                yield return new WaitForSecondsRealtime(.7f);
                Require(Mathf.Abs(lamp.Brightness-held)<.00001f && lamp.CurrentState==LightLevelState.InBetween,"split holds exact intermediate brightness");
                Require(Mathf.Abs(flow.effects.world.environment.NightBlend-sky)<.00001f && lamp.surface.material.GetColor("_BaseColor")==color,"split holds rendered lamp and sky transition");
                Require(flow.lightField.ActiveLamps.All(l=>Mathf.Abs(l.Brightness-held)<.00001f),"pooled lights use exact held source brightness");
                Require(!flow.door.Revealed,"intermediate darkness is not full blackout");
                int played=presentation.PlayCount;
                Sentence(s,"negative light");
                deadline=Time.realtimeSinceStartup+10;
                while(!presentation.CanInterrupt && Time.realtimeSinceStartup<deadline)yield return null;
                Require(presentation.PlayCount==played+1 && presentation.CanInterrupt,"same target from intermediate state produces new feedback and presentation");
                presentation.InterruptForEditing();
                presentation.enabled=false;
                yield return new WaitForSecondsRealtime(.25f);
                Require(lamp.Brightness<held,"reapplied off resumes from held value");
                s.ClearWorkspace();float reverseStart=lamp.Brightness;
                Sentence(s,"positive light");
                yield return new WaitForSecondsRealtime(.25f);
                Require(lamp.Brightness>reverseStart && lamp.CurrentState==LightLevelState.InBetween,"opposite command reverses continuously without snapping");
                Sentence(s,"negative light");
                Require(s.Groups.Last().Conflicted,"earliest opposing command still wins");
                float beforeHandoff=lamp.Brightness;
                var winner=s.Groups.First();s.ReturnToLibrary(winner.Id,winner.Words[0].Id,true);
                Require(Mathf.Abs(lamp.Brightness-beforeHandoff)<.00001f,"conflict takeover does not snap brightness");
                yield return new WaitForSecondsRealtime(.25f);
                Require(lamp.Brightness<beforeHandoff,"suppressed off takes over from current brightness");
                lamp.transitionDuration=.25f;
                yield return new WaitForSecondsRealtime(.4f);
                Require(lamp.IsOff && flow.door.Revealed,"full blackout still reveals door");
                s.ClearWorkspace();yield return new WaitForSecondsRealtime(.3f);
                Require(lamp.IsOff,"completed off persists after split");
                Sentence(s,"positive light");yield return new WaitForSecondsRealtime(.4f);
                s.ClearWorkspace();Require(lamp.IsOn,"completed on persists after split");
                Sentence(s,"light move");yield return new WaitForSecondsRealtime(.3f);
                s.ClearWorkspace();var moving=flow.effects.lights.Lamps.First();var position=moving.transform.position;
                yield return new WaitForSecondsRealtime(.25f);
                Require(Vector3.Distance(position,moving.transform.position)<.02f,"motion stops at current position");
                var door=flow.door;var appearance=door.GetComponent<DoorStateEffects>();
                Sentence(s,"negative door");s.ClearWorkspace();
                var fish=door.GetComponentsInChildren<ThresholdFish>();var poses=fish.Select(f=>f.transform.localPosition).ToArray();
                yield return new WaitForSecondsRealtime(.2f);
                Require(appearance.IsOff && !appearance.PassageEnabled && fish.Select((f,i)=>Vector3.Distance(f.transform.localPosition,poses[i])<.0001f).All(v=>v),"door off and paused fish persist after split");
                var ground=flow.effects.world.ground.bounds.max.y+.02f;
                door.transform.position=new Vector3(30,ground+.12f,20);door.GetComponent<Rigidbody>().position=door.transform.position;Physics.SyncTransforms();
                Sentence(s,"door ground move");yield return new WaitForSecondsRealtime(.6f);s.ClearWorkspace();
                Require(door.LandingCompleted,"landing progress persists");
                var mist=door.GetComponentInChildren<ThresholdMist>();
                Sentence(s,"positive door");yield return new WaitForSecondsRealtime(.25f);s.ClearWorkspace();
                float mistHeld=mist.Visibility;
                yield return new WaitForSecondsRealtime(.35f);
                Require(appearance.IsOn && appearance.PassageEnabled && mistHeld>0 && mistHeld<1 && Mathf.Abs(mist.Visibility-mistHeld)<.00001f,"door on persists and mist holds intermediate visibility");
                Sentence(s,"positive door");yield return new WaitForSecondsRealtime(.25f);
                Require(mist.Visibility>mistHeld,"same door state resumes mist from intermediate visibility");
                s.ClearWorkspace();
                Result="PASS: editable cinematic interruption, exact held brightness/sky/mist, intermediate feedback, continuous reversal, conflict takeover, completed on/off retention, movement stop, door/fish/passage retention";
            }
            finally
            {
                lamp.transitionDuration=duration;presentation.enabled=true;
                hint.Progress.Clear();hint.progressKey=key;Application.runInBackground=background;
            }
        }
    }
}
