using System;
using UnityEngine;

namespace PuzzleApple.V5
{
    public enum InstanceShape { Sphere, Cube, Octahedron }
    public sealed class WorldDropRegion : MonoBehaviour
    {
        public bool allowInstantiation;
        public string[] words={"light"};
        public InstanceShape[] shapes={InstanceShape.Sphere,InstanceShape.Cube,InstanceShape.Octahedron};
        public bool Accepts(string word)=>allowInstantiation && shapes!=null && shapes.Length>0 && words!=null && Array.IndexOf(words,word)>=0;
    }
}
