using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wapawapa.Rendering
{
    public sealed class BloodFocusRadialGrayscaleFeature : ScriptableRendererFeature
    {
        public static Vector2 Center = new Vector2(0.5f, 0.5f);
        public static float Radius;
        public static float Edge = 0.08f;
        public static float Strength = 1f;

        private Material material;
        private RadialPass pass;

        public override void Create()
        {
            var shader = Shader.Find("Hidden/Wapawapa/BloodFocusRadialGrayscale");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new RadialPass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || pass == null || Strength <= 0.001f) return;
            Debug.Log($"[BloodFocusRadialGrayscaleFeature] Enqueue center={Center} radius={Radius} strength={Strength}");
            material.SetVector("_Center", Center);
            material.SetFloat("_Radius", Radius);
            material.SetFloat("_Edge", Edge);
            material.SetFloat("_Strength", Strength);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        private sealed class RadialPass : ScriptableRenderPass
        {
            private readonly Material material;
            private RTHandle temp;

            public RadialPass(Material material) => this.material = material;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref temp, descriptor, FilterMode.Bilinear, name: "BloodFocusRadialGrayscale");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get("Blood Focus Radial Grayscale");
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                Blitter.BlitCameraTexture(cmd, source, temp, material, 0);
                Blitter.BlitCameraTexture(cmd, temp, source);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
