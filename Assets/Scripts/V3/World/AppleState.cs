using UnityEngine;

namespace PuzzleApple.V3
{
    // A reversible physical-object state; the apple keeps its name and motion.
    public sealed class AppleState : MonoBehaviour
    {
        public Renderer[] wholeSurfaces=new Renderer[0];
        public Collider[] wholeColliders=new Collider[0];
        public GameObject core;
        public bool IsCore { get; private set; }
        public static float WeightFor(bool core)=>core?.25f:1f;
        public float Weight=>WeightFor(IsCore);
        public void SetCore(bool value)
        {
            if(!core)return;
            IsCore=value;
            foreach(var r in wholeSurfaces)if(r)r.enabled=!value;
            foreach(var c in wholeColliders)if(c)c.enabled=!value;
            core.SetActive(value);
        }
    }
}
