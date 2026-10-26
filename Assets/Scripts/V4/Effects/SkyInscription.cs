using PuzzleApple.V4.Cognition;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace PuzzleApple.V4
{
    [DefaultExecutionOrder(500)]
    public sealed class SkyInscription : MonoBehaviour
    {
        public OpeningFlow opening;
        public NightEnvironment environment;
        public Material inkMaterial;
        [Header("入场时在玩家背后显现")]
        [Min(.1f)] public float fadeDuration=1.6f;
        [Header("固定世界方位，保持视距与大小")]
        [Min(10)] public float viewingDistance=90;
        [Range(5,40)] public float elevation=14;
        [Range(8,45)] public float angularWidth=24;
        [Range(0,1)] public float opacity=.82f;
        public Color dayInk=new Color(.2f,.24f,.2f), nightInk=new Color(.74f,.81f,.9f);
        [Header("本地完成记录（不是世界存档）")]
        public string progressKey=IntroHintProgress.DefaultKey;
        IntroHintProgress progress;
        public IntroHintProgress Progress
        {
            get { if(progress==null||progress.Key!=progressKey)progress=new IntroHintProgress(progressKey);return progress; }
        }
        public bool Revealed {get;private set;}
        public Vector3 Bearing {get;private set;}
        public float Elapsed {get;private set;}
        public int DisplayStage {get;private set;} // 0: checklist, 1: answer, 2: finished
        public float Visibility {get;private set;}
        public Transform VisualRoot=>root;
        RectTransform root;
        CanvasGroup group;
        Row[] rows;
        bool nightRequested;
        readonly HashSet<CognitionSignal> appliedSignals=new HashSet<CognitionSignal>();
        sealed class Row
        {
            public CognitionSignal signal;
            public Image[] symbols;
            public Image strike;
            public float width, strikeProgress;
        }
        void Start()
        {
            if(!opening)opening=FindFirstObjectByType<OpeningFlow>();
            if(!environment)environment=FindFirstObjectByType<NightEnvironment>();
            DisplayStage=DesiredStage();
            var go=new GameObject("Sky inscriptions",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
            root=go.GetComponent<RectTransform>();root.SetParent(transform,false);
            root.sizeDelta=new Vector2(420,330);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            canvas.worldCamera=opening.player.view.GetComponent<Camera>();
            group=go.GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;group.alpha=0;
            BuildRows();
        }
        int DesiredStage()=>Progress.Finished?2:Progress.ChecklistComplete?1:0;
        public void RecordApplied(CognitionState state)
        {
            var current=new HashSet<CognitionSignal>();
            foreach(var sentence in state.Groups)
                if(sentence.Effective)foreach(var signal in sentence.Signals)current.Add(signal);
            foreach(var signal in current)if(!appliedSignals.Contains(signal))Progress.Record(signal);
            appliedSignals.Clear();
            appliedSignals.UnionWith(current);
        }
        void Update()=>TryClearFromShortcut(Keyboard.current!=null&&Keyboard.current.xKey.wasPressedThisFrame);
        public bool TryClearFromShortcut(bool pressed)
        {
            if(!pressed)return false;
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            var input=selected?selected.GetComponentInParent<InputField>():null;
            if(input&&input.isFocused)return false;
            ClearProgress();
            return true;
        }
        public void Record(CognitionSignal signal)=>Progress.Record(signal);
        // NightEnvironment asks before changing sky/fog/light. Fade the inscription first.
        public bool PrepareNightTransition(bool night)
        {
            nightRequested=night;
            return !night||!isActiveAndEnabled||!group||group.alpha<=.001f;
        }
        void LateUpdate()
        {
            if(!root||!opening)return;
            bool active=opening.Ready&&!opening.effects.Suspended;
            if(!active){group.alpha=0;return;}
            Elapsed+=Time.unscaledDeltaTime;
            if(!Revealed)
            {
                Revealed=true;
                Bearing=-Vector3.ProjectOnPlane(opening.player.view.forward,Vector3.up).normalized;
                if(Bearing.sqrMagnitude<.1f)Bearing=-Vector3.ProjectOnPlane(opening.player.transform.forward,Vector3.up).normalized;
            }
            int desired=DesiredStage();
            float target=Revealed&&!nightRequested&&DisplayStage!=2&&desired==DisplayStage?opacity:0;
            Visibility=Mathf.MoveTowards(Visibility,target,Time.unscaledDeltaTime/Mathf.Max(.1f,fadeDuration));
            if(desired!=DisplayStage&&Visibility<=.001f){DisplayStage=desired;BuildRows();}
            group.alpha=Visibility;
            var eye=opening.player.view;
            root.position=eye.position+(Bearing+Vector3.up*Mathf.Tan(elevation*Mathf.Deg2Rad))*viewingDistance;
            if(Bearing.sqrMagnitude>.1f)root.rotation=Quaternion.LookRotation(Bearing,Vector3.up);
            root.localScale=Vector3.one*(2*viewingDistance*Mathf.Tan(angularWidth*.5f*Mathf.Deg2Rad)/420);
            Color ink=Color.Lerp(dayInk,nightInk,environment?environment.NightBlend:0);
            foreach(var row in rows)
            {
                bool done=Progress.Has(row.signal);
                foreach(var symbol in row.symbols){var color=ink;color.a=done?.55f:1;symbol.color=color;}
                row.strikeProgress=Mathf.MoveTowards(row.strikeProgress,done?1:0,Time.unscaledDeltaTime*2);
                row.strike.rectTransform.sizeDelta=new Vector2(row.width*row.strikeProgress,3);
                row.strike.color=ink;
            }
        }
        void BuildRows()
        {
            for(int i=root.childCount-1;i>=0;i--){var child=root.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}
            if(DisplayStage==2){rows=new Row[0];return;}
            rows=DisplayStage==0?new[]{
                CreateRow(CognitionSignal.WorldOff,new[]{"negative","world"},110),
                CreateRow(CognitionSignal.LightMove,new[]{"light","move"},0),
                CreateRow(CognitionSignal.LightWorldMove,new[]{"light","ground","move"},-110)}:
                new[]{CreateRow(CognitionSignal.LightOff,new[]{"negative","light"},0)};
        }
        Row CreateRow(CognitionSignal signal,string[] words,float y)
        {
            var row=new Row{signal=signal,symbols=new Image[words.Length],width=words.Length*92-16,strikeProgress=Progress.Has(signal)?1:0};
            for(int i=0;i<words.Length;i++)
            {
                var image=NewImage(signal+" "+words[i]);image.sprite=opening.board.Catalog.Word(words[i]).Symbol;image.preserveAspect=true;
                image.rectTransform.sizeDelta=new Vector2(76,76);
                image.rectTransform.anchoredPosition=new Vector2((i-(words.Length-1)*.5f)*92,y);
                row.symbols[i]=image;
            }
            row.strike=NewImage(signal+" completed");
            row.strike.rectTransform.pivot=new Vector2(0,.5f);
            row.strike.rectTransform.anchoredPosition=new Vector2(-row.width*.5f,y);
            row.strike.rectTransform.sizeDelta=new Vector2(row.width*row.strikeProgress,3);
            return row;
        }
        Image NewImage(string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(root,false);
            var image=go.GetComponent<Image>();image.raycastTarget=false;image.material=inkMaterial;return image;
        }
        [ContextMenu("清除本地天空提示完成记录")]
        public void ClearProgress()
        {
            Progress.Clear();
            DisplayStage=0;
            if(root)BuildRows();
        }
        void OnDisable(){if(group)group.alpha=0;}
    }
}
