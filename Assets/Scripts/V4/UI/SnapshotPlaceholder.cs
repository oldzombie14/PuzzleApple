using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V4
{
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class SnapshotPlaceholder : MonoBehaviour
    {
        public Image stroke;
        [Min(.5f)] public float thickness = 1.5f;
        void OnEnable() => Resize();
        void OnRectTransformDimensionsChange() => Resize();
        void OnValidate() => Resize();
        void Resize()
        {
            if (!stroke) return;
            var size = ((RectTransform)transform).rect.size;
            stroke.rectTransform.sizeDelta = new Vector2(size.magnitude, thickness);
            stroke.rectTransform.anchoredPosition = Vector2.zero;
            stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(size.y, size.x) * Mathf.Rad2Deg);
        }
    }
}
