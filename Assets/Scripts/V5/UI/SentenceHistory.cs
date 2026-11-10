using PuzzleApple.V5.Cognition;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    // Read-only session history. It never takes ownership of the sentence workspace.
    public sealed class SentenceHistory : MonoBehaviour
    {
        public CognitionBoard board;
        public Button toggle;
        public RectTransform drawer, content;
        public CanvasGroup visibility;
        public ScrollRect scroll;
        public float duration = .12f;
        public bool IsOpen { get; private set; }
        public bool IsVisible => progress > 0;
        float progress;
        int renderedVersion = -1;
        const float RowHeight = 64;
        void Start()
        {
            toggle.onClick.AddListener(Toggle);
            board.State.Changed += Refresh;
            Refresh(CognitionChange.Add);
            Apply();
        }
        void Toggle() { if (board.Panel.IsOpen && !board.Panel.InputBlocked) SetOpen(!IsOpen); }
        public void SetOpen(bool value) { IsOpen = value; Apply(); }
        void Update()
        {
            progress = Mathf.MoveTowards(progress, IsOpen ? 1 : 0, Time.unscaledDeltaTime / Mathf.Max(.001f, duration));
            Apply();
        }
        void Apply()
        {
            float eased = Mathf.SmoothStep(0, 1, progress);
            drawer.localScale = new Vector3(eased, 1, 1);
            visibility.alpha = eased;
            visibility.blocksRaycasts = IsOpen && board.Panel.IsOpen && !board.Panel.InputBlocked;
            visibility.interactable = visibility.blocksRaycasts;
        }
        void Refresh(CognitionChange change)
        {
            if (renderedVersion == board.State.HistoryVersion) return;
            renderedVersion = board.State.HistoryVersion;
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var entries = board.State.History;
            content.sizeDelta = new Vector2(0, entries.Count * RowHeight);
            scroll.StopMovement();
            content.anchoredPosition = Vector2.zero;
            if (scroll.verticalScrollbar) scroll.verticalScrollbar.SetValueWithoutNotify(1);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float width = content.rect.width;
                float cell = Mathf.Min(44, (width - 16) / entry.Words.Length);
                float total = cell * entry.Words.Length;
                var row = Rect("History sentence", content, new Vector2(width, RowHeight), new Vector2(0, -i * RowHeight));
                for (int j = 0; j < entry.Words.Length; j++)
                {
                    var rect = Rect(entry.Words[j].Id, row, new Vector2(cell - 8, 36), new Vector2((width-total)*.5f+j*cell+4, -14));
                    var glyph = rect.gameObject.AddComponent<CenteredSymbolImage>();
                    glyph.sprite = entry.Words[j].Symbol; glyph.preserveAspect = true;
                    glyph.color = new Color(.8f,.8f,.8f); glyph.raycastTarget = false;
                }
                if (!entry.Effective)
                {
                    var line = Rect("Not established", row, new Vector2(total, 2), new Vector2((width-total)*.5f, -31));
                    var ink = line.gameObject.AddComponent<Image>(); ink.color = new Color(.8f,.8f,.8f); ink.raycastTarget = false;
                }
            }
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        void OnDestroy()
        {
            if (toggle) toggle.onClick.RemoveListener(Toggle);
            if (board && board.State != null) board.State.Changed -= Refresh;
        }
    }
}
