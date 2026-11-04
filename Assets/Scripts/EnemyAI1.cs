using System.Collections.Generic;
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

    [Header("Multi-floor (taas/baba)")]
    [Tooltip("Max na pagkakaiba ng taas para mahuli ang player. Mas mataas dito = ibang palapag, hindi pa abot.")]
    public float catchHeight = 1.5f;
    [Tooltip("Max na pagkakaiba ng taas para marinig ang yabag ng player.")]
    public float hearHeight = 2.5f;
    [Tooltip("Max na pagkakaiba ng taas para makita ang player.")]
    public float sightHeight = 3.5f;

    [Header("Timers")]
    public float loseSightTime = 3f;
    public float searchTime = 6f;

    [Header("Fair start")]
    public float startDelay = 8f;

    [Header("Random spawn")]
    [Tooltip("Kapag naka-on, random ang pwesto ng enemy sa simula ng laro (hindi kung saan lang siya nakalagay sa scene).")]
    public bool randomSpawn = true;
    [Tooltip("Pinakamalapit na distansya mula sa player (bedroom) na pwede siyang mag-spawn.")]
    public float minSpawnDistance = 25f;
    [Tooltip("Opsyonal. Kung may nakalagay dito, ito lang ang pagpipilian. Kung wala, ang mga waypoint ang gagamitin.")]
    public Transform[] spawnPoints;
    [Tooltip("Gagamitin lang kung walang spawn points at walang waypoints.")]
    public float fallbackSpawnRadius = 60f;

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
    public string walkingParameter = "IsWalking";   // Bool
    public string runningParameter = "IsRunning";   // Bool (optional, true habang humahabol)
    public string attackParameter = "Attack";       // Trigger
    [Tooltip("Naka-off dapat. Kapag naka-on, nag-aaway ang animation at NavMeshAgent.")]
    public bool forceNoRootMotion = true;
    [Tooltip("Para hindi huminto ang animation kapag wala sa screen.")]
    public bool alwaysAnimate = true;

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

    // animator
    readonly HashSet<string> animParams = new HashSet<string>();
    bool animReady;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }
        if (player != null) pc = player.GetComponent<PlayerController>();

        EnsureAnimator();

        if (player == null)
        {
            Debug.LogError("EnemyAI '" + name + "': walang Player. I-assign ang 'player' o lagyan ng tag na Player.", this);
            enabled = false;
            return;
        }

        pc = player.GetComponentInParent<PlayerController>();
        if (pc == null) pc = player.GetComponentInChildren<PlayerController>();

        if (pc == null)
        {
            Debug.LogError("EnemyAI '" + name + "': walang PlayerController sa player.", this);
            enabled = false;
            return;
        }

        SetupAnimator();

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

        if (randomSpawn && agent.isOnNavMesh) PlaceAtRandomSpawn();

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

    // ------------------------------------------------ random spawn
    void PlaceAtRandomSpawn()
    {
        var valid = new List<Vector3>();
        Vector3 farthest = transform.position;
        float farthestDist = -1f;

        // mga pagpipilian: spawnPoints, kung wala ay waypoints
        Transform[] pool = (spawnPoints != null && spawnPoints.Length > 0) ? spawnPoints : waypoints;

        if (pool != null && pool.Length > 0)
        {
            foreach (var t in pool)
            {
                if (t == null) continue;
                Consider(t.position, valid, ref farthest, ref farthestDist);
            }
        }
        else
        {
            // walang points: random na pwesto sa NavMesh
            for (int i = 0; i < 60; i++)
            {
                Vector3 p = transform.position + Random.insideUnitSphere * fallbackSpawnRadius;
                Consider(p, valid, ref farthest, ref farthestDist);
            }
        }

        Vector3 spot;
        if (valid.Count > 0)
        {
            spot = valid[Random.Range(0, valid.Count)];
        }
        else if (farthestDist >= 0f)
        {
            // walang umabot sa minimum na layo, kunin ang pinakamalayo
            spot = farthest;
            if (debugLogs)
                Debug.LogWarning("EnemyAI '" + name + "': walang spawn point na " + minSpawnDistance +
                                 "+ ang layo sa player. Ginamit ang pinakamalayo (" + farthestDist.ToString("0.0") + ").", this);
        }
        else
        {
            return;
        }

        agent.Warp(spot);
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        if (debugLogs)
            Debug.Log("EnemyAI '" + name + "' nag-spawn sa " + spot + ", layo sa player: " +
                      Vector3.Distance(spot, player.position).ToString("0.0"), this);
    }

    void Consider(Vector3 pos, List<Vector3> valid, ref Vector3 farthest, ref float farthestDist)
    {
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(pos, out hit, 3f, NavMesh.AllAreas)) return;

        float d = Vector3.Distance(hit.position, player.position);

        if (d > farthestDist)
        {
            farthestDist = d;
            farthest = hit.position;
        }

        if (d >= minSpawnDistance) valid.Add(hit.position);
    }

    // ------------------------------------------------ animator
    void SetupAnimator()
    {
        animReady = false;
        animParams.Clear();

        if (anim == null) anim = GetComponentInChildren<Animator>(true);

        if (anim == null)
        {
            if (debugLogs)
                Debug.LogWarning("EnemyAI '" + name + "': walang Animator. Buksan ang Haunted House > Enemy Animation Fixer.", this);
            return;
        }

        if (forceNoRootMotion) anim.applyRootMotion = false;
        if (alwaysAnimate) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (anim.runtimeAnimatorController == null)
        {
            if (debugLogs)
                Debug.LogWarning("EnemyAI '" + name + "': walang Controller ang Animator. Buksan ang Haunted House > Enemy Animation Fixer.", this);
            return;
        }

        foreach (var p in anim.parameters) animParams.Add(p.name);
        animReady = true;

        if (debugLogs)
        {
            if (!animParams.Contains(walkingParameter))
                Debug.LogWarning("EnemyAI '" + name + "': walang parameter na '" + walkingParameter + "' sa Animator Controller.", this);
            if (!animParams.Contains(attackParameter))
                Debug.LogWarning("EnemyAI '" + name + "': walang parameter na '" + attackParameter + "' sa Animator Controller.", this);
        }
    }

    void SetBool(string param, bool value)
    {
        if (animReady && !string.IsNullOrEmpty(param) && animParams.Contains(param))
            anim.SetBool(param, value);
    }

    void SetTrigger(string param)
    {
        if (animReady && !string.IsNullOrEmpty(param) && animParams.Contains(param))
            anim.SetTrigger(param);
    }

    // Updates the Animator parameters.
    void UpdateAnimations()
    {
        if (!animReady) return;

        bool moving =
            !waiting &&
            !hasCaughtPlayer &&
            !agent.isStopped &&
            agent.velocity.magnitude > 0.1f;

        SetBool(walkingParameter, moving);
        SetBool(runningParameter, moving && state == State.Chase);
    }

    // ------------------------------------------------ update
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

                SetBool(walkingParameter, false);
                SetBool(runningParameter, false);
                SetTrigger(attackParameter);

                GameManager.Instance.Lose();
            }

            return;
        }

        bool sees = CanSeePlayer();

        float heightDiff = Mathf.Abs(player.position.y - transform.position.y);

        // Hindi maririnig ang yabag kung nasa ibang palapag.
        bool hears = !pc.IsHidden &&
                     heightDiff < hearHeight &&
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

        // Ibang palapag? Hindi pa abot.
        if (Mathf.Abs(flat.y) > catchHeight) return false;

        flat.y = 0f;

        return flat.magnitude < catchDistance;
    }

    bool CanSeePlayer()
    {
        if (pc.IsHidden) return false;

        // Hindi nakikita ang player sa ibang palapag.
        if (Mathf.Abs(player.position.y - transform.position.y) > sightHeight)
            return false;

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
        // (Sa parehong palapag lang, para hindi niya "alam" kung nasaan ka sa ibang floor.)
        if (!pc.IsHidden &&
            Mathf.Abs(player.position.y - transform.position.y) < hearHeight &&
            Random.value < hunt)
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