#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Furnishes the BIG House so the rooms are not empty:
//  - the plain cube furniture (crib, wardrobes, sofa, bathtub, sink, kitchen counter, fireplace,
//    bookshelves, toybox) is replaced by furniture built from many small cubes
//  - new furniture in every room (armchairs, chairs, rugs, dressers, fridge, stove, toilet, clock, ...)
//  - picture frames hung unevenly on the walls (different sizes, heights and a little crooked)
//
// The old cubes stay in the scene with their colliders (only their mesh is hidden), so collisions,
// hiding spots and page spots keep working. New big furniture gets one simple collider.
//
// Everything goes under BigHouse/BigDecor. Running it again rebuilds it (safe to repeat).
//
// Menu:  Haunted House/Decorate BIG House
//        Haunted House/Remove Decor
//
// Order:  Build BIG House > Build 2ND FLOOR + ATTIC > Ramp To Stairs > Apply Asset Pack > Decorate BIG House
// then select BigHouse > Nav Mesh Surface > Bake, and Ctrl+S.
//
// Optional: import painting_1..painting_6 (png) anywhere in the project for the framed pictures.
// Without them the frames use plain dark colors.
//
// Put this file in Assets/Editor/.
public static class DecorateHouse
{
    const int SEED = 1313;          // change it for a different random layout of the pictures
    const float F2 = 3.5f;          // 2nd floor level
    const float F3 = 7f;            // attic level
    const float FB = -3.5f;         // basement level

    const string DECOR = "BigDecor";
    const string GEN = "Assets/Materials/Generated";

    delegate void Build(Transform r, float W, float H, float D);

    class Rep
    {
        public Build fn;
        public float yaw;
        public Rep(Build fn, float yaw) { this.fn = fn; this.yaw = yaw; }
    }

    // existing cube furniture that gets replaced (names made by the Build scripts)
    static readonly string[] REPLACED =
        { "Crib", "Wardrobe", "GuestWardrobe", "Sofa", "Bathtub", "Sink", "KitchenCounter", "Fireplace",
          "LibraryShelves", "StudyShelf", "Toybox" };

    static System.Random rnd;
    static int obstacleLayer;

    static Material wood, dark, fabric, fabric2, cream, ceramic, metal, black, glass, frameWood,
                    cardboard, shade, rugA, rugB, rugC, water;
    static Material[] books;
    static Material[] paints;
    static readonly bool[] PORTRAIT = { true, true, true, true, false, false };

    // ================================================================== menu

    [MenuItem("Haunted House/Decorate BIG House")]
    public static void Apply()
    {
        var big = FindRoot("BigHouse");
        if (big == null)
        {
            EditorUtility.DisplayDialog("Decor",
                "BigHouse not found in the open scene. Open the House scene and run Build BIG House first.", "OK");
            return;
        }

        Clear(big);

        obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0) obstacleLayer = 6;

        rnd = new System.Random(SEED);
        LoadMats();

        var decor = new GameObject(DECOR).transform;
        decor.SetParent(big.transform, false);

        ReplaceExisting(big, decor);

        Living(Group(decor, "Living"));
        Kitchen(Group(decor, "Kitchen"));
        Hall(Group(decor, "Hall"));
        Bedroom(Group(decor, "Bedroom"));
        Nursery(Group(decor, "Nursery"));
        Bath(Group(decor, "Bath"));
        Study(Group(decor, "Study"));
        Guest(Group(decor, "Guest"));
        Library(Group(decor, "Library"));
        Playroom(Group(decor, "Playroom"));
        Attic(Group(decor, "Attic"));
        Basement(Group(decor, "Basement"));

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = decor.gameObject;
        Debug.Log("House decorated (" + decor.GetComponentsInChildren<Renderer>().Length +
                  " parts). NEXT: select BigHouse > Nav Mesh Surface > Bake, then Ctrl+S.");
    }

    [MenuItem("Haunted House/Remove Decor")]
    public static void Remove()
    {
        var big = FindRoot("BigHouse");
        if (big == null) return;
        Clear(big);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Decor removed. The plain cubes are visible again.");
    }

    static void Clear(GameObject big)
    {
        var old = big.transform.Find(DECOR);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        foreach (var mr in big.GetComponentsInChildren<MeshRenderer>(true))
            if (System.Array.IndexOf(REPLACED, mr.name) >= 0) mr.enabled = true;
    }

    // ================================================================== replace existing cubes

    static void ReplaceExisting(GameObject big, Transform decor)
    {
        var g = Group(decor, "Replaced");

        var reps = new Dictionary<string, Rep>
        {
            { "Crib",           new Rep(Crib, 0f) },
            { "Wardrobe",       new Rep(Wardrobe, 270f) },
            { "GuestWardrobe",  new Rep(Wardrobe, 270f) },
            { "Sofa",           new Rep(Sofa, 180f) },
            { "Bathtub",        new Rep(Bathtub, 270f) },
            { "Sink",           new Rep(Sink, 180f) },
            { "KitchenCounter", new Rep(KitchenCounter, 180f) },
            { "Fireplace",      new Rep(Fireplace, 90f) },
            { "LibraryShelves", new Rep(BookShelf, 180f) },
            { "StudyShelf",     new Rep(BookShelf, 0f) },
            { "Toybox",         new Rep(Toybox, 0f) },
        };

        foreach (var mr in big.GetComponentsInChildren<MeshRenderer>(true))
        {
            Rep rep;
            if (!reps.TryGetValue(mr.name, out rep)) continue;
            if (mr.transform.IsChildOf(decor)) continue;

            Bounds b = mr.bounds;
            bool turned = Mathf.Abs(Mathf.Sin(rep.yaw * Mathf.Deg2Rad)) > 0.5f;
            float W = turned ? b.size.z : b.size.x;
            float D = turned ? b.size.x : b.size.z;

            // solid = false: the old cube keeps its collider
            Put(g, mr.name + "_Visual", rep.fn, b.center.x, b.min.y, b.center.z, rep.yaw, W, b.size.y, D, false, false);
            mr.enabled = false;
        }
    }

    // ================================================================== rooms

    static void Living(Transform g)
    {
        Put(g, "Rug_Living", RugA, 4f, 0f, 3.2f, 0f, 3.4f, 0.02f, 2.6f, false);
        Put(g, "Armchair_Living", ArmChair, 7.2f, 0f, 3.3f, 270f, 0.95f, 0.95f, 0.95f);
        Put(g, "SideTable_Living", SideTable, 1.9f, 0f, 4.9f, 0f, 0.5f, 0.55f, 0.5f);
        Put(g, "Bookshelf_Living", BookShelf, 12.7f, 0f, 0.42f, 0f, 3.4f, 2.1f, 0.4f, true, true);
        Put(g, "Sideboard_Living", Sideboard, 14f, 0f, 7.62f, 180f, 3.0f, 0.9f, 0.45f);
        Put(g, "Clock_Living", GrandClock, 16.4f, 0f, 7.55f, 180f, 0.5f, 2.2f, 0.35f, true, true);

        // big mirror over the fireplace
        Frame(g, new Vector3(0.15f, 2.6f, 4f), Vector3.right, 1.3f, 0.85f, 0f, glass);

        Gallery(g, 0f, true, 0.15f, 1f, 0.4f, 7.3f, 1.5f, 2.2f);
        Gallery(g, 0f, true, 0.15f, 1f, 14.7f, 16.6f, 1.5f, 2.2f);
        Gallery(g, 0f, false, 0.15f, 1f, 0.4f, 2.2f, 1.5f, 2.2f);
        Gallery(g, 0f, true, 7.85f, -1f, 11.4f, 16.0f, 1.5f, 2.2f);
        Gallery(g, 0f, false, 16.85f, -1f, 0.4f, 2.0f, 1.5f, 2.2f);
        Gallery(g, 0f, false, 16.85f, -1f, 6.0f, 7.7f, 1.5f, 2.2f);
    }

    static void Kitchen(Transform g)
    {
        Put(g, "Fridge", Fridge, 29.35f, 0f, 5.5f, 270f, 0.8f, 1.9f, 0.8f, true, true);
        Put(g, "Stove", Stove, 21.6f, 0f, 0.55f, 0f, 0.8f, 0.9f, 0.7f);
        Put(g, "UpperCabs_A", UpperCabs, 20.95f, 1.5f, 7.7f, 180f, 2.9f, 0.7f, 0.3f, false);
        Put(g, "UpperCabs_B", UpperCabs, 26.55f, 1.5f, 7.7f, 180f, 1.9f, 0.7f, 0.3f, false);
        Put(g, "Rug_Kitchen", RugB, 23.5f, 0f, 3.5f, 0f, 3.4f, 0.02f, 2.6f, false);
        Put(g, "Chair_K1", Chair, 22.8f, 0f, 2.5f, 0f, 0.45f, 0.9f, 0.45f, false);
        Put(g, "Chair_K2", Chair, 24.2f, 0f, 2.5f, 8f, 0.45f, 0.9f, 0.45f, false);
        Put(g, "Chair_K3", Chair, 22.8f, 0f, 4.6f, 175f, 0.45f, 0.9f, 0.45f, false);
        Put(g, "Chair_K4", Chair, 24.5f, 0f, 4.2f, 215f, 0.45f, 0.9f, 0.45f, false);   // pushed out a little
        Put(g, "Sideboard_Kitchen", Sideboard, 26.5f, 0f, 0.4f, 0f, 2.8f, 0.9f, 0.45f);

        Gallery(g, 0f, true, 0.15f, 1f, 17.4f, 20.8f, 1.5f, 2.2f);
        Gallery(g, 0f, true, 0.15f, 1f, 22.4f, 29.6f, 1.5f, 2.2f);
        Gallery(g, 0f, false, 29.85f, -1f, 0.4f, 7.7f, 1.5f, 2.2f, 1.4f, 2.6f, 4.9f, 6.1f);
        Gallery(g, 0f, true, 7.85f, -1f, 17.4f, 19.4f, 1.5f, 2.2f);
        Gallery(g, 0f, true, 7.85f, -1f, 27.6f, 29.6f, 1.5f, 2.2f);
        Gallery(g, 0f, false, 17.15f, 1f, 0.4f, 2.0f, 1.5f, 2.2f);
        Gallery(g, 0f, false, 17.15f, 1f, 6.0f, 7.7f, 1.5f, 2.2f);
    }

    static void Hall(Transform g)
    {
        Put(g, "Runner_Hall", RugC, 15f, 0f, 10f, 0f, 26f, 0.02f, 0.9f, false);
        Put(g, "Console_Hall1", ConsoleTable, 14.5f, 0f, 8.33f, 0f, 1.8f, 0.8f, 0.35f);
        Put(g, "Console_Hall2", ConsoleTable, 19.5f, 0f, 11.67f, 180f, 1.8f, 0.8f, 0.35f);
        Put(g, "Bench_Hall", Bench, 9.5f, 0f, 11.65f, 180f, 1.6f, 0.5f, 0.4f);
        Put(g, "CoatRack", CoatRack, 0.6f, 0f, 10f, 0f, 0.5f, 1.9f, 0.5f);

        Gallery(g, 0f, true, 8.15f, 1f, 0.4f, 29.6f, 1.45f, 2.25f, 8.6f, 11.4f, 22.8f, 25.2f, 27.4f, 29.6f);
        Gallery(g, 0f, true, 11.85f, -1f, 0.4f, 29.6f, 1.45f, 2.25f, 4.9f, 7.1f, 15.4f, 17.6f, 24.4f, 26.6f);
    }

    static void Bedroom(Transform g)
    {
        Put(g, "Rug_Bedroom", RugA, 4.4f, 0f, 16.4f, 0f, 2.6f, 0.02f, 3.2f, false);
        Put(g, "Dresser_Bedroom", Dresser, 9.3f, 0f, 19.55f, 180f, 2.0f, 0.95f, 0.5f);
        Put(g, "Armchair_Bedroom", ArmChair, 11.0f, 0f, 18.9f, 225f, 0.9f, 0.9f, 0.9f);
        Put(g, "Chest_Bedroom", Trunk, 5.0f, 0f, 19.4f, 180f, 1.0f, 0.5f, 0.5f);

        Frame(g, new Vector3(9.3f, 1.8f, 19.85f), new Vector3(0f, 0f, -1f), 1.0f, 1.3f, 2f, glass);

        Gallery(g, 0f, false, 0.15f, 1f, 12.4f, 19.6f, 1.5f, 2.2f);
        Gallery(g, 0f, true, 19.85f, -1f, 0.4f, 11.6f, 1.5f, 2.2f, 6.4f, 7.7f, 8.2f, 10.4f);
        Gallery(g, 0f, true, 12.15f, 1f, 0.4f, 11.6f, 1.5f, 2.2f, 4.8f, 7.2f);
        Gallery(g, 0f, false, 11.85f, -1f, 12.4f, 19.6f, 1.5f, 2.2f, 12.6f, 15.4f, 15.0f, 17.0f);
    }

    static void Nursery(Transform g)
    {
        Put(g, "RockingChair", RockingChair, 19.7f, 0f, 18.9f, 200f, 0.7f, 0.95f, 0.9f);
        Put(g, "ToyShelf_Nursery", BookShelf, 18.4f, 0f, 19.62f, 180f, 1.3f, 0.9f, 0.4f);
        Put(g, "Blocks_Nursery", ToyBlocks, 16.5f, 0f, 14.5f, 0f, 2.4f, 0.12f, 2.0f, false);
        Put(g, "Teddy_Nursery", Teddy, 17.7f, 0f, 15.3f, 200f, 0.4f, 0.45f, 0.4f, false);
        Put(g, "Mobile_Nursery", Mobile, 16.5f, 2.3f, 17.5f, 0f, 0.6f, 0.9f, 0.6f, false);

        Gallery(g, 0f, false, 12.15f, 1f, 12.4f, 19.6f, 1.4f, 2.1f, 15.0f, 17.0f);
        Gallery(g, 0f, true, 19.85f, -1f, 12.4f, 20.6f, 1.4f, 2.1f);
        Gallery(g, 0f, false, 20.85f, -1f, 12.4f, 19.6f, 1.4f, 2.1f, 15.0f, 17.0f);
        Gallery(g, 0f, true, 12.15f, 1f, 12.4f, 20.6f, 1.4f, 2.1f, 15.4f, 17.6f);
    }

    static void Bath(Transform g)
    {
        Put(g, "Toilet", Toilet, 29.5f, 0f, 13.5f, 270f, 0.4f, 0.85f, 0.7f);
        Put(g, "Basket_Bath", Basket, 22.0f, 0f, 19.3f, 0f, 0.45f, 0.6f, 0.45f);
        Put(g, "BathMat", RugB, 27.0f, 0f, 17.4f, 0f, 0.9f, 0.02f, 0.6f, false);
        Put(g, "WallShelf_Bath", WallShelf, 26.5f, 1.4f, 19.7f, 180f, 1.2f, 0.3f, 0.25f, false);

        Frame(g, new Vector3(24f, 1.75f, 19.85f), new Vector3(0f, 0f, -1f), 0.9f, 1.1f, -2f, glass);

        Gallery(g, 0f, false, 21.15f, 1f, 12.4f, 19.6f, 1.4f, 2.1f, 15.0f, 17.0f);
        Gallery(g, 0f, true, 19.85f, -1f, 21.4f, 23.0f, 1.4f, 2.1f);
        Gallery(g, 0f, true, 19.85f, -1f, 25.2f, 27.4f, 1.5f, 2.1f);
    }

    static void Study(Transform g)
    {
        Put(g, "Chair_Desk", Chair, 3.0f, F2, 3.1f, 170f, 0.5f, 0.95f, 0.5f, false);
        Put(g, "Rug_Study", RugA, 3.4f, F2, 3.0f, 0f, 3.2f, 0.02f, 2.8f, false);
        Put(g, "Armchair_Study", ArmChair, 13.8f, F2, 6.9f, 225f, 0.9f, 0.9f, 0.9f);
        Put(g, "SideTable_Study", SideTable, 13.9f, F2, 5.7f, 0f, 0.5f, 0.55f, 0.5f);
        Put(g, "FilingCabinet", Cabinet, 0.5f, F2, 4.7f, 90f, 0.6f, 1.3f, 0.45f);

        Gallery(g, F2, true, 0.15f, 1f, 0.4f, 9.8f, 1.5f, 2.2f);
        Gallery(g, F2, false, 0.15f, 1f, 0.4f, 4.2f, 1.5f, 2.2f);
        Gallery(g, F2, true, 7.85f, -1f, 0.4f, 2.7f, 1.5f, 2.2f);
        Gallery(g, F2, true, 7.85f, -1f, 13.2f, 14.7f, 1.5f, 2.2f);
        Gallery(g, F2, false, 14.85f, -1f, 0.4f, 7.7f, 1.5f, 2.2f, 3.0f, 5.0f);
    }

    static void Guest(Transform g)
    {
        Put(g, "Nightstand_Guest", SideTable, 26.0f, F2, 4.9f, 0f, 0.5f, 0.6f, 0.5f);
        Put(g, "Armchair_Guest", ArmChair, 17.5f, F2, 6.8f, 135f, 0.9f, 0.9f, 0.9f);
        Put(g, "Dresser_Guest", Dresser, 25.0f, F2, 7.6f, 180f, 1.8f, 0.9f, 0.45f);
        Put(g, "Rug_Guest", RugB, 26.0f, F2, 3.0f, 0f, 3.2f, 0.02f, 3.4f, false);

        Frame(g, new Vector3(25f, F2 + 1.7f, 7.85f), new Vector3(0f, 0f, -1f), 0.9f, 1.2f, 3f, glass);

        Gallery(g, F2, true, 7.85f, -1f, 15.4f, 20.9f, 1.5f, 2.2f);
        Gallery(g, F2, true, 7.85f, -1f, 23.1f, 28.4f, 1.5f, 2.2f, 24.3f, 25.7f);
        Gallery(g, F2, true, 0.15f, 1f, 25.4f, 29.6f, 1.5f, 2.2f);
        Gallery(g, F2, false, 29.85f, -1f, 0.4f, 5.3f, 1.5f, 2.2f);
        Gallery(g, F2, false, 15.15f, 1f, 0.4f, 3.0f, 1.5f, 2.2f);
        Gallery(g, F2, false, 15.15f, 1f, 5.0f, 7.7f, 1.5f, 2.2f);
    }

    static void Library(Transform g)
    {
        Put(g, "Chair_L1", Chair, 7.4f, F2, 16.1f, 190f, 0.5f, 0.95f, 0.5f, false);
        Put(g, "Chair_L2", Chair, 8.6f, F2, 13.9f, -12f, 0.5f, 0.95f, 0.5f, false);
        Put(g, "Armchair_Library", ArmChair, 2.0f, F2, 13.6f, 45f, 0.9f, 0.9f, 0.9f);
        Put(g, "FloorLamp_Library", FloorLamp, 0.9f, F2, 12.9f, 0f, 0.35f, 1.5f, 0.35f, false);
        Put(g, "Rug_Library", RugA, 8f, F2, 15f, 0f, 4f, 0.02f, 3f, false);
        Put(g, "Ladder", Ladder, 11.5f, F2, 18.75f, 180f, 0.5f, 2.3f, 0.3f, false);
        Put(g, "Globe", Globe, 12.6f, F2, 16.5f, 0f, 0.5f, 1.1f, 0.5f);
        Put(g, "SideShelf_Library", BookShelf, 0.35f, F2, 16f, 90f, 3.6f, 2.1f, 0.3f, true, true);

        Gallery(g, F2, true, 12.15f, 1f, 0.4f, 14.6f, 1.5f, 2.2f, 5.9f, 8.1f);
        Gallery(g, F2, false, 0.15f, 1f, 12.4f, 13.8f, 1.5f, 2.2f);
        Gallery(g, F2, false, 0.15f, 1f, 18.2f, 19.6f, 1.5f, 2.2f);
        Gallery(g, F2, false, 14.85f, -1f, 12.4f, 19.6f, 1.5f, 2.2f, 15.0f, 17.0f);
    }

    static void Playroom(Transform g)
    {
        Put(g, "Dollhouse", Dollhouse, 28.4f, F2, 18.8f, 225f, 1.1f, 1.2f, 0.7f);
        Put(g, "Blocks_Playroom", ToyBlocks, 22f, F2, 15f, 0f, 3f, 0.12f, 3f, false);
        Put(g, "KidTable", KidTable, 26.5f, F2, 13.8f, 0f, 1.0f, 0.5f, 0.7f, false);
        Put(g, "KidChair1", Chair, 26.1f, F2, 14.6f, 175f, 0.3f, 0.6f, 0.3f, false);
        Put(g, "KidChair2", Chair, 26.9f, F2, 14.4f, 200f, 0.3f, 0.6f, 0.3f, false);
        Put(g, "RockingHorse", RockingHorse, 17.5f, F2, 18.8f, 160f, 0.5f, 0.9f, 1.0f);
        Put(g, "ToyShelf_Playroom", BookShelf, 19.5f, F2, 19.62f, 180f, 1.6f, 0.9f, 0.4f);
        Put(g, "Teddy_Playroom", Teddy, 23.3f, F2, 16.3f, 100f, 0.4f, 0.45f, 0.4f, false);

        Gallery(g, F2, true, 12.15f, 1f, 15.4f, 29.6f, 1.5f, 2.2f, 20.9f, 23.1f);
        Gallery(g, F2, true, 19.85f, -1f, 20.5f, 29.6f, 1.5f, 2.2f);
        Gallery(g, F2, false, 29.85f, -1f, 12.4f, 17.4f, 1.5f, 2.2f);
        Gallery(g, F2, false, 15.15f, 1f, 12.4f, 15.0f, 1.5f, 2.2f);
        Gallery(g, F2, false, 15.15f, 1f, 17.0f, 19.6f, 1.5f, 2.2f);
    }

    static void Attic(Transform g)
    {
        Put(g, "Trunk_A1", Trunk, 8f, F3, 18.8f, 10f, 1.1f, 0.6f, 0.6f);
        Put(g, "Trunk_A2", Trunk, 8.2f, F3 + 0.6f, 18.8f, -6f, 0.9f, 0.5f, 0.5f, false);
        Put(g, "Boxes_A", Boxes, 2.5f, F3, 18.5f, 20f, 1.2f, 1.2f, 1.2f);
        Put(g, "Boxes_B", Boxes, 28f, F3, 3.0f, 70f, 1.2f, 1.2f, 1.2f);
        Put(g, "Mannequin", Mannequin, 13.5f, F3, 5f, 200f, 0.5f, 1.7f, 0.5f);
        Put(g, "RockingHorse_Attic", RockingHorse, 3f, F3, 12f, 70f, 0.5f, 0.9f, 1.0f);
        Put(g, "Crib_Attic", Crib, 22f, F3, 17f, 30f, 1.2f, 0.9f, 2.0f);
    }

    static void Basement(Transform g)
    {
        Put(g, "Barrel_1", Barrel, 39.3f, FB, 16.8f, 0f, 0.6f, 0.9f, 0.6f);
        Put(g, "Barrel_2", Barrel, 40.1f, FB, 16.9f, 0f, 0.6f, 0.9f, 0.6f);
        Put(g, "Barrel_3", Barrel, 39.6f, FB, 3.4f, 0f, 0.6f, 0.9f, 0.6f);
        Put(g, "Workbench", Workbench, 58.0f, FB, 6f, 90f, 1.8f, 0.95f, 0.7f);
        Put(g, "MetalRack", MetalRack, 52.5f, FB, 2.5f, 0f, 1.6f, 1.9f, 0.5f, true, true);
    }

    // ================================================================== furniture builders
    // Local space: origin = center of the floor footprint, +x = width, +y = up, +z = front.

    static void Crib(Transform r, float W, float H, float D)
    {
        float hx = W / 2f, hz = D / 2f;
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cube(r, sx * (hx - 0.04f), (H + 0.1f) / 2f, sz * (hz - 0.04f), 0.08f, H + 0.1f, 0.08f, wood);

        Cube(r, 0f, 0.42f, 0f, W - 0.16f, 0.12f, D - 0.16f, cream);                          // mattress
        Cube(r, 0.05f, 0.5f, 0.35f, W - 0.4f, 0.04f, D * 0.5f, fabric2, 0f, 6f, 0f);         // blanket

        foreach (var sx in new[] { -1f, 1f })
        {
            float x = sx * (hx - 0.04f);
            Cube(r, x, H, 0f, 0.05f, 0.05f, D - 0.08f, wood);                                 // top rail
            Cube(r, x, 0.3f, 0f, 0.05f, 0.05f, D - 0.08f, wood);                              // bottom rail

            int n = Mathf.Max(3, Mathf.RoundToInt(D / 0.14f));
            for (int i = 0; i < n; i++)                                                        // bars
            {
                float z = -hz + 0.12f + i * (D - 0.24f) / (n - 1);
                Cube(r, x, (0.3f + H) / 2f, z, 0.025f, H - 0.3f, 0.025f, wood);
            }
        }

        Cube(r, 0f, (H + 0.15f) / 2f, -hz + 0.04f, W - 0.16f, H + 0.15f, 0.05f, wood);       // headboard
        Cube(r, 0f, 0.5f, hz - 0.04f, W - 0.16f, 0.7f, 0.05f, wood);                          // footboard
    }

    static void Wardrobe(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.05f, 0f, W + 0.04f, 0.1f, D + 0.04f, dark);                             // plinth
        Cube(r, 0f, H / 2f, 0f, W, H - 0.2f, D, wood);                                        // body
        Cube(r, 0f, H - 0.05f, 0f, W + 0.08f, 0.1f, D + 0.08f, wood);                         // crown
        foreach (var sx in new[] { -1f, 1f })
        {
            Cube(r, sx * W / 4f, H / 2f, D / 2f + 0.015f, W / 2f - 0.08f, H - 0.5f, 0.03f, wood);   // door panel
            Cube(r, sx * 0.07f, H * 0.5f, D / 2f + 0.04f, 0.03f, 0.2f, 0.03f, metal);               // handle
        }
        Cube(r, 0f, H / 2f, D / 2f + 0.005f, 0.015f, H - 0.3f, 0.01f, black);                 // gap between doors
    }

    static void Sofa(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.25f, 0f, W, 0.3f, D, fabric);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cube(r, sx * (W / 2f - 0.08f), 0.05f, sz * (D / 2f - 0.08f), 0.06f, 0.1f, 0.06f, wood);

        Cube(r, 0f, 0.6f, -D / 2f + 0.07f, W, 0.7f, 0.14f, fabric);                           // backrest
        int n = Mathf.Max(1, Mathf.RoundToInt((W - 0.5f) / 0.8f));
        float cw = (W - 0.5f) / n;
        for (int i = 0; i < n; i++)
        {
            float x = -(W - 0.5f) / 2f + cw * (i + 0.5f);
            Cube(r, x, 0.46f, 0.08f, cw - 0.03f, 0.16f, D - 0.3f, fabric);                    // seat cushion
            Cube(r, x, 0.72f, -D / 2f + 0.2f, cw - 0.04f, 0.45f, 0.16f, fabric, -10f, 0f, 0f); // back cushion
        }
        foreach (var sx in new[] { -1f, 1f })
            Cube(r, sx * (W / 2f - 0.1f), 0.5f, 0f, 0.2f, 0.55f, D, fabric);                  // arms
    }

    static void ArmChair(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.25f, 0f, W, 0.3f, D, fabric);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cube(r, sx * (W / 2f - 0.08f), 0.05f, sz * (D / 2f - 0.08f), 0.06f, 0.1f, 0.06f, wood);

        Cube(r, 0f, 0.46f, 0.06f, W - 0.35f, 0.14f, D - 0.25f, fabric);
        Cube(r, 0f, 0.7f, -D / 2f + 0.08f, W, 0.8f, 0.16f, fabric);
        foreach (var sx in new[] { -1f, 1f })
            Cube(r, sx * (W / 2f - 0.09f), 0.5f, 0f, 0.18f, 0.5f, D, fabric);
    }

    static void Bathtub(Transform r, float W, float H, float D)
    {
        float hx = W / 2f, hz = D / 2f;
        foreach (var sz in new[] { -1f, 1f })
            Cube(r, 0f, (H - 0.1f) / 2f + 0.05f, sz * (hz - 0.05f), W, H - 0.1f, 0.1f, ceramic);
        foreach (var sx in new[] { -1f, 1f })
            Cube(r, sx * (hx - 0.05f), (H - 0.1f) / 2f + 0.05f, 0f, 0.1f, H - 0.1f, D - 0.2f, ceramic);

        Cube(r, 0f, 0.2f, 0f, W - 0.2f, 0.08f, D - 0.2f, ceramic);                             // bottom
        Cube(r, 0f, 0.5f, 0f, W - 0.26f, 0.02f, D - 0.26f, water);                             // stagnant water

        foreach (var sz in new[] { -1f, 1f })
            Cube(r, 0f, H, sz * (hz - 0.07f), W, 0.04f, 0.14f, ceramic);                       // rim
        foreach (var sx in new[] { -1f, 1f })
            Cube(r, sx * (hx - 0.07f), H, 0f, 0.14f, 0.04f, D - 0.28f, ceramic);

        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cyl(r, sx * (hx - 0.15f), 0.05f, sz * (hz - 0.15f), 0.1f, 0.1f, metal);        // feet

        Cube(r, -hx + 0.12f, H + 0.15f, 0f, 0.04f, 0.3f, 0.04f, metal);                        // faucet
        Cube(r, -hx + 0.2f, H + 0.3f, 0f, 0.16f, 0.04f, 0.04f, metal);
    }

    static void Sink(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.35f, -0.05f, 0.25f, 0.7f, 0.2f, ceramic);                                // pedestal
        Cube(r, 0f, 0.78f, 0f, W, 0.14f, D, ceramic);                                          // basin
        Cube(r, 0f, 0.86f, 0.02f, W - 0.25f, 0.02f, D - 0.18f, black);                         // inside
        Cube(r, 0f, 0.98f, -D / 2f + 0.02f, W, 0.2f, 0.04f, ceramic);                          // backsplash
        Cube(r, 0f, 1.0f, -D / 2f + 0.1f, 0.03f, 0.18f, 0.03f, metal);                         // faucet
        Cube(r, 0f, 1.08f, -D / 2f + 0.17f, 0.03f, 0.03f, 0.16f, metal);
    }

    static void Toilet(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.19f, 0.05f, W, 0.38f, D - 0.2f, ceramic);                                // bowl
        Cube(r, 0f, 0.4f, 0.08f, W - 0.02f, 0.04f, D - 0.25f, cream);                          // seat
        Cube(r, 0f, 0.62f, -D / 2f + 0.1f, W, 0.4f, 0.18f, ceramic);                           // tank
        Cube(r, 0f, 0.84f, -D / 2f + 0.1f, W + 0.02f, 0.03f, 0.2f, ceramic);                   // lid
    }

    static void KitchenCounter(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, (H - 0.06f) / 2f, 0f, W, H - 0.06f, D - 0.04f, wood);
        Cube(r, 0f, H - 0.03f, 0f, W + 0.02f, 0.06f, D + 0.02f, metal);                        // countertop
        int n = Mathf.Max(2, Mathf.RoundToInt(W / 0.6f));
        float dw = W / n;
        for (int i = 0; i < n; i++)
        {
            float x = -W / 2f + dw * (i + 0.5f);
            Cube(r, x, 0.4f, D / 2f - 0.015f, dw - 0.04f, H - 0.2f, 0.02f, dark);              // door
            Cube(r, x + dw * 0.3f, 0.62f, D / 2f + 0.01f, 0.02f, 0.14f, 0.02f, metal);         // handle
        }
        Cube(r, W * 0.12f, H + 0.0f, 0f, 0.7f, 0.01f, D * 0.6f, black);                        // sink basin
        Cube(r, W * 0.12f, H + 0.17f, -D / 2f + 0.1f, 0.03f, 0.3f, 0.03f, metal);              // faucet
        Cube(r, W * 0.12f, H + 0.31f, -D / 2f + 0.2f, 0.03f, 0.03f, 0.2f, metal);
    }

    static void Fireplace(Transform r, float W, float H, float D)
    {
        foreach (var sx in new[] { -1f, 1f })
            Cube(r, sx * (W / 2f - 0.2f), H / 2f, 0f, 0.4f, H, D, dark);                       // side columns
        Cube(r, 0f, H - 0.25f, 0f, W, 0.5f, D, dark);                                          // lintel
        Cube(r, 0f, H - 0.5f, 0.1f, W + 0.2f, 0.08f, D + 0.15f, wood);                         // mantel
        Cube(r, 0f, (H - 0.5f) / 2f, 0f, W - 0.8f, H - 0.5f, D - 0.05f, black);                // firebox
        Cube(r, 0f, 0.06f, D / 2f + 0.2f, W + 0.3f, 0.06f, D * 0.5f + 0.2f, dark);             // hearth
        Cube(r, -0.2f, 0.12f, 0.05f, 0.7f, 0.1f, 0.1f, wood, 0f, 20f, 0f);                     // burnt logs
        Cube(r, 0.15f, 0.12f, -0.05f, 0.7f, 0.1f, 0.1f, wood, 0f, -15f, 0f);
    }

    static void BookShelf(Transform r, float W, float H, float D)
    {
        Cube(r, -W / 2f + 0.02f, H / 2f, 0f, 0.04f, H, D, wood);
        Cube(r, W / 2f - 0.02f, H / 2f, 0f, 0.04f, H, D, wood);
        Cube(r, 0f, H - 0.02f, 0f, W, 0.04f, D, wood);
        Cube(r, 0f, 0.06f, 0f, W - 0.08f, 0.1f, D, dark);
        Cube(r, 0f, H / 2f, -D / 2f + 0.01f, W - 0.08f, H - 0.1f, 0.02f, dark);

        int n = Mathf.Max(2, Mathf.RoundToInt((H - 0.15f) / 0.36f));
        float step = (H - 0.15f) / n;

        for (int i = 0; i < n; i++)
        {
            float y = 0.11f + i * step;
            Cube(r, 0f, y, 0f, W - 0.08f, 0.03f, D - 0.02f, wood);

            float avail = step - 0.06f;
            float x = -W / 2f + 0.06f;
            while (x < W / 2f - 0.12f)
            {
                float bw = 0.1f + (float)rnd.NextDouble() * 0.2f;
                if (x + bw > W / 2f - 0.05f) break;

                if (rnd.NextDouble() < 0.2) { x += bw; continue; }   // gap

                float bh = Mathf.Min(avail, 0.18f + (float)rnd.NextDouble() * 0.16f);
                float lean = rnd.NextDouble() < 0.1 ? 12f : 0f;
                Cube(r, x + bw / 2f, y + 0.015f + bh / 2f, 0f, bw, bh, D - 0.12f, books[rnd.Next(books.Length)], 0f, 0f, lean);
                x += bw + 0.005f;
            }
        }
    }

    static void Toybox(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, (H - 0.05f) / 2f, 0f, W, H - 0.05f, D, wood);
        Cube(r, 0f, H * 0.4f, D / 2f + 0.005f, W - 0.1f, 0.04f, 0.01f, dark);                  // painted band
        Cube(r, 0f, H + 0.02f, -D / 2f + 0.15f, W + 0.04f, 0.04f, D * 0.7f, wood, -14f, 0f, 0f); // lid, a bit open
        Cube(r, 0.2f, H + 0.05f, 0.15f, 0.15f, 0.15f, 0.15f, fabric, 0f, 20f, 0f);            // toy sticking out
    }

    static void Sideboard(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.1f + (H - 0.2f) / 2f, 0f, W, H - 0.2f, D, wood);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cube(r, sx * (W / 2f - 0.08f), 0.05f, sz * (D / 2f - 0.08f), 0.06f, 0.1f, 0.06f, dark);
        Cube(r, 0f, H - 0.02f, 0f, W + 0.04f, 0.04f, D + 0.04f, dark);

        int n = Mathf.Max(2, Mathf.RoundToInt(W / 0.9f));
        float dw = W / n;
        for (int i = 0; i < n; i++)
        {
            float x = -W / 2f + dw * (i + 0.5f);
            Cube(r, x, H * 0.5f, D / 2f + 0.005f, dw - 0.08f, H - 0.35f, 0.02f, dark);
            Cube(r, x, H * 0.5f, D / 2f + 0.03f, 0.05f, 0.05f, 0.03f, metal);
        }

        if (W > 2.5f) Lamp(r, -W * 0.3f, H, 0f);
    }

    static void ConsoleTable(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H - 0.025f, 0f, W, 0.05f, D, wood);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cube(r, sx * (W / 2f - 0.05f), (H - 0.05f) / 2f, sz * (D / 2f - 0.04f), 0.05f, H - 0.05f, 0.05f, wood);
        Cube(r, 0f, 0.2f, 0f, W - 0.1f, 0.03f, D - 0.06f, wood);
        Cyl(r, W * 0.25f, H + 0.12f, 0f, 0.14f, 0.24f, ceramic);                               // vase
        for (int i = 0; i < 3; i++)                                                            // dead flowers
            Cube(r, W * 0.25f + (i - 1) * 0.03f, H + 0.35f, 0f, 0.01f, 0.25f, 0.01f, black, 0f, 0f, (i - 1) * 14f);
    }

    static void Bench(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H - 0.04f, 0f, W, 0.07f, D, wood);
        foreach (var sx in new[] { -1f, 1f })
            Cube(r, sx * (W / 2f - 0.1f), (H - 0.08f) / 2f, 0f, 0.06f, H - 0.08f, D - 0.06f, wood);
    }

    static void CoatRack(Transform r, float W, float H, float D)
    {
        Cyl(r, 0f, 0.02f, 0f, 0.45f, 0.04f, dark);
        Cyl(r, 0f, H / 2f, 0f, 0.05f, H, wood);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f + 20f;
            var arm = Cube(r, 0f, H - 0.12f - (i % 2) * 0.12f, 0f, 0.32f, 0.03f, 0.03f, wood, 0f, a, 25f);
            arm.transform.localPosition += Quaternion.Euler(0f, a, 0f) * new Vector3(0.14f, 0f, 0f);
        }
        Cube(r, 0.14f, H - 0.62f, 0.08f, 0.4f, 0.9f, 0.1f, fabric2, 0f, 15f, 4f);              // an old coat
    }

    static void GrandClock(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.2f, 0f, W, 0.4f, D, dark);
        Cube(r, 0f, 0.4f + (H - 0.95f) / 2f, 0f, W - 0.1f, H - 0.95f, D - 0.06f, dark);
        Cube(r, 0f, H - 0.27f, 0f, W + 0.04f, 0.45f, D + 0.02f, dark);                         // hood
        var face = Cyl(r, 0f, H - 0.27f, D / 2f + 0.01f, 0.3f, 0.02f, cream);
        face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Cube(r, 0f, H - 0.27f, D / 2f + 0.03f, 0.02f, 0.12f, 0.01f, black);                    // hands
        Cube(r, 0.04f, H - 0.27f, D / 2f + 0.03f, 0.08f, 0.02f, 0.01f, black);
        Cube(r, 0f, H * 0.45f, D / 2f - 0.01f, W - 0.2f, 0.7f, 0.01f, glass);                  // pendulum window
        Cube(r, 0f, H * 0.45f, D / 2f - 0.05f, 0.03f, 0.55f, 0.01f, metal);
    }

    static void Cabinet(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H / 2f, 0f, W, H, D, metal);
        for (int i = 0; i < 3; i++)
        {
            float y = H * (0.2f + 0.3f * i);
            Cube(r, 0f, y, D / 2f + 0.005f, W - 0.04f, H / 3f - 0.04f, 0.015f, metal);
            Cube(r, 0f, y + 0.06f, D / 2f + 0.025f, 0.15f, 0.025f, 0.025f, black);
        }
    }

    static void Dresser(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H / 2f, 0f, W, H, D, wood);
        Cube(r, 0f, H + 0.02f, 0f, W + 0.06f, 0.04f, D + 0.04f, dark);
        for (int j = 0; j < 3; j++)
        {
            float y = H * (0.2f + 0.3f * j);
            Cube(r, 0f, y, D / 2f + 0.008f, W - 0.08f, H / 3f - 0.06f, 0.02f, dark);
            foreach (var sx in new[] { -1f, 1f })
                Cube(r, sx * W * 0.2f, y, D / 2f + 0.03f, 0.05f, 0.05f, 0.03f, metal);
        }
        Cube(r, -W * 0.3f, H + 0.14f, 0f, 0.18f, 0.2f, 0.12f, ceramic);                         // trinket
    }

    static void Fridge(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H / 2f, 0f, W, H, D, cream);
        Cube(r, 0f, H * 0.68f, D / 2f + 0.003f, W, 0.015f, 0.01f, black);
        Cube(r, W / 2f - 0.1f, H * 0.82f, D / 2f + 0.03f, 0.03f, 0.35f, 0.04f, metal);
        Cube(r, W / 2f - 0.1f, H * 0.45f, D / 2f + 0.03f, 0.03f, 0.6f, 0.04f, metal);
    }

    static void Stove(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H / 2f, 0f, W, H, D, metal);
        Cube(r, 0f, H + 0.01f, 0f, W, 0.02f, D, black);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cyl(r, sx * W * 0.22f, H + 0.03f, sz * D * 0.2f, 0.18f, 0.02f, dark);
        Cube(r, 0f, H * 0.4f, D / 2f + 0.01f, W - 0.1f, H * 0.5f, 0.02f, black);
        Cube(r, 0f, H * 0.45f, D / 2f + 0.025f, W - 0.3f, H * 0.25f, 0.01f, glass);
        Cube(r, 0f, H * 0.72f, D / 2f + 0.04f, W - 0.2f, 0.03f, 0.03f, metal);
        Cube(r, 0f, H + 0.25f, -D / 2f + 0.02f, W, 0.4f, 0.04f, metal);
    }

    static void UpperCabs(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H / 2f, 0f, W, H, D, wood);
        int n = Mathf.Max(2, Mathf.RoundToInt(W / 0.5f));
        for (int i = 1; i < n; i++)
            Cube(r, -W / 2f + i * W / n, H / 2f, D / 2f + 0.003f, 0.012f, H - 0.06f, 0.01f, dark);
        for (int i = 0; i < n; i++)
            Cube(r, -W / 2f + (i + 0.5f) * W / n + 0.1f, H * 0.3f, D / 2f + 0.015f, 0.02f, 0.12f, 0.02f, metal);
    }

    static void Chair(Transform r, float W, float H, float D)
    {
        float s = W / 0.45f;
        Cube(r, 0f, 0.45f * s, 0f, W, 0.04f * s, D, wood);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cube(r, sx * (W / 2f - 0.03f * s), 0.22f * s, sz * (D / 2f - 0.03f * s), 0.04f * s, 0.45f * s, 0.04f * s, wood);
        foreach (var sx in new[] { -1f, 1f })
            Cube(r, sx * (W / 2f - 0.03f * s), 0.72f * s, -D / 2f + 0.03f * s, 0.04f * s, 0.5f * s, 0.04f * s, wood);
        Cube(r, 0f, 0.62f * s, -D / 2f + 0.03f * s, W - 0.05f * s, 0.1f * s, 0.03f * s, wood);
        Cube(r, 0f, 0.82f * s, -D / 2f + 0.03f * s, W - 0.05f * s, 0.1f * s, 0.03f * s, wood);
    }

    static void Table(Transform r, float W, float H, float D, bool lamp)
    {
        Cube(r, 0f, H - 0.02f, 0f, W, 0.04f, D, wood);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cube(r, sx * (W / 2f - 0.04f), (H - 0.04f) / 2f, sz * (D / 2f - 0.04f), 0.04f, H - 0.04f, 0.04f, wood);
        if (H < 0.9f) Cube(r, 0f, 0.2f, 0f, W - 0.1f, 0.02f, D - 0.1f, wood);
        if (lamp) Lamp(r, 0f, H, 0f);
    }

    static void SideTable(Transform r, float W, float H, float D) { Table(r, W, H, D, true); }
    static void KidTable(Transform r, float W, float H, float D) { Table(r, W, H, D, false); }

    static void Lamp(Transform r, float x, float y, float z)
    {
        Cyl(r, x, y + 0.02f, z, 0.14f, 0.04f, metal);
        Cyl(r, x, y + 0.15f, z, 0.03f, 0.25f, metal);
        Cyl(r, x, y + 0.34f, z, 0.24f, 0.16f, shade);
    }

    static void FloorLamp(Transform r, float W, float H, float D)
    {
        Cyl(r, 0f, 0.02f, 0f, 0.28f, 0.04f, metal);
        Cyl(r, 0f, H / 2f, 0f, 0.03f, H, metal);
        Cyl(r, 0f, H - 0.12f, 0f, 0.36f, 0.26f, shade);
    }

    static void Basket(Transform r, float W, float H, float D)
    {
        Cyl(r, 0f, H / 2f, 0f, W, H, cardboard);
        Cyl(r, 0f, H + 0.01f, 0f, W + 0.04f, 0.03f, wood);
        Cube(r, 0f, H + 0.05f, 0f, W * 0.7f, 0.12f, W * 0.6f, cream, 0f, 25f, 0f);             // dirty laundry
    }

    static void WallShelf(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0f, 0f, W, 0.03f, D, wood);
        Cyl(r, -W * 0.3f, 0.1f, 0f, 0.07f, 0.17f, ceramic);
        Cyl(r, 0f, 0.07f, 0f, 0.05f, 0.11f, glass);
        Cube(r, W * 0.3f, 0.06f, 0f, 0.14f, 0.09f, 0.1f, cream);
    }

    static void Trunk(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H * 0.35f, 0f, W, H * 0.7f, D, wood);
        Cube(r, 0f, H * 0.78f, 0f, W + 0.02f, H * 0.3f, D + 0.02f, dark);
        foreach (var sx in new[] { -1f, 1f })
            Cube(r, sx * W * 0.32f, H * 0.5f, 0f, 0.05f, H, D + 0.04f, metal);
        Cube(r, 0f, H * 0.62f, D / 2f + 0.03f, 0.08f, 0.1f, 0.03f, metal);
    }

    static void Boxes(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.3f, 0f, 0.8f, 0.6f, 0.7f, cardboard, 0f, 8f, 0f);
        Cube(r, 0.1f, 0.8f, 0.05f, 0.6f, 0.4f, 0.55f, cardboard, 0f, -14f, 0f);
        Cube(r, -0.55f, 0.25f, 0.25f, 0.5f, 0.5f, 0.5f, cardboard, 0f, 30f, 0f);
    }

    static void Mannequin(Transform r, float W, float H, float D)
    {
        Cyl(r, 0f, 0.02f, 0f, 0.42f, 0.04f, metal);
        Cyl(r, 0f, 0.6f, 0f, 0.04f, 1.2f, metal);
        Cube(r, 0f, 0.9f, 0f, 0.3f, 0.25f, 0.17f, cream);
        Cube(r, 0f, 1.2f, 0f, 0.34f, 0.5f, 0.19f, cream);
        Sph(r, 0f, 1.6f, 0f, 0.2f, cream);
        Cube(r, 0f, 1.55f, 0f, 0.05f, 0.12f, 0.05f, cream);
    }

    static void Barrel(Transform r, float W, float H, float D)
    {
        Cyl(r, 0f, H / 2f, 0f, W, H, wood);
        Cyl(r, 0f, H * 0.2f, 0f, W + 0.02f, 0.04f, metal);
        Cyl(r, 0f, H * 0.8f, 0f, W + 0.02f, 0.04f, metal);
    }

    static void Workbench(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H - 0.03f, 0f, W, 0.06f, D, wood);
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cube(r, sx * (W / 2f - 0.06f), (H - 0.06f) / 2f, sz * (D / 2f - 0.06f), 0.08f, H - 0.06f, 0.08f, wood);
        Cube(r, 0f, 0.25f, 0f, W - 0.1f, 0.04f, D - 0.1f, wood);
        Cube(r, W * 0.35f, H + 0.07f, D / 2f - 0.1f, 0.12f, 0.14f, 0.12f, metal);              // vise
        Cube(r, -W * 0.2f, H + 0.02f, 0f, 0.3f, 0.03f, 0.04f, metal, 0f, 25f, 0f);             // a saw lying around
    }

    static void MetalRack(Transform r, float W, float H, float D)
    {
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Cube(r, sx * (W / 2f - 0.03f), H / 2f, sz * (D / 2f - 0.03f), 0.05f, H, 0.05f, metal);
        for (int i = 0; i < 4; i++)
        {
            float y = 0.2f + i * 0.55f;
            Cube(r, 0f, y, 0f, W, 0.03f, D, metal);
            if (rnd.NextDouble() < 0.8)
                Cube(r, (float)(rnd.NextDouble() - 0.5) * W * 0.6f, y + 0.12f, 0f, 0.3f + (float)rnd.NextDouble() * 0.3f, 0.22f, D * 0.7f, cardboard);
        }
    }

    static void RockingChair(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.45f, 0f, W - 0.1f, 0.05f, D - 0.2f, wood);
        Cube(r, 0f, 0.78f, -D / 2f + 0.15f, W - 0.1f, 0.6f, 0.04f, wood, -12f, 0f, 0f);
        foreach (var sx in new[] { -1f, 1f })
        {
            float x = sx * (W / 2f - 0.06f);
            Cube(r, x, 0.07f, 0f, 0.04f, 0.05f, D, wood);                                      // runner
            Cube(r, x, 0.25f, 0.22f, 0.04f, 0.4f, 0.04f, wood);
            Cube(r, x, 0.25f, -0.22f, 0.04f, 0.4f, 0.04f, wood);
            Cube(r, x, 0.62f, 0.05f, 0.04f, 0.04f, D * 0.6f, wood);                            // arm
        }
    }

    static void RockingHorse(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.5f, 0f, 0.3f, 0.3f, 0.7f, wood);
        Cube(r, 0f, 0.82f, 0.38f, 0.16f, 0.4f, 0.2f, wood, 25f, 0f, 0f);
        Cube(r, 0f, 1.0f, 0.5f, 0.14f, 0.15f, 0.2f, wood);
        Cube(r, 0f, 0.7f, -0.4f, 0.04f, 0.3f, 0.04f, black, -25f, 0f, 0f);                     // tail
        foreach (var sx in new[] { -1f, 1f })
        {
            Cube(r, sx * 0.22f, 0.07f, 0f, 0.04f, 0.05f, D, wood);                             // runners
            Cube(r, sx * 0.16f, 0.3f, 0.25f, 0.04f, 0.5f, 0.04f, wood);
            Cube(r, sx * 0.16f, 0.3f, -0.25f, 0.04f, 0.5f, 0.04f, wood);
        }
    }

    static void Dollhouse(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H * 0.3f, 0f, W, H * 0.6f, D, cream);
        Cube(r, 0f, H * 0.3f, D / 2f + 0.005f, W - 0.1f, 0.02f, 0.01f, dark);
        Cube(r, -W / 4f, H * 0.82f, 0f, W * 0.6f, 0.04f, D + 0.1f, dark, 0f, 0f, 32f);
        Cube(r, W / 4f, H * 0.82f, 0f, W * 0.6f, 0.04f, D + 0.1f, dark, 0f, 0f, -32f);
        Cube(r, 0f, H * 0.1f, D / 2f + 0.01f, 0.2f, 0.25f, 0.02f, black);                      // door
        foreach (var sx in new[] { -1f, 1f })
        {
            Cube(r, sx * W * 0.3f, H * 0.2f, D / 2f + 0.01f, 0.14f, 0.14f, 0.02f, glass);
            Cube(r, sx * W * 0.3f, H * 0.45f, D / 2f + 0.01f, 0.14f, 0.14f, 0.02f, glass);
        }
        Cube(r, W * 0.3f, H * 1.02f, 0f, 0.1f, 0.2f, 0.1f, dark);                               // chimney
    }

    static void Teddy(Transform r, float W, float H, float D)
    {
        Color brown = new Color(0.4f, 0.27f, 0.16f);
        var m = Mat("Teddy", brown, 0.05f, 0f);
        Sph(r, 0f, 0.14f, 0f, 0.26f, m);
        Sph(r, 0.02f, 0.34f, 0f, 0.18f, m);
        Sph(r, -0.07f, 0.44f, 0f, 0.07f, m);
        Sph(r, 0.1f, 0.44f, 0f, 0.07f, m);
        Sph(r, -0.15f, 0.16f, 0.05f, 0.1f, m);
        Sph(r, 0.18f, 0.12f, 0.03f, 0.1f, m);
        Sph(r, -0.03f, 0.35f, 0.08f, 0.025f, black);                                            // one eye
        Sph(r, 0.07f, 0.35f, 0.08f, 0.025f, black);
    }

    static void Mobile(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, H / 2f, 0f, 0.015f, H, 0.015f, black);
        Cube(r, 0f, 0.3f, 0f, W, 0.015f, 0.015f, wood);
        Cube(r, 0f, 0.3f, 0f, 0.015f, 0.015f, W, wood);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            float x = Mathf.Cos(a) * W / 2f, z = Mathf.Sin(a) * W / 2f;
            float len = 0.15f + 0.08f * (i % 2);
            Cube(r, x, 0.3f - len / 2f, z, 0.005f, len, 0.005f, black);
            Cube(r, x, 0.3f - len - 0.04f, z, 0.08f, 0.08f, 0.08f, books[(i * 2) % books.Length], 20f, i * 30f, 10f);
        }
    }

    static void ToyBlocks(Transform r, float W, float H, float D)
    {
        for (int i = 0; i < 9; i++)
        {
            float s = 0.07f + (float)rnd.NextDouble() * 0.05f;
            float x = ((float)rnd.NextDouble() - 0.5f) * W;
            float z = ((float)rnd.NextDouble() - 0.5f) * D;
            Cube(r, x, s / 2f + 0.02f, z, s, s, s, books[rnd.Next(books.Length)], 0f, (float)rnd.NextDouble() * 90f, 0f);
        }
        Cube(r, 0f, 0.06f, 0f, 0.07f, 0.07f, 0.07f, fabric, 0f, 40f, 0f);
        Cube(r, 0.01f, 0.13f, 0f, 0.07f, 0.07f, 0.07f, fabric2, 0f, 10f, 0f);
    }

    static void Ladder(Transform r, float W, float H, float D)
    {
        var t = new GameObject("tilt").transform;
        t.SetParent(r, false);
        t.localRotation = Quaternion.Euler(-14f, 0f, 0f);
        foreach (var sx in new[] { -1f, 1f })
            Cube(t, sx * 0.2f, H / 2f, 0f, 0.04f, H, 0.04f, wood);
        for (int i = 0; i < 7; i++)
            Cube(t, 0f, 0.3f + i * 0.32f, 0f, 0.4f, 0.03f, 0.04f, wood);
    }

    static void Globe(Transform r, float W, float H, float D)
    {
        Cyl(r, 0f, 0.02f, 0f, 0.36f, 0.04f, dark);
        Cyl(r, 0f, 0.4f, 0f, 0.04f, 0.76f, dark);
        Sph(r, 0f, 0.85f, 0f, 0.42f, fabric2);
        Cube(r, 0f, 0.85f, 0f, 0.015f, 0.5f, 0.015f, metal, 0f, 0f, 23f);                       // axis
    }

    static void RugA(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.01f, 0f, W, 0.02f, D, rugA);
        Cube(r, 0f, 0.016f, 0f, W - 0.3f, 0.02f, D - 0.3f, dark);
        Cube(r, 0f, 0.02f, 0f, W - 0.55f, 0.02f, D - 0.55f, rugA);
    }

    static void RugB(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.01f, 0f, W, 0.02f, D, rugB);
        Cube(r, 0f, 0.016f, 0f, W - 0.25f, 0.02f, D - 0.25f, cream);
        Cube(r, 0f, 0.02f, 0f, W - 0.45f, 0.02f, D - 0.45f, rugB);
    }

    static void RugC(Transform r, float W, float H, float D)
    {
        Cube(r, 0f, 0.01f, 0f, W, 0.02f, D, rugC);
        Cube(r, 0f, 0.016f, 0f, W - 0.1f, 0.02f, D - 0.14f, dark);
        Cube(r, 0f, 0.02f, 0f, W - 0.2f, 0.02f, D - 0.28f, rugC);
    }

    // ================================================================== picture frames

    // Hangs frames along one wall. They differ in size, height and spacing and hang a little crooked.
    //   alongX: the wall runs along x (fixedCoord is z), otherwise along z (fixedCoord is x)
    //   normalSign: +1 or -1, the direction the wall faces into the room
    //   a..b: stretch of wall to fill;  yMin..yMax: height of the frame centers above the floor
    //   blocked: pairs (start, end) where nothing should hang (doors, tall furniture)
    static void Gallery(Transform parent, float floorY, bool alongX, float fixedCoord, float normalSign,
                        float a, float b, float yMin, float yMax, params float[] blocked)
    {
        float cur = a + (float)rnd.NextDouble() * 0.4f;
        int guard = 0;

        while (cur < b && guard++ < 60)
        {
            int idx = rnd.Next(paints.Length);
            float s = 0.4f + (float)rnd.NextDouble() * 0.45f;
            if (rnd.NextDouble() < 0.2) s *= 1.45f;
            float w = PORTRAIT[idx] ? s : s * 1.5f;
            float h = PORTRAIT[idx] ? s * 1.25f : s;

            bool moved = false;
            for (int i = 0; i + 1 < blocked.Length; i += 2)
            {
                if (cur < blocked[i + 1] && cur + w > blocked[i])
                {
                    cur = blocked[i + 1] + 0.25f;
                    moved = true;
                    break;
                }
            }
            if (moved) continue;
            if (cur + w > b) break;

            if (rnd.NextDouble() < 0.12)   // leave some wall empty
            {
                cur += w + 0.3f;
                continue;
            }

            float cx = cur + w / 2f;
            float yc = yMin + (float)rnd.NextDouble() * (yMax - yMin);
            yc = Mathf.Min(yc, 3.0f - h / 2f);
            yc = Mathf.Max(yc, 0.95f + h / 2f);

            float maxTilt = rnd.NextDouble() < 0.25 ? 14f : 5f;
            float tilt = ((float)rnd.NextDouble() - 0.5f) * 2f * maxTilt;

            Vector3 pos = alongX ? new Vector3(cx, floorY + yc, fixedCoord) : new Vector3(fixedCoord, floorY + yc, cx);
            Vector3 n = alongX ? new Vector3(0f, 0f, normalSign) : new Vector3(normalSign, 0f, 0f);

            Frame(parent, pos, n, w, h, tilt, paints[idx]);

            cur += w + 0.25f + (float)rnd.NextDouble() * 1.6f;
        }
    }

    // One frame. pos = point on the wall, normal = direction the wall faces into the room.
    static Transform Frame(Transform parent, Vector3 pos, Vector3 normal, float w, float h, float tilt, Material canvas)
    {
        var root = new GameObject("Frame").transform;
        root.SetParent(parent, false);
        root.position = pos + normal * 0.03f;
        root.rotation = Quaternion.LookRotation(normal, Vector3.up) * Quaternion.Euler(0f, 0f, tilt);

        float t = 0.06f;
        Cube(root, 0f, h / 2f - t / 2f, 0f, w, t, 0.05f, frameWood);
        Cube(root, 0f, -(h / 2f - t / 2f), 0f, w, t, 0.05f, frameWood);
        Cube(root, -(w / 2f - t / 2f), 0f, 0f, t, h - 2f * t, 0.05f, frameWood);
        Cube(root, (w / 2f - t / 2f), 0f, 0f, t, h - 2f * t, 0.05f, frameWood);

        // the picture: a quad whose visible side faces the room
        Prim(root, PrimitiveType.Quad, "Picture", new Vector3(0f, 0f, 0.012f),
             new Vector3(w - 2f * t + 0.01f, h - 2f * t + 0.01f, 1f), canvas, new Vector3(0f, 180f, 0f));

        // nail
        Cube(root, 0f, h / 2f + 0.1f, -0.01f, 0.025f, 0.025f, 0.03f, metal);
        return root;
    }

    // ================================================================== building helpers

    static Transform Group(Transform parent, string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        return g;
    }

    // Builds a piece of furniture at (x, y, z) facing yaw, with the given footprint. solid = add one collider.
    static Transform Put(Transform room, string name, Build fn, float x, float y, float z, float yaw,
                         float W, float H, float D, bool solid = true, bool tall = false)
    {
        var g = new GameObject(name).transform;
        g.SetParent(room, false);
        g.position = new Vector3(x, y, z);
        g.rotation = Quaternion.Euler(0f, yaw, 0f);

        fn(g, W, H, D);
        WoodUV(g);

        if (solid)
        {
            Bounds b = LocalBounds(g);
            var bc = g.gameObject.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
            if (tall) g.gameObject.layer = obstacleLayer;
        }

        return g;
    }

    static Bounds LocalBounds(Transform root)
    {
        bool has = false;
        Bounds b = new Bounds();

        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            Bounds wb = r.bounds;
            Vector3 mn = wb.min, mx = wb.max;
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z);
                Vector3 lp = root.InverseTransformPoint(p);
                if (!has) { b = new Bounds(lp, Vector3.zero); has = true; }
                else b.Encapsulate(lp);
            }
        }

        return b;
    }

    static GameObject Cube(Transform p, float x, float y, float z, float sx, float sy, float sz, Material m,
                           float rx = 0f, float ry = 0f, float rz = 0f)
    {
        return Prim(p, PrimitiveType.Cube, "part", new Vector3(x, y, z), new Vector3(sx, sy, sz), m, new Vector3(rx, ry, rz));
    }

    // diameter dia, real height h (a Unity cylinder is 2 m tall at scale 1)
    static GameObject Cyl(Transform p, float x, float y, float z, float dia, float h, Material m)
    {
        return Prim(p, PrimitiveType.Cylinder, "part", new Vector3(x, y, z), new Vector3(dia, h / 2f, dia), m, Vector3.zero);
    }

    static GameObject Sph(Transform p, float x, float y, float z, float dia, Material m)
    {
        return Prim(p, PrimitiveType.Sphere, "part", new Vector3(x, y, z), new Vector3(dia, dia, dia), m, Vector3.zero);
    }

    static GameObject Prim(Transform p, PrimitiveType t, string name, Vector3 lp, Vector3 scale, Material m, Vector3 euler)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name;
        g.transform.SetParent(p, false);
        g.transform.localPosition = lp;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.transform.localScale = scale;

        var c = g.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);

        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    // box projection in world meters for the bigger wooden parts, so the wood texture does not stretch
    static void WoodUV(Transform root)
    {
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var r = mf.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial != wood) continue;
            if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;

            Vector3 s = mf.transform.lossyScale;
            if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) < 0.3f) continue;

            Mesh mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = "UV_" + root.name;

            var verts = mesh.vertices;
            var normals = mesh.normals;
            var uv = new Vector2[verts.Length];

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 wp = mf.transform.TransformPoint(verts[i]);
                Vector3 wn = mf.transform.TransformDirection(normals[i]);
                float ax = Mathf.Abs(wn.x), ay = Mathf.Abs(wn.y), az = Mathf.Abs(wn.z);
                float u, v;

                if (ax >= ay && ax >= az) { u = wp.z; v = wp.y; }
                else if (ay >= az)        { u = wp.x; v = wp.z; }
                else                      { u = wp.x; v = wp.y; }

                uv[i] = new Vector2(u / 1.5f, v / 1.5f);
            }

            mesh.uv = uv;
            mf.sharedMesh = mesh;
        }
    }

    // ================================================================== materials

    static void LoadMats()
    {
        wood = FindPackMat("M_wood");
        if (wood == null) wood = Mat("Wood", new Color(0.28f, 0.17f, 0.10f), 0.1f, 0f);

        dark = Mat("DarkWood", new Color(0.12f, 0.08f, 0.06f), 0.15f, 0f);
        fabric = Mat("FabricRed", new Color(0.32f, 0.12f, 0.11f), 0.05f, 0f);
        fabric2 = Mat("FabricGreen", new Color(0.18f, 0.24f, 0.20f), 0.05f, 0f);
        cream = Mat("Cream", new Color(0.62f, 0.58f, 0.48f), 0.05f, 0f);
        ceramic = Mat("Ceramic", new Color(0.74f, 0.74f, 0.70f), 0.5f, 0f);
        metal = Mat("Metal", new Color(0.30f, 0.30f, 0.32f), 0.6f, 0.7f);
        black = Mat("Black", new Color(0.04f, 0.04f, 0.045f), 0.2f, 0f);
        glass = Mat("Mirror", new Color(0.07f, 0.09f, 0.11f), 0.95f, 0.4f);
        frameWood = Mat("Frame", new Color(0.10f, 0.065f, 0.04f), 0.25f, 0f);
        cardboard = Mat("Cardboard", new Color(0.42f, 0.32f, 0.20f), 0.02f, 0f);
        rugA = Mat("RugRed", new Color(0.28f, 0.07f, 0.07f), 0.02f, 0f);
        rugB = Mat("RugGreen", new Color(0.12f, 0.18f, 0.15f), 0.02f, 0f);
        rugC = Mat("RugRunner", new Color(0.22f, 0.10f, 0.08f), 0.02f, 0f);
        water = Mat("Water", new Color(0.04f, 0.07f, 0.05f), 0.9f, 0f);

        shade = Mat("LampShade", new Color(0.9f, 0.75f, 0.5f), 0.1f, 0f);
        shade.EnableKeyword("_EMISSION");
        if (shade.HasProperty("_EmissionColor")) shade.SetColor("_EmissionColor", new Color(0.9f, 0.75f, 0.5f) * 0.35f);

        books = new Material[]
        {
            Mat("Book1", new Color(0.35f, 0.10f, 0.08f), 0.1f, 0f),
            Mat("Book2", new Color(0.10f, 0.18f, 0.14f), 0.1f, 0f),
            Mat("Book3", new Color(0.30f, 0.22f, 0.10f), 0.1f, 0f),
            Mat("Book4", new Color(0.12f, 0.12f, 0.22f), 0.1f, 0f),
            Mat("Book5", new Color(0.40f, 0.36f, 0.28f), 0.1f, 0f),
            Mat("Book6", new Color(0.18f, 0.08f, 0.14f), 0.1f, 0f),
        };

        // pictures: painting_1 .. painting_6 if they exist in the project, otherwise plain dark colors
        Color[] fallback =
        {
            new Color(0.20f, 0.14f, 0.09f), new Color(0.10f, 0.15f, 0.12f), new Color(0.22f, 0.10f, 0.08f),
            new Color(0.15f, 0.13f, 0.09f), new Color(0.16f, 0.16f, 0.12f), new Color(0.12f, 0.14f, 0.18f),
        };

        paints = new Material[6];
        for (int i = 0; i < 6; i++)
        {
            var tex = FindTexture("painting_" + (i + 1));
            paints[i] = tex != null ? PaintMat("Painting" + (i + 1), tex) : Mat("Painting" + (i + 1), fallback[i], 0.1f, 0f);
        }
    }

    static Material Mat(string name, Color c, float smooth, float metallic)
    {
        EnsureFolder(GEN);
        string path = GEN + "/Decor_" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }

        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);

        EditorUtility.SetDirty(m);
        return m;
    }

    static Material PaintMat(string name, Texture2D tex)
    {
        var m = Mat(name, Color.white, 0.1f, 0f);
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D FindTexture(string fileName)
    {
        foreach (var guid in AssetDatabase.FindAssets(fileName + " t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == fileName)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        return null;
    }

    // a material of your asset pack, by exact name (ignores the Tiled and Generated folders)
    static Material FindPackMat(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != name) continue;
            if (path.Contains("/Tiled/") || path.Contains("/Generated/")) continue;
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static GameObject FindRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }
}
#endif
