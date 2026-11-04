using System.Collections.Generic;
using UnityEngine;

namespace HauntedItems
{
    public static class ItemMaterials
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static MaterialPropertyBlock block;

        public static void ClearCache() { cache.Clear(); }

        public static Shader Lit()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("HDRP/Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        public static Material Get(string name, Color c, float metallic = 0f, float smooth = 0.3f, Color? emission = null)
        {
            if (cache.TryGetValue(name, out var cached) && cached != null) return cached;
            var m = new Material(Lit()) { name = "HI_" + name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission.Value);
            }
            cache[name] = m;
            return m;
        }

        /// Pang-toggle ng glow (ammo lights, trap light) nang hindi gumagawa ng material instances.
        public static void SetEmission(Renderer r, Color c)
        {
            if (r == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, c);
            r.SetPropertyBlock(block);
        }
    }

    public static class ItemModelFactory
    {
        // ------------------------------------------------ helpers
        static void Kill(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }

        static GameObject Empty(Transform parent, string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            return g;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (col != null) Kill(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        // cylinder na nakahiga sa Z axis (diameter, length)
        static GameObject CylZ(Transform p, string n, Vector3 c, float dia, float len, Material m) =>
            Prim(PrimitiveType.Cylinder, n, p, c, new Vector3(dia, len * 0.5f, dia), Quaternion.Euler(90, 0, 0), m);

        // cylinder na nakatayo sa Y axis (diameter, length)
        static GameObject CylY(Transform p, string n, Vector3 c, float dia, float len, Material m) =>
            Prim(PrimitiveType.Cylinder, n, p, c, new Vector3(dia, len * 0.5f, dia), Quaternion.identity, m);

        static GameObject Ball(Transform p, string n, Vector3 c, float dia, Material m) =>
            Prim(PrimitiveType.Sphere, n, p, c, new Vector3(dia, dia, dia), Quaternion.identity, m);

        static GameObject Block(Transform p, string n, Vector3 c, Vector3 size, Material m) =>
            Prim(PrimitiveType.Cube, n, p, c, size, Quaternion.identity, m);

        // cylinder mula point A papuntang point B
        static GameObject Stick(Transform p, string n, Vector3 a, Vector3 b, float dia, Material m)
        {
            Vector3 d = b - a;
            return Prim(PrimitiveType.Cylinder, n, p, (a + b) * 0.5f, new Vector3(dia, d.magnitude * 0.5f, dia),
                Quaternion.FromToRotation(Vector3.up, d), m);
        }

        // patag na piraso na nakahilig mula A papuntang B (width = lapad, thick = kapal)
        static GameObject Strip(Transform p, string n, Vector3 a, Vector3 b, float width, float thick, Material m)
        {
            Vector3 d = b - a;
            var g = Prim(PrimitiveType.Cube, n, p, (a + b) * 0.5f, new Vector3(width, thick, d.magnitude), Quaternion.identity, m);
            g.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            return g;
        }

        static Mesh coneMesh;
        /// Unit cone: base radius 0.5 sa y=0, apex sa y=1
        public static Mesh ConeMesh()
        {
            if (coneMesh != null) return coneMesh;
            const int seg = 16;
            var v = new List<Vector3>(); var t = new List<int>();
            Vector3 apex = new Vector3(0, 1, 0);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * .5f, 0, Mathf.Sin(a0) * .5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * .5f, 0, Mathf.Sin(a1) * .5f);
                int s = v.Count;
                v.Add(p0); v.Add(apex); v.Add(p1); t.Add(s); t.Add(s + 1); t.Add(s + 2);
                s = v.Count;
                v.Add(Vector3.zero); v.Add(p0); v.Add(p1); t.Add(s); t.Add(s + 1); t.Add(s + 2);
            }
            coneMesh = new Mesh { name = "HI_Cone" };
            coneMesh.SetVertices(v); coneMesh.SetTriangles(t, 0);
            coneMesh.RecalculateNormals(); coneMesh.RecalculateBounds();
            return coneMesh;
        }

        static GameObject Cone(Transform p, string n, Vector3 baseCenter, Vector3 dir, float baseDia, float len, Material m)
        {
            var g = new GameObject(n);
            g.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            g.transform.SetParent(p, false);
            g.transform.localPosition = baseCenter;
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            g.transform.localScale = new Vector3(baseDia, len, baseDia);
            return g;
        }

        public static Bounds GetBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static void AddTriggerBox(GameObject root)
        {
            var b = GetBounds(root);
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = root.transform.InverseTransformPoint(b.center);
            col.size = b.size + Vector3.one * 0.06f;
        }

        static void AddSolidBox(GameObject root)
        {
            var b = GetBounds(root);
            var col = root.AddComponent<BoxCollider>();
            col.center = root.transform.InverseTransformPoint(b.center);
            col.size = b.size;
        }

        // ------------------------------------------------ DART
        static GameObject DartModel(Transform parent, string name, Vector3 pos, float scale)
        {
            var root = Empty(parent, name, pos);
            root.transform.localScale = Vector3.one * scale;
            var t = root.transform;
            var glass = ItemMaterials.Get("DartGlass", new Color(.6f, .8f, .85f), 0f, .9f);
            var liquid = ItemMaterials.Get("Sedative", new Color(.1f, .6f, .25f), 0f, .8f, new Color(.04f, .45f, .12f));
            var red = ItemMaterials.Get("DartRed", new Color(.65f, .05f, .05f), 0f, .4f);
            var brass = ItemMaterials.Get("Brass", new Color(.55f, .4f, .15f), .85f, .5f);
            var steel = ItemMaterials.Get("DarkSteel", new Color(.12f, .12f, .13f), .9f, .45f);

            CylZ(t, "TailCap", new Vector3(0, 0, .004f), .014f, .008f, red);
            CylZ(t, "SyringeBody", new Vector3(0, 0, .045f), .012f, .07f, glass);
            CylZ(t, "Sedative", new Vector3(0, 0, .045f), .008f, .056f, liquid);
            CylZ(t, "Collar", new Vector3(0, 0, .083f), .016f, .008f, brass);
            Cone(t, "Needle", new Vector3(0, 0, .087f), Vector3.forward, .006f, .04f, steel);
            for (int i = 0; i < 3; i++)
            {
                var pivot = Empty(t, "Fin" + i, Vector3.zero);
                pivot.transform.localRotation = Quaternion.Euler(0, 0, i * 120f);
                Block(pivot.transform, "FinBlade", new Vector3(0, .011f, .002f), new Vector3(.002f, .02f, .03f), red);
            }
            return root;
        }

        public static GameObject BuildDart()
        {
            var root = new GameObject("TranquilizerDart");
            DartModel(root.transform, "Model", Vector3.zero, 1.5f);
            root.AddComponent<TranquilizerDart>();
            return root;
        }

        public static GameObject BuildDartPack()
        {
            var root = new GameObject("TranquilizerDartPack");
            var t = root.transform;
            var tray = ItemMaterials.Get("DartTray", new Color(.08f, .1f, .07f), .1f, .3f);
            var cloth = ItemMaterials.Get("DartStrap", new Color(.45f, .06f, .05f), 0f, .1f);
            Block(t, "Tray", new Vector3(0, .008f, 0), new Vector3(.15f, .016f, .27f), tray);
            Block(t, "LipL", new Vector3(-.07f, .02f, 0), new Vector3(.012f, .02f, .27f), tray);
            Block(t, "LipR", new Vector3(.07f, .02f, 0), new Vector3(.012f, .02f, .27f), tray);
            for (int i = 0; i < 3; i++)
                DartModel(t, "Dart" + i, new Vector3(-.04f + i * .04f, .026f, -.12f), 2f);
            Block(t, "Strap", new Vector3(0, .039f, 0f), new Vector3(.15f, .008f, .03f), cloth);

            var p = root.AddComponent<PickupItem>();
            p.kind = ItemKind.DartPack; p.amount = 3; p.displayName = "Tranquilizer Darts";
            AddTriggerBox(root);
            return root;
        }

        // ------------------------------------------------ GUN
        public static GameObject BuildGun()
        {
            var root = new GameObject("TranquilizerGun");
            var t = root.transform;
            var steel = ItemMaterials.Get("DarkSteel", new Color(.12f, .12f, .13f), .9f, .45f);
            var olive = ItemMaterials.Get("OliveBody", new Color(.17f, .2f, .12f), .2f, .35f);
            var wood = ItemMaterials.Get("OldWood", new Color(.22f, .12f, .06f), 0f, .2f);
            var brass = ItemMaterials.Get("Brass", new Color(.55f, .4f, .15f), .85f, .5f);
            var rubber = ItemMaterials.Get("Rubber", new Color(.04f, .04f, .04f), 0f, .15f);
            var dark = ItemMaterials.Get("DarkPlastic", new Color(.07f, .07f, .08f), .1f, .35f);
            var red = ItemMaterials.Get("DartRed", new Color(.65f, .05f, .05f), 0f, .4f);
            var lens = ItemMaterials.Get("ScopeLens", new Color(.1f, .2f, .3f), .1f, .95f, new Color(0, .12f, .25f));
            var ammoMat = ItemMaterials.Get("AmmoLight", new Color(.4f, .02f, .02f), 0f, .6f, Color.black);

            // barrel + body
            CylZ(t, "Barrel", new Vector3(0, .065f, .24f), .04f, .36f, steel);
            CylZ(t, "BarrelShroud", new Vector3(0, .065f, .15f), .058f, .22f, olive);
            CylZ(t, "MuzzleRing", new Vector3(0, .065f, .425f), .056f, .03f, brass);
            CylZ(t, "Receiver", new Vector3(0, .05f, -.03f), .088f, .30f, olive);
            Ball(t, "RearCap", new Vector3(0, .05f, -.18f), .088f, olive);
            CylZ(t, "Band", new Vector3(0, .05f, .095f), .095f, .012f, brass);

            // grip + guard + trigger
            Prim(PrimitiveType.Cylinder, "Grip", t, new Vector3(0, -.035f, -.06f), new Vector3(.046f, .08f, .046f), Quaternion.Euler(15, 0, 0), wood);
            Ball(t, "GripCap", new Vector3(0, -.112f, -.081f), .05f, steel);
            Stick(t, "GuardA", new Vector3(0, .012f, .07f), new Vector3(0, -.045f, .05f), .008f, steel);
            Stick(t, "GuardB", new Vector3(0, -.045f, .05f), new Vector3(0, -.05f, -.02f), .008f, steel);
            Stick(t, "GuardC", new Vector3(0, -.05f, -.02f), new Vector3(0, .005f, -.025f), .008f, steel);
            Stick(t, "Trigger", new Vector3(0, .008f, .012f), new Vector3(0, -.03f, .022f), .01f, steel);

            // stock
            Stick(t, "StockTube", new Vector3(0, .04f, -.19f), new Vector3(0, 0f, -.36f), .05f, wood);
            Ball(t, "StockPad", new Vector3(0, -.002f, -.365f), .06f, rubber);

            // scope
            CylZ(t, "ScopeTube", new Vector3(0, .128f, .03f), .03f, .2f, steel);
            CylZ(t, "ScopeBell", new Vector3(0, .128f, .145f), .045f, .05f, steel);
            Prim(PrimitiveType.Sphere, "FrontLens", t, new Vector3(0, .128f, .171f), new Vector3(.04f, .04f, .006f), Quaternion.identity, lens);
            CylZ(t, "Eyepiece", new Vector3(0, .128f, -.085f), .04f, .04f, steel);
            Prim(PrimitiveType.Sphere, "RearLens", t, new Vector3(0, .128f, -.106f), new Vector3(.032f, .032f, .006f), Quaternion.identity, lens);
            Block(t, "ScopeMount0", new Vector3(0, .102f, -.02f), new Vector3(.018f, .04f, .02f), steel);
            Block(t, "ScopeMount1", new Vector3(0, .102f, .09f), new Vector3(.018f, .04f, .02f), steel);

            // ammo lights
            Block(t, "AmmoPanel", new Vector3(.046f, .058f, -.06f), new Vector3(.008f, .03f, .14f), dark);
            var lights = new Renderer[3];
            for (int i = 0; i < 3; i++)
                lights[i] = Ball(t, "AmmoLight" + i, new Vector3(.052f, .058f, -.11f + i * .05f), .018f, ammoMat).GetComponent<Renderer>();

            // bolt
            var bolt = Empty(t, "Bolt", Vector3.zero);
            CylZ(bolt.transform, "BoltRod", new Vector3(.05f, .082f, 0f), .012f, .06f, steel);
            Ball(bolt.transform, "BoltKnob", new Vector3(.05f, .082f, -.045f), .026f, brass);

            // magazine
            var mag = Empty(t, "Magazine", new Vector3(0, 0f, .11f));
            Prim(PrimitiveType.Cylinder, "MagBody", mag.transform, new Vector3(0, -.045f, 0), new Vector3(.04f, .045f, .04f), Quaternion.identity, dark);
            Prim(PrimitiveType.Cylinder, "MagBand", mag.transform, new Vector3(0, -.02f, 0), new Vector3(.044f, .008f, .044f), Quaternion.identity, red);
            Ball(mag.transform, "MagBase", new Vector3(0, -.09f, 0), .04f, dark);

            // dart sa chamber + muzzle point
            var chamber = DartModel(t, "ChamberDart", new Vector3(0, .065f, .40f), 1f);
            var muzzle = Empty(t, "Muzzle", new Vector3(0, .065f, .53f));

            var gun = root.AddComponent<TranquilizerGun>();
            gun.muzzle = muzzle.transform; gun.magazine = mag.transform; gun.bolt = bolt.transform;
            gun.chamberDart = chamber.transform; gun.ammoLights = lights;
            gun.enabled = false; // naka-enable lang kapag hawak na ng player

            var p = root.AddComponent<PickupItem>();
            p.kind = ItemKind.TranquilizerGun; p.displayName = "Tranquilizer Gun";
            AddTriggerBox(root);
            return root;
        }

        // ------------------------------------------------ TRAP (bear trap)
        // Nakabukas na trap: parihabang base, dalawang jaw na nakahiga palabas sa magkabilang gilid
        // (hinge sa gilid ng base, arko papalayo), leaf spring sa ibabaw, pan sa gitna, at kadena.
        // side = +1 kanan, -1 kaliwa. Naka-mirror ang jaw para magkabilang gilid talaga.
        static Vector3 ArcPt(float rad, int k, int n, float side)
        {
            float phi = Mathf.Lerp(-90f, 90f, k / (float)n) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(phi) * rad * side, 0, Mathf.Sin(phi) * rad);
        }

        static void BuildJaw(Transform parent, float r, float side, Material rust, Material dark, Material steel)
        {
            const int n = 12;
            const float inner = .72f;

            for (int k = 0; k < n; k++)
            {
                Stick(parent, "JawOuter" + k, ArcPt(r, k, n, side), ArcPt(r, k + 1, n, side), .03f, rust);
                Stick(parent, "JawInner" + k, ArcPt(r * inner, k, n, side), ArcPt(r * inner, k + 1, n, side), .02f, dark);
            }

            // tulay sa pagitan ng panlabas at panloob na arko
            for (int k = 1; k < n; k += 2)
                Stick(parent, "Rung" + k, ArcPt(r, k, n, side), ArcPt(r * inner, k, n, side), .014f, dark);

            // ngipin: nasa panloob na arko, nakaturo sa gitna at bahagyang pataas
            const int teeth = 9;
            for (int i = 0; i < teeth; i++)
            {
                float phi = Mathf.Lerp(-72f, 72f, i / (float)(teeth - 1)) * Mathf.Deg2Rad;
                Vector3 b = new Vector3(Mathf.Cos(phi) * side, 0, Mathf.Sin(phi)) * (r * inner);
                Vector3 dir = -b.normalized + Vector3.up * .45f;
                Cone(parent, "Tooth" + i, b, dir, .036f, .085f, steel);
            }
        }

        // leaf spring: kurbadong patag na bakal sa ibabaw ng base
        static void BuildLeafSpring(Transform parent, string name, float x, float halfLen, float height, Material m)
        {
            const int seg = 8;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= seg; i++)
            {
                float u = i / (float)seg;
                Vector3 pt = new Vector3(x, .035f + Mathf.Sin(u * Mathf.PI) * height, Mathf.Lerp(-halfLen, halfLen, u));
                if (i > 0) Strip(parent, name + i, prev, pt, .045f, .008f, m);
                prev = pt;
            }
            Block(parent, name + "MountA", new Vector3(x, .03f, -halfLen), new Vector3(.06f, .02f, .035f), m);
            Block(parent, name + "MountB", new Vector3(x, .03f, halfLen), new Vector3(.06f, .02f, .035f), m);
        }

        public static GameObject BuildTrap()
        {
            var root = new GameObject("BearTrap");
            var t = root.transform;
            var rust = ItemMaterials.Get("TrapRust", new Color(.36f, .17f, .08f), .7f, .2f);
            var dark = ItemMaterials.Get("TrapDarkIron", new Color(.12f, .1f, .09f), .8f, .3f);
            var steel = ItemMaterials.Get("ChainSteel", new Color(.3f, .3f, .32f), .9f, .35f);
            var blood = ItemMaterials.Get("DriedBlood", new Color(.2f, .02f, .02f), 0f, .7f);
            var lightMat = ItemMaterials.Get("TrapLight", new Color(.5f, .02f, .02f), 0f, .6f, Color.black);

            const float hx = .15f;       // distansya ng hinge mula sa gitna
            const float halfLen = .28f;  // kalahating haba ng base (at radius ng jaw)

            // base: parihaba, hindi bilog
            Block(t, "BasePlate", new Vector3(0, .012f, 0), new Vector3(hx * 2f, .024f, halfLen * 2f), dark);
            Block(t, "EndPlateF", new Vector3(0, .02f, halfLen), new Vector3(hx * 2f + .04f, .04f, .03f), rust);
            Block(t, "EndPlateB", new Vector3(0, .02f, -halfLen), new Vector3(hx * 2f + .04f, .04f, .03f), rust);
            Block(t, "BloodStain", new Vector3(.03f, .0255f, .12f), new Vector3(.12f, .001f, .09f), blood);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Ball(t, "Rivet", new Vector3(sx * (hx - .03f), .027f, sz * (halfLen - .05f)), .018f, steel);

            // pan (trigger) sa gitna + latch na nakatawid
            Block(t, "PanBase", new Vector3(0, .03f, 0), new Vector3(.15f, .012f, .17f), rust);
            Block(t, "PanPlate", new Vector3(0, .04f, 0), new Vector3(.12f, .01f, .14f), steel);
            Block(t, "DogPost", new Vector3(hx - .03f, .05f, 0), new Vector3(.025f, .06f, .035f), dark);
            Block(t, "Dog", new Vector3(.02f, .08f, 0), new Vector3(.2f, .012f, .028f), dark);
            Block(t, "DogHook", new Vector3(-.075f, .07f, 0), new Vector3(.014f, .03f, .028f), dark);
            var statusLight = Ball(t, "StatusLight", new Vector3(0, .042f, -.2f), .035f, lightMat).GetComponent<Renderer>();

            // leaf springs sa ibabaw ng base
            BuildLeafSpring(t, "LeafSpringL", -.095f, .21f, .08f, steel);
            BuildLeafSpring(t, "LeafSpringR", .095f, .21f, .08f, steel);

            // hinge + jaws (nakahiga palabas)
            Transform jawL = null, jawR = null;
            foreach (int s in new[] { 1, -1 })
            {
                float side = s;
                CylZ(t, "HingeBar", new Vector3(side * hx, .048f, 0), .024f, halfLen * 2f + .04f, steel);
                Ball(t, "HingeKnuckleF", new Vector3(side * hx, .048f, halfLen + .02f), .045f, steel);
                Ball(t, "HingeKnuckleB", new Vector3(side * hx, .048f, -halfLen - .02f), .045f, steel);

                var pivot = Empty(t, s > 0 ? "JawRight" : "JawLeft", new Vector3(side * hx, .048f, 0));
                BuildJaw(pivot.transform, halfLen, side, rust, dark, steel);
                if (s > 0) jawR = pivot.transform; else jawL = pivot.transform;
            }

            // kadena
            Prim(PrimitiveType.Cylinder, "ChainMount", t, new Vector3(0, .03f, -halfLen - .05f), new Vector3(.05f, .012f, .05f), Quaternion.identity, steel);
            for (int i = 0; i < 6; i++)
            {
                Vector3 s = (i % 2 == 0) ? new Vector3(.034f, .012f, .06f) : new Vector3(.012f, .034f, .06f);
                Prim(PrimitiveType.Sphere, "ChainLink" + i, t, new Vector3(0, .02f, -halfLen - .09f - i * .07f), s, Quaternion.identity, steel);
            }
            Prim(PrimitiveType.Cylinder, "ChainAnchor", t, new Vector3(0, .015f, -halfLen - .53f), new Vector3(.11f, .008f, .11f), Quaternion.identity, steel);

            var trap = root.AddComponent<TrapItem>();
            trap.jawLeft = jawL; trap.jawRight = jawR; trap.statusLight = statusLight;
            trap.SetJawAngle(trap.openAngle);

            var p = root.AddComponent<PickupItem>();
            p.kind = ItemKind.Trap; p.displayName = "Bear Trap";
            AddTriggerBox(root);
            return root;
        }

        // ------------------------------------------------ BOTTLE
        public static GameObject BottleModel(Transform parent, string name, Vector3 pos, float scale)
        {
            var root = Empty(parent, name, pos);
            root.transform.localScale = Vector3.one * scale;
            var t = root.transform;
            var glass = ItemMaterials.Get("BottleGlass", new Color(.08f, .2f, .1f), 0f, .92f);
            var cork = ItemMaterials.Get("Cork", new Color(.45f, .3f, .15f), 0f, .1f);
            var label = ItemMaterials.Get("BottleLabel", new Color(.75f, .68f, .5f), 0f, .1f);
            var stain = ItemMaterials.Get("LabelStain", new Color(.3f, .05f, .04f), 0f, .1f);

            CylY(t, "Body", new Vector3(0, .09f, 0), .07f, .18f, glass);
            Cone(t, "Shoulder", new Vector3(0, .18f, 0), Vector3.up, .07f, .06f, glass);
            CylY(t, "Neck", new Vector3(0, .255f, 0), .026f, .09f, glass);
            CylY(t, "Lip", new Vector3(0, .298f, 0), .034f, .012f, glass);
            CylY(t, "Cork", new Vector3(0, .31f, 0), .022f, .012f, cork);
            Ball(t, "BaseRing", new Vector3(0, .004f, 0), .072f, glass);
            Block(t, "Label", new Vector3(0, .09f, .0355f), new Vector3(.05f, .07f, .003f), label);
            Block(t, "LabelStain", new Vector3(.008f, .075f, .0375f), new Vector3(.02f, .025f, .001f), stain);
            return root;
        }

        public static GameObject BuildBottle()
        {
            var root = new GameObject("Bottle");
            BottleModel(root.transform, "Model", Vector3.zero, 1f);
            AddSolidBox(root);
            var pick = root.AddComponent<ItemPickup>();
            pick.type = ItemPickup.Type.Bottle;
            return root;
        }

        // ------------------------------------------------ SHELL (shotgun shell)
        public static GameObject ShellModel(Transform parent, string name, Vector3 pos, float scale)
        {
            var root = Empty(parent, name, pos);
            root.transform.localScale = Vector3.one * scale;
            var t = root.transform;
            var hull = ItemMaterials.Get("ShellHull", new Color(.6f, .06f, .05f), 0f, .55f);
            var band = ItemMaterials.Get("ShellBand", new Color(.3f, .03f, .03f), 0f, .4f);
            var brass = ItemMaterials.Get("Brass", new Color(.55f, .4f, .15f), .85f, .5f);
            var primer = ItemMaterials.Get("ShellPrimer", new Color(.7f, .7f, .72f), .9f, .5f);

            CylY(t, "Hull", new Vector3(0, .034f, 0), .02f, .068f, hull);
            CylY(t, "HullBand", new Vector3(0, .05f, 0), .0205f, .006f, band);
            CylY(t, "BrassBase", new Vector3(0, .009f, 0), .0215f, .018f, brass);
            CylY(t, "Rim", new Vector3(0, .0015f, 0), .0235f, .003f, brass);
            CylY(t, "Primer", new Vector3(0, -.0005f, 0), .008f, .002f, primer);
            Cone(t, "Crimp", new Vector3(0, .068f, 0), Vector3.up, .018f, .004f, band);
            return root;
        }

        public static GameObject BuildShell()
        {
            var root = new GameObject("Shell");
            ShellModel(root.transform, "Model", Vector3.zero, 1.5f);
            AddTriggerBox(root);
            return root;
        }

        // ------------------------------------------------ MUSIC PAGE (3 punit na piraso)
        // Isang buong papel na hinati sa tatlo gamit ang gula-gulanit na gilid. Pareho ang gilid ng
        // magkatabing piraso, kaya kapag pinagtabi sa PageSlot(i) ay buo ulit ang pahina.
        const float PageW = .30f;
        const float PageH = .21f;
        const int PageRows = 10;
        static float[][] pageEdges;
        static Texture2D pageTex;
        static Material pageMat;

        static void BuildPageEdges()
        {
            if (pageEdges != null) return;
            pageEdges = new float[4][];
            var rng = new System.Random(23);
            for (int e = 0; e < 4; e++)
            {
                pageEdges[e] = new float[PageRows + 1];
                float baseX = -PageW / 2f + PageW * e / 3f;
                float jag = (e == 0 || e == 3) ? .004f : .022f;
                for (int r = 0; r <= PageRows; r++)
                    pageEdges[e][r] = baseX + ((float)rng.NextDouble() * 2f - 1f) * jag;
            }
        }

        static Vector2 PageUV(float x, float z)
        {
            return new Vector2((x + PageW / 2f) / PageW, (z + PageH / 2f) / PageH);
        }

        /// Pwesto ng piraso (index 0 hanggang 2) sa loob ng buong pahina, relatibo sa gitna ng pahina.
        public static Vector3 PageSlot(int index)
        {
            BuildPageEdges();
            float cx = 0f;
            for (int r = 0; r <= PageRows; r++)
                cx += (pageEdges[index][r] + pageEdges[index + 1][r]) * .5f;
            return new Vector3(cx / (PageRows + 1), 0f, 0f);
        }

        static Mesh PageMesh(int index)
        {
            BuildPageEdges();
            float cx = PageSlot(index).x;
            var l = pageEdges[index];
            var rg = pageEdges[index + 1];

            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            for (int r = 0; r <= PageRows; r++)
            {
                float z = Mathf.Lerp(-PageH / 2f, PageH / 2f, r / (float)PageRows);
                v.Add(new Vector3(l[r] - cx, 0f, z)); uv.Add(PageUV(l[r], z));
                v.Add(new Vector3(rg[r] - cx, 0f, z)); uv.Add(PageUV(rg[r], z));
            }

            int n = v.Count;
            for (int i = 0; i < n; i++)
            {
                v.Add(v[i] + Vector3.down * .0008f);
                uv.Add(uv[i]);
            }

            var tri = new List<int>();
            for (int r = 0; r < PageRows; r++)
            {
                int a = r * 2, b = a + 1, c = a + 2, d = a + 3;
                // itaas
                tri.Add(a); tri.Add(c); tri.Add(b);
                tri.Add(b); tri.Add(c); tri.Add(d);
                // ilalim (baliktad)
                tri.Add(n + a); tri.Add(n + b); tri.Add(n + c);
                tri.Add(n + b); tri.Add(n + d); tri.Add(n + c);
            }

            var m = new Mesh { name = "HI_PagePiece" + index };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static void Put(Color32[] px, int w, int h, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = c;
        }

        static void Disc(Color32[] px, int w, int h, int cx, int cy, int rx, int ry, Color32 c)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                    if ((x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry) <= 1f)
                        Put(px, w, h, cx + x, cy + y, c);
        }

        static Texture2D PageTexture()
        {
            if (pageTex != null) return pageTex;

            const int w = 600, h = 420;
            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = Mathf.PerlinNoise(x * .015f + 3f, y * .015f + 7f);
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y)) / 40f;
                    float shade = Mathf.Lerp(.72f, 1f, n) * Mathf.Lerp(.7f, 1f, Mathf.Clamp01(edge));
                    px[y * w + x] = new Color(.84f * shade, .77f * shade, .6f * shade, 1f);
                }

            var ink = new Color32(40, 28, 20, 255);
            var rng = new System.Random(5);
            int left = (int)(w * .05f), right = (int)(w * .95f);
            foreach (float sv in new[] { .2f, .5f, .8f })
            {
                int cy = (int)(sv * h);
                for (int line = 0; line < 5; line++)
                {
                    int y = cy + (line - 2) * 12;
                    for (int x = left; x < right; x++) { Put(px, w, h, x, y, ink); Put(px, w, h, x, y + 1, ink); }
                }
                for (int y = cy - 24; y <= cy + 24; y++)
                {
                    Put(px, w, h, left, y, ink); Put(px, w, h, left + 1, y, ink);
                    Put(px, w, h, right, y, ink); Put(px, w, h, right - 1, y, ink);
                }
                for (int i = 0; i < 9; i++)
                {
                    int nx = (int)(w * .12f) + i * 55;
                    int ny = cy - 24 + rng.Next(-1, 9) * 6;
                    Disc(px, w, h, nx, ny, 8, 6, ink);
                    for (int s = 0; s < 40; s++) { Put(px, w, h, nx + 7, ny + s, ink); Put(px, w, h, nx + 8, ny + s, ink); }
                }
            }

            pageTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "HI_MusicPage" };
            pageTex.wrapMode = TextureWrapMode.Clamp;
            pageTex.filterMode = FilterMode.Bilinear;
            pageTex.SetPixels32(px);
            pageTex.Apply();
            return pageTex;
        }

        static Material PageMaterial()
        {
            if (pageMat != null) return pageMat;
            pageMat = new Material(ItemMaterials.Lit()) { name = "HI_MusicPage" };
            var tex = PageTexture();
            if (pageMat.HasProperty("_BaseMap")) pageMat.SetTexture("_BaseMap", tex);
            if (pageMat.HasProperty("_MainTex")) pageMat.SetTexture("_MainTex", tex);
            if (pageMat.HasProperty("_BaseColor")) pageMat.SetColor("_BaseColor", Color.white);
            if (pageMat.HasProperty("_Color")) pageMat.SetColor("_Color", Color.white);
            if (pageMat.HasProperty("_Smoothness")) pageMat.SetFloat("_Smoothness", 0f);
            if (pageMat.HasProperty("_Glossiness")) pageMat.SetFloat("_Glossiness", 0f);
            return pageMat;
        }

        static GameObject PagePiece(Transform parent, string name, int index)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = PageMesh(index);
            g.AddComponent<MeshRenderer>().sharedMaterial = PageMaterial();
            return g;
        }

        /// Isang punit na piraso (0, 1, o 2). Nakahiga sa lupa, y = 0.
        public static GameObject BuildTornPage(int index)
        {
            index = Mathf.Clamp(index, 0, 2);
            var root = new GameObject("TornMusicPage" + index);
            PagePiece(root.transform, "Paper", index);
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0, .02f, 0);
            col.size = new Vector3(.14f, .06f, PageH + .04f);
            return root;
        }

        /// Buong pahina (tatlong piraso na nakaayos sa tamang pwesto). Para sa music box o sa display.
        public static GameObject BuildFullPage()
        {
            var root = new GameObject("MusicPageComplete");
            for (int i = 0; i < 3; i++)
            {
                var piece = PagePiece(root.transform, "Piece" + i, i);
                piece.transform.localPosition = PageSlot(i);
            }
            return root;
        }
    }
}