using PlanarMirror = PuzzleApple.V4.Dependencies.PlanarMirror;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V4.Editor
{
    public static class ChapterIChecks
    {
        public static string Result { get; private set; }
        static void Require(bool condition,string text) { if(!condition){Result="FAIL: "+text;throw new InvalidOperationException(Result);} }
        public static void Run()
        {
            var m=UnityEngine.Object.FindFirstObjectByType<SessionSceneManager>();
            if(!Application.isPlaying||!m||m.Current.gameObject.scene.name!="ChapterI")throw new InvalidOperationException("Fresh ChapterI Play required");
            Result="Running";m.StartCoroutine(Check(m));
        }
        static void Capture(Camera camera,string name)
        {
            Directory.CreateDirectory("Temp/ChapterI");
            var prior=camera.targetTexture;var active=RenderTexture.active;
            var frame=RenderTexture.GetTemporary(960,600,24);var photo=new Texture2D(960,600,TextureFormat.RGB24,false);
            try {camera.targetTexture=frame;camera.Render();RenderTexture.active=frame;photo.ReadPixels(new Rect(0,0,960,600),0,0);photo.Apply();File.WriteAllBytes("Temp/ChapterI/"+name+".png",photo.EncodeToPNG());}
            finally {camera.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(frame);UnityEngine.Object.Destroy(photo);}
        }
        static IEnumerator WalkThrough(SessionSceneManager m,ScenePortal portal)
        {
            var player=m.player;
            // The actual CharacterController crosses the plane; no direct scene-switch call.
            var start=portal.transform.position+portal.transform.forward*2.8f;
            start.y=portal.gameObject.scene.name=="ChapterI"?.105f:.03f;
            player.Teleport(start,Quaternion.LookRotation(-portal.transform.forward));
            player.view.localRotation=Quaternion.identity;player.AdoptCurrentView();Physics.SyncTransforms();
            yield return null;
            string source=m.Current.gameObject.scene.name;
            for(int i=0;i<115&&m.Current.gameObject.scene.name==source;i++)
            {player.GetComponent<CharacterController>().Move(-portal.transform.forward*.04f+Vector3.down*.02f);yield return null;}
            Require(m.Current.gameObject.scene.name==portal.destinationScene,"physical crossing enters "+portal.destinationScene);
        }
        static IEnumerator Check(SessionSceneManager m)
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            var player=m.player;var camera=player.view.GetComponent<Camera>();
            try
            {
                m.board.Panel.SetOpen(false);player.enabled=true;
                var fold=UnityEngine.Object.FindFirstObjectByType<FoldingRoom>();
                Require(!m.board.State.Knows("mirror")&&!fold.Opened,"fresh room closed and mirror unknown");
                var target=UnityEngine.Object.FindFirstObjectByType<ChapterVocabulary>();
                var originalMirrorPosition=target.transform.position;
                var arrival=m.Current.arrival;
                Require(Vector3.Dot(Vector3.ProjectOnPlane(originalMirrorPosition-arrival.position,Vector3.up).normalized,arrival.forward)<-.999f,"mirror is directly behind the arrival facing");
                player.Teleport(new Vector3(29,.105f,0),Quaternion.Euler(0,90,0));
                player.view.rotation=Quaternion.LookRotation(target.transform.position-player.view.position);player.AdoptCurrentView();
                yield return null;
                var mirror=target.GetComponent<PlanarMirror>();mirror.RenderForCamera(camera);
                var reflection=new MaterialPropertyBlock();target.GetComponent<Renderer>().GetPropertyBlock(reflection);
                Require(reflection.GetFloat("_ReflectionReady")==1,"mirror renders from shared player camera");
                Capture(camera,"01-mirror-before");
                Cursor.lockState=CursorLockMode.Locked;player.CinematicLocked=false;
                var flow=m.Current.content.GetComponent<OpeningFlow>();
                Require(flow.TryCollectPlacedWord(),"mirror collected through actual range/occlusion ray");
                player.CinematicLocked=true;
                Require(!m.board.State.Knows("mirror"),"vocabulary waits for collection animation");
                yield return new WaitForSeconds(2);
                Require(m.board.State.Knows("mirror"),"mirror acquired after flight");
                Require(fold.Animating,"word acquisition starts unfolding");
                Capture(camera,"02-unfolding");
                player.CinematicLocked=false;
                Require(!player.MovementLocked,"unfold does not lock player movement");
                float foldDeadline=Time.realtimeSinceStartup+10;
                while(fold.Animating&&Time.realtimeSinceStartup<foldDeadline)yield return null;
                Require(fold.Opened&&!fold.Animating,"unfold completed");
                Require(fold.GetComponentsInChildren<Renderer>().All(r=>r.bounds.max.y<.075f),"unfolded panels sink below floor");
                Require(!m.board.Catalog.Rules.Any(r=>r.Words.Any(w=>w.Id=="mirror")),"no mirror grammar introduced");
                Require(Vector3.Distance(target.transform.position,originalMirrorPosition)<.001f,"mirror stays fixed during unfolding");
                player.Teleport(new Vector3(24,.105f,0),Quaternion.Euler(0,-90,0));player.view.localRotation=Quaternion.identity;player.AdoptCurrentView();
                yield return null;Capture(camera,"03-corridor");
                var entrance=m.Current.content.GetComponentInChildren<ScenePortal>();
                float until=Time.realtimeSinceStartup+15;
                while(!entrance.Ready&&Time.realtimeSinceStartup<until)yield return null;
                Require(entrance.Ready,"garden preload and return link ready");
                player.CinematicLocked=true;
                player.Teleport(entrance.transform.position+entrance.transform.forward*3.5f-Vector3.up*(entrance.aperture.y*.5f-.105f),Quaternion.LookRotation(-entrance.transform.forward));player.view.localRotation=Quaternion.identity;player.AdoptCurrentView();
                yield return null;entrance.RenderView(camera);Capture(camera,"04-frame-center");
                var view=entrance.ViewCamera.transform.position;
                player.Teleport(player.transform.position+Vector3.right*.8f,player.transform.rotation);
                yield return null;entrance.RenderView(camera);Capture(camera,"05-frame-side");
                Require(Vector3.Distance(view,entrance.ViewCamera.transform.position)>.7f,"portal viewpoint responds to player motion");
                player.CinematicLocked=false;player.enabled=false;
                yield return WalkThrough(m,entrance);
                Require(m.board.State.Knows("mirror"),"core word survives garden entry");
                yield return null;
                for(int i=0;i<8&&m.Current.gameObject.scene.name=="ChapterI_Garden";i++)
                {player.GetComponent<CharacterController>().Move(-entrance.Destination.transform.forward*.04f);yield return null;}
                Require(m.Current.gameObject.scene.name=="ChapterI","immediate step back across frame returns without a cooldown trap");
                yield return WalkThrough(m,entrance);
                yield return null;Capture(camera,"06-inside-garden");
                var drift=m.Current.content.GetComponentInChildren<FloatingFlowers>();var pos=drift.flowers[0].position;
                yield return new WaitForSeconds(.7f);
                Require(Vector3.Distance(pos,drift.flowers[0].position)>.001f,"flowers float over time");
                yield return WalkThrough(m,entrance.Destination);
                Require(fold.Opened&&m.board.State.Knows("mirror"),"return preserves unfolded room and vocabulary");
                yield return new WaitForSeconds(.3f);
                yield return WalkThrough(m,entrance);
                yield return new WaitForSeconds(.3f);
                yield return WalkThrough(m,entrance.Destination);
                Require(UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None).Length==1,"one persistent player after repeated crossing");
                m.board.Panel.SetOpen(true);Require(player.PanelOpen,"panel works on return");m.board.Panel.SetOpen(false);
                player.enabled=true;
                Require(m.Request("P4"),"normal scene transition accepted after portals");
                while(m.Busy)yield return null;
                Require(!SceneManager.GetSceneByName("ChapterI").isLoaded&&!SceneManager.GetSceneByName("ChapterI_Garden").isLoaded,"normal transition unloads portal neighbours");
                Require(m.board.State.Knows("mirror"),"core word persists beyond chapter");
                Result="PASS: ray collection, delayed acquisition, unfold, fixed mirror, no grammar, live parallax, physical entry/exit twice, floating flowers, state/UI continuity and neighbour unload. Screenshots: Temp/ChapterI.";
            }
            finally {player.CinematicLocked=false;player.MovementLocked=false;player.enabled=true;Application.runInBackground=background;}
        }
    }
}
