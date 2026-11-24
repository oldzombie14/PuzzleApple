using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    [DefaultExecutionOrder(-100)]
    public sealed class StarLift : MonoBehaviour
    {
        [Min(6)] public float whiteLayerHeight=13;
        [Min(.5f)] public float starSpeed=1.7f, driftSpeed=1.9f;
        [Min(.1f)] public float fadeSeconds=.75f, departureDelay=.85f, accelerationSeconds=1.3f;
        [Min(.2f)] public float clearDistance=2.8f, refreshDelay=.55f;
        [Min(2)] public float driftRange=7;
        public Texture2D inscription;
        readonly Vector3 dock=new Vector3(4.95f,-.14f,0);
        sealed class Deck
        {
            public Renderer surface;
            public BoxCollider solid;
            public Vector3 heading;
            public float age, speed, travelled, fade, turnIn;
            public bool retiring;
        }
        struct Star
        {
            public Transform body;
            public float speed, pickRadius;
        }
        readonly List<Deck> decks=new List<Deck>();
        readonly List<Star> stars=new List<Star>();
        ChapterLesson lesson;
        OutdoorPassage passage;
        FirstPersonController player;
        Material ink, starMaterial, trailMaterial, inscriptionMaterial, solidPlatform, fadingPlatform;
        Mesh wakeMesh;
        Renderer whiteLayer;
        MaterialPropertyBlock props;
        SentenceMotionOutput floorMotion;
        Deck newest;
        float emptyTime, fallTime, starEmission;
        bool lostRide;
        public bool IsDeck(Collider c)=>decks.Exists(d=>d.solid==c);
        public void Build(ChapterLesson owner,OutdoorPassage route,Material lessonInk)
        {
            lesson=owner;passage=route;player=route.entry.player;ink=lessonInk;
            props=new MaterialPropertyBlock();
            var e=route.entry;
            var c=SentenceCapabilityCatalog.Default;
            floorMotion=SentenceMotionOutput.Bind(e.Entities,this,()=>ChapterLesson.LessonSpace,c.position,
                m=>m==SentenceMotion.Floor,(m,g)=>lesson.Openness>0 || lesson.InStarfield);
            floorMotion.BindIllumination(SentenceMotion.Floor,()=>e.sentenceRuntime.Emits(c.floor,ChapterLesson.LessonSpace));
            solidPlatform=e.journey.whitePath;
            fadingPlatform=new Material(solidPlatform);
            fadingPlatform.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            fadingPlatform.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            fadingPlatform.SetFloat("_ZWrite",0);
            fadingPlatform.renderQueue=(int)RenderQueue.Transparent;
            BuildStars(e);
            whiteLayer=ChapterLesson.Block(transform,"White light layer",new Vector3(12,whiteLayerHeight+1.7f,0),
                new Vector3(65,.045f,65),ink,false);
            SpawnDeck(true);
        }
        void BuildStars(WorldEntry e)
        {
            var mesh=e.journey.Stars.GetComponentInChildren<MeshFilter>(true).sharedMesh;
            var sign=GameObject.CreatePrimitive(PrimitiveType.Quad);
            sign.name="Constellation floor mirror light";
            sign.transform.SetParent(transform,false);
            sign.transform.localPosition=new Vector3(29,5.6f,0);
            sign.transform.localRotation=Quaternion.Euler(0,90,0);
            sign.transform.localScale=new Vector3(19.2f,4.8f,1);
            sign.GetComponent<Collider>().enabled=false;
            inscriptionMaterial=new Material(ink);
            inscriptionMaterial.SetTexture("_BaseMap",inscription);
            sign.GetComponent<Renderer>().sharedMaterial=inscriptionMaterial;
            var quad=sign.GetComponent<MeshFilter>().sharedMesh;
            // A fixed brush canvas below the diamond. The shader masks individual strokes
            // within it; changing their length does not transform the canvas or its pattern.
            wakeMesh=Instantiate(quad);
            wakeMesh.name="Star wake canvas";
            var vertices=wakeMesh.vertices;
            float height=mesh.bounds.size.y;
            for(int i=0;i<vertices.Length;i++){
                vertices[i].x*=height*.50f;
                vertices[i].y=mesh.bounds.min.y+height*((vertices[i].y-.5f)*9-.025f);
            }
            wakeMesh.vertices=vertices;wakeMesh.RecalculateBounds();
            starMaterial=new Material(ink);
            trailMaterial=new Material(ink);
            trailMaterial.SetFloat("_DrawWake",1);
            var style=StarGlowStyle.Load();
            starEmission=style?style.emission:1;
            var layout=new System.Random(1451);
            float Between(float a,float b)=>Mathf.Lerp(a,b,(float)layout.NextDouble());
            void Place(Vector3 p,float size,float speed,float tone){
                var star=ChapterLesson.Child(transform,"Rising star",p);
                star.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=star.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=starMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                props.Clear();props.SetFloat("_Intensity",tone);renderer.SetPropertyBlock(props);
                star.localScale=Vector3.one*(size/height);
                var wake=ChapterLesson.Child(star,"Star wake",Vector3.zero);
                wake.gameObject.AddComponent<MeshFilter>().sharedMesh=wakeMesh;
                var strokes=wake.gameObject.AddComponent<MeshRenderer>();
                strokes.sharedMaterial=trailMaterial;strokes.shadowCastingMode=ShadowCastingMode.Off;
                props.SetFloat("_WakeEnd",Between(.66f,1));
                props.SetFloat("_StrokeSeed",Between(1,99));strokes.SetPropertyBlock(props);
                stars.Add(new Star{body=star,speed=speed,pickRadius=Mathf.Max(.22f,size*.5f)});
            }
            // Cover the full sky at three depths. Jitter azimuth and height independently;
            // occasional smaller companions share a speed so their loose spacing survives the ride.
            for(int layer=0;layer<3;layer++){
                int count=27+layer*15;
                float offset=Between(0,1);
                for(int i=0;i<count;i++){
                    float angle=(offset+(i+Between(-.42f,.42f))/count)*2*Mathf.PI;
                    float distance=Between(9+layer*9,17+layer*14);
                    float y=Mathf.Repeat(offset+i*.618034f+Between(-.09f,.09f),1)*52-17;
                    var p=new Vector3(dock.x+Mathf.Cos(angle)*distance,y,Mathf.Sin(angle)*distance);
                    float size=Mathf.Lerp(.34f,1.04f,Mathf.Pow(Between(0,1),1.25f))*(1+.12f*layer);
                    float speed=starSpeed*Between(.79f,2.38f);
                    Place(p,size,speed,Between(.57f,1));
                    if(i%11==4)Place(p+new Vector3(Between(-1.4f,1.4f),Between(1.2f,2.8f),Between(1,2.2f)),
                        size*.69f,speed,Between(.40f,.68f));
                }
            }
            // A staggered foreground beside the dock. Each set keeps its vertical spacing
            // as it cycles, so stars still pass close after the player waits or rides up.
            for(int band=0;band<3;band++){
                float phase=Between(0,1);
                float speed=starSpeed*(1.23f+.36f*band);
                for(int i=0;i<10;i++){
                    float distance=Between(2.8f+band*.5f,4.1f+band*1.25f);
                    float angle=Between(27,100)*Mathf.Deg2Rad*((i+band)%2==0?1:-1);
                    float y=Mathf.Repeat(phase+(i+Between(-.25f,.25f))*.1f,1)*52-17;
                    var p=new Vector3(dock.x+Mathf.Cos(angle)*distance,y,Mathf.Sin(angle)*distance);
                    Place(p,Between(.26f,.54f),speed,Between(.72f,1));
                }
            }
        }
        public bool Pick(Ray ray,float reach,out Renderer picked)
        {
            picked=null;
            if(Physics.Raycast(ray,out var wall,reach,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))reach=wall.distance;
            foreach(var star in stars){
                var offset=star.body.position-ray.origin;
                float depth=Vector3.Dot(offset,ray.direction);
                if(depth<=0 || depth>=reach)continue;
                if((offset-ray.direction*depth).sqrMagnitude>star.pickRadius*star.pickRadius)continue;
                reach=depth;picked=star.body.GetComponent<Renderer>();
            }
            return picked && lesson.StarBrightness>0;
        }
        void SpawnDeck(bool ready=false)
        {
            var surface=ChapterLesson.Block(transform,"Starfield floor",dock,new Vector3(3.7f,.28f,2.55f),solidPlatform);
            var deck=new Deck{surface=surface,solid=surface.GetComponent<BoxCollider>(),heading=Random.onUnitSphere,
                age=ready?fadeSeconds:0};
            deck.heading.x=Mathf.Abs(deck.heading.x);
            decks.Add(deck);newest=deck;emptyTime=0;
            Draw(deck,ready?1:0);
        }
        void Draw(Deck deck,float visibility)
        {
            var material=visibility>=1?solidPlatform:fadingPlatform;
            if(deck.surface.sharedMaterial!=material)deck.surface.sharedMaterial=material;
            var color=Color.white;
            color.a=visibility;
            props.Clear();props.SetColor("_BaseColor",color);
            props.SetFloat("_PlayableFloor",1);
            props.SetFloat("_Dissolve",1-visibility);
            deck.surface.SetPropertyBlock(props);
        }
        public void Respawn()
        {
            if(newest==null || newest.retiring || Vector3.Distance(newest.surface.transform.localPosition,dock)>.45f)
                SpawnDeck();
            newest.age=0;newest.speed=0;fallTime=0;lostRide=false;
            player.Teleport(transform.TransformPoint(dock+Vector3.up*.28f),transform.rotation*Quaternion.Euler(0,90,0));
        }
        void Update()
        {
            if(!lesson || !passage.InLesson || passage.Transitioning || passage.entry.Busy)return;
            float dt=Time.deltaTime;
            float eyeHeight=transform.InverseTransformPoint(player.view.position).y;
            float veil=Mathf.SmoothStep(0,.72f,Mathf.InverseLerp(whiteLayerHeight-2,whiteLayerHeight+1.6f,eyeHeight));
            ChapterLesson.Tint(whiteLayer,props,new Color(1,1,1,veil));
            bool rise=lesson.Effect(CognitionSignal.FloorMirrorLight) && lesson.StarBrightness>0;
            bool wander=floorMotion.HasMotion(SentenceMotion.Floor);
            // Closed geometry hides the sky but also prevents an outdoor floor command
            // from exhausting the dock before the charge door is first opened.
            if(lesson.Openness>0 || lesson.InStarfield)unlocked=true;
            float brightness=starEmission*lesson.StarBrightness*passage.entry.sentenceRuntime.LampBoost(ChapterLesson.LessonSpace);
            var starlight=new Color(brightness,brightness,brightness,lesson.StarBrightness);
            starMaterial.SetColor("_BaseColor",starlight);trailMaterial.SetColor("_BaseColor",starlight);
            foreach(var star in stars){
                var p=star.body.localPosition;
                p.y+=dt*star.speed;
                if(p.y>=35)p.y=Mathf.Repeat(p.y+17,52)-17;
                star.body.localPosition=p;
                // The Intro diamond is flat. Keep its vertical axis upright as the observer moves.
                var facing=Vector3.ProjectOnPlane(star.body.position-player.view.position,Vector3.up);
                if(facing.sqrMagnitude>.0001f)star.body.rotation=Quaternion.LookRotation(facing);
            }
            if(!unlocked)return;
            for(int i=decks.Count-1;i>=0;i--){
                var deck=decks[i];
                Tick(deck,rise,wander,dt);
                if(deck.retiring && deck.fade>=1){Destroy(deck.surface.gameObject);decks.RemoveAt(i);}
            }
            if(newest==null || newest.retiring || Vector3.Distance(newest.surface.transform.localPosition,dock)>clearDistance){
                emptyTime+=dt;
                if(emptyTime>=refreshDelay)SpawnDeck();
            }else emptyTime=0;
            // A failed ride can end well above the dock. Count the visible fall from release.
            if(lostRide && player.VerticalVelocity<0)fallTime+=dt;
            float y=transform.InverseTransformPoint(player.transform.position).y;
            if(y < -3.8f || fallTime>=1.65f)passage.FinishLesson(false);
        }
        bool unlocked;
        void Tick(Deck deck,bool rise,bool wander,float dt)
        {
            deck.age+=dt;
            var t=deck.surface.transform;
            var position=t.localPosition;
            bool rider=player.StandingOn(deck.solid);
            if(rider && !deck.retiring){lostRide=false;fallTime=0;}
            if(deck.retiring){
                deck.fade=Mathf.Min(1,deck.fade+dt/fadeSeconds);
                Draw(deck,1-deck.fade);
                if(deck.fade>=1){if(rider)lostRide=true;deck.solid.enabled=false;}
                return;
            }
            Draw(deck,Mathf.Clamp01(deck.age/fadeSeconds));
            if(deck.age<fadeSeconds+departureDelay || !rise && !wander){deck.speed=0;return;}
            deck.speed=Mathf.MoveTowards(deck.speed,1,dt/accelerationSeconds);
            Vector3 delta;
            if(rise){
                delta=Vector3.up*(starSpeed*deck.speed*dt);
            }else{
                deck.turnIn-=dt;
                if(deck.turnIn<=0){
                    deck.heading=(deck.heading+Random.insideUnitSphere*.42f).normalized;
                    deck.turnIn=Random.Range(.45f,1.15f);
                }
                // Reflect the drifting board away from the doorway and the lower sky boundary.
                if(position.x<=dock.x && deck.heading.x<0)deck.heading.x=-deck.heading.x;
                if(position.y<dock.y-.4f && deck.heading.y<0)deck.heading.y=-deck.heading.y;
                if(Mathf.Abs(position.z)>4 && position.z*deck.heading.z>0)deck.heading.z=-deck.heading.z;
                delta=deck.heading*(driftSpeed*deck.speed*dt);
                deck.travelled+=delta.magnitude;
            }
            player.CarryOn(deck.solid,transform.TransformVector(delta));
            t.localPosition=position+delta;
            Physics.SyncTransforms();
            if(rise && t.localPosition.y>=whiteLayerHeight){
                if(rider)passage.FinishLesson(true);
                else deck.retiring=true;
            }else if(!rise && (deck.travelled>=driftRange || t.localPosition.y>=whiteLayerHeight-3))
                deck.retiring=true;
        }
        void OnDestroy()
        {
            if(wakeMesh)Destroy(wakeMesh);
            if(inscriptionMaterial)Destroy(inscriptionMaterial);
            if(trailMaterial)Destroy(trailMaterial);
            if(fadingPlatform)Destroy(fadingPlatform);
            if(starMaterial)Destroy(starMaterial);
        }
    }
}
