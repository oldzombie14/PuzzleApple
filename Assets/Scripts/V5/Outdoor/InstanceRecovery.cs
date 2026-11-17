using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using PuzzleApple.V5.Outdoor;

namespace PuzzleApple.V5
{
    public sealed class InstanceRecovery : MonoBehaviour
    {
        [Min(.1f)] public float holdDuration=1;
        public OutdoorPassage passage;
        ObservationRing ring;
        ShapeOutline icon;
        PlayerInstance pressedInstance;
        bool armed,all;
        float elapsed;
        public float Progress=>armed && all?Mathf.Clamp01(elapsed/holdDuration):0;
        void Start(){
            var light=passage.entry.chamberLight;
            ring=Instantiate(light.observer.ring,light.observer.ring.transform.parent);
            ring.name="Recover created lights";ring.raycastTarget=false;ring.gameObject.SetActive(false);
            var rect=(RectTransform)new GameObject("Recover light",typeof(RectTransform),typeof(ShapeOutline)).transform;
            rect.SetParent(light.hoverEye.transform.parent,false);rect.sizeDelta=Vector2.one*38;
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
            icon=rect.GetComponent<ShapeOutline>();icon.returnArrow=true;icon.raycastTarget=false;icon.material=light.hoverEye.material;icon.gameObject.SetActive(false);
        }
        void LateUpdate(){
            var e=passage.entry;var p=e.player;var mouse=Mouse.current;
            var region=passage.DropRegion;
            bool available=passage.DropField && region && region.allowInstantiation && !passage.Transitioning && !e.Busy && !e.ConfirmingExit && !e.chamberLight.Collecting && p.CanInteractWithWorld && mouse!=null;
            PlayerInstance target=null;
            if(available){
                var eye=p.view;
                if(Physics.Raycast(eye.position,eye.forward,out var hit,e.chamberLight.collectionDistance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){
                    target=hit.collider.GetComponentInParent<PlayerInstance>();
                    if(target && (target.Region!=region || target.Recovering))target=null;
                }
                Advance(target,mouse.rightButton.wasPressedThisFrame,mouse.rightButton.isPressed,mouse.rightButton.wasReleasedThisFrame,Time.unscaledDeltaTime);
            }else ResetGesture();
            icon.gameObject.SetActive(target);
            if(target){e.chamberLight.hoverEye.gameObject.SetActive(false);e.chamberLight.reticle.SetActive(false);}
            ring.gameObject.SetActive(Progress>0);ring.material=e.chamberLight.hoverEye.material;ring.SetState(Progress,1);
        }
        void ResetGesture(){armed=false;pressedInstance=null;elapsed=0;all=false;}
        public void Advance(PlayerInstance target,bool pressed,bool held,bool released,float dt){
            if(pressed){
                ResetGesture();pressedInstance=target;all=!target;
                armed=target || passage.DropField.PlayerCount>0;
            }
            if(!armed)return;
            if(passage.DropRegion==null || !passage.DropRegion.allowInstantiation){ResetGesture();return;}
            if(target!=pressedInstance){ResetGesture();return;}
            if(held || released)elapsed+=dt;
            if(all && held && elapsed>=holdDuration){
                var p=passage.entry.player.transform.position;
                var instances=passage.DropField.ActiveLamps.Select(l=>l.GetComponent<PlayerInstance>())
                    .Where(i=>i && i.Region==passage.DropRegion).OrderBy(i=>(i.transform.position-p).sqrMagnitude).ToArray();
                for(int i=0;i<instances.Length;i++)instances[i].Recover(i*.05f);
                ResetGesture();return;
            }
            if(released){
                if(!all && target && elapsed<passage.entry.chamberLight.clickDuration)target.Recover(0);
                ResetGesture();
            }else if(!held)ResetGesture();
        }
    }
}
