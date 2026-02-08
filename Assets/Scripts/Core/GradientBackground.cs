using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Applies a vertical gradient skybox to the camera.
    /// Attach to any GameObject or let GameSetup create it.
    /// </summary>
    public class GradientBackground : MonoBehaviour
    {
        [SerializeField] private Color topColor = Color.white;
        [SerializeField] private Color bottomColor = new Color(0.75f, 0.75f, 0.75f, 1f);

        private void Start()
        {
            Apply();
        }

        public void Apply()
        {
            Shader gradientShader = Shader.Find("Custom/GradientSkybox");
            if (gradientShader == null)
            {
                Debug.LogWarning("[GradientBackground] Custom/GradientSkybox shader not found");
                return;
            }

            Material skyboxMat = new Material(gradientShader);
            skyboxMat.SetColor("_TopColor", topColor);
            skyboxMat.SetColor("_BottomColor", bottomColor);

            RenderSettings.skybox = skyboxMat;

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
            }
        }
    }
}
