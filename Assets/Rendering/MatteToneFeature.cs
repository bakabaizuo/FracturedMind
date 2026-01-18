using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MatteToneFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class MatteToneSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRendering;
        public Shader shader;
        public float strength = 1f;
        public float threshold = 0.7f;
        public float compression = 0.6f;
        public float desaturate = 0.6f;
        public int facetSteps = 1;
    }

    public MatteToneSettings settings = new MatteToneSettings();

    class MatteTonePass : ScriptableRenderPass
    {
        Material m_Material;
        RenderTargetIdentifier currentTarget;
        MatteToneSettings m_Settings;

        public MatteTonePass(Material mat, MatteToneSettings settings)
        {
            m_Material = mat;
            m_Settings = settings;
        }

        public void Setup(RenderTargetIdentifier src)
        {
            currentTarget = src;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_Material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("MatteTonePost");

            var cameraColor = currentTarget;
            int tempID = Shader.PropertyToID("_TempMatteToneRT");
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            cmd.GetTemporaryRT(tempID, desc, FilterMode.Bilinear);

            // set parameters
            m_Material.SetFloat("_Strength", m_Settings.strength);
            m_Material.SetFloat("_Threshold", m_Settings.threshold);
            m_Material.SetFloat("_Compression", m_Settings.compression);
            m_Material.SetFloat("_Desaturate", m_Settings.desaturate);
            m_Material.SetFloat("_FacetSteps", Mathf.Max(1, m_Settings.facetSteps));

            // Blit source -> temp with material then back
            cmd.Blit(cameraColor, tempID);
            cmd.Blit(tempID, cameraColor, m_Material);

            cmd.ReleaseTemporaryRT(tempID);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    Material m_Material;
    MatteTonePass m_ScriptablePass;

    public override void Create()
    {
        if (settings.shader == null)
        {
            settings.shader = Shader.Find("Hidden/URP/MatteTonePost");
        }

        if (settings.shader != null)
            m_Material = CoreUtils.CreateEngineMaterial(settings.shader);

        m_ScriptablePass = new MatteTonePass(m_Material, settings);
        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Material == null) return;
        // Use BuiltinRenderTextureType.CameraTarget to avoid calling renderer.cameraColorTarget here
        var cameraTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
        m_ScriptablePass.Setup(cameraTarget);
        renderer.EnqueuePass(m_ScriptablePass);
    }
}
