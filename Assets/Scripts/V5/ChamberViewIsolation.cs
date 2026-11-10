using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V5
{
    // A finite return-facing plane cannot isolate near-clipped or grazing views.
    // Hide the original room's renderers after crossing its inner wall plane,
    // without changing geometry, collision, collection layers or return traversal.
    [DefaultExecutionOrder(600)]
    public sealed class ChamberViewIsolation : MonoBehaviour
    {
        public PassageJourney journey;
        readonly Dictionary<Renderer,bool> original=new Dictionary<Renderer,bool>();
        bool captured;
        void LateUpdate()
        {
            if(!journey||!journey.Root)return;
            var entry=journey.entry;
            if(!captured)
            {
                foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    if(r.gameObject.scene==entry.gameObject.scene&&!r.transform.IsChildOf(journey.Root)&&
                        !r.transform.IsChildOf(entry.player.transform)&&!r.GetComponentInParent<SentenceFeedback>())
                        original[r]=r.forceRenderingOff;
                captured=true;
            }
            bool portable=entry.portableClosure&&entry.portableClosure.Isolated;
            foreach(var pair in original)
            {
                if(!pair.Key)continue;
                bool box=pair.Key.transform.IsChildOf(entry.room.transform);
                if(portable&&!box)continue; // PortableClosure owns environment isolation here.
                pair.Key.forceRenderingOff=pair.Value||(journey.ViewBeyondChamber&&!(portable&&box));
            }
        }
        void OnDisable()
        {
            foreach(var pair in original)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;
            original.Clear();captured=false;
        }
    }
}
