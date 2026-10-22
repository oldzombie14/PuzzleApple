using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V4.Editor
{
    public static class CloudMaskAuthoring
    {
        public static Texture2D GetOrCreate()
        {
            const string path = "Assets/Textures/V4/CloudOpacity.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing) return existing;
            if (!AssetDatabase.IsValidFolder("Assets/Textures/V4")) AssetDatabase.CreateFolder("Assets/Textures","V4");
            const int size = 512;
            var texture = new Texture2D(size,size,TextureFormat.RGBA32,true,true)
            { name="CloudOpacity", wrapMode=TextureWrapMode.Repeat, filterMode=FilterMode.Trilinear, anisoLevel=4 };
            var pixels = new Color[size*size];
            for (int y=0; y<size; y++) for (int x=0; x<size; x++)
            {
                var uv = new Vector2((x+.5f)/size,(y+.5f)/size);
                float n = Noise(uv,8)*.57f+Noise(uv,16)*.28f+Noise(uv,32)*.15f;
                float alpha = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.47f,.67f,n));
                // URP RGB cookies encode transmitted light, not opacity.
                // Derive it exclusively from the exact alpha used to draw the clouds.
                float transmission = 1-.85f*alpha;
                pixels[y*size+x] = new Color(transmission,transmission,transmission,alpha);
            }
            texture.SetPixels(pixels); texture.Apply(true,false);
            AssetDatabase.CreateAsset(texture,path);
            return texture;
        }

        static float Hash(int x,int y,int period)
        {
            x=(x%period+period)%period; y=(y%period+period)%period;
            unchecked
            {
                uint h=(uint)(x*374761393+y*668265263+127);
                h=(h^(h>>13))*1274126177u;
                return (h^(h>>16))/(float)uint.MaxValue;
            }
        }
        static float Noise(Vector2 uv,int cells)
        {
            var p=uv*cells; int x=Mathf.FloorToInt(p.x), y=Mathf.FloorToInt(p.y);
            float fx=Mathf.SmoothStep(0,1,p.x-x), fy=Mathf.SmoothStep(0,1,p.y-y);
            return Mathf.Lerp(Mathf.Lerp(Hash(x,y,cells),Hash(x+1,y,cells),fx),
                Mathf.Lerp(Hash(x,y+1,cells),Hash(x+1,y+1,cells),fx),fy);
        }
    }
}
