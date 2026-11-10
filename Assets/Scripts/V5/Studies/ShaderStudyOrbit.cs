using UnityEngine;
using UnityEngine.EventSystems;
namespace PuzzleApple.V5.Studies
{
    public sealed class ShaderStudyOrbit : MonoBehaviour,IDragHandler,IScrollHandler
    {
        public ShaderStudyController study;
        public void OnDrag(PointerEventData e)=>study.Orbit(e.delta);
        public void OnScroll(PointerEventData e)=>study.Zoom(e.scrollDelta.y);
    }
}
