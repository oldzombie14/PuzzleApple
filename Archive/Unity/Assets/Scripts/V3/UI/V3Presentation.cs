using System.Collections;
using System.Collections.Generic;
using PuzzleApple.V3.Cognition;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V3
{
    [DefaultExecutionOrder(-90)]
    public sealed class V3Presentation : MonoBehaviour
    {
        public Camera view;
        public Camera secondaryView;
        public Material viewMaterial;
        public Shader blurShader;


        [Min(1)] public float openingDuration=5.5f;
        public bool selfBeforeEyes;
        [Min(0)] public float selfBlackoutDuration=1;
        [Min(.1f)] public float selfFadeInDuration=.65f;
        [Min(0)] public float selfHoldDuration=.3f;
        [Min(0)] public float openingSettleTime=.35f;
        [Tooltip("Total fade, hold and collection flight for subsequent words; opening self uses its own timing.")]
        [Min(1)] public float wordDisplayDuration=3.4f;
        [Min(.1f)] public float wordFadeInDuration=.65f;
        [Min(.1f)] public float wordFlyDuration=.95f;
        public CognitionBoard board;
        public bool Ready { get; private set; }
        public bool SelfRevealReady { get; private set; }
        public bool Split { get; private set; }
        public RenderTexture Frame => frame;
        public event System.Action<WordDefinition> WordCollected;
        RenderTexture frame,secondaryFrame;Material material;
        RenderTexture softFrame,softTemp;Material softMaterial;
        [SerializeField] Camera displayCamera;
        [SerializeField] RawImage first,second;
        [SerializeField] RectTransform hud,collection;
        [SerializeField] Image eye,forbidden,symbol;
        [SerializeField] Text question,message;
        [SerializeField] GameObject cross;
        [SerializeField] Image otherEye,otherForbidden; [SerializeField] Text otherQuestion; [SerializeField] GameObject otherCross;
        readonly Queue<WordDefinition> announcements=new Queue<WordDefinition>();
        bool showing,introducedSelf,selfCanOpenEyes;float messageUntil;
        Vector2 collectionHome; Vector3 collectionScale;
        void Start()
        {
            material=new Material(viewMaterial);material.SetFloat("_Open",0);material.SetFloat("_Blur",1);
            if(blurShader)softMaterial=new Material(blurShader);
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering+=SoftenFrame;
            first.material=second.material=material;
            displayCamera.targetDisplay=view.targetDisplay;
            first.canvas.targetDisplay=view.targetDisplay;
            displayCamera.depth=Mathf.Max(view.depth,secondaryView?secondaryView.depth:view.depth)+1;
            if(secondaryView)secondaryView.enabled=false;
            collectionHome=collection.anchoredPosition;collectionScale=collection.localScale;
            Resize(); SetHover(-1);StartCoroutine(OpenEyes());
        }
        IEnumerator OpenEyes()
        {
            yield return new WaitForSecondsRealtime(selfBeforeEyes?selfBlackoutDuration:.45f);
            if(selfBeforeEyes){SelfRevealReady=true;while(!selfCanOpenEyes)yield return null;}
            float t=0;
            while(t<openingDuration)
            {
                t+=Time.deltaTime;float progress=Mathf.Clamp01(t/openingDuration);
                material.SetFloat("_Open",Mathf.SmoothStep(0,1,progress));
                if(progress>=.45f)SelfRevealReady=true;
                material.SetFloat("_Blur",1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,1,progress)));
                yield return null;
            }
            material.SetFloat("_Open",1);material.SetFloat("_Blur",0);
            yield return new WaitForSeconds(openingSettleTime);Ready=true;
        }
        void Resize()
        {
            int fullWidth=Mathf.Clamp(Screen.width,640,1920),h=Mathf.Max(360,Mathf.RoundToInt(fullWidth*(float)Screen.height/Mathf.Max(1,Screen.width)));
            int w=Split?fullWidth/2:fullWidth;
            if(frame&&frame.width==w&&frame.height==h)return;
            if(frame){view.targetTexture=null;frame.Release();Destroy(frame);}
            // URP derives its camera color format from the target. An LDR target clips
            // bright exhibit lighting before the hall's negative exposure is applied.
            frame=new RenderTexture(w,h,24,RenderTextureFormat.DefaultHDR){name="V3 world",filterMode=FilterMode.Bilinear};frame.Create();view.targetTexture=frame;view.aspect=(float)w/h;first.texture=second.texture=frame;
            ReleaseSecondaryFrame();
            if(secondaryView&&Split)
            {
                secondaryFrame=new RenderTexture(w,h,24,RenderTextureFormat.DefaultHDR){name="V3 mirrored viewpoint",filterMode=FilterMode.Bilinear};secondaryFrame.Create();
                secondaryView.targetTexture=secondaryFrame;secondaryView.aspect=(float)w/h;secondaryView.enabled=true;second.texture=secondaryFrame;
            }
            ReleaseSoftFrames();
            softFrame=new RenderTexture(Mathf.Max(1,w/4),Mathf.Max(1,h/4),0){name="V3 soft focus",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};softFrame.Create();
            softTemp=new RenderTexture(softFrame.descriptor){name="V3 focus intermediate",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};softTemp.Create();
            material.SetTexture("_SoftTex",softFrame);
        }
        void SoftenFrame(UnityEngine.Rendering.ScriptableRenderContext context,Camera camera)
        {
            if(camera!=view||!frame||!softFrame||!material||material.GetFloat("_Blur")<.001f)return;
            Graphics.Blit(frame,softFrame);
            if(!softMaterial)return;
            float radius=Mathf.Lerp(.25f,1.8f,material.GetFloat("_Blur"));
            softMaterial.SetVector("_Axis",new Vector4(radius,0,0,0));Graphics.Blit(softFrame,softTemp,softMaterial);
            softMaterial.SetVector("_Axis",new Vector4(0,radius,0,0));Graphics.Blit(softTemp,softFrame,softMaterial);
        }
        void ReleaseSoftFrames(){if(softFrame){softFrame.Release();Destroy(softFrame);}if(softTemp){softTemp.Release();Destroy(softTemp);}}
        void Update(){if(!first)return;Resize();if(!showing&&announcements.Count>0)StartCoroutine(Announce(announcements.Dequeue()));if(Time.unscaledTime>messageUntil)message.text="";}
        public void SetSplit(bool value)
        {
            if(!first||Split==value)return;Split=value;second.gameObject.SetActive(value);
            first.rectTransform.anchorMax=new Vector2(value?.5f:1,1);first.rectTransform.offsetMin=first.rectTransform.offsetMax=Vector2.zero;
            second.rectTransform.anchorMin=new Vector2(.5f,0);second.rectTransform.anchorMax=Vector2.one;second.rectTransform.offsetMin=second.rectTransform.offsetMax=Vector2.zero;
            foreach(var r in new[]{eye.rectTransform,forbidden.rectTransform,question.rectTransform,(RectTransform)cross.transform})r.anchorMin=r.anchorMax=new Vector2(value?.25f:.5f,.5f);
            // The secondary camera already occupies the symmetric viewpoint.
            // Flipping its image again cancels the visible left/right perspective.
            second.uvRect=new Rect(0,0,1,1);
            if(material)Resize();
        }
        void ReleaseSecondaryFrame()
        {
            if(secondaryView){secondaryView.enabled=false;secondaryView.targetTexture=null;secondaryView.ResetAspect();}
            if(secondaryFrame){secondaryFrame.Release();Destroy(secondaryFrame);secondaryFrame=null;}
        }
        public void SetHover(int state){if(!eye)return;bool visible=Ready&&!board.Panel.IsOpen;eye.gameObject.SetActive(visible&&state==1);question.gameObject.SetActive(visible&&state==2);forbidden.gameObject.SetActive(visible&&state==3);cross.SetActive(visible&&state==0);otherEye.gameObject.SetActive(Split&&visible&&state==1);otherQuestion.gameObject.SetActive(Split&&visible&&state==2);otherForbidden.gameObject.SetActive(Split&&visible&&state==3);otherCross.SetActive(Split&&visible&&state==0);}
        public void Notify(string value,int fontSize=22,float duration=2.2f)
        {message.fontSize=fontSize;message.rectTransform.sizeDelta=new Vector2(700,90);message.text=value;messageUntil=Time.unscaledTime+duration;}
        public void ShowWord(WordDefinition word){announcements.Enqueue(word);}
        IEnumerator Announce(WordDefinition word)
        {
            showing=true;symbol.sprite=word.Symbol;symbol.preserveAspect=true;collection.gameObject.SetActive(true);collection.anchoredPosition=collectionHome;collection.localScale=collectionScale;
            bool openingSelf=word.Id=="i"&&selfBeforeEyes;
            float fadeIn=openingSelf?selfFadeInDuration:wordFadeInDuration;
            float flyDuration=openingSelf?.95f:wordFlyDuration;
            float duration=openingSelf?fadeIn+selfHoldDuration+.95f:wordDisplayDuration;
            float t=0;
            while(t<duration)
            {
                t+=Time.unscaledDeltaTime;float fly=Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration-flyDuration,duration,t));
                float reveal=openingSelf?Mathf.Clamp01(t/fadeIn):Mathf.SmoothStep(0,1,t/fadeIn);
                if(openingSelf&&t>=fadeIn+selfHoldDuration)selfCanOpenEyes=true;
                symbol.color=new Color(1,1,1,reveal*(1-fly*.7f));
                collection.localScale=collectionScale*Mathf.Lerp(Mathf.Lerp(.9f,1,reveal),.4f,fly);
                collection.anchoredPosition=Vector2.Lerp(collectionHome,new Vector2(-((RectTransform)hud.parent).rect.width*.42f,-((RectTransform)hud.parent).rect.height*.4f),fly);
                yield return null;
            }
            collection.gameObject.SetActive(false);showing=false;
            WordCollected?.Invoke(word);
            if(word.Id=="i"&&!introducedSelf)
            {
                while(!Ready)yield return null;
                introducedSelf=true;board.Panel.SetOpen(true);
            }
        }
        void OnDestroy(){UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=SoftenFrame;ReleaseSoftFrames();ReleaseSecondaryFrame();if(softMaterial)Destroy(softMaterial);if(view){view.targetTexture=null;view.ResetAspect();}if(frame){frame.Release();Destroy(frame);}if(material)Destroy(material);}
    }
}
