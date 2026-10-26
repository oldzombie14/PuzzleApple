using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V4
{
    // Native particles converge onto the existing mesh surfaces; only temporary materials are changed.
    public sealed class DoorAssembly : MonoBehaviour
    {
        public float duration=2.4f;
        public bool Playing {get;private set;}
        public float Progress {get;private set;}
        readonly Dictionary<Renderer,Material[]> originals=new Dictionary<Renderer,Material[]>();
        readonly List<Material> fades=new List<Material>();
        readonly List<Color> colors=new List<Color>();
        ParticleSystem particles;
        struct Point {public Transform owner;public Vector3 local,scatter;}
        public void Begin(Material particleMaterial){if(!Playing)StartCoroutine(Assemble(particleMaterial));}
        IEnumerator Assemble(Material particleMaterial)
        {
            Playing=true;Progress=0;
            var points=new List<Point>();
            var renderers=GetComponentsInChildren<MeshRenderer>(true);
            foreach(var renderer in renderers)
            {
                if(renderer.GetComponentInParent<ThresholdMist>()||!renderer.enabled)continue;
                var materials=renderer.sharedMaterials;originals[renderer]=materials;
                var temporary=new Material[materials.Length];
                for(int i=0;i<materials.Length;i++)
                {
                    var material=new Material(materials[i]);temporary[i]=material;
                    var color=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):Color.white;
                    fades.Add(material);colors.Add(color);color.a=0;material.SetColor("_BaseColor",color);
                    material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=(int)RenderQueue.Transparent;
                    material.SetOverrideTag("RenderType","Transparent");material.SetShaderPassEnabled("ShadowCaster",false);
                }
                renderer.sharedMaterials=temporary;
                var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
                var vertices=mesh&&mesh.isReadable?mesh.vertices:null;var triangles=vertices!=null?mesh.triangles:null;
                int count=Mathf.Clamp(Mathf.RoundToInt(renderer.bounds.size.magnitude*35),10,250);
                for(int i=0;i<count;i++)
                {
                    Vector3 point;
                    if(triangles!=null&&triangles.Length>=3)
                    {
                        int t=Random.Range(0,triangles.Length/3)*3;float u=Random.value,v=Random.value;if(u+v>1){u=1-u;v=1-v;}
                        point=vertices[triangles[t]]+(vertices[triangles[t+1]]-vertices[triangles[t]])*u+(vertices[triangles[t+2]]-vertices[triangles[t]])*v;
                    }
                    else point=renderer.localBounds.center+Vector3.Scale(Random.insideUnitSphere,renderer.localBounds.extents);
                    points.Add(new Point{owner=renderer.transform,local=point,scatter=Random.onUnitSphere*Random.Range(1.5f,4.5f)});
                }
            }
            var go=new GameObject("Door assembly particles",typeof(ParticleSystem));go.transform.SetParent(transform,false);
            particles=go.GetComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.loop=false;main.playOnAwake=false;main.duration=30;main.startSpeed=0;main.maxParticles=points.Count;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var pr=particles.GetComponent<ParticleSystemRenderer>();pr.sharedMaterial=particleMaterial;pr.shadowCastingMode=ShadowCastingMode.Off;pr.receiveShadows=false;
            var data=new ParticleSystem.Particle[points.Count];particles.Play();
            for(float t=0;t<duration;t+=Mathf.Min(Time.unscaledDeltaTime,.05f))
            {
                Progress=Mathf.Clamp01(t/duration);float converge=Mathf.SmoothStep(0,1,Progress);
                float surface=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,Progress));
                for(int i=0;i<fades.Count;i++){var color=colors[i];color.a*=surface;fades[i].SetColor("_BaseColor",color);}
                for(int i=0;i<data.Length;i++)
                {
                    var point=points[i];data[i].position=transform.InverseTransformPoint(point.owner.TransformPoint(point.local))+point.scatter*(1-converge);
                    data[i].startSize=Mathf.Lerp(.085f,.025f,converge);data[i].startColor=new Color(.8f,.88f,1,Mathf.SmoothStep(0,1,Progress/.15f)*(1-surface));
                    data[i].startLifetime=30;data[i].remainingLifetime=30;
                }
                particles.SetParticles(data,data.Length);yield return null;
            }
            Progress=1;Cleanup();
        }
        void Cleanup()
        {
            foreach(var pair in originals)if(pair.Key)pair.Key.sharedMaterials=pair.Value;originals.Clear();
            foreach(var material in fades)if(material)Destroy(material);fades.Clear();colors.Clear();
            if(particles)Destroy(particles.gameObject);particles=null;Playing=false;
        }
        void OnDisable(){StopAllCoroutines();Cleanup();}
    }
}
