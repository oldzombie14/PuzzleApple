using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5
{
    // One behaviour may cover a region and several moving noun-bearing members.
    public sealed class ObservableBehaviour : MonoBehaviour
    {
        public ObservationLesson lesson;
        [Range(0,3)] public float aimToleranceDegrees=1.5f;
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
        public Collider MemberAt(Ray ray,float reach)
        {
            if(!isActiveAndEnabled||!Available||!lesson)return null;
            if(Physics.Raycast(ray,out var direct,reach,~0,QueryTriggerInteraction.Ignore)&&
               System.Array.IndexOf(members,direct.collider)>=0)return direct.collider;
            Collider best=null;
            float bestMiss=Mathf.Tan(aimToleranceDegrees*Mathf.Deg2Rad);
            foreach(var member in members)
            {
                if(!member||!member.enabled||!member.gameObject.activeInHierarchy)continue;
                Vector3 offset=member.bounds.center-ray.origin;
                float depth=Vector3.Dot(offset,ray.direction);
                if(depth<=0||depth>reach)continue;
                Vector3 aim=ray.GetPoint(depth);
                float miss=Mathf.Max(0,Vector3.Distance(aim,member.bounds.center)-member.bounds.extents.x)/depth;
                if(miss>bestMiss)continue;
                if(!Physics.Raycast(ray.origin,offset.normalized,out var visible,reach,~0,QueryTriggerInteraction.Ignore)||visible.collider!=member)continue;
                best=member;bestMiss=miss;
            }
            return best;
        }
    }
}
