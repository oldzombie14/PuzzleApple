using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class ObservationChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string text){if(!ok){Result="FAIL: "+text;throw new Exception(Result);}}
        public static void Run(){Result="Running";var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();e.StartCoroutine(Run(e));}
        static IEnumerator Until(Func<bool> predicate)
        {float end=Time.realtimeSinceStartup+20;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Check(predicate(),"timeout");}
        static IEnumerator Run(WorldEntry e)
        {
            var o=e.chamberLight.observer;bool background=Application.runInBackground;Application.runInBackground=true;
            bool shots=e.sentenceRuntime.presentation.enabledShots;
            // Input/ownership checks are independent of the separately tested camera timelines.
            e.sentenceRuntime.presentation.enabledShots=false;
            try
            {
                e.EnterWorld();yield return Until(()=>e.Entered);
                e.board.State.TryAcquireSentence("observe.check.world","positive world");yield return Until(()=>e.room.Opened&&!e.Busy);
                var j=e.journey;var p=e.player;
                e.board.State.TryAcquireWord("observe.check.light",e.board.Catalog.Word("light"));
                p.Teleport(j.Root.TransformPoint(new Vector3(0,.08f,j.pathLength+2)),j.Root.rotation);e.board.Panel.SetOpen(false);yield return null;
                var body=j.Lamps[0].GetComponent<Rigidbody>();var before=body.position;yield return new WaitForSeconds(.2f);
                Check(!j.LightsOn&&Vector3.Distance(before,body.position)>.02f,"unlearned dark lamps keep moving");
                Check(!e.board.State.HasEffect(Cognition.CognitionSignal.LightMove)&&j.movementObservation.HasEffect(e.board.State,e.board.Catalog,Cognition.CognitionSignal.LightMove),"preset sentence drives movement before ownership transfer");
                Check(!j.movementObservation.HasEffect(e.board.State,e.board.Catalog,Cognition.CognitionSignal.LightOff),"preset evaluates its actual rule effects");
                Check(o.FindTarget()==null,"dark behaviour unavailable");
                e.board.State.TryAcquireSentence("observe.check.on","positive light");yield return Until(()=>j.Brightness==1&&!e.sentenceRuntime.presentation.Playing);
                Cursor.lockState=CursorLockMode.Locked;
                p.view.LookAt(j.Root.TransformPoint(new Vector3(0,4,j.pathLength+j.roomSize.z*.5f)));p.AdoptCurrentView();yield return null;
                Check(o.FindTarget()==j.movementObservation,"ray finds whole group");
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                Check(o.ring.gameObject.activeInHierarchy&&o.ring.canvasRenderer&&o.ring.canvasRenderer.GetMesh().vertexCount>0,"ring submits actual UI geometry");
                Check(o.ring.material==e.chamberLight.hoverEye.material&&o.ring.material.GetTexture("_ReticleSceneColor")==p.view.GetComponent<Camera>().targetTexture,"ring shares exact symbol contrast material");
                o.enabled=false;o.Advance(true,.8f);Check(o.Progress>0&&o.Progress<1&&!e.board.State.Knows("move"),"partial hold");
                o.Advance(false,.1f);Check(o.Progress==0,"release resets");
                o.Advance(true,.8f);p.view.rotation=Quaternion.LookRotation(Vector3.down,j.Root.forward);o.Advance(true,.1f);Check(o.Progress==0&&o.Target==null,"look away resets");
                p.view.LookAt(j.Root.TransformPoint(new Vector3(0,4,j.pathLength+j.roomSize.z*.5f)));p.AdoptCurrentView();
                o.Advance(true,.8f);e.board.Panel.SetOpen(true);o.Advance(true,.1f);Check(o.Progress==0,"panel interrupts");e.board.Panel.SetOpen(false);yield return null;
                p.view.LookAt(j.Root.TransformPoint(new Vector3(0,4,j.pathLength+j.roomSize.z*.5f)));p.AdoptCurrentView();
                for(int i=0;i<119;i++)o.Advance(true,1f/60);
                Check(!e.board.State.Knows("move"),"requires full configured duration");o.Advance(true,.03f);
                Check(o.Progress==1&&e.chamberLight.Collecting&&e.chamberLight.collectionSymbol.gameObject.activeSelf,"full bar immediately shows vocabulary collection without release");
                o.Advance(true,1);
                yield return Until(()=>!e.chamberLight.Collecting);
                o.Advance(false,0);yield return null;
                var s=e.board.State;Check(s.Knows("move")&&s.HasEffect(Cognition.CognitionSignal.LightMove)&&e.board.Panel.IsOpen,"learned verb, effective example and automatic panel");
                Check(j.LampsMoving,"learned example sustains movement");
                var group=s.Groups.Single(g=>g.RuleId=="light.move");var detached=s.Detach(group.Id,group.Words[1].Id);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
                Check(!j.LampsMoving&&j.Lamps.All(l=>l.GetComponent<Rigidbody>().isKinematic),"split stops whole group");
                before=body.position;yield return new WaitForSeconds(.15f);Check(Vector3.Distance(before,body.position)<.001f,"stopped positions stable");
                s.Join(detached.Id,group.Id,false);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();Check(j.LampsMoving&&!body.isKinematic,"rejoin resumes");
                e.board.Panel.SetOpen(false);yield return null;p.view.LookAt(j.Root.TransformPoint(new Vector3(0,4,j.pathLength+j.roomSize.z*.5f)));p.AdoptCurrentView();
                int groups=s.Groups.Count;o.Advance(false,0);o.Advance(true,2.1f);
                Check(e.chamberLight.ShowingKnown,"known full bar immediately shows vocabulary feedback");
                Check(s.Groups.Count==groups&&!e.board.Panel.IsOpen,"known observation does not duplicate sentence");
                Check(e.chamberLight.ShowingKnown&&e.chamberLight.KnownFeedbackWord=="move"&&!p.MovementLocked,"known observation displays only the learned verb");
                yield return new WaitForSecondsRealtime(1);
                o.Advance(true,2.1f);Check(!e.chamberLight.ShowingKnown,"held button cannot repeatedly replay known feedback");
                o.Advance(false,0);
                // The same concrete lamp remains left-click collectible while its group is observable.
                o.enabled=true;
                body.position=p.view.position+p.view.forward*3;Physics.SyncTransforms();p.view.LookAt(body.position);p.AdoptCurrentView();
                Check(o.FindTarget()==j.movementObservation&&e.chamberLight.LookingAtLight(),"noun and behaviour coexist");
                e.chamberLight.AdvancePointer(true,true,false,.03f);
                Check(!e.chamberLight.ShowingKnown,"noun does not collect on press");
                e.chamberLight.AdvancePointer(false,false,true,.03f);
                Check(e.chamberLight.ShowingKnown&&e.chamberLight.KnownFeedbackWord=="light","short release collects noun");
                o.Advance(true,.1f);Check(o.Progress==0,"word feedback interrupts observation");
                yield return new WaitForSecondsRealtime(1);
                body.position=p.view.position+p.view.forward*3;Physics.SyncTransforms();
                e.chamberLight.AdvancePointer(true,true,false,.3f);
                e.chamberLight.AdvancePointer(false,false,true,0);
                Check(!e.chamberLight.ShowingKnown&&!e.chamberLight.Collecting,"cancelled long hold never falls through to noun");
                var on=s.Groups.First(g=>g.RuleId=="light.on");s.ReturnToLibrary(on.Id,on.Words[0].Id,true);
                s.TryAcquireSentence("observe.check.off","negative light");yield return Until(()=>j.Brightness==0);Check(j.LampsMoving&&o.FindTarget()==null,"off light hides observation without stopping effective move");
                Result="PASS: dark prelearning motion; group ray; hold/release/look/panel interruption; acquisition and tab; split/rejoin; known observation; noun coexistence; on/off light";
            }
            finally{o.enabled=true;e.sentenceRuntime.presentation.enabledShots=shots;Application.runInBackground=background;}
        }
    }
}
