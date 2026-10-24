using PuzzleApple.V4.Cognition;
using UnityEngine;

namespace PuzzleApple.V4
{
    public enum IntroShotTarget { NearestLight, Door, Sky, LightGroup }
    public enum IntroShotAim { Focus, PitchOnly, AdaptiveLightGround }

    [CreateAssetMenu(menuName="PuzzleApple/V4/Intro Shot",fileName="IntroShot")]
    public sealed class IntroShotProfile : ScriptableObject
    {
        [Tooltip("此配置只用于这个完整句式。")]
        public CognitionRule rule;
        public IntroShotTarget target;
        public IntroShotAim aim;
        [Tooltip("观察目标的世界坐标偏移。")]
        public Vector3 targetOffset;
        [Tooltip("在目标朝向上追加的俯仰和左右角度；X 为负时抬头。")]
        public Vector2 angleOffset;
        [Range(0,180)] public float maxCorrectionAngle=6;
        [Range(0,80)] public float skyMinimumElevation=15;
        [Tooltip("灯群离地较低时只调 pitch，超过此高度后对准灯群。")]
        [Min(0)] public float elevatedGroupHeight=6;
        [Min(1)] public float lightGroupRadius=18;
        [Tooltip("目标在画面中的垂直位置，0 为底部，0.5 为中央。")]
        [Range(.2f,.8f)] public float targetViewportY=.5f;
        public bool followTarget;
        [Min(.1f)] public float followResponse=1.5f;
        [Min(.1f)] public float maxFollowDegreesPerSecond=12;
        [Tooltip("偏转超过此角度时直接切到目标；180 表示不使用瞬切。")]
        [Range(0,180)] public float snapAboveAngle=180;
        [Min(0)] public float enterDuration=.65f, holdDuration=2.4f, exitDuration=.65f;
        [Range(0,.2f)] public float letterboxHeight=.09f;
    }
}
