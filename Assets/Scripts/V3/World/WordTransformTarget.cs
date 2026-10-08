using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V3
{
    // Explicit supported properties: adding a word does not imply every noun supports it.
    public sealed class WordTransformTarget : MonoBehaviour
    {
        public string wordId="equal";
        public Renderer[] surfaces=new Renderer[0];
        public bool supportsRed=true;
        public Color red=new Color(.65f,.025f,.025f,1);
        public bool IsRed { get; private set; }
        readonly Dictionary<Renderer,Material[]> originals=new Dictionary<Renderer,Material[]>();
        readonly List<Material> generated=new List<Material>();
        public bool Supports(string transformation)=>transformation=="red"&&supportsRed;
        public void SetRed(bool value)
        {
            value&=supportsRed;if(value==IsRed)return;
            if(value)
            {
                foreach(var surface in surfaces)
                {
                    if(!surface)continue;
                    var source=surface.sharedMaterials;originals[surface]=source;
                    var colored=new Material[source.Length];
                    for(int i=0;i<source.Length;i++)
                    {
                        if(!source[i])continue;
                        var material=new Material(source[i]);generated.Add(material);colored[i]=material;
                        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",red);
                        else if(material.HasProperty("_Color"))material.SetColor("_Color",red);
                    }
                    surface.sharedMaterials=colored;
                }
            }
            else
            {
                foreach(var pair in originals)if(pair.Key)pair.Key.sharedMaterials=pair.Value;
                foreach(var material in generated)if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);
                originals.Clear();generated.Clear();
            }
            IsRed=value;
        }
        void OnDisable()=>SetRed(false);
    }
}
