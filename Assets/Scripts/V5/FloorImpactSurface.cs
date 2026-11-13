using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class FloorImpactSurface : MonoBehaviour
    {
        public PassageJourney journey;
        public Material floorMaterial;
        [Header("Carved wall mural")]
        [Tooltip("Linear RGB tangent normals, alpha recess occlusion. Uses the bundled bake when empty.")]
        public Texture2D muralNormal;
        public Vector2 muralSize=new Vector2(11,3.1875f);
        [UnityEngine.Serialization.FormerlySerializedAs("hintHeight")]
        [Range(0,1)] public float muralHeight=.5f;
        [Range(0,1)] public float muralRelief=.7f,muralCavity=.42f;
        [Tooltip("Local mural shading direction: horizontal, vertical, out of wall.")]
        public Vector3 muralLightDirection=new Vector3(-.42f,.64f,.64f);
        public bool MuralAvailable=>muralNormal;
        [Min(.1f)] public float rippleDuration=1.2f,rippleSpeed=1.6f;
        [Range(1,4)] public int ringCount=2;
        [Min(.01f)] public float ringSpacing=.34f,ringWidth=.03f;
        [Range(0,1)] public float rippleStrength=.9f;
        [Min(0)] public float thinningStartsAt=.65f;
        [Min(.1f)] public float thinningDistance=1.1f;
        [Range(.05f,1)] public float distantWidth=.18f;
        public bool contactParticles=true;
        [Header("Lighting style trial")]
        public bool steppedLighting=false;
        [Range(2,8)] public int lightSteps=4;
        [Range(0,1)] public float lightStepBlend=.9f;
        [Tooltip("Band only the dim outer light; brighter areas retain smooth lighting.")]
        [Range(.1f,.8f)] public float lightBandEdge=.45f;
        [Range(0,12)] public int particleCount=5;
        public Vector2 particleSize=new Vector2(.07f,.16f);
        [Min(.05f)] public float particleLifetime=.65f;
        [Min(1)] public float holeSize=3.5f;
        [Min(.1f)] public float holeOpenDuration=1.4f;
        public float HoleProgress {get;private set;}
        bool holeOpening;
        public bool HoleOpen {get;private set;}
        public Renderer Surface {get;private set;}
        public Collider FloorCollider {get;private set;}
        public int ImpactCount {get;private set;}
        public int WallImpactCount {get;private set;}
        public bool HasWalls=>walls.Count>0;
        sealed class WallInk
        {
            public Collider collider;public Material material;public int plane;
        }
        readonly System.Collections.Generic.List<WallInk> walls=new System.Collections.Generic.List<WallInk>();
        readonly Vector4[] hits=new Vector4[16];
        int nextHit;
        Material ink;
        ParticleSystem particles;
        readonly System.Collections.Generic.List<Collider> rim=new System.Collections.Generic.List<Collider>();
        public void Build(Renderer surface)
        {
            Surface=surface;FloorCollider=surface.GetComponent<Collider>();
            if(!muralNormal)muralNormal=Resources.Load<Texture2D>("V5/Murals/GravityMuralNormal");
            ink=new Material(floorMaterial);Surface.sharedMaterial=ink;
            for(int i=0;i<hits.Length;i++)hits[i]=new Vector4(0,0,0,-1000);
            var go=new GameObject("Floor impact squares",typeof(ParticleSystem));go.transform.SetParent(journey.Root,false);
            particles=go.GetComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.maxParticles=96;main.startColor=Color.black;main.gravityModifier=0;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var shrink=particles.sizeOverLifetime;shrink.enabled=true;shrink.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;
            renderer.mesh=surface.GetComponent<MeshFilter>().sharedMesh;renderer.sharedMaterial=journey.blackSurround;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        public bool Owns(Collider collider)=>collider&&(collider==FloorCollider||rim.Contains(collider));
        public void BuildWalls()
        {
            foreach(Transform child in journey.Root)
            {
                if(child.name!="Room far wall"&&child.name!="Room side wall"&&child.name!="Door wall"&&child.name!="Door lintel"&&child.name!="Room ceiling")continue;
                var wall=new WallInk{collider=child.GetComponent<Collider>(),material=new Material(floorMaterial),plane=child.name=="Room ceiling"?0:child.name=="Room side wall"?2:1};
                wall.material.SetFloat("_Plane",wall.plane);wall.material.SetVector("_Hole",Vector4.zero);
                bool mural=child.name=="Room far wall"&&muralNormal;
                wall.material.SetFloat("_MuralEnabled",mural?1:0);
                if(mural)
                {
                    wall.material.SetTexture("_MuralNormal",muralNormal);
                    var catalog=journey.entry.board.Catalog;
                    wall.material.SetTexture("_HintLight",catalog.Word("light").Symbol.texture);
                    var floorSymbol=catalog.Word("floor").Symbol;
                    wall.material.SetTexture("_HintFloor",floorSymbol.texture);
                    float bottomPadding=UnityEngine.Sprites.DataUtility.GetPadding(floorSymbol).y/floorSymbol.rect.height;
                    wall.material.SetFloat("_HintFloorUVOffset",bottomPadding-Cognition.SymbolUI.FloorBottomInset);
                    wall.material.SetTexture("_HintMove",catalog.Word("move").Symbol.texture);
                }
                child.GetComponent<Renderer>().sharedMaterial=wall.material;walls.Add(wall);
            }
        }
        public bool ImpactWall(Collider surface,Vector3 point,Vector3 normal)
        {
            foreach(var wall in walls)
            {
                if(wall.collider!=surface)continue;
                RecordImpact(point);WallImpactCount++;
                EmitSquares(point,normal);return true;
            }
            return false;
        }
        public void OpenHole()
        {
            if(HoleOpen||holeOpening||!FloorCollider)return;
            holeOpening=true;
            for(int i=0;i<4;i++)Rim(Vector3.zero,Vector3.one);
            UpdateHole(0);FloorCollider.enabled=false;
        }
        public float HoleWidth=>Mathf.Clamp(holeSize,1,Mathf.Min(journey.roomSize.x,journey.roomSize.z)-2);
        public float HoleCentreZ=>journey.pathLength+journey.roomSize.z*.5f;
        public bool FitsOpening(Vector3 local,float radius)
        {
            float clearance=HoleWidth*.5f*Mathf.SmoothStep(0,1,HoleProgress)-radius;
            return clearance>0&&Mathf.Abs(local.x)<clearance&&Mathf.Abs(local.z-HoleCentreZ)<clearance;
        }
        void UpdateHole(float progress)
        {
            float size=HoleWidth*Mathf.SmoothStep(0,1,progress);
            float half=size*.5f,width=journey.roomSize.x,front=journey.pathLength-.1f,back=journey.pathLength+journey.roomSize.z;
            float centre=HoleCentreZ;
            SetRim(0,new Vector3(-(width*.5f+half)*.5f,-.15f,(front+back)*.5f),new Vector3((width-size)*.5f,.3f,back-front));
            SetRim(1,new Vector3((width*.5f+half)*.5f,-.15f,(front+back)*.5f),new Vector3((width-size)*.5f,.3f,back-front));
            SetRim(2,new Vector3(0,-.15f,(front+centre-half)*.5f),new Vector3(Mathf.Max(.001f,size),.3f,centre-half-front));
            SetRim(3,new Vector3(0,-.15f,(centre+half+back)*.5f),new Vector3(Mathf.Max(.001f,size),.3f,back-centre-half));
            ink.SetVector("_Hole",new Vector4(0,centre,half,progress>0?1:0));
        }
        void SetRim(int index,Vector3 position,Vector3 size)
        {var box=(BoxCollider)rim[index];box.transform.localPosition=position;box.size=size;}
        void Rim(Vector3 position,Vector3 size)
        {
            var go=new GameObject("Floor rim collision",typeof(BoxCollider));go.transform.SetParent(journey.Root,false);go.transform.localPosition=position;
            var box=go.GetComponent<BoxCollider>();box.size=size;rim.Add(box);
        }
        public void Impact(Vector3 worldPoint,Vector3 normal)
        {
            if(!ink)return;
            RecordImpact(worldPoint);ImpactCount++;
            EmitSquares(worldPoint,normal);
        }
        void RecordImpact(Vector3 worldPoint)
        {
            var p=journey.Root.InverseTransformPoint(worldPoint);
            // All room planes sample the same expanding 3D wave, including at seams.
            hits[nextHit]=new Vector4(p.x,p.y,p.z,Time.time);nextHit=(nextHit+1)%hits.Length;
        }
        void EmitSquares(Vector3 worldPoint,Vector3 normal)
        {
            if(journey.Brightness>.001f)EmitContactSquares(worldPoint,normal);
        }
        public void EmitContactSquares(Vector3 worldPoint,Vector3 normal)
        {
            if(!contactParticles||!particles)return;
            if(!particles.isPlaying)particles.Play();
            var orientation=Quaternion.FromToRotation(Vector3.up,normal);
            const float spreadDegrees=55f;
            for(int i=0;i<particleCount;i++)
            {
                var p=new ParticleSystem.EmitParams();p.position=worldPoint+normal*.02f;
                float axial=Random.Range(Mathf.Cos(spreadDegrees*Mathf.Deg2Rad),1f);
                float radial=Mathf.Sqrt(1f-axial*axial);
                float angle=Random.value*Mathf.PI*2f;
                var direction=new Vector3(radial*Mathf.Cos(angle),axial,radial*Mathf.Sin(angle));
                p.velocity=orientation*direction*Random.Range(.52f,.98f);
                p.startSize=Random.Range(particleSize.x,particleSize.y);p.startLifetime=particleLifetime;p.startColor=Color.black;
                p.rotation3D=Random.insideUnitSphere*180;
                particles.Emit(p,1);
            }
        }
        void LateUpdate()
        {
            if(!ink)return;
            if(holeOpening){HoleProgress=Mathf.MoveTowards(HoleProgress,1,Time.deltaTime/holeOpenDuration);UpdateHole(HoleProgress);if(HoleProgress>=1){holeOpening=false;HoleOpen=true;}}
            Configure(ink,hits);
            foreach(var wall in walls)Configure(wall.material,hits);
        }
        void Configure(Material ink,Vector4[] impacts)
        {
            ink.SetMatrix("_WorldToFloor",journey.Root.worldToLocalMatrix);ink.SetVectorArray("_Impacts",impacts);
            ink.SetVector("_RoomBounds",new Vector4(journey.roomSize.x*.5f-.15f,journey.roomSize.y,journey.pathLength+.15f,journey.roomSize.z-.3f));
            ink.SetFloat("_RoomOpeningWidth",journey.PathWidth);
            ink.SetFloat("_Brightness",1);
            ink.SetFloat("_RippleDuration",rippleDuration);ink.SetFloat("_RippleSpeed",rippleSpeed);
            ink.SetFloat("_RingCount",ringCount);ink.SetFloat("_RingSpacing",ringSpacing);ink.SetFloat("_RingWidth",ringWidth);ink.SetFloat("_RippleStrength",rippleStrength);
            ink.SetVector("_RippleTaper",new Vector4(thinningStartsAt,thinningDistance,distantWidth,0));
            ink.SetFloat("_LightSteps",lightSteps);ink.SetFloat("_LightStepBlend",steppedLighting?lightStepBlend:0);
            ink.SetFloat("_LightBandEdge",lightBandEdge);
            ink.SetVector("_MuralRect",new Vector4(0,journey.roomSize.y*muralHeight,Mathf.Max(.1f,muralSize.x),Mathf.Max(.1f,muralSize.y)));
            ink.SetVector("_MuralLight",muralLightDirection);
            ink.SetFloat("_MuralRelief",muralRelief);ink.SetFloat("_MuralCavity",muralCavity);
        }
        void OnDestroy(){if(ink)Destroy(ink);foreach(var wall in walls)if(wall.material)Destroy(wall.material);}
    }
}
