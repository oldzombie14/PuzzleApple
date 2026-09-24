using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PuzzleApple.Cognition
{
    public sealed class CognitionDragHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler
    {
        CognitionBoard board;
        int groupId;
        bool wholeSentence, pressed, began;
        Vector2 pointer;
        public int WordId { get; private set; }
        public void Initialize(CognitionBoard owner, int group, int word, bool isSentenceHandle)
        { board = owner; groupId = group; WordId = word; wholeSentence = isSentenceHandle; }
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = true; }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressed = true; began = false; pointer = e.position;
        }
        public void OnBeginDrag(PointerEventData e)
        { pointer = e.position; if (pressed) Begin(); }
        void Begin() { board.BeginDrag(groupId, WordId, wholeSentence, pointer); began = board.IsDragging; }
        public void OnDrag(PointerEventData e) { pointer = e.position; if (began) board.MoveDrag(pointer); }
        public void OnPointerUp(PointerEventData e) { Finish(e); }
        public void OnEndDrag(PointerEventData e) { Finish(e); }
        void Finish(PointerEventData e)
        { if (began) board.EndDrag(e.position); pressed = began = false; }
    }
}
