using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class RailChargeRoom : MonoBehaviour, ISwitchableLight, ISentenceOutputState<EmissionState>
    {
        public const float Depth = 13, TrackDepth = 7, HallLength = 9, LandingDepth = 2, JumpGap = .72f;
        PassageJourney journey;
        Renderer cube;
        Light lamp;
        bool targetOn, changing;
        MaterialPropertyBlock ink;
        readonly System.Collections.Generic.HashSet<Collider> floors = new System.Collections.Generic.HashSet<Collider>();
        internal SentenceStateOutput LightOutput;
        public float Brightness { get; private set; }
        public bool IsLit(Collider hit) => Brightness > 0 && hit && hit.transform == cube.transform;
        public bool IsFloor(Collider hit) => floors.Contains(hit);
        public Vector3 SafeLanding => journey.Root.TransformPoint(new Vector3(journey.PathWidth * .5f + 1.15f, .08f, journey.GarageStart + 3));

        public void Build(PassageJourney owner, float openingHeight)
        {
            journey = owner;
            ink = new MaterialPropertyBlock();
            float start = owner.GarageStart, end = start + Depth, centre = (start + end) * .5f;
            float holeWidth = owner.PathWidth + .08f;
            float width = Mathf.Max(11, holeWidth + 6), height = Mathf.Max(7, openingHeight + 2.5f);
            float wing = (width - holeWidth) * .5f;
            foreach (int side in new[] { -1, 1 }) {
                Floor("Rail room bank", side * (holeWidth + wing) * .5f, centre, wing, Depth);
                owner.RoomBlock("Rail room side", new Vector3(side * width * .5f, height * .5f, centre),
                    new Vector3(.3f, height, Depth), Vector3.left * side);
            }
            Floor("Rail room landing", 0, (start + TrackDepth + end) * .5f, holeWidth, Depth - TrackDepth);
            owner.RoomBlock("Rail room ceiling", new Vector3(0, height + .15f, centre), new Vector3(width, .3f, Depth), Vector3.down);
            foreach (float z in new[] { start, end }) {
                Vector3 inward = z == start ? Vector3.forward : Vector3.back;
                float panel = (width - owner.PathWidth) * .5f;
                foreach (int side in new[] { -1, 1 })
                    owner.RoomBlock("Rail room doorway side", new Vector3(side * (owner.PathWidth + panel) * .5f, height * .5f, z),
                        new Vector3(panel, height, .3f), inward);
                owner.RoomBlock("Rail room lintel", new Vector3(0, (height + openingHeight) * .5f, z),
                    new Vector3(owner.PathWidth, height - openingHeight, .3f), inward);
            }
            RoomLightBalance.Create(owner.Root, "Rail room light balance", new Vector3(0, height * .5f, centre),
                new Vector3(width, height, Depth), owner.LightStyle);
            float armY = openingHeight + 1.35f;
            owner.Block("Black lamp bracket", new Vector3(0, armY, end - .49f), new Vector3(.10f,.10f,.94f), owner.blackSurround);
            owner.Block("Lamp hanger", new Vector3(0, armY - .18f, end - .91f), new Vector3(.09f,.43f,.09f), owner.blackSurround);
            cube = owner.Block("Wall-mounted cube light", new Vector3(0, armY - .66f, end - .91f), Vector3.one * .62f, owner.lampMaterial);
            cube.gameObject.layer = 29;
            var emitter = new GameObject("Cube light", typeof(Light));
            emitter.transform.SetParent(cube.transform, false);
            lamp = emitter.GetComponent<Light>();
            lamp.type = LightType.Point;
            lamp.shadows = LightShadows.Soft;
            cube.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ApplyLevel(0);

            float hallEnd = owner.RoomStart - LandingDepth - JumpGap;
            Floor("Approach walkway", 0, (end + hallEnd) * .5f, owner.PathWidth, hallEnd - end);
            float doorFloorEdge = owner.RoomStart - .15f;
            float padStart = owner.RoomStart - LandingDepth;
            Floor("Room 3 doorstep", 0, (doorFloorEdge + padStart) * .5f, owner.PathWidth, doorFloorEdge - padStart);
        }

        void Floor(string name, float x, float z, float width, float depth)
        {
            var floor = journey.Block(name, new Vector3(x,-.15f,z), new Vector3(width,.3f,depth), journey.roomMaterial);
            floor.gameObject.layer = 29;
            ink.Clear();
            ink.SetVector("_RoomShellInward", Vector3.up);
            floor.SetPropertyBlock(ink);
            floors.Add(floor.GetComponent<Collider>());
        }

        void ApplyLevel(float level)
        {
            Brightness = level;
            float boost = journey.entry.sentenceRuntime ? journey.entry.sentenceRuntime.LampBoost(PassageJourney.LightSpace.RailRoom) : 1;
            ink.Clear();
            ink.SetColor("_BaseColor", Color.Lerp(Color.gray * .24f, Color.white, level));
            ink.SetColor("_EmissionColor", Color.white * (level * boost * 1.4f));
            cube.SetPropertyBlock(ink);
            lamp.intensity = journey.LightStyle.LampIntensity(.62f) * level * boost;
            lamp.range = journey.LightStyle.LampRange(journey.LightStyle.LampIntensity(.62f));
        }
        void Update()
        {
            if (!journey || !journey.enabled) return;
            float next = changing ? RoomLighting.AdvanceLevel(Brightness, targetOn ? 1 : 0,
                Time.unscaledDeltaTime / Mathf.Max(.01f, journey.lightFadeDuration), LightOutput?.FinishFraction ?? 0) : Brightness;
            ApplyLevel(next);
            if (!Settling(targetOn)) changing = false;
        }
        public bool WouldChange(bool on) => Settling(on);
        public bool Settling(bool on) => Brightness != (on ? 1 : 0);
        public void SetOn(bool on) { targetOn = on; changing = true; }
        public void Hold() => changing = false;
        public EmissionState CaptureState() => new EmissionState(Brightness);
        public bool TryRestoreState(EmissionState state)
        {
            if (!cube || !state.Valid) return false;
            Hold();
            ApplyLevel(state.brightness);
            return true;
        }
    }
}
