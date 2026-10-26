using UnityEngine;
using System.Linq;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4
{
    [DefaultExecutionOrder(310)]
    public sealed class SentenceEffectManager : MonoBehaviour
    {
        public CognitionBoard board;
        public LightSentenceEffects lights;
        public DoorSentenceEffects door;
        public WorldSentenceEffects world;
        public IntroPresentation presentation;
        public SkyInscription inscriptions;
        public bool Suspended { get; private set; }
        CognitionChange currentChange;
        readonly System.Collections.Generic.HashSet<int> exitGroups=new System.Collections.Generic.HashSet<int>();
        void Start()
        {
            board.State.Changed+=Apply;
            Apply(CognitionChange.Add);
        }
        public void Suspend()
        {
            Suspended=true;
            if(presentation)presentation.Cancel();
            // An empty snapshot stops ongoing effects at their current values without discarding learned words.
            var empty=new CognitionState(board.Catalog);
            lights.Apply(empty); door.Apply(empty); world.Apply(empty);
        }
        void Apply(CognitionChange change)
        {
            currentChange=change;
            var currentExitGroups=board.State.Groups.Where(g=>g.Effective&&g.Signals.Contains(CognitionSignal.WorldOff)).Select(g=>g.Id).ToArray();
            bool requestExit=currentExitGroups.Any(id=>!exitGroups.Contains(id));
            exitGroups.Clear();foreach(int id in currentExitGroups)exitGroups.Add(id);
            if(requestExit)
            {
                var entry=FindFirstObjectByType<WorldEntry>();
                if(entry&&entry.Exiting)return;
                if(presentation&&presentation.feedback)
                    foreach(var group in board.State.Groups)
                        if(group.Effective&&System.Array.IndexOf(group.Signals.ToArray(),CognitionSignal.WorldOff)>=0)presentation.feedback.Play(group.Id,false);
                WorldEntry.QuitApplication();return;
            }
            if(Suspended) return;
            if(presentation) presentation.Apply(change,ApplyWorld);
            else ApplyWorld();
        }
        void ApplyWorld()
        {
            lights.Apply(board.State);door.Apply(board.State,currentChange==CognitionChange.Split);world.Apply(board.State);
            if(inscriptions)inscriptions.RecordApplied(board.State);
        }
        public void DoorRevealed() { if(!Suspended && door) door.RefreshMotion(); }
        void OnDestroy() { if(board && board.State!=null) board.State.Changed-=Apply; }
    }
}
