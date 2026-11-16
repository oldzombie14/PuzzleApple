using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class CorridorStars : MonoBehaviour, ISwitchableLight
    {
        sealed class Star
        {
            public Transform shape;
            public Vector3 home;
            public Renderer renderer;
            public Plane[] faces;
            Vector3 direction;
            float travel, roadContact, speed, flightOpacity = 1;
            Bounds tunnel, road;
            public float age, period;
            public float height, size=1, transitionAge, transitionDuration, startSize;
            public float Opacity => Mathf.SmoothStep(0,1,size/.3f);
            public bool targetOn=true, transitioning;
            public void SetFlightBounds(float pathLength, float pathWidth, float doorHeight)
            {
                // Reserve space for the fully grown star at any orientation.
                float margin = renderer.localBounds.extents.magnitude * height + .12f;
                float ceiling = 3 * doorHeight;
                tunnel = new Bounds(new Vector3(0,ceiling*.5f,pathLength*.5f),
                    new Vector3(7*pathWidth,ceiling,pathLength));
                road = new Bounds(new Vector3(0,-.15f,pathLength*.5f),new Vector3(pathWidth,.3f,pathLength));
                road.Expand(2*margin);
                ChooseFlight();
            }

            void ChooseFlight()
            {
                travel = 0;
                speed = Random.Range(.65f,.85f);
                direction = Random.onUnitSphere;
                // Launch away from the nearby end wall, especially for doorway stars.
                direction.z = home.z < tunnel.center.z ? Mathf.Abs(direction.z) : -Mathf.Abs(direction.z);
                roadContact = road.IntersectRay(new Ray(home,direction),out float hit)
                    ? Mathf.Max(0,hit) : float.PositiveInfinity;
            }

            public float AdvanceFlight(bool moving, float delta, float visualDelta)
            {
                if (moving) travel = Mathf.Min(roadContact,travel+speed*delta);
                shape.localPosition = home + direction * travel;
                // Crossing the tunnel boundary starts a one-metre fade. Inside it,
                // only an imminent road contact can attenuate the flight.
                float outside = Mathf.Sqrt(tunnel.SqrDistance(shape.localPosition));
                float target = (1-Mathf.SmoothStep(0,1,outside)) *
                    Mathf.SmoothStep(0,1,(roadContact-travel)/.65f);
                flightOpacity = Mathf.MoveTowards(flightOpacity,target,visualDelta);
                if (moving && target==0 && flightOpacity==0)
                {
                    ChooseFlight();
                    shape.localPosition = home;
                }
                return flightOpacity;
            }
            public void Advance(float delta,bool on,bool enabled,float fadeDuration,Vector3 viewer)
            {
                if(on!=targetOn)
                {
                    targetOn=on;transitioning=true;transitionAge=0;
                    startSize=size;
                    float distanceSquared=(shape.position-viewer).sqrMagnitude;
                    bool distantSmall=distanceSquared>=144 && height*height<.025f*.025f*distanceSquared;
                    // Shorten each star's remaining cycle proportionally, preserving its phase.
                    float remainingCycle = period - age;
                    transitionDuration = Mathf.Max(.001f, on ? fadeDuration :
                        remainingCycle * (distantSmall ? .08f : .55f));
                }
                if(transitioning)
                {
                    if(!enabled)return;
                    transitionAge=Mathf.Min(transitionDuration,transitionAge+delta);
                    float t=transitionAge/transitionDuration;
                    float u=on?Mathf.SmoothStep(0,1,t):t*t*t;
                    size=Mathf.Lerp(startSize,on?TwinkleSize():0,u);
                    if(transitionAge>=transitionDuration) transitioning=false;
                }
                else if(on)
                {
                    age=Mathf.Repeat(age+delta,period);
                    size=TwinkleSize();
                }
            }

            public float TwinkleSize()
            {
                const float ramp = 1.5f;
                // The dark hold straddles the wrap; split the non-ramping time
                // equally between full brightness and darkness.
                float halfDarkHold = (period - 2*ramp) * .25f;
                float fromDark = Mathf.Min(age,period-age) - halfDarkHold;
                return Mathf.SmoothStep(0,1,fromDark/ramp);
            }
        }

        Star[] stars;
        MaterialPropertyBlock glow;
        PassageJourney journey;
        Vector4 depthPlane;
        float visibleLength;
        Material pathInk, edgeInk;
        public bool On { get; private set; } = true;
        public bool TransitionEnabled { get; private set; }
        public float Brightness { get; private set; } = 1;
        public bool Changing => TransitionEnabled && (Brightness!=(On?1:0) || SwitchPending(On));
        public void SetOn(bool on) { On=on; TransitionEnabled=true; }
        public void Hold() { TransitionEnabled=false; }
        public bool WouldChange(bool on) => Settling(on);
        public bool Settling(bool on) => Brightness!=(on?1:0) || SwitchPending(on);
        public ObservableBehaviour Observation { get; private set; }
        public bool Moving => journey && journey.entry.sentenceRuntime && !journey.entry.sentenceRuntime.MotionFrozen &&
            journey.DemonstrationLampMoving(true);

        public void Bind(PassageJourney owner, float startZ, float doorHeight, float entranceThickness,
            Material pathMaterial, Material edgeMaterial)
        {
            journey = owner;
            pathInk = pathMaterial;
            edgeInk = edgeMaterial;
            Observation = gameObject.AddComponent<ObservableBehaviour>();
            Observation.lesson = owner.movementObservation.lesson;
            transform.SetParent(owner.Root, false);
            transform.localPosition = Vector3.forward * startZ;
            glow = new MaterialPropertyBlock();
            var axis = transform.forward;
            float near = entranceThickness * .5f;
            depthPlane = new Vector4(axis.x,axis.y,axis.z,-Vector3.Dot(axis,transform.position)-near);
            visibleLength = owner.pathLength - near - .15f;
            glow.SetVector("_CorridorAxis",depthPlane);
            glow.SetFloat("_CorridorLength",visibleLength);
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            stars = new Star[renderers.Length];
            var hulls = new Dictionary<Mesh,Plane[]>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var star = new Star
                {
                    shape = renderer.transform,
                    home = renderer.transform.localPosition,
                    renderer = renderer,
                    height = renderer.transform.localScale.y,
                    period = Random.Range(5.5f, 8.5f)
                };
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                if (mesh.bounds.size.z > .001f)
                {
                    if (!hulls.TryGetValue(mesh,out star.faces))
                    {
                        var vertices = mesh.vertices;
                        var triangles = mesh.triangles;
                        star.faces = new Plane[triangles.Length/3];
                        for (int face=0; face<star.faces.Length; face++)
                        {
                            int n = 3*face;
                            var plane = new Plane(vertices[triangles[n]],vertices[triangles[n+1]],vertices[triangles[n+2]]);
                            if (plane.GetDistanceToPoint(mesh.bounds.center)>0) plane.Flip();
                            star.faces[face] = plane;
                        }
                        hulls.Add(mesh,star.faces);
                    }
                    star.shape.localRotation = Quaternion.Euler(0,(i*137.5f)%360,0);
                }
                star.age = Random.Range(0f, star.period);
                star.size = star.TwinkleSize();
                star.SetFlightBounds(owner.pathLength,owner.PathWidth,doorHeight);
                stars[i] = star;
            }
            gameObject.SetActive(true);
        }

        public bool SwitchPending(bool on)
        {
            foreach(var star in stars)
                if(star!=null&&(star.targetOn!=on||star.transitioning))return true;
            return false;
        }
        void LateUpdate()
        {
            bool visible=journey.isActiveAndEnabled&&
                (!journey.entry.portableClosure||!journey.entry.portableClosure.Isolated);
            Color emission=Color.white*(1.6f*journey.entry.sentenceRuntime.LampMultiplier);
            var view=journey.entry.player.view;
            if (TransitionEnabled)
            {
                Brightness=RoomLighting.AdvanceLevel(Brightness,On?1:0,Time.unscaledDeltaTime/Mathf.Max(.01f,journey.lightFadeDuration));
                float level=Mathf.SmoothStep(0,1,Brightness);
                pathInk.SetFloat("_CorridorLight",Mathf.Lerp(.065f,1,level));
                if(edgeInk)edgeInk.SetFloat("_Visibility",Mathf.Lerp(.09f,1,level));
            }
            bool moving = visible && Moving && On;
            for(int i=0;i<stars.Length;i++)
            {
                var star=stars[i];
                star.Advance(Time.unscaledDeltaTime,On,TransitionEnabled,journey.lightFadeDuration,view.position);
                if(!visible){star.renderer.enabled=false;continue;}
                float flight = star.AdvanceFlight(moving,Time.deltaTime,Time.unscaledDeltaTime);
                float reveal=star.Opacity*flight;
                star.shape.localScale=Vector3.one*(star.height*star.size*Mathf.Lerp(.4f,1,flight));
                Vector3 fromView=star.shape.position-view.position;
                if(star.faces==null && fromView.sqrMagnitude>.000001f)
                    star.shape.rotation=Quaternion.LookRotation(fromView,Vector3.up);
                glow.SetColor("_EmissionColor",emission);
                glow.SetFloat("_FacetStrength",star.faces!=null?1:0);
                glow.SetColor("_BaseColor",new Color(0,0,0,reveal));
                star.renderer.SetPropertyBlock(glow);
                star.renderer.enabled=reveal>.005f;
            }
        }

        public Renderer Pick(Ray ray,float reach)
        {
            if(!journey.InCorridor||!On||Brightness<.999f)return null;
            Renderer best=null;
            foreach(var star in stars)
            {
                if(!star.renderer.enabled||star.renderer.forceRenderingOff)continue;
                float distance;
                if (star.faces!=null)
                {
                    if (!star.renderer.bounds.IntersectRay(ray) || !HitSolid(star,ray,reach,out distance)) continue;
                }
                else
                {
                    var plane=new Plane(star.shape.forward,star.shape.position);
                    if(!plane.Raycast(ray,out distance)||distance>=reach)continue;
                    var point=star.shape.InverseTransformPoint(ray.GetPoint(distance));
                    if(Mathf.Abs(point.x)*Mathf.Sqrt(3)+Mathf.Abs(point.y)>.5f)continue;
                }
                var world = ray.GetPoint(distance);
                float depth = Vector3.Dot(transform.forward,world)+depthPlane.w;
                if(depth<0 || depth>visibleLength)continue;
                best=star.renderer;
                reach=distance;
            }
            return best;
        }

        static bool HitSolid(Star star,Ray ray,float reach,out float enter)
        {
            var local = star.shape.worldToLocalMatrix;
            Vector3 origin = local.MultiplyPoint3x4(ray.origin);
            Vector3 direction = local.MultiplyVector(ray.direction);
            enter = float.NegativeInfinity;
            float exit = reach;
            foreach (var face in star.faces)
            {
                float speed = Vector3.Dot(face.normal,direction);
                float side = face.GetDistanceToPoint(origin);
                if (Mathf.Abs(speed)<.000001f)
                {
                    if (side>0) return false;
                    continue;
                }
                float hit = -side/speed;
                if (speed<0) enter = Mathf.Max(enter,hit);
                else exit = Mathf.Min(exit,hit);
                if (enter>exit) return false;
            }
            return enter>=0 && enter<reach;
        }


    }
}
