using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    // V4's contrast shader reads the world image, not the UI or a later effect camera.
    // Keep V5 at native resolution with an unstyled output image.
    public sealed class ReticleBackground : MonoBehaviour
    {
        public Camera worldCamera;
        public Image eye;
        public GameObject reticle;
        public Material Contrast { get; private set; }
        RenderTexture frame, previousTarget;
        GameObject output;
        RawImage image;
        PassageJourney journey;
        public float WorldVisibility=>image?image.color.a:1;
        void OnEnable()
        {
            journey=FindFirstObjectByType<PassageJourney>();
            previousTarget=worldCamera.targetTexture;
            Contrast=new Material(eye.material);
            eye.material=Contrast;
            foreach(var stroke in reticle.GetComponentsInChildren<Image>(true))stroke.material=Contrast;
            output=new GameObject("V5 world output",typeof(RectTransform),typeof(Canvas));
            output.transform.SetParent(transform,false);
            var canvas=output.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=-100;
            canvas.targetDisplay=worldCamera.targetDisplay;
            var display=new GameObject("V5 display camera",typeof(Camera));
            display.transform.SetParent(output.transform,false);
            var camera=display.GetComponent<Camera>();
            camera.targetDisplay=worldCamera.targetDisplay;camera.depth=worldCamera.depth+1;
            camera.cullingMask=0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.allowHDR=false;camera.allowMSAA=false;camera.useOcclusionCulling=false;
            var panel=new GameObject("World image",typeof(RectTransform),typeof(RawImage));
            panel.transform.SetParent(output.transform,false);image=panel.GetComponent<RawImage>();
            image.raycastTarget=false;
            image.rectTransform.anchorMin=Vector2.zero;image.rectTransform.anchorMax=Vector2.one;
            image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero;
            Resize();
        }
        void Resize()
        {
            worldCamera.targetTexture=previousTarget;
            if(frame){frame.Release();Destroy(frame);}
            // Preserve lamp radiance through the world-camera post-processing chain.
            // An LDR target clips emission before Bloom; the same final image still feeds UI contrast.
            frame=new RenderTexture(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height),24,RenderTextureFormat.DefaultHDR)
                {name="V5 reticle world",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            frame.Create();worldCamera.targetTexture=frame;image.texture=frame;
            Contrast.SetTexture("_ReticleSceneColor",frame);
            Contrast.SetVector("_ReticleViewportSize",new Vector4(frame.width,frame.height,0,0));
        }
        void LateUpdate()
        {
            if(frame&&(frame.width!=Screen.width||frame.height!=Screen.height))Resize();
            // Fade the whole rendered world (path, door, outlines and background) together.
            // The haze and gameplay UI live on separate canvases and remain independent.
            if(image)image.color=new Color(1,1,1,journey&&journey.enabled?journey.EntrySceneVisibility:1);
        }
        void OnDisable()
        {
            if(worldCamera)worldCamera.targetTexture=previousTarget;
            if(frame){frame.Release();Destroy(frame);}
            if(output)Destroy(output);
            if(Contrast)Destroy(Contrast);
        }
    }
}
