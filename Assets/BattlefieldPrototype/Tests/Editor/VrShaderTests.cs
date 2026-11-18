using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BattlefieldPrototype.Tests
{
    public sealed class VrShaderTests
    {
        [TestCase("BattlefieldOverlay")]
        [TestCase("BattlefieldHands")]
        public void UiAndHandShadersDrawIntoBothStereoTextureSlices(string resource)
        {
            var shader = Resources.Load<Shader>(resource);
            var material = new Material(shader) { enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            var target = new RenderTexture(32, 32, 0) { dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray, volumeDepth = 2 };
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-.8f,-.8f,.5f), new Vector3(.8f,-.8f,.5f), new Vector3(.8f,.8f,.5f), new Vector3(-.8f,.8f,.5f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0 };
            var commands = new UnityEngine.Rendering.CommandBuffer();
            var pixels = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                material.EnableKeyword("STEREO_INSTANCING_ON");

                ShaderUtil.CompilePass(material, 0, true);
                commands.SetRenderTarget(target, 0, CubemapFace.Unknown, -1);
                commands.ClearRenderTarget(false, true, Color.clear);
                commands.SetGlobalMatrixArray("unity_StereoMatrixVP", new[] { Matrix4x4.identity, Matrix4x4.identity });
                commands.SetGlobalMatrixArray("unity_StereoMatrixV", new[] { Matrix4x4.identity, Matrix4x4.identity });
                commands.SetGlobalMatrixArray("unity_StereoMatrixP", new[] { Matrix4x4.identity, Matrix4x4.identity });
                commands.DrawMeshInstanced(mesh, 0, material, 0, new[] { Matrix4x4.identity, Matrix4x4.identity }, 2);
                Graphics.ExecuteCommandBuffer(commands);
                for (int eye = 0; eye < 2; eye++)
                {
                    Graphics.SetRenderTarget(target, 0, CubemapFace.Unknown, eye);
                    pixels.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); pixels.Apply();
                    Assert.That(pixels.GetPixel(16,16).r, Is.GreaterThan(.6f), resource + " missing from eye slice " + eye);
                }
            }
            finally
            {
                RenderTexture.active = previous; commands.Release(); target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(material);
                Object.DestroyImmediate(mesh); Object.DestroyImmediate(pixels);
            }
        }

        [TestCase("BattlefieldOverlay")]
        [TestCase("BattlefieldEffects")]
        [TestCase("BattlefieldSky")]
        [TestCase("BattlefieldHands")]
        public void CustomShadersCompileSinglePassStereoVariants(string resource)
        {
            var shader = Resources.Load<Shader>(resource);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.keywordSpace.FindKeyword("STEREO_INSTANCING_ON").isValid, Is.True,
                "The shader must include an instanced stereo variant, not just stereo struct macros.");
            Assert.That(shader.keywordSpace.FindKeyword("STEREO_MULTIVIEW_ON").isValid, Is.True);
            var material = new Material(shader) { enableInstancing = true };
            try
            {
                material.EnableKeyword("STEREO_INSTANCING_ON");
                ShaderUtil.CompilePass(material, 0, true);
                Assert.That(ShaderUtil.IsPassCompiled(material, 0), Is.True);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
