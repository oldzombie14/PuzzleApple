using UnityEngine;

namespace PuzzleApple.V5
{
    // Shared inner boundary: wall thickness must not hide half of a corner's ink.
    [ExecuteAlways]
    public sealed class OuterRoomEdges : MonoBehaviour
    {
        public Renderer[] surfaces;
        MaterialPropertyBlock block;

        readonly Vector4[] hits=new Vector4[16];
        readonly float[] impactScales=new float[16];
        int nextHit;
        bool initialized;
        public void Impact(Vector3 point,Vector3 normal,FloorImpactSurface style,float scale=1)
        {
            if(!initialized)
            {
                for(int i=0;i<hits.Length;i++)hits[i].w=-1000;
                initialized=true;
            }
            impactScales[nextHit]=scale;
            hits[nextHit]=new Vector4(point.x,point.y,point.z,Time.time);
            nextHit=(nextHit+1)%hits.Length;
            if(block==null)block=new MaterialPropertyBlock();
            foreach(var surface in surfaces)
            {
                if(!surface)continue;
                surface.GetPropertyBlock(block);
                block.SetVectorArray("_Impacts",hits);
                block.SetFloatArray("_ImpactScales",impactScales);
                block.SetFloat("_RippleDuration",style.rippleDuration);
                block.SetFloat("_RippleSpeed",style.rippleSpeed);
                block.SetFloat("_RingCount",style.ringCount);
                block.SetFloat("_RingSpacing",style.ringSpacing);
                block.SetFloat("_RingWidth",style.ringWidth);
                block.SetFloat("_RippleStrength",style.rippleStrength);
                block.SetVector("_RippleTaper",new Vector4(style.thinningStartsAt,style.thinningDistance,style.distantWidth,0));
                surface.SetPropertyBlock(block);
            }
            style.EmitContactSquares(point,normal);
        }

        static int ThicknessAxis(Bounds bounds)
        {
            var size = bounds.size;
            if (size.x < size.y) return size.x < size.z ? 0 : 2;
            return size.y < size.z ? 1 : 2;
        }

        void LateUpdate()
        {
            if (surfaces == null || surfaces.Length < 6 || !surfaces[0]) return;
            if (block == null) block = new MaterialPropertyBlock();
            // One side may have several coplanar panels around a doorway.
            var shell = surfaces[0].bounds;
            foreach (var surface in surfaces)
            {
                if (!surface) return;
                shell.Encapsulate(surface.bounds);
            }
            Vector3 min = Vector3.one * float.NegativeInfinity, max = Vector3.one * float.PositiveInfinity;
            foreach (var surface in surfaces)
            {
                var b = surface.bounds;
                int axis = ThicknessAxis(b);
                if (b.center[axis] < shell.center[axis]) min[axis] = Mathf.Max(min[axis], b.max[axis]);
                else max[axis] = Mathf.Min(max[axis], b.min[axis]);
            }
            for (int i = 0; i < 3; i++) if (float.IsInfinity(min[i]) || float.IsInfinity(max[i])) return;
            foreach (var surface in surfaces)
            {
                var b = surface.bounds;
                int axis = ThicknessAxis(b);
                var inward = Vector3.zero;
                inward[axis] = b.center[axis] < shell.center[axis] ? 1 : -1;
                surface.GetPropertyBlock(block);
                var localNormal = surface.transform.localToWorldMatrix.transpose.MultiplyVector(inward);
                block.SetVector("_RoomShellInward", localNormal.normalized);
                block.SetFloat("_UseInkRoomBounds", 1);
                block.SetVector("_InkRoomMin", min);
                block.SetVector("_InkRoomMax", max);
                surface.SetPropertyBlock(block);
            }
        }

        void OnDisable()
        {
            if (surfaces == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var surface in surfaces)
            {
                if (!surface) continue;
                surface.GetPropertyBlock(block);
                block.SetFloat("_UseInkRoomBounds", 0); block.SetVector("_RoomShellInward",Vector4.zero);
                surface.SetPropertyBlock(block);
            }
        }
    }
}
