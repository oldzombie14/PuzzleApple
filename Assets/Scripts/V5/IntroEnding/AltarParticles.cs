using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class AltarParticles : MonoBehaviour
    {
        ParticleSystem rising,burst;
        Material blackStar,whiteStar;

        public void Build(Mesh diamond,Material lamp)
        {
            blackStar=new Material(Resources.Load<Shader>("V5/AltarParticles"));
            whiteStar=new Material(lamp);whiteStar.SetColor("_EmissionColor",Color.white*4);
            rising=Create("Rising motes",whiteStar,.025f,.065f,2.4f,2.4f);
            var diamonds=rising.GetComponent<ParticleSystemRenderer>();
            diamonds.renderMode=ParticleSystemRenderMode.Mesh;diamonds.mesh=diamond;
            burst=Create("Ripple stars",blackStar,.07f,.14f,.95f,1.45f);
            var shrink=burst.sizeOverLifetime;shrink.enabled=true;
            shrink.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
        }
        ParticleSystem Create(string label,Material material,float minSize,float maxSize,float minLife,float maxLife)
        {
            var obj=new GameObject(label);obj.transform.SetParent(transform,false);
            var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.startSpeed=0;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Local;
            main.startSize=new ParticleSystem.MinMaxCurve(minSize,maxSize);main.startLifetime=new ParticleSystem.MinMaxCurve(minLife,maxLife);
            var shape=ps.shape;shape.enabled=false;
            var emission=ps.emission;emission.enabled=false;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
            return ps;
        }
        public void Ignite()
        {
            rising.Play(false);burst.Play(false);
            for(int i=0;i<32;i++){
                var p=new ParticleSystem.EmitParams{position=transform.position+Random.insideUnitSphere*.4f,velocity=Vector3.up*Random.Range(.3f,1.2f)};
                rising.Emit(p,1);
            }
        }
        public void Scatter(Transform eye,int wave)
        {
            const int count=8;
            for(int i=0;i<count;i++){
                float angle=(i+wave*.5f+Random.Range(-.22f,.22f))*2*Mathf.PI/count;
                Vector3 outward=eye.right*Mathf.Cos(angle)+eye.up*Mathf.Sin(angle);
                var p=new ParticleSystem.EmitParams{position=transform.position-eye.forward*.12f+outward*Random.Range(.14f,.26f),
                    velocity=outward*Random.Range(.70f,1.15f)-eye.forward*.06f};
                burst.Emit(p,1);
            }
        }
        void OnDestroy(){if(blackStar)Destroy(blackStar);if(whiteStar)Destroy(whiteStar);}
    }
}
