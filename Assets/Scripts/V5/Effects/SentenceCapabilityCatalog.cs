using System;
using UnityEngine;

namespace PuzzleApple.V5
{
    [CreateAssetMenu(menuName = "PuzzleApple/V5/Sentence Capabilities")]
    public sealed class SentenceCapabilityCatalog : ScriptableObject
    {
        public SentenceProperty potentialLight, light, door, floor, world, motion;
        public SentenceOutputDefinition emission, hinge, lift, position;
        static SentenceCapabilityCatalog current;
        public static SentenceCapabilityCatalog Default
        {
            get
            {
                if (!current) current = Resources.Load<SentenceCapabilityCatalog>("V5/SentenceCapabilities");
                if (!current) throw new InvalidOperationException("Missing Resources/V5/SentenceCapabilities catalog.");
                return current;
            }
        }
    }
}
