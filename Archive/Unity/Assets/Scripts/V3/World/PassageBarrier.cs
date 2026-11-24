using UnityEngine;

namespace PuzzleApple.V3
{
    // One native collider blocks both physical passage and the normal interaction ray.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PassageBarrier : MonoBehaviour
    {
        public bool WaitingForClearance { get; private set; }
        BoxCollider barrier;
        Collider protectedBody;
        bool wasPassable;
        public void SetPassable(bool passable,Collider body)
        {
            if(!barrier)barrier=GetComponent<BoxCollider>();
            if(wasPassable&&!passable&&body)
            {
                // Relocking must not create a collider inside someone already crossing.
                barrier.enabled=true;
                var area=barrier.bounds;area.Expand(.12f);
                if(area.Intersects(body.bounds))
                {protectedBody=body;WaitingForClearance=true;Physics.IgnoreCollision(barrier,body,true);}
            }
            if(WaitingForClearance)
            {
                var area=barrier.bounds;area.Expand(.12f);
                if(passable||!protectedBody||!area.Intersects(protectedBody.bounds))ClearProtection();
            }
            barrier.enabled=!passable;wasPassable=passable;
        }
        void ClearProtection()
        {
            if(barrier&&protectedBody)Physics.IgnoreCollision(barrier,protectedBody,false);
            protectedBody=null;WaitingForClearance=false;
        }
        void OnDisable()=>ClearProtection();
    }
}
