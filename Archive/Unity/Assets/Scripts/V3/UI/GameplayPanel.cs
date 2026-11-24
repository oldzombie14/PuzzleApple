using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V3
{
    // Runs before player and EventSystem updates so Tab takes ownership immediately.
    [DefaultExecutionOrder(-300)]
    public sealed class GameplayPanel : MonoBehaviour
    {
        [SerializeField] FirstPersonController player;
        [SerializeField] RectTransform panel;
        [SerializeField] RectTransform contentRoot;
        [SerializeField] CanvasGroup interaction;
        [SerializeField] Button worldInputBlocker;
        [SerializeField, Min(0)] float slideDuration = 0.25f;
        [SerializeField, Min(0)] float stopDuration = 0.2f;
        [SerializeField, Range(0, 20)] float lookAngle = 2.5f;
        [SerializeField, Range(0, 2)] float idlePitchAngle = 0.35f;
        [SerializeField] UnityEvent<bool> openChanged = new UnityEvent<bool>();
        float progress;
        Vector2 openPosition;

        public bool IsOpen { get; private set; }
        public bool InputBlocked { get; set; }
        public RectTransform ContentRoot => contentRoot;
        public UnityEvent<bool> OpenChanged => openChanged;

        void Awake()
        {
            if (!player) player = FindFirstObjectByType<FirstPersonController>();
            openPosition=panel.anchoredPosition;
            if (worldInputBlocker)
            {
                worldInputBlocker.onClick.AddListener(DismissFromWorld);
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
            float eased = Mathf.SmoothStep(0, 1, progress);
            panel.anchoredPosition = openPosition+Vector2.left*panel.rect.width*(1-eased);
            if (interaction)
            {
                interaction.interactable = IsOpen;
                interaction.blocksRaycasts = IsOpen;
                interaction.alpha = progress > 0 ? 1 : 0;
            }
            if (worldInputBlocker) worldInputBlocker.gameObject.SetActive(IsOpen);
        }

        void OnDestroy(){if(worldInputBlocker)worldInputBlocker.onClick.RemoveListener(DismissFromWorld);}
        void OnDisable()
        {
            if(!Application.isPlaying)return;
            if (IsOpen) SetOpen(false);
            progress = 0;
            ApplyVisuals();
        }
    }
}
