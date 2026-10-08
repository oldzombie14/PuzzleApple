using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PuzzleApple.V3.Cognition;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V3.Editor
{
    public static class OpeningChecks
    {
        public static readonly List<string> Results=new List<string>();
        public static bool Running { get; private set; }
        [MenuItem("PuzzleApple/V3/Checks/Opening cameras (Edit Mode)")]
        public static string Cameras()
        {
            if(Application.isPlaying)throw new InvalidOperationException("This check runs without Play Mode.");
            Results.Clear();var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
            try
            {
                var r=root.GetComponent<OpeningRoom>();var original=r.player.view;
                Vector3 position=original.position;Quaternion rotation=original.rotation;
                Check(root.GetComponentsInChildren<FirstPersonController>(true).Length==1,"only one controlled subject");
                Check(r.player.GetComponentsInChildren<Renderer>(true).Length==0&&!root.transform.Find("Reflected body"),"no original or reflected visible capsule body");
                Check(r.presentation.secondaryView==r.mirrorView.reflectedCamera&&r.presentation.secondaryView!=r.presentation.view,"two separate cameras feed the two views");
                r.mirrorView.SetReflected(true);
                typeof(OpeningMirrorView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(r.mirrorView,null);
                Check(r.presentation.Split,"reflection activates split screen");
                Check(Vector3.Distance(original.position,position)<.0001f&&Quaternion.Angle(original.rotation,rotation)<.001f,"left camera pose remains unchanged");
                Check(Vector3.Distance(r.mirrorView.reflectedCamera.transform.position,r.mirrorView.ReflectSelfPoint(position))<.001f,"right camera uses the dedicated player-view reflection plane");
                var reference=r.doorwayMirror.transform;
                foreach(var offset in new[]{Vector3.zero,new Vector3(-1.2f,0,.6f),new Vector3(1.4f,.2f,-.5f)})
                {
                    var left=position+offset;var aim=Quaternion.LookRotation(reference.position-left,Vector3.up);
                    var right=r.mirrorView.ReflectSelfPoint(left);var forward=r.mirrorView.ReflectSelfRotation(aim)*Vector3.forward;
                    Check(Vector3.Dot(forward,(reference.position-right).normalized)>.999f&&Vector3.Dot(right-reference.position,reference.forward)>0,
                        "both view rays face the same mirror from symmetric front-side positions "+offset);
                }
                Check(Mathf.Abs(Vector3.Dot(r.mirrorView.plane.forward,r.mirrorView.selfPlane.forward))<.001f,"player and opposite-wall object reflections use separate perpendicular planes");
                var fields=BindingFlags.Instance|BindingFlags.NonPublic;
                var first=(UnityEngine.UI.RawImage)typeof(V3Presentation).GetField("first",fields).GetValue(r.presentation);
                var second=(UnityEngine.UI.RawImage)typeof(V3Presentation).GetField("second",fields).GetValue(r.presentation);
                Check(Mathf.Approximately(first.rectTransform.anchorMax.x,.5f)&&Mathf.Approximately(second.rectTransform.anchorMin.x,.5f)&&second.uvRect==new Rect(0,0,1,1),"equal half viewports preserve the symmetric camera image without another flip");
                original.position=position+Vector3.right*1.2f;
                original.rotation=Quaternion.LookRotation(reference.position-original.position,Vector3.up);
                typeof(OpeningMirrorView).GetMethod("LateUpdate",fields).Invoke(r.mirrorView,null);
                var edge=reference.position+reference.right*.4f;
                var leftEdge=r.presentation.view.WorldToViewportPoint(edge).x;
                var rightEdge=r.presentation.secondaryView.WorldToViewportPoint(edge).x;
                var displayedRight=(rightEdge-second.uvRect.x)/second.uvRect.width;
                Check((leftEdge-.5f)*(displayedRight-.5f)>0,"the same physical mirror edge stays on the same screen side in both natural camera views");
                original.SetPositionAndRotation(position,rotation);
                r.mirrorView.SetReflected(false);
                Check(!r.presentation.Split&&!r.mirrorView.reflectedCamera.enabled&&Mathf.Approximately(first.rectTransform.anchorMax.x,1),"dismantling restores one full-width view and disables extra camera");
                var subject=r.doorwayMirror.transform;
                var fixedPoint=r.mirrorView.ReflectSelfPoint(position+Vector3.right);
                var fixedRotation=r.mirrorView.ReflectSelfRotation(rotation);
                subject.position+=new Vector3(1,.3f,-.8f);subject.Rotate(0,25,0);
                Check(Vector3.Distance(fixedPoint,r.mirrorView.ReflectSelfPoint(position+Vector3.right))<.001f&&Quaternion.Angle(fixedRotation,r.mirrorView.ReflectSelfRotation(rotation))<.001f,"moving and rotating the mirror cannot move the player reflection axis");
                Check(r.mirrorView.selfPlane.parent==r.transform,"player reflection plane belongs to the fixed room root");
                foreach(float x in new[]{-3.2f,0,3.2f})foreach(float z in new[]{-3.7f,0,3.7f})
                {
                    var point=r.transform.TransformPoint(new Vector3(x,1.2f,z));
                    var mapped=r.transform.InverseTransformPoint(r.mirrorView.ReflectSelfPoint(point));
                    Check(Vector3.Distance(mapped,new Vector3(-x,1.2f,z))<.001f,"room-edge point stays inside at symmetric X: "+x+", "+z);
                }
                Check(Vector3.Distance(original.position,position)<.0001f&&Quaternion.Angle(original.rotation,rotation)<.001f,"reading vocabulary images never moves the player camera");
                Check(root.GetComponentsInChildren<OpeningSnapshotCamera>(true).Length==0,"playable prefab contains no vocabulary capture cameras");
                Check(new[]{"move","mirror","apple","equal","door","positive","negative","analyzer","red","round"}.All(id=>r.library.vocabularyImages.Count(p=>p.wordId==id&&p.image)==1),"each illustrated word has one saved image");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            return string.Join("\n",Results);
        }
        [MenuItem("PuzzleApple/V3/Checks/Opening collection (Edit Mode)")]
        public static string Collection()
        {
            if(Application.isPlaying)throw new InvalidOperationException("This check runs without Play Mode.");
            Results.Clear();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            for(int branch=0;branch<2;branch++)
            {
                var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
                try
                {
                    var r=root.GetComponent<OpeningRoom>();
                    typeof(CognitionBoard).GetField("<State>k__BackingField",flags).SetValue(r.board,new CognitionState(r.board.Catalog));
                    typeof(OpeningRoom).GetMethod("Start",flags).Invoke(r,null);
                    var complete=(Action<WordDefinition>)typeof(V3Presentation).GetField("WordCollected",flags).GetValue(r.presentation);
                    if(branch==0)
                    {
                        Check(r.Learn("mirror")&&r.player.MovementLocked,"mirror first blocks translation immediately");
                        Check(!r.State.Knows("mirror")&&!r.Learn("move"),"mirror stays pending and blocks move acquisition until collection ends");
                        Check(!r.Learn("mirror"),"pending collection cannot queue duplicates");
                        complete(r.board.Catalog.Word("mirror"));
                        Check(r.State.Knows("mirror")&&!r.player.MovementLocked,"animation completion commits mirror and restores movement");
                        Check(r.Learn("move"),"movement can be learned after mirror collection");
                    }
                    else
                    {
                        typeof(FirstPersonController).GetField("<WalkedDistance>k__BackingField",flags).SetValue(r.player,.02f);
                        typeof(OpeningRoom).GetMethod("UpdateMovementDiscovery",flags).Invoke(r,null);
                        Check(!r.Learn("mirror")&&!r.State.Knows("move"),"first step blocks mirror even before move threshold");
                        typeof(FirstPersonController).GetField("<WalkedDistance>k__BackingField",flags).SetValue(r.player,r.player.learnMoveDistance+.01f);
                        typeof(OpeningRoom).GetMethod("UpdateMovementDiscovery",flags).Invoke(r,null);
                        Check(!r.Learn("mirror")&&!r.State.Knows("move")&&!r.player.MovementLocked,"move announcement blocks mirror while walking remains available");
                        complete(r.board.Catalog.Word("move"));
                        Check(r.State.Knows("move")&&r.Learn("mirror"),"move collection completes before mirror becomes available");
                    }
                    Check(r.library.vocabularyImages.Any(p=>p.wordId=="move"&&p.image)&&r.GetComponentsInChildren<OpeningSnapshotCamera>(true).Length==0,"word collection uses saved images without capture cameras");
                    Check(Mathf.Approximately(r.presentation.selfBlackoutDuration,1)&&Mathf.Approximately(r.presentation.selfFadeInDuration,1.5f)&&Mathf.Approximately(r.presentation.selfHoldDuration,.3f),"opening timing is 1 + 1.5 + 0.3 seconds");
                    Check(r.library.SavedNote("mirror")=="","fresh session has no saved notes");
                    if(branch==0){r.library.Select("mirror");r.library.NoteField.SetTextWithoutNotify("test meaning");r.library.SaveNote();Check(r.library.SavedNote("mirror")=="test meaning","manual note save works within this session");}
                    Check(Vector3.Distance(r.player.view.position,r.doorwayMirror.transform.position)<r.interactionDistance,"mirror is reachable from the unmoved spawn view");
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            return string.Join("\n",Results);
        }
        static void Check(bool condition,string label){Results.Add((condition?"PASS ":"FAIL ")+label);}
        static CognitionState.Group Sentence(CognitionState s,string text)
        {s.TryAcquireSentence(Guid.NewGuid().ToString(),text);return s.Groups.Last();}
        static void Remove(CognitionState s,CognitionState.Group g)=>s.ReturnToLibrary(g.Id,0,true);
        static void Clear(OpeningRoom r){foreach(var g in r.State.Groups.ToArray())Remove(r.State,g);r.ApplySentences();r.ResetObject(r.doorwayMirror);}
        [MenuItem("PuzzleApple/V3/Checks/Opening grammar (Edit Mode)")]
        public static string Grammar()
        {
            Results.Clear();
            var catalog=AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V3/Opening/OpeningCatalog.asset");
            Check(catalog.Validate().Count==0,"catalog references and unique grammar");
            Check(catalog.Rules.All(r=>r.Trigger==TriggerMode.Direct),"all rules activate directly");
            foreach(var rule in catalog.Rules)
            {
                var s=new CognitionState(catalog);var g=Sentence(s,string.Join(" ",rule.Words.Select(w=>w.Id)));
                Check(g.Effective,"recognizes "+g.RuleId);
            }
            var state=new CognitionState(catalog);
            var a=Sentence(state,"mirror mirror");var b=Sentence(state,"i mirror");
            Check(a.Effective&&b.Conflicted,"mirror first wins over reset");Remove(state,a);
            Check(b.Effective,"reset takes over when mirror sentence removed");a=Sentence(state,"mirror mirror");
            Check(b.Effective&&a.Conflicted,"reset first wins over mirror");
            var move=Sentence(state,"mirror move");var go=Sentence(state,"mirror positive move");
            Check(move.Effective&&go.Conflicted,"movement forms first established wins");Remove(state,move);Check(go.Effective,"queued motion takes over");
            var invalid=Sentence(state,"move mirror");Check(!invalid.Recognized&&!invalid.Effective,"invalid word order stays invalid");
            var legacy=AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V3/Cognition/V3Catalog.asset");
            Check(legacy.Rules.Any(r=>r.Trigger==TriggerMode.Interaction)&&legacy.Validate().Count==0,"legacy catalog retained");
            return string.Join("\n",Results);
        }
        [MenuItem("PuzzleApple/V3/Checks/Opening runtime (Play Mode)")]
        public static void Runtime()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Start Play Mode first.");
            if(Running)throw new InvalidOperationException("Checks already running.");
            var room=UnityEngine.Object.FindFirstObjectByType<OpeningRoom>();
            Running=true;Results.Clear();room.StartCoroutine(Run(room));
        }
        static object Motion(OpeningRoom r,string subject)
        {return ((IDictionary)typeof(OpeningRoom).GetField("motion",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(r))[subject];}
        static Vector3 Target(OpeningRoom r,string subject)
        {
            var m=Motion(r,subject);var origin=subject=="i"?r.player.transform.position:r.doorwayMirror.transform.position;
            return (Vector3)typeof(OpeningRoom).GetMethod("Target",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(r,new[]{m,(object)origin,subject});
        }
        static IEnumerator Run(OpeningRoom r)
        {
            while(!r.presentation.Ready)yield return null;
            r.board.Panel.SetOpen(false);r.player.SetPresentationLocked(true);
            yield return null;
            Check(r.State.Knows("i")&&!r.presentation.Split,"self learned, full viewport");
            Check(UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None).Length==1,"only one physical player controller");
            Check(!UnityEngine.Object.FindObjectsByType<V3World>(FindObjectsSortMode.None).Any(),"legacy gameplay disabled");
            Check(r.doorwayMirror.AtHome()&&r.secretSeal.activeSelf,"mirror covers original gate; opposite wall sealed");
            Clear(r);var state=r.State;var mirror=r.doorwayMirror;
            var reflect=Sentence(state,"mirror mirror");r.ApplySentences();
            Check(r.Copies.Count==1&&r.SecretAvailable&&!r.secretSeal.activeSelf,"mirror before movement creates secret passage");
            var copy=r.Copies[mirror];var twice=Sentence(state,"mirror mirror");r.ApplySentences();
            Check(r.Copies.Count==1,"duplicate mirror sentences do not multiply bodies");Remove(state,twice);
            var move=Sentence(state,"mirror move");r.ApplySentences();var home=mirror.transform.position;
            yield return new WaitForSeconds(1.4f);
            Check(Vector3.Distance(home,mirror.transform.position)>.2f,"Brownian motion moves original");
            Check(Vector3.Distance(copy.transform.position,r.mirrorView.ReflectPoint(mirror.transform.position))<.12f,"copy follows reflected motion");
            Remove(state,move);r.ApplySentences();var stopped=mirror.transform.position;
            yield return new WaitForSeconds(.35f);
            Check(Vector3.Distance(stopped,mirror.transform.position)<.025f&&mirror.Body.isKinematic,"dismantled motion stops without falling");
            Remove(state,reflect);r.ApplySentences();
            Check(r.Copies.Count==0&&Vector3.Distance(stopped,mirror.transform.position)<.025f&&!r.SecretAvailable&&r.secretSeal.activeSelf,"dismantled mirror preserves source position and seals secret");
            move=Sentence(state,"mirror move");r.ApplySentences();yield return new WaitForSeconds(.8f);
            Remove(state,move);r.ApplySentences();reflect=Sentence(state,"mirror mirror");r.ApplySentences();
            Check(r.Copies.Count==1&&!r.SecretAvailable&&r.secretSeal.activeSelf,"moving first then mirroring never creates secret gate");
            var reset=Sentence(state,"i mirror");r.ApplySentences();
            Check(reset.Conflicted&&r.Copies.Count==1,"late reset cannot override mirror");
            Remove(state,reflect);r.ApplySentences();Check(mirror.AtHome()&&r.Copies.Count==0,"reset takeover restores full original");
            Remove(state,reset);reflect=Sentence(state,"mirror mirror");r.ApplySentences();
            Check(r.SecretAvailable,"reset then mirror can create secret passage again");
            r.player.Teleport(new Vector3(0,.05f,-8));Remove(state,reflect);r.ApplySentences();
            Check(r.player.transform.position.z>-4,"closing secret passage cannot strand player");
            Clear(r);r.player.ResetPose(r.playerSpawn.position,r.playerSpawn.rotation);
            reflect=Sentence(state,"mirror i");r.ApplySentences();yield return new WaitForEndOfFrame();
            Check(r.mirrorView.Reflected&&r.presentation.Split,"self reflection splits original and reflected views");
            var expected=r.mirrorView.ReflectSelfPoint(r.player.transform.position+Vector3.up*1.15f);
            Check(Vector3.Distance(r.mirrorView.reflectedCamera.transform.position,expected)<.08f,"secondary camera is spatially reflected around preset plane");
            Check(r.player.GetComponentsInChildren<Renderer>(true).Length==0,"player has no visible body mesh");
            reset=Sentence(state,"i i");r.ApplySentences();Check(reset.Conflicted&&r.mirrorView.Reflected,"self reset/mirror are mutually exclusive");
            Remove(state,reflect);r.ApplySentences();yield return null;
            Check(!r.mirrorView.Reflected&&Vector3.Distance(r.player.transform.position,r.playerSpawn.position)<.05f,"self reset restores view and position");
            Clear(r);r.player.ResetPose(r.playerSpawn.position,r.playerSpawn.rotation);
            move=Sentence(state,"mirror positive move");r.ApplySentences();var target=Target(r,"mirror");
            r.player.Teleport(new Vector3(2,.04f,0));r.player.transform.rotation=Quaternion.Euler(0,90,0);r.ApplySentences();
            Check(Vector3.Distance(Target(r,"mirror"),target)<.001f,"away endpoint is frozen at activation");
            Clear(r);move=Sentence(state,"mirror negative move");r.ApplySentences();target=Target(r,"mirror");
            r.player.Teleport(new Vector3(-1,.04f,0));r.player.transform.rotation=Quaternion.identity;r.ApplySentences();
            Check(Vector3.Distance(Target(r,"mirror"),target)>1&&Target(r,"mirror").z>r.player.transform.position.z,"come target tracks the player's front");
            Clear(r);move=Sentence(state,"i move mirror");r.ApplySentences();
            Check(r.player.SentenceTravel&&r.player.SentenceTarget.z>2,"explicit object destination routes the player");
            Clear(r);move=Sentence(state,"i move");r.ApplySentences();Check(state.PlayerMoving&&!r.player.SentenceTravel,"self move retains forward locomotion");
            Clear(r);r.player.ResetPose(r.playerSpawn.position,r.playerSpawn.rotation);r.player.SetPresentationLocked(false);
            Running=false;Debug.Log("Opening checks: "+Results.Count(s=>s.StartsWith("PASS"))+" passed, "+Results.Count(s=>s.StartsWith("FAIL"))+" failed.");
        }
    }
}
