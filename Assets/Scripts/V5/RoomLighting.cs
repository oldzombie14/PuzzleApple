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
        [Range(0,.15f)] public float offAmbient=.045f;
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
            if(chamberBloom)chamberBloom.weight=journey&&journey.ViewBeyondChamber?0:blend;
            Color outside=journey&&journey.ViewBeyondChamber?Color.black:Color.Lerp(Color.white*offAmbient,openAmbient+new Color(unfoldedAmbientLift,unfoldedAmbientLift,unfoldedAmbientLift,0),lamp);
            if(journey&&journey.ViewBeyondChamber)outside=Color.white*(journey.DoorwayLighting*Mathf.Lerp(offAmbient,journey.ambientIntensity,Mathf.SmoothStep(0,1,journey.Brightness)));
            RenderSettings.ambientLight=Color.Lerp(closedAmbient,outside,blend);
            // Keep the fog shader variant ready from the initial menu frame.
            RenderSettings.fog=true;RenderSettings.fogColor=Color.black;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=journey&&journey.ViewBeyondChamber&&room.Opened?
                Mathf.Lerp(journey.corridorFogDensity,journey.fogDensity,journey.DoorwayLighting):0;
        }
    }
}
