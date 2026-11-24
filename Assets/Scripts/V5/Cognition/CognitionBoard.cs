using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V5.Cognition
{
    [DefaultExecutionOrder(-100)]
    public sealed class CognitionBoard : MonoBehaviour
    {
        [SerializeField] GameplayPanel panel;
        [SerializeField] CognitionCatalog catalog;
        [SerializeField] Font chalkFont;
        [Header("UI templates")]
        [SerializeField] CenteredSymbolImage wordPrefab;
        [SerializeField] RectTransform groupPrefab;
        [SerializeField] CognitionChalkStroke strokePrefab;
        [SerializeField, Min(10)] float snapDistance = 38;
        [SerializeField, Min(12)] float wordGap = 18;
        [SerializeField, Range(0, 20)] float wordFloatAmplitude = 8;
        [Header("Experimental punctuation")]
        public bool requirePeriod;
        public PeriodStyle punctuation=new PeriodStyle();
        PeriodTray periods;
        public bool PreviewMirrorCopy {get;private set;}
        public bool PreviewMirrorMove {get;private set;}
        public CognitionState State { get; private set; }
        public CognitionCatalog Catalog => catalog;
        public GameplayPanel Panel => panel;
        public Font ChalkFont => chalkFont;
        [SerializeField] RectTransform surface;
        public RectTransform Surface => surface;
        internal SentenceSelection Selection { get; private set; }
        public bool IsDragging => drag != null || periods && periods.Dragging;
        public RectTransform GroupRect(int id) => views.TryGetValue(id, out var view) ? view.Root : null;
        internal void FadeCompletedSentence(int id,float duration)
        {
            if(!Panel.IsOpen || !views.Remove(id,out var view))return;
            if(Selection.Selected==id)Selection.Clear();
            view.Fade.blocksRaycasts=false;
            if(periods)periods.DetachForFade(id,view.Root);
            StartCoroutine(FadeCompletedView(view,duration));
        }
        System.Collections.IEnumerator FadeCompletedView(View view,float duration)
        {
            for(float t=0;t<duration && Panel.IsOpen;t+=Time.unscaledDeltaTime){
                view.Fade.alpha=1-Mathf.SmoothStep(0,1,t/duration);
                yield return null;
            }
            Destroy(view.Root.gameObject);
        }
        internal float PeriodVisibility(int id)=>views.TryGetValue(id,out var v)?Mathf.Clamp01(v.Fade.alpha/(v.Conflicted?ConflictAlpha:1)):0;
        public void PauseSentence(int id){if(State.Find(id)?.ExecutionLocked==true)return;CancelDrag();State.Unsay(id);}
        public void SetSentenceOpacity(int id,float opacity)
        {
            if (!views.TryGetValue(id,out var view)) return;
            view.Fade.alpha = opacity * (view.Conflicted ? ConflictAlpha : 1);
            view.Fade.blocksRaycasts = opacity >= 1;
        }
        public Rect GroupContentBounds(int id, bool fitToInkHeight = false)
        {
            if (!views.TryGetValue(id, out var view)) return default;
            var rect = view.Root.rect;
            var bounds=new Rect(rect.xMin+view.InkStart,rect.yMin,view.InkEnd-view.InkStart,rect.height);
            if (fitToInkHeight)
            {
                bounds.yMin = view.InkBottom;
                bounds.yMax = view.InkTop;
            }
            if(State.RequirePeriod && !view.Group.Independent){
                var point=view.Root.InverseTransformPoint(PeriodEnd(id));
                float radius=punctuation.diameter*.5f*view.FontScale;
                bounds.xMax=point.x+radius;
                bounds.yMin=Mathf.Min(bounds.yMin,point.y-radius);
                bounds.yMax=Mathf.Max(bounds.yMax,point.y+radius);
            }
            return bounds;
        }
        internal const float HeldRippleExpansion = .55f;
        internal Rect SelectionFrameBounds(int id) => RippleBounds(GroupContentBounds(id), HeldRippleExpansion);
        public static Rect RippleBounds(Rect bounds, float expansion = 0)
        {
            var padding = Vector2.Lerp(new Vector2(4, 2), new Vector2(45, 35), expansion);
            return Rect.MinMaxRect(bounds.xMin-padding.x, bounds.yMin-padding.y,
                bounds.xMax+padding.x, bounds.yMax+padding.y);
        }
        public void RemoveToken(int groupId,int wordId,bool whole=false)
        {
            if (State.Find(groupId)?.ExecutionLocked == true) return;
            CancelDrag();
            if (views.TryGetValue(groupId, out var view))
                foreach (var word in view.Words)
                    if (whole || view.Group.Independent || word.GetComponent<CognitionDragHandle>().WordId == wordId)
                        BeginRemovalEffect(word, view.Fade.alpha);
            State.ReturnToLibrary(groupId,wordId,whole);
        }
        sealed class RemovedWord
        {
            public MaskableGraphic Image;
            public Material Material;
            public Vector3 Position;
            public float Started;
        }
        readonly List<RemovedWord> removedWords = new List<RemovedWord>();
        Shader removalShader;
        internal const float RemovalDuration = .6f;
        static readonly int DissolveProgress = Shader.PropertyToID("_Progress");
        internal void BeginRemovalEffect(MaskableGraphic source, float opacity)
        {
            if (!removalShader) removalShader = Resources.Load<Shader>("V5SymbolDissolve");
            if (!removalShader) return;
            // Erode one intact glyph using broad, soft regions rather than moving cutouts.
            MaskableGraphic image;
            Vector2 inkSize;
            if(source is PeriodGlyph period){
                var dot=new GameObject("Dissolving period",typeof(RectTransform),typeof(PeriodGlyph)).GetComponent<PeriodGlyph>();
                dot.diameter=period.diameter;dot.ring=period.ring;
                image=dot;inkSize=Vector2.one*period.diameter;
            }else{
                var symbol=(Image)source;
                var copy=new GameObject("Dissolving word",typeof(RectTransform),typeof(CenteredSymbolImage)).GetComponent<CenteredSymbolImage>();
                copy.sprite=symbol.sprite;copy.preserveAspect=symbol.preserveAspect;
                image=copy;inkSize=SymbolUI.InkRect(symbol).size;
            }
            var rect = image.rectTransform;
            var original = source.rectTransform;
            rect.SetParent(original, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = original.rect.size;
            rect.anchoredPosition = Vector2.zero;
            rect.SetParent(Surface, true);
            image.raycastTarget = false;
            var color = source.color;
            color.a *= opacity;
            image.color = color;
            var material = new Material(removalShader);
            material.SetVector("_InkSize", new Vector4(Mathf.Max(1, inkSize.x), Mathf.Max(1, inkSize.y), 0, 0));
            material.SetFloat("_Seed", Mathf.Repeat(source.GetInstanceID() * .618034f, 97));
            image.material = material;
            removedWords.Add(new RemovedWord { Image = image, Material = material,
                Position = rect.localPosition, Started = Time.unscaledTime });
        }
        void UpdateRemovalEffects()
        {
            for (int i = removedWords.Count - 1; i >= 0; i--)
            {
                var word = removedWords[i];
                float t = Mathf.Clamp01((Time.unscaledTime - word.Started) / RemovalDuration);
                if (!word.Image || t >= 1)
                {
                    DisposeRemovedWord(word);
                    removedWords.RemoveAt(i);
                    continue;
                }
                float progress = t * t * (3 - 2 * t);
                word.Image.rectTransform.localPosition = word.Position + Vector3.up * (2 * progress);
                word.Material.SetFloat(DissolveProgress, progress);
                // A parent Mask may provide a stencil-derived rendering material.
                word.Image.materialForRendering.SetFloat(DissolveProgress, progress);
            }
        }
        void DisposeRemovedWord(RemovedWord word)
        {
            if (word.Image) Destroy(word.Image.gameObject);
            if (word.Material) Destroy(word.Material);
        }
        void ClearRemovalEffects()
        {
            foreach (var word in removedWords) DisposeRemovedWord(word);
            removedWords.Clear();
        }
        void OnDisable() { ClearRemovalEffects(); }
        public void PlaceGroup(int id, Vector2 screen)
        {
            if (!views.TryGetValue(id, out var v)) return;
            centerFirstWord = false;
            v.Position = v.DisplayPosition = Clamp(ScreenToBoard(screen), v.Width, v.Height); Place(v);
        }
        public void PlaceAcquiredSentence(int id)
        {
            if (!views.TryGetValue(id, out var v)) return;
            centerFirstWord = false;
            float top = Mathf.Min(Mathf.Max(8, Surface.rect.height - v.Height - 8),
                Mathf.Max(WordHeight + 16, Surface.rect.height * .12f));
            var centre = new Vector2(Surface.rect.width * .5f - v.TextCenterX,
                Surface.rect.height * .5f - v.TextCenterY);
            v.Position = v.DisplayPosition = FindFree(v, centre, top);
            Place(v);
        }

        sealed class View
        {
            public CognitionState.Group Group;
            public RectTransform Root, Mask;
            public RectTransform NewScratch;
            public float ScratchWidth;
            public CanvasGroup Fade;
            public readonly List<Image> Words = new List<Image>();
            public readonly List<float> Offsets = new List<float>();
            public readonly List<float> Widths = new List<float>();
            public Vector2 Position, DisplayPosition;
            public float Width, Height, AnimationStart;
            public int Scratches;
            public bool Recognized;
            public bool Conflicted, Spoken;
            public string Signature;
            public float FontScale = 1;
            public float TextCenterY;
            public float TextCenterX;
            public float TextStart, TextEnd;
            public float InkStart, InkEnd;
            public float InkBottom, InkTop;
            public bool FloatIndependently = true;
        }
        sealed class Drag
        {
            public int GroupId, WordId;
            public bool Whole, WasIndependent;
            public Vector2 Point, GrabOffset;
            public RectTransform Ghost;
        }
        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        Drag drag;
        int snapTargetId = -1;
        [SerializeField] CognitionSnapDot snapHint;
        float wordSize => wordPrefab.rectTransform.rect.width;
        float WordHeight => wordPrefab.rectTransform.rect.height;
        internal const float ConflictAlpha = .16f;
        public void ClearWorkspace(){CancelDrag();State.ClearWorkspace();}
        Color ChalkColor => wordPrefab.color;
        Vector2 lastSurfaceSize;
        [SerializeField] RectTransform dragLayer;
        bool centerFirstWord;

        void Awake()
        {
            if (!panel) panel = GetComponent<GameplayPanel>();
            if (!chalkFont) chalkFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            State = new CognitionState(catalog,requirePeriod);
        }
        void Start()
        {
            snapHint.gameObject.SetActive(false);
            if(State.RequirePeriod){periods=gameObject.AddComponent<PeriodTray>();periods.Initialize(this,dragLayer,ChalkColor);}
            Selection = SentenceSelection.Create(this);
            State.Changed += Refresh;
            panel.OpenChanged.AddListener(OnPanelChanged);
            lastSurfaceSize = Surface.rect.size;
            Refresh(CognitionChange.Add);
        }
        void OnDestroy()
        {
            ClearRemovalEffects();
            if (State != null) State.Changed -= Refresh;
            if (panel) panel.OpenChanged.RemoveListener(OnPanelChanged);
        }
        void OnPanelChanged(bool open) { if (!open) { Selection?.Clear(); CancelDrag(); ClearRemovalEffects(); } }
        internal float PeriodScale(int id)=>views[id].FontScale;
        public Vector3 PeriodEnd(int id)
        {
            var v=views[id];
            float inkBottom=v.Words.Min(word=>v.Root.InverseTransformPoint(
                word.rectTransform.TransformPoint(new Vector3(0,SymbolUI.InkRect(word).yMin,0))).y);
            return v.Root.TransformPoint(new Vector3(v.Root.rect.xMin+v.TextEnd+(wordGap+punctuation.diameter*.5f)*v.FontScale,
                inkBottom+punctuation.diameter*.5f*v.FontScale,0));
        }
        bool Dim(CognitionState.Group g)=>g.Conflicted || (State.RequirePeriod && !g.Independent && !g.Spoken);
        bool CanFit(IEnumerable<CognitionState.Word> words)
        {
            var tokens = words.ToArray();
            if (tokens.Length > 12) return false;
            float width = tokens.Sum(w => SymbolUI.Width(w.Definition, wordSize))
                + Mathf.Max(0, tokens.Length - 1) * wordGap;
            return width <= (Surface ? Surface.rect.width : 590) * 1.65f;
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
                    wordPositions[handle.WordId] = new Vector2(v.Root.anchoredPosition.x,
                        -v.Root.anchoredPosition.y) + new Vector2(v.Offsets[i], 0);
                }
            var next = new Dictionary<int, View>();
            foreach (var group in State.Groups)
            {
                views.TryGetValue(group.Id, out var old);
                string signature = string.Join(" ", group.Words.Select(w => w.Id));
                bool changed = old == null || old.Signature != signature || old.Recognized != group.Recognized || old.Conflicted != Dim(group) || old.Spoken!=group.Spoken || State.RequirePeriod && old.Scratches!=group.RejectedAttempts;
                if (!changed) { next[group.Id] = old; continue; }
                Vector2 position = old != null ? old.Position : wordPositions.TryGetValue(group.Words[0].Id, out var p) ? p : NewPosition(next.Count);
                int scratches = State.RequirePeriod ? group.RejectedAttempts : group.Independent || group.Recognized ? 0 : (old != null ? old.Scratches : 0) + 1;
                var view = BuildView(group, position, scratches, signature);
                if (kind == CognitionChange.Split && wordPositions.TryGetValue(group.Words[0].Id, out var firstPosition))
                {
                    // Keep the first remaining glyph in place when its sentence indent disappears.
                    view.FloatIndependently = false;
                    view.Position = view.DisplayPosition = firstPosition - new Vector2(view.Offsets[0], 0);
                    Place(view);
                }
                else if (old != null) view.FloatIndependently = old.FloatIndependently;
                next[group.Id] = view;
            }
            if (drag != null && (!next.TryGetValue(drag.GroupId, out var moving) || views[drag.GroupId] != moving))
                CancelDrag();
            foreach (var old in previous)
                if (!next.TryGetValue(old.Group.Id, out var same) || same != old)
                { old.Root.name = "Retired group"; old.Root.gameObject.SetActive(false); Destroy(old.Root.gameObject); }
            if (previous.Any(v => v.Group.Id == Selection.Selected && (!next.TryGetValue(v.Group.Id, out var sameView) || sameView.Signature != v.Signature))) Selection.Clear();
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
            if(periods)periods.Refresh();
            if (snapHint) snapHint.transform.SetAsLastSibling();
        }
        View BuildView(CognitionState.Group group, Vector2 position, int scratches, string signature)
        {
            var v = new View { Group = group, Position = position, DisplayPosition = position,
                Scratches = scratches, AnimationStart = Time.unscaledTime,
                Recognized = group.Recognized, Conflicted = Dim(group), Spoken=group.Spoken, Signature = signature };
            v.Root = Instantiate(groupPrefab,Surface);v.Root.name="Group "+group.Id;
            v.Fade = v.Root.GetComponent<CanvasGroup>();
            v.Fade.alpha = v.Conflicted ? ConflictAlpha : 1;
            v.Mask = (RectTransform)v.Root.Find("Chalk reveal");
            float textStart = 0;
            float x = textStart;
            foreach (var word in group.Words)
            {
                var text = Instantiate(wordPrefab,v.Mask);text.name="Word "+word.Id;text.sprite=word.Definition.Symbol;
                float width = SymbolUI.Width(word.Definition, wordSize);
                v.Words.Add(text); v.Offsets.Add(x); v.Widths.Add(width);
                CognitionUI.Place(text.rectTransform, x, 0, width, WordHeight);
                text.GetComponent<CognitionDragHandle>().Initialize(this, group.Id, word.Id, false);
                text.GetComponent<WordNoteTarget>().WordId=word.Definition.Id;
                x += width + wordGap;
            }
            float textEnd = x - wordGap;
            x = textEnd+(State.RequirePeriod && !group.Independent?wordGap+wordSize:0);
            v.FontScale = Mathf.Min(1, (Surface.rect.width - 8) / Mathf.Max(1, x));
            v.Width = x * v.FontScale; v.Height = WordHeight * v.FontScale;
            v.Mask.localScale = Vector3.one * v.FontScale;
            v.Mask.sizeDelta = new Vector2(x, WordHeight);
            v.Root.sizeDelta = new Vector2(v.Width, v.Height);
            // All symbols share the same row center; width comes from sprite aspect, not English spelling.
            float textCenter = WordHeight*.5f;
            v.TextCenterY = textCenter * v.FontScale;
            v.TextStart = textStart * v.FontScale;
            v.TextEnd = textEnd * v.FontScale;
            v.InkStart = (v.Offsets[0] + SymbolUI.InkRect(v.Words[0]).xMin) * v.FontScale;
            v.InkEnd = (v.Offsets[v.Offsets.Count - 1] + SymbolUI.InkRect(v.Words[v.Words.Count - 1]).xMax) * v.FontScale;
            v.TextCenterX = (v.InkStart + v.InkEnd) * .5f;
            v.InkBottom = v.Words.Min(word => SymbolUI.InkRect(word).yMin) * v.FontScale;
            v.InkTop = v.Words.Max(word => SymbolUI.InkRect(word).yMax) * v.FontScale;
            for (int i = 0; i < v.Offsets.Count; i++) { v.Offsets[i] *= v.FontScale; v.Widths[i] *= v.FontScale; }
            // Preserve additional scratches; each has a slightly different slope and placement.
            for (int i = 0; i < scratches; i++)
            {
                var line = Instantiate(strokePrefab,v.Mask);line.name="Scratch "+i;
                line.Seed = group.Id * 19 + i * 31;
                float offset = i == 0 ? 0 : ((i + 1) / 2 % 3 + 1) * (i % 2 == 0 ? 1 : -1);
                float angle = i == 0 ? 0 : (i % 2 == 0 ? .6f : -.6f);
                float strokeWidth = (State.RequirePeriod && group.Spoken
                    ? textEnd+wordGap+punctuation.diameter : textEnd-6)-textStart;
                float centerCorrection = Mathf.Sin(angle * Mathf.Deg2Rad) * strokeWidth * .5f;
                float strokeHeight=strokePrefab.rectTransform.rect.height;
                CognitionUI.Place(line.rectTransform, textStart + 1, textCenter + offset - strokeHeight*.5f + centerCorrection, strokeWidth, strokeHeight);
                line.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                if (i == scratches - 1) { v.NewScratch = line.rectTransform; v.ScratchWidth = strokeWidth; }
            }
            Place(v);
            return v;
        }
        Vector2 NewPosition(int count) => new Vector2(18 + (count % 2) * 55, 24 + (count % 7) * 82);
        public void SetPeriodMode(bool enabled)
        {
            requirePeriod=enabled;
            if(State.RequirePeriod==enabled)return;
            CancelDrag();
            if(enabled){periods=gameObject.AddComponent<PeriodTray>();periods.Initialize(this,dragLayer,ChalkColor);}
            else if(periods){periods.Remove();Destroy(periods);periods=null;}
            foreach(var view in views.Values)view.Signature="mode changed";
            State.SetPeriodMode(enabled);
        }
        void Update()
        {
            if(State.RequirePeriod!=requirePeriod)SetPeriodMode(requirePeriod);
            UpdateRemovalEffects();
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
                v.Mask.sizeDelta = new Vector2(v.Width / v.FontScale, WordHeight);
            }
            if (drag != null) UpdateDragPreview();
        }
        void Place(View v)
        {
            float bob = v.FloatIndependently && v.Group.Independent && v.Group.Id != snapTargetId && (drag == null || drag.GroupId != v.Group.Id)
                ? Mathf.Sin(Time.unscaledTime * 1.25f + v.Group.Id * 1.73f) * wordFloatAmplitude : 0;
            v.Root.anchoredPosition = new Vector2(v.DisplayPosition.x, -v.DisplayPosition.y + bob);
        }
        Vector2 Clamp(Vector2 p, float width, float height) => new Vector2(
            Mathf.Clamp(p.x, 4, Mathf.Max(4, Surface.rect.width - width - 4)),
            Mathf.Clamp(p.y, 8, Mathf.Max(8, Surface.rect.height - height - 8)));
        Rect Bounds(View v, bool displayed = false)
        { var p = displayed ? v.DisplayPosition : v.Position; return new Rect(p, new Vector2(v.Width,v.Height)); }
        Vector2 FindFree(View moving, Vector2 wanted, float minimumY = 8)
        {
            wanted = Clamp(wanted, moving.Width, moving.Height);
            wanted.y = Mathf.Max(minimumY, wanted.y);
            bool Free(Vector2 p)
            {
                var rect = new Rect(p - new Vector2(9, 10), new Vector2(moving.Width + 18, moving.Height + 20));
                return views.Values.All(v => v == moving || !rect.Overlaps(Bounds(v)));
            }
            if (Free(wanted)) return wanted;
            // Choose the nearest vacant patch, then animate there to show repulsion.
            Vector2 best = wanted; float distance = float.MaxValue;
            for (float y = minimumY; y <= Surface.rect.height - moving.Height - 8; y += 18)
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
        public void BeginDrag(int groupId, int wordId, bool whole, Vector2 screen,bool fromLibrary=false)
        {
            if (!panel.IsOpen || panel.InputBlocked || IsDragging || !views.TryGetValue(groupId, out var v)) return;
            if (v.Group.ExecutionLocked && !whole) return;
            centerFirstWord = false;
            int index = whole ? 0 : v.Group.Words.ToList().FindIndex(w => w.Id == wordId);
            if (index < 0) return;
            var point = ScreenToBoard(screen);
            dragLayer.SetAsLastSibling();
            var origin = new Vector2(v.Root.anchoredPosition.x, -v.Root.anchoredPosition.y)
                + new Vector2(whole ? 0 : v.Offsets[index], 0);
            RectTransform ghost;
            if (whole)
            {
                ghost = Instantiate(v.Root, dragLayer);
                ghost.name = "Dragging chalk";
                var fade = ghost.GetComponent<CanvasGroup>(); fade.alpha = v.Conflicted ? ConflictAlpha : 1; fade.blocksRaycasts = false;
                foreach (var handle in ghost.GetComponentsInChildren<CognitionDragHandle>()) Destroy(handle);
                ((RectTransform)ghost.GetChild(0)).sizeDelta = new Vector2(v.Width / v.FontScale,WordHeight);
            }
            else
            {
                var text = Instantiate(wordPrefab,dragLayer);text.name="Dragging chalk";text.sprite=v.Group.Words[index].Definition.Symbol;text.raycastTarget=false;
                Destroy(text.GetComponent<CognitionDragHandle>());Destroy(text.GetComponent<WordNoteTarget>());
                text.color = new Color(ChalkColor.r, ChalkColor.g, ChalkColor.b, v.Conflicted ? ConflictAlpha : 1);
                ghost = text.rectTransform;
                ghost.localScale = Vector3.one * v.FontScale;
                ghost.sizeDelta = new Vector2(v.Widths[index] / v.FontScale, WordHeight);
            }
            drag = new Drag { GroupId = groupId, WordId = wordId, Whole = whole, WasIndependent = v.Group.Independent,
                Point = point, GrabOffset = fromLibrary ? new Vector2(v.TextCenterX,v.TextCenterY) : point - origin, Ghost = ghost };
            if (whole || v.Group.Independent) v.Fade.alpha = fromLibrary ? 0 : .2f * (v.Conflicted ? ConflictAlpha : 1);
            else v.Words[index].color = new Color(ChalkColor.r,ChalkColor.g,ChalkColor.b,ChalkColor.a*.2f);
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
            bool canJoin = drag.GroupId != Selection.Selected && (drag.Whole || drag.WasIndependent);
            bool prepend = false;
            var target = canJoin ? SnapTarget(drag, out prepend) : null;
            var source = views[drag.GroupId];
            drag.Ghost.localScale = Vector3.one * (drag.Whole ? 1 : source.FontScale);
            if(target!=null && target.FloatIndependently){
                target.Position=target.DisplayPosition=new Vector2(target.Root.anchoredPosition.x,-target.Root.anchoredPosition.y);
                target.FloatIndependently=false;
            }
            snapTargetId = target != null ? target.Group.Id : -1;
            PreviewMirrorCopy=PreviewMirrorMove=false;
            if(target!=null && target.Group.Independent && source.Group.Independent){
                string first=(prepend?source:target).Group.Words[0].Meaning;
                string last=(prepend?target:source).Group.Words[0].Meaning;
                PreviewMirrorCopy=first=="mirror" && last=="mirror";
                PreviewMirrorMove=first=="mirror" && last=="move";
            }
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
            prepend = false;
            if (views[d.GroupId].Group.ExecutionLocked) return null;
            View best = null; float nearest = float.MaxValue;
            foreach (var candidate in views.Values)
            {
                if (candidate.Group.Id == d.GroupId || candidate.Group.ExecutionLocked) continue;
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
                if (!CanFit(candidate.Group.Words.Concat(views[d.GroupId].Group.Words))) continue;
                nearest = distance; best = candidate; prepend = before < after;
            }
            return best;
        }
        public void EndDrag(Vector2 screen)
        {
            if (drag == null) return;
            MoveDrag(screen);
            var d = drag; var source = views[d.GroupId];
            if (source.Group.ExecutionLocked && !d.Whole) { CancelDrag(); return; }
            var token=source.Group.Words.FirstOrDefault(w=>w.Id==d.WordId);
            if((!d.Whole||source.Group.Independent)&&token!=null)
            {
                var entry=FindFirstObjectByType<WorldEntry>();
                if(!RectTransformUtility.RectangleContainsScreenPoint(panel.ContentRoot,screen))
                {
                    if(entry&&entry.outdoor)entry.outdoor.TryWorldDrop(this,token.Meaning,screen);
                    CancelDrag();State.ReturnToLibrary(d.GroupId,d.WordId,false);return;
                }
            }
            var target = d.GroupId != Selection.Selected && (d.Whole || d.WasIndependent) ? SnapTarget(d, out var unused) : null;
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
                joined.DisplayPosition = joined.Position;
                Place(joined);
            }
            else
            {
                source.DisplayPosition = Clamp(position, source.Width, source.Height);
                source.Position = FindFree(source, position);
            }
        }
        public void CancelDrag()
        {
            if(periods)periods.Cancel();
            PreviewMirrorCopy=PreviewMirrorMove=false;
            if (drag == null) return;
            if (views.TryGetValue(drag.GroupId, out var v))
            { v.Fade.alpha = v.Conflicted ? ConflictAlpha : 1; foreach (var text in v.Words) text.color = ChalkColor; }
            drag.Ghost.gameObject.SetActive(false);
            Destroy(drag.Ghost.gameObject); drag = null;
            snapTargetId = -1;
            if (snapHint) snapHint.gameObject.SetActive(false);
        }
    }
}
