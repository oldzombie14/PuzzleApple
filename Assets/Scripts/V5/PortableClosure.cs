using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class PortableClosure : MonoBehaviour
    {
        public WorldEntry entry;
        public bool Isolated {get;private set;}
        readonly Dictionary<Renderer,bool> renderers=new Dictionary<Renderer,bool>();
        readonly Dictionary<Light,int> lights=new Dictionary<Light,int>();
        BoxCollider support;
        ClosureRegion Region()
        {
            ClosureRegion best=null;
            foreach(var region in FindObjectsByType<ClosureRegion>(FindObjectsSortMode.None))
                if(region.isActiveAndEnabled&&region.Contains(entry.player.transform.position)&&(!best||region.priority>best.priority))best=region;
            return best;
        }
        public bool CanFold
        {
            get{if(entry.outdoor&&entry.outdoor.InChapter)return entry.outdoor.Chapter.box.Opened;
                var region=Region();return region?region.allowBox:!entry.journey||!entry.journey.InCorridor||entry.journey.allowCorridorClosure;}
        }
        public void Prepare()
        {
            var region=Region();
            bool chapter=entry.outdoor&&entry.outdoor.InChapter;
            bool isolate=chapter||(region?region.isolateEnvironment:entry.journey&&entry.journey.BeyondChamber);
            if(Isolated||!isolate)return;
            Isolated=true;entry.room.PortablePlacement=true;
            if(chapter)SetChapterInterior(false);
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if(!r.transform.IsChildOf(entry.room.transform)&&!r.transform.IsChildOf(entry.player.transform)&&r is MeshRenderer)
                {renderers[r]=r.forceRenderingOff;r.forceRenderingOff=true;}
            foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(!l.transform.IsChildOf(entry.room.transform)){lights[l]=l.cullingMask;l.cullingMask=0;}
            if(!support)
            {
                var go=new GameObject("Portable box support");go.transform.SetParent(entry.room.transform,false);
                support=go.AddComponent<BoxCollider>();support.size=new Vector3(5.88f,.2f,5.88f);
                support.center=new Vector3(0,-entry.room.closedOffset-.1f,0);
            }
            support.enabled=true;
        }
        public void Restore()
        {
            if(!Isolated)return;
            foreach(var pair in renderers)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;
            foreach(var pair in lights)if(pair.Key)pair.Key.cullingMask=pair.Value;
            renderers.Clear();lights.Clear();support.enabled=false;
            Isolated=false;entry.room.PortablePlacement=false;
            if(entry.outdoor&&entry.outdoor.InChapter)SetChapterInterior(true);
        }
        void SetChapterInterior(bool chapter)
        {
            entry.room.GetComponent<FoldingInterior>().enabled=!chapter;
            entry.outdoor.Chapter.box.GetComponent<FoldingInterior>().enabled=chapter;
        }
        void OnDestroy(){Restore();}
    }
}
