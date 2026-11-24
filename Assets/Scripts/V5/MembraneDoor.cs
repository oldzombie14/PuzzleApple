using System.Collections.Generic;
using UnityEngine;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    public sealed class MembraneDoor : MonoBehaviour
    {
        public SentenceSpace[] interiorSpaces = new SentenceSpace[0];
        SentenceSpaceSet interiorRegion;
        public SentenceSpaceSet InteriorRegion => interiorRegion ??= new SentenceSpaceSet(interiorSpaces);
        public Space inside
        {
            get => SentenceSpaceCatalog.Default.ToLegacy(InteriorRegion);
            set => SetInteriorRegion(SentenceSpaceCatalog.Default.FromLegacy(value));
        }
        public void SetInteriorRegion(SentenceSpaceSet region)
        {
            interiorSpaces = new SentenceSpace[region.Count];
            int index = 0;
            foreach (var space in region) interiorSpaces[index++] = space;
            interiorRegion = region;
        }
        void OnValidate() => interiorRegion = null;
        public RoomLightBalance interior;
        public ChapterRoom room;
        static readonly HashSet<MembraneDoor> active=new HashSet<MembraneDoor>();
        void OnEnable()=>active.Add(this);
        void OnDisable()=>active.Remove(this);
        void OnTriggerEnter(Collider other)=>Notify(other);
        void OnTriggerExit(Collider other)=>Notify(other);
        void Notify(Collider other){var player=other.GetComponent<FirstPersonController>();if(player&&player.journey)player.journey.entry.sentenceRuntime.ReevaluateScope();}
        public bool Contains(Vector3 feet)=>interior && interior.Contains(feet+Vector3.up*.3f);
        public static MembraneDoor At(Vector3 feet,Space chapter)=>At(feet,SentenceSpaceCatalog.Default.FromLegacy(chapter));
        public static MembraneDoor At(Vector3 feet,SentenceSpaceSet chapter){
            MembraneDoor result=null;float size=float.PositiveInfinity;
            foreach(var gate in active)if(gate && gate.InteriorRegion.Overlaps(chapter) && gate.Contains(feet)){
                var bounds=Vector3.Scale(gate.interior.interior.size,gate.interior.transform.lossyScale);
                float volume=Mathf.Abs(bounds.x*bounds.y*bounds.z);
                if(volume<size){size=volume;result=gate;}
            }
            return result;
        }
        public static Space Region(Vector3 feet,Space chapter)=>SentenceSpaceCatalog.Default.ToLegacy(Region(feet,SentenceSpaceCatalog.Default.FromLegacy(chapter)));
        public static SentenceSpaceSet Region(Vector3 feet,SentenceSpaceSet chapter){
            var containing=At(feet,chapter);if(containing)return containing.InteriorRegion.Intersect(chapter);
            var outside = new HashSet<SentenceSpace>(chapter);
            foreach(var gate in active)if(gate)outside.ExceptWith(gate.InteriorRegion);
            return new SentenceSpaceSet(outside);
        }
    }
}
