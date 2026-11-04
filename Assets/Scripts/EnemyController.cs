using UnityEngine;
using UnityEngine.AI;

// Hinahabol ang player. Pag binaril = Knockdown (tumba). Pag natapakan ang trap = Trap (hindi makagalaw).
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public Transform target;          // Player (auto-find via tag "Player")
    public Transform visual;          // model na itutumba (child ng enemy)
    public float repathInterval = 0.2f;
    public float fallSpeed = 6f;

    NavMeshAgent agent;
    float downUntil, trappedUntil, nextPath;
    Quaternion standRotation;

    public bool IsDown => Time.time < downUntil;
    public bool IsTrapped => Time.time < trappedUntil;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (visual == null) visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
        standRotation = visual.localRotation;
    }

    void Start()
    {
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
    }

    void Update()
    {
        bool frozen = IsDown || IsTrapped;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = frozen;
            if (!frozen && target != null && Time.time >= nextPath)
            {
                agent.SetDestination(target.position);
                nextPath = Time.time + repathInterval;
            }
        }
        agent.updateRotation = !IsDown;

        // tumba pabalik / tayo ulit
        Quaternion goal = IsDown ? standRotation * Quaternion.Euler(-90f, 0f, 0f) : standRotation;
        visual.localRotation = Quaternion.Slerp(visual.localRotation, goal, fallSpeed * Time.deltaTime);
    }

    public void Knockdown(float seconds)
    {
        downUntil = Time.time + seconds;
        if (agent.isOnNavMesh) agent.velocity = Vector3.zero;
    }

    public void Trap(float seconds)
    {
        trappedUntil = Time.time + seconds;
        if (agent.isOnNavMesh) agent.velocity = Vector3.zero;
    }
}
