using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class MoveTutorialRoom : MonoBehaviour, ISwitchableLight, ISentenceMovable
    {
        // The shell is lit only by the spheres' real lights plus this room's RoomLightBalance fill.
        public RoomLightBalance Fill { get; private set; }

        sealed class Lamp
        {
            public float size;
            public Renderer surface;
            public SphereCollider collider;
            public Light light;
            public Vector3 direction;
            public float level;
            public bool isOn;
        }

        PassageJourney journey;
        readonly List<Renderer> shell = new List<Renderer>();
        readonly Dictionary<Vector3, Mesh> panelMeshes = new Dictionary<Vector3, Mesh>();
        readonly List<Mesh> openingMeshes = new List<Mesh>();
        MaterialPropertyBlock properties;
        OuterRoomEdges edges;
        Lamp sphere;
        readonly List<Lamp> lamps = new List<Lamp>();
        Material shellMaterial;
        float height, roomLevel;
        bool targetOn, fading;
        Color emission, baseColor;
        public float HalfWidth { get; private set; }
        public float ExitZ => journey.pathLength + 2 * HalfWidth;
        public Transform Sphere => sphere.surface.transform;
        public Renderer EntranceWall { get; private set; }
        public bool AnyLit => lamps.Exists(lamp => lamp.isOn);
        // Brightest sphere glow, 0 dark to 1 lit; drives the sphere-sourced "move" memory.
        public float SphereBrightness => Mathf.SmoothStep(0, 1, lamps.Count == 0 ? 0 : lamps.Max(lamp => lamp.level));
        public bool ExitBlocked
        {
            get
            {
                var motor = journey.entry.player.GetComponent<CharacterController>();
                Vector3 p = Sphere.localPosition;
                float playerRadius = motor.radius + motor.skinWidth;
                float lane = Mathf.Max(0, journey.PathWidth*.5f - playerRadius);
                // The farthest walkable lane is the last one the sphere can block.
                float dx = Mathf.Abs(p.x) + lane;
                float dy = p.y - Mathf.Clamp(p.y,playerRadius,Mathf.Max(playerRadius,motor.height-playerRadius));
                float dz = Mathf.Max(0, Mathf.Abs(p.z-ExitZ)-.15f);
                float clearance = Sphere.localScale.x*.5f + playerRadius;
                return dx*dx+dy*dy+dz*dz < clearance*clearance;
            }
        }
        public bool WouldChange(bool on)
        {
            float target = on ? 1 : 0;
            return roomLevel != target || lamps.Exists(lamp => lamp.level != target);
        }
        public bool Changing => fading && WouldChange(targetOn);
        public bool Settling(bool on) => WouldChange(on);

        public void Build(PassageJourney owner, float openingHeight, Material lampFinish)
        {
            journey = owner;
            properties = new MaterialPropertyBlock();
            var original = FindFirstObjectByType<OuterRoomEdges>();
            var floor = original.surfaces.OrderBy(r=>r.bounds.center.y).First();
            var ceiling = original.surfaces.OrderByDescending(r=>r.bounds.center.y).First();
            HalfWidth = floor.bounds.size.x * .5f;
            height = ceiling.bounds.min.y - floor.bounds.max.y;
            shellMaterial = new Material(floor.sharedMaterial);
            Material wall = shellMaterial;
            float middle = journey.pathLength + HalfWidth;
            float width = HalfWidth * 2;
            Fill = RoomLightBalance.Create(journey.Root, "Sphere room light balance", new Vector3(0,height*.5f,middle),
                new Vector3(width,height,width), journey.LightStyle);
            Panel("Floor", new Vector3(0,-.15f,middle), new Vector3(width,.3f,width+.3f), Vector3.up, wall);
            Panel("Ceiling", new Vector3(0,height+.15f,middle), new Vector3(width,.3f,width), Vector3.down, wall);
            foreach (int sign in new[] {-1,1})
                Panel("Side", new Vector3(sign*(HalfWidth+.15f),height*.5f,middle), new Vector3(.3f,height,width), Vector3.left*sign, wall);
            foreach (float z in new[] {journey.pathLength, ExitZ})
            {
                Vector3 inward = z == ExitZ ? Vector3.back : Vector3.forward;
                var mesh = OpeningWallMesh.Build(journey.PathWidth/width,0,openingHeight/height,inward==Vector3.forward);
                openingMeshes.Add(mesh);
                Panel("Door wall", new Vector3(0,height*.5f,z), new Vector3(width,height,.3f), inward, wall, mesh, openingHeight);
            }
            edges = shell[0].gameObject.AddComponent<OuterRoomEdges>();
            edges.surfaces = shell.ToArray();
            var ink = lampFinish;
            // Same lamp finish as the far room's drifting cubes: white body, uniform emission and Bloom.
            emission = ink.GetColor("_EmissionColor") * journey.emissionStrength;
            baseColor = Color.white;
            float diameter = Mathf.Max(journey.PathWidth,openingHeight)*1.15f;
            float radius = diameter * .5f;
            float clearance = Mathf.Clamp(Mathf.Min(journey.PathWidth * .5f, openingHeight - radius), 0, radius);
            float inset = .17f + Mathf.Sqrt(radius * radius - clearance * clearance);
            sphere = CreateSphere("Move tutorial Sphere", diameter, new Vector3(0,radius,ExitZ-inset), ink, false);
            // Stagger the small lamps on both sides of the route, clear of the exit sphere.
            for (int i=0; i<10; i++)
            {
                float side = i%2 == 0 ? -1 : 1;
                float x = side * HalfWidth * Random.Range(.2f,.8f);
                float y = height * Random.Range(.18f,.75f);
                float z = journey.pathLength + width * (.12f + .16f*(i/2));
                float sizeFraction=Mathf.Lerp(.14f,.30f,((i*7)%10)/9f);
                CreateSphere("Move tutorial small sphere " + (i+1), diameter*sizeFraction,
                    new Vector3(x,y,z), ink, true);
            }
        }

        void Panel(string name, Vector3 position, Vector3 size, Vector3 inward, Material material, Mesh opening=null, float openingHeight=0)
        {
            var go = opening ? new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Move tutorial " + name;
            go.transform.SetParent(journey.Root,false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            var surface = go.GetComponent<Renderer>();
            var filter = go.GetComponent<MeshFilter>();
            var mesh = opening;
            if (opening)
            {
                float halfOpening = journey.PathWidth/(2*size.x);
                float top = openingHeight/size.y-.5f;
                foreach (int sign in new[]{-1,1})
                {
                    var jamb=go.AddComponent<BoxCollider>();
                    jamb.center=new Vector3(sign*(.5f+halfOpening)*.5f,0,0);
                    jamb.size=new Vector3(.5f-halfOpening,1,1);
                }
                var lintel=go.AddComponent<BoxCollider>();
                lintel.center=Vector3.up*((.5f+top)*.5f);
                lintel.size=new Vector3(2*halfOpening,.5f-top,1);
            }
            else if (!panelMeshes.TryGetValue(inward, out mesh))
            {
                mesh = SplitPanelFaces(filter.sharedMesh, inward);
                panelMeshes.Add(inward, mesh);
            }
            filter.sharedMesh = mesh;
            surface.sharedMaterials = new[] { material, journey.blackSurround };
            properties.Clear();
            properties.SetVector("_RoomShellInward",inward);
            properties.SetFloat("_UseInkRoomBounds",1);
            Vector3 a = journey.Root.TransformPoint(new Vector3(-HalfWidth,0,journey.pathLength));
            Vector3 b = journey.Root.TransformPoint(new Vector3(HalfWidth,height,ExitZ));
            properties.SetVector("_InkRoomMin",Vector3.Min(a,b));
            properties.SetVector("_InkRoomMax",Vector3.Max(a,b));
            surface.SetPropertyBlock(properties);
            shell.Add(surface);
            if (inward == Vector3.forward) EntranceWall = surface;
        }

        static Mesh SplitPanelFaces(Mesh cube, Vector3 inward)
        {
            var inside = new List<int>();
            var outside = new List<int>();
            var triangles = cube.triangles;
            var normals = cube.normals;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var face = Vector3.Dot(normals[triangles[i]], inward) > .5f ? inside : outside;
                face.Add(triangles[i]);
                face.Add(triangles[i+1]);
                face.Add(triangles[i+2]);
            }
            var mesh = Instantiate(cube);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(inside, 0);
            mesh.SetTriangles(outside, 1);
            return mesh;
        }

        Lamp CreateSphere(string name, float diameter, Vector3 position, Material material, bool startsLit)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(journey.Root,false);
            go.transform.localScale = Vector3.one * diameter;
            go.transform.localPosition = position;
            Vector3 direction = Random.onUnitSphere;
            if (!startsLit)
            {
                direction.y = Mathf.Abs(direction.y);
                direction.z = -Mathf.Max(.35f, Mathf.Abs(direction.z));
            }
            var lamp = new Lamp {size=diameter, surface=go.GetComponent<Renderer>(), collider=go.GetComponent<SphereCollider>(), direction=direction.normalized, isOn=startsLit, level=startsLit?1:0};
            lamps.Add(lamp);
            lamp.surface.sharedMaterial = material;
            lamp.surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = new GameObject("Soft local light",typeof(Light));
            light.transform.SetParent(go.transform,false);
            lamp.light = light.GetComponent<Light>();
            lamp.light.range = journey.LightStyle.LampRange(journey.LightStyle.LampIntensity(diameter));
            lamp.light.shadows = startsLit ? LightShadows.None : LightShadows.Soft;
            Render(lamp);
            return lamp;
        }


        public bool CanDirect(CognitionSignal signal)
        {
            return lamps.Exists(lamp => lamp.isOn && CanDirect(lamp,signal));
        }
        // light move drifts any lit sphere; directed motions need a sphere that can reach its target.
        public bool CanMove(CognitionSignal motion) => motion==CognitionSignal.LightMove ? AnyLit
            : (motion==CognitionSignal.LightDoorMove || motion==CognitionSignal.LightLightMove || motion==CognitionSignal.LightFloorMove) && CanDirect(motion);

        bool CanDirect(Lamp lamp, CognitionSignal signal)
        {
            var t=lamp.surface.transform;
            var ext=t.localScale*.5f;
            switch(signal)
            {
                case CognitionSignal.LightFloorMove:return t.localPosition.y>ext.y+.031f;
                case CognitionSignal.LightDoorMove:return Vector3.Distance(t.localPosition,new Vector3(0,ext.y+.03f,journey.pathLength+ext.z+.3f))>.01f;
                default:return false;
            }
        }

        public bool IsLit(Collider hit) => lamps.Exists(lamp => hit==lamp.collider && lamp.isOn);
        // Spheres currently showing motion, observable for learning "move".
        public Collider[] MovingSpheres() => lamps.Where(Moving).Select(lamp => lamp.collider).ToArray();
        public void SetOn(bool on)
        {
            targetOn = on;
            fading = true;
            foreach(var lamp in lamps)
                if (on || lamp.level == 0) lamp.isOn = on;
        }
        public void Hold() { fading=false; }

        void Render(Lamp lamp)
        {
            float level = Mathf.SmoothStep(0,1,lamp.level);
            float boost = journey.entry.sentenceRuntime ? journey.entry.sentenceRuntime.LampMultiplier : 1;
            properties.Clear();
            properties.SetColor("_BaseColor",Color.Lerp(new Color(.12f,.12f,.12f),baseColor,level));
            properties.SetColor("_EmissionColor",emission*(level*boost));
            lamp.surface.SetPropertyBlock(properties);
            lamp.light.intensity = journey.LightStyle.LampIntensity(lamp.size)*level*boost;
        }

        void Update()
        {
            if (fading && (!targetOn || lamps.TrueForAll(lamp => lamp.level >= 1))) roomLevel = RoomLighting.AdvanceLevel(roomLevel, targetOn ? 1 : 0,
                Time.unscaledDeltaTime / Mathf.Max(.01f, journey.lightFadeDuration));
            foreach(var lamp in lamps) UpdateLamp(lamp);
        }

        void OnDestroy()
        {
            foreach (var mesh in panelMeshes.Values) Destroy(mesh);
            foreach (var mesh in openingMeshes) Destroy(mesh);
            if (shellMaterial) Destroy(shellMaterial);
        }

        void UpdateLamp(Lamp lamp)
        {
            if (fading)
            {
                lamp.level = RoomLighting.AdvanceLevel(lamp.level, targetOn ? 1 : 0,
                    Time.unscaledDeltaTime / Mathf.Max(.01f, journey.lightFadeDuration));
                if (!targetOn && lamp.level == 0 && lamp.isOn) lamp.isOn = false;
            }
            Render(lamp);
        }

        bool Moving(Lamp lamp)
        {
            if (lamp != sphere) return journey.DemonstrationLampMoving(lamp.isOn);
            return lamp.isOn && journey.entry.sentenceRuntime.HasEffect(CognitionSignal.LightMove);
        }

        void FixedUpdate()
        {
            if (journey.entry.portableClosure && journey.entry.portableClosure.Isolated) return;
            if (journey.entry.sentenceRuntime.MotionFrozen) return;
            bool connected = journey.InTutorial || journey.InCorridor && (journey.InReturnPath || journey.DoorProgress > 0);
            if (connected)
                foreach(var lamp in lamps) Step(lamp);
        }

        void Step(Lamp lamp)
        {
            var t=lamp.surface.transform;
            Vector3 p=t.localPosition, ext=t.localScale*.5f;
            Vector3 lo=new Vector3(-HalfWidth,0,journey.pathLength+.15f)+ext;
            Vector3 hi=new Vector3(HalfWidth,height,ExitZ-.15f)-ext;
            var runtime=journey.entry.sentenceRuntime;
            float step=journey.entry.chamberLight.moveSpeed*Time.fixedDeltaTime;
            bool drifting=Moving(lamp);
            Vector3 target;
            if(drifting)
            {
                // Clear the narrow opening before starting free three-dimensional motion.
                Vector3 direction = lamp==sphere && p.z > hi.z+.001f ? Vector3.back : lamp.direction;
                target = p + direction * step;
            }
            else if(!lamp.isOn)return;
            else if(runtime.HasEffect(CognitionSignal.LightFloorMove))target=Vector3.MoveTowards(p,new Vector3(p.x,ext.y+.03f,p.z),step);
            else if(runtime.HasEffect(CognitionSignal.LightDoorMove))target=Vector3.MoveTowards(p,new Vector3(0,ext.y+.03f,journey.pathLength+ext.z+.3f),step);
            else return;
            // The sphere starts seated in the opening. Allow its first motion to slide clear.
            if(p.z>hi.z)hi.z=p.z;
            for(int axis=0;axis<3;axis++)
            {
                if(target[axis]>=lo[axis]&&target[axis]<=hi[axis])continue;
                bool low=target[axis]<lo[axis];
                target[axis]=low?lo[axis]:hi[axis];
                var normal=Vector3.zero;
                normal[axis]=low?1:-1;
                if(Vector3.Dot(lamp.direction,normal)>=0||!drifting)continue;
                lamp.direction=Vector3.Reflect(lamp.direction,normal);
                var contact=target;
                contact[axis]-=normal[axis]*ext[axis];
                if(lamp.isOn)edges.Impact(journey.Root.TransformPoint(contact),journey.Root.TransformDirection(normal),journey.floorSurface);
            }
            Vector3 offset = journey.Root.TransformVector(target - p);
            if (lamp.isOn && !drifting && runtime.HasEffect(CognitionSignal.LightDoorMove))
                offset = DirectedMotion.ApproachStep(offset, d => DirectedMotion.SphereStepClear(lamp.collider,d,false));
            else if (!DirectedMotion.SphereStepClear(lamp.collider,offset,false,out var worldNormal))
            {
                var normal=journey.Root.InverseTransformDirection(worldNormal);
                if (drifting && normal.sqrMagnitude>0 && Vector3.Dot(lamp.direction,normal)<0)
                    lamp.direction=Vector3.Reflect(lamp.direction,normal);
                return;
            }
            t.position += offset;
        }
    }
}
