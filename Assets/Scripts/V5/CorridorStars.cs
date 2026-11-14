using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V5
{
    public sealed class CorridorStars : MonoBehaviour
    {
        sealed class Star
        {
            public Transform shape;
            public Renderer renderer;
            public Vector3 origin, drift;
            public float age, period, height;
        }

        const int PathSections=10, StarsPerSection=8;
        readonly Star[] stars=new Star[PathSections*StarsPerSection];
        PassageJourney journey;
        Mesh diamond;
        Material ink;

        public void Build(PassageJourney owner)
        {
            journey=owner;
            diamond=new Mesh { name="60 degree corridor diamond" };
            float halfWidth=.5f/Mathf.Sqrt(3);
            diamond.vertices=new[]{Vector3.up*.5f,Vector3.right*halfWidth,Vector3.down*.5f,Vector3.left*halfWidth};
            diamond.triangles=new[]{0,1,2,0,2,3};
            diamond.RecalculateNormals();
            diamond.RecalculateBounds();
            ink=new Material(owner.lampMaterial);
            ink.EnableKeyword("_EMISSION");
            ink.SetColor("_BaseColor",Color.black);
            for(int i=0;i<stars.Length;i++)
            {
                var go=new GameObject("Corridor star",typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=diamond;
                var renderer=go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=ink;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                var star=stars[i]=new Star {shape=go.transform,renderer=renderer};
                Spawn(star,i);
                star.age=Random.value*star.period;
            }
        }

        void Spawn(Star star,int slot)
        {
            // Sample both sides of a vaulted cross-section, staggered along the path.
            float side=slot%2==0?1:-1;
            int band=(slot%StarsPerSection)/2;
            float z=(slot/StarsPerSection+Random.value)/PathSections;
            float angle=(band+Random.value)*Mathf.PI/8f;
            float radius=band==0?Random.Range(journey.PathWidth*.5f+.8f,5.2f):Random.Range(4.4f,9.4f);
            float x=side*Mathf.Cos(angle)*radius;
            float y=Random.Range(.55f,1.45f)+Mathf.Sin(angle)*radius*.78f;
            // Extend the feet of the arch below the walkway, tapering to zero at 45 degrees.
            y-=1.9f*(1f-Mathf.InverseLerp(0f,Mathf.PI*.25f,angle));
            star.origin=new Vector3(x,y,Mathf.Lerp(.9f,journey.pathLength-.9f,z));
            star.height=Random.Range(.32f,.72f);
            star.period=Random.Range(3.2f,7f);
            star.drift=new Vector3(side*Random.Range(-.07f,.07f),Random.Range(-.07f,.12f),Random.Range(-.13f,.13f));
            star.age=0;
        }

        void LateUpdate()
        {
            bool visible=journey.isActiveAndEnabled&&
                (!journey.entry.portableClosure||!journey.entry.portableClosure.Isolated)&&journey.CorridorBrightness>.001f;
            float light=journey.CorridorBrightness;
            ink.SetColor("_EmissionColor",Color.white*(1.6f*light*journey.entry.sentenceRuntime.LampMultiplier));
            var view=journey.entry.player.view;
            for(int i=0;i<stars.Length;i++)
            {
                var star=stars[i];
                if(!visible){star.renderer.enabled=false;continue;}
                star.age+=Time.deltaTime;
                if(star.age>=star.period)Spawn(star,i);
                float phase=star.age/star.period;
                float reveal=Mathf.SmoothStep(0,1,Mathf.Min(phase,1-phase)/.32f);
                star.shape.localPosition=star.origin+star.drift*star.age;
                star.shape.rotation=Quaternion.LookRotation((star.shape.position-view.position).normalized,Vector3.up);
                star.shape.localScale=Vector3.one*(star.height*reveal);
                star.renderer.enabled=reveal>.005f;
            }
        }

        public Renderer Pick(Ray ray,float reach)
        {
            if(!journey.InCorridor||!journey.CorridorOn||journey.CorridorBrightness<.999f)return null;
            Renderer best=null;
            foreach(var star in stars)
            {
                if(!star.renderer.enabled||star.renderer.forceRenderingOff)continue;
                var plane=new Plane(star.shape.forward,star.shape.position);
                if(!plane.Raycast(ray,out float distance)||distance>=reach)continue;
                var point=star.shape.InverseTransformPoint(ray.GetPoint(distance));
                if(Mathf.Abs(point.x)*Mathf.Sqrt(3)+Mathf.Abs(point.y)>.5f)continue;
                best=star.renderer;
                reach=distance;
            }
            return best;
        }

        void OnDestroy(){if(ink)Destroy(ink);if(diamond)Destroy(diamond);}
    }
}
