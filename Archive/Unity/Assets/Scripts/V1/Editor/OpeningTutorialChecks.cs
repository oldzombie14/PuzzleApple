using System;
using System.Collections;
using System.Linq;
using PuzzleApple.Cognition;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace PuzzleApple.Editor
{
    // Explicit integration check; runs the real gaze, movement, UI and presentation over frames.
    public sealed class OpeningTutorialChecks
    {
        public static string Result { get; private set; }
        static OpeningTutorial lastRunTutorial;
        static Key[] heldKeys = Array.Empty<Key>();
        OpeningTutorial tutorial;
        FirstPersonController player;
        CognitionBoard board;
        int checks;
        public static void Run(bool closeWithTab = false)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in a fresh Play session.");
            var test = new OpeningTutorialChecks();
            test.tutorial = UnityEngine.Object.FindFirstObjectByType<OpeningTutorial>();
            if (lastRunTutorial == test.tutorial) return;
            lastRunTutorial = test.tutorial;
            test.player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            test.board = UnityEngine.Object.FindFirstObjectByType<CognitionBoard>();
            if (test.tutorial.CurrentPhase != OpeningTutorial.Phase.LookingForMirror) throw new InvalidOperationException("Use a fresh opening.");
            Result = "Running"; Application.runInBackground = true;
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            test.board.StartCoroutine(test.HoldTestInput());
            test.tutorial.StartCoroutine(test.Check(closeWithTab));
        }
        static void Keys(params Key[] keys)
        {
            heldKeys = keys;
            if (!Keyboard.current.enabled) InputSystem.EnableDevice(Keyboard.current);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        }
        IEnumerator HoldTestInput()
        {
            // MCP can move focus away from Game View. Keep only this test's input active during the run.
            while (Result == "Running")
            {
                if (!board.Panel.IsOpen) Cursor.lockState = CursorLockMode.Locked;
                Keys(heldKeys);
                yield return null;
            }
            Keys();
        }
        IEnumerator Check(bool closeWithTab)
        {
            yield return null;
            Cursor.lockState = CursorLockMode.Locked;
            Vector3 spawn = player.transform.position;
            Keys(Key.W,Key.Tab);
            yield return new WaitForSeconds(.3f);
            Keys();
            Require(Vector3.ProjectOnPlane(player.transform.position-spawn,Vector3.up).magnitude < .01f,"spawn movement locked");
            Require(!board.Panel.IsOpen,"Tab unavailable before first acquisition");
            player.SetViewPose(180,0);
            yield return new WaitForSeconds(.55f);
            Require(tutorial.GazeElapsed > .3f && tutorial.CurrentPhase == OpeningTutorial.Phase.LookingForMirror,"partial gaze does not trigger");
            player.SetViewPose(270,0); yield return new WaitForSeconds(.1f);
            Require(tutorial.GazeElapsed == 0,"looking away resets continuous gaze");
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = Camera.main.transform.position + Vector3.back*.6f;
            blocker.transform.localScale = Vector3.one*.3f;
            player.SetViewPose(180,0); yield return new WaitForSeconds(.15f);
            Require(!tutorial.IsLookingAtMirror(),"solid occlusion blocks mirror trigger");
            UnityEngine.Object.Destroy(blocker); yield return null;
            yield return WaitFor(OpeningTutorial.Phase.FocusingMirror,3);
            Require(player.MovementLocked && !player.CanInteractWithWorld,"focus owns movement and interaction");
            yield return new WaitForSeconds(.8f);
            Require(Camera.main.fieldOfView < 60,"mirror focus zooms in");
            yield return WaitFor(OpeningTutorial.Phase.ReadingI,4);
            Require(board.Panel.IsOpen && player.MovementLocked,"I automatically opens panel while movement remains locked");
            Require(board.State.Groups.Count == 1 && board.State.Groups[0].Independent && board.State.Groups[0].Words[0].Meaning == "i","I arrives as one independent token");
            yield return new WaitForSeconds(.35f);
            var visibleText=board.Panel.ContentRoot.GetComponentsInChildren<Text>();
            Require(visibleText.Length==1&&visibleText[0].text=="I","first panel contains only I, without headings or test controls");
            Require(board.Panel.ContentRoot.GetComponentsInChildren<Selectable>(true).Length==0,"production panel has no input fields, dropdowns or test buttons");
            var word = board.Surface.GetComponentsInChildren<Text>().First(t=>t.text=="I");
            var vertices=word.cachedTextGenerator.verts;
            Vector2 glyphMin=new Vector2(float.PositiveInfinity,float.PositiveInfinity),glyphMax=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int q=0;q+3<vertices.Count;q+=4)
            {
                if(Mathf.Approximately(vertices[q].position.y,vertices[q+2].position.y))continue;
                for(int j=0;j<4;j++){var v=(Vector2)vertices[q+j].position/word.pixelsPerUnit;glyphMin=Vector2.Min(glyphMin,v);glyphMax=Vector2.Max(glyphMax,v);}
            }
            var wordCenter=word.rectTransform.TransformPoint((glyphMin+glyphMax)*.5f);
            var panelCenter=board.Panel.ContentRoot.TransformPoint(board.Panel.ContentRoot.rect.center);
            Require(Mathf.Abs(wordCenter.x-panelCenter.x)<1&&Mathf.Abs(wordCenter.y-panelCenter.y)<10,"first I glyph is centered in the full left panel with only vertical float");
            ScreenCapture.CaptureScreenshot("Temp/PuzzleChecks/production-first-I.png");
            Vector3 before = word.transform.position;
            yield return new WaitForSeconds(.4f);
            Require((word.transform.position-before).sqrMagnitude > .001f,"independent I floats");
            if (closeWithTab) { Keys(Key.Tab); yield return null; yield return null; Keys(); }
            else
            {
                var button = board.GetComponentsInChildren<Button>().First(b=>b.name=="WorldInputBlocker");
                ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current)
                    {position=new Vector2(Screen.width*.8f,Screen.height*.5f),button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                Require(!player.CanInteractWithWorld,"dismiss click cannot leak into world");
            }
            Require(!board.Panel.IsOpen,"requested dismissal closes automatic panel");
            yield return WaitFor(OpeningTutorial.Phase.LearningToWalk,2);
            Require(Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y,270)) < .1f && Mathf.Abs(player.ViewPitch)<.1f && Mathf.Abs(Camera.main.fieldOfView-75)<.1f,"view returns forward with original FOV");
            Require(!player.MovementLocked && player.LearningProgress==0,"movement unlocks at initial learning speed");
            yield return new WaitForSeconds(.5f);
            Require(tutorial.RecoveryProgress==0,"waiting does not recover movement");
            Keys(Key.W); yield return new WaitForSeconds(.12f);
            Require(Mathf.Abs(player.LearningYaw)<.01f && player.HorizontalVelocity.magnitude>.25f && player.HorizontalVelocity.magnitude<1f,"first short push is tentative and straight");
            float maxSway=0, maxBob=0; float routeZ=player.transform.position.z;
            var walkStart=player.transform.position;float previousSpeed=player.HorizontalVelocity.magnitude;bool hesitated=false;
            for(float t=0;t<2f;t+=Time.deltaTime)
            {
                maxSway=Mathf.Max(maxSway,Mathf.Abs(player.LearningYaw));maxBob=Mathf.Max(maxBob,Mathf.Abs(player.LearningBob));
                float speed=player.HorizontalVelocity.magnitude;if(speed<previousSpeed-.002f)hesitated=true;previousSpeed=speed;
                yield return null;
            }
            float firstStepsDistance=Vector3.Distance(player.transform.position,walkStart);
            Require(firstStepsDistance>.6f&&firstStepsDistance<2f&&hesitated,"first two seconds contain short hesitant steps, not a fast stride");
            Debug.Log("Opening first two seconds distance: "+firstStepsDistance.ToString("F2")+"m");
            Require(maxSway>.1f && maxSway<=2.8f,"tentative sway starts after first step and stays bounded");
            Require(maxBob>.002f && maxBob<=.025f,"learning produces bounded vertical footfalls");
            Require(Mathf.Abs(player.transform.position.z-routeZ)<.01f,"stumble does not steer movement sideways");
            Keys(); yield return new WaitForSeconds(.2f);
            float progress=tutorial.RecoveryProgress;
            Keys(Key.S); yield return new WaitForSeconds(.4f); Keys(); yield return new WaitForSeconds(.1f);
            Require(Mathf.Abs(tutorial.RecoveryProgress-progress)<.001f,"backtracking does not regress learning");
            board.Panel.SetOpen(true); yield return new WaitForSeconds(.4f);
            Require(Mathf.Abs(tutorial.RecoveryProgress-progress)<.001f && player.HorizontalVelocity.magnitude<.01f,"manual Tab pauses movement and recovery");
            board.Panel.SetOpen(false); player.SetViewPose(270,0); Keys(Key.W);
            yield return WaitFor(OpeningTutorial.Phase.DiscoveringMove,15);
            Require(tutorial.RecoveryProgress>=.5f && tutorial.RecoveryProgress<.57f,"move is discovered halfway through the learning route");
            Require(board.State.HasAcquired(OpeningTutorial.MoveAcquisition),"move is available when its overlay starts");
            Require(player.LearningProgress<.6f && !player.MovementLocked && player.CanInteractWithWorld,"midpoint acquisition does not lock movement or jump to full speed");
            Require(!board.Panel.IsOpen && !board.Panel.InputBlocked,"move does not open or block the panel");
            var discoveryPosition=player.transform.position;
            float discoveryProgress=tutorial.RecoveryProgress;
            player.SetViewPose(270,-12);
            yield return new WaitForSeconds(.35f);
            Require(Vector3.Distance(discoveryPosition,player.transform.position)>.35f && tutorial.RecoveryProgress>discoveryProgress,
                "walking and recovery continue during collection overlay");
            Require(Mathf.Abs(player.ViewPitch+12)<.1f,"collection does not steer the player's view down or restore a forced pose");
            ScreenCapture.CaptureScreenshot("Temp/PuzzleChecks/move-while-walking.png");
            Keys(Key.Tab); yield return null; yield return null; Keys();
            Require(board.Panel.IsOpen && board.State.Groups.SelectMany(g=>g.Words).Any(w=>w.Meaning=="move"),
                "Tab can show the acquired move during its overlay");
            yield return new WaitForSeconds(.35f);
            Require(player.HorizontalVelocity.magnitude<.01f,"manually opening the panel still stops walking");
            board.Panel.SetOpen(false); player.SetViewPose(270,0);
            yield return new WaitForSeconds(1.7f);
            Require(tutorial.CurrentPhase==OpeningTutorial.Phase.LearningToWalk && player.LearningProgress<1,
                "finishing collection while stationary does not finish learning");
            Keys(Key.W); yield return WaitFor(OpeningTutorial.Phase.Complete,10);
            Require(!board.Panel.IsOpen && !player.MovementLocked && player.CanInteractWithWorld,"move finishes without auto-opening Tab and releases controls");
            Require(board.State.Groups.SelectMany(g=>g.Words).Count(w=>w.Meaning=="move")==1,"move granted once");
            Require(!board.State.TryAcquireWord(OpeningTutorial.SelfAcquisition,board.Catalog.Word("i")),"repeat source cannot issue another I");
            var i=board.State.Groups.First(g=>g.Words[0].Meaning=="i");
            var move=board.State.Groups.First(g=>g.Words[0].Meaning=="move");
            board.State.Join(move.Id,i.Id,false); board.State.Detach(i.Id,i.Words[1].Id);
            Require(board.State.HasAcquired(OpeningTutorial.SelfAcquisition) && board.State.HasAcquired(OpeningTutorial.MoveAcquisition) && tutorial.CurrentPhase==OpeningTutorial.Phase.Complete,"word edits cannot rewind acquisition facts or tutorial");
            var routeEndPosition=player.transform.position;
            // Without the old camera shot, the final footstep settles through the normal damping.
            Keys(Key.W); yield return new WaitForSeconds(.8f); Keys();
            Require(Mathf.Abs(player.HorizontalVelocity.magnitude-3.5f)<.05f && Mathf.Abs(player.LearningYaw)<.01f && Mathf.Abs(player.LearningBob)<.001f,
                "normal walking speed and steady view restored: speed="+player.HorizontalVelocity.magnitude+", sway="+player.LearningYaw+", bob="+player.LearningBob);
            Require(Vector3.Distance(routeEndPosition,player.transform.position)>2,"walking continues beyond recovery endpoint without the old shot barrier");
            Result="PASS: "+checks+" opening assertions ("+(closeWithTab?"Tab":"right click")+")"; Debug.Log(Result);
        }
        IEnumerator WaitFor(OpeningTutorial.Phase phase,float timeout)
        {
            for(float t=0;t<timeout && tutorial.CurrentPhase!=phase;t+=Time.deltaTime) yield return null;
            Require(tutorial.CurrentPhase==phase,"reaches "+phase+" before timeout");
        }
        void Require(bool condition,string message)
        {
            if(!condition) { Keys(); Result="FAIL: "+message; throw new Exception(Result); }
            checks++;
        }
    }
}


