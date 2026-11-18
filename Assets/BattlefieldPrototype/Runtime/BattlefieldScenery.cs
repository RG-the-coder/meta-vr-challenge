using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattlefieldPrototype
{
    /// <summary>Runtime surface detail and atmosphere; never contributes gameplay colliders.</summary>
    public sealed class BattlefieldScenery : MonoBehaviour
    {
        readonly List<Object> owned = new List<Object>();
        Material previousSky, sky;
        AmbientMode previousAmbient;
        Color previousSkyColor, previousEquator, previousGround, previousFog;
        bool previousFogEnabled, initialized, applied;
        FogMode previousFogMode;
        float previousFogStart, previousFogEnd, previousAmbientIntensity;
        Light sun, previousSun;
        Quaternion sunRotation;
        Color sunColor;
        float sunIntensity, sunStrength, sunBias, sunNormalBias;
        LightShadows sunShadows;
        bool ownsSun;
        int surfaceResources;
        float atmosphereScale=1;

        public void Initialize(Material earth, Material concrete, Vector2 arenaSize)
        {
            if (initialized) return;
            initialized = true;
            atmosphereScale=Mathf.Max(1,Mathf.Max(arenaSize.x,arenaSize.y)/44);
            previousSky = RenderSettings.skybox;
            previousAmbient = RenderSettings.ambientMode;
            previousSkyColor = RenderSettings.ambientSkyColor;
            previousEquator = RenderSettings.ambientEquatorColor;
            previousGround = RenderSettings.ambientGroundColor;
            previousAmbientIntensity = RenderSettings.ambientIntensity;
            previousFogEnabled = RenderSettings.fog;
            previousFogMode = RenderSettings.fogMode;
            previousFog = RenderSettings.fogColor;
            previousFogStart = RenderSettings.fogStartDistance;
            previousFogEnd = RenderSettings.fogEndDistance;
            previousSun = RenderSettings.sun;
            foreach (var candidate in FindObjectsByType<Light>())
                if (candidate.isActiveAndEnabled && candidate.type == LightType.Directional) { sun = candidate; break; }
            if (sun == null)
            {
                sun = new GameObject("Late afternoon sun").AddComponent<Light>();
                sun.transform.SetParent(transform, false);
                sun.type = LightType.Directional;
                ownsSun = true;
            }
            sunRotation = sun.transform.rotation;
            sunColor = sun.color;
            sunIntensity = sun.intensity;
            sunShadows = sun.shadows;
            sunStrength = sun.shadowStrength;
            sunBias = sun.shadowBias;
            sunNormalBias = sun.shadowNormalBias;
            Shader shader = Resources.Load<Shader>("BattlefieldSky");
            if (shader != null) { sky = new Material(shader) { name = "Battlefield hazy daylight" }; owned.Add(sky); }
            Texture2D soil = MakeSurface("Soft meadow playmat", true);
            Texture2D cement = MakeSurface("Painted cream masonry", false);
            earth.mainTexture = soil;
            earth.mainTextureScale = arenaSize / 48f;
            concrete.mainTexture = cement;
            concrete.mainTextureScale = Vector2.one;
            SetRelief(earth, true);
            SetRelief(concrete, false);
            Apply();
            surfaceResources = owned.Count;
        }

        public void ClearDressing()
        {
            for (int i = owned.Count - 1; i >= surfaceResources; i--)
            {
                if (owned[i] != null) Destroy(owned[i]);
                owned.RemoveAt(i);
            }
        }

        // Broad, low-contrast color patches read like a painted toy landscape at every scale.
        Texture2D MakeSurface(string name, bool meadow)
        {
            const int size = 256;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float patch = Mathf.SmoothStep(0, 1, Noise(x, y, 4));
                    pixels[y * size + x] = meadow
                        ? Color.Lerp(new Color(.43f, .62f, .28f), new Color(.54f, .71f, .35f), patch)
                        : Color.Lerp(new Color(.83f, .77f, .62f), new Color(.88f, .82f, .69f), patch);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGB24, true)
                { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            owned.Add(texture);
            return texture;
        }

        static void SetRelief(Material material, bool meadow)
        {
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", meadow ? .16f : .28f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", meadow ? .16f : .28f);
        }

        static float Hash(int x, int y)
        {
            uint h = unchecked((uint)(x * 374761393 + y * 668265263 + 9871));
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        static float Noise(int x, int y, int cells)
        {
            float px = x / 256f * cells, py = y / 256f * cells;
            int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py);
            float fx = Mathf.SmoothStep(0, 1, px - ix), fy = Mathf.SmoothStep(0, 1, py - iy);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix % cells, iy % cells), Hash((ix + 1) % cells, iy % cells), fx),
                Mathf.Lerp(Hash(ix % cells, (iy + 1) % cells), Hash((ix + 1) % cells, (iy + 1) % cells), fx), fy);
        }

        void Apply()
        {
            if (!initialized || applied) return;
            applied = true;
            if (ownsSun) sun.enabled = true;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            sun.color = new Color(1f, .96f, .85f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .55f;
            sun.shadowBias = .025f;
            sun.shadowNormalBias = .18f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.65f, .77f, .89f);
            RenderSettings.ambientEquatorColor = new Color(.53f, .57f, .49f);
            RenderSettings.ambientGroundColor = new Color(.34f, .39f, .25f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.72f, .84f, .88f);
            RenderSettings.fogStartDistance = 65*atmosphereScale;
            RenderSettings.fogEndDistance = 230*atmosphereScale;
            if (sky != null) RenderSettings.skybox = sky;
        }

        void OnEnable() { Apply(); }
        void OnDisable()
        {
            if (!applied) return;
            applied = false;
            if (ownsSun && sun != null) sun.enabled = false;
            RenderSettings.skybox = previousSky;
            RenderSettings.sun = previousSun;
            RenderSettings.ambientMode = previousAmbient;
            RenderSettings.ambientSkyColor = previousSkyColor;
            RenderSettings.ambientEquatorColor = previousEquator;
            RenderSettings.ambientGroundColor = previousGround;
            RenderSettings.ambientIntensity = previousAmbientIntensity;
            RenderSettings.fog = previousFogEnabled;
            RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogColor = previousFog;
            RenderSettings.fogStartDistance = previousFogStart;
            RenderSettings.fogEndDistance = previousFogEnd;
            if (sun != null && !ownsSun)
            {
                sun.transform.rotation = sunRotation;
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.shadows = sunShadows;
                sun.shadowStrength = sunStrength;
                sun.shadowBias = sunBias;
                sun.shadowNormalBias = sunNormalBias;
            }
        }
        void OnDestroy() { foreach (var item in owned) if (item != null) Destroy(item); }

        public void DressArena(Transform arena, Vector2 size, Material earth, Material concrete, Material metal)
        {
            // Shared static meshes keep the scenery to a handful of renderers.
            var concreteParts = new List<CombineInstance>();
            var metalParts = new List<CombineInstance>();
            var rubble = new List<CombineInstance>();
            var grass = new List<CombineInstance>();
            var bronzeParts = new List<CombineInstance>();
            var blueCloth = new List<CombineInstance>();
            var redCloth = new List<CombineInstance>();
            Mesh cube = BeveledBlock(Vector3.one, .16f), rock = RockMesh(), tuft = GrassMesh(), flagMesh = BannerMesh();
            owned.Add(cube); owned.Add(rock); owned.Add(tuft); owned.Add(flagMesh);
            Material rockMaterial = Tint(concrete, "Smooth pebble grey", new Color(.68f, .74f, .71f));
            Material bronze = Tint(metal, "Painted golden fittings", new Color(.83f, .61f, .25f));
            bronze.mainTexture = null; SetRelief(bronze, false);
            Material vegetation = Tint(earth, "Meadow grass tufts", new Color(.32f, .53f, .19f));
            vegetation.mainTexture = null;
            if (vegetation.HasProperty("_BumpMap")) { vegetation.SetTexture("_BumpMap", null); vegetation.DisableKeyword("_NORMALMAP"); }
            if (vegetation.HasProperty("_Cull")) vegetation.SetFloat("_Cull", 0);
            foreach (Transform cover in arena)
            {
                bool boundary = cover.name.EndsWith("boundary");
                bool masonry = cover.GetComponent<BoxCollider>() != null && cover.name != "Ground" && cover.localScale.y > .5f;
                if (!boundary && !masonry) continue;
                Vector3 p = cover.localPosition, s = cover.localScale;
                // Replace the rendered skin only; original BoxColliders and navigation stay intact.
                Mesh skin = BeveledBlock(s, .25f); owned.Add(skin);
                cover.GetComponent<MeshFilter>().sharedMesh = skin;
                if (boundary) continue;
                AddBox(concreteParts, cube, p + Vector3.up * (s.y * .5f - .025f), new Vector3(s.x + .1f, .18f, s.z + .1f));
                for (int side = -1; side <= 1; side += 2)
                {
                    AddBox(metalParts, cube, p + new Vector3(side * (s.x * .5f - .12f), 0, -s.z * .5f - .006f), new Vector3(.085f, s.y - .18f, .04f));
                    AddBox(metalParts, cube, p + new Vector3(side * (s.x * .5f - .12f), 0, s.z * .5f + .006f), new Vector3(.085f, s.y - .18f, .04f));
                }
                for (float y = -.55f; y < s.y * .5f - .16f; y += .55f)
                    AddBox(metalParts, cube, p + new Vector3(0, y, -s.z * .5f - .008f), new Vector3(s.x, .012f, .016f));
                for (int i = 0; i < 12; i++)
                {
                    float a = i * 2.39996f, r = .06f + Hash(i, (int)p.x) * .22f;
                    Vector3 edge = new Vector3(Mathf.Cos(a) * (s.x * .5f + .16f), .04f, Mathf.Sin(a) * (s.z * .5f + .16f));
                    AddPart(rubble, rock, new Vector3(p.x, 0, p.z) + edge, new Vector3(r * 2, .07f, r), Quaternion.Euler(0, i * 37, 0));
                }
            }
            float hx = size.x * .5f, hz = size.y * .5f;
            for (float z = -hz + 1; z < hz; z += 3.7f)
                foreach (int sign in new[] { -1, 1 })
                {
                    AddBox(concreteParts, cube, new Vector3(sign * (hx + .28f), 1.12f, z), new Vector3(.70f, 2.4f, .70f));
                    AddBox(concreteParts, cube, new Vector3(sign * hx, .14f, z), new Vector3(1.12f, .28f, 3.6f));
                }
            for (float x = -hx + 1; x < hx; x += 3.7f)
                foreach (int sign in new[] { -1, 1 })
                    AddBox(concreteParts, cube, new Vector3(x, 1.12f, sign * (hz + .28f)), new Vector3(.70f, 2.4f, .70f));

            // Architecture and standards frame the playable area without consuming deployment space.
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                {
                    AddRuinedTower(concreteParts, cube, new Vector3(side * (hx + 4.8f), 0, end * (hz + 3.8f)), side * end);
                    Vector3 p = new Vector3(side * (hx + 1.25f), 0, end * (hz - 2));
                    AddBox(bronzeParts, cube, p + Vector3.up * 2.9f, new Vector3(.075f, 5.8f, .075f));
                    AddBox(bronzeParts, cube, p + new Vector3(.7f, 5.15f, 0), new Vector3(1.4f, .055f, .055f));
                    AddBox(concreteParts, cube, p + Vector3.up * .16f, new Vector3(.6f, .32f, .6f));
                    AddBox(end < 0 ? blueCloth : redCloth, flagMesh, p + Vector3.up * 5.15f, Vector3.one);
                    AddPart(bronzeParts, cube, p + new Vector3(.7f, 4.36f, -.10f), new Vector3(.27f, .43f, .025f), Quaternion.Euler(0, 0, 45));
                }
            AddRuinedArch(concreteParts, cube, new Vector3(-5, 0, hz + 9));
            // Hashes deliberately do not advance the gameplay random-number generator.
            for (int i = 0; i < 130; i++)
            {
                float a = Hash(i, 8) * Mathf.PI * 2, radius = 1.05f + Hash(i, 21) * .55f;
                Vector3 p = new Vector3(Mathf.Cos(a) * (hx + 2) * radius, -.12f, Mathf.Sin(a) * (hz + 2) * radius);
                if (Mathf.Abs(p.x) < hx + .7f && Mathf.Abs(p.z) < hz + .7f) continue;
                float r = .16f + Mathf.Pow(Hash(i, 18), 3) * 2.2f;
                AddPart(rubble, rock, p, new Vector3(r * 1.4f, r * .72f, r), Quaternion.Euler(i * 7, i * 43, i * 11));
            }
            for (int i = 0; i < 620; i++)
            {
                float x = (Hash(i, 81) - .5f) * (size.x + 18), z = (Hash(i, 82) - .5f) * (size.y + 18);
                if (Mathf.Abs(x) < hx - 1.15f && Mathf.Abs(z) < hz - 1.15f) continue;
                bool inside = Mathf.Abs(x) < hx && Mathf.Abs(z) < hz;
                float r = .45f + Hash(i, 83) * .85f;
                AddPart(grass, tuft, new Vector3(x, inside ? .01f : -.22f, z), Vector3.one * r, Quaternion.Euler(0, i * 71, 0));
            }
            BuildBatch("Concrete caps and buttresses", arena, concreteParts, concrete);
            BuildBatch("Cover seams and edge guards", arena, metalParts, Tint(concrete, "Warm inset masonry seams", new Color(.72f, .65f, .49f)));
            BuildBatch("Stone fragments and outcrops", arena, rubble, rockMaterial);
            BuildBatch("Wind worn grasses", arena, grass, vegetation, false);
            BuildBatch("Bronze standards and fittings", arena, bronzeParts, bronze);
            Material blue = Cloth(concrete, "Blue toy standards", new Color(.16f, .57f, .76f));
            Material red = Cloth(concrete, "Red toy standards", new Color(.85f, .29f, .21f));
            BuildBatch("Blue regimental standards", arena, blueCloth, blue);
            BuildBatch("Red regimental standards", arena, redCloth, red);
            CreateWornMarkings(arena, size, cube, blue, red);
            CreateSurroundingGround(arena, size, earth);
            CreateRidge(arena, "Distant landscape", size, .92f, 7, Tint(earth, "Meadow green hills", new Color(.72f, .88f, .64f)), 0);
            CreateRidge(arena, "Far slate escarpment", size, 1.65f, 14, Tint(concrete, "Pastel sage hills", new Color(.58f, .76f, .67f)), 1.3f);
            CreateRidge(arena, "Mountains in haze", size, 2.55f, 20, Tint(concrete, "Pastel blue hills", new Color(.68f, .81f, .86f)), 3.2f);
        }

        void CreateSurroundingGround(Transform arena, Vector2 size, Material earth)
        {
            float hx = size.x * .5f, hz = size.y * .5f, extent = Mathf.Max(size.x, size.y) * 3;
            var verts = new List<Vector3>();
            var uv = new List<Vector2>();
            var indices = new List<int>();
            AddGroundRect(verts, uv, indices, -extent, -hx, -extent, extent, size);
            AddGroundRect(verts, uv, indices, hx, extent, -extent, extent, size);
            AddGroundRect(verts, uv, indices, -hx, hx, -extent, -hz, size);
            AddGroundRect(verts, uv, indices, -hx, hx, hz, extent, size);
            var mesh = new Mesh { name = "Surrounding ground mesh" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            owned.Add(mesh);
            var obj = new GameObject("Surrounding ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            obj.transform.SetParent(arena, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = earth;
            obj.GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        static void AddGroundRect(List<Vector3> vertices, List<Vector2> uv, List<int> indices,
            float x0, float x1, float z0, float z1, Vector2 arenaSize)
        {
            // Keep collision triangles reasonably sized as the surrounding terrain expands.
            int nx=Mathf.Max(1,Mathf.CeilToInt((x1-x0)/120)),nz=Mathf.Max(1,Mathf.CeilToInt((z1-z0)/120));
            for(int x=0;x<nx;x++) for(int z=0;z<nz;z++)
            {
                float a=Mathf.Lerp(x0,x1,x/(float)nx),b=Mathf.Lerp(x0,x1,(x+1f)/nx);
                float c=Mathf.Lerp(z0,z1,z/(float)nz),d=Mathf.Lerp(z0,z1,(z+1f)/nz);
                int start=vertices.Count;
                vertices.Add(new Vector3(a,-.28f,c)); vertices.Add(new Vector3(a,-.28f,d));
                vertices.Add(new Vector3(b,-.28f,d)); vertices.Add(new Vector3(b,-.28f,c));
                for(int i=start;i<start+4;i++) uv.Add(new Vector2(vertices[i].x/arenaSize.x,vertices[i].z/arenaSize.y));
                indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
        }

        Material Tint(Material source, string label, Color color)
        {
            var material = new Material(source) { name = label, color = color, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            owned.Add(material); return material;
        }

        Material Cloth(Material source, string label, Color color)
        {
            var material = Tint(source, label, color);
            material.mainTextureScale = Vector2.one * 3;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .02f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0);
            return material;
        }

        static void AddRuinedTower(List<CombineInstance> parts, Mesh block, Vector3 origin, int seed)
        {
            AddBox(parts, block, origin + Vector3.up * .25f, new Vector3(5.2f, .7f, 5.2f));
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                {
                    float h = 5.8f + Hash(side + seed, end) * 1.8f;
                    AddBox(parts, block, origin + new Vector3(side * 1.65f, h * .5f, end * 1.65f), new Vector3(.92f, h, .92f));
                    AddBox(parts, block, origin + new Vector3(side * 1.65f, 2.7f, end * 1.65f), new Vector3(1.16f, .28f, 1.16f));
                    AddBox(parts, block, origin + new Vector3(side * 1.65f, 5.1f, end * 1.65f), new Vector3(1.12f, .24f, 1.12f));
                }
            for (int side = -1; side <= 1; side += 2)
            {
                AddBox(parts, block, origin + new Vector3(side * 1.65f, 4.6f, 0), new Vector3(.85f, .65f, 3.8f));
                AddBox(parts, block, origin + new Vector3(0, 4.6f, side * 1.65f), new Vector3(3.8f, .65f, .85f));
                AddPart(parts, block, origin + new Vector3(side * 2.4f, .65f, .6f), new Vector3(1.1f, 1.4f, .85f), Quaternion.Euler(0, side * 23, side * 14));
            }
        }

        static void AddRuinedArch(List<CombineInstance> parts, Mesh block, Vector3 p)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                AddBox(parts, block, p + new Vector3(side * 3.2f, 2.1f, 0), new Vector3(1.1f, 4.2f, 1.5f));
                AddBox(parts, block, p + new Vector3(side * 3.2f, .3f, 0), new Vector3(1.6f, .6f, 2));
                AddBox(parts, block, p + new Vector3(side * 3.2f, 3.9f, 0), new Vector3(1.5f, .3f, 1.8f));
            }
            for (int i = 0; i < 11; i++)
            {
                float a = (i + .5f) / 11 * Mathf.PI;
                AddPart(parts, block, p + new Vector3(Mathf.Cos(a) * 3.2f, 4.1f + Mathf.Sin(a) * 2.3f, 0),
                    new Vector3(.95f, .85f, 1.5f), Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90));
            }
        }

        void CreateWornMarkings(Transform arena, Vector2 size, Mesh block, Material blue, Material red)
        {
            for (int team = 0; team < 2; team++)
            {
                var stripe = arena.Find(team == 0 ? "Blue deployment stripe" : "Red deployment stripe");
                if (stripe != null) stripe.GetComponent<Renderer>().enabled = false;
                var marks = new List<CombineInstance>();
                for (float x = -size.x * .5f + 1.5f; x < size.x * .5f - 1; x += 1.2f)
                    AddBox(marks, block, new Vector3(x, .013f, (team == 0 ? -1 : 1) * (size.y * .5f - 5)), new Vector3(.85f, .016f, .13f));
                BuildBatch(team == 0 ? "Blue worn deployment markings" : "Red worn deployment markings", arena, marks, team == 0 ? blue : red, false);
            }
        }

        void CreateRidge(Transform arena, string label, Vector2 size, float distance, float height, Material material, float phase)
        {
            // Solid pastel hills avoid distant texture shimmer in headset views.
            material.mainTexture = null;
            height*=atmosphereScale;
            const int segments = 112, rings = 5;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            float radius = Mathf.Max(size.x, size.y) * distance;
            for (int ring = 0; ring < rings; ring++)
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments;
                    float ridge = .56f + Mathf.Sin(a * 3 + phase) * .16f + Mathf.Sin(a * 7 - phase) * .13f + Mathf.Cos(a * 11 + phase) * .025f;
                    float profile = ring == 0 ? 0 : ring == 1 ? .24f : ring == 2 ? 1 : ring == 3 ? .66f : .10f;
                    float r = radius * (.84f + ring * .18f + Mathf.Sin(a * 9 + ring * 1.6f) * .018f);
                    Vector3 p = new Vector3(Mathf.Cos(a) * r, -.33f + ridge * height * profile, Mathf.Sin(a) * r);
                    vertices.Add(p); uv.Add(new Vector2(p.x, p.z) / 5f);
                    if (ring == 0 || i == 0) continue;
                    int v = vertices.Count - 1;
                    indices.AddRange(new[] { v, v - segments - 2, v - segments - 1, v, v - 1, v - segments - 2 });
                }
            var mesh = new Mesh { name = label + " mesh" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); owned.Add(mesh);
            var obj = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(arena, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh; obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            obj.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // Real bevel rings catch grazing light. UVs follow physical dimensions rather than stretching a face texture.
        static Mesh BeveledBlock(Vector3 dimensions, float bevel)
        {
            float bx = Mathf.Min(.22f, bevel / dimensions.x), by = Mathf.Min(.22f, bevel / dimensions.y), bz = Mathf.Min(.22f, bevel / dimensions.z);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); var rings = new Vector3[4][];
            for (int ring = 0; ring < 4; ring++)
            {
                bool cap = ring == 0 || ring == 3;
                float x = cap ? .5f - bx * .45f : .5f, z = cap ? .5f - bz * .45f : .5f;
                float y = ring == 0 ? -.5f : ring == 1 ? -.5f + by : ring == 2 ? .5f - by : .5f;
                rings[ring] = new[] { new Vector3(-x + bx, y, -z), new Vector3(x - bx, y, -z), new Vector3(x, y, -z + bz),
                    new Vector3(x, y, z - bz), new Vector3(x - bx, y, z), new Vector3(-x + bx, y, z), new Vector3(-x, y, z - bz), new Vector3(-x, y, -z + bz) };
            }
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < 8; i++)
                {
                    int next = (i + 1) % 8;
                    Vector3 a = rings[ring][i], b = rings[ring + 1][i], c = rings[ring + 1][next], d = rings[ring][next];
                    AddQuad(vertices, uv, triangles, a, b, c, d, new Vector2(Vector3.Scale(d - a, dimensions).magnitude / 2.8f, dimensions.y / 2.8f));
                }
            for (int cap = 0; cap < 2; cap++)
                for (int i = 0; i < 8; i++)
                {
                    int ring = cap == 0 ? 0 : 3, next = (i + 1) % 8, start = vertices.Count;
                    vertices.Add(new Vector3(0, cap == 0 ? -.5f : .5f, 0)); vertices.Add(rings[ring][cap == 0 ? i : next]); vertices.Add(rings[ring][cap == 0 ? next : i]);
                    for (int k = start; k < start + 3; k++) uv.Add(new Vector2(vertices[k].x * dimensions.x, vertices[k].z * dimensions.z) / 2.8f);
                    triangles.AddRange(new[] { start, start + 1, start + 2 });
                }
            return FinishMesh("Bevelled masonry", vertices, uv, triangles);
        }

        static Mesh RockMesh()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); const int sides = 9; var rings = new Vector3[4][];
            for (int r = 0; r < 4; r++)
            {
                rings[r] = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides + r * .04f, radius = (r == 0 ? .6f : r == 1 ? 1 : r == 2 ? .8f : .24f) * (.94f + Hash(i, r) * .08f);
                    rings[r][i] = new Vector3(Mathf.Cos(a) * radius, -.35f + r * .35f + Hash(i, r + 8) * .04f, Mathf.Sin(a) * radius);
                }
            }
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < sides; i++) AddQuad(vertices, uv, triangles, rings[r][i], rings[r + 1][i], rings[r + 1][(i + 1) % sides], rings[r][(i + 1) % sides], Vector2.one);
            for (int i = 0; i < sides; i++)
            {
                int start = vertices.Count; vertices.Add(new Vector3(0, .86f, 0)); vertices.Add(rings[3][(i + 1) % sides]); vertices.Add(rings[3][i]);
                for (int k = start; k < start + 3; k++)
                    uv.Add(new Vector2(vertices[k].x, vertices[k].z) * .5f + Vector2.one * .5f);
                triangles.AddRange(new[] { start, start + 1, start + 2 });
            }
            return FinishMesh("Rounded toy pebbles", vertices, uv, triangles);
        }

        static Mesh GrassMesh()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i < 7; i++)
            {
                float a = i * 2.39996f, h = .22f + Hash(i, 1) * .38f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .11f, lean = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * .10f;
                Vector3 width = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .025f;
                int start = vertices.Count; vertices.Add(p - width); vertices.Add(p + Vector3.up * h + lean); vertices.Add(p + width);
                uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.right); triangles.AddRange(new[] { start, start + 1, start + 2 });
            }
            return FinishMesh("Bent grass tuft", vertices, uv, triangles);
        }

        static Mesh BannerMesh()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>(); const int columns = 8, rows = 10;
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns, v = y / (float)rows, torn = y == rows ? Hash(x, 33) * .24f : 0;
                    vertices.Add(new Vector3(.12f + u * 1.18f, -v * 1.95f + torn, Mathf.Sin(u * 10 + v * 2) * (.025f + v * .10f) + v * v * .11f)); uv.Add(new Vector2(u, v));
                    if (x == 0 || y == 0) continue;
                    int k = vertices.Count - 1; indices.AddRange(new[] { k, k - columns - 1, k - columns - 2, k, k - columns - 2, k - 1 });
                }
            return FinishMesh("Folded torn regimental cloth", vertices, uv, indices);
        }

        static Mesh FinishMesh(string label, List<Vector3> vertices, List<Vector2> uv, List<int> indices)
        {
            var mesh = new Mesh { name = label }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }

        static void AddQuad(List<Vector3> vertices, List<Vector2> uv, List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 tiling)
        {
            int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uv.Add(Vector2.zero); uv.Add(new Vector2(0, tiling.y)); uv.Add(tiling); uv.Add(new Vector2(tiling.x, 0));
            indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        static void AddPart(List<CombineInstance> parts, Mesh mesh, Vector3 p, Vector3 s, Quaternion rotation) =>
            parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(p, rotation, s) });

        static void AddBox(List<CombineInstance> parts, Mesh cube, Vector3 p, Vector3 s) =>
            parts.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(p, Quaternion.identity, s) });

        void BuildBatch(string name, Transform parent, List<CombineInstance> parts, Material material, bool shadows = true)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            owned.Add(mesh);
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            obj.GetComponent<MeshRenderer>().shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }
    }
}
