using UnityEngine;
using UnityEngine.Events;

namespace PuzzleApple.V5
{
    public sealed class WallPassage : MonoBehaviour
    {
        public ChamberLight lamp;
        public PassageJourney journey;
        public Transform[] walls;
        public Material edgeMaterial;
        [Min(.8f)] public float width=3.6f,height=3.8f;
        [Min(.01f)] public float edgeInset=.06f;
        [Min(.1f)] public float sillHeight=.95f;
        [Min(1)] public float entranceDepth=3;
        public UnityEvent reachedEntrance=new UnityEvent();
        public bool ReachedEntrance {get;private set;}
        public int SelectedWall {get;private set;}
        public Transform Entrance {get;private set;}
        Renderer cover;
        MaterialPropertyBlock wallProperties;
        Renderer openingWall;
        Mesh openingMesh;
        Material glow;
        Material coverInk;
        GameObject stairs;
        Material stairInk;
        Light stairLight;
        ProgressLock revealLock;
        public bool Revealed=>revealLock && revealLock.Unlocked;
        public float LightReveal=>1-Mathf.SmoothStep(0,1,lamp.Brightness);
        public float Reveal=>Revealed || journey&&journey.ViewBeyondChamber?1:LightReveal;
        Material returnGlow;
        void Start()
        {
            SelectedWall=Random.Range(0,walls.Length);var wall=walls[SelectedWall];
            cover=wall.GetComponent<Renderer>();wall.GetComponent<Collider>().enabled=false;
            bool x=wall.lossyScale.x<wall.lossyScale.z;
            float span=x?wall.lossyScale.z:wall.lossyScale.x;
            float tall=wall.lossyScale.y,thick=x?wall.lossyScale.x:wall.lossyScale.z;
            var outward=new Vector3(wall.position.x,0,wall.position.z).normalized;
            var root=new GameObject("Random passage - runtime").transform;root.SetParent(transform,false);
            var lockTemplate=Resources.Load<ProgressLock>("V5/PassageRevealProgressLock");
            if(journey && lockTemplate){revealLock=Instantiate(lockTemplate,root,false);revealLock.passageReveal=this;}
            root.SetPositionAndRotation(new Vector3(wall.position.x,wall.position.y-tall*.5f,wall.position.z),Quaternion.LookRotation(outward));
            float centre=0;
            float left=centre-width*.5f,right=centre+width*.5f;
            var mat=cover.sharedMaterial;
            WallCollider(root,"Wall left",new Vector3((-span*.5f+left)*.5f,tall*.5f,0),new Vector3(left+span*.5f,tall,thick));
            WallCollider(root,"Wall right",new Vector3((right+span*.5f)*.5f,tall*.5f,0),new Vector3(span*.5f-right,tall,thick));
            WallCollider(root,"Wall below opening",new Vector3(centre,sillHeight*.5f,0),new Vector3(width,sillHeight,thick));
            float top=sillHeight+height;
            WallCollider(root,"Wall above opening",new Vector3(centre,(top+tall)*.5f,0),new Vector3(width,tall-top,thick));
            var front=new GameObject("Wall with passage",typeof(MeshFilter),typeof(MeshRenderer));
            front.transform.SetParent(root,false);
            front.transform.localPosition=Vector3.up*(tall*.5f);
            front.transform.localScale=new Vector3(span,tall,thick);
            openingMesh=OpeningWallMesh.Build(width/span,sillHeight/tall,top/tall,false);
            front.GetComponent<MeshFilter>().sharedMesh=openingMesh;
            openingWall=front.GetComponent<Renderer>();
            openingWall.sharedMaterials=new[]{mat,mat};
            wallProperties=new MaterialPropertyBlock();
            Entrance=new GameObject("Passage entrance").transform;Entrance.SetParent(root,false);Entrance.localPosition=new Vector3(centre,sillHeight,0);
            BuildStairs(thick, mat);
            // Solid wall sections keep their depth and shadows. Only the aperture fades,
            // after the opaque exterior has rendered, so the path appears continuously.
            coverInk=new Material(mat);
            coverInk.SetFloat("_PassageCover",1);
            coverInk.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            coverInk.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            coverInk.SetFloat("_ZWrite",0);
            // Composite the aperture over corridor stars even when their renderer
            // centres sort nearer than the centre of the full wall.
            coverInk.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent+1;
            coverInk.SetShaderPassEnabled("DepthOnly",false);
            coverInk.SetShaderPassEnabled("DepthNormals",false);
            cover.sharedMaterial=coverInk;
            cover.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            if(journey)journey.Build(Entrance,width,height,thick,edgeMaterial);
            else
            {
            float start=-thick*.5f+.025f;
            Block(Entrance,"Landing",new Vector3(0,-.1f,(entranceDepth+start)*.5f),new Vector3(width,.2f,entranceDepth-start),mat);
            Block(Entrance,"Passage ceiling",new Vector3(0,height+.1f,entranceDepth*.5f),new Vector3(width,.2f,entranceDepth),mat);
            Block(Entrance,"Left boundary",new Vector3(-width*.5f-.1f,height*.5f,entranceDepth*.5f),new Vector3(.2f,height,entranceDepth),mat);
            Block(Entrance,"Right boundary",new Vector3(width*.5f+.1f,height*.5f,entranceDepth*.5f),new Vector3(.2f,height,entranceDepth),mat);
            Block(Entrance,"Temporary passage end",new Vector3(0,height*.5f,entranceDepth),new Vector3(width,height,.2f),mat);
            }
            glow=new Material(edgeMaterial);

            AddOutline(Entrance,glow,-thick*.5f-edgeInset);
            if(journey)
            {
                var returnOutline=new GameObject("Return entrance outline").transform;
                returnOutline.SetParent(journey.Root,false);
                returnGlow=new Material(edgeMaterial);returnGlow.SetFloat("_Visibility",1);
                returnGlow.SetColor("_Color",new Color(2.4f,2.4f,2.4f,1));
                AddOutline(returnOutline,returnGlow,thick*.5f+.07f);
                returnGlow.renderQueue=coverInk.renderQueue-1;
            }
        }
        void BuildStairs(float wallThickness, Material surface)
        {
            stairs = new GameObject("Entrance stairs");
            stairs.transform.SetParent(Entrance, false);
            stairInk = new Material(surface);
            stairInk.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            stairInk.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            stairInk.SetFloat("_ZWrite", 0);
            stairInk.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Light spilling in from the threshold reaches the upper treads first.
            stairLight = new GameObject("Light from corridor", typeof(Light)).GetComponent<Light>();
            stairLight.transform.SetParent(stairs.transform, false);
            stairLight.transform.localPosition = new Vector3(0, .92f, -wallThickness * .5f - .16f);
            stairLight.transform.localRotation = Quaternion.Euler(90, 0, 0);
            stairLight.type = LightType.Spot;
            stairLight.range = 2.9f;
            stairLight.spotAngle = 112f;
            stairLight.innerSpotAngle = 110f;
            stairLight.shadows = LightShadows.Hard;

            const float tread = .32f;
            int count = Mathf.CeilToInt(sillHeight / .2f);
            for (int i = 0; i < count; i++)
            {
                float top = sillHeight * (i + 1) / count;
                float z = -wallThickness * .5f - (count - i - .5f) * tread;
                Block(stairs.transform, "Tread " + (i + 1),
                    new Vector3(0, -sillHeight + top * .5f, z), new Vector3(width, top, tread), stairInk);
            }
            UpdateStairs();
        }

        void UpdateStairs()
        {
            // Once revealed, the entrance and its walking access remain available after relighting.
            float dissolve = Revealed?0:1-LightReveal;
            stairInk.SetFloat("_Dissolve", dissolve);
            float corridorLight = journey && journey.Stars ? journey.Stars.Brightness : 1;
            stairLight.intensity = 2.2f * (1 - dissolve) * corridorLight;
            if (stairs.activeSelf != (dissolve < 1)) stairs.SetActive(dissolve < 1);
        }

        void AddOutline(Transform parent,Material material,float depth)
        {
            foreach(float sign in new[]{-1f,1f})
            {
                CreateFilament(parent,material,"Vertical white filaments",new Vector3(sign*(width*.5f-.05f),height*.5f,depth),height);
                var cross=CreateFilament(parent,material,"Horizontal white filaments",new Vector3(0,sign<0?.05f:height-.05f,depth),width);
                cross.transform.localRotation=Quaternion.Euler(0,0,90);
            }
        }
        public static Renderer CreateFilament(Transform parent,Material material,string name,Vector3 position,float length)
        {
            // A single plane gives one unambiguous UV flow axis, without cube-face repeats.
            var obj=GameObject.CreatePrimitive(PrimitiveType.Quad);obj.name=name;
            obj.transform.SetParent(parent,false);obj.transform.localPosition=position;
            obj.transform.localScale=new Vector3(.1f,length,1);
            obj.GetComponent<Collider>().enabled=false;Destroy(obj.GetComponent<Collider>());
            var renderer=obj.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return renderer;
        }
        static Renderer Block(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=size;
            var renderer=obj.GetComponent<Renderer>();renderer.sharedMaterial=material;return renderer;
        }
        static void WallCollider(Transform parent,string name,Vector3 position,Vector3 size)
        {
            var obj=new GameObject(name,typeof(BoxCollider));
            obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;
            obj.transform.localScale=size;
        }
        void Update()
        {
            if(!cover)return;
            UpdateStairs();
            float darkness=Reveal;
            cover.GetPropertyBlock(wallProperties);
            wallProperties.SetVector("_PassageOrigin",Entrance.position);
            wallProperties.SetVector("_PassageRight",Entrance.right);
            wallProperties.SetVector("_PassageUp",Entrance.up);
            wallProperties.SetVector("_PassageSize",new Vector4(width,height,0,0));
            wallProperties.SetFloat("_PassageOpacity",1-darkness);
            cover.SetPropertyBlock(wallProperties);
            // Sections use their own local inward normal and must never inherit the cover mask.
            wallProperties.SetVector("_RoomShellInward",Vector3.back);
            openingWall.SetPropertyBlock(wallProperties);
            cover.enabled=darkness<1;
            float corridorGlow=journey&&journey.Stars?Mathf.Lerp(.09f,1,journey.Stars.Brightness):1;
            glow.SetFloat("_Visibility",darkness*corridorGlow);
            if(returnGlow)returnGlow.SetFloat("_Visibility",corridorGlow);
            var p=Entrance.InverseTransformPoint(lamp.entry.player.transform.position);
            if(!ReachedEntrance&&Mathf.Abs(p.x)<width*.5f&&p.z>.35f&&p.z<entranceDepth&&p.y>-.2f&&p.y<height)
            {ReachedEntrance=true;reachedEntrance.Invoke();}
        }
        void OnDestroy(){if(glow)Destroy(glow);if(returnGlow)Destroy(returnGlow);if(coverInk)Destroy(coverInk);if(openingMesh)Destroy(openingMesh);if(stairInk)Destroy(stairInk);}
    }
}
