using System.Collections;
using UnityEngine;

namespace PuzzleApple.V5.Outdoor
{
    public sealed class PlayerInstance : MonoBehaviour
    {
        public InstanceShape Shape {get;private set;}
        public WorldDropRegion Region {get;private set;}
        public bool Recovering {get;private set;}
        OpeningLight lamp;
        OpeningLightField field;
        public void Bind(OpeningLightField owner,OpeningLight light,InstanceShape shape,WorldDropRegion region){
            field=owner;lamp=light;Shape=shape;Region=region;
        }
        public void Recover(float delay){
            if(Recovering)return;
            Recovering=true;
            field.Remove(lamp);
            GetComponent<LightMotion>().SetMoving(false);
            GetComponent<Collider>().enabled=false;
            lamp.enabled=false;lamp.glow.enabled=false;
            StartCoroutine(Dissolve(delay));
        }
        IEnumerator Dissolve(float delay){
            if(delay>0)yield return new WaitForSecondsRealtime(delay);
            var size=transform.localScale;float visible=lamp.RevealAmount;
            for(float t=0;t<.6f;t+=Time.unscaledDeltaTime){
                float a=1-Mathf.SmoothStep(0,1,t/.6f);
                transform.localScale=size*Mathf.Lerp(.08f,1,a);
                lamp.SetReveal(field.RevealMaterial,visible*a);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
