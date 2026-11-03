using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V4
{
    public sealed class DoorPassage : MonoBehaviour
    {
        public string destinationScene="ChapterI";
        public OpeningFlow flow;
        public SentenceEffectManager effects;
        public float halfOpeningWidth=2.1f, openingHeight=5.8f;
        public bool Entered { get; private set; }
        DoorStateEffects state;
        FirstPersonController tracked;
        float previousSide;
        bool crossing;
        Quaternion crossingRotation;
        void Awake() => state=GetComponent<DoorStateEffects>();
        void OnTriggerEnter(Collider other)
        {
            var player=other.GetComponentInParent<FirstPersonController>();
            if(!player) return;
            tracked=player;crossingRotation=transform.rotation;
            previousSide=(Quaternion.Inverse(crossingRotation)*(player.transform.position-transform.position)).z;
            crossing=state && state.PassageEnabled;
        }
        void OnTriggerStay(Collider other)
        {
            var player=other.GetComponentInParent<FirstPersonController>();
            if(!player || player!=tracked || Entered) return;
            // Keep the entry plane stable while the frame turns to face the approaching player.
            var p=Quaternion.Inverse(crossingRotation)*(player.transform.position-transform.position);
            bool open=state && state.PassageEnabled;
            if(open && crossing && previousSide*p.z<0 && Mathf.Abs(p.x)<halfOpeningWidth && p.y>=-.2f && p.y<openingHeight)
                StartCoroutine(EnterTransitionRoom(player));
            if(Mathf.Abs(p.z)>.005f) previousSide=p.z;
            crossing=open;
        }
        void OnTriggerExit(Collider other)
        {
            if(other.GetComponentInParent<FirstPersonController>()!=tracked) return;
            tracked=null; crossing=false;
        }
        IEnumerator EnterTransitionRoom(FirstPersonController player)
        {
            var manager=FindFirstObjectByType<SessionSceneManager>();
            if(!manager || !manager.Request(destinationScene))yield break;
            Entered=true;
            while(manager.Busy)yield return null;
            if(manager.LastError!=null)Entered=false;
        }
    }
}
