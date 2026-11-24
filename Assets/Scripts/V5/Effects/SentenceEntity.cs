using System;
using System.Collections.Generic;
using UnityEngine;
using Space = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    // A lifetime-bound registration. During migration an endpoint may still represent a group.
    // Control ownership is queried independently of the object's physical transform.
    public sealed class SentenceEntity : MonoBehaviour
    {
        SentenceEntityRegistry registry;
        Func<SentenceSpaceSet> controlRegion;
        public object Endpoint { get; private set; }
        public SentenceSpaceSet ControlRegion => controlRegion != null ? controlRegion() : SentenceSpaceSet.Empty;
        public Space ControlSpaces => SentenceSpaceCatalog.Default.ToLegacy(ControlRegion);
        readonly Dictionary<SentenceProperty, Func<bool>> properties = new Dictionary<SentenceProperty, Func<bool>>();
        readonly List<SentenceStateOutput> outputs = new List<SentenceStateOutput>();
        public IReadOnlyList<SentenceStateOutput> Outputs => outputs;
        public bool Has(SentenceProperty property) => property && properties.TryGetValue(property, out var present) && (present == null || present());
        public SentenceEntity WithProperty(SentenceProperty property, Func<bool> present = null)
        {
            if (!property) throw new ArgumentNullException(nameof(property));
            properties[property] = present; registry?.Changed(this); return this;
        }
        public SentenceEntity WithOutput(SentenceStateOutput output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            for (int i = 0; i < outputs.Count; i++)
                if (outputs[i].Definition == output.Definition)
                {
                    if (ReferenceEquals(outputs[i], output)) return this;
                    outputs[i] = output; registry?.Changed(this); return this;
                }
            outputs.Add(output); registry?.Changed(this); return this;
        }
        public SentenceStateOutput Output(SentenceOutputDefinition definition) => outputs.Find(output => output.Definition == definition);

        public static SentenceEntity Bind(SentenceEntityRegistry registry, Component lifetime,
            object endpoint, Func<Space> spaces)
        {
            if (spaces == null) throw new ArgumentNullException(nameof(spaces));
            return Bind(registry, lifetime, endpoint, () => SentenceSpaceCatalog.Default.FromLegacy(spaces()));
        }

        public static SentenceEntity Bind(SentenceEntityRegistry registry, Component lifetime,
            object endpoint, Func<SentenceSpaceSet> spaces)
        {
            if (registry == null || !lifetime || endpoint == null) throw new ArgumentNullException();
            foreach (var existing in lifetime.GetComponents<SentenceEntity>())
                if (ReferenceEquals(existing.Endpoint, endpoint))
                {
                    existing.Configure(registry, endpoint, spaces);
                    return existing;
                }
            var entity = lifetime.gameObject.AddComponent<SentenceEntity>();
            entity.Configure(registry, endpoint, spaces);
            return entity;
        }

        void Configure(SentenceEntityRegistry next, object endpoint, Func<SentenceSpaceSet> spaces)
        {
            registry?.Remove(this);
            registry = next;
            Endpoint = endpoint;
            controlRegion = spaces ?? throw new ArgumentNullException(nameof(spaces));
            if (isActiveAndEnabled) registry.Add(this);
        }

        void OnEnable() { if (Endpoint != null) registry?.Add(this); }
        void OnDisable() => registry?.Remove(this);
        void OnDestroy() => registry?.Remove(this);
    }
}
