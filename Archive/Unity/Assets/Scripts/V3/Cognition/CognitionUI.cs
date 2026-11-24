using UnityEngine;

namespace PuzzleApple.V3.Cognition
{
    // Positions dynamic sentence elements; static UI is authored in V3Interface.prefab.
    public static class CognitionUI
    {
        public static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
    }
}
