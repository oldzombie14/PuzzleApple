using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V3.Cognition;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PuzzleApple.V3.Editor
{
    public static class V3Checks
    {
        public static string Status { get; private set; }="Not started";
        public static int Passed { get; private set; }
        static Coroutine activeRun;
        static V3World activeWorld;
        static int generation;
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);Passed++;}
        public static CognitionState.Group Sentence(CognitionState state,CognitionCatalog catalog,string pattern)
        {
            CognitionState.Group group=null;
            foreach(var id in pattern.Split(' ')){var next=state.Spawn(catalog.Word(id));if(next==null)throw new Exception("Word not learned: "+id);if(group==null)group=next;else state.Join(next.Id,group.Id,false);}
            return group;
        }
        static void Clear(CognitionState state){foreach(var g in state.Groups.ToArray())state.ReturnToLibrary(g.Id,0,true);}
        [MenuItem("PuzzleApple/V3/Check cognition and isolation")]
        public static string StateChecks()
        {
            Passed=0;var catalog=AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V3/Cognition/V3Catalog.asset");catalog.ValidateOrThrow();Check(catalog.Words.Count==8,"Eight words");Check(catalog.Rules.Count==15,"Fifteen rules");
            var state=new CognitionState(catalog);Check(state.Spawn(catalog.Word("apple"))==null,"Cannot spawn unlearned words");
            foreach(var w in catalog.Words)Check(state.TryAcquireWord("test-"+w.Id,w),"Acquire "+w.Id);
            Check(!state.TryAcquireWord("test-i",catalog.Word("i")),"Collection is idempotent");Clear(state);
            Check(state.Knows("i")&&state.Groups.Count==0,"Returning every token retains knowledge");
            var nouns=Sentence(state,catalog,"apple apple");Check(!nouns.Recognized,"Two nouns rejected");Clear(state);
            Check(Sentence(state,catalog,"mirror mirror").Effective,"One symbol can supply noun and transform meanings");Clear(state);
            var first=Sentence(state,catalog,"i equal apple");var second=Sentence(state,catalog,"i i");Check(first.Effective&&second.Conflicted,"Earlier equality wins");
            state.Spawn(catalog.Word("door"));Check(first.Effective&&second.Conflicted,"Unrelated edits preserve priority");
            state.ReturnToLibrary(first.Id,0,true);Check(second.Effective,"Removing winner activates remaining statement");
            first=Sentence(state,catalog,"i equal apple");Check(second.Effective&&first.Conflicted,"Rebuilt equality is later");Clear(state);
            first=Sentence(state,catalog,"i i");second=Sentence(state,catalog,"i equal apple");Check(first.Effective&&second.Conflicted,"Essence first wins");Clear(state);
            var move=Sentence(state,catalog,"apple move");Check(state.AppleMoving,"Apple move effective");
            state.Detach(move.Id,move.Words.Last().Id);Check(!state.AppleMoving,"Splitting removes continuous effect");Clear(state);
            first=Sentence(state,catalog,"positive door");second=Sentence(state,catalog,"negative door");Check(first.Conflicted&&second.Conflicted,"Other conflicts use explicit both-inactive policy");Clear(state);
            Check(catalog.Word("i").Senses.Count==2&&catalog.Word("mirror").Senses.Count==2,"Polysemy stored in SO");
            Check(catalog.Rules.Single(r=>r.Effect==CognitionSignal.MirrorApple).Trigger==TriggerMode.Interaction,"Object mirroring requires interaction");
            var bad=AssetDatabase.GetDependencies("Assets/Scenes/V3/P3.unity",true).Where(p=>p.Contains("/V1/")||p.Contains("/V2/")).ToArray();Check(bad.Length==0,"V3 has no V1/V2 asset dependencies: "+string.Join(",",bad));
            return Status="PASS cognition/isolation: "+Passed;
        }
        public static void Begin(string route)
        {
            if(!EditorApplication.isPlaying)throw new Exception("Run in a fresh Play session");
            var world=UnityEngine.Object.FindFirstObjectByType<V3World>();StartCheck(world,Run(world,route),route);
        }
        public static void BeginVariants(string variant)
        {
            if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required");
            var world=UnityEngine.Object.FindFirstObjectByType<V3World>();StartCheck(world,Variants(world,variant),variant);
        }
        static void StartCheck(V3World world,IEnumerator routine,string name)
        {
            if(activeWorld&&activeRun!=null)throw new Exception("A V3 check is already running: "+Status);
            Application.runInBackground=true;Passed=0;Status="Running "+name;
            activeWorld=world;int run=++generation;Debug.Log("V3 check start: "+name+" #"+run);
            activeRun=world.StartCoroutine(Guard(routine,run,name));
        }
        static IEnumerator Variants(V3World w,string variant)
        {
            while(!w.presentation.Ready)yield return null;yield return null;
            foreach(var word in w.board.Catalog.Words)w.Learn(word.Id);Clear(w.State);
            if(variant=="limits")
            {
                var original=w.originalApple.transform.position;
                Sentence(w.State,w.board.Catalog,"mirror apple");
                w.Interact(w.originalApple);int two=w.Apples.Count;Check(two==2,"First axis doubles apple");
                w.Interact(w.originalApple);int four=w.Apples.Count;Check(four==4,"Second axis mirrors current group");
                w.Interact(w.originalApple);int eight=w.Apples.Count;Check(eight<=8&&eight>=four,"Third axis respects collisions and eight limit");
                w.Interact(w.originalApple);Check(w.Apples.Count==eight,"Fourth click cannot create more");
                Clear(w.State);Sentence(w.State,w.board.Catalog,"i apple");w.Interact(w.Apples.Last());Check(w.Apples.Count==1,"Essence on a copy clears its whole copy group");Check(Vector3.Distance(w.originalApple.transform.position,original)<.02f,"Essence preserves original location");Clear(w.State);
                Sentence(w.State,w.board.Catalog,"mirror apple");w.Interact(w.originalApple);Check(w.Apples.Count==2,"Essence resets axis attempts");Clear(w.State);w.Restore(ObjectKind.Apple);
                var essence=Sentence(w.State,w.board.Catalog,"i i");var equality=Sentence(w.State,w.board.Catalog,"i equal apple");yield return new WaitForSeconds(.6f);Check(!w.player.AppleIdentity&&equality.Conflicted,"Essence established first blocks later identity");
                w.State.ReturnToLibrary(essence.Id,0,true);yield return new WaitForSeconds(.7f);Check(w.player.AppleIdentity&&w.player.view.localPosition.y<.6f,"Removing first statement activates equality and low view");
                w.State.ReturnToLibrary(equality.Id,0,true);yield return new WaitForSeconds(.7f);Check(!w.player.AppleIdentity&&w.player.view.localPosition.y>.9f,"Breaking equality restores identity and view");
                w.board.Panel.SetOpen(true);yield return new WaitForSeconds(.4f);w.library.Select("i");yield return null;
                Check(w.library.NoteField && w.library.NoteField.lineType==InputField.LineType.SingleLine,"Compact single-line note field");
                Check(w.library.SnapshotImage.texture==null,"Self acquisition deliberately has a blank memory");
                yield break;
            }
            var move=Sentence(w.State,w.board.Catalog,"apple move");yield return new WaitForSeconds(5);
            if(variant=="landed"){w.State.ReturnToLibrary(move.Id,0,true);yield return new WaitForSeconds(2);}
            Sentence(w.State,w.board.Catalog,"mirror apple");w.Interact(w.originalApple);Check(w.Apples.Count==2,"Mirror works while "+variant);
            if(variant!="landed"){yield return new WaitForSeconds(2);w.State.ReturnToLibrary(move.Id,0,true);}
            float end=Time.time+9;while(!w.Solved&&Time.time<end)yield return null;
            Check(w.Solved,"Alternate operation order solves: "+variant);
        }
        static IEnumerator Guard(IEnumerator inner,int run,string name)
        {
            while(run==generation)
            {
                object current;
                try{if(!inner.MoveNext()){Status="PASS "+name+": "+Passed;activeRun=null;Debug.Log(Status);yield break;}current=inner.Current;}
                catch(Exception e){Status="FAIL "+name+" after "+Passed+": "+e.Message;activeRun=null;Debug.LogError(Status+"\n"+e.StackTrace);yield break;}
                yield return current;
            }
        }
        static IEnumerator Run(V3World w,string route)
        {
            float deadline=Time.time+8;while(!w.presentation.Ready&&Time.time<deadline)yield return null;
            yield return null;Check(w.State.Knows("i"),"Opening grants self");Check(!w.Solved,"Empty balance never solves");
            w.Interact(w.originalMirror);w.Interact(w.originalApple);
            var objects=UnityEngine.Object.FindObjectsByType<V3Object>(FindObjectsSortMode.None);
            foreach(var o in objects.Where(o=>o.kind==ObjectKind.Door||o.kind==ObjectKind.Indicator||o.kind==ObjectKind.Balance))w.Interact(o);
            w.Learn("move");Check(w.board.Catalog.Words.All(word=>w.State.Knows(word.Id)),"All eight words available through acquisitions");
            Clear(w.State);w.board.Panel.SetOpen(true);yield return new WaitForSeconds(.4f);
            var a=w.State.Spawn(w.board.Catalog.Word("apple"));var b=w.State.Spawn(w.board.Catalog.Word("move"));
            w.board.PlaceGroup(a.Id,new Vector2(Screen.width*.08f,Screen.height*.55f));w.board.PlaceGroup(b.Id,new Vector2(Screen.width*.18f,Screen.height*.35f));yield return null;
            var image=w.board.Surface.Find("Group "+a.Id).GetComponentsInChildren<CenteredSymbolImage>().First();var corners=new Vector3[4];image.rectTransform.GetWorldCorners(corners);Vector2 right=new Vector2(corners[2].x+8,(corners[0].y+corners[1].y)*.5f);
            w.board.BeginDrag(b.Id,b.Words[0].Id,false,new Vector2(Screen.width*.18f,Screen.height*.35f));w.board.MoveDrag(right);w.board.EndDrag(right);yield return null;
            Check(w.State.AppleMoving,"Actual board drag joins apple move");
            var group=w.State.Groups.First(g=>g.Signal==CognitionSignal.AppleMove);
            var wordHandle=w.board.Surface.Find("Group "+group.Id).GetComponentsInChildren<CognitionDragHandle>().First(h=>h.WordId==group.Words[1].Id);
            ExecuteEvents.Execute(wordHandle.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right},ExecuteEvents.pointerDownHandler);yield return null;
            Check(!w.State.AppleMoving&&w.State.Knows("move"),"Right click removes token and breaks effect without forgetting word");Clear(w.State);
            w.library.Select("i");yield return null;int before=w.State.Groups.Count;
            var tile=w.library.GetComponentsInChildren<LibraryDrag>(true).First();var rt=tile.GetComponent<RectTransform>();rt.GetWorldCorners(corners);var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=(corners[0]+corners[2])*.5f};e.pressPosition=e.position-Vector2.right*20;
            ExecuteEvents.Execute(tile.gameObject,e,ExecuteEvents.beginDragHandler);e.position=new Vector2(Screen.width*.13f,Screen.height*.4f);ExecuteEvents.Execute(tile.gameObject,e,ExecuteEvents.dragHandler);ExecuteEvents.Execute(tile.gameObject,e,ExecuteEvents.endDragHandler);yield return null;
            Check(w.State.Groups.Count==before+1,"Library drag creates reusable token");Clear(w.State);w.board.Panel.SetOpen(false);
            var split=Sentence(w.State,w.board.Catalog,"mirror i");yield return null;Check(w.presentation.Split,"Self mirror creates viewports");Check(UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None).Length==1,"Split adds no player entity");w.State.ReturnToLibrary(split.Id,0,true);yield return null;Check(!w.presentation.Split,"Splitting mirror sentence restores single view");
            var door=Sentence(w.State,w.board.Catalog,"positive door");yield return new WaitForSeconds(2f);Check(w.GateOpen,"Positive gate held open");w.State.ReturnToLibrary(door.Id,0,true);door=Sentence(w.State,w.board.Catalog,"negative door");yield return new WaitForSeconds(2f);Check(!w.GateOpen,"Negative gate held closed");Clear(w.State);
            var mirror=Sentence(w.State,w.board.Catalog,"mirror mirror");w.Interact(w.originalMirror);Check(w.Mirrors.Count==2,"First mirror appears on opposite corridor wall");Clear(w.State);var origin=w.originalMirror.transform.position;Sentence(w.State,w.board.Catalog,"i mirror");w.Interact(w.Mirrors.Last());Check(w.Mirrors.Count==1&&w.originalMirror.transform.position==origin,"Essence removes copies without resetting original position");Clear(w.State);
            // Undo incidental apple motion from the UI gesture check, only in this disposable Play test.
            w.originalApple.body.isKinematic=true;w.originalApple.transform.position=new Vector3(-6.3f,.04f,-.75f);w.originalApple.slot=-1;yield return null;
            if(route=="mirror")
            {
                Sentence(w.State,w.board.Catalog,"mirror apple");w.Interact(w.originalApple);Check(w.Apples.Count==2,"Mirror apple yields two");Clear(w.State);
                var move=Sentence(w.State,w.board.Catalog,"apple move");yield return new WaitForSeconds(5f);
                Check(w.Apples.Select(o=>o.slot).Distinct().Count()==2,"Two apples assigned different slots");
                w.State.ReturnToLibrary(move.Id,0,true);
            }
            else
            {
                var move=Sentence(w.State,w.board.Catalog,"apple move");yield return new WaitForSeconds(5f);w.State.ReturnToLibrary(move.Id,0,true);yield return new WaitForSeconds(2f);
                var eq=Sentence(w.State,w.board.Catalog,"i equal apple");var essence=Sentence(w.State,w.board.Catalog,"i i");yield return new WaitForSeconds(.7f);
                Check(w.player.AppleIdentity&&w.player.view.localPosition.y<.6f,"First equality wins and lowers view");Check(essence.Conflicted,"Later essence suppressed");w.State.ReturnToLibrary(essence.Id,0,true);
                w.player.Teleport(new Vector3(-6.3f,.1f,.8f));move=Sentence(w.State,w.board.Catalog,"i move");yield return new WaitForSeconds(5f);Check(w.player.Carried,"Self move transports apple-identity player");w.State.ReturnToLibrary(move.Id,0,true);
            }
            deadline=Time.time+10;while(!w.Solved&&Time.time<deadline)yield return null;
            Check(w.Solved,"Balance route completes ("+route+")");yield return new WaitForSeconds(3.5f);Check(w.passageSeal.position.y>4.5f,"Balance completion opens passage");
            Clear(w.State);w.player.Teleport(new Vector3(-21,.1f,0));yield return new WaitForSeconds(.5f);Check(w.Solved&&w.ReachedExit,"Leaving pans preserves passage and reaches exit");
        }
    }
}
