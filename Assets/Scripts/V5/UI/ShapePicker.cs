using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    public sealed class ShapePicker : MonoBehaviour
    {
        public bool Open=>overlay && overlay.activeSelf;
        GameObject overlay;
        RectTransform box;
        CanvasGroup fade;
        float elapsed;
        Action<InstanceShape> choose;
        GameplayPanel panel;
        public RectTransform Bounds=>box;
        public void Show(GameplayPanel parent,Vector2 screen,InstanceShape[] shapes,Action<InstanceShape> selected){
            Close();panel=parent;choose=selected;
            var canvas=parent.ContentRoot.GetComponentInParent<Canvas>().rootCanvas;
            overlay=new GameObject("Shape selection",typeof(RectTransform),typeof(Image),typeof(Button));
            var back=(RectTransform)overlay.transform;back.SetParent(canvas.transform,false);Stretch(back);
            overlay.GetComponent<Image>().color=Color.clear;
            var cancel=overlay.GetComponent<Button>();cancel.transition=Selectable.Transition.None;cancel.onClick.AddListener(Close);
            box=Rect("Shape choices",back);box.gameObject.AddComponent<Image>().color=Color.black;
            var swallow=box.gameObject.AddComponent<Button>();swallow.transition=Selectable.Transition.None;
            fade=box.gameObject.AddComponent<CanvasGroup>();
            var list=new System.Collections.Generic.List<InstanceShape>();
            foreach(var shape in shapes)if(!list.Contains(shape))list.Add(shape);
            options=list.ToArray();
            const float cell=56,gap=8,pad=9;
            float width=options.Length*(cell+gap)-gap+2*pad;
            box.sizeDelta=new Vector2(width,cell+2*pad);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(back,screen,null,out var point);
            var area=back.rect;
            float fit=Mathf.Min(1,area.width/width,area.height/box.sizeDelta.y);
            maxScale=fit;
            box.anchoredPosition=new Vector2(Mathf.Clamp(point.x,area.xMin+width*fit*.5f,area.xMax-width*fit*.5f),Mathf.Clamp(point.y+(cell*.5f+20)*fit,area.yMin+box.sizeDelta.y*fit*.5f,area.yMax-box.sizeDelta.y*fit*.5f));
            for(int i=0;i<options.Length;i++){
                var shape=options[i];var tile=Rect(shape.ToString(),box);tile.sizeDelta=Vector2.one*cell;
                tile.anchoredPosition=Vector2.right*(-width*.5f+pad+cell*.5f+i*(cell+gap));
                var border=tile.gameObject.AddComponent<Image>();border.color=Color.white;
                var button=tile.gameObject.AddComponent<Button>();button.targetGraphic=border;
                var colors=button.colors;colors.normalColor=new Color(.68f,.68f,.68f);colors.highlightedColor=Color.white;button.colors=colors;
                var fill=Rect("Inset",tile);fill.sizeDelta=Vector2.one*(cell-3);fill.gameObject.AddComponent<Image>().color=Color.black;
                var icon=Rect("Outline",fill);Stretch(icon);var graphic=icon.gameObject.AddComponent<ShapeOutline>();graphic.shape=shape;graphic.raycastTarget=false;
                button.onClick.AddListener(()=>Select(shape));
            }
            elapsed=0;fade.alpha=0;box.localScale=Vector3.one*fit*.92f;
        }
        float maxScale;
        InstanceShape[] options=Array.Empty<InstanceShape>();
        static RectTransform Rect(string name,RectTransform parent){var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);return r;}
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        public void Select(InstanceShape shape){if(!Open || Array.IndexOf(options,shape)<0)return;var callback=choose;Close();callback?.Invoke(shape);}
        public void Close(){if(overlay){overlay.SetActive(false);Destroy(overlay);}overlay=null;choose=null;}
        void Update(){
            if(!Open)return;
            var keyboard=Keyboard.current;var mouse=Mouse.current;
            if(!panel.IsOpen || panel.InputBlocked || keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame || mouse!=null&&mouse.rightButton.wasPressedThisFrame){Close();return;}
            elapsed+=Time.unscaledDeltaTime;float t=Mathf.SmoothStep(0,1,elapsed/.12f);fade.alpha=t;box.localScale=Vector3.one*(maxScale*Mathf.Lerp(.92f,1,t));
            if(keyboard==null)return;
            int index=keyboard.digit1Key.wasPressedThisFrame?0:keyboard.digit2Key.wasPressedThisFrame?1:keyboard.digit3Key.wasPressedThisFrame?2:-1;
            if(index>=0 && index<options.Length)Select(options[index]);
        }
        public void Failed(Vector2 screen){
            var canvas=panel.ContentRoot.GetComponentInParent<Canvas>().rootCanvas;
            var r=Rect("Unavailable placement",(RectTransform)canvas.transform);r.sizeDelta=Vector2.one*32;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,screen,null,out var p);r.anchoredPosition=p;
            var ring=r.gameObject.AddComponent<ShapeOutline>();ring.shape=InstanceShape.Sphere;ring.raycastTarget=false;
            StartCoroutine(Ripple(r,ring));
        }
        System.Collections.IEnumerator Ripple(RectTransform r,ShapeOutline ring){
            for(float t=0;t<.35f;t+=Time.unscaledDeltaTime){r.localScale=Vector3.one*(.7f+2*t);ring.color=new Color(.6f,.6f,.6f,1-t/.35f);yield return null;}Destroy(r.gameObject);
        }
        void OnDisable()=>Close();
    }
}
