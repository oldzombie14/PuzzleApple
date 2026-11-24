using UnityEngine;

namespace PuzzleApple.V5
{
    [CreateAssetMenu(menuName="PuzzleApple/V5/Sentence presentation",fileName="SentencePresentation")]
    public sealed class SentencePresentationSettings : ScriptableObject
    {
        public SentencePresentation.Timing timing=new SentencePresentation.Timing();
    }
}
