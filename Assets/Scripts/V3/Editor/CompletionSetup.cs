using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace PuzzleApple.V3.Editor
{
    // Author editable native scene objects and Timeline assets once; no runtime scene construction.
    public static class CompletionSetup
    {
        static GameObject Child(string name,Transform parent,params Type[] types)
        {var g=new GameObject(name,types);g.transform.SetParent(parent,false);return g;}
        static RectTransform Full(GameObject g)
        {var rt=(RectTransform)g.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt;}
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode only.");
            var r=UnityEngine.Object.FindFirstObjectByType<OpeningRoom>();var m=r.mainRoute;
            if(m.completion)throw new InvalidOperationException("Already authored; edit existing assets.");
            var lighting=m.gameObject.AddComponent<RoomLighting>();m.galleryLighting=lighting;
            lighting.lights=m.GetComponentsInChildren<Light>().Where(l=>l.name!="Corridor light").ToArray();
            lighting.reflections=m.GetComponentsInChildren<ReflectionProbe>();
            lighting.darknessBoundary=Child("Hall lighting boundary",m.transform).transform;
            lighting.darknessBoundary.position=m.gateFrame.position;lighting.darknessBoundary.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(m.balance.position-m.gateFrame.position,Vector3.up));
            lighting.fixtures=new[]{m.transform.Find("Exhibit light shaft").GetComponent<Renderer>(),m.transform.Find("Ceiling light aperture").GetComponent<Renderer>()};
            var host=Child("Balance completion",m.transform,typeof(CutscenePlayer),typeof(PlayableDirector));
            var cut=host.GetComponent<CutscenePlayer>();m.completion=cut;cut.director=host.GetComponent<PlayableDirector>();
            cut.player=r.player;cut.panel=r.board.Panel;cut.presentation=r.presentation;
            var camera=Child("Fixed shot",host.transform,typeof(Camera));cut.shot=camera.GetComponent<Camera>();cut.shot.CopyFrom(r.presentation.view);
            cut.shot.enabled=false;cut.shot.targetTexture=null;cut.shot.fieldOfView=43;
            camera.transform.position=new Vector3(1.8f,2.7f,16.8f);camera.transform.LookAt(new Vector3(0,1,22));
            var cameraData=camera.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing=true;cameraData.requiresDepthOption=UnityEngine.Rendering.Universal.CameraOverrideOption.On;
            cameraData.volumeLayerMask=r.presentation.view.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().volumeLayerMask;
            var canvas=Child("Cinema overlay",host.transform,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            cut.overlay=canvas.GetComponent<Canvas>();cut.overlay.renderMode=RenderMode.ScreenSpaceOverlay;cut.overlay.sortingOrder=1000;cut.overlay.enabled=false;
            canvas.SetActive(false);
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            var viewport=Child("Shot",canvas.transform,typeof(RectTransform),typeof(RawImage));Full(viewport);cut.viewport=viewport.GetComponent<RawImage>();cut.viewport.raycastTarget=false;
            foreach(bool top in new[]{true,false})
            {
                var bar=Child(top?"Top bar":"Bottom bar",canvas.transform,typeof(RectTransform),typeof(Image));
                var rect=Full(bar);bar.GetComponent<Image>().color=Color.black;bar.GetComponent<Image>().raycastTarget=false;
                if(top){rect.anchorMin=new Vector2(0,.88f);cut.topBar=rect;}else{rect.anchorMax=new Vector2(1,.12f);cut.bottomBar=rect;}
            }
            if(!AssetDatabase.IsValidFolder("Assets/Animations"))AssetDatabase.CreateFolder("Assets","Animations");
            if(!AssetDatabase.IsValidFolder("Assets/Animations/V3"))AssetDatabase.CreateFolder("Assets/Animations","V3");
            var clip=new AnimationClip{name="Balance completion"};
            clip.SetCurve("",typeof(MainRoute),"completionPress",new AnimationCurve(new Keyframe(0,0),new Keyframe(.65f,0),new Keyframe(1.8f,1),new Keyframe(5.5f,1)));
            clip.SetCurve("",typeof(MainRoute),"completionExit",new AnimationCurve(new Keyframe(0,0),new Keyframe(1.8f,0),new Keyframe(4.5f,1),new Keyframe(5.5f,1)));
            AssetDatabase.CreateAsset(clip,"Assets/Animations/V3/BalanceCompletion.anim");
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.name="Balance completion";
            AssetDatabase.CreateAsset(timeline,"Assets/Animations/V3/BalanceCompletion.playable");
            var track=timeline.CreateTrack<AnimationTrack>(null,"Button and exit");track.trackOffset=TrackOffset.ApplySceneOffsets;
            var timelineClip=track.CreateClip(clip);timelineClip.duration=5.5;
            var animator=m.GetComponent<Animator>();if(!animator)animator=m.gameObject.AddComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            cut.director.playableAsset=timeline;cut.director.playOnAwake=false;cut.director.extrapolationMode=DirectorWrapMode.Hold;
            cut.director.SetGenericBinding(track,animator);UnityEventTools.AddPersistentListener(cut.completed,m.CompletePresentation);
            EditorUtility.SetDirty(timeline);AssetDatabase.SaveAssets();PrefabUtility.ApplyPrefabInstance(r.gameObject,InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(r.gameObject.scene);return "Authored room lighting, fixed camera, cinema overlay and Timeline.";
        }
    }
}
