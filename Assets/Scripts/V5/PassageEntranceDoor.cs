using System;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using Space = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    // A lift reads player proximity and power. Its frame remains collectible while the leaf is up.
    public sealed class PassageEntranceDoor : MonoBehaviour, IProgressBoundary
    {
        PassageJourney journey;
        CharacterController player;
        ProgressLock gate;
        RailChargeRoom supply;
        Renderer leaf;
        readonly Light[] spill = new Light[2];
        readonly Transform[] fills = new Transform[4];
        MaterialPropertyBlock ink;
        float width, height, outerRange;
        public float Energy { get; private set; }
        public float Progress { get; private set; }
        public Transform Leaf => leaf.transform;
        public Space ControlSpaces { get; private set; }
        public float Clearance => height * Mathf.SmoothStep(0, 1, Progress);
        public float BoundaryProgress => Clearance / height;
        public bool AllowsConnection => Progress > 0;
        public event Action BoundaryChanged;

        public static PassageEntranceDoor Build(PassageJourney journey, string name, float z, float openingHeight,
            string lockName, RailChargeRoom supply = null, bool receivesPlatform = false)
        {
            var root = new GameObject(name).transform;
            root.SetParent(journey.Root, false);
            root.localPosition = Vector3.forward * z;
            var door = root.gameObject.AddComponent<PassageEntranceDoor>();
            door.journey = journey;
            door.ink = new MaterialPropertyBlock();
            door.width = journey.PathWidth;
            door.height = openingHeight;
            door.supply = supply;
            door.outerRange = receivesPlatform ? Mathf.Max(journey.sensorRange,
                journey.platformSpeed * journey.sensorLiftSeconds + 1f) : journey.sensorRange;
            door.player = journey.entry.player.GetComponent<CharacterController>();
            door.leaf = door.Part("Lift slab", Vector3.up * (openingHeight * .5f),
                new Vector3(door.width, openingHeight, .22f), journey.roomMaterial, true);
            door.MakeFrame();
            for (int i=0;i<door.spill.Length;i++) {
                var emitter=new GameObject("Door spill",typeof(Light));
                emitter.transform.SetParent(root,false);
                emitter.transform.localPosition=new Vector3(0,openingHeight*.62f,i==0?-.65f:.65f);
                var light=emitter.GetComponent<Light>();
                light.type=LightType.Point;
                light.shadows=LightShadows.Soft;
                light.range=journey.LightStyle.LampRange(journey.LightStyle.LampIntensity(.19f));
                door.spill[i]=light;
            }
            door.gate = Instantiate(Resources.Load<ProgressLock>("V5/" + lockName), root, false);
            door.gate.passableProgress = Mathf.Clamp01((door.player.height + door.player.skinWidth) / openingHeight);
            door.gate.BindBoundary(door);
            door.ControlSpaces = door.gate.BoundarySpaces;
            door.Energy = supply ? .86f : 1f;
            door.Draw(true);
            return door;
        }

        Renderer Part(string name, Vector3 centre, Vector3 dimensions, Material material, bool solid)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.layer = 29;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = centre;
            part.transform.localScale = dimensions;
            part.GetComponent<Collider>().enabled = solid;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        // Thin bars span both wall faces. The lens is shifted along each recess as it fills.
        Vector3 Corner(int index)
        {
            float x = width * .5f + .18f, top = height + .18f;
            switch (index) {
                case 0: return new Vector3(x, top, 0);
                case 1: return new Vector3(x, .025f, 0);
                case 2: return new Vector3(-x, .025f, 0);
                default: return new Vector3(-x, top, 0);
            }
        }

        void MakeFrame()
        {
            for (int i = 0; i < 4; i++) {
                Vector3 a = Corner(i), b = Corner((i + 1) % 4);
                Vector3 size = b - a;
                size = new Vector3(Mathf.Abs(size.x) + .13f, Mathf.Abs(size.y) + .13f, .36f);
                Part("Charge frame", (a + b) * .5f, size, journey.blackSurround, i != 1);
                fills[i] = Part("Energy", a, Vector3.one, journey.lampMaterial, false).transform;
            }
        }

        void Draw(bool powered)
        {
            ink.Clear();
            ink.SetFloat("_ChargeDoorEmission", powered ? .68f : 0);
            foreach (var light in spill) light.intensity = powered ? journey.LightStyle.LampIntensity(.19f) : 0;
            leaf.SetPropertyBlock(ink);
            float perimeter = 2 * (Vector3.Distance(Corner(0), Corner(1)) + Vector3.Distance(Corner(1), Corner(2)));
            float remaining = Energy * perimeter;
            for (int i = 0; i < 4; i++) {
                Vector3 a = Corner(i), delta = Corner((i + 1) % 4) - a;
                float length = delta.magnitude;
                float lit = Mathf.Clamp(remaining, 0, length);
                var strip = fills[i];
                strip.gameObject.SetActive(lit > .001f);
                if (lit > .001f) {
                    Vector3 span = delta * (lit / length);
                    strip.localPosition = a + span * .5f;
                    strip.localScale = new Vector3(Mathf.Max(.11f, Mathf.Abs(span.x)), Mathf.Max(.11f, Mathf.Abs(span.y)), .375f);
                }
                remaining -= length;
            }
        }

        public static bool WithinSensor(Vector3 feet, float openingWidth, float openingHeight, float outside, float inside)
        {
            return feet.y > -.45f && feet.y < openingHeight && Mathf.Abs(feet.x) < openingWidth * .5f + .35f
                && feet.z >= -outside && feet.z <= inside;
        }

        void Update()
        {
            if (!journey.CanCycleDoor || !journey.entry.room.Opened || journey.entry.ConfirmingExit) return;
            bool powered = !journey.EndingDoor.LessonAcquired || journey.entry.sentenceRuntime.HasIntent(
                SentenceAction.Illuminate, SentenceCapabilityCatalog.Default.door, ControlSpaces);
            float energy = powered ? (supply ? Mathf.Lerp(.86f, 1f, supply.Brightness) : 1f) : 0;
            if (energy != Energy) { Energy = energy; Draw(powered); }
            var feet = transform.InverseTransformPoint(journey.entry.player.transform.position);
            bool open = Energy >= 1 && WithinSensor(feet, width, height, outerRange, journey.sensorRange);
            if (open && !gate.Unlocked)
                open = (ProgressLock.Accessible(Space.All) & gate.requiresSpaces) == gate.requiresSpaces;
            float next = Mathf.MoveTowards(Progress, open ? 1 : 0, Time.deltaTime / journey.sensorLiftSeconds);
            if (next == Progress) return;
            if (next < Progress && Mathf.Abs(feet.z) < player.radius + .14f && Mathf.Abs(feet.x) < width * .5f + player.radius
                && feet.y + player.height > height * Mathf.SmoothStep(0, 1, next) && feet.y < height) return;
            Progress = next;
            Leaf.localPosition = Vector3.up * (height * .5f + Clearance);
            BoundaryChanged?.Invoke();
        }

        public bool Owns(Collider hit) => hit && hit.transform.IsChildOf(transform);
    }
}
