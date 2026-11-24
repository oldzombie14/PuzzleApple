using System.Linq;
using PuzzleApple.V3.Cognition;
using UnityEngine;

namespace PuzzleApple.V3
{
    [DefaultExecutionOrder(350)]
    public sealed class MainRoute : MonoBehaviour
    {
        public OpeningRoom room;
        public Transform gateFrame;
        public Transform[] doorLeaves;
        public PassageBarrier gateBarrier;
        public Renderer indicator;
        public Transform pressurePlate, balance, beam, passageSeal;
        public Transform[] trays;
        public DestinationSlots trayPlaces;
        public OpeningObject apple;
        public RoomLighting galleryLighting;
        public CutscenePlayer completion;
        public CutscenePlayer insufficientWeight;
        public ParticleSystem celebration;
        [Min(0)] public float minimumTotalWeight=2;
        [Range(0,1)] public float completionPress,completionExit;
        public BoxCollider exitVolume;
        public float blinkInterval=1.6f, gateTravel=1.68f, idleGateTravel=.07f, stableDuration=1.2f, pressDepth=.1f;
        public bool Solved { get; private set; }
        public bool GateOpen { get; private set; }
        public bool ReachedExit { get; private set; }
        public float LeftWeight { get; private set; }
        public float RightWeight { get; private set; }
        public int InsufficientAttempts { get; private set; }
        Vector3[] closed, trayRest;
        Vector3 plateRest, balanceRest, sealRest;
        Quaternion beamRest;
        float gateAmount, stableTime, tilt, press;
        MaterialPropertyBlock lamp;
        bool completionFinished;
        bool insufficientShown;

        public void Initialize()
        {
            if(closed!=null)return;
            closed=doorLeaves.Select(t=>t.localPosition).ToArray();
            trayRest=trays.Select(t=>t.localPosition).ToArray();
            plateRest=pressurePlate.localPosition;balanceRest=balance.localPosition;
            beamRest=beam.localRotation;sealRest=passageSeal.localPosition;
            lamp=new MaterialPropertyBlock();
        }
        void Awake()=>Initialize();
        void Start()=>room.player.gameObject.AddComponent<V3PlayerAppearance>().Initialize(room.player,apple.transform,room.presentation.view);
        void Update()
        {
            if(!room.presentation.Ready)return;
            Tick(Time.deltaTime,Time.time);
        }
        public void SetLamp(bool lit)
        {
            if(lamp==null)lamp=new MaterialPropertyBlock();
            indicator.GetPropertyBlock(lamp);
            lamp.SetColor("_BaseColor",lit?Color.white:Color.black);
            indicator.SetPropertyBlock(lamp);
        }
        public bool LampLit(float clock)=>room.State.HasEffect(CognitionSignal.DoorPositive)||
            (!room.State.HasEffect(CognitionSignal.DoorNegative)&&((int)(clock/blinkInterval)&1)==0);
        public void Tick(float dt,float clock)
        {
            Initialize();
            bool lit=LampLit(clock);SetLamp(lit);
            bool positive=room.State.HasEffect(CognitionSignal.DoorPositive);
            var body=room.player.GetComponent<CharacterController>();
            gateBarrier.SetPassable(positive&&gateAmount>=.999f,body);
            float target=positive||gateBarrier.WaitingForClearance?1:lit?Mathf.Clamp01(idleGateTravel/gateTravel):0;
            float rate=positive||gateAmount>idleGateTravel/gateTravel?1.6f:1.6f*idleGateTravel/gateTravel;
            gateAmount=Mathf.MoveTowards(gateAmount,target,dt*rate);
            GateOpen=positive&&gateAmount>=.999f;
            gateBarrier.SetPassable(GateOpen,body);
            if(galleryLighting)galleryLighting.Tick(GateOpen,dt);
            for(int i=0;i<doorLeaves.Length;i++)
                doorLeaves[i].localPosition=closed[i]+Vector3.forward*(i==0?1:-1)*gateTravel/doorLeaves[i].parent.lossyScale.z*gateAmount;
            var left=Weight(0);var right=Weight(1);
            if(!Mathf.Approximately(left,LeftWeight)||!Mathf.Approximately(right,RightWeight))
            {stableTime=0;insufficientShown=false;}
            LeftWeight=left;RightWeight=right;
            bool balanced=LeftWeight>0&&Mathf.Abs(LeftWeight-RightWeight)<.001f;
            bool heavyEnough=LeftWeight+RightWeight>=minimumTotalWeight-.001f;
            tilt=Mathf.MoveTowards(tilt,Solved?0:Mathf.Clamp(LeftWeight-RightWeight,-1,1)*7,dt*12);
            beam.localRotation=beamRest*Quaternion.Euler(tilt,0,0);
            var prior=trays.Select(t=>t.position).ToArray();
            var supported=Enumerable.Range(0,2).Select(i=>room.objects.Where(o=>o&&o.wordId=="apple"&&trayPlaces.Contains(i,o)).ToArray()).ToArray();
            var playerSupported=Enumerable.Range(0,2).Select(i=>trayPlaces.Contains(i,room.player)).ToArray();
            stableTime=balanced?stableTime+dt:0;
            if(!balanced)insufficientShown=false;
            if(!Solved&&stableTime>=stableDuration&&!(insufficientWeight&&insufficientWeight.Playing))
            {
                if(heavyEnough)
                {
                    Solved=true;
                    if(!Application.isPlaying||!completion||!completion.Play())CompletePresentation();
                }
                else if(!insufficientShown)
                {
                    insufficientShown=true;InsufficientAttempts++;
                    if(Application.isPlaying&&insufficientWeight)insufficientWeight.Play();
                }
            }
            press=Application.isPlaying&&completion&&!completionFinished?completionPress*pressDepth:
                Mathf.MoveTowards(press,Solved?pressDepth:0,dt*.12f);
            pressurePlate.localPosition=plateRest-Vector3.up*press;
            balance.localPosition=balanceRest-Vector3.up*press;
            for(int i=0;i<2;i++)
            {
                trays[i].localPosition=trayRest[i]+Vector3.up*((i==0?-1:1)*Mathf.Sin(tilt*Mathf.Deg2Rad)*.8f);
                var delta=trays[i].position-prior[i];
                foreach(var obj in supported[i])
                {obj.transform.position+=delta;obj.Body.position=obj.transform.position;}
                if(playerSupported[i])
                    room.player.GetComponent<CharacterController>().Move(delta);
            }
            if(Solved)
            {
                passageSeal.localPosition=Application.isPlaying&&completion&&!completionFinished?
                    sealRest+Vector3.up*3.8f*completionExit:
                    Vector3.MoveTowards(passageSeal.localPosition,sealRest+Vector3.up*3.8f,dt*1.4f);
                if(!ReachedExit&&exitVolume.bounds.Contains(room.player.transform.position+Vector3.up*.3f))
                {ReachedExit=true;}
            }
        }
        public void CompletePresentation()
        {
            if(completionFinished)return;
            completionFinished=true;completionPress=completionExit=1;
            if(Application.isPlaying&&celebration)celebration.Play(true);
        }
        public OpeningObject[] Apples()=>room.objects.Where(o=>o&&o.wordId=="apple")
            .Concat(room.Copies.Where(p=>p.Key.wordId=="apple").Select(p=>p.Value)).ToArray();
        public void RefreshTrayOccupancy()
        {
            var presence=room.objects.Concat(room.Copies.Values).Where(o=>o).Select(o=>
            {var b=o.PhysicalBounds;return new DestinationSlots.Presence(o,new Vector3(b.center.x,b.min.y,b.center.z));}).ToList();
            presence.Add(new DestinationSlots.Presence(room.player,room.player.transform.position));
            trayPlaces.RefreshOccupancy(presence);
        }
        public bool OnTray(Vector3 feet,Vector3 tray)=>Mathf.Abs(feet.x-tray.x)<.38f&&Mathf.Abs(feet.z-tray.z)<.38f&&Mathf.Abs(feet.y-tray.y)<.12f;
        public float Weight(int side)=>Apples().Where(o=>(!room.playerBalance||!room.playerBalance.Carries(o))&&trayPlaces.Contains(side,o))
            .Sum(o=>o.GetComponent<AppleState>()?o.GetComponent<AppleState>().Weight:1f)+
            (room.player.AppleIdentity&&trayPlaces.Contains(side,room.player)?AppleState.WeightFor(room.player.AppleCore):0);
    }
}
