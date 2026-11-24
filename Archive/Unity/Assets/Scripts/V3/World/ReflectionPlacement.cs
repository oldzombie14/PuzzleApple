using UnityEngine;
namespace PuzzleApple.V3
{
    // Optional spatial adaptation. Effects do not need to know which puzzle owns a surface.
    public abstract class ReflectionPlacement : MonoBehaviour
    {
        public abstract Vector3 Map(OpeningObject source,Vector3 point,Vector3 reflected);
    }
}
