using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple
{
    // Runs before player and EventSystem updates so Tab takes ownership immediately.
    [DefaultExecutionOrder(-300)]
    public sealed class GameplayPanel : MonoBehaviour
    {
        [SerializeField] FirstPersonController player;
        [SerializeField] RectTransform panel;
        [SerializeField] RectTransform contentRoot;
        [SerializeField] Image background;
        [SerializeField] CanvasGroup interaction;
        [SerializeField] Image worldInputBlocker;
        [SerializeField] Color backgroundColor = Color.black;
        [SerializeField, Range(0.1f, 0.8f)] float widthFraction = 1f / 3f;
        [SerializeField, Min(0)] float slideDuration = 0.25f;
        [SerializeField, Min(0)] float stopDuration = 0.2f;
        [SerializeField, Range(0, 20)] float lookAngle = 2.5f;
        [SerializeField, Range(0, 2)] float idlePitchAngle = 0.35f;
        [SerializeField] UnityEvent<bool> openChanged = new UnityEvent<bool>();
        float progress;

        public bool IsOpen { get; private set; }
        public bool InputBlocked { get; set; }
        public RectTransform ContentRoot => contentRoot;
        public UnityEvent<bool> OpenChanged => openChanged;
        public Color BackgroundColor { get => backgroundColor; set { backgroundColor = value; ApplyVisuals(); } }

        void Awake()
        {
            if (!player) player = FindFirstObjectByType<FirstPersonController>();
            if (worldInputBlocker)
            {
                var dismiss = worldInputBlocker.GetComponent<Button>();
                if (!dismiss) dismiss = worldInputBlocker.gameObject.AddComponent<Button>();
                dismiss.transition = Selectable.Transition.None;
                dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
                dismiss.onClick.AddListener(DismissFromWorld);
            }
            ApplyVisuals();
        }

        void DismissFromWorld()
        {
            if (IsOpen && !InputBlocked) SetOpen(false);
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            if (player) player.SetPanelOpen(open, stopDuration, lookAngle, idlePitchAngle);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            ApplyVisuals();
            openChanged.Invoke(open);
        }

        void Update()
        {
            if (!InputBlocked && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) Toggle();
            progress = Mathf.MoveTowards(progress, IsOpen ? 1 : 0,
                slideDuration > 0 ? Time.unscaledDeltaTime / slideDuration : 1);
            ApplyVisuals();
        }

        void ApplyVisuals()
        {
            if (!panel) return;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = new Vector2(widthFraction, 1);
            panel.sizeDelta = Vector2.zero;
            float eased = Mathf.SmoothStep(0, 1, progress);
            var parent = panel.parent as RectTransform;
            panel.anchoredPosition = new Vector2(-(parent ? parent.rect.width : Screen.width) * widthFraction * (1 - eased), 0);
            if (background) background.color = backgroundColor;
            if (interaction)
            {
                interaction.interactable = IsOpen;
                interaction.blocksRaycasts = IsOpen;
                interaction.alpha = progress > 0 ? 1 : 0;
            }
            if (worldInputBlocker) worldInputBlocker.raycastTarget = IsOpen;
        }

        void OnValidate() => ApplyVisuals();
        void OnDisable()
        {
            if (IsOpen) SetOpen(false);
            progress = 0;
            ApplyVisuals();
        }
    }
}
