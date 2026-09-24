using System;
using System.Collections;
using System.Linq;
using PuzzleApple.Cognition;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PuzzleApple.Editor
{
    public sealed class ApplePuzzleChecks
    {
        public static string Result { get; private set; }
        static CognitionBoard lastRunBoard;
        CognitionBoard board;
        CognitionWorldInteraction world;
        FirstPersonController player;
        int checks;
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            var test = new ApplePuzzleChecks();
            test.board = UnityEngine.Object.FindFirstObjectByType<CognitionBoard>();
            if (lastRunBoard == test.board) return;
            lastRunBoard = test.board;
            test.world = UnityEngine.Object.FindFirstObjectByType<CognitionWorldInteraction>();
            test.player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            Result = "Running"; Application.runInBackground = true;
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            test.board.StartCoroutine(test.Check());
        }
        IEnumerator Check()
        {
            var tutorial = UnityEngine.Object.FindFirstObjectByType<OpeningTutorial>(); tutorial.enabled = false;
            player.SetMovementLocked(false); player.SetLearningProgress(1); player.SetPresentationLocked(false);
            board.Panel.InputBlocked = false; board.Panel.SetOpen(false);
            board.State.TryAcquireWord(OpeningTutorial.SelfAcquisition,board.Catalog.Word("i"));
            board.State.TryAcquireWord(OpeningTutorial.MoveAcquisition,board.Catalog.Word("move"));
            var apple=UnityEngine.Object.FindFirstObjectByType<CognitionApple>();
            var door=UnityEngine.Object.FindFirstObjectByType<CognitionDoor>();
            var key=UnityEngine.Object.FindObjectsByType<CognitionKey>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
            var leaf=door.GetComponentsInChildren<MeshRenderer>().Single(r=>r.GetComponent<MeshFilter>().sharedMesh.name=="立方体.001");
            var doorway=leaf.bounds.center;doorway.y=1.5f;
            var doorApproach=new Vector3(leaf.bounds.max.x+2,.08f,doorway.z);
            Position(doorApproach,doorway);yield return AimAt(door,()=>doorway);
            Require(world.HoveredTarget==door&&door.Hover==CognitionHover.Collect&&!door.TryOpen(),"unlearned door offers collection and cannot open");
            door.Interact(world);door.Interact(world);yield return new WaitForSeconds(2.2f);
            Require(door.Learned&&!door.Opened&&!board.Panel.IsOpen,"door grants vocabulary without opening door or panel, even before apple");
            door.Interact(world);yield return null;
            Require(board.State.Groups.SelectMany(g=>g.Words).Count(w=>w.Meaning=="door")==1,"door vocabulary is awarded once despite repeated interaction");
            Position(new Vector3(-5.7f,.08f,0),apple.Center);
            yield return AimAt(apple,()=>apple.Center);
            Require(world.HoveredTarget==apple && apple.Hover==CognitionHover.Collect,"initial apple eye ray");
            Require(door.Hover==CognitionHover.Forbidden && !key.gameObject.activeSelf,"door locked and key hidden");
            world.Collect(apple);
            yield return new WaitForSeconds(2.2f);
            var sentence=board.State.Groups.Single(g=>g.Words.Count==4);
            Require(!sentence.Recognized && board.Panel.IsOpen,"negative sentence invalid and panel automatically opens");
            Require(apple.Hover==CognitionHover.Forbidden,"collected apple locked");
            // Real pointer handlers: drag no out, then join the remaining I with consume apple.
            var no=sentence.Words[1];
            var h=Handle(sentence,no.Id);var start=Center((RectTransform)h.transform);
            var end=RectTransformUtility.WorldToScreenPoint(null,board.Surface.TransformPoint(new Vector3(240,-450,0)));
            h.OnPointerDown(Event(start));h.OnBeginDrag(Event(start));h.OnDrag(Event(end));
            Require(!board.State.CanConsumeApple,"drag preview does not activate consume");
            h.OnPointerUp(Event(end));yield return new WaitForSeconds(.25f);
            Require(board.State.Owning(no.Id).Independent,"no detached via real drag");
            var rest=board.State.Groups.Single(g=>g.Words.Count==2 && g.Words[0].Meaning=="consume");
            var self=board.State.Groups.First(g=>g.Independent && g.Words[0].Meaning=="i");
            // Move independent I to the beginning of consume apple through the board pointer path.
            h=Handle(self,self.Words[0].Id);start=Center((RectTransform)h.transform);
            var r=Group(rest); end=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(new Vector3(-12,-r.rect.height*.5f,0)));
            h.OnPointerDown(Event(start));h.OnBeginDrag(Event(start));h.OnDrag(Event(end));h.OnPointerUp(Event(end));
            yield return new WaitForSeconds(.25f);
            Require(board.State.CanConsumeApple && apple.Hover==CognitionHover.Question,"drag forms I consume apple and enables interaction");
            var consume=board.State.Groups.Single(g=>g.Signal==CognitionSignal.ConsumeApple);
            var token=board.State.Detach(consume.Id,consume.Words[0].Id);
            Require(!board.State.CanConsumeApple && apple.Hover==CognitionHover.Forbidden,"breaking consume locks apple");
            var tail=board.State.Groups.Single(g=>g.Words.Count==2 && g.Words[0].Meaning=="consume");
            board.State.Join(token.Id,tail.Id,true);
            var move=board.State.Groups.Single(g=>g.Independent && g.Words[0].Meaning=="move");
            consume=board.State.Groups.Single(g=>g.Signal==CognitionSignal.ConsumeApple);
            var extraApple=board.State.Detach(consume.Id,consume.Words[2].Id);
            board.State.Join(move.Id,extraApple.Id,false);
            var before=apple.Center; yield return new WaitForSeconds(.5f);
            Require(Vector3.Distance(before,apple.Center)>.08f,"apple floats in a random direction");
            board.State.Detach(extraApple.Id,extraApple.Words[1].Id);
            before=apple.Center;yield return new WaitForSeconds(.3f);
            Require(apple.GetComponent<Rigidbody>().useGravity && apple.Center.y < before.y,
                "breaking move restores gravity and the apple falls");
            board.State.Join(extraApple.Id,consume.Id,false);
            var blocker=board.GetComponentsInChildren<Button>().Single(b=>b.name=="WorldInputBlocker");
            ExecuteEvents.Execute(blocker.gameObject,Event(new Vector2(Screen.width*.8f,Screen.height*.5f)),ExecuteEvents.pointerClickHandler);
            Require(!board.Panel.IsOpen && !player.CanInteractWithWorld,"right area closes panel without click-through");
            yield return new WaitForSeconds(1.6f);
            var approach = Vector3.ProjectOnPlane(apple.Center - new Vector3(-8, 0, 0), Vector3.up).normalized;
            if (approach.sqrMagnitude < .1f) approach = Vector3.right;
            var nearApple = apple.Center + approach * 1.5f; nearApple.y = .08f;
            Position(nearApple,apple.Center); yield return AimAt(apple,()=>apple.Center);
            Require(world.HoveredTarget==apple && board.transform.Find("Cognition HUD/Interaction question").gameObject.activeSelf,"apple question HUD visible");
            world.Consume(apple);Require(world.IsPresenting && apple.Busy,"consume presentation locks interaction");
            yield return new WaitForSeconds(.95f);
            Require(apple.Eaten && apple.Busy && key.Revealed && key.gameObject.activeInHierarchy,
                "key is already visible inside the apple at the bite, before the toss");
            ScreenCapture.CaptureScreenshot("Temp/PuzzleChecks/bite-key-visible.png");
            bool rises=false, falls=false; float rotationTravel=0;
            var movingApple=key.transform.parent;
            var previousPosition=movingApple.position; var previousRotation=movingApple.rotation;
            for(float elapsed=0;elapsed<2.3f && apple.Busy;elapsed+=Time.deltaTime)
            {
                yield return null;
                if(elapsed>.62f)
                {
                    float dy=movingApple.position.y-previousPosition.y;
                    if(dy>.0005f)rises=true;if(dy<-.002f)falls=true;
                    rotationTravel+=Quaternion.Angle(previousRotation,movingApple.rotation);
                }
                previousPosition=movingApple.position;previousRotation=movingApple.rotation;
            }
            Require(rises && falls && rotationTravel>10,"toss rises, accelerates down and rotates before settling: rise="+rises+", fall="+falls+", rotation="+rotationTravel);
            Require(apple.Eaten && !apple.Busy && key.Revealed,"bite and toss finish with key revealed");
            var keyMesh=key.GetComponentInChildren<MeshFilter>();
            Require(keyMesh && AssetDatabase.GetAssetPath(keyMesh.sharedMesh)=="Assets/ArtAssets-3D/Key.fbx" && !key.GetComponent<MeshRenderer>(),
                "authored key replaces cube placeholder");
            var bitten=key.transform.parent;
            var mesh=bitten.GetComponent<MeshFilter>();
            float lowest=mesh.sharedMesh.vertices.Min(v=>mesh.transform.TransformPoint(v).y);
            var ground=Physics.RaycastAll(bitten.position+Vector3.up,Vector3.down,3,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>!h.collider.transform.IsChildOf(bitten)).OrderBy(h=>h.distance).First();
            Require(lowest>=ground.point.y-.002f && lowest<ground.point.y+.015f,"rotated apple rests on its mesh surface without hovering or sinking");
            Require(bitten && bitten.GetComponent<MeshFilter>() && Vector3.Dot(key.transform.up,bitten.up)<-.5f,
                "key remains attached at an oblique downward insertion angle");
            RaycastHit insertion;
            var insertionRay=new Ray(key.transform.TransformPoint(new Vector3(0,-.2f,0)),key.transform.up);
            Require(bitten.GetComponent<Collider>().Raycast(insertionRay,out insertion,.4f) && insertion.distance>.2f && insertion.distance<.36f,
                "key teeth penetrate flesh while ring and shaft remain exposed");
            Require(!board.State.HasAcquired(CognitionKey.SourceId) && !board.Panel.IsOpen,"bite alone does not grant open or show panel");
            consume=board.State.Groups.Single(g=>g.Signal==CognitionSignal.ConsumeApple);
            board.State.Detach(consume.Id,consume.Words[0].Id);
            Require(apple.Eaten && key.Revealed,"breaking consume preserves eaten apple and key");
            yield return AimAt(key,()=>key.FocusPoint);
            Require(world.HoveredTarget==key && key.Hover==CognitionHover.Question,"key reachable by center ray with question");
            key.Interact(world);yield return new WaitForSeconds(2.2f);
            Require(key.Learned && !board.Panel.IsOpen,"key grants open without opening panel");
            Require(key.gameObject.activeSelf && key.Hover==CognitionHover.Forbidden,"key remains in world and cannot grant twice");
            int count=board.State.Groups.SelectMany(g=>g.Words).Count();key.Interact(world);yield return null;
            Require(count==board.State.Groups.SelectMany(g=>g.Words).Count(),"no duplicate open");
            var open=board.State.Groups.Single(g=>g.Independent&&g.Words[0].Meaning=="open");
            self=board.State.Groups.First(g=>g.Independent&&g.Words[0].Meaning=="i");board.State.Join(open.Id,self.Id,false);
            Require(!self.Recognized&&door.Hover==CognitionHover.Forbidden&&!door.TryOpen(),"I open alone cannot open door");
            var doorWord=board.State.Groups.Single(g=>g.Independent&&g.Words[0].Meaning=="door");
            board.State.Join(doorWord.Id,self.Id,false);
            Require(door.Hover==CognitionHover.Question,"I open door enables door");
            token=board.State.Detach(self.Id,self.Words[2].Id);
            Require(door.Hover==CognitionHover.Forbidden&&!door.TryOpen(),"removing door from I open door locks unopened door");
            board.State.Join(token.Id,self.Id,false);
            Position(doorApproach,doorway);yield return AimAt(door,()=>doorway);
            Require(world.HoveredTarget==door,"door reachable by center ray");
            door.Interact(world);
            Require(door.Opened,"door opens on interaction");
            board.State.Detach(self.Id,self.Words[1].Id);yield return new WaitForSeconds(1.4f);
            Require(door.Opened&&!door.TryOpen(),"opening persists after sentence breaks and cannot repeat");
            var doorHits=Physics.RaycastAll(new Vector3(doorApproach.x,1.5f,doorway.z),Vector3.left,
                doorApproach.x-doorway.x+.3f,~0,QueryTriggerInteraction.Ignore);
            Require(doorHits.All(h=>h.collider.GetComponentInParent<CognitionDoor>()!=door),"open doorway is clear of the door's own colliders");
            Result="PASS: "+checks+" apple puzzle assertions";Debug.Log(Result);
        }
        IEnumerator AimAt(CognitionInteractable target,Func<Vector3> focus)
        {
            // Follow moving targets and recover from tool-induced Editor focus changes.
            for(float elapsed=0;elapsed<1;elapsed+=Time.deltaTime)
            {
                Position(player.transform.position,focus());
                yield return null;
                if(world.HoveredTarget==target && Cursor.lockState==CursorLockMode.Locked) yield break;
            }
        }
        void Position(Vector3 position,Vector3 target)
        {
            var motor=player.GetComponent<CharacterController>();motor.enabled=false;player.transform.position=position;motor.enabled=true;
            var q=Quaternion.LookRotation(target-Camera.main.transform.position);
            player.SetViewPose(q.eulerAngles.y,Mathf.DeltaAngle(0,q.eulerAngles.x));Cursor.lockState=CursorLockMode.Locked;
        }
        RectTransform Group(CognitionState.Group g)=>(RectTransform)board.Surface.Find("Group "+g.Id);
        CognitionDragHandle Handle(CognitionState.Group g,int word)=>Group(g).GetComponentsInChildren<CognitionDragHandle>().Single(h=>h.WordId==word);
        static Vector2 Center(RectTransform r)=>RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
        static PointerEventData Event(Vector2 p)=>new PointerEventData(EventSystem.current){position=p,button=PointerEventData.InputButton.Left};
        void Require(bool value,string message){if(!value){Result="FAIL: "+message;throw new Exception(Result);}checks++;}
    }
}

