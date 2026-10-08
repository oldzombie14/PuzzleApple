using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V3.Cognition
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
