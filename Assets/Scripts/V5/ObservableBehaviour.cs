using PuzzleApple.V5.Cognition;
using UnityEngine;
using System.Linq;

namespace PuzzleApple.V5
{
    // One behaviour may cover a region and several moving noun-bearing members.
    public sealed class ObservableBehaviour : MonoBehaviour
    {
        public ObservationLesson lesson;
        public bool wordOnly;
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
            return Demonstrates(catalog,signal);
        }
        public bool Demonstrates(CognitionCatalog catalog,CognitionSignal signal) => Demonstration(catalog)?.HasEffect(signal) == true;
        public bool DemonstratesMotion(CognitionCatalog catalog, SentenceMotion motion) =>
            Demonstration(catalog)?.Groups.Any(group => group.Effective && group.Intent?.Motion == motion) == true;

        CognitionState Demonstration(CognitionCatalog catalog){
            if(!lesson || !lesson.example)return null;
            if(initialState==null)
            {
                initialState=new CognitionState(catalog);
                initialState.TryAcquireSentence(lesson.acquisitionId,lesson.Sentence);
            }
            return initialState;
        }
        public Collider MemberAt(Ray ray,float reach,Collider tracked=null,float trackingSlack=0)
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
                bool held=tracked && member==tracked;
                if(miss>(held?Mathf.Tan((aimToleranceDegrees+trackingSlack)*Mathf.Deg2Rad):bestMiss))continue;
                if(!Physics.Raycast(ray.origin,offset.normalized,out var visible,reach,~0,QueryTriggerInteraction.Ignore)||visible.collider!=member)continue;
                if(held)return member;
                best=member;bestMiss=miss;
            }
            return best;
        }
    }
}
