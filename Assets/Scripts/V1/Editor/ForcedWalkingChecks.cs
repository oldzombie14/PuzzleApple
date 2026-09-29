using System;
using System.Collections;
using System.Linq;
using PuzzleApple.Cognition;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PuzzleApple.Editor
{
    public sealed class ForcedWalkingChecks
    {
        public static string Result { get; private set; }
        static CognitionBoard lastRunBoard;
        static Key[] heldKeys = Array.Empty<Key>();
        CognitionBoard board;
        FirstPersonController player;
        int checks;
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Use a fresh Play session.");
            var test = new ForcedWalkingChecks {
                board = UnityEngine.Object.FindFirstObjectByType<CognitionBoard>(),
                player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>() };
            if (lastRunBoard == test.board) return;
            lastRunBoard = test.board;
            Application.runInBackground = true;
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            Result = "Running"; test.board.StartCoroutine(test.HoldTestInput()); test.board.StartCoroutine(test.Check());
        }
        static void Keys(params Key[] keys)
        {
            heldKeys = keys;
            // Editor tool focus changes can disable the keyboard; re-enable the fixture's input device.
            if (!Keyboard.current.enabled) InputSystem.EnableDevice(Keyboard.current);
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));
        }
        IEnumerator HoldTestInput()
        {
            while (Result == "Running")
            {
                if (!board.Panel.IsOpen) Cursor.lockState = CursorLockMode.Locked;
                Keys(heldKeys); yield return null;
            }
            Keys();
        }
        IEnumerator Check()
        {
            UnityEngine.Object.FindFirstObjectByType<OpeningTutorial>().enabled = false;
            player.SetLearningProgress(1); player.SetMovementLocked(false); player.SetPresentationLocked(false);
            board.Panel.InputBlocked = false; board.Panel.SetOpen(false); Keys();
            Position(new Vector3(-4,.08f,3),270);
            board.State.TryAcquireWord("forced/self",board.Catalog.Word("i"));
            board.State.TryAcquireWord("forced/move",board.Catalog.Word("move"));
            var self=board.State.Groups.Single(g=>g.Words[0].Meaning=="i");
            var move=board.State.Groups.Single(g=>g.Words[0].Meaning=="move");
            yield return new WaitForSeconds(.2f);
            Require(!player.ForcedWalking && player.HorizontalVelocity.sqrMagnitude<.001f,"loose words do not move player");
            board.Panel.SetOpen(true);
            board.State.Join(move.Id,self.Id,false);
            var start=player.transform.position;
            yield return new WaitForSeconds(.4f);
            Require(self.Recognized && player.ForcedWalking && player.transform.position.x<start.x-1,"forming I move inside Tab starts continuous travel");
            float routeZ=player.transform.position.z;
            Keys(Key.S,Key.D);
            yield return new WaitForSeconds(.4f); Keys();
            Require(Mathf.Abs(player.transform.position.z-routeZ)<.01f && player.HorizontalVelocity.x<-3.4f,
                "panel idle rotation and WASD cannot steer forced travel");
            board.Panel.SetOpen(false);player.SetViewPose(0,0);
            start=player.transform.position;
            yield return new WaitForSeconds(.25f);
            Require(player.transform.position.z>start.z+.65f && Mathf.Abs(player.transform.position.x-start.x)<.01f,
                "closed panel allows view heading to steer automatic walking");
            Keys(Key.Tab);yield return new WaitForSeconds(.1f);Keys();
            Require(board.Panel.IsOpen,"real Tab opens while forced walking");
            start=player.transform.position;
            yield return new WaitForSeconds(.3f);
            Require(player.transform.position.z>start.z+.85f && Mathf.Abs(player.transform.position.x-start.x)<.01f,
                "opening Tab retains last world direction and full speed");
            board.State.TryAcquireSentence("forced/second","I move");
            var second=board.State.Groups.Single(g=>g.Id!=self.Id && g.Signal==CognitionSignal.PlayerMove);
            var detached=board.State.Detach(self.Id,self.Words[1].Id);
            yield return new WaitForSeconds(.15f);
            Require(player.ForcedWalking && player.HorizontalVelocity.magnitude>3.4f,"second valid sentence keeps walking active");
            board.State.Detach(second.Id,second.Words[1].Id);
            yield return new WaitForSeconds(.1f);
            start=player.transform.position;yield return new WaitForSeconds(.2f);
            Require(!player.ForcedWalking && Vector3.Distance(start,player.transform.position)<.01f,
                "breaking last sentence stops movement inside Tab");
            board.Panel.SetOpen(false);Keys(Key.D);start=player.transform.position;
            yield return new WaitForSeconds(.2f);Keys();
            Require(player.transform.position.x>start.x+.5f,"ordinary WASD control returns after splitting");
            Position(new Vector3(-10,.08f,3),270);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Forced walking test wall";
            wall.transform.position=new Vector3(-11,1,3);wall.transform.localScale=new Vector3(.1f,3,3);
            try
            {
                Physics.SyncTransforms();board.State.Join(detached.Id,self.Id,false);
                yield return new WaitForSeconds(.65f);
                Require(player.ForcedWalking && player.transform.position.x>-10.76f,"continuous walking respects solid collision");
                board.Panel.SetOpen(true);start=player.transform.position;yield return new WaitForSeconds(.3f);
                Require(Vector3.Distance(start,player.transform.position)<.03f,"Tab cannot bypass a wall while walking");
                board.State.Detach(self.Id,self.Words[1].Id);board.Panel.SetOpen(false);
            }
            finally { UnityEngine.Object.Destroy(wall);Keys(); }
            Position(new Vector3(-4,.08f,3),0);
            board.State.TryAcquireSentence("blocked/first","I no move");
            var blocked=board.State.Groups.Last();
            start=player.transform.position;Keys(Key.W,Key.D);
            yield return new WaitForSeconds(.25f);Keys();
            Require(blocked.Recognized && player.CognitionMovementBlocked &&
                Vector3.ProjectOnPlane(player.transform.position-start,Vector3.up).magnitude<.01f,
                "I no move blocks ordinary WASD");
            board.State.TryAcquireSentence("blocked/walking","I move");var positive=board.State.Groups.Last();
            yield return new WaitForSeconds(.15f);
            Require(!player.ForcedWalking && !player.CognitionMovementBlocked && player.HorizontalVelocity.sqrMagnitude<.001f,"conflict disables both effects");
            start=player.transform.position;Keys(Key.D);yield return new WaitForSeconds(.2f);Keys();
            Require(player.transform.position.x>start.x+.5f,"conflict restores ordinary WASD");
            Keys(Key.Tab);yield return new WaitForSeconds(.1f);Keys();
            Require(board.Panel.IsOpen,"Tab remains available during conflict");
            Require(board.Surface.Find("Group "+positive.Id).GetComponent<CanvasGroup>().alpha<.5f &&
                board.Surface.Find("Group "+blocked.Id).GetComponent<CanvasGroup>().alpha<.5f,
                "both conflicting sentences reduce opacity");
            board.State.TryAcquireSentence("blocked/second","I no move");var otherBlock=board.State.Groups.Last();
            board.State.Detach(blocked.Id,blocked.Words[1].Id);
            yield return new WaitForSeconds(.15f);
            Require(!player.CognitionMovementBlocked && !player.ForcedWalking && player.HorizontalVelocity.sqrMagnitude<.001f,"remaining opposing sentence sustains conflict");
            board.State.Detach(otherBlock.Id,otherBlock.Words[1].Id);
            start=player.transform.position;yield return new WaitForSeconds(.25f);
            Require(player.ForcedWalking && player.transform.position.x>start.x+.65f,"resolving conflict resumes I move along Tab entry direction");
            Require(Mathf.Approximately(board.Surface.Find("Group "+positive.Id).GetComponent<CanvasGroup>().alpha,1),
                "resolving conflict restores original opacity");
            board.State.TryAcquireSentence("blocked/third","I no move");var thirdBlock=board.State.Groups.Last();
            yield return null;yield return null;start=player.transform.position;
            yield return new WaitForSeconds(.15f);
            Require(player.HorizontalVelocity.sqrMagnitude<.001f && Vector3.Distance(start,player.transform.position)<.01f,
                "forming prohibition stops forced Tab travel without drift");
            board.State.Detach(positive.Id,positive.Words[1].Id);
            yield return null;
            Require(player.CognitionMovementBlocked&&!thirdBlock.Conflicted,"removing positive restores remaining prohibition");
            board.State.Detach(thirdBlock.Id,thirdBlock.Words[1].Id);
            yield return new WaitForSeconds(.15f);
            Require(player.HorizontalVelocity.sqrMagnitude<.001f,"unblocking does not revive stale panel inertia");
            board.Panel.SetOpen(false);player.SetViewPose(0,0);Keys(Key.D);start=player.transform.position;
            yield return new WaitForSeconds(.2f);Keys();
            Require(player.transform.position.x>start.x+.5f,"unblocking restores ordinary WASD");
            board.State.TryAcquireSentence("blocked/gravity","I no move");
            Position(new Vector3(-4,2,3),0);start=player.transform.position;
            yield return new WaitForSeconds(.3f);
            Require(player.transform.position.y<start.y-.3f && player.HorizontalVelocity.sqrMagnitude<.001f,"prohibition preserves gravity");
            Result="PASS: "+checks+" forced walking assertions";Debug.Log(Result);
        }
        void Position(Vector3 position,float yaw)
        {
            var motor=player.GetComponent<CharacterController>();motor.enabled=false;player.transform.position=position;motor.enabled=true;
            player.SetViewPose(yaw,0);Cursor.lockState=CursorLockMode.Locked;
        }
        void Require(bool value,string message)
        { if(!value){Keys();Result="FAIL: "+message;throw new Exception(Result);}checks++; }
    }
}
