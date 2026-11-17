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
        [Tooltip("How much of the light the room's surfaces bounce back. White V5 rooms are 1; dark walls bounce less.")]
        [Range(0,1)] public float reflectance=1;
        [Header("Readout")]
        [SerializeField] int lightsInside;
        [SerializeField] float lightOutput;
        [SerializeField] float surfaceArea;
        [SerializeField] float fill;
        [SerializeField] Color tint=Color.white;

        public float Fill=>fill;
        // Output-weighted colour of the lights inside, brightest channel normalised to 1.
        public Color Tint=>tint;
        public Color FillColor{get{var c=tint*fill;c.a=1;return c;}}
        public float LightOutput=>lightOutput;
        // A room that is still being revealed (the folding chamber) passes from its closed fill to its own fill.
        [System.NonSerialized] public float openness=1;
        [System.NonSerialized] public Color closedFill=Color.black;
        public Color SurfaceFill=>Color.Lerp(closedFill,FillColor,openness);
        static readonly List<RoomLightBalance> rooms=new List<RoomLightBalance>();
        static Light[] lights=System.Array.Empty<Light>();
        static int lightsFrame=-1, publishFrame=-1;
        const int MaxRooms=4;
        const float SurfaceMargin=.25f;
        static readonly Vector4[] fillMin=new Vector4[MaxRooms], fillMax=new Vector4[MaxRooms], fillColour=new Vector4[MaxRooms];
        static readonly int FillMinId=Shader.PropertyToID("_RoomFillMin"), FillMaxId=Shader.PropertyToID("_RoomFillMax"),
            FillColourId=Shader.PropertyToID("_RoomFillColor"), FillCountId=Shader.PropertyToID("_RoomFillCount");

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
        void OnDisable(){rooms.Remove(this);Publish(true);}
        void Update(){Measure();Publish(false);}

        // World Surface lights each room's walls, floor and ceiling with that room's own fill,
        // so a room looks the same from a corridor or doorway as from inside.
        static void Publish(bool force)
        {
            if(!force&&Application.isPlaying&&publishFrame==Time.frameCount)return;
            publishFrame=Time.frameCount;
            int count=0;
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            foreach(var room in rooms)
            {
                if(count>=MaxRooms)break;
                if(!room||!room.isActiveAndEnabled||!room.style)continue;
                var t=room.transform;var b=room.interior;
                Vector3 lo=Vector3.positiveInfinity,hi=Vector3.negativeInfinity;
                for(int corner=0;corner<8;corner++)
                {
                    var local=new Vector3((corner&1)==0?b.min.x:b.max.x,(corner&2)==0?b.min.y:b.max.y,(corner&4)==0?b.min.z:b.max.z);
                    var world=t.TransformPoint(local);
                    lo=Vector3.Min(lo,world);hi=Vector3.Max(hi,world);
                }
                lo-=Vector3.one*SurfaceMargin;hi+=Vector3.one*SurfaceMargin;
                var c=room.SurfaceFill;
                if(linear)c=c.linear;
                fillMin[count]=lo;fillMax[count]=hi;fillColour[count]=new Vector4(c.r,c.g,c.b,1);
                count++;
            }
            Shader.SetGlobalVectorArray(FillMinId,fillMin);
            Shader.SetGlobalVectorArray(FillMaxId,fillMax);
            Shader.SetGlobalVectorArray(FillColourId,fillColour);
            Shader.SetGlobalFloat(FillCountId,count);
        }

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
            Color colour=Color.black;
            foreach(var light in lights)
            {
                if(!light||!Contains(light.transform.position))continue;
                float output=RoomLightStyle.Output(light);
                if(output<=0)continue;
                lightsInside++;lightOutput+=output;colour+=light.color*output;
            }
            float peak=colour.maxColorComponent;
            if(peak>0){tint=colour/peak;tint.a=1;}
            fill=style?style.Fill(lightOutput*reflectance,surfaceArea):0;
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
