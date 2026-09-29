using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V2.Cognition
{
    // One visual vocabulary drives the board, drag ghost and acquisition overlay.
    public static class SymbolUI
    {
        public static float Width(WordDefinition word, float size)
        {
            var sprite = word.Symbol;
            float aspect = sprite ? sprite.rect.width / Mathf.Max(1, sprite.rect.height) : 1;
            return Mathf.Max(20, size * Mathf.Clamp(aspect, .35f, 2.5f));
        }

        public static Image Create(string name, Transform parent, WordDefinition word, Color color)
        {
            var image = CognitionUI.Rect(name, parent).gameObject.AddComponent<CenteredSymbolImage>();
            image.color = color;
            image.raycastTarget = false;
            image.sprite = word.Symbol;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            return image;
        }

        public static Rect InkRect(Image image)
        {
            var rect = image.rectTransform.rect;
            if (!image.sprite) return rect;
            var size = image.sprite.rect.size;
            float scale = Mathf.Min(rect.width / size.x, rect.height / size.y);
            var padding = UnityEngine.Sprites.DataUtility.GetPadding(image.sprite);
            var rendered = (size - new Vector2(padding.x + padding.z, padding.y + padding.w)) * scale;
            return new Rect(rect.center - rendered * .5f, rendered);
        }
    }
}
