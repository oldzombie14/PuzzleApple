using System.Collections.Generic;
using PuzzleApple.V3.Cognition;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V3
{
    [DefaultExecutionOrder(-50)]
    public sealed class WordLibrary : MonoBehaviour
    {
        public CognitionBoard board;
        [System.Serializable] public struct VocabularyImage { public string wordId; public Texture2D image; }
        [UnityEngine.Serialization.FormerlySerializedAs("memoryPresets")]
        public VocabularyImage[] vocabularyImages=new VocabularyImage[0];
        [Header("Prefab references — edit appearance on the UI components")]
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform column,details;
        [SerializeField] Image entryPrefab;
        [SerializeField] RawImage snapshot;
        [SerializeField] AspectRatioFitter snapshotAspect;
        [SerializeField] InputField note;
        [SerializeField] Button saveNote,closeMemory;
        [SerializeField] RectTransform tooltip;
        [SerializeField] Text tooltipText;
        [SerializeField] LayoutElement tooltipTextLayout;
        [SerializeField] Vector2 tooltipOffset=new Vector2(0,12);
        public bool IsOpen { get; private set; }
        public ScrollRect Scroll => scroll;
        public RawImage SnapshotImage => snapshot;
        public string Selected => selected;
        public InputField NoteField => note;
        public string SavedNote(string id) => savedNotes.TryGetValue(id,out var value)?value:"";
        readonly Dictionary<string,string> savedNotes=new Dictionary<string,string>();
        string selected;
        int draggedGroup=-1;
        readonly Dictionary<string,string> drafts=new Dictionary<string,string>();
        readonly Dictionary<string,Texture2D> photos=new Dictionary<string,Texture2D>();
        readonly List<Texture2D> capturedPhotos=new List<Texture2D>();
        readonly Dictionary<string,Button> entries=new Dictionary<string,Button>();
        readonly List<RaycastResult> hoverHits=new List<RaycastResult>();
        void Start()
        {
            note.onValueChanged.AddListener(EditNote);
            saveNote.onClick.AddListener(SaveNote);
            closeMemory.onClick.AddListener(CloseMemory);
            board.State.Changed+=Refresh;
            board.Panel.OpenChanged.AddListener(OnPanel);
            Refresh(CognitionChange.Add);Show(false);
        }
        void EditNote(string value){if(selected!=null)drafts[selected]=value;UpdateSaveButton();}
        void CloseMemory()=>Show(false);
        void OnPanel(bool open){if(!open){CancelLibraryDrag();Show(false);}}
        public void Show(bool value)
        {
            tooltip.gameObject.SetActive(false);
            IsOpen=value;details.gameObject.SetActive(value);
            board.Surface.gameObject.SetActive(!value);
        }
        void Refresh(CognitionChange change)
        {
            foreach(var word in board.Catalog.Words)
            {
                if(!board.State.Knows(word.Id)||entries.ContainsKey(word.Id))continue;
                var tile=Instantiate(entryPrefab,column);tile.name="Vocabulary "+word.Id;
                var button=tile.GetComponent<Button>();button.onClick.AddListener(()=>Select(word.Id));
                tile.transform.Find("Library symbol").GetComponent<Image>().sprite=word.Symbol;
                tile.GetComponent<LibraryDrag>().Initialize(this,word);
                tile.GetComponent<WordNoteTarget>().WordId=word.Id;
                entries.Add(word.Id,button);
            }
        }
        public void Remember(string id)
        {
            Texture2D photo=null;
            if(id!="i")foreach(var preset in vocabularyImages)if(preset.wordId==id){photo=preset.image;break;}
            photos[id]=photo;if(selected==id)UpdatePhoto();
        }
        public void Remember(string id,RenderTexture frame)
        {
            if(id=="i"||!frame){Remember(id);return;}
            var previous=RenderTexture.active;
            try
            {
                RenderTexture.active=frame;
                var photo=new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false){name="Learning "+id};
                photo.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);photo.Apply();
                capturedPhotos.Add(photo);photos[id]=photo;if(selected==id)UpdatePhoto();
            }
            finally{RenderTexture.active=previous;}
        }
        public void Select(string id)
        {
            if(!board.State.Knows(id))return;
            selected=id;UpdatePhoto();
            note.SetTextWithoutNotify(drafts.TryGetValue(id,out var draft)?draft:SavedNote(id));
            UpdateSaveButton();Show(true);
        }
        void UpdatePhoto()
        {
            snapshot.texture=photos.TryGetValue(selected,out var photo)?photo:null;
            snapshot.enabled=snapshot.texture;
            if(snapshot.texture)snapshotAspect.aspectRatio=(float)snapshot.texture.width/snapshot.texture.height;
        }
        void UpdateSaveButton(){saveNote.interactable=selected!=null&&note.text.Trim()!=SavedNote(selected);}
        public void SaveNote()
        {
            if(selected==null)return;
            string value=note.text.Trim();
            if(value.Length==0)savedNotes.Remove(selected);else savedNotes[selected]=value;
            drafts.Remove(selected);note.SetTextWithoutNotify(value);UpdateSaveButton();
        }
        void LateUpdate()=>UpdateTooltip();
        void UpdateTooltip()
        {
            tooltip.gameObject.SetActive(false);
            if(!board.Panel.IsOpen||board.IsDragging||Mouse.current==null||!EventSystem.current)return;
            var mouse=Mouse.current;
            if(mouse.leftButton.isPressed||mouse.rightButton.isPressed)return;
            var pointer=new PointerEventData(EventSystem.current){position=mouse.position.ReadValue()};
            hoverHits.Clear();EventSystem.current.RaycastAll(pointer,hoverHits);
            if(hoverHits.Count==0)return;
            var target=hoverHits[0].gameObject.GetComponentInParent<WordNoteTarget>();
            if(!target)return;
            string value=SavedNote(target.WordId);if(string.IsNullOrWhiteSpace(value))return;
            tooltipText.text=value;
            // Native layout sizes the bubble; only constrain long notes at the screen edge.
            var root=board.Panel.ContentRoot;
            var layout=tooltip.GetComponent<HorizontalLayoutGroup>();
            float limit=root.rect.width-layout.padding.horizontal-16;
            tooltipTextLayout.preferredWidth=tooltipText.preferredWidth>limit?limit:-1;
            tooltip.gameObject.SetActive(true);LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip);
            float width=tooltip.rect.width,height=tooltip.rect.height;
            var symbol=target.GetComponentInChildren<CenteredSymbolImage>();
            var glyph=symbol?symbol.rectTransform:(RectTransform)target.transform;
            var ink=symbol?SymbolUI.InkRect(symbol):glyph.rect;
            var p=root.InverseTransformPoint(glyph.TransformPoint(new Vector3(ink.center.x,ink.yMax,0)));
            float x=Mathf.Clamp(p.x-width*.5f+tooltipOffset.x,root.rect.xMin+8,root.rect.xMax-width-8);
            float y=Mathf.Clamp(p.y+tooltipOffset.y+height,root.rect.yMin+height+8,root.rect.yMax-8);
            tooltip.localPosition=new Vector3(x,y,0);tooltip.SetAsLastSibling();
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
        void OnDestroy()
        {
            foreach(var photo in capturedPhotos)if(photo)Destroy(photo);
            if(board&&board.State!=null)board.State.Changed-=Refresh;
            if(board&&board.Panel)board.Panel.OpenChanged.RemoveListener(OnPanel);
            if(note)note.onValueChanged.RemoveListener(EditNote);
            if(saveNote)saveNote.onClick.RemoveListener(SaveNote);
            if(closeMemory)closeMemory.onClick.RemoveListener(CloseMemory);
        }
    }
}
