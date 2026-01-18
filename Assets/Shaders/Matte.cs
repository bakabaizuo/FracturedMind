using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Matte : ScriptableRendererFeature
{
    [System.Serializable]
    public class MatteSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRendering;
        public Shader shader = null;
        [Range(0f,1f)] public float strength = 1f;
        [Range(0f,1f)] public float threshold = 0.7f;
        [Range(0.2f,1f)] public float compression = 0.6f;
        [Range(0f,1f)] public float desaturate = 0.6f;
        [Range(1,16)] public int facetSteps = 1;
    }

    public MatteSettings settings = new MatteSettings();

    class MattePass : ScriptableRenderPass
    {
        Material mat;
        MatteSettings settings;
        RenderTargetIdentifier source;

        public MattePass(Material material, MatteSettings settings)
        {
            this.mat = material;
            this.settings = settings;
        }

        public void Setup(RenderTargetIdentifier src)
        {
            source = src;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (mat == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("MattePass");

            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            int tmp = Shader.PropertyToID("_MatteTmpRT");
            cmd.GetTemporaryRT(tmp, desc, FilterMode.Bilinear);

            mat.SetFloat("_Strength", settings.strength);
            mat.SetFloat("_Threshold", settings.threshold);
            mat.SetFloat("_Compression", settings.compression);
            mat.SetFloat("_Desaturate", settings.desaturate);
            mat.SetFloat("_FacetSteps", Mathf.Max(1, settings.facetSteps));

            // Copy source -> tmp, then tmp -> source with material
            cmd.Blit(source, tmp);
            cmd.Blit(tmp, source, mat);

            cmd.ReleaseTemporaryRT(tmp);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    Material material;
    MattePass pass;

    public override void Create()
    {
        if (settings.shader == null)
            settings.shader = Shader.Find("Hidden/URP/MatteTonePost");

        if (settings.shader != null)
            material = new Material(settings.shader);

        pass = new MattePass(material, settings)
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null) return;
        // Avoid calling renderer.cameraColorTarget here; use the built-in camera target identifier instead.
        var cameraTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
        pass.Setup(cameraTarget);
        renderer.EnqueuePass(pass);
    }
}


