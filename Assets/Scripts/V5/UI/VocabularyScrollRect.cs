using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    public sealed class VocabularyScrollRect : ScrollRect
    {
        float freePosition=1;
        [SerializeField,Range(.05f,1)] float maximumThumbSize=.28f;
        [SerializeField,Range(.02f,.3f)] float wheelSmoothTime=.09f;
        [SerializeField,Range(.02f,.3f)] float maximumWheelStep=.1f;
        float wheelTarget,wheelVelocity;
        bool wheelActive;
        protected override void Start()
        {
            base.Start();
            if(verticalScrollbar)verticalScrollbar.onValueChanged.AddListener(OnScrollbarChanged);
        }
        bool synchronizing,wasOverflowing;
        bool Overflowing => content&&viewport&&content.rect.height>viewport.rect.height+.5f;
        public void OnScrollbarChanged(float value)
        {
            if(!synchronizing){wheelActive=false;wheelVelocity=0;}
            if(!synchronizing&&!Overflowing)freePosition=value;
        }
        protected override void SetNormalizedPosition(float value,int axis)
        {
            if(axis==1&&!Overflowing)return;
            base.SetNormalizedPosition(value,axis);
        }
        public override void OnScroll(PointerEventData e)
        {
            if(!IsActive()||!content||!viewport)return;
            Canvas.ForceUpdateCanvases();
            // Input System sends six UI units per wheel notch in this scene.
            // Convert to notches before applying the intended content-pixel step.
            var module=e.currentInputModule as InputSystemUIInputModule;
            float unit=module?Mathf.Abs(module.scrollDeltaPerTick):1;
            if(unit<.0001f)return;
            float ticks=e.scrollDelta.y/unit;
            float overflow=content.rect.height-viewport.rect.height;
            float step=Overflowing?Mathf.Min(scrollSensitivity/overflow,maximumWheelStep):.06f;
            float current=Overflowing?verticalNormalizedPosition:freePosition;
            wheelTarget=Mathf.Clamp01((wheelActive?wheelTarget:current)+ticks*step);
            wheelActive=true;StopMovement();
        }
        public override void OnBeginDrag(PointerEventData e)
        {wheelActive=false;wheelVelocity=0;base.OnBeginDrag(e);}
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
                if(overflow!=wasOverflowing){wheelActive=false;wheelVelocity=0;}
                if(overflow&&!wasOverflowing){StopMovement();verticalNormalizedPosition=1;}
                if(wheelActive)
                {
                    StopMovement();
                    float current=overflow?verticalNormalizedPosition:freePosition;
                    float next=Mathf.SmoothDamp(current,wheelTarget,ref wheelVelocity,wheelSmoothTime,Mathf.Infinity,Time.unscaledDeltaTime);
                    if(Mathf.Abs(next-wheelTarget)<.0001f){next=wheelTarget;wheelActive=false;wheelVelocity=0;}
                    if(overflow)verticalNormalizedPosition=next;else freePosition=next;
                }
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
        protected override void OnDisable()
        {wheelActive=false;wheelVelocity=0;base.OnDisable();}
    }
}
