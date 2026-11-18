using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Hands;

namespace BattlefieldPrototype
{
    [DefaultExecutionOrder(195)]
    public sealed class BattleOpticalHands : MonoBehaviour
    {
        PossessionController player;
        Material material, accent, feedbackMaterial;
        readonly Transform[] hands = new Transform[2], palms = new Transform[2], cuffs = new Transform[2];
        readonly Transform[,,] fingers = new Transform[2, 5, 3];
        readonly Vector3[] joints = new Vector3[4];
        LineRenderer moveRing, moveLine;
        static readonly XRHandJointID[][] Paths = {
            new[] { XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip },
            new[] { XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
            new[] { XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
            new[] { XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
            new[] { XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip }
        };
        void Start()
        {
            player = GetComponent<PossessionController>();
            material = BattleTouchHands.HandMaterial(new Color(.12f, .5f, .61f));
            accent = BattleTouchHands.HandMaterial(new Color(.45f, .96f, 1));
            if (material == null || accent == null) { enabled = false; return; }
            for (int h = 0; h < 2; h++)
            {
                hands[h] = new GameObject(h == 0 ? "Left tracked gauntlet" : "Right tracked gauntlet").transform;
                hands[h].SetParent(transform, false);
                palms[h] = BattleTouchHands.Shape(hands[h], "Palm", Vector3.zero, Vector3.one, material);
                cuffs[h] = BattleTouchHands.Shape(hands[h], "Wrist seal", Vector3.zero, Vector3.one, accent);
                for (int f = 0; f < 5; f++) for (int b = 0; b < 3; b++)
                    fingers[h, f, b] = BattleTouchHands.Shape(hands[h], "Finger " + f + " joint " + b, Vector3.zero, Vector3.one, material);
                hands[h].gameObject.SetActive(false);
            }
            var shader = Resources.Load<Shader>("BattlefieldEffects");
            if (shader != null)
            {
                feedbackMaterial = new Material(shader) { enableInstancing = true };
                moveRing = Line("Hand movement anchor", 24, .004f); moveRing.loop = true;
                moveLine = Line("Hand movement direction", 2, .005f);
            }
        }
        LineRenderer Line(string name, int points, float width)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(transform, false); line.sharedMaterial = feedbackMaterial;
            line.useWorldSpace = true; line.positionCount = points; line.startWidth = line.endWidth = width;
            line.numCapVertices = 4; line.enabled = false;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        void OnEnable() => Application.onBeforeRender += UpdateHands;
        void LateUpdate() => UpdateHands();
        [BeforeRenderOrder(205)] void UpdateHands()
        {
            if (player == null || hands[0] == null) return;
            bool active = player.isActiveAndEnabled && player.IsUsingXR;
            accent.SetColor("_BaseColor", player.WaveGestureCharging ? new Color(1, .75f, .2f) : new Color(.45f, .96f, 1));
            for (int h = 0; h < 2; h++)
            {
                Pose palm = default, wrist = default, index = default, little = default;
                bool tracked = active && player.TryOpticalJoint(h == 0, XRHandJointID.Palm, out palm);
                hands[h].gameObject.SetActive(tracked);
                if (!tracked) continue;
                // An occluded knuckle must not hide the entire tracked hand.
                if (!player.TryOpticalJoint(h == 0, XRHandJointID.Wrist, out wrist))
                    wrist = new Pose(palm.position - palm.rotation * Vector3.forward * .045f, palm.rotation);
                if (!player.TryOpticalJoint(h == 0, XRHandJointID.IndexProximal, out index))
                    index = new Pose(palm.position + palm.rotation * new Vector3(-.025f, 0, .035f), palm.rotation);
                if (!player.TryOpticalJoint(h == 0, XRHandJointID.LittleProximal, out little))
                    little = new Pose(palm.position + palm.rotation * new Vector3(.025f, 0, .035f), palm.rotation);
                Vector3 knuckles = (index.position + little.position) * .5f;
                Vector3 forward = knuckles - wrist.position;
                Quaternion rotation = forward.sqrMagnitude > .00001f ? Quaternion.LookRotation(forward, palm.rotation * Vector3.up) : palm.rotation;
                float width = Mathf.Clamp(Vector3.Distance(index.position, little.position) + .018f, .055f, .105f);
                palms[h].SetPositionAndRotation(Vector3.Lerp(wrist.position, knuckles, .55f), rotation);
                palms[h].localScale = new Vector3(width, .028f, Mathf.Clamp(forward.magnitude, .045f, .11f));
                cuffs[h].SetPositionAndRotation(wrist.position, rotation);
                cuffs[h].localScale = new Vector3(width + .01f, .038f, .02f);
                bool pinched = player.OpticalPinchHeld(h == 0);
                for (int f = 0; f < 5; f++)
                {
                    bool valid = true;
                    for (int j = 0; j < 4; j++)
                    {
                        if (!player.TryOpticalJoint(h == 0, Paths[f][j], out var pose)) { valid = false; break; }
                        joints[j] = pose.position;
                    }
                    for (int b = 0; b < 3; b++)
                    {
                        var segment = fingers[h, f, b];
                        Vector3 delta = valid ? joints[b + 1] - joints[b] : Vector3.zero;
                        bool visible = valid && delta.sqrMagnitude > .000001f;
                        segment.gameObject.SetActive(visible);
                        if (!visible) continue;
                        // Full anatomical finger lengths and all three tracked articulations.
                        // Slight overlap joins the armor plates without moving tracked fingertips.
                        segment.SetPositionAndRotation((joints[b] + joints[b + 1]) * .5f, Quaternion.FromToRotation(Vector3.up, delta));
                        float thickness = (f == 0 ? .019f : f == 4 ? .012f : .015f) - b * .001f;
                        segment.localScale = new Vector3(thickness, delta.magnitude + .003f, thickness);
                        segment.GetComponent<Renderer>().sharedMaterial = pinched && f < 2 && b == 2 ? accent : material;
                    }
                }
            }
            if (moveRing == null) return;
            Vector3 origin = default, hand = default;
            bool moving = active && player.TryHandMoveFeedback(out origin, out hand);
            moveRing.enabled = moveLine.enabled = moving;
            if (!moving) return;
            moveLine.SetPosition(0, origin); moveLine.SetPosition(1, hand);
            moveLine.startColor = moveLine.endColor = moveRing.startColor = moveRing.endColor = new Color(1, .75f, .2f);
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2 / 24;
                moveRing.SetPosition(i, origin + player.ViewCamera.transform.right * (Mathf.Cos(angle) * .025f) + Vector3.up * (Mathf.Sin(angle) * .025f));
            }
        }
        void OnDisable()
        {
            Application.onBeforeRender -= UpdateHands;
            foreach (var hand in hands) if (hand != null) hand.gameObject.SetActive(false);
            if (moveRing != null) moveRing.enabled = false; if (moveLine != null) moveLine.enabled = false;
        }
        void OnDestroy()
        {
            foreach (var hand in hands) if (hand != null) Destroy(hand.gameObject);
            if (moveRing != null) Destroy(moveRing.gameObject); if (moveLine != null) Destroy(moveLine.gameObject);
            if (material != null) Destroy(material); if (accent != null) Destroy(accent);
            if (feedbackMaterial != null) Destroy(feedbackMaterial);
        }
    }
}
