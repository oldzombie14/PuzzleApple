using PuzzleApple.V5.Cognition;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    public static class SentenceCaption
    {
        public static CanvasGroup Show(Transform parent, CognitionState.Group sentence)
        {
            if(sentence==null)return null;
            var root=new GameObject("Sentence caption",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
            var parentCanvas=parent.GetComponentInParent<Canvas>();
            root.transform.SetParent(parentCanvas?parentCanvas.rootCanvas.transform:parent,false);
            var canvas=root.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            if(parentCanvas)
            {
                canvas.overrideSorting=true;
                var bounds=(RectTransform)root.transform;
                bounds.anchorMin=Vector2.zero;
                bounds.anchorMax=Vector2.one;
                bounds.offsetMin=bounds.offsetMax=Vector2.zero;
            }
            canvas.sortingOrder=41;
            var group=root.GetComponent<CanvasGroup>();
            group.blocksRaycasts=false;
            group.interactable=false;
            int count=sentence.Words.Count;
            float frameWidth=count<=2?.54f:.76f;
            var frame=new GameObject("Caption bounds",typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(root.transform,false);
            frame.anchorMin=new Vector2((1-frameWidth)*.5f,.27f);
            frame.anchorMax=Vector2.one-frame.anchorMin;
            frame.offsetMin=frame.offsetMax=Vector2.zero;

            const float gap=.24f;
            float width=gap*(count-1);
            foreach(var word in sentence.Words)width+=InkAspect(word.Definition.Symbol);
            var row=new GameObject("Symbols",typeof(RectTransform),typeof(AspectRatioFitter)).GetComponent<RectTransform>();
            row.SetParent(frame,false);
            const float contentScale=.48f;
            row.localScale=new Vector3(contentScale,contentScale,1);
            var fit=row.GetComponent<AspectRatioFitter>();
            fit.aspectRatio=width;
            fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
            float left=0;
            foreach(var word in sentence.Words)
            {
                var sprite=word.Definition.Symbol;
                float aspect=InkAspect(sprite);
                var image=new GameObject(word.Meaning,typeof(RectTransform),typeof(CenteredSymbolImage)).GetComponent<CenteredSymbolImage>();
                var rect=image.rectTransform;
                rect.SetParent(row,false);
                // Compensate for sprite trim so the visible glyph fills its cell.
                var padding=UnityEngine.Sprites.DataUtility.GetPadding(sprite);
                var size=sprite.rect.size;
                float fullHeight=size.y/Mathf.Max(1,size.y-padding.y-padding.w);
                float fullWidth=fullHeight*size.x/size.y;
                float centre=(left+aspect*.5f)/width;
                rect.anchorMin=new Vector2(centre-fullWidth/(2*width),.5f-fullHeight*.5f);
                rect.anchorMax=new Vector2(centre+fullWidth/(2*width),.5f+fullHeight*.5f);
                rect.offsetMin=rect.offsetMax=Vector2.zero;
                image.sprite=sprite;
                image.preserveAspect=true;
                image.raycastTarget=false;
                left+=aspect+gap;
            }
            return group;
        }

        static float InkAspect(Sprite sprite)
        {
            var padding=UnityEngine.Sprites.DataUtility.GetPadding(sprite);
            var size=sprite.rect.size;
            return Mathf.Max(1,size.x-padding.x-padding.z)/Mathf.Max(1,size.y-padding.y-padding.w);
        }

        public static void Clear(ref CanvasGroup caption)
        {
            if(!caption)return;
            caption.gameObject.SetActive(false);
            Object.Destroy(caption.gameObject);
            caption=null;
        }
    }
}
