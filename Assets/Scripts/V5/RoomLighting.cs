using UnityEngine;

namespace PuzzleApple.V5
{
    // Native broad spotlights: dim slit light when closed, white diffuse fill when open.
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
        [Range(0,1)] public float unfoldedAmbientLift=.15f;
        public Color openAmbient=new Color(.24f,.24f,.24f);
        [Range(0,.15f)] public float offAmbient=.055f;
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
                    spotlights[i].intensity=Mathf.Lerp(closedIntensity,openIntensity*scale*unfoldedSpotContribution,blend)*lamp;
                }
            if(interiorFill)
            {
                interiorFill.enabled=false;
            }
            float corridor=journey?journey.CorridorBlend:0;
            if(chamberBloom)chamberBloom.weight=blend*(1-corridor);
            Color dim=Color.white*offAmbient;
            Color outside=Color.Lerp(dim,openAmbient+Color.white*unfoldedAmbientLift,lamp);
            if(journey)
            {
                float roomLight=Mathf.SmoothStep(0,1,journey.Brightness);
                Color farRoom=Color.Lerp(dim,Color.white*journey.ambientIntensity,roomLight);
                float visibleRoom=Mathf.Lerp(.9f*journey.DoorProgress,1,journey.DoorwayLighting);
                outside=Color.Lerp(outside,Color.Lerp(dim,farRoom,visibleRoom),corridor);
            }
            RenderSettings.ambientLight=Color.Lerp(closedAmbient,outside,blend);
            // Keep the fog shader variant ready from the initial menu frame.
            RenderSettings.fog=true;RenderSettings.fogColor=Color.black;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=journey&&journey.ViewBeyondChamber&&room.Opened?
                corridor*Mathf.Lerp(journey.corridorFogDensity,journey.fogDensity*journey.Brightness,journey.DoorwayLighting):0;
        }
    }
}
