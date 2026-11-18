using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BattlefieldPrototype
{
    /// <summary>A cached portrait of the actual battlefield model, rendered only when the class changes.</summary>
    public sealed class BattleCharacterPreview : MonoBehaviour
    {
        GameObject studio;
        Transform model;
        Camera portraitCamera;
        RenderTexture texture;
        BattlefieldBootstrap bootstrap;
        RawImage image;
        UnitRole shownRole;
        bool hasRole, dirty;
        static readonly HashSet<int> studioSlots = new HashSet<int>();
        int studioSlot = -1;

        public void Initialize(BattlefieldBootstrap source, UnitRole role, Material uiMaterial)
        {
            bootstrap = source;
            image = GetComponent<RawImage>();
            image.material = uiMaterial;
            image.raycastTarget = false;
            SetRole(role);
        }

        public void SetRole(UnitRole role)
        {
            if (bootstrap == null || hasRole && shownRole == role) return;
            hasRole = true; shownRole = role;
            if (studio == null)
            {
                studio = new GameObject("Character portrait studio");
                studio.transform.SetParent(bootstrap.transform, false);
                // Each visible panel owns an isolated studio, including the editor
                // portrait underneath an open dossier and during layout rebuilds.
                studioSlot = 0;
                while (studioSlots.Contains(studioSlot)) studioSlot++;
                studioSlots.Add(studioSlot);
                studio.transform.localPosition = new Vector3(studioSlot * 20, -600, 0);
                int portraitWidth = Mathf.Clamp(Mathf.RoundToInt(640 * image.rectTransform.rect.width / Mathf.Max(1, image.rectTransform.rect.height)), 128, 960);
                texture = new RenderTexture(portraitWidth, 640, 24, RenderTextureFormat.ARGB32) { name = "Army creator portrait", antiAliasing = 2 };
                texture.Create(); image.texture = texture;
                var cameraObject = new GameObject("Portrait camera", typeof(Camera));
                cameraObject.transform.SetParent(studio.transform, false);
                portraitCamera = cameraObject.GetComponent<Camera>();
                portraitCamera.enabled = false;
                portraitCamera.clearFlags = CameraClearFlags.SolidColor;
                portraitCamera.backgroundColor = new Color(.045f, .061f, .071f, 1);
                portraitCamera.cullingMask = 1 << 30;
                portraitCamera.nearClipPlane = .05f;
                portraitCamera.farClipPlane = 12;
                portraitCamera.orthographic = true;
                portraitCamera.allowHDR = false;
                portraitCamera.targetTexture = texture;
                AddLight("Warm portrait key", new Vector3(-2, 3, 3), new Color(1, .83f, .6f), 5);
                AddLight("Cool portrait rim", new Vector3(2, 2, -1), new Color(.35f, .68f, 1), 6);
                AddLight("Portrait fill", new Vector3(1, 1, 3), new Color(.7f, .8f, 1), 2);
            }
            if (model != null) { model.gameObject.SetActive(false); Destroy(model.gameObject); }
            model = bootstrap.CreateCharacterPreview(role, studio.transform);
            model.localRotation = Quaternion.Euler(0, -24, 0);
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(model.position + Vector3.up, Vector3.one);
            var colors=new MaterialPropertyBlock();
            foreach (Renderer renderer in renderers)
            {
                // Portraits render on demand in the same frame as a model swap. The GPU
                // resident drawer can retain the previous class for that manual request.
                colors.Clear(); colors.SetColor("_BaseColor",renderer.sharedMaterial.color); colors.SetColor("_Color",renderer.sharedMaterial.color);
                renderer.SetPropertyBlock(colors);
                bounds.Encapsulate(renderer.bounds);
            }
            Vector3 target = bounds.center;
            float extent = Mathf.Max(bounds.extents.y, bounds.extents.x * texture.height / texture.width) * 1.13f;
            portraitCamera.orthographicSize = Mathf.Max(.7f, extent);
            portraitCamera.transform.position = target + new Vector3(0, .16f, 4.5f);
            portraitCamera.transform.LookAt(target);
            dirty = true;
        }

        void AddLight(string label, Vector3 position, Color color, float intensity)
        {
            var lightObject = new GameObject(label, typeof(Light));
            lightObject.transform.SetParent(studio.transform, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Point; light.range = 9; light.intensity = intensity;
            light.color = color; light.cullingMask = 1 << 30; light.shadows = LightShadows.None;
        }

        void LateUpdate()
        {
            if (!dirty || portraitCamera == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            dirty = false;
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(portraitCamera, new RenderPipeline.StandardRequest { destination = texture });
            else portraitCamera.Render();
        }

        void OnEnable() { if (studio != null) studio.SetActive(true); }
        void OnDisable() { if (studio != null) studio.SetActive(false); }
        void OnDestroy()
        {
            if (studio != null) Destroy(studio);
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (studioSlot >= 0) studioSlots.Remove(studioSlot);
        }
    }
}
