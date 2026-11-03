using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    enum State { Patrol, Idle, Investigate, Chase, Search }

    [Header("Refs")]
    public Transform player;
    public Transform[] waypoints;
    public LayerMask obstacleMask;

    [Header("Speeds")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float speedPerPage = 0.35f;

    [Header("Senses")]
    public float sightRange = 12f;
    public float sightAngle = 100f;
    public float catchDistance = 1.6f;

    [Header("Timers")]
    public float loseSightTime = 3f;
    public float searchTime = 6f;

    [Header("Fair start")]
    public float startDelay = 8f;

    [Header("Wandering")]
    public Vector2 idleTime = new Vector2(2f, 5f);
    public float lookAngle = 70f;
    public float lookSpeed = 1.2f;
    [Range(0f, 1f)] public float huntChance = 0.15f;
    public float huntChancePerPage = 0.1f;
    public float searchRadius = 5f;
    public float stuckTime = 3f;

    [Header("Patrol fallback (used if no waypoints are assigned)")]
    public float fallbackPatrolRadius = 15f;

    [Header("Animations")]
    public Animator anim;
    public string walkingParameter = "IsWalking";
    public string attackParameter = "Attack";

    [Header("Debug")]
    public bool debugLogs = true;

    // Script property (not an Animator parameter). Other scripts may use it.
    public bool IsChasing => state == State.Chase;

    public event System.Action ChaseStarted;

    NavMeshAgent agent;
    PlayerController pc;

    State state;

    int wpIndex = -1;
    int prevWpIndex = -1;
    int pages;

    float timer;
    float baseChase;
    float basePatrol;

    bool waiting;
    bool hasCaughtPlayer;
    bool warnedNoNavMesh;

    float idleStart;
    float idleEnd;
    float idleYaw;

    float stuckTimer;

    Vector3 lastKnownPos;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }
        if (player != null) pc = player.GetComponent<PlayerController>();

        EnsureAnimator();

        baseChase = chaseSpeed;
        basePatrol = patrolSpeed;

        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                transform.position,
                out hit,
                6f,
                NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        if (debugLogs)
        {
            Debug.Log("EnemyAI '" + name + "' started. On NavMesh: " + agent.isOnNavMesh +
                      ", waypoints: " + (waypoints == null ? 0 : waypoints.Length), this);

            if (waypoints == null || waypoints.Length == 0)
            {
                Debug.LogWarning("EnemyAI '" + name + "' has NO waypoints assigned. Using random patrol points instead.", this);
            }

            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning("EnemyAI '" + name + "' is NOT on a NavMesh. Bake the NavMesh (Window > AI > Navigation) and place the enemy on it.", this);
            }
        }

        GoPatrol();

        if (startDelay > 0f)
        {
            waiting = true;
            agent.isStopped = true;
        }
    }

    void Update()
    {
        if (player == null || pc == null) return;

        if (!agent.isOnNavMesh)
        {
            if (debugLogs && !warnedNoNavMesh)
            {
                warnedNoNavMesh = true;
                Debug.LogWarning("EnemyAI '" + name + "' is not on the NavMesh, so it cannot move.", this);
            }

            return;
        }

        // Wait before the enemy starts moving.
        if (waiting)
        {
            UpdateAnimations();

            if (Time.timeSinceLevelLoad < startDelay)
                return;

            waiting = false;
            agent.isStopped = false;
        }

        // Check if the enemy caught the player.
        if (IsPlayerInCatchRange())
        {
            if (!hasCaughtPlayer)
            {
                hasCaughtPlayer = true;

                if (anim != null)
                {
                    anim.SetBool(walkingParameter, false);
                    anim.SetTrigger(attackParameter);
                }

                GameManager.Instance.Lose();
            }

            return;
        }

        bool sees = CanSeePlayer();

        bool hears = !pc.IsHidden &&
                     Vector3.Distance(
                         transform.position,
                         player.position) < pc.CurrentNoise;

        switch (state)
        {
            case State.Patrol:

                if (sees)
                {
                    StartChase();
                }
                else if (hears)
                {
                    Investigate(player.position);
                }
                else if (Arrived(0.6f))
                {
                    StartIdle();
                }
                else
                {
                    CheckStuck();
                }

                break;

            case State.Idle:

                if (sees)
                {
                    StartChase();
                }
                else if (hears)
                {
                    Investigate(player.position);
                }
                else
                {
                    float yaw = idleYaw +
                        Mathf.Sin((Time.time - idleStart) * lookSpeed)
                        * lookAngle;

                    transform.rotation =
                        Quaternion.Euler(0f, yaw, 0f);

                    if (Time.time >= idleEnd)
                    {
                        GoPatrol();
                    }
                }

                break;

            case State.Investigate:

                if (sees)
                {
                    StartChase();
                }
                else if (Arrived(0.8f))
                {
                    StartSearch();
                }

                break;

            case State.Chase:

                if (sees)
                {
                    timer = 0f;
                    lastKnownPos = player.position;

                    agent.SetDestination(player.position);
                }
                else
                {
                    timer += Time.deltaTime;

                    if (timer > loseSightTime)
                    {
                        StartSearch();
                    }
                }

                break;

            case State.Search:

                timer -= Time.deltaTime;

                if (sees)
                {
                    StartChase();
                }
                else if (timer <= 0f)
                {
                    GoPatrol();
                }
                else if (Arrived(0.8f))
                {
                    PickSearchPoint();
                }

                break;
        }

        UpdateAnimations();
    }

    void EnsureAnimator()
    {
        if (anim != null) return;

        anim = GetComponentInChildren<Animator>(true);

        foreach (var name in new[] { "WomanModel", "EnemyVisual", "GhostlyWoman", "CharacterModel", "Model" })
        {
            if (anim != null) break;
            var child = transform.Find(name);
            if (child != null) anim = child.GetComponent<Animator>();
        }

        if (anim == null)
        {
            var controller = Resources.Load<RuntimeAnimatorController>("WomanController");
            if (controller != null)
            {
                anim = gameObject.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
            }
        }

        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            if (anim.runtimeAnimatorController == null)
            {
                var fallback = Resources.Load<RuntimeAnimatorController>("WomanController");
                if (fallback != null) anim.runtimeAnimatorController = fallback;
            }
        }
    }

    // Updates the Animator parameters.
    void UpdateAnimations()
    {
        if (anim == null) return;

        bool isWalking =
            !waiting &&
            !hasCaughtPlayer &&
            agent.velocity.magnitude > 0.15f;

        anim.SetBool(walkingParameter, isWalking);
        if (state == State.Chase)
            anim.speed = 1.15f;
        else if (isWalking)
            anim.speed = Mathf.Clamp(agent.speed / Mathf.Max(0.5f, patrolSpeed), 0.85f, 1.4f);
        else
            anim.speed = 1f;
    }

    bool Arrived(float dist)
    {
        return !agent.pathPending &&
               agent.remainingDistance <= dist;
    }

    void CheckStuck()
    {
        if (agent.velocity.sqrMagnitude < 0.05f)
        {
            stuckTimer += Time.deltaTime;

            if (stuckTimer > stuckTime)
            {
                stuckTimer = 0f;
                PickWaypoint();
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    bool IsPlayerInCatchRange()
    {
        if (pc.IsHidden) return false;

        Vector3 flat = player.position - transform.position;

        flat.y = 0f;

        return flat.magnitude < catchDistance;
    }

    bool CanSeePlayer()
    {
        if (pc.IsHidden) return false;

        Vector3 eye =
            transform.position + Vector3.up * 1.6f;

        Vector3 target =
            player.position + Vector3.up * 1f;

        Vector3 dir = target - eye;

        float dist = dir.magnitude;

        if (dist > sightRange) return false;

        if (Vector3.Angle(transform.forward, dir) >
            sightAngle * 0.5f)
        {
            return false;
        }

        if (Physics.Raycast(
            eye,
            dir.normalized,
            dist,
            obstacleMask))
        {
            return false;
        }

        return true;
    }

    void GoPatrol()
    {
        state = State.Patrol;

        agent.speed = patrolSpeed;

        stuckTimer = 0f;

        PickWaypoint();
    }

    void StartIdle()
    {
        state = State.Idle;

        agent.ResetPath();

        idleStart = Time.time;

        idleEnd = idleStart +
            Random.Range(idleTime.x, idleTime.y);

        idleYaw = transform.eulerAngles.y;
    }

    void PickWaypoint()
    {
        // No waypoints assigned: wander to random points on the NavMesh.
        if (waypoints == null || waypoints.Length == 0)
        {
            PickRandomPatrolPoint();
            return;
        }

        int pick = -1;

        float hunt =
            huntChance + huntChancePerPage * pages;

        // Sometimes head toward the player's area.
        if (!pc.IsHidden && Random.value < hunt)
        {
            pick = NearestWaypoint(player.position);
        }

        // Otherwise choose a random waypoint.
        if (pick < 0 || pick == wpIndex)
        {
            for (int i = 0; i < 12; i++)
            {
                int r = Random.Range(0, waypoints.Length);

                if (waypoints.Length > 2 &&
                    (r == wpIndex || r == prevWpIndex))
                {
                    continue;
                }

                pick = r;

                break;
            }

            if (pick < 0)
            {
                pick = Random.Range(0, waypoints.Length);
            }
        }

        prevWpIndex = wpIndex;

        wpIndex = pick;

        if (waypoints[wpIndex] == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning("EnemyAI '" + name + "': waypoint " + wpIndex + " is empty (None) in the Inspector.", this);
            }

            PickRandomPatrolPoint();
            return;
        }

        agent.SetDestination(
            waypoints[wpIndex].position);
    }

    void PickRandomPatrolPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 p =
                transform.position +
                Random.insideUnitSphere * fallbackPatrolRadius;

            p.y = transform.position.y;

            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                p,
                out hit,
                3f,
                NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    int NearestWaypoint(Vector3 pos)
    {
        int best = -1;

        float bestDist = float.MaxValue;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            float d =
                (waypoints[i].position - pos).sqrMagnitude;

            if (d < bestDist)
            {
                bestDist = d;

                best = i;
            }
        }

        return best;
    }

    void StartChase()
    {
        state = State.Chase;

        agent.speed = chaseSpeed;

        timer = 0f;

        lastKnownPos = player.position;

        if (ChaseStarted != null)
        {
            ChaseStarted();
        }
    }

    void StartSearch()
    {
        state = State.Search;

        agent.speed = patrolSpeed;

        timer = searchTime;

        PickSearchPoint();
    }

    void PickSearchPoint()
    {
        Vector3 p =
            lastKnownPos +
            Random.insideUnitSphere * searchRadius;

        p.y = lastKnownPos.y;

        NavMeshHit hit;

        if (NavMesh.SamplePosition(
            p,
            out hit,
            3f,
            NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    void Investigate(Vector3 pos)
    {
        state = State.Investigate;

        agent.speed = patrolSpeed * 1.4f;

        lastKnownPos = pos;

        agent.SetDestination(pos);
    }

    // Called by thrown bottles.
    public void HearNoise(Vector3 pos)
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            if (debugLogs)
            {
                Debug.LogWarning("HearNoise reached '" + name + "' but it is not ready. " +
                                 "Either it is not on the NavMesh, or this is NOT the Enemy in the scene " +
                                 "(maybe a prefab or a duplicate). Click this message to see which object.", this);
            }

            return;
        }

        if (state == State.Chase) return;

        if (debugLogs)
        {
            Debug.Log("Enemy '" + name + "' heard a noise and is investigating.", this);
        }

        Investigate(pos);
    }

    // Called by GameManager when a page is collected.
    public void Enrage(int pagesCollected)
    {
        pages = pagesCollected;

        chaseSpeed =
            baseChase + speedPerPage * pagesCollected;

        patrolSpeed =
            basePatrol +
            speedPerPage * 0.5f * pagesCollected;

        if (state == State.Chase)
        {
            agent.speed = chaseSpeed;
        }
        else if (state == State.Patrol)
        {
            agent.speed = patrolSpeed;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            catchDistance);

        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            sightRange);
    }
}