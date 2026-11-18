using UnityEngine;

namespace Betta3D
{
    /// <summary>
    /// Global lighting values the betta shaders read (ambient, back light, reflections, tone mapping).
    /// If nothing sets them, the first fish applies the defaults below (an aquarium look).
    /// An app that sets its own values calls Apply / sets Configured = true first.
    /// </summary>
    public static class BettaLook
    {
        public static bool Configured;

        public static Color Ambient = new Color(0.30f, 0.38f, 0.46f);
        public static Vector3 RimDirection = new Vector3(0.15f, 0.6f, 0.8f);   // toward the back light
        public static Color RimColor = new Color(0.88f, 0.99f, 1.1f);
        public static Color EnvTop = new Color(0.75f, 0.9f, 1.0f);
        public static Color EnvBottom = new Color(0.02f, 0.06f, 0.09f);
        /// <summary>Tone-map in the shaders (for cameras without post-processing, e.g. a transparent window).</summary>
        public static bool ShaderTonemap;
        public static float Exposure = 1.27f;

        public static void Apply()
        {
            Shader.SetGlobalColor("_BettaAmbient", Ambient);
            Shader.SetGlobalVector("_BettaRimDir", RimDirection.normalized);
            Shader.SetGlobalColor("_BettaRimColor", RimColor);
            Shader.SetGlobalColor("_BettaEnvTop", EnvTop);
            Shader.SetGlobalColor("_BettaEnvBottom", EnvBottom);
            Shader.SetGlobalFloat("_BettaTonemap", ShaderTonemap ? 1f : 0f);
            Shader.SetGlobalFloat("_BettaExposure", Exposure);
            Configured = true;
        }

        public static void EnsureConfigured()
        {
            if (!Configured) Apply();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { Configured = false; }   // works with "enter play mode" without domain reload
    }
}
