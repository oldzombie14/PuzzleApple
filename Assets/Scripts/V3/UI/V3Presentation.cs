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
        public Material viewMaterial;
        public Shader blurShader;
        public Sprite eyeSprite, forbiddenSprite;
        public Material reticleMaterial;
        [Min(1)] public float openingDuration=5.5f;
        [Min(0)] public float openingSettleTime=.35f;
        [Min(1)] public float wordDisplayDuration=3.4f;
        public CognitionBoard board;
        public bool Ready { get; private set; }
        public bool SelfRevealReady { get; private set; }
        public bool Split { get; private set; }
        public RenderTexture Frame => frame;
        RenderTexture frame;Material material;
        RenderTexture softFrame,softTemp;Material softMaterial;
        Camera displayCamera;
        RawImage first,second;
        RectTransform hud,collection;
        Image eye,forbidden,symbol;
        Text question,message;
        GameObject cross;
        Image otherEye,otherForbidden;Text otherQuestion;GameObject otherCross;
        readonly Queue<WordDefinition> announcements=new Queue<WordDefinition>();
        bool showing,introducedSelf;float messageUntil;
        Font font;
        void Start()
        {
            material=new Material(viewMaterial);material.SetFloat("_Open",0);material.SetFloat("_Blur",1);
            if(blurShader)softMaterial=new Material(blurShader);
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering+=SoftenFrame;
            var display=new GameObject("V3 world view",typeof(RectTransform),typeof(Canvas));var canvas=display.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=-10;
            // The world camera renders offscreen. Keep a real display output for the
            // overlay compositor, otherwise Game View reports "No cameras rendering".
            var output=new GameObject("V3 display output",typeof(Camera));output.transform.SetParent(display.transform,false);
            displayCamera=output.GetComponent<Camera>();displayCamera.cullingMask=0;
            displayCamera.clearFlags=CameraClearFlags.SolidColor;displayCamera.backgroundColor=Color.black;
            displayCamera.targetDisplay=view.targetDisplay;canvas.targetDisplay=view.targetDisplay;
            displayCamera.depth=view.depth+1;displayCamera.allowHDR=false;displayCamera.allowMSAA=false;
            var outputData=output.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            outputData.renderPostProcessing=false;outputData.renderShadows=false;
            outputData.requiresColorOption=UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
            outputData.requiresDepthOption=UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
            first=ViewImage(display.transform,"World viewport A");second=ViewImage(display.transform,"World viewport B");second.gameObject.SetActive(false);
            hud=CognitionUI.Rect("V3 HUD",board.transform);CognitionUI.Stretch(hud);hud.SetAsLastSibling();
            eye=CognitionUI.Image("Collect",hud,Color.white);eye.sprite=eyeSprite;eye.preserveAspect=true;eye.material=reticleMaterial;Center(eye.rectTransform,140,140);
            forbidden=CognitionUI.Image("Unavailable",hud,Color.white);forbidden.sprite=forbiddenSprite;forbidden.material=reticleMaterial;Center(forbidden.rectTransform,38,38);
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},18);
            question=CognitionUI.Text("Interact",hud,font,"?",42,Color.white);question.alignment=TextAnchor.MiddleCenter;question.material=reticleMaterial;Center(question.rectTransform,52,52);
            cross=CognitionUI.Rect("Aim",hud).gameObject;Center((RectTransform)cross.transform,24,24);
            foreach(float angle in new[]{45f,-45f}){var line=CognitionUI.Image("Aim stroke",cross.transform,Color.white);line.material=reticleMaterial;Center(line.rectTransform,20,3);line.rectTransform.localRotation=Quaternion.Euler(0,0,angle);}
            otherEye=Instantiate(eye,hud);otherForbidden=Instantiate(forbidden,hud);otherQuestion=Instantiate(question,hud);otherCross=Instantiate(cross,hud);
            foreach(var r in new[]{otherEye.rectTransform,otherForbidden.rectTransform,otherQuestion.rectTransform,(RectTransform)otherCross.transform})r.anchorMin=r.anchorMax=new Vector2(.75f,.5f);
            collection=CognitionUI.Rect("Acquired word",hud);Center(collection,100,100);symbol=collection.gameObject.AddComponent<CenteredSymbolImage>();symbol.raycastTarget=false;symbol.color=Color.white;collection.gameObject.SetActive(false);
            var outline=collection.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.04f,.04f,.04f,.9f);outline.effectDistance=new Vector2(1.5f,-1.5f);outline.useGraphicAlpha=true;
            message=CognitionUI.Text("World feedback",hud,font,"",19,Color.white);message.alignment=TextAnchor.MiddleCenter;message.gameObject.AddComponent<Outline>().effectColor=Color.black;Center(message.rectTransform,620,50);message.rectTransform.anchoredPosition=new Vector2(0,-210);
            Resize(); SetHover(-1);StartCoroutine(OpenEyes());
        }
        RawImage ViewImage(Transform parent,string name){var r=CognitionUI.Rect(name,parent).gameObject.AddComponent<RawImage>();r.material=material;r.raycastTarget=false;CognitionUI.Stretch(r.rectTransform);return r;}
        public static void Center(RectTransform r,float w,float h){r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(w,h);}
        IEnumerator OpenEyes()
        {
            yield return new WaitForSeconds(.45f);
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
            frame=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){name="V3 world",filterMode=FilterMode.Bilinear};frame.Create();view.targetTexture=frame;view.aspect=(float)w/h;first.texture=second.texture=frame;
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
        }
        public void SetHover(int state){if(!eye)return;bool visible=Ready&&!board.Panel.IsOpen;eye.gameObject.SetActive(visible&&state==1);question.gameObject.SetActive(visible&&state==2);forbidden.gameObject.SetActive(visible&&state==3);cross.SetActive(visible&&state==0);otherEye.gameObject.SetActive(Split&&visible&&state==1);otherQuestion.gameObject.SetActive(Split&&visible&&state==2);otherForbidden.gameObject.SetActive(Split&&visible&&state==3);otherCross.SetActive(Split&&visible&&state==0);}
        public void Notify(string value){message.text=value;messageUntil=Time.unscaledTime+2.2f;}
        public void ShowWord(WordDefinition word){announcements.Enqueue(word);}
        IEnumerator Announce(WordDefinition word)
        {
            showing=true;symbol.sprite=word.Symbol;symbol.preserveAspect=true;collection.gameObject.SetActive(true);Center(collection,90,90);
            float t=0;
            while(t<wordDisplayDuration)
            {
                t+=Time.unscaledDeltaTime;float fly=Mathf.SmoothStep(0,1,Mathf.InverseLerp(wordDisplayDuration-.95f,wordDisplayDuration,t));
                float reveal=Mathf.SmoothStep(0,1,t/.65f);
                symbol.color=new Color(1,1,1,reveal*(1-fly*.7f));
                collection.localScale=Vector3.one*Mathf.Lerp(Mathf.Lerp(.9f,1,reveal),.4f,fly);
                collection.anchoredPosition=Vector2.Lerp(Vector2.zero,new Vector2(-((RectTransform)hud.parent).rect.width*.42f,-((RectTransform)hud.parent).rect.height*.4f),fly);
                yield return null;
            }
            collection.gameObject.SetActive(false);showing=false;
            if(word.Id=="i"&&!introducedSelf)
            {
                while(!Ready)yield return null;
                introducedSelf=true;board.Panel.SetOpen(true);
            }
        }
        void OnDestroy(){UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=SoftenFrame;ReleaseSoftFrames();if(softMaterial)Destroy(softMaterial);if(view){view.targetTexture=null;view.ResetAspect();}if(first)Destroy(first.transform.parent.gameObject);if(frame){frame.Release();Destroy(frame);}if(material)Destroy(material);if(font)Destroy(font);}
    }
}
