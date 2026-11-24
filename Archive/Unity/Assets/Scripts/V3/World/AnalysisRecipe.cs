using PuzzleApple.V3.Cognition;
using UnityEngine;

namespace PuzzleApple.V3
{
    [CreateAssetMenu(menuName="PuzzleApple/V3/Analysis recipe")]
    public sealed class AnalysisRecipe : ScriptableObject
    {
        public enum AppleForm { Any, Whole, Core }
        public WordDefinition input;
        public AppleForm requiredAppleForm;
        public WordDefinition[] outputs=new WordDefinition[0];
        [Tooltip("Optional visual-only result. These symbols are not added to the vocabulary.")]
        public Sprite[] previewSymbols=new Sprite[0];
        public int SymbolCount=>previewSymbols.Length>0?previewSymbols.Length:outputs.Length;
        public Sprite SymbolAt(int index)=>previewSymbols.Length>0?previewSymbols[index]:outputs[index].Symbol;
        public bool Matches(OpeningObject obj)
        {
            if(!input||input.Id!=obj.wordId)return false;
            var state=obj.GetComponent<AppleState>();bool core=state&&state.IsCore;
            return requiredAppleForm==AppleForm.Any||core==(requiredAppleForm==AppleForm.Core);
        }
    }
}
