using System.Collections.Generic;
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
        readonly List<Renderer> segments=new List<Renderer>();
        Material glow;
        Transform returnOutline;
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
            root.SetPositionAndRotation(new Vector3(wall.position.x,wall.position.y-tall*.5f,wall.position.z),Quaternion.LookRotation(outward));
            float centre=0;
            float left=centre-width*.5f,right=centre+width*.5f;
            var mat=cover.sharedMaterial;
            segments.Add(Block(root,"Wall left",new Vector3((-span*.5f+left)*.5f,tall*.5f,0),new Vector3(left+span*.5f,tall,thick),mat));
            segments.Add(Block(root,"Wall right",new Vector3((right+span*.5f)*.5f,tall*.5f,0),new Vector3(span*.5f-right,tall,thick),mat));
            segments.Add(Block(root,"Wall below opening",new Vector3(centre,sillHeight*.5f,0),new Vector3(width,sillHeight,thick),mat));
            float top=sillHeight+height;
            segments.Add(Block(root,"Wall above opening",new Vector3(centre,(top+tall)*.5f,0),new Vector3(width,tall-top,thick),mat));
            wallProperties=new MaterialPropertyBlock();
            Entrance=new GameObject("Passage entrance").transform;Entrance.SetParent(root,false);Entrance.localPosition=new Vector3(centre,sillHeight,0);
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
                returnOutline=new GameObject("Return entrance outline").transform;
                returnOutline.SetParent(journey.Root,false);
                returnGlow=new Material(edgeMaterial);returnGlow.SetFloat("_Visibility",1);
                returnGlow.SetColor("_Color",new Color(2.4f,2.4f,2.4f,1));
                AddOutline(returnOutline,returnGlow,thick*.5f+.07f);
                returnOutline.gameObject.SetActive(false);
            }
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
        void Update()
        {
            if(!cover)return;
            // Switch the visible mesh; the sill and the passage collision stay in place.

            bool covered=lamp.Brightness>.01f;
            if(!covered&&cover.enabled)
            {
                cover.GetPropertyBlock(wallProperties);
                foreach(var segment in segments)segment.SetPropertyBlock(wallProperties);
            }
            cover.enabled=covered;
            foreach(var segment in segments)segment.enabled=!covered;
            float darkness=1-Mathf.SmoothStep(0,1,lamp.Brightness);
            glow.SetFloat("_Visibility",darkness);
            if(returnOutline)returnOutline.gameObject.SetActive(journey.ViewBeyondChamber);
            var p=Entrance.InverseTransformPoint(lamp.entry.player.transform.position);
            if(!ReachedEntrance&&Mathf.Abs(p.x)<width*.5f&&p.z>.35f&&p.z<entranceDepth&&p.y>-.2f&&p.y<height)
            {ReachedEntrance=true;reachedEntrance.Invoke();}
        }
        void OnDestroy(){if(glow)Destroy(glow);if(returnGlow)Destroy(returnGlow);}
    }
}
