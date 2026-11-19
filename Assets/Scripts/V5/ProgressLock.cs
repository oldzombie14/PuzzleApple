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
        // Progress is permanent. A physical door separately joins its two regions only until fully closed.
        public bool Connected=>Unlocked && (whiteDoor?whiteDoor.DoorProgress>0:
            entranceDoor?entranceDoor.Progress>0:chargeDoor?chargeDoor.Lift>0:true);
        public Space BoundarySpaces=>requiresSpaces|unlocksSpaces;
        public static Space DoorBoundary(Component owner){
            foreach(var gate in active)
                if(gate && (gate.whiteDoor==owner || gate.entranceDoor==owner || gate.chargeDoor==owner))return gate.BoundarySpaces;
            return 0;
        }
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
        public static Space Region(Space chapter,Space playerSpace){
            var accessible=Accessible(chapter);
            var initial=chapter;
            foreach(var gate in active)if(gate)initial&=~gate.unlocksSpaces;
            var result=playerSpace&accessible;
            // An editor teleport behind an unopened progress lock must not unlock that layer.
            if(result==0)result=initial&accessible;
            Space previous;
            do{
                previous=result;
                if((result&initial)!=0)result|=initial&accessible;
                foreach(var gate in active){
                    if(!gate)continue;
                    var inside=gate.unlocksSpaces&accessible;
                    var outside=gate.requiresSpaces&accessible;
                    // Spaces introduced by one lock form a region, independently of door placement.
                    if((result&inside)!=0)result|=inside;
                    if(!gate.Connected)continue;
                    if((result&outside)!=0)result|=inside;
                    if((result&inside)!=0)result|=outside;
                }
            }while(result!=previous);
            return result;
        }
    }
}
