using System.Collections.Generic;
using UnityEngine;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    public sealed class MembraneDoor : MonoBehaviour
    {
        public Space inside;
        public RoomLightBalance interior;
        public ChapterRoom room;
        static readonly HashSet<MembraneDoor> active=new HashSet<MembraneDoor>();
        void OnEnable()=>active.Add(this);
        void OnDisable()=>active.Remove(this);
        void OnTriggerEnter(Collider other)=>Notify(other);
        void OnTriggerExit(Collider other)=>Notify(other);
        void Notify(Collider other){var player=other.GetComponent<FirstPersonController>();if(player&&player.journey)player.journey.entry.sentenceRuntime.ReevaluateScope();}
        public bool Contains(Vector3 feet)=>interior && interior.Contains(feet+Vector3.up*.3f);
        public static MembraneDoor At(Vector3 feet,Space chapter){
            MembraneDoor result=null;float size=float.PositiveInfinity;
            foreach(var gate in active)if(gate && (gate.inside&chapter)!=0 && gate.Contains(feet)){
                var bounds=Vector3.Scale(gate.interior.interior.size,gate.interior.transform.lossyScale);
                float volume=Mathf.Abs(bounds.x*bounds.y*bounds.z);
                if(volume<size){size=volume;result=gate;}
            }
            return result;
        }
        public static Space Region(Vector3 feet,Space chapter){
            var containing=At(feet,chapter);if(containing)return containing.inside&chapter;
            foreach(var gate in active)if(gate)chapter&=~gate.inside;
            return chapter;
        }
    }
}
