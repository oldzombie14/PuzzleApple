using System.Collections;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using UnityEngine.UI;
using PuzzleApple.V5.Outdoor;

namespace PuzzleApple.V5
{
    public sealed class LightCapacityDots : MonoBehaviour
    {
        LightDotsGraphic counter;
        Image symbol,traveller;
        RawImage halo;
        Texture2D haloTexture;
        PeriodGlyph guide;
        void Awake()
        {
            var dots = new GameObject("Remaining light instances", typeof(RectTransform), typeof(CanvasRenderer), typeof(LightDotsGraphic));
            var rect = (RectTransform)dots.transform;
            symbol=transform.Find("Library symbol").GetComponent<Image>();
            rect.SetParent(symbol.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(34, 5);
            counter=dots.GetComponent<LightDotsGraphic>();
            counter.raycastTarget=false;
        }
        public void AwaitArrival()=>counter.canvasRenderer.SetAlpha(0);
        public void Introduce(CognitionBoard board)=>StartCoroutine(Arrival(board));
        IEnumerator Arrival(CognitionBoard board)
        {
            var panel=board.Panel;
            var canvas=(RectTransform)symbol.canvas.rootCanvas.transform;
            try{
                while(panel.IsOpen&&!panel.FullyOpen)yield return null;
                if(!panel.IsOpen)yield break;
                traveller=new GameObject("Light transfer",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
                traveller.transform.SetParent(canvas,false);
                traveller.sprite=symbol.sprite;
                traveller.preserveAspect=true;
                traveller.raycastTarget=false;
                var rect=traveller.rectTransform;
                rect.sizeDelta=symbol.rectTransform.rect.size;
                halo=new GameObject("Light halo",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
                halo.transform.SetParent(rect,false);
                halo.rectTransform.sizeDelta=rect.sizeDelta*1.9f;
                halo.raycastTarget=false;
                haloTexture=new Texture2D(32,32,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
                for(int y=0;y<32;y++)for(int x=0;x<32;x++){
                    float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).sqrMagnitude;
                    haloTexture.SetPixel(x,y,new Color(1,1,1,Mathf.Exp(-5*radius)*Mathf.Clamp01(1-radius)));
                }
                haloTexture.Apply(false,true);
                halo.texture=haloTexture;
                for(float t=0;t<1.25f;t+=Time.unscaledDeltaTime){
                    if(!panel.IsOpen||board.IsDragging)yield break;
                    float fly=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,1f,t));
                    rect.position=Vector3.Lerp(canvas.TransformPoint(canvas.rect.center),symbol.rectTransform.TransformPoint(symbol.rectTransform.rect.center),fly);
                    rect.localScale=Vector3.one*Mathf.Lerp(2.5f,1,fly);
                    float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.95f,1.25f,t));
                    traveller.canvasRenderer.SetAlpha(alpha);
                    halo.canvasRenderer.SetAlpha(alpha*.65f);
                    yield return null;
                }
                traveller.gameObject.SetActive(false);
                for(float t=0;t<.45f;t+=Time.unscaledDeltaTime){
                    if(!panel.IsOpen||board.IsDragging)yield break;
                    counter.canvasRenderer.SetAlpha(Mathf.SmoothStep(0,1,t/.45f));
                    yield return null;
                }
                counter.canvasRenderer.SetAlpha(1);
                // Use the period prompt's ring as a moving, non-interactive drag hint.
                guide=new GameObject("Light drag prompt",typeof(RectTransform),typeof(PeriodGlyph)).GetComponent<PeriodGlyph>();
                guide.transform.SetParent(canvas,false);
                guide.ring=true;guide.diameter=board.punctuation.diameter*2;
                guide.raycastTarget=false;
                float elapsed=0;
                while(panel.IsOpen&&!board.IsDragging&&PlayerInstance.Remaining==PlayerInstance.Limit){
                    float u=Mathf.Clamp01(Mathf.Repeat(elapsed,2.1f)-.45f);
                    guide.rectTransform.position=Vector3.Lerp(symbol.rectTransform.TransformPoint(symbol.rectTransform.rect.center),
                        canvas.TransformPoint(canvas.rect.center+Vector2.down*canvas.rect.height*.16f),Mathf.SmoothStep(0,1,u));
                    guide.canvasRenderer.SetAlpha(Mathf.Min(u*8,(1-u)*8,1)*.8f);
                    elapsed+=Time.unscaledDeltaTime;
                    yield return null;
                }
            }finally{Finish();}
        }
        void Finish()
        {
            if(counter)counter.canvasRenderer.SetAlpha(1);
            if(traveller)Destroy(traveller.gameObject);
            if(guide)Destroy(guide.gameObject);
            if(haloTexture)Destroy(haloTexture);
        }
        void OnDestroy()=>Finish();
    }

    internal sealed class LightDotsGraphic : MaskableGraphic
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            PlayerInstance.CountChanged += SetVerticesDirty;
        }
        protected override void OnDisable()
        {
            PlayerInstance.CountChanged -= SetVerticesDirty;
            base.OnDisable();
        }
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = rectTransform.rect;
            for (int i = 0; i < PlayerInstance.Limit; i++)
            {
                var center = new Vector2(rect.xMin + (i + .5f) * rect.width / PlayerInstance.Limit, rect.center.y);
                Color32 tint = new Color(1, 1, 1, i < PlayerInstance.Remaining ? .95f : .23f);
                int first = vertices.currentVertCount;
                vertices.AddVert(center, tint, Vector2.zero);
                for (int j = 0; j < 12; j++)
                {
                    float angle = j * Mathf.PI / 6;
                    vertices.AddVert(center + 2f * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), tint, Vector2.zero);
                }
                for (int j = 0; j < 12; j++) vertices.AddTriangle(first, first + 1 + j, first + 1 + (j + 1) % 12);
            }
        }
    }
}
