using UnityEngine;

namespace PuzzleApple.V5
{
    // Shared inner boundary: wall thickness must not hide half of a corner's ink.
    [ExecuteAlways]
    public sealed class OuterRoomEdges : MonoBehaviour
    {
        public Renderer[] surfaces;
        MaterialPropertyBlock block;

        void LateUpdate()
        {
            if (surfaces == null || surfaces.Length != 6) return;
            if (block == null) block = new MaterialPropertyBlock();
            Vector3 centre = Vector3.zero;
            foreach (var surface in surfaces) { if (!surface) return; centre += surface.bounds.center / 6f; }
            Vector3 min = Vector3.one * float.NegativeInfinity, max = Vector3.one * float.PositiveInfinity;
            foreach (var surface in surfaces)
            {
                var b = surface.bounds;
                int axis = b.size.x < b.size.y ? (b.size.x < b.size.z ? 0 : 2) : (b.size.y < b.size.z ? 1 : 2);
                if (b.center[axis] < centre[axis]) min[axis] = b.max[axis];
                else max[axis] = b.min[axis];
            }
            for (int i = 0; i < 3; i++) if (float.IsInfinity(min[i]) || float.IsInfinity(max[i])) return;
            foreach (var surface in surfaces)
            {
                surface.GetPropertyBlock(block);
                var direction=surface.transform.InverseTransformDirection(centre-surface.bounds.center);
                block.SetVector("_RoomShellInward",direction.normalized);
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
