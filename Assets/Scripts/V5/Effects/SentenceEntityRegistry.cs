using System.Collections.Generic;
using Space = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    // Per-world storage, with no scene names, grammar or object-specific selection rules.
    public sealed class SentenceEntityRegistry
    {
        readonly List<SentenceEntity> entities = new List<SentenceEntity>();
        public int Count => entities.Count;
        public int Revision { get; private set; }
        internal void Changed(SentenceEntity entity) { if (entities.Contains(entity)) Revision++; }

        internal void Add(SentenceEntity entity)
        {
            if (entities.Contains(entity)) return;
            entities.Add(entity);
            Revision++;
        }

        internal void Remove(SentenceEntity entity)
        {
            if (entities.Remove(entity)) Revision++;
        }

        public IEnumerable<T> Query<T>(Space scope) where T : class => Query<T>(SentenceSpaceCatalog.Default.FromLegacy(scope));

        public IEnumerable<T> QueryAll<T>() where T : class
        {
            var seen=new HashSet<T>();
            foreach(var entity in entities)
                if(entity&&entity.isActiveAndEnabled&&entity.Endpoint is T endpoint&&seen.Add(endpoint))yield return endpoint;
        }

        public IEnumerable<T> Query<T>(SentenceSpaceSet scope) where T : class
        {
            var seen = new HashSet<T>();
            foreach (var entity in entities)
                if (entity && entity.isActiveAndEnabled && entity.ControlRegion.Overlaps(scope) &&
                    entity.Endpoint is T endpoint && seen.Add(endpoint))
                    yield return endpoint;
        }

        public IEnumerable<SentenceEntity> QueryEntities(SentenceSpaceSet scope, SentenceProperty property)
        {
            foreach (var entity in entities)
                if (entity && entity.isActiveAndEnabled && entity.ControlRegion.Overlaps(scope) && entity.Has(property)) yield return entity;
        }
        public IEnumerable<SentenceEntity> QueryEntities(Space scope, SentenceProperty property) => QueryEntities(SentenceSpaceCatalog.Default.FromLegacy(scope), property);
        public IEnumerable<SentenceStateOutput> QueryOutputs(Space scope, SentenceProperty property, SentenceOutputDefinition definition = null) =>
            QueryOutputs(SentenceSpaceCatalog.Default.FromLegacy(scope), property, definition);
        public IEnumerable<SentenceStateOutput> QueryOutputs(SentenceSpaceSet scope, SentenceProperty property, SentenceOutputDefinition definition = null)
        {
            var seen = new HashSet<SentenceStateOutput>();
            foreach (var entity in QueryEntities(scope, property))
                foreach (var output in entity.Outputs)
                    if (output.Alive && (!definition || output.Definition == definition) && seen.Add(output)) yield return output;
        }
        public SentenceSpaceSet RegionOf(SentenceStateOutput output)
        {
            var region = new HashSet<SentenceSpace>();
            foreach (var entity in entities)
                if (entity && entity.isActiveAndEnabled)
                    foreach (var candidate in entity.Outputs)
                        if (ReferenceEquals(candidate, output)) { region.UnionWith(entity.ControlRegion); break; }
            return new SentenceSpaceSet(region);
        }

        public Space SpacesOf(object endpoint) => SentenceSpaceCatalog.Default.ToLegacy(RegionOf(endpoint));

        public SentenceSpaceSet RegionOf(object endpoint)
        {
            foreach (var entity in entities)
                if (entity && ReferenceEquals(entity.Endpoint, endpoint)) return entity.ControlRegion;
            return SentenceSpaceSet.Empty;
        }
    }
}
