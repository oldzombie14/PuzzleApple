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
            Require(catalog.Words.Count==7 && catalog.Word("ground").Symbol, "Seven words include Ground symbol");
            Require(catalog.Rules.Where(r=>r.Words.Any(w=>w.Id=="world")).All(r=>r.Words.Count==2 && (r.Words[0].Id=="positive"||r.Words[0].Id=="negative")), "World is only valid with positive/negative");
            foreach(var sentence in new[]{"world move","light world move","door world move","world light move","world door move","positive ground","negative ground","door door move","ground ground move","door move ground","light positive","positive negative light","negative negative light","light move move","light door"})
            {
                var invalid=new CognitionState(catalog); invalid.TryAcquireSentence("test",sentence);
                Require(!invalid.Groups.Single().Recognized,"Unexpected valid sentence: "+sentence);
            }
            Require(catalog.Rules.Count==16,"Only six state sentences and ten motion sentences remain");
            foreach(var rule in catalog.Rules.Where(r=>r.Words.Last().Id=="move"))
                foreach(var modifier in new[]{"positive","negative"})
                {
                    var invalid=new CognitionState(catalog);
                    invalid.TryAcquireSentence("test",modifier+" "+string.Join(" ",rule.Words.Select(w=>w.Id)));
                    Require(!invalid.Groups.Single().Recognized,"State-modified motion must be invalid");
                }
            var s=new CognitionState(catalog);
            s.TryAcquireSentence("first","positive light");s.TryAcquireSentence("second","light ground move");
            Require(s.Groups.All(g=>g.Effective)&&s.HasEffect(CognitionSignal.LightOn)&&s.HasEffect(CognitionSignal.LightWorldMove),"Separate state and directed motion coexist");
            s.TryAcquireSentence("third","negative light");Require(s.Groups.Last().Conflicted,"Opposite state remains suppressed");
            s.ReturnToLibrary(s.Groups[0].Id,0,true);
            Require(s.HasEffect(CognitionSignal.LightOff)&&s.HasEffect(CognitionSignal.LightWorldMove),"Removing state winner leaves movement intact");
            s.ClearWorkspace(); s.TryAcquireSentence("fourth","light move"); s.TryAcquireSentence("fifth","light move");
            Require(s.Groups.All(g=>g.Effective),"Identical motion claims should coexist");
            s.TryAcquireSentence("sixth","light ground move"); Require(s.Groups.Last().Conflicted,"Motion priority missing");
            s.ReturnToLibrary(s.Groups[0].Id,0,true); Require(s.HasEffect(CognitionSignal.LightMove),"Duplicate removal prematurely disables effect");
            s.ClearWorkspace(); s.TryAcquireSentence("seventh","positive world"); s.TryAcquireSentence("eighth","negative world");
            Require(s.HasEffect(CognitionSignal.WorldOff),"World on must not suppress exit");
            return "PASS: 16 sentences, 20 rejected modified motions, independent state/motion, conflict chronology, duplicate sharing and exit";
        }
    }
}
