using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V5
{
    // Measures the real lights inside one room and turns them into that room's diffuse fill.
    // Every room uses the same RoomLightStyle, so more or larger lamps brighten a room and a
    // larger room spreads the same light thinner.
    [ExecuteAlways]
    public sealed class RoomLightBalance : MonoBehaviour
    {
        public RoomLightStyle style;
        [Tooltip("Room interior, in this object's local space.")]
        public Bounds interior=new Bounds(Vector3.zero,Vector3.one*10);
        [Tooltip("The fill fades in over this distance as the viewer approaches from outside.")]
        [Min(.01f)] public float blendDistance=2;
        [Header("Readout")]
        [SerializeField] int lightsInside;
        [SerializeField] float lightOutput;
        [SerializeField] float surfaceArea;
        [SerializeField] float fill;

        public float Fill=>fill;
        public float LightOutput=>lightOutput;
        static readonly List<RoomLightBalance> rooms=new List<RoomLightBalance>();
        static Light[] lights=System.Array.Empty<Light>();
        static int lightsFrame=-1;

        public static RoomLightBalance Create(Transform parent,string name,Vector3 centre,Vector3 size,RoomLightStyle style)
        {
            var go=new GameObject(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=centre;
            var room=go.AddComponent<RoomLightBalance>();
            room.style=style;
            room.interior=new Bounds(Vector3.zero,size);
            room.Measure();
            return room;
        }

        void OnEnable(){if(!rooms.Contains(this))rooms.Add(this);}
        void OnDisable(){rooms.Remove(this);}
        void Update(){Measure();}

        Vector3 WorldSize=>Vector3.Scale(interior.size,transform.lossyScale);
        public bool Contains(Vector3 world)=>interior.Contains(transform.InverseTransformPoint(world));
        public float Weight(Vector3 world)
        {
            var local=transform.InverseTransformPoint(world);
            var outside=Vector3.Scale(Vector3.Max(Vector3.zero,Vector3.Max(interior.min-local,local-interior.max)),transform.lossyScale);
            return 1-Mathf.SmoothStep(0,1,outside.magnitude/blendDistance);
        }

        public void Measure()
        {
            if(lightsFrame!=Time.frameCount)
            {
                lights=FindObjectsByType<Light>(FindObjectsSortMode.None);
                lightsFrame=Time.frameCount;
            }
            var size=WorldSize;
            surfaceArea=2*(size.x*size.y+size.y*size.z+size.x*size.z);
            lightsInside=0;lightOutput=0;
            foreach(var light in lights)
            {
                if(!light||!Contains(light.transform.position))continue;
                float output=RoomLightStyle.Output(light);
                if(output<=0)continue;
                lightsInside++;lightOutput+=output;
            }
            fill=style?style.Fill(lightOutput,surfaceArea):0;
        }

        // Fill seen from a viewpoint: the containing room's fill, blended across doorways.
        public static bool FillAt(Vector3 view,out float result,out float weight)
        {
            float sum=0;result=0;weight=0;
            foreach(var room in rooms)
            {
                if(!room||!room.style)continue;
                float w=room.Weight(view);
                if(w<=0)continue;
                sum+=w;result+=w*room.fill;weight=Mathf.Max(weight,w);
            }
            if(sum<=0)return false;
            result/=sum;
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.color=new Color(1,.85f,.4f,.8f);
            Gizmos.DrawWireCube(interior.center,interior.size);
        }
    }
}
