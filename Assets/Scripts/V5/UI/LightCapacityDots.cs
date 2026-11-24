using UnityEngine;
using UnityEngine.UI;
using PuzzleApple.V5.Outdoor;

namespace PuzzleApple.V5
{
    public sealed class LightCapacityDots : MonoBehaviour
    {
        void Awake()
        {
            var dots = new GameObject("Remaining light instances", typeof(RectTransform), typeof(CanvasRenderer), typeof(LightDotsGraphic));
            var rect = (RectTransform)dots.transform;
            rect.SetParent(transform.Find("Library symbol"), false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(34, 5);
            dots.GetComponent<LightDotsGraphic>().raycastTarget = false;
        }
    }

    internal sealed class LightDotsGraphic : MaskableGraphic
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            PlayerInstance.CountChanged += SetVerticesDirty;
        }
        protected override void OnDisable()
        {
            PlayerInstance.CountChanged -= SetVerticesDirty;
            base.OnDisable();
        }
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = rectTransform.rect;
            for (int i = 0; i < PlayerInstance.Limit; i++)
            {
                var center = new Vector2(rect.xMin + (i + .5f) * rect.width / PlayerInstance.Limit, rect.center.y);
                Color32 tint = new Color(1, 1, 1, i < PlayerInstance.Remaining ? .95f : .23f);
                int first = vertices.currentVertCount;
                vertices.AddVert(center, tint, Vector2.zero);
                for (int j = 0; j < 12; j++)
                {
                    float angle = j * Mathf.PI / 6;
                    vertices.AddVert(center + 2f * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), tint, Vector2.zero);
                }
                for (int j = 0; j < 12; j++) vertices.AddTriangle(first, first + 1 + j, first + 1 + (j + 1) % 12);
            }
        }
    }
}
