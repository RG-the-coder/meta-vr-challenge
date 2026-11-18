using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattlefieldPrototype
{
    /// <summary>Bounded cosmetic effects, batched into one depth-tested mesh.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class BattleEffects : MonoBehaviour
    {
        public const int Capacity = 96;
        public int ActiveCount { get; private set; }
        struct Effect
        {
            public Vector3 Position, End, Velocity;
            public Color Color;
            public float Age, Duration, Radius;
            public int Kind; // 0: fleck; 1: ground pulse; 2: lightning segment
        }
        readonly Effect[] effects = new Effect[Capacity];
        readonly List<Vector3> vertices = new List<Vector3>(4096);
        readonly List<Color> colors = new List<Color>(4096);
        readonly List<int> triangles = new List<int>(8192);
        int cursor;
        BattleDirector director;
        Mesh mesh;
        Material material;
        MeshRenderer meshRenderer;

        public void Initialize(BattleDirector battle)
        {
            if (mesh != null) return;
            director = battle;
            var child = new GameObject("Batched combat effects", typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Transient battlefield effects" };
            mesh.MarkDynamic();
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = child.GetComponent<MeshRenderer>();
            var shader = Resources.Load<Shader>("BattlefieldEffects");
            if (shader != null) material = new Material(shader);
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
            director.RoundReset += Clear;
        }

        void Add(Effect effect)
        {
            if (director == null || !director.IsBattleRunning || !isActiveAndEnabled) return;
            effects[cursor] = effect;
            cursor = (cursor + 1) % Capacity;
        }

        public void Impact(Vector3 point, Vector3 normal, Color color)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = (i + cursor) * 2.39996f;
                Vector3 velocity = new Vector3(Mathf.Cos(a), .5f + (i % 3) * .3f, Mathf.Sin(a)) * .6f + normal * .8f;
                Add(new Effect { Position = point + normal * .03f, Velocity = velocity, Color = color,
                    Radius = .018f + i % 3 * .009f, Duration = .22f + i % 3 * .07f });
            }
        }

        public void Muzzle(Vector3 point, Vector3 direction, Color color)
        {
            Add(new Effect { Position = point + direction * .45f, Velocity = direction * .5f,
                Color = color, Radius = .045f, Duration = .055f });
        }

        public void Pulse(Vector3 point, float radius, Color color)
        {
            Add(new Effect { Position = point + Vector3.up * .07f, Color = color,
                Radius = Mathf.Min(8, radius), Duration = .4f, Kind = 1 });
        }

        public void Lightning(Vector3 from, Vector3 to)
        {
            Vector3 previous = from;
            for (int i = 1; i <= 5; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / 5f);
                if (i < 5) p += new Vector3(Mathf.Sin(i * 8.1f), Mathf.Cos(i * 4.3f), 0) * .10f;
                Add(new Effect { Position = previous, End = p, Color = new Color(.72f, .67f, 1f), Radius = .018f, Duration = .12f, Kind = 2 });
                previous = p;
            }
        }

        public void TowerWave(Vector3 point, float radius, Color color)
        {
            Add(new Effect { Position = point + Vector3.up * .1f, Color = color,
                Radius = Mathf.Clamp(radius, 1, 40), Duration = .75f, Kind = 1 });
        }

        void LateUpdate()
        {
            if (mesh == null) return;
            if (director == null || !director.IsBattleRunning) { Clear(); return; }
            vertices.Clear(); colors.Clear(); triangles.Clear();
            ActiveCount = 0;
            for (int i = 0; i < Capacity; i++)
            {
                var effect = effects[i];
                if (effect.Duration <= 0 || effect.Age >= effect.Duration) continue;
                effect.Age += Time.deltaTime;
                if (effect.Kind == 0)
                {
                    effect.Position += effect.Velocity * Time.deltaTime;
                    effect.Velocity += Vector3.down * Time.deltaTime * 2;
                }
                effects[i] = effect;
                if (effect.Age >= effect.Duration) continue;
                ActiveCount++;
                float t = effect.Age / effect.Duration;
                Color color = effect.Color;
                color.a *= (1 - t) * .8f;
                Vector3 p = transform.InverseTransformPoint(effect.Position);
                if (effect.Kind == 1)
                {
                    float r = effect.Radius * Mathf.Lerp(.25f, 1, t);
                    for (int segment = 0; segment < 32; segment++)
                    {
                        float a = segment * Mathf.PI / 16, b = (segment + 1) * Mathf.PI / 16;
                        Vector3 da = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), db = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                        Quad(p + da * r, p + da * (r + .045f), p + db * (r + .045f), p + db * r, color);
                    }
                }
                else if (effect.Kind == 2)
                {
                    Vector3 end = transform.InverseTransformPoint(effect.End);
                    Vector3 axis = (end - p).normalized;
                    Vector3 across = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized * effect.Radius;
                    Vector3 up = Vector3.Cross(axis, across);
                    Quad(p - across, p + across, end + across, end - across, color);
                    Quad(p - up, p + up, end + up, end - up, color);
                }
                else
                {
                    float r = effect.Radius * (1 - t * .5f);
                    Quad(p - Vector3.right * r, p + Vector3.up * r, p + Vector3.right * r, p - Vector3.up * r, color);
                    Quad(p - Vector3.forward * r, p + Vector3.up * r, p + Vector3.forward * r, p - Vector3.up * r, color);
                }
            }
            mesh.Clear();
            if (ActiveCount > 0)
            {
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            }
            meshRenderer.enabled = ActiveCount > 0 && material != null;
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        void Clear()
        {
            System.Array.Clear(effects, 0, effects.Length);
            ActiveCount = cursor = 0;
            if (mesh != null) mesh.Clear();
            if (meshRenderer != null) meshRenderer.enabled = false;
        }
        void OnDisable() { Clear(); }
        void OnDestroy()
        {
            if (director != null) director.RoundReset -= Clear;
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
