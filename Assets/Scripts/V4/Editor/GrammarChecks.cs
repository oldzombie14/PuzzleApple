using System;
using System.Linq;
using UnityEditor;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4.Editor
{
    public static class GrammarChecks
    {
        static void Require(bool valid,string message) { if(!valid) throw new InvalidOperationException(message); }
        public static string Run()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V4/Opening/OpeningCatalog.asset");
            catalog.ValidateOrThrow();
            foreach(var rule in catalog.Rules)
            {
                var isolated=new CognitionState(catalog);
                isolated.TryAcquireSentence("test",string.Join(" ",rule.Words.Select(w=>w.Id)));
                Require(isolated.Groups.Single().Effective,"Registered rule unrecognized: "+rule.Id);
                foreach(var signal in rule.Effects) Require(isolated.HasEffect(signal),"Missing composite signal: "+rule.Id);
            }
            foreach(var sentence in new[]{"door door move","world world move","door move world","light positive","positive negative light","negative negative light","light move move","light door"})
            {
                var invalid=new CognitionState(catalog); invalid.TryAcquireSentence("test",sentence);
                Require(!invalid.Groups.Single().Recognized,"Unexpected valid sentence: "+sentence);
            }
            var s=new CognitionState(catalog);
            s.TryAcquireSentence("first","positive light"); s.TryAcquireSentence("second","negative light move");
            Require(s.Groups[1].Recognized && s.Groups[1].Conflicted,"Composite must remain recognized but faded");
            Require(s.HasEffect(CognitionSignal.LightOn) && !s.HasEffect(CognitionSignal.LightOff) && !s.HasEffect(CognitionSignal.LightMove),"Composite partially applied");
            s.TryAcquireSentence("third","light door move");
            Require(s.HasEffect(CognitionSignal.LightDoorMove),"Suppressed composite incorrectly reserves motion");
            s.ReturnToLibrary(s.Groups[0].Id,0,true);
            Require(s.HasEffect(CognitionSignal.LightOff) && s.HasEffect(CognitionSignal.LightMove) && !s.HasEffect(CognitionSignal.LightDoorMove),"Oldest composite did not take over atomically");
            s.ClearWorkspace(); s.TryAcquireSentence("fourth","light move"); s.TryAcquireSentence("fifth","positive light move");
            Require(s.Groups.All(g=>g.Effective),"Identical motion claims should coexist");
            s.TryAcquireSentence("sixth","light world move"); Require(s.Groups.Last().Conflicted,"Motion priority missing");
            s.ReturnToLibrary(s.Groups[0].Id,0,true); Require(s.HasEffect(CognitionSignal.LightMove),"Duplicate removal prematurely disables effect");
            s.ClearWorkspace(); s.TryAcquireSentence("seventh","positive world"); s.TryAcquireSentence("eighth","negative world move");
            Require(s.HasEffect(CognitionSignal.WorldOff),"World on must not suppress exit");
            return "PASS: "+catalog.Rules.Count+" registered sentences, strict invalid cases, atomic conflict/chronology, duplicate sharing and exit semantics";
        }
    }
}
