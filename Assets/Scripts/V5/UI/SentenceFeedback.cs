using System.Collections;
using System.Collections.Generic;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    // Native line renderers and sprite-based particle systems rendered onto the overlay UI.
    // A dedicated layer/target preserves the existing screen-space overlay and its masks.
    public sealed class SentenceFeedback : MonoBehaviour
    {
        public CognitionBoard board;
        public ParticleSystem sparklePrefab;
        public Material rippleMaterial;
        public AudioClip rippleSound;
        [Range(0,1)] public float rippleVolume=.45f;
        [Range(0,31)] public int effectLayer=31;
        public float duration=1.5f;
        [Tooltip("Time between the starts of sustained ripples.")]
        [Min(.3f)] public float repeatDuration=1.24f;
        [Tooltip("Extra per-side spread for sentence stars, in canvas units.")]
        public Vector2 repeatSparklePadding=new Vector2(44,32);
        internal SentenceRuntime runtime;
        // Refresh while the board is visible, including when the player crosses a space boundary.
        Dictionary<int,SentenceResponse> visibleResponses=new Dictionary<int,SentenceResponse>();
        Dictionary<int,SentenceResponse> previousResponses=new Dictionary<int,SentenceResponse>();
        float nextSample;
        readonly HashSet<int> pulsing=new HashSet<int>();
        readonly HashSet<long> sounded = new HashSet<long>();
        readonly HashSet<long> effective = new HashSet<long>();
        public int StrongCount {get;private set;}
        public int WeakCount {get;private set;}
        Camera effectCamera;
        RawImage output;
        RenderTexture texture;
        Transform stage;
        AudioSource rippleAudio;
        void Start()
        {
            rippleAudio=gameObject.AddComponent<AudioSource>();
            rippleAudio.playOnAwake=false;
            rippleAudio.loop=false;
            rippleAudio.spatialBlend=0;
            var root=new GameObject("Sentence feedback rendering");root.transform.SetParent(transform,false);
            stage=root.transform;stage.position=new Vector3(0,0,-10000);
            var cameraObject=new GameObject("Sentence feedback camera",typeof(Camera));cameraObject.transform.SetParent(stage,false);
            effectCamera=cameraObject.GetComponent<Camera>();
            effectCamera.orthographic=true;effectCamera.nearClipPlane=.1f;effectCamera.farClipPlane=20;
            effectCamera.cullingMask=1<<effectLayer;effectCamera.clearFlags=CameraClearFlags.SolidColor;
            effectCamera.backgroundColor=Color.clear;effectCamera.allowHDR=false;effectCamera.allowMSAA=false;
            var ui=new GameObject("Sentence feedback overlay",typeof(RectTransform),typeof(Canvas));ui.transform.SetParent(transform,false);
            var canvas=ui.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;
            var image=new GameObject("Ripple and sparkle image",typeof(RectTransform),typeof(RawImage));image.transform.SetParent(ui.transform,false);
            output=image.GetComponent<RawImage>();output.raycastTarget=false;
            var rect=output.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            Resize();
        }
        void Resize()
        {
            if(texture){effectCamera.targetTexture=null;texture.Release();Destroy(texture);}
            texture=new RenderTexture(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height),16,RenderTextureFormat.ARGB32){name="Sentence feedback"};
            texture.Create();effectCamera.targetTexture=texture;output.texture=texture;
            effectCamera.orthographicSize=Screen.height*.5f;
        }
        void LateUpdate()
        {
            if(!output)return;
            if(texture.width!=Screen.width||texture.height!=Screen.height)Resize();
            output.enabled=board.Panel.IsOpen&&board.Surface.gameObject.activeInHierarchy;
            if(!runtime || board.State==null)return;
            if(!output.enabled){visibleResponses.Clear();nextSample=0;return;}
            if(Time.unscaledTime<nextSample)return;
            nextSample=Time.unscaledTime+.08f;
            var spare=previousResponses;
            previousResponses=visibleResponses;
            visibleResponses=spare;
            visibleResponses.Clear();
            effective.Clear();
            foreach(var group in board.State.Groups)
            {
                if(!group.Recognized || !group.Spoken)continue;
                effective.Add(group.Established);
                if(group.OneShot || (!group.Conflicted && !runtime.Committed(group)))continue;
                var response=runtime.Response(group);
                visibleResponses[group.Id]=response;
                board.SetResponseOpacity(group.Id,WordOpacity(response));
                if(SentenceRuntime.Responding(response)){
                    if(!pulsing.Contains(group.Id))StartCoroutine(Pulse(group.Id,response,true));
                }else if(previousResponses.TryGetValue(group.Id,out var previous) && previous!=response){
                    StartCoroutine(Pulse(group.Id,response,true));
                }
            }
            sounded.IntersectWith(effective);
        }
        void SoundOnce(CognitionState.Group group)
        {
            if (rippleAudio && rippleSound && sounded.Add(group.Established))
                rippleAudio.PlayOneShot(rippleSound,rippleVolume);
        }
        static float WordOpacity(SentenceResponse response)=>response==SentenceResponse.Local?1:
            response==SentenceResponse.Remote?.62f:CognitionBoard.ConflictAlpha;
        static float RingOpacity(SentenceResponse response)=>response==SentenceResponse.Local?.62f:
            response==SentenceResponse.Remote?.42f:.25f;

        public void Play(int groupId,bool strong)=>Play(groupId,strong?SentenceResponse.Local:SentenceResponse.NoTarget);
        internal void Play(int groupId,SentenceResponse response)
        {
            visibleResponses.Remove(groupId);
            var group = board.State.Find(groupId);
            bool responding=SentenceRuntime.Responding(response);
            if(responding && group!=null && group.Effective)SoundOnce(group);
            board.SetResponseOpacity(groupId,WordOpacity(response));
            if(!stage||!board.Panel.IsOpen)return;
            if(responding)StrongCount++;else WeakCount++;
            StartCoroutine(Pulse(groupId,response));
        }
        IEnumerator Pulse(int groupId,SentenceResponse response,bool repeat=false)
        {
            bool strong=SentenceRuntime.Responding(response);
            var rect=board.GroupRect(groupId);if(!rect)yield break;
            var initialGroup=board.State.Find(groupId);
            if(initialGroup==null||!initialGroup.Recognized||(strong&&!initialGroup.Effective))yield break;
            if(strong && !pulsing.Add(groupId))yield break;
            long established=initialGroup.Established;
            bool ownsPulse = strong;
            var content=board.GroupContentBounds(groupId);
            var root=new GameObject(strong?"Sentence effect ripple":"Sentence grammar ripple");root.layer=effectLayer;
            root.transform.SetParent(stage,false);
            var lines=new LineRenderer[strong?2:1];
            for(int i=0;i<lines.Length;i++)
            {
                var ring=new GameObject("Ripple "+i);ring.layer=effectLayer;ring.transform.SetParent(root.transform,false);
                var line=ring.AddComponent<LineRenderer>();line.sharedMaterial=rippleMaterial;line.useWorldSpace=false;
                line.loop=true;line.positionCount=4;line.widthMultiplier=strong?1.8f:1.1f;
                line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
                lines[i]=line;
            }
            float scale=board.GetComponentInParent<Canvas>().scaleFactor;
            if(response!=SentenceResponse.NoTarget&&sparklePrefab)
            {
                var particles=Instantiate(sparklePrefab,root.transform);
                var padding=repeatSparklePadding;
                var shape=particles.shape;
                shape.scale=new Vector3((content.width+2*Mathf.Max(0,padding.x))*scale,
                    (content.height*.8f+2*Mathf.Max(0,padding.y))*scale,0);
                var main=particles.main;main.startSize=new ParticleSystem.MinMaxCurve(34*scale,60*scale);
                main.useUnscaledTime=true;
                main.simulationSpeed *= .85f/duration;
                particles.Play();
            }
            if(!strong&&!repeat&&rippleSound&&rippleAudio)rippleAudio.PlayOneShot(rippleSound,rippleVolume);
            float visualDuration=duration*1.2f;
            for(float t=0;t<visualDuration;t+=Time.unscaledDeltaTime)
            {
                var group=board.State.Find(groupId);
                if(!rect||!board.Panel.IsOpen||group==null||group.Established!=established||
                    !group.Recognized||(strong&&!group.Effective))break;
                if(!group.OneShot && visibleResponses.TryGetValue(groupId,out var current) && current!=response)break;
                if (ownsPulse && t >= repeatDuration*1.2f)
                {
                    // Let the next pulse start while this pair of rings finishes fading.
                    pulsing.Remove(groupId);
                    ownsPulse = false;
                }
                content=board.GroupContentBounds(groupId);
                Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(content.center));
                root.transform.localPosition=new Vector3(screen.x-Screen.width*.5f,screen.y-Screen.height*.5f,5);
                for(int i=0;i<lines.Length;i++)
                {
                    float delay=i*.14f*visualDuration;
                    float u=Mathf.Clamp01((t-delay)/(visualDuration-delay));
                    float alpha=RingOpacity(response)*Mathf.Sin(Mathf.PI*Mathf.Pow(u,.55f));
                    lines[i].startColor=lines[i].endColor=new Color(.89f,.94f,1,alpha);
                    // Briefly hold the expanded frame before it resumes spreading and fades.
                    const float held = CognitionBoard.HeldRippleExpansion;
                    float expansion=u<.35f?Mathf.SmoothStep(0,held,u/.35f):u<.55f?held:Mathf.SmoothStep(held,1,(u-.55f)/.45f);
                    var bounds=CognitionBoard.RippleBounds(content,expansion);
                    float x=bounds.width*.5f*scale;
                    float y=bounds.height*.5f*scale;
                    lines[i].SetPosition(0,new Vector3(-x,-y,0));
                    lines[i].SetPosition(1,new Vector3(-x,y,0));
                    lines[i].SetPosition(2,new Vector3(x,y,0));
                    lines[i].SetPosition(3,new Vector3(x,-y,0));
                }
                yield return null;
            }
            Destroy(root);
            if(ownsPulse)pulsing.Remove(groupId);
        }
        void OnDestroy(){if(texture){texture.Release();Destroy(texture);}}
    }
}
