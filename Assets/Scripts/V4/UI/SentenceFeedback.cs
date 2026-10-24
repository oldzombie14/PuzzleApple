using System.Collections;
using PuzzleApple.V4.Cognition;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V4
{
    // Native line renderers and sprite-based particle systems rendered onto the overlay UI.
    // A dedicated layer/target preserves the existing screen-space overlay and its masks.
    public sealed class SentenceFeedback : MonoBehaviour
    {
        public CognitionBoard board;
        public ParticleSystem sparklePrefab;
        public Material rippleMaterial;
        [Range(0,31)] public int effectLayer=31;
        public float duration=.85f;
        public int StrongCount {get;private set;}
        public int WeakCount {get;private set;}
        Camera effectCamera;
        RawImage output;
        RenderTexture texture;
        Transform stage;
        void Start()
        {
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
        }
        public void Play(int groupId,bool strong)
        {
            if(!stage||!board.Panel.IsOpen)return;
            if(strong)StrongCount++;else WeakCount++;
            StartCoroutine(Pulse(groupId,strong));
        }
        IEnumerator Pulse(int groupId,bool strong)
        {
            var rect=board.GroupRect(groupId);if(!rect)yield break;
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
            if(strong&&sparklePrefab)
            {
                var particles=Instantiate(sparklePrefab,root.transform);
                var shape=particles.shape;shape.scale=new Vector3(rect.rect.width*scale,rect.rect.height*scale*.8f,0);
                var main=particles.main;main.startSize=new ParticleSystem.MinMaxCurve(34*scale,60*scale);
                particles.Play();
            }
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
            {
                var group=board.State.Find(groupId);
                if(!rect||group==null||!group.Effective)break;
                Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                root.transform.localPosition=new Vector3(screen.x-Screen.width*.5f,screen.y-Screen.height*.5f,5);
                for(int i=0;i<lines.Length;i++)
                {
                    float u=Mathf.Clamp01((t-i*.12f)/(duration-i*.12f));
                    float alpha=(strong?.62f:.25f)*Mathf.Sin(Mathf.PI*Mathf.Pow(u,.55f));
                    lines[i].startColor=lines[i].endColor=new Color(.89f,.94f,1,alpha);
                    // Briefly hold the expanded frame before it resumes spreading and fades.
                    float expansion=u<.35f?Mathf.SmoothStep(0,.55f,u/.35f):u<.55f?.55f:Mathf.SmoothStep(.55f,1,(u-.55f)/.45f);
                    float x=(rect.rect.width*.5f+Mathf.Lerp(4,45,expansion))*scale;
                    float y=(rect.rect.height*.5f+Mathf.Lerp(2,35,expansion))*scale;
                    lines[i].SetPosition(0,new Vector3(-x,-y,0));
                    lines[i].SetPosition(1,new Vector3(-x,y,0));
                    lines[i].SetPosition(2,new Vector3(x,y,0));
                    lines[i].SetPosition(3,new Vector3(x,-y,0));
                }
                yield return null;
            }
            Destroy(root);
        }
        void OnDestroy(){if(texture){texture.Release();Destroy(texture);}}
    }
}
