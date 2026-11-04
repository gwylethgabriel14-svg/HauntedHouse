using System;
using System.Collections.Generic;
using UnityEngine;

// Put this file in Assets/Scripts/  (NOT in the Editor folder).
// Backup data for the Burn House editor tool. Lives on the "BurnDecor" object.
public class BurnState : MonoBehaviour
{
    [Serializable]
    public class RendererBackup
    {
        public Renderer renderer;
        public Material[] materials;
        public MeshFilter filter;
        public Mesh mesh;
    }

    [Serializable]
    public class LightBackup
    {
        public Light light;
        public Color color;
        public float intensity;
        public bool hasBase;
        public float baseIntensity;
    }

    public List<RendererBackup> renderers = new List<RendererBackup>();
    public List<LightBackup> lights = new List<LightBackup>();

    public bool envSaved;
    public bool fogWas;
    public FogMode fogModeWas;
    public Color fogColorWas;
    public float fogDensityWas;
    public UnityEngine.Rendering.AmbientMode ambientModeWas;
    public Color ambientLightWas;
}
