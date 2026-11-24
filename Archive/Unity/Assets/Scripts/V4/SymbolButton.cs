using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V4
{
    // Preserve native Button input/navigation while tinting only the symbol ink.
    public sealed class SymbolButton : Button
    {
        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            Color tint = colors.normalColor;
            if (state == SelectionState.Highlighted || state == SelectionState.Selected) tint = colors.highlightedColor;
            else if (state == SelectionState.Pressed) tint = colors.pressedColor;
            else if (state == SelectionState.Disabled) tint = colors.disabledColor;
            foreach (var image in GetComponentsInChildren<Image>(true))
                if (image.sprite) image.CrossFadeColor(tint, instant ? 0 : colors.fadeDuration, true, true);
        }
    }
}
