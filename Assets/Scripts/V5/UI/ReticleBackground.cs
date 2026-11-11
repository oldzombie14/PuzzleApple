using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
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
        [SerializeField,Min(4)] float pointerDiameterPixels=12;
        RawImage pointerDot;
        Material pointerInk,solidPointerInk;
        GameplayPanel gameplayPanel;
        WorldEntry entry;
        Vector2 lastPointerPosition;
        float lastPointerMotion;
        public Material Contrast { get; private set; }
        RenderTexture frame, previousTarget;
        GameObject output;
        RawImage image;
        public float WorldVisibility=>image?image.color.a:1;
        void OnEnable()
        {
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
            var pointerCanvas=new GameObject("Game pointer overlay",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
            pointerCanvas.transform.SetParent(output.transform.parent,false);
            pointerCanvas.renderMode=RenderMode.ScreenSpaceOverlay;
            pointerCanvas.sortingOrder=short.MaxValue;
            pointerCanvas.targetDisplay=worldCamera.targetDisplay;
            pointerInk=new Material(Contrast);
            pointerInk.SetFloat("_ScreenPointer",1);
            pointerInk.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusDstColor);
            solidPointerInk=new Material(pointerInk);
            solidPointerInk.SetFloat("_ScreenPointer",2);
            solidPointerInk.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.One);
            gameplayPanel=FindFirstObjectByType<GameplayPanel>();
            entry=FindFirstObjectByType<WorldEntry>();
            pointerDot=new GameObject("Pointer dot",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            pointerDot.transform.SetParent(pointerCanvas.transform,false);
            pointerDot.material=pointerInk;pointerDot.raycastTarget=false;
            Cursor.visible=false;
            Resize();
        }
        void UpdatePointer()
        {
            var mouse=Mouse.current;
            Vector2 pos=mouse!=null?mouse.position.ReadValue():Vector2.zero;
            pointerDot.enabled=mouse!=null&&Application.isFocused&&Cursor.lockState!=CursorLockMode.Locked&&
                new Rect(0,0,Screen.width,Screen.height).Contains(pos);
            if(!pointerDot.enabled){lastPointerPosition=pos;lastPointerMotion=float.NegativeInfinity;return;}
            var curtain=entry?entry.blackBackground:null;
            pointerDot.canvasRenderer.SetAlpha(curtain && curtain.isActiveAndEnabled ? 1-curtain.color.a : 1);
            if(pos!=lastPointerPosition)lastPointerMotion=Time.unscaledTime;
            lastPointerPosition=pos;
            bool moving=Time.unscaledTime-lastPointerMotion<.08f;
            var module=EventSystem.current?EventSystem.current.currentInputModule as InputSystemUIInputModule:null;
            var hit=module?module.GetLastRaycastResult(mouse.deviceId).gameObject:null;
            bool control=hit&&(!gameplayPanel||!gameplayPanel.IsWorldBackdrop(hit))&&
                (ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit)||ExecuteEvents.GetEventHandler<IDragHandler>(hit));
            pointerDot.material=moving&&!control&&!mouse.leftButton.isPressed?pointerInk:solidPointerInk;
            pointerDot.rectTransform.sizeDelta=Vector2.one*Mathf.Round(pointerDiameterPixels);
            pointerDot.rectTransform.position=new Vector2(Mathf.Round(pos.x),Mathf.Round(pos.y));
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
            UpdatePointer();
            if(frame&&(frame.width!=Screen.width||frame.height!=Screen.height))Resize();
        }
        void OnDisable()
        {
            if(pointerDot)Destroy(pointerDot.transform.parent.gameObject);
            if(pointerInk)Destroy(pointerInk);
            if(solidPointerInk)Destroy(solidPointerInk);
            if(worldCamera)worldCamera.targetTexture=previousTarget;
            if(frame){frame.Release();Destroy(frame);}
            if(output)Destroy(output);
            if(Contrast)Destroy(Contrast);
        }
    }
}
