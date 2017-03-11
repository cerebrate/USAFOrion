#region using

using UnityEngine ;

#endregion

namespace USAFOrion
{
    // Special effect for the flash of light created by bomb detonation
    public class NukeFlash : MonoBehaviour
    {
        private readonly float extinguishDelay = 0.05f ; // time delay to destruction, in seconds

        public void Awake ()
        {
            Light light ;
            Destroy (this.gameObject, this.extinguishDelay) ;
            this.gameObject.AddComponent<Light> () ;
            light = this.gameObject.GetComponent<Light>();
            light.type = LightType.Point ; // Spot, Directional, or Point
            light.color = Color.white ; // The color of the light.
            light.range = 2000f ; // The range of the light. What does that mean?
            light.intensity = 5.0f ; // The Intensity of a light is multiplied with the Light color.
            light.renderMode = LightRenderMode.ForcePixel ; // for very important lights that absolutely must be visible
        }
    }
}
