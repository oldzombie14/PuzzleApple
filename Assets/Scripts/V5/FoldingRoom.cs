using UnityEngine;

namespace PuzzleApple.V5
{
    // One reversible timeline: closing samples exactly the opening poses backwards.
    public sealed class FoldingRoom : MonoBehaviour
    {
        [System.Serializable] public struct Panel
        {
            public Transform hinge;
            public Vector3 openEuler;
            [Min(0)] public float durationOverride;
        }
        public Panel[] panels;
        public Collider[] movingColliders;
        [Min(.1f)] public float duration = 4;
        [Min(0)] public float sinkDepth = .35f;
        public float closedOffset = -.08f;
        [Tooltip("Contain the full player capsule while closed, independently of panel collision seams.")]
        public bool containPlayerWhileClosed = true;
        [Tooltip("Allowed room centre XZ bounds, keeping the box inside the outer walls.")]
        public Vector2 centreMin = new Vector2(-13,-13), centreMax = new Vector2(13,13);
        public float Progress { get; private set; }
        public bool PortablePlacement {get;set;}
        public bool Animating => !Mathf.Approximately(Progress, target);
        public bool Opened => Progress >= 1;
        float target;
        Vector3 anchor;
        Bounds closedInterior;
        void Awake() { anchor = transform.position - Vector3.up * closedOffset; Apply(); MeasureClosedInterior(); }
        void MeasureClosedInterior()
        {
            var min=new Vector3(-3,-closedOffset,-3);var max=new Vector3(3,6-closedOffset,3);
            foreach(var collider in movingColliders)
            {
                var box=collider as BoxCollider;if(!box)continue;
                var local=new Bounds(transform.InverseTransformPoint(box.transform.TransformPoint(box.center)),Vector3.zero);
                for(int i=0;i<8;i++)
                {
                    var corner=box.center+Vector3.Scale(box.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    local.Encapsulate(transform.InverseTransformPoint(box.transform.TransformPoint(corner)));
                }
                var size=local.size;
                if(size.x<size.y&&size.x<size.z)
                {if(local.center.x<0)min.x=local.max.x;else max.x=local.min.x;}
                else if(size.z<size.x&&size.z<size.y)
                {if(local.center.z<0)min.z=local.max.z;else max.z=local.min.z;}
                else if(local.center.y>0)max.y=local.min.y;
            }
            closedInterior=new Bounds((min+max)*.5f,max-min);
        }
        public Vector3 ConstrainClosedMovement(CharacterController motor,Vector3 motion)
        {
            if(!containPlayerWhileClosed||target>0||Progress>0)return motion;
            var centre=motor.transform.TransformPoint(motor.center);
            var desired=transform.InverseTransformPoint(centre+motion);
            var scale=transform.lossyScale;var playerScale=motor.transform.lossyScale;
            float radius=(motor.radius+motor.skinWidth)*Mathf.Max(Mathf.Abs(playerScale.x),Mathf.Abs(playerScale.z));
            var extent=new Vector3(radius/Mathf.Abs(scale.x),(motor.height*.5f+motor.skinWidth)*Mathf.Abs(playerScale.y/scale.y),radius/Mathf.Abs(scale.z));
            var min=closedInterior.min+extent;var max=closedInterior.max-extent;
            // Leave room for the controller's downward skin-width probe; clamping at
            // its resting height would make isGrounded false and suppress jumping.
            min.y-=2*motor.skinWidth*Mathf.Abs(playerScale.y/scale.y)+.01f;
            for(int axis=0;axis<3;axis++)desired[axis]=Mathf.Clamp(desired[axis],min[axis],Mathf.Max(min[axis],max[axis]));
            return transform.TransformPoint(desired)-centre;
        }
        public void Open() { target = 1; SetCollision(false); }
        public void HoldCurrentPose(){target=Progress;SetCollision(Progress<=0);}
        public void CloseAt(Vector3 feet)
        {
            // Reposition only while hidden below the floor; never teleport the player.
            if (Progress >= 1) anchor = PortablePlacement?feet:new Vector3(Mathf.Clamp(feet.x,centreMin.x,centreMax.x),
                feet.y,Mathf.Clamp(feet.z,centreMin.y,centreMax.y));
            target = 0; SetCollision(false); Apply();
        }
        void Update()
        {
            if (!Animating) return;
            Progress = Mathf.MoveTowards(Progress, target, Time.deltaTime / TotalDuration());
            if (!Animating) Progress = target;
            Apply();
            if (!Animating) SetCollision(Progress <= 0);
        }
        float TotalDuration()
        {
            float total = Mathf.Max(.1f, duration);
            foreach (var p in panels) total = Mathf.Max(total, p.durationOverride);
            return total;
        }
        void Apply()
        {
            float time = Progress * TotalDuration();
            foreach (var p in panels)
            {
                if (!p.hinge) continue;
                float t = Mathf.SmoothStep(0, 1, time / Mathf.Max(.1f, p.durationOverride > 0 ? p.durationOverride : duration));
                float eased = 1 - (1-t)*(1-t);
                p.hinge.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(p.openEuler), eased);
            }
            transform.position = anchor + Vector3.up * (closedOffset - sinkDepth * Mathf.SmoothStep(0, 1, Progress));
        }
        void SetCollision(bool value) { foreach (var c in movingColliders) if(c) c.enabled = value; }
    }
}
