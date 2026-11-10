using UnityEngine;

namespace PuzzleApple.V5.Cognition
{
    // Positions dynamic sentence elements; static UI is authored in V5Interface.prefab.
    public static class CognitionUI
    {
        public static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
    }
}
