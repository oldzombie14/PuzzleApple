using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V3
{
    // A room's lights, reflected light and visible fixtures share one reveal value.
    public sealed class RoomLighting : MonoBehaviour
    {
        public Light[] lights;
        public ReflectionProbe[] reflections;
        public Renderer[] fixtures;
        public Transform darknessBoundary;
        public float fadeSeconds=1.2f;
        public float Amount { get; private set; }
        public bool Revealed { get; private set; }
        float[] intensity,reflectionIntensity;
        readonly Dictionary<Material,Material> darkMaterials=new Dictionary<Material,Material>();
        readonly Dictionary<Material,Material> originals=new Dictionary<Material,Material>();
        public Material Original(Material material)=>material&&originals.TryGetValue(material,out var source)?source:material;
        public Material WithoutEnvironment(Material material)
        {
            if(!material||!material.HasProperty("_EnvironmentReflections"))return material;
            material=Original(material);
            if(darkMaterials.TryGetValue(material,out var dark))return dark;
            dark=new Material(material){name=material.name+" (room lights off)"};
            dark.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");dark.SetFloat("_EnvironmentReflections",0);
            darkMaterials[material]=dark;originals[dark]=material;return dark;
        }
        public float AmbientAt(Vector3 point)=>darknessBoundary&&Vector3.Dot(point-darknessBoundary.position,darknessBoundary.forward)>=0?Amount:1;
        void Awake(){Initialize();Apply(0);}
        void Initialize()
        {
            if(intensity!=null)return;
            intensity=lights.Select(l=>l.intensity).ToArray();
            reflectionIntensity=reflections.Select(p=>p.intensity).ToArray();
        }
        public void Tick(bool visible,float dt)
        {
            Revealed|=visible;
            Initialize();Apply(Mathf.MoveTowards(Amount,Revealed?1:0,dt/Mathf.Max(.01f,fadeSeconds)));
        }
        void Apply(float value)
        {
            Amount=value;
            for(int i=0;i<lights.Length;i++)lights[i].intensity=intensity[i]*Mathf.SmoothStep(0,1,value);
            for(int i=0;i<reflections.Length;i++)reflections[i].intensity=reflectionIntensity[i]*value;
            foreach(var fixture in fixtures)fixture.enabled=value>.02f;
        }
        void OnDestroy(){foreach(var material in darkMaterials.Values)if(material)Destroy(material);}
    }
}
