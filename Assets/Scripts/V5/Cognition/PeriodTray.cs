using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PuzzleApple.V5.Cognition
{
    [System.Serializable]
    public sealed class PeriodStyle
    {
        [Min(8)] public float hitSize=64;
        [Min(2)] public float diameter=11;
        [Min(.1f)] public float breathSeconds=2.5f;
        [Min(.01f)] public float hoverSeconds=.15f,returnSeconds=.22f;
        [Min(.1f)] public float rippleSeconds=.88f;
        [Min(0)] public float rippleScale=2,rippleGrowth=1.5f;
        [Range(0,1)] public float rippleOpacity=.27f;
    }
    public sealed class PeriodTray : MonoBehaviour
    {
        CognitionBoard board;
        RectTransform layer;
        PeriodGlyph ghost;
        readonly Dictionary<int,PeriodGlyph> attached=new Dictionary<int,PeriodGlyph>();
        readonly HashSet<int> returning=new HashSet<int>();
        Color chalk;
        int source;
        PeriodStyle Style=>board.punctuation;
        public bool Dragging=>ghost;
        public void Initialize(CognitionBoard owner,RectTransform overlay,Color ink){
            board=owner;layer=overlay;chalk=ink;
        }
        PeriodGlyph Glyph(string name,RectTransform parent,int id,bool touch){
            var token=new GameObject(name,typeof(RectTransform),typeof(PeriodGlyph)).GetComponent<PeriodGlyph>();
            token.rectTransform.SetParent(parent,false);token.rectTransform.sizeDelta=Vector2.one*Style.hitSize;
            token.diameter=Style.diameter;token.owner=this;token.group=id;token.color=chalk;token.raycastTarget=touch;
            token.born=Time.unscaledTime;token.opacity=CognitionBoard.ConflictAlpha;
            token.gameObject.SetActive(touch);return token;
        }
        public void Refresh(){
            var active=new HashSet<int>();
            foreach(var group in board.State.Groups){
                if(group.Independent)continue;
                active.Add(group.Id);
                if(!attached.TryGetValue(group.Id,out var token)||!token){
                    // Independent of word opacity, inside the panel mask and fade.
                    token=Glyph("Sentence period",board.Surface,group.Id,true);attached[group.Id]=token;
                    token.guide=Glyph("Period prompt",token.rectTransform,group.Id,false);
                    token.guide.ring=true;
                }
                if(token.wasSpoken!=group.Spoken){token.born=Time.unscaledTime;token.opacity=group.Spoken?1:CognitionBoard.ConflictAlpha;}
                token.wasSpoken=group.Spoken;
                token.transform.SetAsLastSibling();
                Place(token);token.gameObject.SetActive(!returning.Contains(group.Id));
            }
            foreach(int id in attached.Keys.Where(id=>!active.Contains(id)).ToArray()){
                if(attached[id]){attached[id].gameObject.SetActive(false);Destroy(attached[id].gameObject);}
                attached.Remove(id);
            }
        }
        void Place(PeriodGlyph token){
            token.transform.position=board.PeriodEnd(token.group);
            token.rectTransform.localScale=Vector3.one*board.PeriodScale(token.group);
            token.canvasRenderer.SetAlpha(ghost && token.group==source?0:token.opacity*board.PeriodVisibility(token.group));
        }
        internal void DetachForFade(int id,RectTransform parent){
            if(!attached.Remove(id,out var token) || !token)return;
            token.raycastTarget=false;
            token.guide.gameObject.SetActive(false);
            token.transform.SetParent(parent,true);
        }
        public bool Interactable(int group)=>board.Panel.IsOpen&&!board.Panel.InputBlocked&&board.State.Find(group)?.ExecutionLocked==false&&board.PeriodVisibility(group)>.99f;
        public void Click(int group){
            if(!Interactable(group)||board.IsDragging)return;
            board.State.Speak(group);
        }
        internal void RightClick(int group,PointerEventData e){
            if(board.Selection.RemoveSelectedAt(e))return;
            if(!Interactable(group)||returning.Contains(group)||!attached.TryGetValue(group,out var token)||!token)return;
            e.Use();
            board.BeginRemovalEffect(token,token.canvasRenderer.GetAlpha());
            returning.Add(group);token.gameObject.SetActive(false);
            board.PauseSentence(group);StartCoroutine(ReturnAfterRemoval(group));
        }
        IEnumerator ReturnAfterRemoval(int group){
            yield return new WaitForSecondsRealtime(CognitionBoard.RemovalDuration);
            Return(group);
        }
        public void Begin(int group,Vector2 screen){
            if(!Interactable(group)||board.IsDragging||board.State.Find(group)?.Spoken!=true)return;
            source=group;ghost=Glyph("Dragging period",layer,-1,false);ghost.gameObject.SetActive(true);
            ghost.canvasRenderer.SetAlpha(1);ghost.rectTransform.localScale=Vector3.one*board.PeriodScale(group);
            // Keep the event target active so Unity can deliver pointer-up/end-drag.
            layer.SetAsLastSibling();attached[group].canvasRenderer.SetAlpha(0);Move(screen);
        }
        public void Move(Vector2 screen){if(ghost)ghost.transform.position=screen;}
        public void End(Vector2 screen){
            if(!ghost)return;
            int id=source;var dot=ghost;ghost=null;source=0;
            var rect=board.GroupRect(id);
            if(rect && RectTransformUtility.RectangleContainsScreenPoint(rect,screen)){
                Destroy(dot.gameObject);Refresh();return;
            }
            if(!board.State.Unsay(id)){Destroy(dot.gameObject);Refresh();return;}
            returning.Add(id);StartCoroutine(FadeAndReturn(dot,id));
        }
        IEnumerator FadeAndReturn(PeriodGlyph dot,int id){
            for(float time=0;time<Style.returnSeconds;time+=Time.unscaledDeltaTime){
                if(!dot)break;dot.canvasRenderer.SetAlpha(1-time/Style.returnSeconds);yield return null;
            }
            if(dot)Destroy(dot.gameObject);Return(id);
        }
        void Return(int id){
            returning.Remove(id);
            if(attached.TryGetValue(id,out var token)&&token){token.born=Time.unscaledTime;token.opacity=CognitionBoard.ConflictAlpha;token.gameObject.SetActive(true);Place(token);}
        }
        public void Cancel(){
            if(ghost){Destroy(ghost.gameObject);ghost=null;}
            if(attached.TryGetValue(source,out var token)&&token)token.gameObject.SetActive(true);
            source=0;
        }
        public void Remove(){
            Cancel();StopAllCoroutines();
            foreach(var glyph in board.Surface.GetComponentsInChildren<PeriodGlyph>(true))
                if(glyph.owner==this){glyph.gameObject.SetActive(false);Destroy(glyph.gameObject);}
            foreach(var glyph in layer.GetComponentsInChildren<PeriodGlyph>(true))
                if(glyph.owner==this){glyph.gameObject.SetActive(false);Destroy(glyph.gameObject);}
            enabled=false;
        }
        void LateUpdate(){
            if(!board)return;
            float dt=Time.unscaledDeltaTime;
            foreach(var pair in attached){
                var token=pair.Value;var group=board.State.Find(pair.Key);
                if(!token||group==null)continue;
                float breath=.5f-.5f*Mathf.Cos(2*Mathf.PI*(Time.unscaledTime-token.born)/Style.breathSeconds);
                bool prompt=board.Panel.IsOpen && !returning.Contains(group.Id) && board.State.CanPrompt(group);
                bool waiting=group.AwaitingRetry && !prompt;
                float alpha=waiting ? CognitionBoard.ConflictAlpha : group.Spoken||token.hovered?1:Mathf.Lerp(CognitionBoard.ConflictAlpha,1,breath);
                token.opacity=group.Spoken?1:Mathf.MoveTowards(token.opacity,alpha,dt/Style.hoverSeconds);
                token.raycastTarget=Interactable(pair.Key)&&(!ghost||source!=pair.Key);Place(token);
                var guide=token.guide;
                if (guide.gameObject.activeSelf!=prompt)
                {
                    guide.gameObject.SetActive(prompt);
                    if (prompt) guide.born=Time.unscaledTime;
                }
                if (!prompt) continue;
                float phase=Mathf.Repeat(Time.unscaledTime-guide.born,Style.rippleSeconds*1.6f);
                float u=Mathf.Clamp01(phase/Style.rippleSeconds);
                guide.rectTransform.localScale=Vector3.one*(Style.rippleScale+u*Style.rippleGrowth);
                guide.canvasRenderer.SetAlpha((1-u)*Style.rippleOpacity*board.PeriodVisibility(group.Id));
            }
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PeriodGlyph : MaskableGraphic, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PeriodTray owner;
        public int group;
        public float diameter,born,opacity;
        internal PeriodGlyph guide;
        public bool ring,hovered,wasSpoken;
        bool pressed,dragged;
        public void OnPointerEnter(PointerEventData e){hovered=true;}
        public void OnPointerExit(PointerEventData e){hovered=false;}
        public void OnPointerDown(PointerEventData e){
            pressed=dragged=false;
            if(e.button==PointerEventData.InputButton.Right)owner.RightClick(group,e);
            else if(e.button==PointerEventData.InputButton.Left && owner.Interactable(group))pressed=true;
        }
        public void OnPointerUp(PointerEventData e){
            if(e.button==PointerEventData.InputButton.Left && pressed && !dragged && RectTransformUtility.RectangleContainsScreenPoint(rectTransform,e.position))owner.Click(group);
            pressed=false;
        }
        public void OnInitializePotentialDrag(PointerEventData e){e.useDragThreshold=true;}
        public void OnBeginDrag(PointerEventData e){if(pressed && e.button==PointerEventData.InputButton.Left){dragged=true;owner.Begin(group,e.position);}}
        public void OnDrag(PointerEventData e){if(dragged)owner.Move(e.position);}
        public void OnEndDrag(PointerEventData e){if(dragged)owner.End(e.position);pressed=dragged=false;}
        protected override void OnPopulateMesh(VertexHelper mesh){
            mesh.Clear();const int segments=48;float radius=diameter*.5f;
            if(ring){
                for(int i=0;i<=segments;i++){
                    float angle=i*2*Mathf.PI/segments;
                    var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    mesh.AddVert(dir*radius,color,Vector2.zero);
                    mesh.AddVert(dir*(radius-.8f),color,Vector2.zero);
                    if(i==0)continue;
                    int n=i*2;
                    mesh.AddTriangle(n-2,n-1,n);mesh.AddTriangle(n,n-1,n+1);
                }
                return;
            }
            Color clear=color;clear.a=0;
            for(int i=0;i<=segments;i++){
                float angle=i*2*Mathf.PI/segments;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                mesh.AddVert(direction*(radius+.5f),clear,Vector2.zero);
                mesh.AddVert(direction*(radius-.5f),color,Vector2.zero);
                mesh.AddVert(Vector2.zero,color,Vector2.zero);
                if(i==0)continue;int n=i*3;
                mesh.AddTriangle(n-3,n-2,n);mesh.AddTriangle(n,n-2,n+1);
                mesh.AddTriangle(n-2,n-1,n+1);mesh.AddTriangle(n+1,n-1,n+2);
            }
        }
    }
}
