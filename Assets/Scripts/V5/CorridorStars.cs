using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class CorridorStars : MonoBehaviour, ISwitchableLight, ISentenceOutputState<CorridorStars.LightState>,
        ISentenceOutputState<CorridorStars.FlightState>
    {
        internal SentenceStateOutput LightOutput;
        public SentenceMotionOutput MotionOutput {get;internal set;}
        sealed class Star
        {
            public Transform shape;
            public Vector3 home;
            public Renderer renderer,halo;
            public Plane[] faces;
            Vector3 direction;
            float travel, roadContact, speed, flightOpacity = 1;
            Bounds tunnel, road;
            public StarLight light = new StarLight { size = 1, targetOn = true };
            public float height;
            public float Opacity => Mathf.SmoothStep(0,1,light.size/.3f);
            public float FlightOpacity => flightOpacity;
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

            public Flight CaptureFlight() => new Flight {
                home = home, position = shape.localPosition, direction = direction, travel = travel,
                speed = speed, opacity = flightOpacity
            };
            public void RestoreFlight(Flight flight)
            {
                home = flight.home;
                shape.localPosition = flight.position;
                direction = flight.direction;
                speed = flight.speed;
                travel = flight.travel;
                flightOpacity = flight.opacity;
                UpdateRoadContact();
            }
            void UpdateRoadContact()
            {
                roadContact = road.IntersectRay(new Ray(home,direction),out float hit)
                    ? Mathf.Max(0,hit) : float.PositiveInfinity;
            }

            void ChooseFlight()
            {
                travel = 0;
                speed = Random.Range(.65f,.85f);
                direction = Random.onUnitSphere;
                // Launch away from the nearby end wall, especially for doorway stars.
                direction.z = home.z < tunnel.center.z ? Mathf.Abs(direction.z) : -Mathf.Abs(direction.z);
                UpdateRoadContact();
            }

            public void ResumeFlight(){
                home=shape.localPosition;
                travel=0;
                UpdateRoadContact();
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
            public void Advance(float delta,bool on,bool enabled,float fadeDuration,Vector3 viewer,float finishFraction=0)
            {
                if (!enabled) on = light.targetOn;
                if(on!=light.targetOn)
                {
                    light.targetOn=on;light.transitioning=true;light.transitionAge=0;
                    light.startSize=light.size;
                    float distanceSquared=(shape.position-viewer).sqrMagnitude;
                    bool distantSmall=distanceSquared>=144 && height*height<.025f*.025f*distanceSquared;
                    // Shorten each star's remaining cycle proportionally, preserving its phase.
                    float remainingCycle = light.period - light.age;
                    light.transitionDuration = Mathf.Max(.001f, on ? fadeDuration :
                        remainingCycle * (distantSmall ? .08f : .55f));
                }
                if(light.transitioning)
                {
                    if(!enabled)return;
                    light.transitionAge=Mathf.Min(light.transitionDuration,light.transitionAge+Mathf.Max(delta,(light.transitionDuration-light.transitionAge)*finishFraction));
                    float t=light.transitionAge/light.transitionDuration;
                    float u=on?Mathf.SmoothStep(0,1,t):t*t*t;
                    light.size=Mathf.Lerp(light.startSize,on?TwinkleSize():0,u);
                    if(light.transitionAge>=light.transitionDuration) light.transitioning=false;
                }
                else if(on)
                {
                    light.age=Mathf.Repeat(light.age+delta,light.period);
                    light.size=TwinkleSize();
                }
            }

            public float TwinkleSize()
            {
                const float ramp = 1.5f;
                // The dark hold straddles the wrap; split the non-ramping time
                // equally between full brightness and darkness.
                float halfDarkHold = (light.period - 2*ramp) * .25f;
                float fromDark = Mathf.Min(light.age,light.period-light.age) - halfDarkHold;
                return Mathf.SmoothStep(0,1,fromDark/ramp);
            }
        }

        [System.Serializable]
        public struct Flight
        {
            public Vector3 home, position, direction;
            public float travel, speed, opacity;
            public bool Valid => float.IsFinite(home.sqrMagnitude) && float.IsFinite(position.sqrMagnitude) &&
                Mathf.Abs(direction.sqrMagnitude - 1) < .001f && float.IsFinite(travel) && travel >= 0 &&
                float.IsFinite(speed) && speed > 0 && opacity >= 0 && opacity <= 1;
        }
        [System.Serializable]
        public struct FlightState
        {
            public bool parked;
            public Flight[] stars;
        }
        FlightState ISentenceOutputState<FlightState>.CaptureState() => new FlightState {
            parked = parked, stars = System.Array.ConvertAll(stars, star => star.CaptureFlight())
        };
        public bool TryRestoreState(FlightState state)
        {
            if (!journey || stars == null || state.stars == null || state.stars.Length != stars.Length ||
                System.Array.Exists(state.stars, star => !star.Valid)) return false;
            parked = state.parked;
            for (int i = 0; i < stars.Length; i++) stars[i].RestoreFlight(state.stars[i]);
            UpdateStars(false);
            return true;
        }

        [System.Serializable]
        public struct StarLight
        {
            public float age, period, size, transitionAge, transitionDuration, startSize;
            public bool targetOn, transitioning;
            public bool Valid => period > 0 && !float.IsInfinity(period) && age >= 0 && age <= period &&
                size >= 0 && size <= 1 && startSize >= 0 && startSize <= 1 && transitionAge >= 0 &&
                transitionAge <= transitionDuration && !float.IsInfinity(transitionDuration) &&
                (!transitioning || transitionDuration > 0);
        }
        [System.Serializable]
        public struct LightState
        {
            public EmissionState path;
            public StarLight[] stars;
        }
        public LightState CaptureState() => new LightState {
            path = new EmissionState(Brightness), stars = System.Array.ConvertAll(stars, star => star.light)
        };
        public bool TryRestoreState(LightState state)
        {
            if (!journey || stars == null || !state.path.Valid || state.stars == null ||
                state.stars.Length != stars.Length || System.Array.Exists(state.stars, star => !star.Valid)) return false;
            Brightness = state.path.brightness;
            for (int i = 0; i < stars.Length; i++) stars[i].light = state.stars[i];
            ApplyPathBrightness();
            UpdateStars(false);
            return true;
        }
        Star[] stars;
        bool parked;
        Vector3 GatherPoint(int id){
            float angle=(id+.5f)*Mathf.PI*2/stars.Length;
            return new Vector3(Mathf.Sin(angle)*(journey.PathWidth*.5f+.4f),2+Mathf.Cos(angle)*1.75f,journey.pathLength-.7f-(id%3)*.12f);
        }
        public bool NeedsGather{
            get{
                if(!On || stars==null)return false;
                for(int i=0;i<stars.Length;i++)if((stars[i].shape.localPosition-GatherPoint(i)).sqrMagnitude>.0001f)return true;
                return false;
            }
        }
        MaterialPropertyBlock glow;
        public StarGlowStyle glowStyle;
        Material haloInk;
        PassageJourney journey;
        Vector4 depthPlane;
        float visibleLength;
        Material pathInk, edgeInk;
        public Vector3 FocusNear(Vector3 point){
            Vector3 nearest=point;
            float best=float.PositiveInfinity;
            foreach(var star in stars){
                if(!star.renderer.enabled || star.Opacity<.05f)continue;
                float distance=(star.shape.position-point).sqrMagnitude;
                if(distance<best){best=distance;nearest=star.shape.position;}
            }
            return nearest;
        }
        public bool On { get; private set; } = true;
        public bool TransitionEnabled { get; private set; }
        public float Brightness { get; private set; } = 1;
        // The stars shrink on their own phases after the path-light fade has reached zero.
        public float EmissionLevel { get; private set; }
        public bool Changing => TransitionEnabled && (Brightness!=(On?1:0) || SwitchPending(On));
        public void SetOn(bool on) { On=on; TransitionEnabled=true; }
        public void Hold() { TransitionEnabled=false; }
        public bool WouldChange(bool on) => Settling(on);
        public bool Settling(bool on) => Brightness!=(on?1:0) || SwitchPending(on);
        public ObservableBehaviour Observation { get; private set; }
        public bool Moving => journey && journey.entry.sentenceRuntime && !journey.entry.sentenceRuntime.MotionFrozen &&
            journey.DemonstrationLampMoving(MotionOutput,true,-1,this==journey.Stars?PassageJourney.LightSpace.FirstPath:PassageJourney.LightSpace.ReturnPath);

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
            if(!glowStyle)glowStyle=StarGlowStyle.Load();
            haloInk=new Material(Resources.Load<Shader>("V5/StarHalo"));
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
                    light = new StarLight { period = Random.Range(5.5f, 8.5f), size = 1, targetOn = true }
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
                star.light.age = Random.Range(0f, star.light.period);
                star.halo=StarGlowStyle.CreateHalo(star.shape,haloInk);
                star.light.size = star.TwinkleSize();
                star.SetFlightBounds(owner.pathLength,owner.PathWidth,doorHeight);
                stars[i] = star;
            }
            gameObject.SetActive(true);
        }

        public bool SwitchPending(bool on)
        {
            foreach(var star in stars)
                if(star!=null&&(star.light.targetOn!=on||star.light.transitioning))return true;
            return false;
        }
        void ApplyPathBrightness()
        {
            float level=Mathf.SmoothStep(0,1,Brightness);
            pathInk.SetFloat("_CorridorLight",Mathf.Lerp(.065f,1,level));
            if(edgeInk)edgeInk.SetFloat("_Visibility",Mathf.Lerp(.09f,1,level));
        }
        void LateUpdate() => UpdateStars(true);
        void UpdateStars(bool advance)
        {
            EmissionLevel = 0;
            if(!journey || stars==null)return;
            bool visible=journey.isActiveAndEnabled&&
                (!journey.entry.portableClosure||!journey.entry.portableClosure.Isolated);
            Color emission=Color.white*((glowStyle?glowStyle.emission:1f)*journey.entry.sentenceRuntime.LampBoost(this==journey.Stars?PassageJourney.LightSpace.FirstPath:PassageJourney.LightSpace.ReturnPath));
            var view=journey.entry.player.view;
            float finishFraction=LightOutput?.FinishFraction??0;
            if (advance && TransitionEnabled)
            {
                Brightness=RoomLighting.AdvanceLevel(Brightness,On?1:0,Time.unscaledDeltaTime/Mathf.Max(.01f,journey.lightFadeDuration),finishFraction);
                ApplyPathBrightness();
            }
            bool gather=this==journey.Stars && (MotionOutput?.HasMotion(SentenceMotion.LightToDoor)==true);
            if(advance && gather)parked=true;
            if(advance && this==journey.Stars && (MotionOutput?.HasMotion(SentenceMotion.Light)==true)){
                if(parked)foreach(var s in stars)s.ResumeFlight();
                parked=false;
            }
            bool moving = visible && Moving && On && !parked;
            bool approachingDoor=advance && gather && On && !journey.EndingDoor.Paused;
            float gatherFraction=approachingDoor?MotionOutput.DirectedStep(0,1,Time.deltaTime):0;
            for(int i=0;i<stars.Length;i++)
            {
                var star=stars[i];
                if(advance)star.Advance(Time.unscaledDeltaTime,On,TransitionEnabled,journey.lightFadeDuration,view.position,finishFraction);
                if(approachingDoor){
                    var target=GatherPoint(i);
                    var position=star.shape.localPosition;
                    float step=Mathf.Max(4*Time.deltaTime,gatherFraction*Vector3.Distance(position,target));
                    star.shape.localPosition=Vector3.MoveTowards(position,target,step);
                }
                if(!visible){star.renderer.enabled=star.halo.enabled=false;continue;}
                float flight = parked ? 1 : star.FlightOpacity;
                if (advance && !parked) flight = star.AdvanceFlight(moving,Time.deltaTime,Time.unscaledDeltaTime);
                float reveal=star.Opacity*flight;
                star.shape.localScale=Vector3.one*(star.height*star.light.size*Mathf.Lerp(.4f,1,flight));
                Vector3 fromView=star.shape.position-view.position;
                if(star.faces==null && fromView.sqrMagnitude>.000001f)
                    star.shape.rotation=Quaternion.LookRotation(fromView,Vector3.up);
                glow.SetColor("_EmissionColor",emission);
                glow.SetFloat("_FacetStrength",star.faces!=null?1:0);
                glow.SetColor("_BaseColor",new Color(0,0,0,reveal));
                star.renderer.SetPropertyBlock(glow);
                star.renderer.enabled=reveal>.005f;
                if (star.renderer.enabled) EmissionLevel = Mathf.Max(EmissionLevel, reveal);
                if(glowStyle)glowStyle.DrawHalo(star.halo,star.renderer.enabled,glow);
                star.halo.transform.rotation=Quaternion.LookRotation(fromView,Vector3.up);
            }
        }

        public Renderer Pick(Ray ray,float reach)
        {
            if(!journey.InCorridor || EmissionLevel<=0)return null;
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
        void OnDestroy(){if(haloInk)Destroy(haloInk);}

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
