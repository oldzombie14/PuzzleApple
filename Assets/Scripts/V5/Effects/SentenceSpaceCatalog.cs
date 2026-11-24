using System;
using System.Collections.Generic;
using UnityEngine;
using LegacySpace = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    // Compatibility mapping for the existing motion/shader consumers. Native queries use assets.
    [CreateAssetMenu(menuName = "PuzzleApple/V5/Sentence Space Catalog")]
    public sealed class SentenceSpaceCatalog : ScriptableObject
    {
        [Serializable] public struct Mapping { public LegacySpace legacy; public SentenceSpace space; }
        public Mapping[] mappings = new Mapping[0];
        readonly Dictionary<LegacySpace, SentenceSpaceSet> converted = new Dictionary<LegacySpace, SentenceSpaceSet>();
        static SentenceSpaceCatalog current;
        public static SentenceSpaceCatalog Default
        {
            get
            {
                if (!current) current = Resources.Load<SentenceSpaceCatalog>("V5/SentenceSpaces");
                if (!current) throw new InvalidOperationException("Missing SentenceSpaceCatalog asset at Resources/V5/SentenceSpaces.");
                return current;
            }
        }
        void OnEnable() => converted.Clear();
        void OnValidate() => converted.Clear();
        public SentenceSpaceSet FromLegacy(LegacySpace mask)
        {
            if (converted.TryGetValue(mask, out var result)) return result;
            var members = new List<SentenceSpace>();
            LegacySpace covered = 0;
            foreach (var mapping in mappings)
                if ((mapping.legacy & mask) != 0 && mapping.space)
                {
                    members.Add(mapping.space);
                    covered |= mapping.legacy;
                }
            if ((mask & ~covered) != 0) throw new InvalidOperationException("Unmapped legacy sentence spaces: " + (mask & ~covered));
            result = new SentenceSpaceSet(members);
            converted.Add(mask, result);
            return result;
        }
        public LegacySpace ToLegacy(SentenceSpaceSet region)
        {
            LegacySpace result = 0;
            foreach (var space in region)
            {
                bool found = false;
                foreach (var mapping in mappings)
                    if (mapping.space == space) { result |= mapping.legacy; found = true; break; }
                if (!found) throw new InvalidOperationException("This legacy consumer cannot represent space: " + space.name);
            }
            return result;
        }
    }
}
