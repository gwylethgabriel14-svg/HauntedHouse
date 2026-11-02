using UnityEngine;

public class Flashlight : MonoBehaviour
{
    public Light spot;
    public float maxBattery = 100f;
    public float drainPerSecond = 2f;
    public float flickerBelow = 15f;

    public float Battery { get; private set; }

    bool on;
    float baseIntensity;

    void Start()
    {
        Battery = maxBattery;
        baseIntensity = spot.intensity;
        spot.enabled = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F) && Battery > 0f)
        {
            on = !on;
            spot.enabled = on;
        }

        if (!on) return;

        Battery -= drainPerSecond * Time.deltaTime;

        if (Battery <= 0f)
        {
            Battery = 0f;
            on = false;
            spot.enabled = false;
            return;
        }

        spot.intensity = Battery < flickerBelow
            ? baseIntensity * Random.Range(0.3f, 1f)
            : baseIntensity;
    }

    public void AddBattery(float amount)
    {
        Battery = Mathf.Min(maxBattery, Battery + amount);
    }

    // used by the save system
    public void SetBattery(float value)
    {
        Battery = Mathf.Clamp(value, 0f, maxBattery);
    }
}