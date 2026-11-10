using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5
{
    // One behaviour may cover a region and several moving noun-bearing members.
    public sealed class ObservableBehaviour : MonoBehaviour
    {
        public ObservationLesson lesson;
        public BoxCollider[] regions=new BoxCollider[0];
        public Collider[] members=new Collider[0];
        public bool Available {get;set;}=true;
        CognitionState initialState;
        // The demonstration and player-owned example use the same rule evaluator.
        // Acquisition transfers authority, even when the player's sentence is later split.
        public bool HasEffect(CognitionState playerState,CognitionCatalog catalog,CognitionSignal signal)
        {
            if(!lesson||!lesson.example)return false;
            if(playerState.HasAcquired(lesson.acquisitionId))return playerState.HasEffect(signal);
            if(initialState==null)
            {
                initialState=new CognitionState(catalog);
                initialState.TryAcquireSentence(lesson.acquisitionId,lesson.Sentence);
            }
            return initialState.HasEffect(signal);
        }
        public bool Contains(Collider collider)=>System.Array.IndexOf(members,collider)>=0;
        public bool Hit(Ray ray,float reach,out float distance)
        {
            distance=float.PositiveInfinity;
            if(!isActiveAndEnabled||!Available||!lesson)return false;
            foreach(var region in regions)
            {
                if(!region||!region.enabled||!region.gameObject.activeInHierarchy)continue;
                if((region.ClosestPoint(ray.origin)-ray.origin).sqrMagnitude<.000001f)distance=0;
                else if(region.Raycast(ray,out var hit,reach))distance=Mathf.Min(distance,hit.distance);
            }
            foreach(var member in members)
                if(member&&member.enabled&&member.Raycast(ray,out var hit,reach))distance=Mathf.Min(distance,hit.distance);
            return distance<=reach;
        }
    }
}
