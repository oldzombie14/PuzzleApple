using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PuzzleApple.V3.Cognition;

namespace PuzzleApple.V3
{
    public sealed class LibraryDrag : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler,IInitializePotentialDragHandler
    {
        WordLibrary library;WordDefinition word;bool dragging;
        public void Initialize(WordLibrary owner,WordDefinition definition){library=owner;word=definition;}
        public void OnInitializePotentialDrag(PointerEventData e){e.useDragThreshold=true;library.Scroll.OnInitializePotentialDrag(e);}
        public void OnBeginDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;e.eligibleForClick=false;
            library.Scroll.StopMovement();
            dragging=true;library.BeginLibraryDrag(word,e.position);
        }
        public void OnDrag(PointerEventData e){if(dragging)library.board.MoveDrag(e.position);}
        public void OnEndDrag(PointerEventData e){if(dragging)library.EndLibraryDrag(e.position);dragging=false;}
    }
}
