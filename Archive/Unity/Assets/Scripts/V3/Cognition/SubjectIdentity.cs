using System.Collections.Generic;
using System.Linq;

namespace PuzzleApple.V3.Cognition
{
    // Identity expands who a noun denotes; each action still decides what its recipients can do.
    public static class SubjectIdentity
    {
        public static bool Matches(string nativeName,string noun,IEnumerable<CognitionState.Group> groups)
        {
            if(nativeName==noun)return true;
            var names=new HashSet<string>{nativeName};
            var identities=groups.Where(g=>g.Effective&&(g.Signal==CognitionSignal.EqualApple||g.Signal==CognitionSignal.EqualBalance)&&g.Words.Count==3).ToArray();
            bool changed;
            do
            {
                changed=false;
                foreach(var g in identities)
                    if(names.Contains(g.Words[0].Meaning))changed|=names.Add(g.Words[2].Meaning);
            }while(changed);
            return names.Contains(noun);
        }
    }
}
