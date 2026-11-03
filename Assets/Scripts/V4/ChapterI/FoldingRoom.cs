using System.Collections;
using UnityEngine;

namespace PuzzleApple.V4
{
    public sealed class FoldingRoom : MonoBehaviour
    {
        [System.Serializable] public struct Panel
        {
            public Transform hinge;
            public Vector3 openEuler;
            [Tooltip("展开时长（秒）；0 使用房间默认时长。")]
            [Min(0)] public float durationOverride;
        }
        public Panel[] panels;
        public Collider[] movingColliders;
        public float duration = 4;
        public float sinkDuration = 2.5f, sinkDepth = .35f;
        public bool Opened { get; private set; }
        public bool Animating { get; private set; }
        SessionSceneManager session;
        Vector3 initialPosition;
        void Awake() => initialPosition = transform.localPosition;
        IEnumerator Start()
        {
            session = FindFirstObjectByType<SessionSceneManager>();
            while (!session || session.Current == null) yield return null;
            if (session.board.State.Knows("mirror")) { SetPose(1); SetSink(1); Opened = true; yield break; }
            while (!session.board.State.Knows("mirror")) yield return null;
            Animating = true;
            foreach (var collider in movingColliders) if (collider) collider.enabled = false;
            float unfoldDuration = Mathf.Max(.01f, duration);
            foreach (var panel in panels)
                if (panel.hinge) unfoldDuration = Mathf.Max(unfoldDuration, PanelDuration(panel));
            for (float t = 0; t < unfoldDuration; t += Time.deltaTime)
            {
                foreach (var panel in panels)
                    if (panel.hinge)
                        panel.hinge.localRotation = Quaternion.Slerp(Quaternion.identity,
                            Quaternion.Euler(panel.openEuler), Mathf.SmoothStep(0, 1, t / PanelDuration(panel)));
                yield return null;
            }
            SetPose(1);
            for(float t=0;t<sinkDuration;t+=Time.deltaTime)
            {SetSink(Mathf.SmoothStep(0,1,t/Mathf.Max(.01f,sinkDuration)));yield return null;}
            SetSink(1); Opened = true; Animating = false;
        }
        float PanelDuration(Panel panel) => Mathf.Max(.01f, panel.durationOverride > 0 ? panel.durationOverride : duration);
        void SetPose(float value)
        {
            foreach (var panel in panels) if (panel.hinge) panel.hinge.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(panel.openEuler), value);
            if (value >= 1) foreach (var collider in movingColliders) if (collider) collider.enabled = false;
        }
        void SetSink(float value) => transform.localPosition = initialPosition - Vector3.up * (sinkDepth * value);
    }
}
