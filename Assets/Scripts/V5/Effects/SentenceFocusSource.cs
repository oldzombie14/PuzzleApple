using System;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5
{
    // A mechanism offers a point only while this sentence would change it.
    // Its registered region describes where the player may see this hint.
    public sealed class SentenceFocusSource
    {
        readonly Component lifetime;
        Func<CognitionState.Group, Vector3?> request;
        SentenceFocusSource(Component owner) { lifetime = owner; }

        public Vector3? Request(CognitionState.Group group)
        {
            if (group == null || !lifetime || !lifetime.gameObject.activeInHierarchy ||
                lifetime is Behaviour behaviour && !behaviour.isActiveAndEnabled) return null;
            return request(group);
        }

        public static SentenceEntity Bind(SentenceEntityRegistry registry, Component lifetime,
            Func<SentenceSpaceSet> visibleFrom, Func<CognitionState.Group, Vector3?> request)
        {
            if (!lifetime || request == null) throw new ArgumentNullException();
            SentenceFocusSource source = null;
            foreach (var entity in lifetime.GetComponents<SentenceEntity>())
                if (entity.Endpoint is SentenceFocusSource existing && existing.lifetime == lifetime) { source = existing; break; }
            if (source == null) source = new SentenceFocusSource(lifetime);
            source.request = request;
            return SentenceEntity.Bind(registry, lifetime, source, visibleFrom);
        }
    }
}
