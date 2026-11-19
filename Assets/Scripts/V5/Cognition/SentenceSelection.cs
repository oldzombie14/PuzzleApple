using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PuzzleApple.V5.Cognition
{
    public sealed class SentenceSelection : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const float FramePadding = 9f;
        CognitionBoard board;
        RectTransform frame;
        Vector2 start;
        bool boxing, moveSelection;
        public int Selected { get; private set; } = -1;

        internal static SentenceSelection Create(CognitionBoard board)
        {
            var surface = board.Surface;
            var background = surface.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            var selection = surface.gameObject.AddComponent<SentenceSelection>();
            selection.board = board;
            var fill = new GameObject("Sentence selection", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fill.raycastTarget = false;
            fill.color = new Color(1, 1, 1, .008f);
            selection.frame = fill.rectTransform;
            selection.frame.SetParent(surface, false);
            selection.frame.SetAsFirstSibling();
            selection.frame.anchorMin = selection.frame.anchorMax = selection.frame.pivot = new Vector2(0, 1);
            Edge(selection.frame,Vector2.zero,Vector2.up);
            Edge(selection.frame,Vector2.right,Vector2.one);
            Edge(selection.frame,Vector2.zero,Vector2.right);
            Edge(selection.frame,Vector2.up,Vector2.one);
            selection.Clear();
            return selection;
        }

        static void Edge(RectTransform frame,Vector2 start,Vector2 end)
        {
            var line=new GameObject("Border",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            line.raycastTarget=false;
            line.color=new Color(1,1,1,.5f);
            var rect=line.rectTransform;
            rect.SetParent(frame,false);
            rect.anchorMin=start;
            rect.anchorMax=end;
            rect.anchoredPosition=Vector2.zero;
            rect.sizeDelta=start.x==end.x?Vector2.right:Vector2.up;
        }

        internal void Clear()
        {
            Selected = -1;
            boxing = moveSelection = false;
            frame.gameObject.SetActive(false);
        }

        internal void PressWord(int group)
        {
            if (Selected != group) Clear();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || board.Panel.InputBlocked) return;
            start = board.ScreenToBoard(e.position);
            moveSelection = Selected >= 0 && Bounds(Selected).Contains(start);
            if (!moveSelection) Clear();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || board.Panel.InputBlocked) return;
            if (moveSelection) board.BeginDrag(Selected, -1, true, e.pressPosition);
            else boxing = true;
        }

        public void OnDrag(PointerEventData e)
        {
            if (moveSelection) board.MoveDrag(e.position);
            else if (boxing) Draw(Box(e.position));
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (moveSelection) board.EndDrag(e.position);
            else if (boxing && !board.Panel.InputBlocked)
            {
                Rect box = Box(e.position);
                foreach (var group in board.State.Groups)
                {
                    if (group.Independent) continue;
                    Rect bounds = Bounds(group.Id);
                    if (box.xMin > bounds.xMin || box.yMin > bounds.yMin ||
                        box.xMax < bounds.xMax || box.yMax < bounds.yMax) continue;
                    if (Selected >= 0) { Selected = -1; break; }
                    Selected = group.Id;
                }
            }
            boxing = moveSelection = false;
            Refresh();
        }

        Rect Bounds(int id, bool fitToInkHeight = false)
        {
            var root = board.GroupRect(id);
            var ink = board.GroupContentBounds(id, fitToInkHeight);
            Vector2 min = board.Surface.InverseTransformPoint(root.TransformPoint(ink.min));
            Vector2 max = board.Surface.InverseTransformPoint(root.TransformPoint(ink.max));
            return Rect.MinMaxRect(min.x, -max.y, max.x, -min.y);
        }

        Rect Box(Vector2 screen)
        {
            Vector2 end = board.ScreenToBoard(screen);
            return Rect.MinMaxRect(Mathf.Min(start.x,end.x), Mathf.Min(start.y,end.y),
                Mathf.Max(start.x,end.x), Mathf.Max(start.y,end.y));
        }

        void LateUpdate()
        {
            if (!board.Panel.IsOpen || board.Panel.InputBlocked) Clear();
            else if (!boxing) Refresh();
        }

        void Refresh()
        {
            if (Selected >= 0 && board.GroupRect(Selected) && !board.IsDragging)
            {
                Rect ink = Bounds(Selected, true);
                Draw(Rect.MinMaxRect(ink.xMin - FramePadding, ink.yMin - FramePadding,
                    ink.xMax + FramePadding, ink.yMax + FramePadding));
            }
            else frame.gameObject.SetActive(false);
        }

        void Draw(Rect rect)
        {
            frame.gameObject.SetActive(true);
            CognitionUI.Place(frame,rect.x,rect.y,rect.width,rect.height);
        }
    }
}
