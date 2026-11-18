using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattlefieldPrototype
{
    /// <summary>Shared sculpted meshes and articulated class silhouettes, independent of combat and collision.</summary>
    public sealed partial class BattleCharacterModel : MonoBehaviour
    {
        public Transform Body, LeftLeg, RightLeg, LeftArm, RightArm;
        public Transform LeftKnee, RightKnee, LeftElbow, RightElbow;
        public bool IsQuadruped { get; private set; }
        public bool IsSpirit { get; private set; }

        // Slots: regiment cloth, iron, hide, brass, eye slit, restoration, arcane.
        const int Cloth = 0, Iron = 1, Hide = 2, Brass = 3, Eye = 4, Life = 5, Arcane = 6;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static int liveModels;
        readonly Dictionary<Transform, List<Piece>> pieces = new Dictionary<Transform, List<Piece>>();
        UnitRole role;
        Material[] palette;
        bool ownsCacheReference;

        struct Piece
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public int Slot;
        }

        public static BattleCharacterModel Create(Transform parent, UnitRole role, float scale, Material[] palette)
        {
            var root = new GameObject(role + " • sculpted character");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;
            var model = root.AddComponent<BattleCharacterModel>();
            model.role = BattleFlyingClasses.ModelRole(role);
            model.palette = palette;
            model.ownsCacheReference = true;
            liveModels++;
            model.Build();
            if (BattleFlyingClasses.IsFlying(role))
            {
                int feathers = role == UnitRole.Crownwing ? 7 : role == UnitRole.Emberwing ? 6 : 5;
                int wingMaterial = role == UnitRole.Dawnwing ? Life : role == UnitRole.Emberwing ? Brass : role == UnitRole.Crownwing ? Iron : role == UnitRole.Skywing ? Cloth : Arcane;
                for (int side = -1; side <= 1; side += 2)
                {
                    model.Rod(model.Body, new Vector3(side * .2f, 1.3f, -.2f), new Vector3(side * 1.25f, 1.7f, -.45f), .07f, Brass);
                    for (int feather = 0; feather < feathers; feather++)
                        model.Part("blade", model.Body, new Vector3(side * (.45f + feather * .18f), 1.28f + feather * .07f, -.45f),
                            new Vector3(.23f, .75f - feather * .07f, .09f), wingMaterial, new Vector3(-25, 0, side * -35));
                }
            }
            model.Finish();
            return model;
        }

        Transform Joint(string label, Transform parent, Vector3 point)
        {
            var joint = new GameObject(label).transform;
            joint.SetParent(parent, false);
            joint.localPosition = point;
            return joint;
        }

        void Part(string mesh, Transform joint, Vector3 point, Vector3 size, int material, Vector3 angles = default)
        {
            if (!pieces.TryGetValue(joint, out var list)) pieces.Add(joint, list = new List<Piece>());
            list.Add(new Piece { Mesh = Geometry(mesh), Matrix = Matrix4x4.TRS(point, Quaternion.Euler(angles), size), Slot = material });
        }

        void Rod(Transform joint, Vector3 from, Vector3 to, float width, int material)
        {
            if (!pieces.TryGetValue(joint, out var list)) pieces.Add(joint, list = new List<Piece>());
            Vector3 direction = to - from;
            list.Add(new Piece { Mesh = Geometry("shaft"), Matrix = Matrix4x4.TRS((from + to) * .5f,
                Quaternion.FromToRotation(Vector3.up, direction), new Vector3(width, direction.magnitude, width)), Slot = material });
        }

        void Build()
        {
            Body = Joint("Articulated body", transform, Vector3.zero);
            IsSpirit = role == UnitRole.Wisp;
            IsQuadruped = role == UnitRole.Hound;
            if (IsSpirit) { BuildSpirit(); return; }
            if (IsQuadruped) { BuildHound(); return; }
            bool heavy = role == UnitRole.Vanguard || role == UnitRole.Brute || role == UnitRole.Sentinel ||
                role == UnitRole.Titan || role == UnitRole.Paladin || role==UnitRole.Halberdier || role==UnitRole.Berserker || role==UnitRole.Warden;
            bool robed = role == UnitRole.Stormcaller || role == UnitRole.Pyromancer || role == UnitRole.Medic || role==UnitRole.Frostbinder || role==UnitRole.Alchemist;
            bool hooded = role == UnitRole.Ranger || role == UnitRole.Assassin || role==UnitRole.Sharpshooter || role==UnitRole.Warden || robed;
            float breadth = role == UnitRole.Brute || role == UnitRole.Titan ? .72f : heavy ? .64f : .53f;

            Part("torso", Body, new Vector3(0, 1.13f, -.005f), new Vector3(breadth, .57f, .34f), Cloth);
            Part("torso", Body, new Vector3(0, 1.18f, .045f), new Vector3(breadth * .91f, .43f, .34f), heavy ? Iron : Hide);
            Part("bevel", Body, new Vector3(0, .87f, .005f), new Vector3(breadth * .76f, .09f, .35f), Hide);
            Part("bevel", Body, new Vector3(0, .87f, .192f), new Vector3(.105f, .085f, .025f), Brass);
            Part("sphere", Body, new Vector3(0, 1.44f, 0), new Vector3(.34f, .14f, .31f), Iron);
            // Split hanging cloth follows the waist taper instead of a rectangular torso.
            for (int side = -1; side <= 1; side += 2)
            {
                Part("cloth", Body, new Vector3(side * .112f, .73f, .13f), new Vector3(.23f, .30f, .075f), Cloth,
                    new Vector3(-7, 0, side * -6));
                Part("bevel", Body, new Vector3(side * breadth * .31f, .99f, .17f), new Vector3(.08f, .26f, .025f), Brass,
                    new Vector3(0, 0, side * -8));
            }
            // A narrow brow and neck keep the head in adult proportions.
            Part("sphere", Body, new Vector3(0, 1.63f, -.008f), new Vector3(.29f, .34f, .31f), hooded ? Hide : Iron);
            Part("bevel", Body, new Vector3(0, 1.55f, .108f), new Vector3(.235f, .19f, .12f), Iron);
            Part("bevel", Body, new Vector3(0, 1.654f, .151f), new Vector3(.228f, .027f, .028f), Eye);
            Part("blade", Body, new Vector3(0, 1.56f, .179f), new Vector3(.036f, .19f, .035f), Brass);
            if (hooded)
            {
                Part("sphere", Body, new Vector3(0, 1.65f, -.048f), new Vector3(.36f, .38f, .30f), Cloth);
                // Face opening sits in front of the hood, framed by the dark cowl.
                Part("bevel", Body, new Vector3(0, 1.595f, .134f), new Vector3(.23f, .21f, .07f), Hide);
                Part("bevel", Body, new Vector3(0, 1.65f, .177f), new Vector3(.175f, .021f, .015f), Eye);
            }
            else
            {
                Part("bevel", Body, new Vector3(0, 1.716f, .088f), new Vector3(.255f, .045f, .19f), Brass);
                Part("blade", Body, new Vector3(0, 1.78f, -.015f), new Vector3(.045f, .19f, .24f), Iron, new Vector3(85, 0, 0));
            }
            LeftLeg = Leg(-.145f, "Left", heavy);
            RightLeg = Leg(.145f, "Right", heavy);
            LeftArm = Arm(-breadth * .53f - .035f, "Left", heavy);
            RightArm = Arm(breadth * .53f + .035f, "Right", heavy);
            LeftKnee = LeftLeg.Find("Left knee"); RightKnee = RightLeg.Find("Right knee");
            LeftElbow = LeftArm.Find("Left elbow"); RightElbow = RightArm.Find("Right elbow");
            if (hooded || role == UnitRole.Paladin || role == UnitRole.Sentinel)
                Part("cape", Body, new Vector3(0, robed ? .93f : 1.06f, -.245f),
                    new Vector3(breadth * 1.17f, robed ? .99f : .70f, .15f), Cloth, new Vector3(-9, 0, 0));
            if (robed)
                Part("torso", Body, new Vector3(0, .60f, -.01f), new Vector3(.55f, .47f, .36f), Cloth, new Vector3(0, 0, 180));
            Equipment();
        }

        Transform Leg(float x, string side, bool heavy)
        {
            Transform leg = Joint(side + " leg", Body, new Vector3(x, .83f, 0));
            Part("muscle", leg, new Vector3(0, -.18f, 0), new Vector3(.22f, .39f, .25f), Hide);
            if (heavy) Part("muscle", leg, new Vector3(0, -.14f, .045f), new Vector3(.225f, .29f, .22f), Iron);
            Transform knee = Joint(side + " knee", leg, new Vector3(0, -.37f, 0));
            Part("sphere", knee, new Vector3(0, 0, .07f), new Vector3(.205f, .19f, .15f), Iron);
            Part("muscle", knee, new Vector3(0, -.155f, .005f), new Vector3(.17f, .31f, .19f), heavy ? Iron : Hide);
            Part("bevel", knee, new Vector3(0, -.37f, .062f), new Vector3(.215f, .16f, .34f), Hide);
            Part("bevel", knee, new Vector3(0, -.34f, .166f), new Vector3(.19f, .085f, .105f), Iron);
            return leg;
        }

        Transform Arm(float x, string side, bool heavy)
        {
            Transform arm = Joint(side + " arm", Body, new Vector3(x, 1.365f, 0));
            Part("muscle", arm, new Vector3(0, -.14f, 0), new Vector3(.18f, .30f, .21f), Cloth);
            Part("sphere", arm, new Vector3(0, -.035f, -.005f), new Vector3(heavy ? .34f : .245f, .21f, heavy ? .36f : .27f), Iron);
            if (heavy)
                Part("bevel", arm, new Vector3(0, -.08f, .025f), new Vector3(.30f, .07f, .32f), Brass);
            Transform elbow = Joint(side + " elbow", arm, new Vector3(0, -.29f, 0));
            Part("sphere", elbow, Vector3.zero, new Vector3(.165f, .16f, .18f), Hide);
            Part("muscle", elbow, new Vector3(0, -.12f, .035f), new Vector3(.18f, .25f, .21f), Iron);
            Part("bevel", elbow, new Vector3(0, -.16f, .13f), new Vector3(.12f, .13f, .035f), Brass);
            Part("sphere", elbow, new Vector3(0, -.265f, .045f), new Vector3(.145f, .16f, .18f), Hide);
            return arm;
        }

        void Equipment()
        {
            if((int)role>=(int)UnitRole.Halberdier) { SiegeEquipment(); return; }
            switch (role)
            {
                case UnitRole.Vanguard: Shield(false, false); Sword(RightElbow, .63f); break;
                case UnitRole.Sentinel: Shield(true, false); Sword(RightElbow, .79f); break;
                case UnitRole.Paladin:
                    Shield(true, true); Sword(RightElbow, .73f);
                    Part("bevel", Body, new Vector3(0, 1.2f, .233f), new Vector3(.065f, .27f, .025f), Brass);
                    Part("bevel", Body, new Vector3(0, 1.235f, .235f), new Vector3(.21f, .065f, .03f), Brass);
                    break;
                case UnitRole.Ranger:
                    Bow(RightElbow, false);
                    Part("muscle", Body, new Vector3(.19f, 1.17f, -.255f), new Vector3(.19f, .53f, .19f), Hide, new Vector3(0, 0, -18));
                    for (int i = 0; i < 3; i++)
                    {
                        Rod(Body, new Vector3(.11f + i * .05f, 1.32f, -.27f), new Vector3(.19f + i * .05f, 1.69f, -.27f), .012f, Brass);
                        Part("blade", Body, new Vector3(.19f + i * .05f, 1.62f, -.27f), new Vector3(.055f, .11f, .018f), Cloth);
                    }
                    break;
                case UnitRole.Medic:
                    Staff(LeftElbow, Life);
                    Part("bevel", Body, new Vector3(.23f, .94f, .08f), new Vector3(.22f, .26f, .26f), Hide);
                    Part("bevel", Body, new Vector3(0, 1.23f, .224f), new Vector3(.055f, .22f, .026f), Life);
                    Part("bevel", Body, new Vector3(0, 1.25f, .226f), new Vector3(.18f, .055f, .03f), Life);
                    break;
                case UnitRole.Skirmisher: Launcher(LeftElbow, false); Launcher(RightElbow, false); Pack(false); break;
                case UnitRole.Brute: Hammer(.59f, .35f); break;
                case UnitRole.Titan:
                    Hammer(.83f, .48f);
                    for (int side = -1; side <= 1; side += 2)
                        Part("crystal", Body, new Vector3(side * .4f, 1.54f, -.02f), new Vector3(.13f, .33f, .16f), Iron, new Vector3(0, 0, -side * 26));
                    break;
                case UnitRole.Engineer: Pack(true); Launcher(RightElbow, true); Staff(LeftElbow, Life, .67f); break;
                case UnitRole.Stormcaller: Staff(RightElbow, Arcane); break;
                case UnitRole.Pyromancer: Staff(RightElbow, Eye); break;
                case UnitRole.Militia: Bow(RightElbow, true); break;
                case UnitRole.Bombard:
                    Pack(true);
                    Part("shaft", RightElbow, new Vector3(0, -.16f, .31f), new Vector3(.23f, .59f, .23f), Iron, new Vector3(78, 0, 0));
                    Part("shaft", RightElbow, new Vector3(0, -.10f, .60f), new Vector3(.26f, .055f, .26f), Brass, new Vector3(78, 0, 0));
                    Part("shaft", RightElbow, new Vector3(0, -.095f, .63f), new Vector3(.19f, .012f, .19f), Hide, new Vector3(78, 0, 0));
                    break;
                case UnitRole.Assassin: Sword(LeftElbow, .36f); Sword(RightElbow, .36f); break;
            }
        }

        void Sword(Transform hand, float length)
        {
            Rod(hand, new Vector3(0, -.36f, .15f), new Vector3(0, -.18f, .15f), .035f, Hide);
            Part("bevel", hand, new Vector3(0, -.17f, .15f), new Vector3(.24f, .045f, .055f), Brass);
            Part("blade", hand, new Vector3(0, -.14f + length * .5f, .15f), new Vector3(.095f, length, .037f), Iron);
            Part("sphere", hand, new Vector3(0, -.37f, .15f), Vector3.one * .065f, Brass);
        }

        void Shield(bool tower, bool gilded)
        {
            Vector3 size = new Vector3(tower ? .56f : .48f, tower ? 1.02f : .78f, .10f);
            Part("shield", LeftElbow, new Vector3(-.04f, -.04f, .235f), size, Brass);
            Part("shield", LeftElbow, new Vector3(-.04f, -.035f, .255f), Vector3.Scale(size, new Vector3(.88f, .89f, 1)), Iron);
            Part("shield", LeftElbow, new Vector3(-.04f, -.02f, .311f), Vector3.Scale(size, new Vector3(.25f, .78f, .25f)), gilded ? Brass : Cloth);
            Part("sphere", LeftElbow, new Vector3(-.04f, .015f, .34f), new Vector3(.16f, .16f, .08f), Brass);
        }

        void Bow(Transform hand, bool crossbow)
        {
            Transform grip = hand;
            var center = new Vector3(0, -.23f, .18f);
            float length = crossbow ? .31f : .49f;
            Vector3 previous = center + new Vector3(0, -length, .06f);
            Vector3 bottom = previous;
            for (int i = 1; i <= 10; i++)
            {
                float t = i / 10f;
                Vector3 point = center + new Vector3(0, Mathf.Lerp(-length, length, t), -.19f * Mathf.Sin(t * Mathf.PI) + .06f);
                if (crossbow) { point = center + new Vector3(point.y - center.y, 0, point.z - center.z); }
                Vector3 from = previous;
                if (crossbow && i == 1) from = center + new Vector3(-length, 0, .06f);
                Rod(grip, from, point, .035f, Brass);
                previous = point;
            }
            if (crossbow)
            {
                bottom = center + new Vector3(-length, 0, .06f);
                Rod(grip, center + new Vector3(0, 0, -.22f), center + new Vector3(0, 0, .45f), .075f, Hide);
            }
            Rod(grip, bottom, previous, .007f, Hide);
            Rod(grip, center + new Vector3(0, 0, -.18f), center + new Vector3(0, 0, .49f), .012f, Iron);
            Part("blade", grip, center + new Vector3(0, 0, .49f), new Vector3(.04f, .10f, .018f), Iron, new Vector3(90, 0, 0));
        }

        void Launcher(Transform hand, bool rivetTool)
        {
            float width = rivetTool ? .22f : .135f;
            float length = rivetTool ? .53f : .39f;
            Part("bevel", hand, new Vector3(0, -.19f, .18f), new Vector3(width, .15f, .26f), Iron);
            Part("bevel", hand, new Vector3(0, -.27f, .11f), new Vector3(width * .65f, .19f, .11f), Hide, new Vector3(-12, 0, 0));
            Rod(hand, new Vector3(0, -.16f, .22f), new Vector3(0, -.16f, .22f + length), width * .53f, Iron);
            Part("shaft", hand, new Vector3(0, -.16f, .22f + length), new Vector3(width * .69f, .05f, width * .69f), Brass, new Vector3(90, 0, 0));
            Part("shaft", hand, new Vector3(0, -.16f, .25f + length), new Vector3(width * .42f, .012f, width * .42f), Hide, new Vector3(90, 0, 0));
            Part("bevel", hand, new Vector3(0, -.095f, .25f), new Vector3(width * .8f, .04f, .17f), Brass);
            if (rivetTool)
            {
                Part("shaft", hand, new Vector3(-.13f, -.19f, .23f), new Vector3(.18f, .10f, .18f), Brass, new Vector3(0, 0, 90));
                Part("bevel", hand, new Vector3(.13f, -.17f, .19f), new Vector3(.06f, .14f, .14f), Life);
            }
        }

        void Hammer(float width, float height)
        {
            Rod(RightElbow, new Vector3(0, -.75f, .15f), new Vector3(0, .57f, .15f), .055f, Hide);
            Part("bevel", RightElbow, new Vector3(0, .48f, .15f), new Vector3(width, height, .31f), Iron);
            for (int side = -1; side <= 1; side += 2)
                Part("bevel", RightElbow, new Vector3(side * width * .44f, .48f, .15f), new Vector3(.075f, height * 1.12f, .35f), Brass);
        }

        void Staff(Transform hand, int focus, float length = 1)
        {
            Rod(hand, new Vector3(0, -.79f * length, .15f), new Vector3(0, .73f * length, .15f), .035f, Brass);
            for (int side = -1; side <= 1; side += 2)
                Rod(hand, new Vector3(0, .57f * length, .15f), new Vector3(side * .12f, .80f * length, .15f), .025f, Iron);
            Part("crystal", hand, new Vector3(0, .78f * length, .15f), new Vector3(.17f, .29f * length, .17f), focus);
        }

        void Pack(bool tools)
        {
            Part("bevel", Body, new Vector3(0, 1.13f, -.24f), new Vector3(.40f, .43f, .24f), Hide);
            for (int side = -1; side <= 1; side += 2)
            {
                Part("shaft", Body, new Vector3(side * .22f, 1.15f, -.26f), new Vector3(.12f, .40f, .12f), tools ? Brass : Iron);
                Part("bevel", Body, new Vector3(side * .14f, 1.17f, -.37f), new Vector3(.045f, .44f, .035f), Brass);
            }
        }

        void BuildSpirit()
        {
            Part("crystal", Body, new Vector3(0, 1.26f, 0), new Vector3(.40f, .72f, .40f), Arcane);
            Part("crystal", Body, new Vector3(0, 1.3f, .02f), new Vector3(.24f, .48f, .24f), Life);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f;
                Part("crystal", Body, new Vector3(Mathf.Cos(angle) * .40f, 1.27f + Mathf.Sin(angle) * .40f, 0),
                    new Vector3(.055f, .20f, .055f), Brass, new Vector3(0, 0, i * 45 - 90));
            }
        }

        void BuildHound()
        {
            Part("muscle", Body, new Vector3(0, .66f, -.04f), new Vector3(.47f, 1.0f, .46f), Hide, new Vector3(90, 0, 0));
            Part("torso", Body, new Vector3(0, .79f, .22f), new Vector3(.49f, .51f, .40f), Cloth, new Vector3(25, 0, 0));
            Part("sphere", Body, new Vector3(0, .97f, .49f), new Vector3(.35f, .37f, .42f), Iron);
            Part("muscle", Body, new Vector3(0, .88f, .73f), new Vector3(.23f, .35f, .21f), Hide, new Vector3(90, 0, 0));
            for (int side = -1; side <= 1; side += 2)
            {
                Part("crystal", Body, new Vector3(side * .12f, 1.18f, .43f), new Vector3(.12f, .29f, .14f), Hide, new Vector3(-12, 0, side * -15));
                Part("bevel", Body, new Vector3(side * .157f, 1.0f, .63f), new Vector3(.025f, .035f, .09f), Eye);
                Part("blade", Body, new Vector3(side * .08f, .79f, .79f), new Vector3(.035f, .12f, .035f), Brass, new Vector3(0, 0, 180));
            }
            LeftLeg = HoundLeg(-.20f, -.36f, "Left leg"); RightLeg = HoundLeg(.20f, -.36f, "Right leg");
            LeftArm = HoundLeg(-.22f, .34f, "Left arm"); RightArm = HoundLeg(.22f, .34f, "Right arm");
            Rod(Body, new Vector3(0, .74f, -.47f), new Vector3(0, .89f, -.85f), .075f, Hide);
            Part("crystal", Body, new Vector3(0, .89f, -.88f), new Vector3(.10f, .25f, .11f), Hide, new Vector3(60, 0, 0));
        }

        Transform HoundLeg(float x, float z, string label)
        {
            Transform leg = Joint(label, Body, new Vector3(x, .61f, z));
            Part("muscle", leg, new Vector3(0, -.14f, -.04f), new Vector3(.18f, .33f, .22f), Hide, new Vector3(15, 0, 0));
            Part("muscle", leg, new Vector3(0, -.37f, .015f), new Vector3(.12f, .29f, .14f), Iron, new Vector3(-12, 0, 0));
            Part("bevel", leg, new Vector3(0, -.54f, .075f), new Vector3(.17f, .10f, .25f), Hide);
            return leg;
        }

        void Finish()
        {
            // Bake decorations by joint and palette slot once per class, then instance those meshes.
            // Weapons remain attached to the articulated hand; no decorative object gets a collider.
            foreach (var pair in pieces)
                for (int slot = 0; slot < palette.Length; slot++)
                {
                    var instances = new List<CombineInstance>();
                    foreach (Piece piece in pair.Value)
                        if (piece.Slot == slot) instances.Add(new CombineInstance { mesh = piece.Mesh, transform = piece.Matrix });
                    if (instances.Count == 0) continue;
                    string key = role + "/" + pair.Key.name + "/" + slot;
                    if (!meshes.TryGetValue(key, out Mesh mesh) || mesh == null)
                    {
                        mesh = new Mesh { name = key, hideFlags = HideFlags.DontSave };
                        mesh.CombineMeshes(instances.ToArray(), true, true);
                        mesh.RecalculateBounds();
                        meshes[key] = mesh;
                    }
                    var part = new GameObject("Sculpted " + palette[slot].name, typeof(MeshFilter), typeof(MeshRenderer));
                    part.transform.SetParent(pair.Key, false);
                    part.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = part.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = palette[slot];
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
            pieces.Clear();
            palette = null;
        }

        static Mesh Geometry(string kind)
        {
            if (meshes.TryGetValue(kind, out Mesh cached) && cached != null) return cached;
            Mesh mesh;
            switch (kind)
            {
                case "shield": mesh = ShieldMesh(); break;
                case "cape": mesh = CapeMesh(); break;
                case "bevel": mesh = Lathe(new[] { new Vector2(-.5f, .82f), new Vector2(-.39f, 1), new Vector2(.39f, 1), new Vector2(.5f, .82f) }, 8, true); break;
                case "torso": mesh = Lathe(new[] { new Vector2(-.5f, .65f), new Vector2(-.28f, .77f), new Vector2(.20f, 1), new Vector2(.40f, .96f), new Vector2(.5f, .70f) }, 12); break;
                case "muscle": mesh = Lathe(new[] { new Vector2(-.5f, .55f), new Vector2(-.35f, .78f), new Vector2(.08f, 1), new Vector2(.35f, .91f), new Vector2(.5f, .59f) }, 10); break;
                case "blade": mesh = Lathe(new[] { new Vector2(-.5f, .85f), new Vector2(-.43f, 1), new Vector2(.28f, .79f), new Vector2(.5f, 0) }, 4); break;
                case "crystal": mesh = Lathe(new[] { new Vector2(-.5f, 0), new Vector2(-.26f, .8f), new Vector2(.16f, 1), new Vector2(.5f, 0) }, 6); break;
                case "shaft": mesh = Lathe(new[] { new Vector2(-.5f, 1), new Vector2(.5f, 1) }, 8); break;
                case "cloth": mesh = Lathe(new[] { new Vector2(-.5f, .75f), new Vector2(-.36f, 1), new Vector2(.5f, .86f) }, 8, true); break;
                default:
                    var profile = new Vector2[9];
                    for (int i = 0; i < profile.Length; i++)
                    {
                        float a = Mathf.PI * i / (profile.Length - 1);
                        profile[i] = new Vector2(-Mathf.Cos(a) * .5f, Mathf.Sin(a));
                    }
                    mesh = Lathe(profile, 12); break;
            }
            mesh.name = "Character " + kind;
            mesh.hideFlags = HideFlags.DontSave;
            meshes[kind] = mesh;
            return mesh;
        }

        static Mesh Lathe(Vector2[] rings, int sides, bool chamfered = false)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uv = new List<Vector2>();
            for (int r = 0; r < rings.Length; r++)
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2 / sides;
                    Vector2 circle = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    if (chamfered)
                    {
                        // An octagonal box with broad faces and clipped corners.
                        Vector2[] corners = { new Vector2(1,.68f), new Vector2(.68f,1), new Vector2(-.68f,1), new Vector2(-1,.68f),
                            new Vector2(-1,-.68f), new Vector2(-.68f,-1), new Vector2(.68f,-1), new Vector2(1,-.68f) };
                        circle = corners[s];
                    }
                    vertices.Add(new Vector3(circle.x * rings[r].y * .5f, rings[r].x, circle.y * rings[r].y * .5f));
                    uv.Add(new Vector2(s / (float)sides, rings[r].x + .5f));
                    if (r == 0) continue;
                    int a0 = (r - 1) * sides + s, a1 = (r - 1) * sides + (s + 1) % sides;
                    int b0 = r * sides + s, b1 = r * sides + (s + 1) % sides;
                    triangles.Add(a0); triangles.Add(b0); triangles.Add(a1);
                    triangles.Add(a1); triangles.Add(b0); triangles.Add(b1);
                }
            for (int cap = 0; cap < 2; cap++)
            {
                int ring = cap == 0 ? 0 : rings.Length - 1;
                int center = vertices.Count;
                vertices.Add(new Vector3(0, rings[ring].x, 0)); uv.Add(new Vector2(.5f, .5f));
                for (int s = 0; s < sides; s++)
                {
                    triangles.Add(center);
                    triangles.Add(ring * sides + (cap == 0 ? s : (s + 1) % sides));
                    triangles.Add(ring * sides + (cap == 0 ? (s + 1) % sides : s));
                }
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh ShieldMesh()
        {
            Vector2[] outline = { new Vector2(-.42f,.5f), new Vector2(.42f,.5f), new Vector2(.5f,.27f),
                new Vector2(.39f,-.23f), new Vector2(0,-.5f), new Vector2(-.39f,-.23f), new Vector2(-.5f,.27f) };
            var v = new List<Vector3> { new Vector3(0, .06f, .5f), new Vector3(0, 0, -.5f) };
            foreach (var p in outline) { v.Add(new Vector3(p.x, p.y, .16f)); v.Add(new Vector3(p.x, p.y, -.5f)); }
            var t = new List<int>();
            for (int i = 0; i < outline.Length; i++)
            {
                int a = 2 + i * 2, b = 2 + (i + 1) % outline.Length * 2;
                t.AddRange(new[] { 0, b, a, 1, a + 1, b + 1, a, b, a + 1, b, b + 1, a + 1 });
            }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh CapeMesh()
        {
            var v = new List<Vector3>(); var t = new List<int>();
            const int columns = 8;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col <= columns; col++)
                {
                    float x = col / (float)columns;
                    v.Add(new Vector3((x - .5f) * (row == 0 ? .72f : 1), .5f - row * .5f +
                        (row == 2 ? Mathf.Cos(x * Mathf.PI * 4) * .035f : 0), Mathf.Cos(x * Mathf.PI * 8) * .21f - row * .16f));
                    if (row == 0 || col == 0) continue;
                    int a = (row - 1) * (columns + 1) + col - 1, b = a + columns + 1;
                    t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            int frontCount = v.Count;
            v.AddRange(v.ToArray());
            int triangleCount = t.Count;
            for (int i = 0; i < triangleCount; i += 3)
                t.AddRange(new[] { t[i + 2] + frontCount, t[i + 1] + frontCount, t[i] + frontCount });
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        void OnDestroy()
        {
            if (!ownsCacheReference) return;
            ownsCacheReference = false;
            if (--liveModels > 0) return;
            foreach (Mesh mesh in meshes.Values)
            {
                if (mesh == null) continue;
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            }
            meshes.Clear();
        }
    }
}
