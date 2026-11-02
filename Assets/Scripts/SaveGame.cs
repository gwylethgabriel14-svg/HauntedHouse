using UnityEngine;

public static class SaveGame
{
    const string ExistsKey = "save_exists";
    const string PagesKey = "save_pages";
    const string HasKeyKey = "save_hasKey";
    const string PosX = "save_px";
    const string PosY = "save_py";
    const string PosZ = "save_pz";
    const string Yaw = "save_yaw";

    public static bool loadRequested;   // set by the menu's CONTINUE button
    public static bool continued;       // true while the current run was loaded from a save

    public static bool Exists => PlayerPrefs.GetInt(ExistsKey, 0) == 1;
    public static int Pages => PlayerPrefs.GetInt(PagesKey, 0);
    public static bool HasKey => PlayerPrefs.GetInt(HasKeyKey, 0) == 1;

    public static void Save(int pages, bool hasKey)
    {
        PlayerPrefs.SetInt(ExistsKey, 1);
        PlayerPrefs.SetInt(PagesKey, pages);
        PlayerPrefs.SetInt(HasKeyKey, hasKey ? 1 : 0);

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Vector3 p = player.transform.position;
            PlayerPrefs.SetFloat(PosX, p.x);
            PlayerPrefs.SetFloat(PosY, p.y);
            PlayerPrefs.SetFloat(PosZ, p.z);
            PlayerPrefs.SetFloat(Yaw, player.transform.eulerAngles.y);
        }

        PlayerPrefs.Save();
    }

    public static bool TryGetPosition(out Vector3 pos, out float yaw)
    {
        pos = Vector3.zero;
        yaw = 0f;
        if (!PlayerPrefs.HasKey(PosX)) return false;

        pos = new Vector3(PlayerPrefs.GetFloat(PosX), PlayerPrefs.GetFloat(PosY), PlayerPrefs.GetFloat(PosZ));
        yaw = PlayerPrefs.GetFloat(Yaw);
        return true;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(ExistsKey);
        PlayerPrefs.DeleteKey(PagesKey);
        PlayerPrefs.DeleteKey(HasKeyKey);
        PlayerPrefs.DeleteKey(PosX);
        PlayerPrefs.DeleteKey(PosY);
        PlayerPrefs.DeleteKey(PosZ);
        PlayerPrefs.DeleteKey(Yaw);
        PlayerPrefs.Save();

        loadRequested = false;
        continued = false;
    }
}