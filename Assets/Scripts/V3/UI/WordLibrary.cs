using System.Collections.Generic;
using PuzzleApple.V3.Cognition;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PuzzleApple.V3
{
    [DefaultExecutionOrder(-50)]
    public sealed class WordLibrary : MonoBehaviour
    {
        public CognitionBoard board;
        public Material snapshotMaterial;
        [System.Serializable] public struct MemoryPreset { public string wordId; public Texture2D image; }
        public MemoryPreset[] memoryPresets=new MemoryPreset[0];
        public bool IsOpen { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public RawImage SnapshotImage => snapshot;
        public string Selected => selected;
        RectTransform column,details;
        RawImage snapshot;
        Material photoMaterial;
        string selected;
        int draggedGroup=-1;
        readonly Dictionary<string,Texture2D> photos=new Dictionary<string,Texture2D>();
        readonly Dictionary<string,Image> entries=new Dictionary<string,Image>();
        static readonly Color Ink=new Color(.88f,.88f,.88f);
        void Start()
        {
            board.Panel.BackgroundColor=new Color(.013f,.013f,.013f,1);
            var rail=CognitionUI.Rect("Vocabulary rail",board.Panel.ContentRoot);
            rail.anchorMin=Vector2.zero;rail.anchorMax=new Vector2(0,1);rail.offsetMin=new Vector2(24,56);rail.offsetMax=new Vector2(158,-124);
            var divider=CognitionUI.Image("Column divider",board.Panel.ContentRoot,Ink);divider.rectTransform.anchorMin=Vector2.zero;divider.rectTransform.anchorMax=new Vector2(0,1);divider.rectTransform.offsetMin=new Vector2(176,40);divider.rectTransform.offsetMax=new Vector2(178,-48);
            var hit=rail.gameObject.AddComponent<Image>();hit.color=Color.clear;
            Scroll=rail.gameObject.AddComponent<VocabularyScrollRect>();Scroll.horizontal=false;Scroll.vertical=true;Scroll.movementType=ScrollRect.MovementType.Clamped;Scroll.scrollSensitivity=32;Scroll.inertia=true;Scroll.decelerationRate=.08f;
            var viewport=CognitionUI.Rect("Vocabulary viewport",rail);CognitionUI.Stretch(viewport);viewport.offsetMax=new Vector2(-32,0);viewport.gameObject.AddComponent<RectMask2D>();
            column=CognitionUI.Rect("Vocabulary column",viewport);column.anchorMin=new Vector2(0,1);column.anchorMax=Vector2.one;column.pivot=new Vector2(.5f,1);column.sizeDelta=Vector2.zero;
            var layout=column.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=22;layout.padding=new RectOffset(0,0,34,34);layout.childAlignment=TextAnchor.UpperCenter;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            Scroll.viewport=viewport;Scroll.content=column;
            var track=CognitionUI.Rect("Vocabulary scrollbar",board.Panel.ContentRoot);track.anchorMin=new Vector2(0,.26f);track.anchorMax=new Vector2(0,.74f);track.pivot=Vector2.one*.5f;track.offsetMin=new Vector2(134,0);track.offsetMax=new Vector2(158,0);
            var trackHit=track.gameObject.AddComponent<Image>();trackHit.color=Color.clear;
            var line=CognitionUI.Image("Scroll track",track,new Color(.8f,.8f,.8f,.18f));line.rectTransform.anchorMin=new Vector2(.5f,0);line.rectTransform.anchorMax=new Vector2(.5f,1);line.rectTransform.pivot=Vector2.one*.5f;line.rectTransform.sizeDelta=new Vector2(2,0);
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
            var area=CognitionUI.Rect("Sliding area",track);CognitionUI.Stretch(area);
            var handle=CognitionUI.Image("Scroll thumb",area,Ink);handle.raycastTarget=true;CognitionUI.Stretch(handle.rectTransform);handle.rectTransform.offsetMin=new Vector2(9,0);handle.rectTransform.offsetMax=new Vector2(-9,0);
            bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;bar.navigation=new Navigation{mode=Navigation.Mode.None};Scroll.verticalScrollbar=bar;Scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;bar.onValueChanged.AddListener(((VocabularyScrollRect)Scroll).OnScrollbarChanged);
            details=CognitionUI.Rect("Word memory",board.Panel.ContentRoot);CognitionUI.Stretch(details);details.offsetMin=new Vector2(214,40);details.offsetMax=new Vector2(-32,-48);
            var backing=details.gameObject.AddComponent<Image>();backing.color=board.Panel.BackgroundColor;backing.raycastTarget=true;
            var close=CognitionUI.Rect("Close memory",details);close.anchorMin=close.anchorMax=new Vector2(1,1);close.pivot=Vector2.one;close.sizeDelta=new Vector2(40,40);
            var closeHit=close.gameObject.AddComponent<Image>();closeHit.color=Color.clear;
            var closeButton=close.gameObject.AddComponent<Button>();closeButton.targetGraphic=closeHit;closeButton.onClick.AddListener(()=>Show(false));closeButton.navigation=new Navigation{mode=Navigation.Mode.None};
            foreach(float angle in new[]{45f,-45f}){var stroke=CognitionUI.Image("Close stroke",close,Ink);V3Presentation.Center(stroke.rectTransform,24,2.5f);stroke.rectTransform.localRotation=Quaternion.Euler(0,0,angle);}
            var photoArea=CognitionUI.Rect("Memory print",details);photoArea.anchorMin=new Vector2(0,.40f);photoArea.anchorMax=Vector2.one;photoArea.offsetMin=new Vector2(0,0);photoArea.offsetMax=new Vector2(0,-48);
            Frame(photoArea);
            snapshot=CognitionUI.Rect("Acquisition snapshot",photoArea).gameObject.AddComponent<RawImage>();snapshot.raycastTarget=false;CognitionUI.Stretch(snapshot.rectTransform);
            snapshot.rectTransform.pivot=Vector2.one*.5f;
            snapshot.rectTransform.offsetMin=Vector2.one*3;snapshot.rectTransform.offsetMax=-Vector2.one*3;
            if(snapshotMaterial){photoMaterial=new Material(snapshotMaterial);snapshot.material=photoMaterial;}
            board.State.Changed+=Refresh;board.Panel.OpenChanged.AddListener(OnPanel);Refresh(CognitionChange.Add);Show(false);
        }
        void OnPanel(bool open){if(!open){CancelLibraryDrag();Show(false);}}
        public void Show(bool value)
        {
            IsOpen=value;if(details)details.gameObject.SetActive(value);
            if(board.Surface)board.Surface.gameObject.SetActive(!value);
            foreach(var pair in entries)pair.Value.color=value&&pair.Key==selected?new Color(.9f,.9f,.9f,.055f):Color.clear;
        }
        void Refresh(CognitionChange change)
        {
            foreach(var word in board.Catalog.Words)
            {
                if(!board.State.Knows(word.Id)||entries.ContainsKey(word.Id))continue;
                var tile=CognitionUI.Image("Vocabulary "+word.Id,column,Color.clear);tile.raycastTarget=true;
                var size=tile.gameObject.AddComponent<LayoutElement>();size.preferredHeight=148;
                var button=tile.gameObject.AddComponent<Button>();button.targetGraphic=tile;button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(()=>Select(word.Id));
                var symbol=SymbolUI.Create("Library symbol",tile.transform,word,Ink);V3Presentation.Center(symbol.rectTransform,48,56);
                tile.gameObject.AddComponent<LibraryDrag>().Initialize(this,word);entries.Add(word.Id,tile);
            }
        }
        public void Remember(string id)
        {
            Texture2D photo=null;
            if(id!="i")foreach(var preset in memoryPresets)if(preset.wordId==id){photo=preset.image;break;}
            photos[id]=photo;
            if(selected==id)UpdatePhoto();
        }
        public void Select(string id)
        {
            if(!board.State.Knows(id))return;selected=id;UpdatePhoto();Show(true);
        }
        void UpdatePhoto()
        {
            snapshot.texture=photos.TryGetValue(selected,out var photo)?photo:null;
            snapshot.color=snapshot.texture?Color.white:Color.clear;
            FitPhoto();
        }
        void LateUpdate(){if(IsOpen)FitPhoto();}
        void FitPhoto()
        {
            if(!snapshot||!snapshot.texture)return;
            float source=(float)snapshot.texture.width/snapshot.texture.height;
            float target=snapshot.rectTransform.rect.width/Mathf.Max(1,snapshot.rectTransform.rect.height);
            float width=Mathf.Min(1,target/source),height=Mathf.Min(1,source/target);
            snapshot.uvRect=new Rect((1-width)*.5f,(1-height)*.5f,width,height);
        }
        static void Frame(RectTransform parent)
        {
            foreach(var side in new[]{0,1,2,3})
            {
                var line=CognitionUI.Image("Frame edge",parent,Ink).rectTransform;
                if(side<2){line.anchorMin=new Vector2(side,0);line.anchorMax=new Vector2(side,1);line.sizeDelta=new Vector2(1.5f,0);}
                else{line.anchorMin=new Vector2(0,side-2);line.anchorMax=new Vector2(1,side-2);line.sizeDelta=new Vector2(0,1.5f);}
                line.pivot=Vector2.one*.5f;line.anchoredPosition=Vector2.zero;
            }
        }
        public void BeginLibraryDrag(WordDefinition word,Vector2 screen)
        {
            CancelLibraryDrag();Show(false);var group=board.State.Spawn(word);if(group==null)return;
            draggedGroup=group.Id;board.BeginDrag(group.Id,group.Words[0].Id,false,screen,true);
        }
        public void EndLibraryDrag(Vector2 screen)
        {
            if(draggedGroup<0)return;
            if(RectTransformUtility.RectangleContainsScreenPoint(board.Surface,screen))board.EndDrag(screen);else CancelLibraryDrag();
            draggedGroup=-1;
        }
        void CancelLibraryDrag(){if(draggedGroup<0)return;board.RemoveToken(draggedGroup,0,true);draggedGroup=-1;}
        void OnDestroy(){if(board&&board.State!=null)board.State.Changed-=Refresh;if(board&&board.Panel)board.Panel.OpenChanged.RemoveListener(OnPanel);if(photoMaterial)Destroy(photoMaterial);}
    }
    public sealed class VocabularyScrollRect : ScrollRect
    {
        float freePosition=1;
        bool synchronizing,wasOverflowing;
        bool Overflowing => content&&viewport&&content.rect.height>viewport.rect.height+.5f;
        public void OnScrollbarChanged(float value)
        {
            if(!synchronizing&&!Overflowing)freePosition=value;
        }
        protected override void SetNormalizedPosition(float value,int axis)
        {
            if(axis==1&&!Overflowing)return;
            base.SetNormalizedPosition(value,axis);
        }
        public override void OnScroll(PointerEventData e)
        {
            if(Overflowing){base.OnScroll(e);return;}
            if(verticalScrollbar)verticalScrollbar.value=Mathf.Clamp01(freePosition+e.scrollDelta.y*.1f);
        }
        public override void OnDrag(PointerEventData e)
        {
            if(Overflowing)base.OnDrag(e);
        }
        public override void Rebuild(CanvasUpdate update)
        {
            synchronizing=true;
            try{base.Rebuild(update);ApplyScrollbar();}finally{synchronizing=false;}
        }
        protected override void LateUpdate()
        {
            synchronizing=true;
            try
            {
                bool overflow=Overflowing;
                if(overflow&&!wasOverflowing){StopMovement();verticalNormalizedPosition=1;}
                base.LateUpdate();ApplyScrollbar();wasOverflowing=overflow;
            }
            finally{synchronizing=false;}
        }
        void ApplyScrollbar()
        {
            if(!verticalScrollbar||!content||!viewport)return;
            verticalScrollbar.size=Mathf.Min(.28f,verticalScrollbar.size);
            verticalScrollbar.interactable=true;
            if(!Overflowing)
            {
                StopMovement();var p=content.anchoredPosition;p.y=0;content.anchoredPosition=p;
                verticalScrollbar.SetValueWithoutNotify(freePosition);
            }
        }
        protected override void OnDestroy()
        {
            if(verticalScrollbar)verticalScrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
            base.OnDestroy();
        }
    }
    public sealed class LibraryDrag : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler,IInitializePotentialDragHandler
    {
        WordLibrary library;WordDefinition word;bool dragging;
        public void Initialize(WordLibrary owner,WordDefinition definition){library=owner;word=definition;}
        public void OnInitializePotentialDrag(PointerEventData e){e.useDragThreshold=true;library.Scroll.OnInitializePotentialDrag(e);}
        public void OnBeginDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left)return;e.eligibleForClick=false;
            library.Scroll.StopMovement();
            dragging=true;library.BeginLibraryDrag(word,e.position);
        }
        public void OnDrag(PointerEventData e){if(dragging)library.board.MoveDrag(e.position);}
        public void OnEndDrag(PointerEventData e){if(dragging)library.EndLibraryDrag(e.position);dragging=false;}
    }
}
