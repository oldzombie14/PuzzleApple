using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace PuzzleApple.V3
{
    // Timeline owns animation; this reusable host owns viewing and input lifetime.
    public sealed class CutscenePlayer : MonoBehaviour
    {
        public PlayableDirector director;
        public Camera shot;
        public Canvas overlay;
        public RawImage viewport;
        public RectTransform topBar,bottomBar;
        public FirstPersonController player;
        public GameplayPanel panel;
        public V3Presentation presentation;
        [Range(0,.25f)] public float barHeight=.12f;
        public float barFade=.35f;
        public UnityEvent completed=new UnityEvent();
        public bool Playing { get; private set; }
        bool wasLocked,wasBlocked,wasPanelOpen;
        RenderTexture frame;
        void Awake(){overlay.gameObject.SetActive(false);shot.enabled=false;director.playOnAwake=false;}
        public bool Play()
        {
            if(Playing||!director.playableAsset)return false;
            wasLocked=player.Locked;wasBlocked=panel.InputBlocked;wasPanelOpen=panel.IsOpen;
            panel.SetOpen(false);panel.InputBlocked=true;player.SetPresentationLocked(true);
            Playing=true;presentation.SetHover(-1);overlay.enabled=true;overlay.gameObject.SetActive(true);shot.enabled=true;
            Resize();director.time=0;director.Play();return true;
        }
        void Resize()
        {
            int w=Mathf.Max(640,Screen.width),h=Mathf.Max(360,Screen.height);
            if(frame&&frame.width==w&&frame.height==h)return;
            Release();frame=new RenderTexture(w,h,24,RenderTextureFormat.DefaultHDR){name="Cutscene view"};frame.Create();
            shot.targetTexture=frame;shot.aspect=(float)w/h;viewport.texture=frame;
        }
        void Update()
        {
            if(!Playing)return;
            Resize();float time=(float)director.time,duration=(float)director.duration;
            float amount=Mathf.SmoothStep(0,1,Mathf.Min(time,duration-time)/Mathf.Max(.01f,barFade));
            topBar.anchorMin=new Vector2(0,1-barHeight*amount);bottomBar.anchorMax=new Vector2(1,barHeight*amount);
            if(time>=duration-.025f)Finish(true);
        }
        void Finish(bool success)
        {
            if(!Playing)return;
            Playing=false;director.Stop();overlay.gameObject.SetActive(false);shot.enabled=false;Release();
            player.SetPresentationLocked(wasLocked);panel.InputBlocked=wasBlocked;panel.SetOpen(wasPanelOpen);
            if(success)completed.Invoke();
        }
        void Release(){if(shot)shot.targetTexture=null;if(frame){frame.Release();Destroy(frame);frame=null;}}
        void OnDisable()=>Finish(false);
        void OnDestroy()=>Release();
    }
}
