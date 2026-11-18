using UnityEngine;
using UnityEngine.Rendering;

namespace BattlefieldPrototype
{
    /// <summary>Lightweight spectral gauntlets, animated by controller grip and trigger rather than hand tracking.</summary>
    [DefaultExecutionOrder(190)]
    public sealed class BattleTouchHands : MonoBehaviour
    {
        PossessionController player;
        Material material, chargeMaterial;
        readonly Transform[] hands = new Transform[2], orbs = new Transform[2];
        readonly Transform[,] fingers = new Transform[2, 4];
        void Start()
        {
            player = GetComponent<PossessionController>();
            // A Resources material keeps the hand shader in standalone Quest builds.
            material = HandMaterial(new Color(.12f, .5f, .61f)); chargeMaterial = HandMaterial(new Color(.45f, .96f, 1));
            if (material == null || chargeMaterial == null) { enabled = false; return; }
            for (int h = 0; h < 2; h++)
            {
                hands[h] = new GameObject(h == 0 ? "Left spectral gauntlet" : "Right spectral gauntlet").transform;
                hands[h].SetParent(transform, false);
                var model = new GameObject("Grip aligned model").transform;
                model.SetParent(hands[h], false);
                // Unity's OpenXR grip has +Z from little finger to index and
                // palm normals +X (left), -X (right). The model was authored with
                // fingers along +Y and palm facing +Z, so identity faces backwards.
                model.localRotation = Quaternion.LookRotation(h == 0 ? Vector3.right : Vector3.left, Vector3.down);
                Shape(model, "Palm", new Vector3(0, -.015f, 0), new Vector3(.072f, .07f, .035f), material);
                Shape(model, "Wrist seal", new Vector3(0, -.075f, -.012f), new Vector3(.082f, .022f, .045f), chargeMaterial);
                var thumb = Shape(model, "Thumb", new Vector3(h == 0 ? .046f : -.046f, -.008f, .016f), new Vector3(.023f, .043f, .025f), material);
                thumb.localRotation = Quaternion.Euler(25, 0, h == 0 ? 30 : -30);
                for (int f = 0; f < 4; f++)
                {
                    fingers[h, f] = new GameObject("Finger " + f).transform;
                    fingers[h, f].SetParent(model, false);
                    fingers[h, f].localPosition = new Vector3((f - 1.5f) * .018f * (h == 0 ? -1 : 1), .018f, 0);
                    Shape(fingers[h, f], "Finger glow", new Vector3(0, .025f, 0), new Vector3(.013f, .05f, .016f), material);
                }
                orbs[h] = Shape(model, "Wave charge", new Vector3(0, .015f, .065f), Vector3.one * .025f, chargeMaterial, PrimitiveType.Sphere);
                hands[h].gameObject.SetActive(false);
            }
        }
        internal static Material HandMaterial(Color color)
        {
            // Keep an instancing-enabled material in Resources so player builds
            // retain this shader's instanced variants as well as its stereo passes.
            var source = Resources.Load<Material>("SkyholdGauntlet");
            if (source == null) return null;
            var result = new Material(source) { enableInstancing = true };
            result.SetColor("_BaseColor", color); return result;
        }
        internal static Transform Shape(Transform parent, string title, Vector3 at, Vector3 scale, Material mat, PrimitiveType type = PrimitiveType.Cube)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = title; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = at; obj.transform.localScale = scale;
            var collider = obj.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            var renderer = obj.GetComponent<Renderer>(); renderer.sharedMaterial = mat;
            renderer.sortingOrder = 600;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return obj.transform;
        }
        void OnEnable() => Application.onBeforeRender += UpdateHands;
        void LateUpdate() => UpdateHands();
        [BeforeRenderOrder(200)]
        void UpdateHands()
        {
            if (player == null || hands[0] == null) return;
            for (int h = 0; h < 2; h++)
            {
                Vector3 position = default; Quaternion rotation = Quaternion.identity;
                bool visible = player.isActiveAndEnabled && player.IsUsingXR && !player.IsDesktopInputActive &&
                    player.CurrentUnit == null && !player.TryOpticalHandPalm(h == 0, out _) && player.TryControllerGripPose(h == 0, out position, out rotation);
                hands[h].gameObject.SetActive(visible);
                if (!visible) continue;
                hands[h].SetPositionAndRotation(position, rotation);
                var input = h == 0 ? player.TouchInput.Left : player.TouchInput.Right;
                for (int f = 0; f < 4; f++)
                    fingers[h, f].localRotation = Quaternion.Euler((f == 0 && input.TriggerHeld ? 75 : input.Grip * 85), 0, 0);
                float glow = player.WaveGestureCharging ? .07f + Mathf.Sin(Time.unscaledTime * 15) * .01f : .025f;
                orbs[h].localScale = Vector3.one * glow;
            }
        }
        void OnDisable()
        {
            Application.onBeforeRender -= UpdateHands;
            foreach (var hand in hands) if (hand != null) hand.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            foreach (var hand in hands) if (hand != null) Destroy(hand.gameObject);
            if (material != null) Destroy(material); if (chargeMaterial != null) Destroy(chargeMaterial);
        }
    }
}
