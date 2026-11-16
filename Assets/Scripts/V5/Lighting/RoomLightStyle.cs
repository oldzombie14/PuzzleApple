using UnityEngine;

namespace PuzzleApple.V5
{
    // One soft-lighting rule shared by every indoor room. Tune it live in Play; the asset keeps the values.
    [CreateAssetMenu(menuName="PuzzleApple/V5/Room Light Style",fileName="RoomLightStyle")]
    public sealed class RoomLightStyle : ScriptableObject
    {
        [Header("Lamp bodies")]
        [Tooltip("Point-light intensity of a lamp body one metre across.")]
        [Min(0)] public float intensityPerMetre=5;
        [Tooltip("How strongly lamp size drives its output. 2 is physical (emitting area); 1 keeps very different lamp sizes readable.")]
        [Range(0,2)] public float sizeExponent=1;
        [Tooltip("Light range = this × √intensity, so brighter lamps reach further at the same falloff.")]
        [Min(0)] public float rangePerRootIntensity=6;
        public Vector2 rangeLimits=new Vector2(3,16);

        [Header("Room fill (diffuse bounce)")]
        [Tooltip("Fill = bounce × total light output ÷ room surface area, softly limited to Max Fill. Spot lights count by their cone's share of a sphere.")]
        [Min(0)] public float bounce=180;
        [Tooltip("Neutral fill with every light off.")]
        [Range(0,.2f)] public float darkFill=.055f;
        [Tooltip("Fill approaches this as rooms get very bright; differences between bright rooms compress near it.")]
        [Range(.1f,2)] public float maxFill=1.2f;

        static RoomLightStyle fallback;
        public static RoomLightStyle Default=>fallback?fallback:fallback=Resources.Load<RoomLightStyle>("V5/RoomLightStyle")??CreateInstance<RoomLightStyle>();

        public float LampIntensity(float size)=>intensityPerMetre*Mathf.Pow(Mathf.Max(0,size),sizeExponent);
        public float LampRange(float intensity)=>Mathf.Clamp(rangePerRootIntensity*Mathf.Sqrt(Mathf.Max(0,intensity)),rangeLimits.x,Mathf.Max(rangeLimits.x,rangeLimits.y));
        public float Fill(float output,float surfaceArea)
        {
            float headroom=Mathf.Max(.001f,maxFill-darkFill);
            float lift=bounce*Mathf.Max(0,output)/Mathf.Max(1,surfaceArea);
            return darkFill+headroom*(1-Mathf.Exp(-lift/headroom));
        }
        public static float Output(Light light)
        {
            if(!light||!light.isActiveAndEnabled||light.intensity<=0)return 0;
            switch(light.type)
            {
                case LightType.Point:return light.intensity;
                case LightType.Spot:return light.intensity*(1-Mathf.Cos(light.spotAngle*.5f*Mathf.Deg2Rad))*.5f;
                default:return 0;
            }
        }
    }
}
