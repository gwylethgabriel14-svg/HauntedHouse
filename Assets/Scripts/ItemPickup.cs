using UnityEngine;

public class ItemPickup : MonoBehaviour, IInteractable
{
    public enum Type { Battery, Bottle }
    public Type type;
    public float batteryAmount = 40f;
    public Sprite icon;              // optional, icon sa hotbar (bote)

    public static readonly Color BottleGlassColor = new Color(0.22f, 0.76f, 0.38f, 1f);

    static Shader PreferredShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("URP/Lit")
            ?? Shader.Find("Standard");
    }

    void Awake()
    {
        EnsurePickupVisual();
    }

    public string Prompt => type == Type.Battery ? "Take battery" : "Take bottle";

    public void Interact(PlayerController player)
    {
        if (type == Type.Battery)
        {
            var fl = player.GetComponentInChildren<Flashlight>();
            if (fl != null) fl.AddBattery(batteryAmount);
        }
        else
        {
            var inv = Inventory.Instance;
            if (inv != null)
            {
                // puno ang hotbar: iiwan sa sahig ang bote
                if (!inv.AddItem("Bottle", icon, null, true)) return;
            }
            else
            {
                var th = player.GetComponent<ThrowItem>();
                if (th != null) th.bottles++;
            }
        }

        Destroy(gameObject);
    }

    public static void EnsureBottleVisual(GameObject root)
    {
        if (root == null) return;
        if (root.transform.Find("BottleVis") != null) return;

        var visual = new GameObject("BottleVis");
        visual.transform.SetParent(root.transform, false);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(visual.transform, false);
        body.transform.localScale = new Vector3(0.2f, 0.38f, 0.2f);
        body.transform.localRotation = Quaternion.identity;
        var glassMat = new Material(PreferredShader());
        glassMat.color = BottleGlassColor;
        glassMat.SetFloat("_Mode", 3f);
        glassMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        glassMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glassMat.SetInt("_ZWrite", 0);
        body.GetComponent<Renderer>().sharedMaterial = glassMat;
        DestroyImmediate(body.GetComponent<Collider>());

        var neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        neck.name = "Neck";
        neck.transform.SetParent(visual.transform, false);
        neck.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        neck.transform.localScale = new Vector3(0.09f, 0.12f, 0.09f);
        var neckMat = new Material(PreferredShader());
        neckMat.color = new Color(0.15f, 0.14f, 0.12f, 1f);
        neck.GetComponent<Renderer>().sharedMaterial = neckMat;
        DestroyImmediate(neck.GetComponent<Collider>());

        var cork = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cork.name = "Cork";
        cork.transform.SetParent(visual.transform, false);
        cork.transform.localPosition = new Vector3(0f, 0.36f, 0f);
        cork.transform.localScale = new Vector3(0.09f, 0.06f, 0.09f);
        var corkMat = new Material(PreferredShader());
        corkMat.color = new Color(0.54f, 0.38f, 0.19f, 1f);
        cork.GetComponent<Renderer>().sharedMaterial = corkMat;
        DestroyImmediate(cork.GetComponent<Collider>());

        var label = GameObject.CreatePrimitive(PrimitiveType.Quad);
        label.name = "Label";
        label.transform.SetParent(visual.transform, false);
        label.transform.localPosition = new Vector3(0f, 0f, 0.11f);
        label.transform.localScale = new Vector3(0.2f, 0.26f, 1f);
        var labelMat = new Material(PreferredShader());
        labelMat.color = new Color(0.9f, 0.88f, 0.74f, 1f);
        label.GetComponent<Renderer>().sharedMaterial = labelMat;
        DestroyImmediate(label.GetComponent<Collider>());

        var mr = root.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
        var mf = root.GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = null;
    }

    void EnsurePickupVisual()
    {
        if (transform.Find("PickupVisual") != null) return;

        var root = new GameObject("PickupVisual");
        root.transform.SetParent(transform, false);

        if (type == Type.Battery)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.30f, 0.12f, 0.30f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mat = new Material(PreferredShader());
            mat.color = new Color(0.95f, 0.82f, 0.22f, 1f);
            var r = body.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            DestroyImmediate(body.GetComponent<Collider>());

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Cap";
            cap.transform.SetParent(root.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            cap.transform.localScale = new Vector3(0.20f, 0.04f, 0.20f);
            cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var capMat = new Material(PreferredShader());
            capMat.color = new Color(0.65f, 0.65f, 0.65f, 1f);
            cap.GetComponent<Renderer>().sharedMaterial = capMat;
            DestroyImmediate(cap.GetComponent<Collider>());

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stripe.name = "Stripe";
            stripe.transform.SetParent(root.transform, false);
            stripe.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            stripe.transform.localScale = new Vector3(0.18f, 0.02f, 0.18f);
            stripe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var stripeMat = new Material(PreferredShader());
            stripeMat.color = new Color(0.18f, 0.72f, 0.26f, 1f);
            stripe.GetComponent<Renderer>().sharedMaterial = stripeMat;
            DestroyImmediate(stripe.GetComponent<Collider>());
        }
        else
        {
            EnsureBottleVisual(root);
        }

        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
        var mf = GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = null;
    }
}
