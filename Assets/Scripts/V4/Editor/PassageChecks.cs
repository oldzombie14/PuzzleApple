using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V4.Editor
{
    public static class PassageChecks
    {
        public static string Result { get; private set; }
        static void Require(bool condition,string message)
        {
            if(!condition) { Result="FAIL: "+message; throw new InvalidOperationException(Result); }
        }
        public static void Run()
        {
            if(!Application.isPlaying) throw new InvalidOperationException("Fresh P4 Play required");
            Result="Running";
            var flow=UnityEngine.Object.FindFirstObjectByType<OpeningFlow>(); UnityEngine.Object.FindFirstObjectByType<SessionSceneManager>().StartCoroutine(Check(flow));
        }
        static IEnumerator Walk(FirstPersonController player,float direction)
        {
            var motor=player.GetComponent<CharacterController>();
            for(int i=0;i<60;i++) {motor.Move(new Vector3(0,0,direction*.07f)); yield return new WaitForFixedUpdate();}
        }
        static IEnumerator Check(OpeningFlow flow)
        {
            var player=flow.player; var board=flow.board;
            bool background=Application.runInBackground; Application.runInBackground=true;
            try
            {
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld(); yield return new WaitForSeconds(2);
                flow.effects.presentation.enabled=false;
                player.enabled=false;
                var d=flow.door; var s=board.State;
                d.Reveal(player.view);
                Require(Vector3.Dot(d.transform.up,Vector3.up)>.9999f,"revealed door remains upright");
                var bearing=Vector3.ProjectOnPlane(d.transform.position-player.view.position,Vector3.up).normalized;
                Require(Vector3.Dot(d.transform.forward,bearing)>.9999f,"revealed door faces camera horizontally");
                var body=d.GetComponent<Rigidbody>();
                var airborne=new Vector3(100,d.groundHeight+.5f,100);
                body.position=airborne;d.transform.position=airborne;
                s.TryAcquireSentence("passage.free","door move");
                d.GetComponent<LightMotion>().RestoreDirection(Vector3.down);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(.5f);
                Require(!d.LandingCompleted&&!GameObject.Find("Door landing ripple")&&d.GetComponent<LightMotion>().Direction.y>0,"free-motion contact bounces without landing");
                body.position=new Vector3(100,d.groundHeight+.25f,100);d.transform.position=body.position;Physics.SyncTransforms();
                s.ClearWorkspace();
                Require(d.LandingCompleted&&Mathf.Abs(body.position.y-d.groundHeight)<.01f,"splitting near ground snaps and commits landing");
                yield return new WaitForSeconds(.1f);
                Require(d.LandingCompleted&&!d.GetComponent<LightMotion>().Moving,"dismantling preserves contact progress");
                yield return new WaitForSeconds(d.rippleDuration+.4f);
                Require(!GameObject.Find("Door landing ripple"),"first ripple finishes");
                var location=new Vector3(100,d.groundHeight,100);
                d.GetComponent<Rigidbody>().position=location; d.transform.SetPositionAndRotation(location,Quaternion.identity);
                d.GetComponent<Rigidbody>().rotation=Quaternion.identity;
                d.SetDescending(true); yield return new WaitForFixedUpdate(); d.SetDescending(false);
                Require(d.LandingCompleted,"test landing");
                Require(!GameObject.Find("Door landing ripple"),"later directed contact does not repeat ripple");
                var origin=location+new Vector3(0,1,-2);
                player.Teleport(origin,Quaternion.identity); Physics.SyncTransforms();
                s.TryAcquireSentence("passage.off","negative door");
                yield return Walk(player,1);
                Require(!d.GetComponent<DoorPassage>().Entered && !SceneManager.GetSceneByName("ChapterI").isLoaded,"closed door must not transition");
                player.Teleport(origin,Quaternion.identity); Physics.SyncTransforms(); yield return new WaitForSeconds(.2f);
                s.ClearWorkspace(); s.TryAcquireSentence("passage.on","positive door");
                yield return new WaitForSeconds(.2f);
                yield return Walk(player,1);
                while(UnityEngine.Object.FindFirstObjectByType<SessionSceneManager>().Busy)yield return null;
                Require(SceneManager.GetSceneByName("ChapterI").isLoaded,"walking through open door loads room");
                Require(!SceneManager.GetSceneByName("P4").isLoaded,"Intro scene unloaded after transition");
                Require(UnityEngine.Object.FindFirstObjectByType<SessionSceneManager>().Current.gameObject.scene.name == "ChapterI" && board.State.Knows("door"),"player/vocabulary preserved");
                Require(s.Groups.Any(g=>g.RuleId=="positive.door"),"sentence workspace retained at transition");
                board.Panel.SetOpen(true); Require(player.PanelOpen,"panel works after transition");
                board.Panel.SetOpen(false); player.enabled=true;
                var before=player.transform.position;
                player.GetComponent<CharacterController>().Move(Vector3.right*.4f);
                Require(Vector3.Distance(before,player.transform.position)>.2f,"controller can move inside room");
                Result="PASS: closed doorway rejects crossing; landed/on crossing loads room, player and vocabulary retained, panel and movement work";
            }
            finally {Application.runInBackground=background;}
        }
    }
}
