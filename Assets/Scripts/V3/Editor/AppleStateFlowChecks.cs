using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PuzzleApple.V3.Cognition;
using Object=UnityEngine.Object;

namespace PuzzleApple.V3.Editor
{
    public static class AppleStateFlowChecks
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(List<string> list,bool pass,string name)=>list.Add((pass?"PASS ":"FAIL ")+name);
        static CognitionState.Group Sentence(OpeningRoom r,string text)
        {r.State.TryAcquireSentence(Guid.NewGuid().ToString(),text);r.ApplySentences();return r.State.Groups.Last();}
        static void Remove(OpeningRoom r,CognitionState.Group g){r.State.ReturnToLibrary(g.Id,0,true);r.ApplySentences();}
        static OpeningRoom Prepare(GameObject root)
        {
            var r=root.GetComponent<OpeningRoom>();
            typeof(CognitionBoard).GetField("<State>k__BackingField",Private).SetValue(r.board,new CognitionState(r.board.Catalog));
            typeof(V3Presentation).GetField("<Ready>k__BackingField",Private).SetValue(r.presentation,true);
            foreach(var o in r.objects)o.Initialize();typeof(FirstPersonController).GetMethod("Awake",Private).Invoke(r.player,null);r.mainRoute.Initialize();return r;
        }
        static void Step(OpeningRoom r,int frames)
        {
            var physics=r.gameObject.scene.GetPhysicsScene();if(physics.Equals(Physics.defaultPhysicsScene))throw new InvalidOperationException("Isolated physics only.");
            for(int i=0;i<frames;i++)
            {r.ApplySentences();typeof(OpeningRoom).GetMethod("FixedUpdate",Private).Invoke(r,null);physics.Simulate(.02f);r.mainRoute.Tick(.02f,i*.02f);r.analyzer.Tick(.02f);}
        }
        public static string Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode only.");
            var result=new List<string>();
            for(int scenario=0;scenario<4;scenario++)
            {
                var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
                try
                {
                    var r=Prepare(root);var route=r.mainRoute;
                    if(scenario==0)
                    {
                        var negative=Sentence(r,"negative apple");Sentence(r,"mirror apple");var move=Sentence(r,"apple move equal");Step(r,1000);
                        Check(result,route.LeftWeight==.25f&&route.RightWeight==.25f,"two mirrored cores carry one quarter weight on each tray");
                        Check(result,!route.Solved&&route.InsufficientAttempts==1,"balanced cores trigger insufficient-weight feedback without solving");
                        Check(result,route.pressurePlate.localPosition.y>.09f&&route.passageSeal.localPosition.y<2,"insufficient weight leaves pressure button and exit closed");
                        Step(r,300);Check(result,route.InsufficientAttempts==1,"unchanged core placement does not replay the close-up");
                        Check(result,route.insufficientWeight.director.duration>2&&route.insufficientWeight.completed.GetPersistentEventCount()==0,"failure shot has its own duration and no success callback");
                        Remove(r,move);Remove(r,negative);Step(r,30);
                        Check(result,!route.Solved&&route.LeftWeight==1&&route.RightWeight==1,"restoring whole apples updates weight and restarts stability interval");
                        Step(r,100);Check(result,route.Solved,"restored whole pair can subsequently complete the puzzle");
                        Sentence(r,"negative apple");Step(r,150);
                        Check(result,route.Solved&&route.passageSeal.localPosition.y>5,"already completed puzzle remains solved after becoming cores");
                        Check(result,route.celebration&&!route.celebration.main.playOnAwake&&!route.celebration.main.loop,"success celebration is an authored one-shot particle system");
                    }
                    else if(scenario==1||scenario==2)
                    {
                        CognitionState.Group identity,negative;
                        if(scenario==1){identity=Sentence(r,"i equal apple");negative=Sentence(r,"negative apple");}
                        else{negative=Sentence(r,"negative apple");identity=Sentence(r,"i equal apple");}
                        var appearance=r.player.gameObject.AddComponent<V3PlayerAppearance>();appearance.Initialize(r.player,route.apple.transform,r.presentation.view);appearance.RefreshAppearance();
                        r.player.Teleport(route.trays[0].position);
                        Check(result,r.player.AppleIdentity&&r.player.AppleCore&&route.Weight(0)==.25f,"player inherits core weight regardless of sentence order "+scenario);
                        var rs=appearance.AppleVisual.GetComponentsInChildren<Renderer>();
                        Check(result,rs.Count(v=>v.enabled)==2,"player appearance renders core and stalk only, order "+scenario);
                        Remove(r,negative);appearance.RefreshAppearance();
                        Check(result,!r.player.AppleCore&&route.Weight(0)==1&&rs.Count(v=>v.enabled)==1,"removing negative restores player appearance and weight, order "+scenario);
                        Sentence(r,"negative apple");Remove(r,identity);appearance.RefreshAppearance();
                        Check(result,!r.player.AppleIdentity&&!r.player.AppleCore&&!appearance.AppleVisual.activeSelf&&route.Weight(0)==0,"identity removal clears player core and apple weight, order "+scenario);
                    }
                    else
                    {
                        var a=r.analyzer;Sentence(r,"apple move analyzer");Step(r,650);
                        Check(result,a.ResultWords().SequenceEqual(new[]{"red","round"}),"whole apple keeps its collectible red and round result");
                        var negative=Sentence(r,"negative apple");
                        Check(result,!a.HasResult&&a.ResultWords().Length==0,"state change immediately invalidates old analyzer result");
                        Step(r,180);
                        Check(result,a.HasResult&&a.Result.requiredAppleForm==AnalysisRecipe.AppleForm.Core&&a.ResultWords().Length==0,"core analysis completes visually without collectible words");
                        Check(result,a.outputSymbols[0].sprite==r.board.Catalog.Word("red").Symbol&&a.outputSymbols[1].sprite==AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/V3/vertical.png"),"core screen displays red plus vertical symbol");
                        Remove(r,negative);Check(result,!a.HasResult,"restoring whole apple restarts analyzer");Step(r,180);
                        Check(result,a.ResultWords().SequenceEqual(new[]{"red","round"}),"whole result becomes collectible again after reanalysis");
                        Check(result,!r.board.Catalog.Words.Any(w=>w.Id=="vertical"),"preview vertical symbol is not inserted into vocabulary");
                    }
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            return string.Join("\n",result);
        }
        public static string PreviewButton()
        {
            var root=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
            try
            {
                var r=Prepare(root);Sentence(r,"negative apple");Sentence(r,"mirror apple");Sentence(r,"apple move equal");Step(r,1000);
                r.mainRoute.galleryLighting.Tick(true,2);
                foreach(var probe in root.GetComponentsInChildren<LocalAmbientProbe>())probe.Apply();
                var camera=r.mainRoute.insufficientWeight.shot;camera.scene=root.scene;
                return Capture(camera,"Temp/InsufficientWeight-button.png");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static string Capture(Camera camera,string path)
        {
            var previous=RenderTexture.active;var target=camera.targetTexture;var rt=RenderTexture.GetTemporary(1000,650,24);Texture2D image=null;
            try
            {
                camera.targetTexture=rt;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;image=new Texture2D(1000,650,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,650),0,0);image.Apply();
                path=System.IO.Path.GetFullPath(path);System.IO.File.WriteAllBytes(path,image.EncodeToPNG());return path;
            }
            finally{camera.targetTexture=target;RenderTexture.active=previous;if(image)Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
