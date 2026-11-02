using UnityEngine;

// Put on a Light. When the enemy is nearby the light flickers.
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    public EnemyAI enemy;
    public float triggerRange = 9f;
    public float baseIntensity = -1f;   // -1 = use whatever the light starts at

    Light lamp;
    float nextChange;

    void Awake()
    {
        lamp = GetComponent<Light>();
        if (baseIntensity < 0f) baseIntensity = lamp.intensity;
    }

    void Start()
    {
        if (enemy == null) enemy = FindFirstObjectByType<EnemyAI>();
    }

    void Update()
    {
        if (enemy == null) return;

        float d = Vector3.Distance(transform.position, enemy.transform.position);
        if (d > triggerRange)
        {
            lamp.intensity = baseIntensity;
            return;
        }

        if (Time.time < nextChange) return;
        nextChange = Time.time + Random.Range(0.04f, 0.18f);
        lamp.intensity = Random.value < 0.45f ? baseIntensity * Random.Range(0f, 0.25f) : baseIntensity;
    }
}
