using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V5.Outdoor
{
    // Lives in the retained P4_Systems scene, alongside the session and listener.
    [DisallowMultipleComponent]
    public sealed class SessionAudio : MonoBehaviour
    {
        sealed class Voice
        {
            public AudioSource source;
            public MusicTrack track;
            public float from, target, elapsed, duration;
        }
        readonly Voice[] voices = new Voice[2];
        MusicTrack current;
        float restoredVolume;
        public bool Muted { get; private set; }

        void Awake()
        {
            restoredVolume = AudioListener.volume;
            for (int i = 0; i < voices.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0;
                source.volume = 0;
                voices[i] = new Voice { source = source };
            }
        }

        public void ToggleMute()
        {
            if (!Muted) restoredVolume = AudioListener.volume;
            Muted = !Muted;
            AudioListener.volume = Muted ? 0 : restoredVolume;
        }

        public bool HandleMuteShortcut()
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (selected && selected.GetComponentInParent<InputField>()) return false;
            ToggleMute();
            return true;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
                HandleMuteShortcut();
            foreach (var voice in voices)
            {
                voice.elapsed += Time.unscaledDeltaTime;
                float t = voice.duration <= 0 ? 1 : Mathf.Clamp01(voice.elapsed / voice.duration);
                voice.source.volume = Mathf.Lerp(voice.from, voice.target, Mathf.SmoothStep(0, 1, t));
                if (t >= 1 && voice.target == 0 && voice.track)
                {
                    voice.source.Stop();
                    voice.source.clip = null;
                    voice.track = null;
                }
            }
        }

        public void ResumeTrack()
        {
            foreach(var voice in voices)
                if(voice.track&&voice.track==current&&!voice.source.isPlaying)voice.source.Play();
        }

        public void Play(MusicTrack track)
        {
            if (track && !track.clip) track = null;
            if (current == track) return;
            current = track;
            Voice incoming = null;
            if (track)
            {
                foreach (var voice in voices)
                    if (voice.track == track) incoming = voice;
                if (incoming == null)
                {
                    incoming = voices[0].source.volume <= voices[1].source.volume ? voices[0] : voices[1];
                    incoming.source.Stop();
                    incoming.track = track;
                    incoming.source.clip = track.clip;
                    incoming.source.volume = 0;
                    incoming.source.Play();
                }
            }
            foreach (var voice in voices)
            {
                if (!voice.track) continue;
                bool entering = voice == incoming;
                voice.from = voice.source.volume;
                voice.target = entering ? track.volume : 0;
                voice.elapsed = 0;
                voice.duration = entering
                    ? (track.fadeIn ? track.fadeInSeconds : 0)
                    : (voice.track.fadeOut ? voice.track.fadeOutSeconds : 0);
                if (voice.duration <= 0) voice.source.volume = voice.target;
            }
        }

        void OnDestroy()
        {
            if (Muted) AudioListener.volume = restoredVolume;
        }
    }
}
