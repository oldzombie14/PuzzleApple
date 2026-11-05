using UnityEngine;

namespace PuzzleApple.V4
{
    [CreateAssetMenu(menuName = "PuzzleApple/Music Track")]
    public sealed class MusicTrack : ScriptableObject
    {
        public AudioClip clip;
        [Range(0, 1)] public float volume = .45f;
        public bool fadeIn = true;
        public bool fadeOut = true;
        [Min(0)] public float fadeInSeconds = 2;
        [Min(0)] public float fadeOutSeconds = 2;
    }
}
