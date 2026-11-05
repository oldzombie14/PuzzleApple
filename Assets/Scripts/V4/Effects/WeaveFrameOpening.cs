using UnityEngine;

namespace PuzzleApple.V4
{
    // Aperture of a single-piece rectangular frame, measured in the door's XY plane.
    [DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class WeaveFrameOpening : MonoBehaviour
    {
        [Tooltip("Inner width and height as fractions of the outer frame.")]
        public Vector2 openingFraction = new Vector2(.8415f, .8828f);
    }
}
