using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PuzzleApple.Cognition
{
    [DefaultExecutionOrder(-100)]
    public sealed class CognitionBoard : MonoBehaviour
    {
        [SerializeField] GameplayPanel panel;
        [SerializeField] CognitionCatalog catalog;
        [SerializeField] Font chalkFont;
        [SerializeField, Min(18)] int wordSize = 38;
        [SerializeField, Min(10)] float snapDistance = 38;
        [SerializeField, Min(12)] float wordGap = 18;
        [SerializeField, Range(0, 20)] float wordFloatAmplitude = 8;
        public CognitionState State { get; private set; }
        public CognitionCatalog Catalog => catalog;
        public GameplayPanel Panel => panel;
        public Font ChalkFont => chalkFont;
        public RectTransform Surface { get; private set; }
        public bool IsDragging => drag != null;

        sealed class View
        {
            public CognitionState.Group Group;
            public RectTransform Root, Mask;
            public RectTransform EraseGhost, EraseMask;
            public float EraseWidth;
            public RectTransform NewScratch;
            public float ScratchWidth;
            public CanvasGroup Fade;
            public readonly List<Text> Words = new List<Text>();
            public readonly List<float> Offsets = new List<float>();
            public readonly List<float> Widths = new List<float>();
            public Vector2 Position, DisplayPosition;
            public float Width, Height, AnimationStart;
            public bool Rewrite;
            public int Scratches;
            public bool Recognized;
            public bool Conflicted;
            public string Signature;
            public float FontScale = 1;
            public float TextCenterY;
            public float TextCenterX;
            public float TextStart, TextEnd;
            public float InkStart, InkEnd;
            public RectTransform LeadingHandle;
        }
        sealed class Drag
        {
            public int GroupId, WordId;
            public bool Whole, WasIndependent;
            public Vector2 Point, GrabOffset;
            public RectTransform Ghost;
            public Transform GhostHandle;
        }
        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        Drag drag;
        int snapTargetId = -1;
        CognitionSnapDot snapHint;
        Image previewBullet;
        const float SentenceIndent = 48;
        const float ConflictAlpha = .4f;
        static readonly Color ChalkColor = new Color(.94f,.94f,.88f);
        Vector2 lastSurfaceSize;
        bool centerFirstWord;

        void Awake()
        {
            if (!panel) panel = GetComponent<GameplayPanel>();
            if (!chalkFont) chalkFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            State = new CognitionState(catalog);
        }
        void Start()
        {
            Surface = CognitionUI.Rect("Cognition workspace", panel.ContentRoot);
            Surface.anchorMin = Vector2.zero; Surface.anchorMax = Vector2.one;
            Surface.offsetMin = new Vector2(24, 24); Surface.offsetMax = new Vector2(-24,-24);
            Surface.pivot = new Vector2(0, 1);
            Surface.gameObject.AddComponent<RectMask2D>();
            snapHint = CognitionUI.Rect("Snap preview", Surface).gameObject.AddComponent<CognitionSnapDot>();
            snapHint.color = ChalkColor;
            snapHint.raycastTarget = false;
            snapHint.gameObject.SetActive(false);
            previewBullet = CognitionUI.Image("Preview sentence bullet", Surface,new Color(.94f,.94f,.88f));
            previewBullet.gameObject.SetActive(false);
            State.Changed += Refresh;
            panel.OpenChanged.AddListener(OnPanelChanged);
            lastSurfaceSize = Surface.rect.size;
            Refresh(CognitionChange.Add);
        }
        void OnDestroy()
        {
            if (State != null) State.Changed -= Refresh;
            if (panel) panel.OpenChanged.RemoveListener(OnPanelChanged);
        }
        void OnPanelChanged(bool open) { if (!open) CancelDrag(); }
        bool CanFit(string sentence)
        {
            // Keep joined groups legible instead of silently clipping them off the board.
            var tokens = CognitionState.Tokenize(sentence);
            if (tokens.Length > 12 || tokens.Any(t => t.Length > 32)) return false;
            var generator = new TextGenerator();
            var settings = new TextGenerationSettings { font = chalkFont, fontSize = wordSize, scaleFactor = 1,
                fontStyle = FontStyle.Normal, lineSpacing = 1, horizontalOverflow = HorizontalWrapMode.Overflow,
                verticalOverflow = VerticalWrapMode.Overflow, generateOutOfBounds = true };
            return generator.GetPreferredWidth(string.Join(" ", tokens) + ".", settings) <= (Surface ? Surface.rect.width : 590) * 1.65f;
        }

        void Refresh(CognitionChange kind)
        {
            if (!Surface) return;
            var previous = views.Values.ToArray();
            if (State.Groups.Count != 1) centerFirstWord = false;
            else if (previous.Length == 0 && State.Groups[0].Independent) centerFirstWord = true;
            var wordPositions = new Dictionary<int, Vector2>();
            foreach (var v in previous)
                for (int i = 0; i < v.Words.Count; i++)
                {
                    // Word IDs are stored on the view, because the model has already committed.
                    var handle = v.Words[i].GetComponent<CognitionDragHandle>();
                    wordPositions[handle.WordId] = v.Position + new Vector2(v.Offsets[i], 0);
                }
            var next = new Dictionary<int, View>();
            foreach (var group in State.Groups)
            {
                views.TryGetValue(group.Id, out var old);
                string signature = string.Join(" ", group.Words.Select(w => w.Id));
                bool changed = old == null || old.Signature != signature || old.Recognized != group.Recognized || old.Conflicted != group.Conflicted;
                if (!changed) { next[group.Id] = old; continue; }
                Vector2 position = old != null ? old.Position : wordPositions.TryGetValue(group.Words[0].Id, out var p) ? p : NewPosition(next.Count);
                bool rewrite = group.Recognized && old != null && !old.Recognized && old.Words.Count > 1;
                int scratches = group.Independent || group.Recognized ? 0 : (old != null ? old.Scratches : 0) + 1;
                var view = BuildView(group, position, scratches, rewrite, signature);
                if (rewrite)
                {
                    view.EraseGhost = Instantiate(old.Root, view.Root);
                    view.EraseGhost.name = "Previous chalk - erase right to left";
                    view.EraseGhost.anchoredPosition = Vector2.zero;
                    var fade = view.EraseGhost.GetComponent<CanvasGroup>(); fade.alpha = 1; fade.blocksRaycasts = false;
                    view.EraseMask = (RectTransform)view.EraseGhost.GetChild(0);
                    view.EraseWidth = view.EraseMask.sizeDelta.x;
                    foreach (var handle in view.EraseGhost.GetComponentsInChildren<CognitionDragHandle>()) Destroy(handle);
                    foreach (var text in view.EraseGhost.GetComponentsInChildren<Text>()) text.color = new Color(.94f,.94f,.88f);
                    view.Mask.sizeDelta = new Vector2(0,56);
                }
                next[group.Id] = view;
            }
            foreach (var old in previous)
                if (!next.TryGetValue(old.Group.Id, out var same) || same != old)
                { old.Root.name = "Retired group"; old.Root.gameObject.SetActive(false); Destroy(old.Root.gameObject); }
            views.Clear(); foreach (var pair in next) views.Add(pair.Key, pair.Value);
            if (centerFirstWord)
            {
                var first = views.Values.Single();
                first.Position = first.DisplayPosition = new Vector2(Surface.rect.width * .5f - first.TextCenterX,
                    Surface.rect.height * .5f - first.TextCenterY);
                Place(first);
            }
            foreach (var v in views.Values) v.Position = Clamp(v.Position, v.Width, v.Height);
            if (kind == CognitionChange.Add)
                foreach (var v in views.Values) v.Position = FindFree(v, v.Position);
            if (snapHint) snapHint.transform.SetAsLastSibling();
        }
        View BuildView(CognitionState.Group group, Vector2 position, int scratches, bool rewrite, string signature)
        {
            var v = new View { Group = group, Position = position, DisplayPosition = position,
                Scratches = scratches, Rewrite = rewrite, AnimationStart = Time.unscaledTime,
                Recognized = group.Recognized, Conflicted = group.Conflicted, Signature = signature };
            v.Root = CognitionUI.Rect("Group " + group.Id, Surface);
            v.Fade = v.Root.gameObject.AddComponent<CanvasGroup>();
            v.Fade.alpha = group.Conflicted ? ConflictAlpha : 1;
            v.Mask = CognitionUI.Rect("Chalk reveal", v.Root);
            v.Mask.gameObject.AddComponent<RectMask2D>();
            float textStart = group.Independent ? 0 : SentenceIndent;
            float x = textStart;
            foreach (var word in group.Words)
            {
                var text = CognitionUI.Text("Word " + word.Id, v.Mask, chalkFont, word.Text, wordSize, new Color(.94f,.94f,.88f));
                text.raycastTarget = true;
                float width = Mathf.Max(24, text.preferredWidth + 10);
                v.Words.Add(text); v.Offsets.Add(x); v.Widths.Add(width);
                CognitionUI.Place(text.rectTransform, x, 0, width, 56);
                var handle = text.gameObject.AddComponent<CognitionDragHandle>(); handle.Initialize(this, group.Id, word.Id, false);
                x += width + wordGap;
            }
            float textEnd = x - wordGap;
            x = textEnd;
            v.FontScale = Mathf.Min(1, (Surface.rect.width - 8) / Mathf.Max(1, x));
            v.Width = x * v.FontScale; v.Height = 56 * v.FontScale;
            v.Mask.localScale = Vector3.one * v.FontScale;
            v.Mask.sizeDelta = new Vector2(x, 56);
            v.Root.sizeDelta = new Vector2(v.Width, v.Height);
            // Center on the actual glyphs, not the padded 56-unit text rectangle.
            float glyphMin = float.PositiveInfinity, glyphMax = float.NegativeInfinity;
            float glyphMinX = float.PositiveInfinity, glyphMaxX = float.NegativeInfinity;
            foreach (var text in v.Words)
            {
                var generator = text.cachedTextGenerator;
                generator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size));
                var vertices = generator.verts;
                for (int q = 0; q + 3 < vertices.Count; q += 4)
                {
                    if (Mathf.Approximately(vertices[q].position.y, vertices[q + 2].position.y)) continue;
                    for (int j = 0; j < 4; j++)
                    {
                        float y = -vertices[q + j].position.y / text.pixelsPerUnit;
                        glyphMin = Mathf.Min(glyphMin, y); glyphMax = Mathf.Max(glyphMax, y);
                        float gx = text.rectTransform.anchoredPosition.x + vertices[q + j].position.x / text.pixelsPerUnit;
                        glyphMinX = Mathf.Min(glyphMinX, gx); glyphMaxX = Mathf.Max(glyphMaxX, gx);
                    }
                }
            }
            float textCenter = float.IsInfinity(glyphMin) ? 28 : (glyphMin + glyphMax) * .5f;
            v.TextCenterY = textCenter * v.FontScale;
            v.TextCenterX = (float.IsInfinity(glyphMinX) ? x * .5f : (glyphMinX + glyphMaxX) * .5f) * v.FontScale;
            v.InkStart = (float.IsInfinity(glyphMinX) ? textStart : glyphMinX) * v.FontScale;
            v.InkEnd = (float.IsInfinity(glyphMaxX) ? textEnd : glyphMaxX) * v.FontScale;
            v.TextStart = textStart * v.FontScale;
            v.TextEnd = textEnd * v.FontScale;
            if (!group.Independent)
            {
                var hit = CognitionUI.Image("Sentence handle", v.Mask, Color.clear);
                hit.raycastTarget = true;
                v.LeadingHandle = hit.rectTransform;
                CognitionUI.Place(hit.rectTransform,0,0,SentenceIndent,56);
                var square = CognitionUI.Image("Square", hit.transform,ChalkColor);
                CognitionUI.Place(square.rectTransform,6,textCenter-5,10,10);
                hit.gameObject.AddComponent<CognitionDragHandle>().Initialize(this,group.Id,-1,true);
            }
            for (int i = 0; i < v.Offsets.Count; i++) { v.Offsets[i] *= v.FontScale; v.Widths[i] *= v.FontScale; }
            // Preserve additional scratches; each has a slightly different slope and placement.
            for (int i = 0; i < scratches; i++)
            {
                var line = CognitionUI.Rect("Scratch " + i, v.Mask).gameObject.AddComponent<CognitionChalkStroke>();
                line.color = new Color(.94f,.94f,.88f,.88f);
                line.raycastTarget = false;
                line.Seed = group.Id * 19 + i * 31;
                float offset = i == 0 ? 0 : ((i + 1) / 2 % 3 + 1) * (i % 2 == 0 ? 1 : -1);
                float angle = i == 0 ? 0 : (i % 2 == 0 ? .6f : -.6f);
                float strokeWidth = textEnd - textStart - 6;
                float centerCorrection = Mathf.Sin(angle * Mathf.Deg2Rad) * strokeWidth * .5f;
                CognitionUI.Place(line.rectTransform, textStart + 1, textCenter + offset - 2 + centerCorrection, strokeWidth, 4);
                line.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                if (i == scratches - 1) { v.NewScratch = line.rectTransform; v.ScratchWidth = strokeWidth; }
            }
            Place(v);
            return v;
        }
        Vector2 NewPosition(int count) => new Vector2(18 + (count % 2) * 55, 24 + (count % 7) * 82);
        void Update()
        {
            if (!Surface) return;
            if ((lastSurfaceSize - Surface.rect.size).sqrMagnitude > 1 && drag == null)
            {
                lastSurfaceSize = Surface.rect.size;
                // Rebuild sizing while preserving model identities and positions.
                foreach (var v in views.Values) v.Signature = "resize";
                Refresh(CognitionChange.Layout);
            }
            foreach (var v in views.Values)
            {
                v.Position = Clamp(v.Position, v.Width, v.Height);
                v.DisplayPosition = Vector2.Lerp(v.DisplayPosition, v.Position, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 13));
                Place(v);
                float elapsed = Time.unscaledTime - v.AnimationStart;
                if (v.NewScratch) v.NewScratch.sizeDelta = new Vector2(v.ScratchWidth * Mathf.Clamp01(elapsed/.24f),4);
                float visible = 1;
                if (v.Rewrite)
                {
                    visible = Mathf.Clamp01((elapsed - .44f) / .55f);
                    if (v.EraseMask) v.EraseMask.sizeDelta = new Vector2(v.EraseWidth * (1 - Mathf.Clamp01(elapsed/.34f)),56);
                    if (elapsed >= .99f) { v.Rewrite = false; if (v.EraseGhost) Destroy(v.EraseGhost.gameObject); }
                }
                v.Mask.sizeDelta = new Vector2(v.Width / v.FontScale * visible, 56);
            }
            if (drag != null) UpdateDragPreview();
        }
        void Place(View v)
        {
            float bob = v.Group.Independent && v.Group.Id != snapTargetId && (drag == null || drag.GroupId != v.Group.Id)
                ? Mathf.Sin(Time.unscaledTime * 1.25f + v.Group.Id * 1.73f) * wordFloatAmplitude : 0;
            v.Root.anchoredPosition = new Vector2(v.DisplayPosition.x, -v.DisplayPosition.y + bob);
        }
        Vector2 Clamp(Vector2 p, float width, float height) => new Vector2(
            Mathf.Clamp(p.x, 4, Mathf.Max(4, Surface.rect.width - width - 4)),
            Mathf.Clamp(p.y, 8, Mathf.Max(8, Surface.rect.height - height - 8)));
        Rect Bounds(View v, bool displayed = false)
        { var p = displayed ? v.DisplayPosition : v.Position; return new Rect(p, new Vector2(v.Width,v.Height)); }
        Vector2 FindFree(View moving, Vector2 wanted)
        {
            wanted = Clamp(wanted, moving.Width, moving.Height);
            bool Free(Vector2 p)
            {
                var rect = new Rect(p - new Vector2(9, 10), new Vector2(moving.Width + 18, moving.Height + 20));
                return views.Values.All(v => v == moving || !rect.Overlaps(Bounds(v)));
            }
            if (Free(wanted)) return wanted;
            // Choose the nearest vacant patch, then animate there to show repulsion.
            Vector2 best = wanted; float distance = float.MaxValue;
            for (float y = 8; y <= Surface.rect.height - moving.Height - 8; y += 18)
                for (float x = 4; x <= Surface.rect.width - moving.Width - 4; x += 18)
                {
                    var candidate = new Vector2(x,y); float d = (candidate-wanted).sqrMagnitude;
                    if (d < distance && Free(candidate)) { distance = d; best = candidate; }
                }
            return best;
        }
        public Vector2 ScreenToBoard(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Surface, screen, null, out var local);
            return new Vector2(local.x, -local.y);
        }
        public void BeginDrag(int groupId, int wordId, bool whole, Vector2 screen)
        {
            if (!panel.IsOpen || drag != null || !views.TryGetValue(groupId, out var v)) return;
            centerFirstWord = false;
            int index = whole ? 0 : v.Group.Words.ToList().FindIndex(w => w.Id == wordId);
            if (index < 0) return;
            var point = ScreenToBoard(screen);
            var origin = v.DisplayPosition + new Vector2(whole ? 0 : v.Offsets[index], 0);
            RectTransform ghost;
            if (whole)
            {
                ghost = Instantiate(v.Root, Surface);
                ghost.name = "Dragging chalk";
                var fade = ghost.GetComponent<CanvasGroup>(); fade.alpha = v.Conflicted ? ConflictAlpha : 1; fade.blocksRaycasts = false;
                foreach (var handle in ghost.GetComponentsInChildren<CognitionDragHandle>()) Destroy(handle);
                var previous = ghost.Find("Previous chalk - erase right to left"); if (previous) previous.gameObject.SetActive(false);
                ((RectTransform)ghost.GetChild(0)).sizeDelta = new Vector2(v.Width / v.FontScale,56);
            }
            else
            {
                var text = CognitionUI.Text("Dragging chalk", Surface, chalkFont, v.Group.Words[index].Text, wordSize, ChalkColor);
                text.color = new Color(ChalkColor.r, ChalkColor.g, ChalkColor.b, v.Conflicted ? ConflictAlpha : 1);
                ghost = text.rectTransform;
                ghost.localScale = Vector3.one * v.FontScale;
                ghost.sizeDelta = new Vector2(v.Widths[index] / v.FontScale, 56);
            }
            drag = new Drag { GroupId = groupId, WordId = wordId, Whole = whole, WasIndependent = v.Group.Independent,
                Point = point, GrabOffset = point - origin, Ghost = ghost,
                GhostHandle = whole ? ghost.Find("Chalk reveal/Sentence handle") : null };
            if (whole || v.Group.Independent) v.Fade.alpha = .2f * (v.Conflicted ? ConflictAlpha : 1);
            else v.Words[index].color = new Color(.94f,.94f,.88f,.2f);
            MoveDrag(screen);
        }
        public void MoveDrag(Vector2 screen)
        {
            if (drag == null) return;
            drag.Point = ScreenToBoard(screen);
            UpdateDragPreview();
        }
        void UpdateDragPreview()
        {
            var pos = drag.Point - drag.GrabOffset;
            bool canJoin = drag.Whole || drag.WasIndependent;
            bool prepend = false;
            var target = canJoin ? SnapTarget(drag, out prepend) : null;
            var source = views[drag.GroupId];
            drag.Ghost.localScale = Vector3.one * (drag.Whole ? 1 : source.FontScale);
            snapTargetId = target != null ? target.Group.Id : -1;
            foreach (var v in views.Values) if (v.LeadingHandle) v.LeadingHandle.gameObject.SetActive(true);
            if (drag.GhostHandle) drag.GhostHandle.gameObject.SetActive(true);
            previewBullet.gameObject.SetActive(false);
            snapHint.gameObject.SetActive(target != null);
            if (target != null)
            {
                float gap = wordGap * target.FontScale;
                float scaleRatio = target.FontScale / source.FontScale;
                drag.Ghost.localScale = Vector3.one * (drag.Whole ? scaleRatio : target.FontScale);
                float targetX = target.Root.anchoredPosition.x;
                float targetY = -target.Root.anchoredPosition.y;
                // Reserve the insertion gap before placing the dragged word. The dot never sits under its glyphs.
                float targetStart = targetX + target.TextStart;
                pos.x = prepend ? targetStart - source.TextEnd * scaleRatio - gap
                    : targetX + target.TextEnd + gap - (drag.Whole ? source.TextStart * scaleRatio : 0);
                // Align visible letter centers, not padded text rectangles (I and move differ markedly).
                pos.y = targetY + target.TextCenterY - source.TextCenterY * scaleRatio;
                if (prepend && target.LeadingHandle) target.LeadingHandle.gameObject.SetActive(false);
                if (!prepend && drag.GhostHandle) drag.GhostHandle.gameObject.SetActive(false);
                if ((prepend && !drag.Whole) || (!prepend && target.Group.Independent))
                {
                    previewBullet.gameObject.SetActive(true);
                    float firstWordX = prepend ? pos.x : targetStart;
                    CognitionUI.Place(previewBullet.rectTransform, firstWordX - (SentenceIndent - 6) * target.FontScale,
                        targetY + target.TextCenterY - 5 * target.FontScale,10 * target.FontScale,10 * target.FontScale);
                }
                float dotSize = 6 * target.FontScale;
                float leftInk = prepend ? pos.x + source.InkEnd * scaleRatio : targetX + target.InkEnd;
                float rightInk = prepend ? targetX + target.InkStart : pos.x + source.InkStart * scaleRatio;
                float dotX = (leftInk + rightInk) * .5f;
                CognitionUI.Place(snapHint.rectTransform, dotX - dotSize * .5f,
                    targetY + target.TextCenterY - dotSize * .5f, dotSize, dotSize);
            }
            drag.Ghost.anchoredPosition = new Vector2(pos.x, -pos.y);
        }
        View SnapTarget(Drag d, out bool prepend)
        {
            prepend = false; View best = null; float nearest = float.MaxValue;
            foreach (var candidate in views.Values)
            {
                if (candidate.Group.Id == d.GroupId) continue;
                float centerY = candidate.Position.y + candidate.Height * .5f;
                if (Mathf.Abs(d.Point.y - centerY) > candidate.Height * .65f) continue;
                // Whole groups snap by their edges; individual words use the pointer position.
                float left = d.Point.x, right = left;
                if (d.Whole)
                {
                    float origin = d.Point.x - d.GrabOffset.x;
                    left = origin + views[d.GroupId].TextStart; right = origin + views[d.GroupId].TextEnd;
                }
                float before = Mathf.Min(Mathf.Abs(right - candidate.Position.x),
                    Mathf.Abs(right - (candidate.Position.x + candidate.TextStart)));
                float after = Mathf.Min(Mathf.Abs(left - (candidate.Position.x + candidate.TextEnd)),
                    Mathf.Abs(left - (candidate.Position.x + candidate.Width)));
                if (d.Whole)
                    after = Mathf.Min(after, Mathf.Abs(d.Point.x - d.GrabOffset.x - (candidate.Position.x + candidate.TextEnd)));
                float distance = Mathf.Min(before,after);
                if (distance > snapDistance || distance >= nearest) continue;
                if (!CanFit(string.Join(" ", candidate.Group.Words.Concat(views[d.GroupId].Group.Words).Select(w => w.Text)))) continue;
                nearest = distance; best = candidate; prepend = before < after;
            }
            return best;
        }
        public void EndDrag(Vector2 screen)
        {
            if (drag == null) return;
            MoveDrag(screen);
            var d = drag; var source = views[d.GroupId];
            var target = d.Whole || d.WasIndependent ? SnapTarget(d, out var unused) : null;
            bool before = false; if (target != null) SnapTarget(d, out before);
            Vector2 position = d.Point - d.GrabOffset;
            CancelDrag();
            if (!d.Whole && !d.WasIndependent)
            {
                // Release inside the original run exchanges positions, without breaking during preview.
                int wordIndex = source.Group.Words.ToList().FindIndex(w => w.Id == d.WordId);
                var draggedBounds = new Rect(position, new Vector2(source.Widths[wordIndex],source.Height));
                if (Bounds(source, true).Overlaps(draggedBounds))
                {
                    int closest = 0; float best = float.MaxValue;
                    for (int i = 0; i < source.Offsets.Count; i++)
                    {
                        float dist = Mathf.Abs(d.Point.x - source.DisplayPosition.x - source.Offsets[i] - source.Widths[i] * .5f);
                        if (dist < best) { best = dist; closest = i; }
                    }
                    State.Swap(d.GroupId, d.WordId, source.Group.Words[closest].Id);
                }
                else
                {
                    var independent = State.Detach(d.GroupId, d.WordId);
                    var view = views[independent.Id];
                    view.DisplayPosition = Clamp(position, view.Width, view.Height);
                    view.Position = FindFree(view, position);
                    foreach (var v in views.Values.Where(v => v != view)) v.Position = FindFree(v, v.Position);
                }
            }
            else if (target != null)
            {
                int firstOldWord = target.Group.Words[0].Id;
                Vector2 oldFirstWordPosition = target.Position + new Vector2(target.Offsets[0],0);
                State.Join(d.GroupId, target.Group.Id, before);
                var joined = views[target.Group.Id];
                int oldIndex = joined.Group.Words.ToList().FindIndex(w => w.Id == firstOldWord);
                joined.Position = oldFirstWordPosition - new Vector2(joined.Offsets[oldIndex],0);
                joined.Position = FindFree(joined, joined.Position);
            }
            else
            {
                source.DisplayPosition = Clamp(position, source.Width, source.Height);
                source.Position = FindFree(source, position);
            }
        }
        public void CancelDrag()
        {
            if (drag == null) return;
            if (views.TryGetValue(drag.GroupId, out var v))
            { v.Fade.alpha = v.Conflicted ? ConflictAlpha : 1; foreach (var text in v.Words) text.color = new Color(.94f,.94f,.88f); }
            Destroy(drag.Ghost.gameObject); drag = null;
            snapTargetId = -1;
            foreach (var view in views.Values) if (view.LeadingHandle) view.LeadingHandle.gameObject.SetActive(true);
            if (snapHint) snapHint.gameObject.SetActive(false);
            if (previewBullet) previewBullet.gameObject.SetActive(false);
        }
    }
}
