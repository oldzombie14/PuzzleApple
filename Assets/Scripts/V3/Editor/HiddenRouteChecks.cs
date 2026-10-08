using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PuzzleApple.V3.Cognition;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V3.Editor
{
    public static class HiddenRouteChecks
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Invoke(object o,string method)=>o.GetType().GetMethod(method,Private).Invoke(o,null);
        static void Check(List<string> list,bool value,string label)=>list.Add((value?"PASS ":"FAIL ")+label);
        static CognitionState.Group Sentence(OpeningRoom r,string text)
        {r.State.TryAcquireSentence(Guid.NewGuid().ToString(),text);return r.State.Groups.Last();}
        static void Place(OpeningObject o,Vector3 p){o.Stop();o.transform.position=p;o.Body.position=p;Physics.SyncTransforms();}
        static void Step(OpeningRoom r,int frames)
        {
            var physics=r.gameObject.scene.GetPhysicsScene();
            if(physics.Equals(Physics.defaultPhysicsScene))throw new InvalidOperationException("Isolated physics required.");
            for(int i=0;i<frames;i++){r.ApplySentences();Invoke(r,"FixedUpdate");physics.Simulate(.02f);r.analyzer.Tick(.02f);}
        }
        [MenuItem("PuzzleApple/V3/Checks/Hidden route (Edit Mode)")]
        public static string Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only.");
            var result=new List<string>();var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
            try
            {
                var r=root.GetComponent<OpeningRoom>();var a=r.analyzer;
                typeof(CognitionBoard).GetField("<State>k__BackingField",Private).SetValue(r.board,new CognitionState(r.board.Catalog));
                typeof(V3Presentation).GetField("<Ready>k__BackingField",Private).SetValue(r.presentation,true);
                foreach(var obj in r.objects)obj.Initialize();Invoke(r.player,"Awake");r.mainRoute.Initialize();
                var apples=r.objects.Where(o=>o.wordId=="apple").ToArray();var near=apples.Single(o=>o!=r.mainRoute.apple);var far=r.mainRoute.apple;
                Check(result,apples.Length==2&&a.intake&&a.recipes.Length==2,"two physical apples, native intake and state-specific recipe references");
                var legacy=AssetDatabase.LoadAssetAtPath<WordDefinition>("Assets/GameData/V3/Cognition/Words/apple.asset");
                Check(result,legacy.Symbol==r.board.Catalog.Word("analyzer").Symbol&&legacy!=r.board.Catalog.Word("apple")&&legacy.Symbol!=r.board.Catalog.Word("apple").Symbol,"symbol reassignment is isolated from legacy catalog");
                var move=Sentence(r,"apple move analyzer");Step(r,1);
                Check(result,apples.All(o=>r.MovesToAnalyzer(o))&&near.Body.linearVelocity.sqrMagnitude>.001f&&far.Body.isKinematic,"all named apples qualify but only reservation holder moves");
                Check(result,!a.Occupant&&a.Reserved==near,"one place is assigned when sentence forms, before arrival");
                Step(r,650);
                Check(result,a.Occupant==near&&Vector3.Distance(near.transform.position,a.LandingPoint(near))<.08f,"first arrival occupies center of tabletop");
                Check(result,a.HasResult&&a.ResultWords().SequenceEqual(new[]{"red","round"}),"settled apple produces red and round recipe");
                Check(result,near.gameObject.activeSelf&&apples.Length==r.objects.Count(o=>o.wordId=="apple"),"analysis preserves apple");
                Check(result,far.Body.isKinematic&&Vector3.Distance(far.transform.position,far.Home)<.001f,"unselected hall apple never lifts or drifts");
                foreach(var id in a.ResultWords())
                {
                    bool first=r.Learn(id),duplicate=r.Learn(id);
                    typeof(OpeningRoom).GetMethod("CompleteCollection",Private).Invoke(r,new object[]{r.board.Catalog.Word(id)});
                    Check(result,first&&!duplicate&&r.State.Knows(id)&&!r.Learn(id),id+" learned once, including repeat click during animation");
                }
                r.State.ReturnToLibrary(move.Id,0,true);Step(r,3);
                Check(result,a.Occupant==near&&a.HasResult,"removing sentence retains apple, occupancy and analysis result");
                Place(near,near.Home);a.RefreshOccupancy();Check(result,!a.Occupant,"physically removing apple releases place");
                Place(far,a.LandingPoint(far)+Vector3.right*.12f);Place(near,a.LandingPoint(near)+Vector3.left*.4f);
                move=Sentence(r,"apple move analyzer");r.ApplySentences();
                a.TryTarget(near,out var ignored);
                Check(result,a.Reserved==far&&!a.TryTarget(near,out ignored),"sentence-time allocation can select hall apple and holds its reservation");
                Place(far,far.Home);a.RefreshOccupancy();
                Check(result,a.Reserved==far&&!a.TryTarget(near,out ignored),"no mid-sentence reassignment when candidate distances change");
                r.State.ReturnToLibrary(move.Id,0,true);r.ApplySentences();
                Check(result,!a.Reserved,"removing sentence releases unfulfilled reservation");
                move=Sentence(r,"apple move analyzer");r.ApplySentences();
                Check(result,a.Reserved==near,"new sentence recalculates the place");

                var target=r.transformTargets.Single(t=>t.wordId=="equal");var originals=target.surfaces.Select(s=>s.sharedMaterials).ToArray();
                var red=Sentence(r,"red equal");r.ApplySentences();
                Check(result,target.IsRed&&r.Outcome(red)==SentenceOutcome.Active,"red transforms balance");
                var round=Sentence(r,"round equal");r.ApplySentences();
                Check(result,round.Recognized&&r.Outcome(round)==SentenceOutcome.Unsupported&&target.IsRed,"round balance is recognized but unsupported, without altering color");
                var reset=Sentence(r,"i equal");r.ApplySentences();
                Check(result,!target.IsRed&&r.Outcome(red)==SentenceOutcome.Resetting,"originalization restores and holds original color");
                r.State.ReturnToLibrary(reset.Id,0,true);r.ApplySentences();
                Check(result,target.IsRed,"remaining continuous red sentence resumes after reset sentence removed");
                r.State.ReturnToLibrary(red.Id,0,true);r.ApplySentences();
                Check(result,!target.IsRed&&target.surfaces.Select((s,i)=>s.sharedMaterials.SequenceEqual(originals[i])).All(x=>x),"dismantling red restores every original material");
                var invalid=Sentence(r,"equal round");Check(result,r.Outcome(invalid)==SentenceOutcome.InvalidGrammar,"invalid grammar distinguished from unsupported effect");
                var mirrorTarget=r.transformTargets.Single(t=>t.wordId=="mirror");
                var glass=r.doorwayMirror.GetComponentsInChildren<Renderer>().Single(s=>s.sharedMaterial.shader.name=="PuzzleApple/Planar Mirror");var glassMaterial=glass.sharedMaterial;
                var mirrorRed=Sentence(r,"red mirror");r.ApplySentences();
                Check(result,mirrorTarget.IsRed&&glass.sharedMaterial==glassMaterial,"red mirror colors frame without replacing reflective glass");
                Sentence(r,"mirror mirror");r.ApplySentences();
                var copy=r.Copies[r.doorwayMirror].GetComponent<WordTransformTarget>();
                Check(result,copy.IsRed,"mirrored frame inherits active red transformation");
                r.State.ReturnToLibrary(mirrorRed.Id,0,true);r.ApplySentences();
                Check(result,!copy.IsRed&&!mirrorTarget.IsRed&&copy.surfaces[0].sharedMaterial==mirrorTarget.surfaces[0].sharedMaterial,"removing red restores both original and copy frames");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            return string.Join("\n",result);
        }
    }
}
