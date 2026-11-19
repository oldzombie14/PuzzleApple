using System.Collections.Generic;
using UnityEngine;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    // A progress layer is authored separately from the physical placement of its spaces.
    public sealed class ProgressLock : MonoBehaviour
    {
        public Space unlocksSpaces,requiresSpaces;
        public PassageJourney whiteDoor;
        public ChargeDoor chargeDoor;
        public PassageEntranceDoor entranceDoor;
        public WallPassage passageReveal;
        [Range(0,1)] public float passableProgress=.5f;
        public bool Unlocked {get;private set;}
        static readonly HashSet<ProgressLock> active=new HashSet<ProgressLock>();
        void OnEnable()=>active.Add(this);
        void OnDisable()=>active.Remove(this);
        public void Unlock(){Unlocked=true;}
        void Update(){
            if(Unlocked)return;
            if(whiteDoor && whiteDoor.DoorProgress>=passableProgress || entranceDoor && entranceDoor.Progress>=passableProgress || chargeDoor && chargeDoor.Lift>=passableProgress || passageReveal && passageReveal.LightReveal>=passableProgress)Unlock();
        }
        public static Space Accessible(Space chapter){
            Space result=chapter;
            foreach(var gate in active)if(gate)result&=~gate.unlocksSpaces;
            // Explicit prerequisites keep deeper layers behind earlier locks, even during debug teleportation.
            Space previous;
            do{
                previous=result;
                foreach(var gate in active)if(gate && gate.Unlocked && (result&gate.requiresSpaces)==gate.requiresSpaces)result|=gate.unlocksSpaces&chapter;
            }while(previous!=result);
            return result;
        }
    }
}
