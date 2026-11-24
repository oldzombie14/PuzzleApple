using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class VitruvianChecks
    {
        public static string Result {get;private set;}
        static void Check(bool ok,string why){if(!ok){Result="FAIL: "+why;throw new InvalidOperationException(Result);}}
        static IEnumerator Until(Func<bool> test,string label){
            float end=Time.realtimeSinceStartup+35;while(!test() && Time.realtimeSinceStartup<end)yield return null;
            Check(test(),label);
        }
        internal static IEnumerator Gallery(WorldEntry e){
            e.OpenWorld(-1);yield return Until(()=>e.room.Opened && !e.Busy,"open Intro box");
            e.sentenceRuntime.presentation.enabledShots=false;e.board.Panel.SetOpen(false);
            yield return Until(()=>e.outdoor.Prepared,"outdoors prepared");
            yield return WhiteAscentChecks.Leave(e);
            yield return ChapterChecks.Cross(e,e.outdoor.World.chapterFrame);
            yield return ChapterLessonChecks.Traverse(e);
            yield return Until(()=>e.outdoor.InChapter && !e.outdoor.Transitioning,"enter gallery via frame");
        }
        internal static CognitionState.Group Say(CognitionBoard board,params string[] words){
            var g=PeriodChecks.Draft(board,words);
            if(board.State.RequirePeriod)board.State.Speak(g.Id);
            return g;
        }
        static void Pause(CognitionBoard board,CognitionState.Group g){
            if(board.State.RequirePeriod){
                board.Panel.SetOpen(true);
                PeriodChecks.Drag(board,g.Id,Vector2.zero);
            }else board.State.Detach(g.Id,g.Words.Last().Id);
        }
        public static void Run(){var e=UnityEngine.Object.FindFirstObjectByType<WorldEntry>();Result="Running";e.StartCoroutine(Exercise(e));}
        static IEnumerator Exercise(WorldEntry e){
            yield return Gallery(e);
            var pass=e.outdoor;var board=e.board;var state=board.State;var p=e.player;
            var chapter=pass.Chapter;var room=pass.Vitruvian;var puzzle=room.puzzle;
            bool squareEvent=false,circleEvent=false;
            puzzle.SquareChanged.AddListener(value=>squareEvent=value);puzzle.CircleChanged.AddListener(value=>circleEvent=value);
            var boxCopy=Say(board,"mirror","mirror");
            yield return Until(()=>chapter.mirrors.CopiesActive,"folded mirror copies");
            var glass=chapter.mirrors.Copy(0);
            Check(Mathf.Abs(glass.position.x-chapter.arrival.position.x)<3.3f && !room.mirrors.CopiesActive,"folded copy stays on opposite box wall");
            Say(board,"positive","world");
            yield return Until(()=>chapter.box.PanelCoverage<.8f && !chapter.box.Opened,"box immersion begins");
            var foldFace=glass.GetComponentInChildren<MeshRenderer>(true);
            Check(foldFace.sharedMaterial.shader.name=="PuzzleApple/V5/Folding Mirror" &&
                Mathf.Abs(foldFace.sharedMaterial.GetFloat("_PanelOpacity")-chapter.box.PanelCoverage)<.06f,
                "mirror uses the folding panel's live cut during immersion");
            yield return Until(()=>chapter.box.Opened && !e.Busy && state.Find(boxCopy.Id)==null,"unfold retires mirror sentence");
            board.Panel.SetOpen(false);
            Capture(p,chapter.arrival.position+Vector3.up*1.55f,chapter.roomFill.transform.position+Vector3.left*24+Vector3.down*7,"gallery");
            Capture(p,chapter.arrival.position+new Vector3(-42,4.8f,.6f),chapter.transform.position+new Vector3(-15,5.45f,0),"balance");
            var gate=room.transform;var turn=Quaternion.LookRotation(gate.forward);
            p.Teleport(gate.position-gate.forward*4.2f-Vector3.up*1.49f,turn);yield return null;
            Check(e.journey.SentenceScope()==PassageJourney.LightSpace.Gallery,"gallery looking through portal stays isolated");
            var walk=typeof(FirstPersonController).GetMethod("AdvanceMovement",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int i=0;i<165;i++)walk.Invoke(p,new object[]{Vector2.up,true,1f/120,false});
            Check(pass.CurrentRoom==chapter,"new frame has a physical sill");
            for(int i=0;i<185;i++)walk.Invoke(p,new object[]{Vector2.up,true,1f/120,i==0});
            Check(pass.CurrentRoom==room && pass.DropRegion.allowInstantiation,"jump through new frame enters isolated region");
            var rig=puzzle.transform;p.Teleport(room.transform.TransformPoint(new Vector3(0,.1f,10)),turn);yield return null;
            Check(e.journey.SentenceScope()==PassageJourney.LightSpace.Vitruvian && puzzle.source.IsOn && puzzle.ball.IsOff,"room scope and lights at rest");
            var ballMaterial=puzzle.ball.AppearanceMaterial;
            Check(ballMaterial.GetColor("_BaseColor")==Color.white && ballMaterial.GetColor("_EmissionColor").maxColorComponent==0,"orb starts white without emission");
            Check(Mathf.Min(Mathf.Abs(puzzle.UpperOffset),Mathf.Abs(puzzle.LowerOffset))>puzzle.snapTolerance,"both mirrors start outside the snap window");
            Capture(p,room.transform.TransformPoint(new Vector3(0,3.9f,5)),rig.position+Vector3.up*5,"room");
            var motion=Say(board,"light","move");var start=puzzle.OrbitAngle;
            yield return new WaitForSeconds(.18f);Check(puzzle.OrbitAngle==start,"dark orb stays still");
            var fixedSource=puzzle.source.transform.position;
            var copy=Say(board,"mirror","mirror");yield return Until(()=>room.mirrors.CopiesActive,"left mirrors reflected");
            Check(!chapter.mirrors.CopiesActive,"mirror command cannot cross portal");
            float restingOffset=puzzle.UpperOffset;
            var move=Say(board,"mirror","move");
            yield return Until(()=>Mathf.Abs(puzzle.UpperOffset-restingOffset)>2,"mirrors swing");
            Pause(board,move);yield return new WaitForSecondsRealtime(.1f);float held=puzzle.UpperOffset;
            yield return new WaitForSecondsRealtime(.15f);
            Check(held==puzzle.UpperOffset && !puzzle.SquareSolved,"stops outside snap tolerance");
            if(!state.RequirePeriod)move=Say(board,"mirror","move");else state.Speak(move.Id);
            yield return Until(()=>puzzle.UpperOffset<2 && puzzle.UpperOffset>-.5f,"return to snap window");
            var freeze=Say(board,"negative","move");
            yield return Until(()=>puzzle.SquareSolved,"pause snaps, both measured rays converge under global freeze");
            state.ReturnToLibrary(move.Id,0,true);state.ReturnToLibrary(freeze.Id,0,true);
            var retry=Say(board,"mirror","move");
            yield return Until(()=>Mathf.Abs(puzzle.UpperOffset)>7,"mirror leaves snapped angle");
            yield return Until(()=>Mathf.Abs(puzzle.UpperOffset)<1.8f,"second snap window");
            Pause(board,retry);yield return Until(()=>puzzle.SquareSolved,"manual split or native unsay snaps mirrors");
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position=(fixedSource+room.mirrors.splitter.position)*.5f;
            obstacle.transform.localScale=Vector3.one*.3f;Physics.SyncTransforms();yield return null;yield return null;
            Check(!puzzle.SquareSolved && !rig.Find("Left reflected ray").GetComponent<LineRenderer>().enabled,"obstruction before splitter cuts both branches");
            UnityEngine.Object.Destroy(obstacle);yield return Until(()=>puzzle.SquareSolved,"removing obstruction restores square");
            var eye=p.view;var camera=eye.GetComponent<Camera>();
            var ball=puzzle.ball.transform.position;
            p.Teleport(ball-rig.forward*3-Vector3.up*eye.localPosition.y,turn);
            eye.LookAt(ball-rig.right*1.85f);p.AdoptCurrentView();board.Panel.SetOpen(true);
            yield return new WaitForSecondsRealtime(.4f);Physics.SyncTransforms();
            Vector2 hit=camera.WorldToScreenPoint(ball);
            Check(pass.TryWorldDrop(board,"light",hit) && !pass.Picker.Open && puzzle.ball.TargetOn,"word drop hits orb without creating a lamp");
            yield return Until(()=>puzzle.CircleSolved,"lit orb completes a full continuous turn");
            Check(puzzle.SquareSolved && squareEvent && circleEvent && !puzzle.ball.GetComponent<Outdoor.PlayerInstance>(),"independent square and circle coexist");
            Capture(p,room.transform.TransformPoint(new Vector3(0,4.3f,12)),rig.position+Vector3.up*5.4f,"square-circle");
            var trail=rig.Find("Orbital light trail").GetComponent<LineRenderer>();
            var ink=new MaterialPropertyBlock();
            trail.GetPropertyBlock(ink);
            float firstLapTail=ink.GetFloat("_TailOpacity");
            float firstLapEmission=ballMaterial.GetColor("_EmissionColor").maxColorComponent;
            Check(firstLapTail>0 && firstLapTail<1,"first lap still fades toward the tail");
            yield return Until(()=>{trail.GetPropertyBlock(ink);return ink.GetFloat("_TailOpacity")>=1;},"continued orbits illuminate the entire ring");
            Check(ballMaterial.GetColor("_EmissionColor").maxColorComponent>firstLapEmission,"orb brightens over successive turns");
            Capture(p,room.transform.TransformPoint(new Vector3(0,4.3f,12)),rig.position+Vector3.up*5.4f,"full-ring");
            freeze=Say(board,"negative","move");yield return Until(()=>!puzzle.CircleSolved,"freeze breaks circle");
            float angle=puzzle.OrbitAngle;yield return new WaitForSecondsRealtime(.18f);
            Check(Mathf.Abs(puzzle.OrbitAngle-angle)<.01f,"freeze preserves orbital angle");
            yield return new WaitForSecondsRealtime(puzzle.trailFadeSeconds);
            Check(!rig.Find("Orbital light trail").GetComponent<LineRenderer>().enabled && puzzle.SquareSolved,"trail fades in frozen time and square stays lit");
            state.ReturnToLibrary(freeze.Id,0,true);yield return new WaitForSecondsRealtime(.22f);
            Check(!puzzle.CircleSolved && puzzle.OrbitAngle!=angle,"orbit resumes with a fresh trail");
            board.Panel.SetOpen(true);yield return new WaitForSecondsRealtime(.35f);
            var empty=new Vector2(Screen.width*.83f,Screen.height*.72f);
            int count=puzzle.field.PlayerCount;
            Check(pass.TryWorldDrop(board,"light",empty) && pass.Picker.Open,"dropping away from ball opens picker");
            pass.Picker.Select(InstanceShape.Sphere);yield return null;
            Check(puzzle.field.PlayerCount==count+1,"creates a room lamp in the selected shape");
            var created=puzzle.field.ActiveLamps.Single().GetComponent<Outdoor.PlayerInstance>();
            var recovery=pass.GetComponent<InstanceRecovery>();
            recovery.Advance(created,true,true,false,0);recovery.Advance(created,false,false,true,.05f);
            Check(created.Recovering && puzzle.field.PlayerCount==count,"room lamp recovery leaves orb intact");
            state.ReturnToLibrary(copy.Id,0,true);yield return Until(()=>!puzzle.SquareSolved,"removing mirror copy breaks square");
            board.Panel.SetOpen(false);
            p.Teleport(room.transform.TransformPoint(new Vector3(0,2.7f,10)),turn);
            eye.LookAt(rig.position+Vector3.up*5);p.AdoptCurrentView();
            yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            float litFrame=SentenceRuntimeChecks.FrameLuminance(camera);
            var off=Say(board,"negative","light");yield return Until(()=>state.Find(off.Id)==null && puzzle.ball.IsOff && puzzle.source.IsOff,"off darkens orb and fixed emitter");
            yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            Check(SentenceRuntimeChecks.FrameLuminance(camera)<litFrame*.8f && chapter.paintingSpot.intensity>0,"room surfaces darken independently of the gallery");
            var on=Say(board,"positive","light");yield return Until(()=>puzzle.ball.IsOn && puzzle.source.IsOn,"on relights both");
            Check(puzzle.source.transform.position==fixedSource,"light commands leave the emitter fixed");
            p.Teleport(gate.position+gate.forward*1.3f+Vector3.up*.13f,Quaternion.LookRotation(-gate.forward));
            board.Panel.SetOpen(false);yield return null;
            for(int i=0;i<160;i++)walk.Invoke(p,new object[]{Vector2.up,true,1f/120,i==0});
            Check(pass.CurrentRoom==chapter,"jump back through the frame into the gallery");
            Result="PASS: folded mirror, gallery isolation, physical threshold, timed snap, measured reflection, direct drop, full orbit, freeze and independent solution states";
        }
        internal static void Capture(FirstPersonController player,Vector3 eye,Vector3 target,string name){
            var folder=UnityEditor.SessionState.GetString("C2C.captureFolder","");
            if(string.IsNullOrEmpty(folder))return;
            var camera=player.view.GetComponent<Camera>();var transform=camera.transform;
            var oldPosition=transform.position;var oldRotation=transform.rotation;
            var buffer=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.DefaultHDR);
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{
                transform.position=eye;transform.LookAt(target);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),image.EncodeToPNG());
            }finally{camera.targetTexture=buffer;RenderTexture.active=active;transform.SetPositionAndRotation(oldPosition,oldRotation);RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(image);}
        }
    }
}
