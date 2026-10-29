using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class GreyboxCityGenerator : EditorWindow
{
    // Small, highly detailed scale
    private int gridWidth = 5;
    private int gridDepth = 5;
    private float lotSize = 16f;
    private float roadWidth = 6f;

    // Color Palette Cache
    private Material matBaseAsphalt;
    private Material matSidewalk;
    private Material matTerracotta;
    private Material matCreamStucco;
    private Material matDeepTeal;
    private Material matWarmWood;
    private Material matGlassBlue;
    private Material matGlassDark;
    private Material matAwningRed;
    private Material matAwningYellow;
    private Material matPoolWater;
    private Material matGoldTrim;

    [MenuItem("Tools/Generate Colorful City")]
    public static void ShowWindow()
    {
        GetWindow<GreyboxCityGenerator>("Intricate Colorful City");
    }

    private void OnGUI()
    {
        GUILayout.Label("Boutique City Showcase", EditorStyles.boldLabel);
        gridWidth = EditorGUILayout.IntSlider("Grid Width", gridWidth, 3, 8);
        gridDepth = EditorGUILayout.IntSlider("Grid Depth", gridDepth, 3, 8);
        lotSize = EditorGUILayout.FloatField("Lot Footprint Size", lotSize);
        roadWidth = EditorGUILayout.FloatField("Street Width", roadWidth);

        EditorGUILayout.Space(14);

        if (GUILayout.Button("Generate Intricate City", GUILayout.Height(42)))
        {
            GenerateCity();
        }

        if (GUILayout.Button("Clear City", GUILayout.Height(26)))
        {
            ClearCity();
        }
    }

    private void InitializeMaterials()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");

        matBaseAsphalt = CreateMat(sh, new Color(0.15f, 0.16f, 0.18f), 0.1f);
        matSidewalk = CreateMat(sh, new Color(0.72f, 0.72f, 0.70f), 0.1f);
        matTerracotta = CreateMat(sh, new Color(0.78f, 0.38f, 0.25f), 0.2f);
        matCreamStucco = CreateMat(sh, new Color(0.92f, 0.90f, 0.85f), 0.15f);
        matDeepTeal = CreateMat(sh, new Color(0.18f, 0.35f, 0.40f), 0.4f);
        matWarmWood = CreateMat(sh, new Color(0.45f, 0.28f, 0.18f), 0.3f);
        matGlassBlue = CreateMat(sh, new Color(0.20f, 0.45f, 0.70f), 0.85f);
        matGlassDark = CreateMat(sh, new Color(0.12f, 0.15f, 0.20f), 0.9f);
        matAwningRed = CreateMat(sh, new Color(0.85f, 0.22f, 0.18f), 0.1f);
        matAwningYellow = CreateMat(sh, new Color(0.92f, 0.75f, 0.20f), 0.1f);
        matPoolWater = CreateMat(sh, new Color(0.22f, 0.65f, 0.75f), 0.95f);
        matGoldTrim = CreateMat(sh, new Color(0.85f, 0.68f, 0.25f), 0.6f);
    }

    private Material CreateMat(Shader s, Color col, float smooth)
    {
        Material m = new Material(s) { color = col };
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        return m;
    }

    private void GenerateCity()
    {
        ClearCity();
        InitializeMaterials();

        GameObject root = new GameObject("Colorful_City_Showcase");
        Undo.RegisterCreatedObjectUndo(root, "Generate Colorful City");

        float fullLot = lotSize + 3.5f; // Sidewalk buffer
        float step = fullLot + roadWidth;
        Vector3 center = new Vector3((gridWidth - 1) * step * 0.5f, 0, (gridDepth - 1) * step * 0.5f);

        // 1. Street Grid Base
        CreateBox(root.transform, "Asphalt_Roads", center + new Vector3(0, -0.5f, 0), new Vector3(gridWidth * step + 40, 1f, gridDepth * step + 40), matBaseAsphalt);

        // 2. Generate Intricate Buildings per lot
        Transform buildingsRoot = new GameObject("Buildings").transform;
        buildingsRoot.parent = root.transform;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                Vector3 pos = new Vector3(x * step, 0, z * step);

                // Create Sidewalk Pad
                CreateBox(buildingsRoot, "Sidewalk", pos + new Vector3(0, 0.1f, 0), new Vector3(fullLot, 0.2f, fullLot), matSidewalk);

                // Alternate building archetypes for variety
                int styleIndex = (x + z) % 4;
                if (styleIndex == 0) BuildModernGlassTower(buildingsRoot, pos, lotSize);
                else if (styleIndex == 1) BuildTerracottaApartment(buildingsRoot, pos, lotSize);
                else if (styleIndex == 2) BuildTealCornerPlaza(buildingsRoot, pos, lotSize);
                else BuildScandinavianLoft(buildingsRoot, pos, lotSize);
            }
        }

        Selection.activeGameObject = root;
    }

    // ==================== INTRICATE BUILDING DESIGNS ====================

    private void BuildModernGlassTower(Transform parent, Vector3 basePos, float size)
    {
        GameObject bldg = new GameObject("Modern_Glass_Tower");
        bldg.transform.parent = parent;
        Vector3 ground = basePos + new Vector3(0, 0.2f, 0);

        float h = 42f;
        float w = size * 0.85f;

        // Ground Lobby (Dark Glass & Gold Trim)
        CreateBox(bldg.transform, "Lobby", ground + new Vector3(0, 3f, 0), new Vector3(w, 6f, w), matGlassDark);
        CreateBox(bldg.transform, "Gold_Canopy", ground + new Vector3(0, 6.2f, w * 0.45f), new Vector3(w * 0.5f, 0.4f, 2.5f), matGoldTrim);

        // Main Blue Glass Tower Shaft
        CreateBox(bldg.transform, "Tower_Shaft", ground + new Vector3(0, 3 + (h - 3) * 0.5f, 0), new Vector3(w * 0.88f, h - 6f, w * 0.88f), matGlassBlue);

        // Floor Slabs (White trim every 4 meters)
        for (float y = 10f; y < h; y += 4f)
        {
            CreateBox(bldg.transform, "Floor_Trim", ground + new Vector3(0, y, 0), new Vector3(w * 0.92f, 0.25f, w * 0.92f), matCreamStucco);
        }

        // Rooftop Pool & Lounge
        CreateBox(bldg.transform, "Roof_Deck", ground + new Vector3(0, h, 0), new Vector3(w * 0.6f, 1f, w * 0.6f), matCreamStucco);
        CreateBox(bldg.transform, "Rooftop_Pool", ground + new Vector3(0, h + 0.6f, 0), new Vector3(w * 0.35f, 0.4f, w * 0.35f), matPoolWater);
    }

    private void BuildTerracottaApartment(Transform parent, Vector3 basePos, float size)
    {
        GameObject bldg = new GameObject("Terracotta_Apartment");
        bldg.transform.parent = parent;
        Vector3 ground = basePos + new Vector3(0, 0.2f, 0);

        float w = size * 0.88f;
        float h = 28f;

        // Base Stucco
        CreateBox(bldg.transform, "Base_Mass", ground + new Vector3(0, h * 0.5f, 0), new Vector3(w, h, w), matTerracotta);

        // Intricate Balconies & Colorful Awnings
        for (float y = 7f; y < h - 4f; y += 7f)
        {
            // Balcony slab
            CreateBox(bldg.transform, "Balcony_Slab", ground + new Vector3(0, y, w * 0.42f), new Vector3(w * 0.6f, 0.4f, 1.8f), matCreamStucco);
            // Railing
            CreateBox(bldg.transform, "Balcony_Rail", ground + new Vector3(0, y + 0.9f, w * 0.5f), new Vector3(w * 0.6f, 1.2f, 0.15f), matWarmWood);
            // Colorful Awning above window
            Material awningMat = (y % 14f == 0) ? matAwningRed : matAwningYellow;
            CreateBox(bldg.transform, "Awning", ground + new Vector3(0, y + 2.8f, w * 0.4f), new Vector3(w * 0.5f, 0.3f, 1.5f), awningMat);
        }

        // Rooftop Terracotta Tiles & Chimneys
        CreateBox(bldg.transform, "Roof_Cap", ground + new Vector3(0, h + 0.6f, 0), new Vector3(w * 1.05f, 1.2f, w * 1.05f), matWarmWood);
        CreateBox(bldg.transform, "Chimney", ground + new Vector3(w * 0.3f, h + 2.5f, 0), new Vector3(1.2f, 3f, 1.2f), matTerracotta);
    }

    private void BuildTealCornerPlaza(Transform parent, Vector3 basePos, float size)
    {
        GameObject bldg = new GameObject("Teal_Commercial");
        bldg.transform.parent = parent;
        Vector3 ground = basePos + new Vector3(0, 0.2f, 0);

        float w = size * 0.9f;
        float h = 20f;

        // Ground Retail Arcade (Glass front with Teal columns)
        CreateBox(bldg.transform, "Retail_Glass", ground + new Vector3(0, 3.5f, 0), new Vector3(w * 0.96f, 7f, w * 0.96f), matGlassBlue);
        CreateBox(bldg.transform, "Arcade_Col1", ground + new Vector3(-w * 0.42f, 3.5f, w * 0.48f), new Vector3(0.8f, 7f, 0.8f), matDeepTeal);
        CreateBox(bldg.transform, "Arcade_Col2", ground + new Vector3(w * 0.42f, 3.5f, w * 0.48f), new Vector3(0.8f, 7f, 0.8f), matDeepTeal);

        // Upper Teal Office Floors
        CreateBox(bldg.transform, "Office_Block", ground + new Vector3(0, 7 + (h - 7) * 0.5f, 0), new Vector3(w, h - 7f, w), matDeepTeal);

        // Vertical Gold Accent Fins
        CreateBox(bldg.transform, "Gold_Fin", ground + new Vector3(0, h * 0.5f + 3f, w * 0.51f), new Vector3(0.4f, h - 6f, 0.3f), matGoldTrim);

        // Rooftop Garden Patio
        CreateBox(bldg.transform, "Roof_Patio", ground + new Vector3(0, h + 0.4f, 0), new Vector3(w * 0.8f, 0.8f, w * 0.8f), matSidewalk);
    }

    private void BuildScandinavianLoft(Transform parent, Vector3 basePos, float size)
    {
        GameObject bldg = new GameObject("Scandinavian_Loft");
        bldg.transform.parent = parent;
        Vector3 ground = basePos + new Vector3(0, 0.2f, 0);

        float w = size * 0.86f;
        float h = 22f;

        // Cream Stucco Base
        CreateBox(bldg.transform, "Stucco_Base", ground + new Vector3(0, h * 0.5f, 0), new Vector3(w, h, w), matCreamStucco);

        // Warm Wood Panel accents
        CreateBox(bldg.transform, "Wood_Panel_Wing", ground + new Vector3(w * 0.3f, h * 0.5f, 0), new Vector3(w * 0.35f, h * 0.95f, w * 0.95f), matWarmWood);

        // Large Grid Windows
        CreateBox(bldg.transform, "Big_Window", ground + new Vector3(-w * 0.15f, h * 0.5f, w * 0.51f), new Vector3(w * 0.35f, h * 0.6f, 0.2f), matGlassDark);

        // Pitched Gable Roof
        CreateBox(bldg.transform, "Roof_Gable", ground + new Vector3(0, h + 1.8f, 0), new Vector3(w * 1.04f, 3.5f, w * 1.04f), matTerracotta);
    }

    // ==================== HELPER METHODS ====================

    private GameObject CreateBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.parent = parent;
        box.transform.position = pos;
        box.transform.localScale = scale;
        if (mat != null) box.GetComponent<Renderer>().sharedMaterial = mat;
        return box;
    }

    private void ClearCity()
    {
        GameObject existing = GameObject.Find("Colorful_City_Showcase");
        if (existing != null) Undo.DestroyObjectImmediate(existing);
    }
}