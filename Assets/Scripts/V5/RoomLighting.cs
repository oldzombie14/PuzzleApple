using UnityEngine;

namespace PuzzleApple.V5
{
    // Native broad spotlights: dim slit light when closed, white diffuse fill when open.
    // Ambient fill comes from each room's RoomLightBalance, so every room follows one RoomLightStyle.
    public sealed class RoomLighting : MonoBehaviour
    {
        public FoldingRoom room;
        public ChamberLight chamberLight;
        public PassageJourney journey;
        public Light[] spotlights;
        [Tooltip("Optional per-light multiplier for unfolded-room intensity; absent entries use 1.")]
        public float[] openIntensityMultipliers;
        [Header("Small room neutral fill")]
        [Tooltip("Legacy centre point light; kept disabled. Closed Ambient provides uniform fill.")]
        public Light interiorFill;
        public UnityEngine.Rendering.Volume chamberBloom;
        [Min(0)] public float closedIntensity=.4f, openIntensity=180;
        public Color closedAmbient=new Color(.003f,.003f,.003f);
        [Range(0,1)] public float unfoldedSpotContribution=.25f;
        [Tooltip("Measures the unfolded chamber's lights; its style sets the shared room fill rule.")]
        public RoomLightBalance chamberFill;
        [Tooltip("Share of the sphere room's fill that reaches the corridor through the fully open door.")]
        [Range(0,1)] public float openDoorFill=1;
        [Min(.05f)] public float transitionSeconds=.8f;
        Color currentAmbient;
        float currentFog;
        bool initialized;
        public static float AdvanceLevel(float current, float target, float step)
        {
            float dark = 1 - Mathf.Clamp01(current);
            if (target < current) step *= 1 + 1.5f*dark*dark;
            return Mathf.MoveTowards(current,target,step);
        }
        public Color FoldingAmbient(Color unfolded, float blend)=>Color.Lerp(closedAmbient,unfolded,blend);
        public float FoldingIntensity(float unfolded, float blend)=>Mathf.Lerp(closedIntensity,unfolded,blend);

        void LateUpdate()
        {
            // Indoor lighting must not inherit the outdoor scene's coloured gradient.
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            float blend=Mathf.SmoothStep(0,1,room.Progress);
            float lamp=chamberLight?chamberLight.Brightness:1;
            for(int i=0;i<spotlights.Length;i++)
                if(spotlights[i])
                {
                    float scale=openIntensityMultipliers!=null&&i<openIntensityMultipliers.Length?Mathf.Max(0,openIntensityMultipliers[i]):1;
                    spotlights[i].intensity=FoldingIntensity(openIntensity*scale*unfoldedSpotContribution,blend)*lamp;
                }
            if(interiorFill)
            {
                interiorFill.enabled=false;
            }
            float corridor=journey?journey.CorridorBlend:0;
            if(chamberBloom)chamberBloom.weight=blend*(journey?1-journey.RoomBloom:1);
            // Each room's fill follows its own lights; doorways blend between rooms by view position.
            var style=chamberFill?chamberFill.style:null;
            Color dim=Color.white*(style?style.darkFill:.055f);
            Color outside=dim;
            var view=journey?journey.entry.player.view:Camera.main?Camera.main.transform:null;
            var hiddenRoom=journey&&journey.InReturnPath?journey.FarRoomFill:null;
            if(view&&RoomLightBalance.FillAt(view.position,out float fill,out float weight,hiddenRoom))
                outside=Color.Lerp(dim,Color.white*fill,weight);
            // In a corridor, take the fill of the room it opens into: the sphere room through the
            // white door, and the far room through its right-hinged entrance.
            if(journey&&journey.InCorridor)
            {
                var ahead=journey.InReturnPath?journey.FarRoomFill:journey.Tutorial?journey.Tutorial.Fill:null;
                float open=journey.InReturnPath?(journey.EntranceDoor?journey.EntranceDoor.Progress:0):journey.DoorProgress;
                float far=ahead?Mathf.Lerp(dim.r,ahead.Fill,openDoorFill*open):dim.r;
                if(far>outside.r)outside=Color.white*far;
            }
            var targetAmbient=FoldingAmbient(outside,blend);
            float smoothing=1-Mathf.Exp(-Time.unscaledDeltaTime*4/transitionSeconds);
            currentAmbient=initialized?Color.Lerp(currentAmbient,targetAmbient,smoothing):targetAmbient;
            RenderSettings.ambientLight=currentAmbient;
            // Room surfaces (World Surface) take their own room's fill; the chamber fades in as it unfolds.
            if(chamberFill){chamberFill.closedFill=closedAmbient;chamberFill.openness=blend;}
            // Keep the fog shader variant ready from the initial menu frame.
            RenderSettings.fog=true;RenderSettings.fogColor=Color.black;RenderSettings.fogMode=FogMode.ExponentialSquared;
            float targetFog=journey&&journey.ViewBeyondChamber&&room.Opened?
                corridor*Mathf.Lerp(journey.corridorFogDensity,journey.fogDensity*journey.Brightness,journey.DoorwayLighting):0;
            currentFog=initialized?Mathf.Lerp(currentFog,targetFog,smoothing):targetFog;
            RenderSettings.fogDensity=currentFog;initialized=true;
        }
    }
}
