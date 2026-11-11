using System.Collections.Generic;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    [DefaultExecutionOrder(-50)]
    public sealed class WordLibrary : MonoBehaviour
    {
        public CognitionBoard board;
        [System.Serializable] public struct VocabularyImage { public string wordId; public Texture2D image; }
        [UnityEngine.Serialization.FormerlySerializedAs("memoryPresets")]
        public VocabularyImage[] vocabularyImages=new VocabularyImage[0];
        public SnapshotSequence moveMemory;
        Material memoryPrint;
        float playbackStart;
        Vector2? dismissPress;
        [Header("Prefab references — edit appearance on the UI components")]
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform column,details,sidebarDivider;
        [SerializeField] Image entryPrefab;
        [SerializeField] RawImage snapshot;
        [SerializeField] AspectRatioFitter snapshotAspect;
        [SerializeField] GameObject snapshotFrame, gapBeforeSnapshot, gapBeforeNote;
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
        readonly Dictionary<string,string> savedNotes=new Dictionary<string,string> { ["world"]="game" };
        string selected;
        int draggedGroup=-1;
        readonly Dictionary<string,string> drafts=new Dictionary<string,string>();
        readonly Dictionary<string,Texture2D> photos=new Dictionary<string,Texture2D>();
        readonly List<Texture2D> capturedPhotos=new List<Texture2D>();
        readonly Dictionary<string,Button> entries=new Dictionary<string,Button>();
        readonly List<RaycastResult> hoverHits=new List<RaycastResult>();
        void Start()
        {
            memoryPrint=snapshot.material;
            note.lineType=InputField.LineType.SingleLine;
            note.caretBlinkRate=.85f;
            note.caretWidth=2;
            note.onValueChanged.AddListener(EditNote);
            note.onSubmit.AddListener(SubmitNote);
            saveNote.onClick.AddListener(SaveNote);
            closeMemory.onClick.AddListener(CloseMemory);
            board.State.Changed+=Refresh;
            board.Panel.OpenChanged.AddListener(OnPanel);
            Refresh(CognitionChange.Add);Show(false);
        }
        void EditNote(string value){if(selected!=null)drafts[selected]=value;UpdateSaveButton();}
        void SubmitNote(string value){if(IsOpen&&!board.Panel.InputBlocked)SaveNote();}
        void CloseMemory()=>Show(false);
        void OnPanel(bool open){if(!open){CancelLibraryDrag();Show(false);}}
        public void Show(bool value)
        {
            tooltip.gameObject.SetActive(false);
            dismissPress=null;
            IsOpen=value;details.gameObject.SetActive(value);
            board.Surface.gameObject.SetActive(!value);
            if(value)
            {
                note.Select();
                note.ActivateInputField();
                if(selected=="world")StartCoroutine(PlaceWorldCaret());
            }
        }
        System.Collections.IEnumerator PlaceWorldCaret()
        {
            // InputField selects all text when it processes activation in LateUpdate.
            yield return null;
            if(IsOpen&&selected=="world"&&note.isFocused)note.MoveTextEnd(false);
        }
        void Refresh(CognitionChange change)
        {
            foreach(var id in new List<string>(entries.Keys))
                if(!board.State.Knows(id))
                { entries[id].gameObject.SetActive(false); Destroy(entries[id].gameObject); entries.Remove(id); }
            if(selected!=null&&!board.State.Knows(selected)){selected=null;Show(false);}
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
        public void ForgetMemories(IEnumerable<string> ids)
        {
            CancelLibraryDrag();
            foreach(var id in ids)
            {
                if(photos.TryGetValue(id,out var photo)&&photo&&capturedPhotos.Remove(photo))Destroy(photo);
                photos.Remove(id);savedNotes.Remove(id);drafts.Remove(id);
            }
        }
        public void Remember(string id)
        {
            if(photos.ContainsKey(id))return;
            Texture2D photo=null;
            if(HasPhoto(id))foreach(var preset in vocabularyImages)if(preset.wordId==id){photo=preset.image;break;}
            photos[id]=photo;if(selected==id)UpdatePhoto();
        }
        static bool HasPhoto(string id)=>id!="move"&&id!="positive"&&id!="negative";
        public void Remember(string id,RenderTexture frame)
        {
            if(photos.ContainsKey(id))return;
            if(!HasPhoto(id)||!frame){Remember(id);return;}
            const float aspect=2f/3;
            float sourceAspect=(float)frame.width/frame.height;
            var scale=new Vector2(Mathf.Min(1,aspect/sourceAspect),Mathf.Min(1,sourceAspect/aspect));
            int height=Mathf.Clamp(Mathf.RoundToInt(frame.height*scale.y),2,768);
            var previous=RenderTexture.active;
            var print=RenderTexture.GetTemporary(Mathf.Max(1,Mathf.RoundToInt(height*aspect)),height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(frame,print,scale,(Vector2.one-scale)*.5f);
                RenderTexture.active=print;
                var photo=new Texture2D(print.width,print.height,TextureFormat.RGB24,false){name="Learning "+id};
                photo.ReadPixels(new Rect(0,0,print.width,print.height),0,0);photo.Apply(false,true);
                capturedPhotos.Add(photo);photos[id]=photo;if(selected==id)UpdatePhoto();
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(print);}
        }
        public void Select(string id)
        {
            if(!board.State.Knows(id))return;
            if(id!="light")Remember(id);
            selected=id;playbackStart=Time.unscaledTime;UpdatePhoto();
            note.SetTextWithoutNotify(drafts.TryGetValue(id,out var draft)?draft:SavedNote(id));
            UpdateSaveButton();Show(true);
        }
        void UpdatePhoto()
        {
            snapshotAspect.aspectMode=selected=="world"||selected=="mirror"
                ?AspectRatioFitter.AspectMode.FitInParent:AspectRatioFitter.AspectMode.EnvelopeParent;
            snapshot.uvRect=new Rect(0,0,1,1);
            snapshot.texture=photos.TryGetValue(selected,out var photo)?photo:null;
            if(selected=="move"&&moveMemory&&moveMemory.atlas)
            {
                snapshot.texture=moveMemory.atlas;
                snapshot.uvRect=moveMemory.Frame(Time.unscaledTime-playbackStart);
            }
            snapshot.material=selected=="light"?null:memoryPrint;
            snapshot.enabled=snapshot.texture;
            if(snapshotFrame.activeSelf!=snapshot.enabled)
            {
                // The save row and its gap occupy 58 units below the note; the close row occupies 40 above.
                var stack=(RectTransform)snapshotFrame.transform.parent;
                stack.anchoredPosition=new Vector2(stack.anchoredPosition.x,snapshot.enabled?0f:-9f);
                snapshotFrame.SetActive(snapshot.enabled);
            }
            gapBeforeSnapshot.SetActive(snapshot.enabled);
            gapBeforeNote.SetActive(snapshot.enabled);
            float exposure=selected=="door"?.76f:1f;
            float opacity=1f;
            if(selected=="move"&&moveMemory&&moveMemory.framesPerSecond>0)
            {
                float duration=moveMemory.frames/moveMemory.framesPerSecond;
                float time=Mathf.Repeat(Time.unscaledTime-playbackStart,duration);
                opacity=Mathf.SmoothStep(0f,1f,Mathf.Min(time,duration-time)/.16f);
            }
            snapshot.color=new Color(exposure,exposure,exposure,opacity);
            if(snapshot.texture)snapshotAspect.aspectRatio=selected=="move"&&moveMemory?moveMemory.aspect:(float)snapshot.texture.width/snapshot.texture.height;
        }
        void UpdateSaveButton(){saveNote.interactable=selected!=null&&note.text.Trim()!=SavedNote(selected);}
        public void SaveNote()
        {
            if(selected==null)return;
            string value=note.text.Trim();
            if(value.Length==0)savedNotes.Remove(selected);else savedNotes[selected]=value;
            drafts.Remove(selected);note.SetTextWithoutNotify(value);UpdateSaveButton();
        }
        void LateUpdate()
        {
            UpdateTooltip();
            if(!IsOpen)return;
            CheckPanelClick();
            if(IsOpen&&selected=="move")UpdatePhoto();
        }

        void CheckPanelClick()
        {
            var mouse=Mouse.current;
            if(board.Panel.InputBlocked||mouse==null||!EventSystem.current)
            {
                dismissPress=null;
                return;
            }
            var position=mouse.position.ReadValue();
            if(mouse.leftButton.wasPressedThisFrame)
                dismissPress=IsDismissArea(position)?position:(Vector2?)null;
            if(!dismissPress.HasValue)return;
            float threshold=EventSystem.current.pixelDragThreshold;
            if((position-dismissPress.Value).sqrMagnitude>threshold*threshold)
                dismissPress=null;
            if(mouse.leftButton.wasReleasedThisFrame)
            {
                if(dismissPress.HasValue&&IsDismissArea(position))CloseMemory();
                dismissPress=null;
            }
        }

        bool IsDismissArea(Vector2 screen)
        {
            var panel=(RectTransform)board.Panel.ContentRoot.parent;
            if(!RectTransformUtility.RectangleContainsScreenPoint(panel,screen))return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(sidebarDivider,screen,null,out var side);
            if(side.x<=sidebarDivider.rect.xMax)return false;

            // Protect the continuous photo-to-note rectangle, including its gutter and a small outer margin.
            var field=(RectTransform)note.transform;
            var bounds=field.rect;
            if(snapshotFrame.activeSelf)
            {
                var frame=(RectTransform)snapshotFrame.transform;
                bounds.max=Vector2.Max(bounds.max,field.InverseTransformPoint(frame.TransformPoint(frame.rect.max)));
                bounds.min=Vector2.Min(bounds.min,field.InverseTransformPoint(frame.TransformPoint(frame.rect.min)));
            }
            bounds.min-=Vector2.one*10;
            bounds.max+=Vector2.one*10;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(field,screen,null,out var local);
            return !bounds.Contains(local);
        }
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
            var anchor=(RectTransform)target.transform;
            var bounds=anchor.rect;
            float gap=tooltipOffset.y;
            bool isTile=target.TryGetComponent<LibraryDrag>(out _);
            if(isTile)gap=-6f;
            else if(target.TryGetComponent<CenteredSymbolImage>(out var symbol))
                bounds=SymbolUI.InkRect(symbol);
            var top=root.InverseTransformPoint(anchor.TransformPoint(new Vector3(bounds.center.x,bounds.yMax)));
            float x=Mathf.Clamp(top.x-width*.5f+tooltipOffset.x,root.rect.xMin+8,root.rect.xMax-width-8);
            float y=top.y+gap+height;
            if(isTile&&y>root.rect.yMax-8)
                y=root.InverseTransformPoint(anchor.TransformPoint(new Vector3(bounds.center.x,bounds.yMin))).y-gap;
            y=Mathf.Clamp(y,root.rect.yMin+height+8,root.rect.yMax-8);
            tooltip.localPosition=new Vector3(x,y,0);tooltip.SetAsLastSibling();
        }
        public void BeginLibraryDrag(WordDefinition word,Vector2 screen)
        {
            if(board.Panel.InputBlocked)return;
            CancelLibraryDrag();Show(false);var group=board.State.Spawn(word);if(group==null)return;
            draggedGroup=group.Id;board.BeginDrag(group.Id,group.Words[0].Id,false,screen,true);
        }
        public void EndLibraryDrag(Vector2 screen)
        {
            if(draggedGroup<0)return;
            if(RectTransformUtility.RectangleContainsScreenPoint(board.Surface,screen))board.EndDrag(screen);
            else
            {
                var entry=FindFirstObjectByType<WorldEntry>();var group=board.State.Find(draggedGroup);
                if(entry&&entry.outdoor&&group!=null)entry.outdoor.TryWorldDrop(board,group.Words[0].Meaning,screen);
                CancelLibraryDrag();
            }
            draggedGroup=-1;
        }
        void CancelLibraryDrag()
        {
            if(draggedGroup<0)return;
            board.CancelDrag();
            board.State.ReturnToLibrary(draggedGroup,0,true);
            draggedGroup=-1;
        }
        void OnDestroy()
        {
            foreach(var photo in capturedPhotos)if(photo)Destroy(photo);
            if(board&&board.State!=null)board.State.Changed-=Refresh;
            if(board&&board.Panel)board.Panel.OpenChanged.RemoveListener(OnPanel);
            if(note)note.onValueChanged.RemoveListener(EditNote);
            if(note)note.onSubmit.RemoveListener(SubmitNote);
            if(saveNote)saveNote.onClick.RemoveListener(SaveNote);
            if(closeMemory)closeMemory.onClick.RemoveListener(CloseMemory);
        }
    }
}
