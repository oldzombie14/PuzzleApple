using UnityEngine;

namespace PuzzleApple.V5
{
    [CreateAssetMenu(menuName="PuzzleApple/V5/Sentence presentation",fileName="SentencePresentation")]
    public sealed class SentencePresentationSettings : ScriptableObject
    {
        public SentencePresentation.Shot[] shots=new SentencePresentation.Shot[0];
    }
}
