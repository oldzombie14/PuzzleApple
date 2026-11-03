using PuzzleApple.V4.Cognition;
using UnityEngine;

namespace PuzzleApple.V4
{
    // Only the four Intro hint completions are persisted, not the world or vocabulary.
    public sealed class IntroHintProgress
    {
        public const string DefaultKey="PuzzleApple.V4.IntroHints.v1";
        public string Key { get; }
        public int Mask { get; private set; }
        public bool ChecklistComplete=>(Mask&7)==7;
        public bool Finished=>ChecklistComplete&&(Mask&8)!=0;
        public IntroHintProgress(string key){Key=key;Mask=PlayerPrefs.GetInt(Key,0)&15;}
        static int Bit(CognitionSignal signal)
        {
            switch(signal)
            {
                case CognitionSignal.WorldOff:return 1;
                case CognitionSignal.LightMove:return 2;
                case CognitionSignal.LightWorldMove:return 4;
                case CognitionSignal.LightOff:return 8;
                default:return 0;
            }
        }
        public bool Has(CognitionSignal signal)=>(Mask&Bit(signal))!=0;
        public void Record(CognitionSignal signal)
        {
            int next=Mask|Bit(signal);if(next==Mask)return;
            Mask=next;
            PlayerPrefs.SetInt(Key,Mask);
            PlayerPrefs.Save(); // Persist before ON can close the application or stop Play.
        }
        public void Clear(){Mask=0;PlayerPrefs.DeleteKey(Key);PlayerPrefs.Save();}
    }
}
