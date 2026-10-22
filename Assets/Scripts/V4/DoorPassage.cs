using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V4
{
    public sealed class DoorPassage : MonoBehaviour
    {
        public string destinationScene="V4_NextRoom";
        public OpeningFlow flow;
        public SentenceEffectManager effects;
        public float halfOpeningWidth=2.1f, openingHeight=5.8f;
        public bool Entered { get; private set; }
        DoorStateEffects state;
        FirstPersonController tracked;
        float previousSide;
        bool crossing;
        void Awake() => state=GetComponent<DoorStateEffects>();
        void OnTriggerEnter(Collider other)
        {
            var player=other.GetComponentInParent<FirstPersonController>();
            if(!player) return;
            tracked=player; previousSide=transform.InverseTransformPoint(player.transform.position).z;
            crossing=state && state.PassageEnabled;
        }
        void OnTriggerStay(Collider other)
        {
            var player=other.GetComponentInParent<FirstPersonController>();
            if(!player || player!=tracked || Entered) return;
            var p=transform.InverseTransformPoint(player.transform.position);
            bool open=state && state.PassageEnabled;
            if(open && crossing && previousSide*p.z<0 && Mathf.Abs(p.x)<halfOpeningWidth && p.y>=-.2f && p.y<openingHeight)
                StartCoroutine(EnterNextRoom(player));
            if(Mathf.Abs(p.z)>.005f) previousSide=p.z;
            crossing=open;
        }
        void OnTriggerExit(Collider other)
        {
            if(other.GetComponentInParent<FirstPersonController>()!=tracked) return;
            tracked=null; crossing=false;
        }
        IEnumerator EnterNextRoom(FirstPersonController player)
        {
            if(!Application.CanStreamedLevelBeLoaded(destinationScene))
            {
                Debug.LogError("Next room is missing from Build Settings: "+destinationScene,this); yield break;
            }
            Entered=true;
            bool locked=player.MovementLocked; player.MovementLocked=true;
            var operation=SceneManager.LoadSceneAsync(destinationScene,LoadSceneMode.Additive);
            if(operation==null) { Entered=false; player.MovementLocked=locked; yield break; }
            yield return operation;
            var spawn=FindFirstObjectByType<NextRoomSpawn>();
            if(!spawn)
            {
                Debug.LogError("Next room has no spawn point.",this);
                Entered=false; player.MovementLocked=locked; yield break;
            }
            if(effects) effects.Suspend();
            if(flow)
            {
                if(flow.lightField) flow.lightField.gameObject.SetActive(false);
                if(flow.reticle) flow.reticle.SetActive(false);
                if(flow.hoverEye) flow.hoverEye.gameObject.SetActive(false);
                flow.enabled=false;
            }
            player.Teleport(spawn.transform.position,spawn.transform.rotation);
            player.MovementLocked=locked;
            SceneManager.SetActiveScene(spawn.gameObject.scene);
            // Preserve the same controller, vocabulary and UI across this placeholder transition.
            gameObject.SetActive(false);
        }
    }
}
