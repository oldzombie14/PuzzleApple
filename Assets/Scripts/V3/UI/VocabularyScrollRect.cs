using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PuzzleApple.V3.Cognition;

namespace PuzzleApple.V3
{
    public sealed class VocabularyScrollRect : ScrollRect
    {
        float freePosition=1;
        [SerializeField,Range(.05f,1)] float maximumThumbSize=.28f;
        protected override void Start()
        {
            base.Start();
            if(verticalScrollbar)verticalScrollbar.onValueChanged.AddListener(OnScrollbarChanged);
        }
        bool synchronizing,wasOverflowing;
        bool Overflowing => content&&viewport&&content.rect.height>viewport.rect.height+.5f;
        public void OnScrollbarChanged(float value)
        {
            if(!synchronizing&&!Overflowing)freePosition=value;
        }
        protected override void SetNormalizedPosition(float value,int axis)
        {
            if(axis==1&&!Overflowing)return;
            base.SetNormalizedPosition(value,axis);
        }
        public override void OnScroll(PointerEventData e)
        {
            if(Overflowing){base.OnScroll(e);return;}
            if(verticalScrollbar)verticalScrollbar.value=Mathf.Clamp01(freePosition+e.scrollDelta.y*.1f);
        }
        public override void OnDrag(PointerEventData e)
        {
            if(Overflowing)base.OnDrag(e);
        }
        public override void Rebuild(CanvasUpdate update)
        {
            synchronizing=true;
            try{base.Rebuild(update);ApplyScrollbar();}finally{synchronizing=false;}
        }
        protected override void LateUpdate()
        {
            synchronizing=true;
            try
            {
                bool overflow=Overflowing;
                if(overflow&&!wasOverflowing){StopMovement();verticalNormalizedPosition=1;}
                base.LateUpdate();ApplyScrollbar();wasOverflowing=overflow;
            }
            finally{synchronizing=false;}
        }
        void ApplyScrollbar()
        {
            if(!verticalScrollbar||!content||!viewport)return;
            verticalScrollbar.size=Mathf.Min(maximumThumbSize,verticalScrollbar.size);
            verticalScrollbar.interactable=true;
            if(!Overflowing)
            {
                StopMovement();var p=content.anchoredPosition;p.y=0;content.anchoredPosition=p;
                verticalScrollbar.SetValueWithoutNotify(freePosition);
            }
        }
        protected override void OnDestroy()
        {
            if(verticalScrollbar)verticalScrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
            base.OnDestroy();
        }
    }
}
