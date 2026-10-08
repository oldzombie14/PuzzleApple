using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PuzzleApple.V3.Cognition;

namespace PuzzleApple.V3
{
    // Capacity belongs to the machine, not to the sentence's subject selection.
    public sealed class WordAnalyzer : MonoBehaviour
    {
        public OpeningRoom room;
        public Transform intake;
        public AnalysisRecipe[] recipes=new AnalysisRecipe[0];
        public GameObject resultRow;
        public Image[] outputSymbols=new Image[0];
        public Image progress;
        [Min(.1f)] public float analysisSeconds=2;
        [Min(.1f)] public float claimRadius=.55f;
        public OpeningObject Occupant { get; private set; }
        public OpeningObject Reserved { get; private set; }
        public AnalysisRecipe Result { get; private set; }
        public bool HasResult=>Result;
        float elapsed;
        AnalysisRecipe recipe;
        string request;

        public void SetRequest(CognitionState.Group group)
        {
            string key=group==null?null:group.RuleId+":"+group.Established;
            if(key==request)return;
            request=key;Reserved=null;RefreshOccupancy();
            if(group==null)return;
            string noun=group.Words[0].Meaning;
            // Resolve the one place once, before either candidate starts moving.
            if(Occupant){if(Occupant.wordId==noun)Reserved=Occupant;return;}
            Reserved=room.objects.Where(o=>o&&o.wordId==noun)
                .OrderBy(o=>(o.transform.position-LandingPoint(o)).sqrMagnitude)
                .ThenBy(o=>o.GetInstanceID()).FirstOrDefault();
        }

        public Vector3 LandingPoint(OpeningObject obj)
        {
            var b=obj.Bounds;
            return intake.position+new Vector3(obj.transform.position.x-b.center.x,
                obj.transform.position.y-b.min.y+.015f,obj.transform.position.z-b.center.z);
        }
        bool NearIntake(OpeningObject obj,float distance)=>obj&&
            Vector3.Distance(obj.transform.position,LandingPoint(obj))<distance;

        public void RefreshOccupancy()
        {
            if(Occupant&&!NearIntake(Occupant,claimRadius+.35f))
            {Occupant=null;elapsed=0;recipe=null;}
        }
        public bool TryTarget(OpeningObject obj,out Vector3 target)
        {
            RefreshOccupancy();target=obj.transform.position;
            if(obj!=Reserved||(Occupant&&Occupant!=obj))return false;
            if(!Occupant&&NearIntake(obj,claimRadius))
            {
                Occupant=obj;
                if(Occupant)
                {
                    recipe=RecipeForOccupant();
                    elapsed=0;Result=null;RefreshDisplay();
                }
            }
            target=LandingPoint(obj);
            float horizontal=Vector3.ProjectOnPlane(target-obj.transform.position,Vector3.up).magnitude;
            if(horizontal>.06f)
            {
                target.y+=.3f;
                if(obj.transform.position.y<target.y-.04f)
                {target.x=obj.transform.position.x;target.z=obj.transform.position.z;}
            }
            return true;
        }
        void Update()=>Tick(Time.deltaTime);
        AnalysisRecipe RecipeForOccupant()=>Occupant?recipes.Where(r=>r&&r.Matches(Occupant))
            .OrderByDescending(r=>r.requiredAppleForm!=AnalysisRecipe.AppleForm.Any).FirstOrDefault():null;
        public void RefreshAnalysis()
        {
            RefreshOccupancy();
            if(Occupant)
            {
                var next=RecipeForOccupant();
                if(next!=recipe){recipe=next;elapsed=0;Result=null;}
            }
            RefreshDisplay();
        }
        public void Tick(float deltaTime)
        {
            RefreshAnalysis();
            if(Occupant&&recipe&&!Result)
            {
                // Once delivered, the machine works independently of the movement sentence.
                if(NearIntake(Occupant,.075f))elapsed+=deltaTime;
                else elapsed=0;
                if(elapsed>=analysisSeconds)Result=recipe;
            }
            RefreshDisplay();
        }
        void RefreshDisplay()
        {
            if(resultRow)resultRow.SetActive(HasResult);
            if(progress){progress.gameObject.SetActive(Occupant&&recipe&&!Result);progress.fillAmount=Mathf.Clamp01(elapsed/analysisSeconds);progress.rectTransform.localScale=new Vector3(progress.fillAmount,1,1);}
            if(!Result)return;
            for(int i=0;i<outputSymbols.Length;i++)
            {outputSymbols[i].gameObject.SetActive(i<Result.SymbolCount);if(i<Result.SymbolCount)outputSymbols[i].sprite=Result.SymbolAt(i);}
        }
        public string[] ResultWords()=>Result?Result.outputs.Where(w=>w).Select(w=>w.Id).ToArray():new string[0];
    }
}
