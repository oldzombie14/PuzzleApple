using System;
using System.Linq;
using PuzzleApple.Cognition;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.Editor
{
    // Editor-only verification of authored cognition and acquisition, with no runtime rule registration.
    public static class CognitionStateChecks
    {
        [MenuItem("PuzzleApple/Cognition/Check state transitions")]
        public static void Run()
        {
            int checks=0;
            void Require(bool value,string message){if(!value)throw new Exception(message);checks++;}
            var catalog=CognitionCatalogEditor.LoadTutorial();
            var state=new CognitionState(catalog);
            Require(state.TryAcquireWord("mirror",catalog.Word("i")),"Acquire I from source");
            Require(!state.TryAcquireWord("mirror",catalog.Word("i")),"Source grants once");
            Require(state.TryAcquireSentence("apple","I no consume apple."),"Acquire apple sentence");
            var negative=state.Groups.Last();
            Require(!negative.Recognized&&!state.CanConsumeApple,"Negative sentence is unsupported");
            Require(!state.TryAcquireSentence("apple","I no consume apple"),"Sentence grants once");
            int noId=negative.Words[1].Id;
            var no=state.Detach(negative.Id,noId);
            var tail=state.Groups.Single(g=>g.Words.Count==2);
            Require(no.Independent&&negative.Independent&&tail.Words[0].Meaning=="consume","Middle removal preserves independent left and right runs");
            state.Join(negative.Id,tail.Id,true);
            Require(tail.Recognized&&state.CanConsumeApple,"Rejoin I consume apple");
            state.Swap(tail.Id,tail.Words[0].Id,tail.Words[1].Id);
            Require(!state.CanConsumeApple,"Wrong word order disables action");
            state.Swap(tail.Id,tail.Words[0].Id,tail.Words[1].Id);
            Require(state.CanConsumeApple,"Restoring word order restores action");
            var apple=state.Detach(tail.Id,tail.Words[2].Id);
            state.TryAcquireWord("walking",catalog.Word("move"));var move=state.Groups.Last();
            state.Join(move.Id,apple.Id,false);
            Require(state.AppleMoving&&!state.CanConsumeApple,"Reusing apple for movement withdraws consume");
            state.TryAcquireSentence("another.apple-move","apple move");var another=state.Groups.Last();
            state.Detach(apple.Id,apple.Words[1].Id);
            Require(state.AppleMoving,"Second supporting sentence preserves movement");
            state.Detach(another.Id,another.Words[1].Id);
            Require(!state.AppleMoving,"Last supporting sentence removed");
            state.TryAcquireWord("key",catalog.Word("open"));var open=state.Groups.Last();
            var self=state.Groups.First(g=>g.Independent&&g.Words[0].Meaning=="i");state.Join(open.Id,self.Id,false);
            Require(!self.Recognized&&!state.CanOpen,"I open alone is no longer a valid opening rule");
            state.TryAcquireWord("door",catalog.Word("door"));var door=state.Groups.Last();state.Join(door.Id,self.Id,false);
            Require(state.CanOpen,"I open door enables door action");
            state.Detach(self.Id,self.Words[1].Id);
            Require(!state.CanOpen&&state.HasAcquired("key"),"Breaking sentence preserves word acquisition");
            var walking=state.Groups.First(g=>g.Independent&&g.Words[0].Meaning=="move");
            state.Join(walking.Id,self.Id,false);
            Require(self.Recognized&&state.PlayerMoving,"I move is valid and enables player movement");
            state.TryAcquireSentence("second.walking","I move");var otherWalk=state.Groups.Last();
            state.Detach(self.Id,self.Words[1].Id);
            Require(state.PlayerMoving,"another I move sustains forced walking");
            state.Detach(otherWalk.Id,otherWalk.Words[1].Id);
            Require(!state.PlayerMoving,"breaking last I move disables forced walking");
            state.TryAcquireSentence("blocked","I no move.");var blocked=state.Groups.Last();
            Require(blocked.Recognized&&state.PlayerMovementBlocked,"I no move is valid");
            state.TryAcquireSentence("blocked/second","I no move");var otherBlock=state.Groups.Last();
            state.TryAcquireSentence("walking/conflict","I move");
            var positive=state.Groups.Last();
            Require(!state.PlayerMoving&&!state.PlayerMovementBlocked&&positive.Conflicted&&blocked.Conflicted&&otherBlock.Conflicted,
                "all opposing duplicates are conflicted and ineffective");
            state.TryAcquireSentence("conflict/unrelated","apple move");
            Require(state.AppleMoving&&!state.Groups.Last().Conflicted,"unrelated cognition remains effective");
            state.TryAcquireSentence("conflict/positive2","I move");var positive2=state.Groups.Last();
            Require(positive2.Conflicted&&!positive2.Effective,"duplicate positive also conflicts");
            state.Detach(positive2.Id,positive2.Words[1].Id);
            Require(positive.Conflicted&&blocked.Conflicted,"one positive still sustains conflict");
            state.Detach(blocked.Id,blocked.Words[1].Id);
            Require(!state.PlayerMovementBlocked&&otherBlock.Conflicted&&positive.Conflicted,"remaining negative sustains conflict");
            state.Swap(otherBlock.Id,otherBlock.Words[0].Id,otherBlock.Words[1].Id);
            Require(!otherBlock.Recognized&&!state.PlayerMovementBlocked&&state.PlayerMoving,"invalidating last prohibition preserves I move");
            Require(!positive.Conflicted&&positive.Effective,"resolved sentence clears conflict state");
            state.Swap(otherBlock.Id,otherBlock.Words[0].Id,otherBlock.Words[1].Id);
            state.Detach(positive.Id,positive.Words[1].Id);
            Require(otherBlock.Effective&&state.PlayerMovementBlocked&&!state.PlayerMoving,"removing positive restores prohibition");
            state.TryAcquireSentence("unsupported","I apple open");
            Require(!state.Groups.Last().Recognized,"Retired prototype rule is absent");
            Require(state.Groups.SelectMany(g=>g.Words).Select(w=>w.Id).Distinct().Count()==state.Groups.Sum(g=>g.Words.Count),"Every token has exactly one owner");
            Require(new CognitionState(catalog).Groups.Count==0,"shared catalog never shares runtime groups");
            Debug.Log("PASS: "+checks+" production cognition state assertions");
        }
    }
}
