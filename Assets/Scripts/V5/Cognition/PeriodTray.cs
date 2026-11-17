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
        [Min(8)] public float hitSize=32;
        [Min(2)] public float diameter=11,spacing=16,snapRadius=36;
        public Vector2 cornerInset=new Vector2(32,34);
        [Min(.1f)] public float hintSeconds=.6f,guideCycle=2.2f;
        [Min(.01f)] public float guidePulseSeconds=.4f,guideFlightSeconds=.9f,guideHoldSeconds=.3f;
        [Min(0)] public float trailDelay=.027f;
        [Range(0,1)] public float restingOpacity=.86f,snapOpacity=.46f;
        [Range(0,1)] public float guideOpacity=.35f;
        [Min(.01f)] public float returnSeconds=.22f,landSeconds=.15f;
        [Min(1)] public float cornerScale=1.3f,landingScale=1.3f;
        [Min(0)] public float hintScale=.1f,guideScale=.25f,guideArc=18,rippleScale=2,rippleGrowth=1.5f;
        [Range(0,1)] public float rippleOpacity=.27f,dragSourceOpacity=.15f;
    }

    public sealed class PeriodTray : MonoBehaviour
    {
        CognitionBoard board;
        RectTransform layer;
        PeriodGlyph corner,ghost,hint,ring;
        PeriodGlyph[] trail;
        readonly Dictionary<int,PeriodGlyph> attached=new Dictionary<int,PeriodGlyph>();
        HashSet<int> drafts=new HashSet<int>();
        Color chalk;
        int source,hover;
        float pulse,guide;
        PeriodStyle Style=>board.punctuation;
        public bool Dragging=>ghost;
        public void Initialize(CognitionBoard owner,RectTransform overlay,Color ink)
        {
            board=owner;layer=overlay;chalk=ink;
            corner=Glyph("Period supply",board.Surface,0,true);
            var rect=corner.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(1,0);
            rect.anchoredPosition=new Vector2(-Style.cornerInset.x,Style.cornerInset.y);
            hint=Glyph("Period snap",board.Surface,-1,false);
            hint.canvasRenderer.SetAlpha(Style.snapOpacity);
            ring=Glyph("Period ripple",board.Surface,-1,false);ring.ring=true;
            trail=new PeriodGlyph[8];
            for(int i=0;i<trail.Length;i++)trail[i]=Glyph("Period guide",board.Surface,-1,false);
        }
        PeriodGlyph Glyph(string name,RectTransform parent,int id,bool touch)
        {
            var t=new GameObject(name,typeof(RectTransform),typeof(PeriodGlyph)).GetComponent<PeriodGlyph>();
            t.rectTransform.SetParent(parent,false);t.rectTransform.sizeDelta=Vector2.one*Style.hitSize;
            t.diameter=Style.diameter;t.owner=this;t.group=id;t.color=chalk;t.raycastTarget=touch;
            t.gameObject.SetActive(touch);return t;
        }
        public void Refresh()
        {
            var active=new HashSet<int>();
            var next=new HashSet<int>();
            foreach(var g in board.State.Groups){
                if(!g.Spoken){if(g.Recognized)next.Add(g.Id);continue;}
                active.Add(g.Id);
                if(!attached.TryGetValue(g.Id,out var token)||!token){
                    token=Glyph("Sentence period",board.GroupRect(g.Id),g.Id,true);
                    attached[g.Id]=token;token.born=Time.unscaledTime;
                }
                token.rectTransform.SetParent(board.GroupRect(g.Id),false);
                token.transform.position=board.PeriodEnd(g.Id);
            }
            foreach(int id in attached.Keys.Where(id=>!active.Contains(id)).ToArray()){
                if(attached[id]){
                    if(board.Panel.IsOpen)StartCoroutine(FlyHome(attached[id].transform.position));
                    Destroy(attached[id].gameObject);
                }
                attached.Remove(id);
            }
            if(next.Except(drafts).Any())pulse=Style.hintSeconds;
            drafts=next;
            corner.transform.SetAsLastSibling();
        }
        System.Collections.IEnumerator FlyHome(Vector3 from)
        {
            var dot=Glyph("Returning period",layer,-1,false);dot.gameObject.SetActive(true);
            for(float t=0;t<1 && board.Panel.IsOpen;t+=Time.unscaledDeltaTime/Style.returnSeconds)
            {dot.transform.position=Vector3.Lerp(from,corner.transform.position,Mathf.SmoothStep(0,1,t));yield return null;}
            Destroy(dot.gameObject);
        }
        public void Begin(int group,Vector2 screen)
        {
            if(!board.Panel.IsOpen||board.Panel.InputBlocked||board.IsDragging)return;
            source=group;ghost=Glyph("Dragging period",layer,-1,false);ghost.gameObject.SetActive(true);
            ghost.rectTransform.localScale=Vector3.one*(group==0?Style.cornerScale:1);
            layer.SetAsLastSibling();
            if(source>0 && attached.TryGetValue(source,out var token))token.canvasRenderer.SetAlpha(Style.dragSourceOpacity);
            Move(screen);
        }
        public void Move(Vector2 screen)
        {
            if(!ghost)return;
            ghost.transform.position=screen;hover=0;
            float best=Style.snapRadius*board.Surface.lossyScale.x;
            if(RectTransformUtility.RectangleContainsScreenPoint(board.Surface,screen))
                foreach(var group in board.State.Groups){
                    if(group.Independent || group.Spoken && group.Id!=source)continue;
                    float d=Vector2.Distance(screen,board.PeriodEnd(group.Id));
                    if(d>=best)continue;best=d;hover=group.Id;
                }
            hint.gameObject.SetActive(hover!=0 && board.State.Find(hover).Recognized);
            if(hint.gameObject.activeSelf){hint.transform.position=board.PeriodEnd(hover);hint.canvasRenderer.SetAlpha(Style.snapOpacity);}
        }
        public void End(Vector2 screen)
        {
            if(!ghost)return;
            Move(screen);int from=source,to=hover;Cancel();
            if(from==to)return;
            if(from>0){board.State.Unsay(from);return;}
            if(to>0 && board.State.Speak(to)){board.LearnedPeriod=true;guide=0;}
        }
        public void Cancel()
        {
            if(ghost){Destroy(ghost.gameObject);ghost=null;}
            if(source>0 && attached.TryGetValue(source,out var token)&&token)token.canvasRenderer.SetAlpha(1);
            source=hover=0;if(hint)hint.gameObject.SetActive(false);
        }
        public void Remove()
        {
            Cancel();
            foreach(var glyph in board.Surface.GetComponentsInChildren<PeriodGlyph>(true))
                if(glyph.owner==this){glyph.gameObject.SetActive(false);Destroy(glyph.gameObject);}
            foreach(var glyph in layer.GetComponentsInChildren<PeriodGlyph>(true))
                if(glyph.owner==this){glyph.gameObject.SetActive(false);Destroy(glyph.gameObject);}
            enabled=false;
        }
        void LateUpdate()
        {
            if(!board || !board.Panel.IsOpen)return;
            float dt=Time.unscaledDeltaTime;
            pulse=Mathf.Max(0,pulse-dt);
            bool show=!board.LearnedPeriod && drafts.Count>0 && !ghost;
            guide=show?Mathf.Repeat(guide+dt,Style.guideCycle):0;
            float beat=Mathf.Max(Mathf.Sin(Mathf.PI*pulse/Style.hintSeconds),show && guide<Style.guidePulseSeconds?Mathf.Sin(Mathf.PI*guide/Style.guidePulseSeconds):0);
            corner.canvasRenderer.SetAlpha(Mathf.Lerp(Style.restingOpacity,1,beat));
            corner.rectTransform.localScale=Vector3.one*Style.cornerScale*(1+beat*(show?Style.guideScale:Style.hintScale));
            ring.gameObject.SetActive(beat>0);
            if(beat>0){ring.transform.position=corner.transform.position;ring.rectTransform.localScale=Vector3.one*(Style.rippleScale+beat*Style.rippleGrowth);ring.canvasRenderer.SetAlpha(beat*Style.rippleOpacity);}
            foreach(var token in attached.Values)if(token){
                float u=Mathf.Clamp01((Time.unscaledTime-token.born)/Style.landSeconds);
                token.rectTransform.localScale=Vector3.one*Mathf.Lerp(Style.landingScale,1,u);
            }
            var end=show?board.PeriodEnd(drafts.Min()):Vector3.zero;
            float land=Style.guidePulseSeconds+Style.guideFlightSeconds;
            float fade=Mathf.Clamp01(1-(guide-land)/Style.guideHoldSeconds);
            if(!ghost){
                hint.gameObject.SetActive(show && guide>=land && fade>0);
                if(hint.gameObject.activeSelf){hint.transform.position=end;hint.canvasRenderer.SetAlpha(Style.snapOpacity*fade);}
            }
            for(int i=0;i<trail.Length;i++){
                float t=(guide-Style.guidePulseSeconds-i*Style.trailDelay)/Style.guideFlightSeconds;
                bool visible=show && t>=0 && fade>0;
                var dot=trail[i];dot.gameObject.SetActive(visible);
                if(!visible)continue;
                float u=Mathf.SmoothStep(0,1,t);
                dot.transform.position=Vector3.Lerp(corner.transform.position,end,u)+Vector3.up*(Style.guideArc*board.Surface.lossyScale.x*Mathf.Sin(Mathf.PI*u));
                dot.canvasRenderer.SetAlpha(Style.guideOpacity*(1-i/(float)trail.Length)*fade);
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PeriodGlyph : MaskableGraphic, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PeriodTray owner;
        public int group;
        public float diameter,born;
        public bool ring;
        public void OnBeginDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)owner.Begin(group,e.position);}
        public void OnDrag(PointerEventData e){owner.Move(e.position);}
        public void OnEndDrag(PointerEventData e){owner.End(e.position);}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for(int i=0;i<=32;i++){
                float a=i*Mathf.PI/16;
                var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                float r=diameter*.5f*(1+.06f*Mathf.Sin(3*a)+.035f*Mathf.Cos(7*a));
                mesh.AddVert(dir*r,color,Vector2.zero);
                mesh.AddVert(dir*(ring?r*.91f:0),color,Vector2.zero);
                if(i==0)continue;
                int n=i*2;
                mesh.AddTriangle(n-2,n-1,n);mesh.AddTriangle(n,n-1,n+1);
            }
        }
    }
}
