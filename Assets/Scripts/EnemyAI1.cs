
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    private enum State
    {
        Patrol,
        Idle,
        Investigate,
        Chase,
        Search
    }

    [Header("References")]
    public Transform player;
    public Transform[] waypoints;
    public LayerMask obstacleMask;

    [Header("Speeds")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4.5f;
    public float speedPerPage = 0.35f;

    [Header("WIDER DETECTION")]
    public float sightRange = 22f;

    [Range(20f, 360f)]
    public float sightAngle = 150f;

    public float catchDistance = 1.6f;

    [Header("Multi-floor Detection")]
    public float catchHeight = 1.5f;
    public float hearHeight = 2.5f;
    public float sightHeight = 3.5f;

    [Header("AGGRESSIVE CHASE")]
    [Tooltip("Seconds before abandoning the chase after losing the player.")]
    public float loseSightTime = 18f;

    [Tooltip("How long Eleanor searches after losing a chase.")]
    public float searchTime = 12f;

    [Tooltip("While chasing, can track player movement through nearby sounds.")]
    public bool trackRunningPlayer = true;

    public float chaseHearingRange = 20f;
    public float chaseHearingHeight = 6f;

    [Tooltip("How often to update the path to the target.")]
    public float chaseRepathInterval = 0.15f;

    [Tooltip("Briefly keep pursuing the player even after line of sight is broken.")]
    public float pursuitTrackingSeconds = 6f;

    [Tooltip("Enable if you want Eleanor to keep chasing for a long time.")]
    public bool persistentChase = true;

    [Header("Fair Start")]
    public float startDelay = 8f;

    [Header("Random Spawn")]
    public bool randomSpawn = true;
    public float minSpawnDistance = 25f;
    public Transform[] spawnPoints;
    public float fallbackSpawnRadius = 60f;

    [Header("Wandering")]
    public Vector2 idleTime = new Vector2(2f, 5f);
    public float lookAngle = 70f;
    public float lookSpeed = 1.2f;

    [Range(0f, 1f)]
    public float huntChance = 0.15f;

    public float huntChancePerPage = 0.1f;
    public float searchRadius = 5f;
    public float stuckTime = 3f;
    public float fallbackPatrolRadius = 15f;

    [Header("Animations")]
    public Animator anim;
    public string walkingParameter = "IsWalking";
    public string runningParameter = "IsRunning";
    public string attackParameter = "Attack";

    public bool forceNoRootMotion = true;
    public bool alwaysAnimate = true;

    [Header("Debug")]
    public bool debugLogs = true;

    public bool IsChasing
    {
        get { return state == State.Chase; }
    }

    public event Action ChaseStarted;

    private NavMeshAgent agent;
    private PlayerController pc;

    private State state = State.Patrol;

    private int wpIndex = -1;
    private int prevWpIndex = -1;
    private int pages;

    private float basePatrol;
    private float baseChase;

    private float stateTimer;
    private float lostTimer;
    private float nextRepath;
    private float stuckTimer;

    private float idleStart;
    private float idleEnd;
    private float idleYaw;

    private float chaseStartedAt;
    private float lastSeenAt;
    private Vector3 lastKnownPos;

    private bool waiting;
    private bool caughtPlayer;
    private bool warnedNoNavMesh;

    private readonly HashSet<string> animParameters =
        new HashSet<string>();

    private bool animatorReady;

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (anim == null)
            anim = GetComponentInChildren<Animator>(true);
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject found = null;

            try
            {
                found = GameObject.FindGameObjectWithTag(
                    "Player");
            }
            catch (UnityException)
            {
                // Player tag has not been created.
            }

            if (found == null)
                found = GameObject.Find("Player");

            if (found != null)
                player = found.transform;
        }

        if (player != null)
        {
            pc = player.GetComponent<PlayerController>();

            if (pc == null)
                pc = player.GetComponentInParent<PlayerController>();

            if (pc == null)
                pc = player.GetComponentInChildren<PlayerController>();

            if (pc != null)
                player = pc.transform;
        }

        if (player == null || pc == null)
        {
            Debug.LogError(
                "[ENEMY AI] Player or PlayerController " +
                "not found. Assign the real Player " +
                "in Woman > EnemyAI.",
                this);

            enabled = false;
            return;
        }

        SetupAnimator();

        basePatrol = patrolSpeed;
        baseChase = chaseSpeed;

        if (agent.enabled && !agent.isOnNavMesh)
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

        if (randomSpawn && agent.isOnNavMesh)
            PlaceAtRandomSpawn();

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.stoppingDistance = 0.45f;
            agent.autoBraking = true;

            GoPatrol();

            if (startDelay > 0f)
            {
                waiting = true;
                agent.isStopped = true;
            }
        }
        else
        {
            Debug.LogWarning(
                "[ENEMY AI] Eleanor is not on a NavMesh. " +
                "Bake the NavMesh before testing chase.",
                this);
        }

        if (debugLogs)
        {
            Debug.Log(
                "[ENEMY AI] Ready. Sight: " +
                sightRange +
                " | Angle: " +
                sightAngle +
                " | Chase memory: " +
                loseSightTime +
                "s",
                this);
        }
    }

    // =====================================================
    // RANDOM SPAWN
    // =====================================================

    private void PlaceAtRandomSpawn()
    {
        List<Vector3> valid = new List<Vector3>();

        Vector3 farthest = transform.position;
        float farthestDistance = -1f;

        Transform[] pool =
            spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints
                : waypoints;

        if (pool != null && pool.Length > 0)
        {
            foreach (Transform point in pool)
            {
                if (point == null)
                    continue;

                ConsiderSpawn(
                    point.position,
                    valid,
                    ref farthest,
                    ref farthestDistance);
            }
        }
        else
        {
            for (int i = 0; i < 60; i++)
            {
                Vector3 position =
                    transform.position +
                    UnityEngine.Random.insideUnitSphere *
                    fallbackSpawnRadius;

                ConsiderSpawn(
                    position,
                    valid,
                    ref farthest,
                    ref farthestDistance);
            }
        }

        if (valid.Count > 0)
        {
            agent.Warp(
                valid[UnityEngine.Random.Range(
                    0,
                    valid.Count)]);
        }
        else if (farthestDistance >= 0f)
        {
            agent.Warp(farthest);
        }

        transform.rotation = Quaternion.Euler(
            0f,
            UnityEngine.Random.Range(0f, 360f),
            0f);
    }

    private void ConsiderSpawn(
        Vector3 position,
        List<Vector3> valid,
        ref Vector3 farthest,
        ref float farthestDistance)
    {
        NavMeshHit hit;

        if (!NavMesh.SamplePosition(
            position,
            out hit,
            3f,
            NavMesh.AllAreas))
        {
            return;
        }

        float distance =
            Vector3.Distance(hit.position, player.position);

        if (distance > farthestDistance)
        {
            farthestDistance = distance;
            farthest = hit.position;
        }

        if (distance >= minSpawnDistance)
            valid.Add(hit.position);
    }

    // =====================================================
    // ANIMATION
    // =====================================================

    private void SetupAnimator()
    {
        animatorReady = false;
        animParameters.Clear();

        if (anim == null)
            anim = GetComponentInChildren<Animator>(true);

        if (anim == null)
        {
            RuntimeAnimatorController controller =
                Resources.Load<RuntimeAnimatorController>(
                    "WomanController");

            if (controller != null)
            {
                anim = GetComponent<Animator>();

                if (anim == null)
                    anim = gameObject.AddComponent<Animator>();

                anim.runtimeAnimatorController = controller;
            }
        }

        if (anim == null)
            return;

        if (anim.runtimeAnimatorController == null)
        {
            anim.runtimeAnimatorController =
                Resources.Load<RuntimeAnimatorController>(
                    "WomanController");
        }

        if (forceNoRootMotion)
            anim.applyRootMotion = false;

        if (alwaysAnimate)
            anim.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;

        if (anim.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter parameter
            in anim.parameters)
        {
            animParameters.Add(parameter.name);
        }

        animatorReady = true;
    }

    private void SetBool(string parameter, bool value)
    {
        if (animatorReady &&
            !string.IsNullOrEmpty(parameter) &&
            animParameters.Contains(parameter))
        {
            anim.SetBool(parameter, value);
        }
    }

    private void SetTrigger(string parameter)
    {
        if (animatorReady &&
            !string.IsNullOrEmpty(parameter) &&
            animParameters.Contains(parameter))
        {
            anim.SetTrigger(parameter);
        }
    }

    private void UpdateAnimations()
    {
        if (!animatorReady ||
            agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        bool moving =
            !waiting &&
            !caughtPlayer &&
            !agent.isStopped &&
            agent.velocity.magnitude > 0.1f;

        SetBool(walkingParameter, moving);

        SetBool(
            runningParameter,
            moving && state == State.Chase);

        if (!moving || waiting || caughtPlayer)
        {
            anim.speed = 1f;
        }
        else if (state == State.Chase)
        {
            anim.speed = 1.15f;
        }
        else
        {
            anim.speed = Mathf.Clamp(
                agent.speed / Mathf.Max(0.5f, patrolSpeed),
                0.85f,
                1.4f);
        }
    }

    // =====================================================
    // MAIN AI UPDATE
    // =====================================================

    private void Update()
    {
        if (player == null || pc == null)
            return;

        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            if (debugLogs && !warnedNoNavMesh)
            {
                warnedNoNavMesh = true;

                Debug.LogWarning(
                    "[ENEMY AI] Not on NavMesh!",
                    this);
            }

            return;
        }

        if (waiting)
        {
            if (Time.timeSinceLevelLoad < startDelay)
            {
                UpdateAnimations();
                return;
            }

            waiting = false;
            agent.isStopped = false;
        }

        if (caughtPlayer)
            return;

        if (IsPlayerInCatchRange())
        {
            CatchPlayer();
            return;
        }

        bool sees = CanSeePlayer();
        bool hears = CanHearPlayer();

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
                    float yaw =
                        idleYaw +
                        Mathf.Sin(
                            (Time.time - idleStart) *
                            lookSpeed) * lookAngle;

                    transform.rotation =
                        Quaternion.Euler(0f, yaw, 0f);

                    if (Time.time >= idleEnd)
                        GoPatrol();
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
                UpdateChase(sees, hears);
                break;

            case State.Search:
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
                    stateTimer -= Time.deltaTime;

                    if (stateTimer <= 0f)
                        GoPatrol();
                    else if (Arrived(0.8f))
                        PickSearchPoint();
                }
                break;
        }

        UpdateAnimations();
    }

    // =====================================================
    // PERSISTENT CHASE
    // =====================================================

    private void UpdateChase(bool sees, bool hears)
    {
        agent.speed = chaseSpeed;
        agent.isStopped = false;

        float heightDifference =
            Mathf.Abs(
                player.position.y - transform.position.y);

        float distance =
            Vector3.Distance(
                player.position,
                transform.position);

        bool closeEnoughToHear =
            trackRunningPlayer &&
            !pc.IsHidden &&
            distance <= chaseHearingRange &&
            heightDifference <= chaseHearingHeight;

        // Fresh visual contact resets chase memory.
        if (sees)
        {
            lostTimer = 0f;
            lastSeenAt = Time.time;
            lastKnownPos = player.position;
        }
        else
        {
            lostTimer += Time.deltaTime;

            // While close or during the first few
            // seconds of lost contact, Eleanor keeps
            // trying to track the moving Player.
            if (closeEnoughToHear &&
                (hears ||
                 Time.time - lastSeenAt <=
                 pursuitTrackingSeconds))
            {
                lastKnownPos = player.position;
            }
        }

        if (Time.time >= nextRepath)
        {
            TrySetDestination(lastKnownPos);

            nextRepath =
                Time.time +
                Mathf.Max(0.05f, chaseRepathInterval);
        }

        // Eleanor no longer gives up immediately
        // just because the player turned a corner.
        if (!persistentChase)
        {
            if (lostTimer >= loseSightTime)
                StartSearch();

            return;
        }

        if (lostTimer >= loseSightTime)
        {
            // If she can still hear you nearby,
            // continue chasing.
            if (closeEnoughToHear && hears)
            {
                lostTimer =
                    Mathf.Max(
                        0f,
                        loseSightTime -
                        2f);

                lastKnownPos = player.position;
            }
            else
            {
                StartSearch();
            }
        }
    }

    private bool TrySetDestination(Vector3 position)
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return false;
        }

        NavMeshHit hit;

        if (NavMesh.SamplePosition(
            position,
            out hit,
            2f,
            NavMesh.AllAreas))
        {
            return agent.SetDestination(hit.position);
        }

        // Fallback to the last valid route target.
        return agent.SetDestination(position);
    }

    // =====================================================
    // SIGHT / HEARING / CATCH
    // =====================================================

    private bool CanSeePlayer()
    {
        if (pc.IsHidden)
            return false;

        if (Mathf.Abs(
            player.position.y - transform.position.y)
            > sightHeight)
        {
            return false;
        }

        Vector3 eye =
            transform.position + Vector3.up * 1.6f;

        Vector3 target =
            player.position + Vector3.up * 1f;

        Vector3 direction = target - eye;

        float distance = direction.magnitude;

        if (distance < 0.01f || distance > sightRange)
            return false;

        float angle =
            Vector3.Angle(
                transform.forward,
                direction);

        if (angle > sightAngle * 0.5f)
            return false;

        // Structural walls and other Obstacle layers
        // still block direct vision.
        if (Physics.Raycast(
            eye,
            direction.normalized,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return true;
    }

    private bool CanHearPlayer()
    {
        if (pc.IsHidden)
            return false;

        if (Mathf.Abs(
            player.position.y - transform.position.y)
            > hearHeight)
        {
            return false;
        }

        float distance =
            Vector3.Distance(
                transform.position,
                player.position);

        return distance < pc.CurrentNoise;
    }

    private bool IsPlayerInCatchRange()
    {
        if (pc.IsHidden)
            return false;

        Vector3 difference =
            player.position - transform.position;

        if (Mathf.Abs(difference.y) > catchHeight)
            return false;

        difference.y = 0f;

        if (difference.magnitude >= catchDistance)
            return false;

        // No grabbing through a solid wall.
        Vector3 eye =
            transform.position + Vector3.up * 1.3f;

        Vector3 target =
            player.position + Vector3.up * 1f;

        Vector3 direction = target - eye;

        if (Physics.Raycast(
            eye,
            direction.normalized,
            direction.magnitude,
            obstacleMask,
            QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return true;
    }

    private void CatchPlayer()
    {
        if (caughtPlayer)
            return;

        caughtPlayer = true;

        agent.isStopped = true;
        agent.ResetPath();

        SetBool(walkingParameter, false);
        SetBool(runningParameter, false);
        SetTrigger(attackParameter);

        if (GameManager.Instance != null)
            GameManager.Instance.Lose();
    }

    // =====================================================
    // PATROL
    // =====================================================

    private void GoPatrol()
    {
        state = State.Patrol;
        stateTimer = 0f;
        stuckTimer = 0f;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = patrolSpeed;
        }

        PickWaypoint();
    }

    private void StartIdle()
    {
        state = State.Idle;

        if (agent.isOnNavMesh)
            agent.ResetPath();

        idleStart = Time.time;

        idleEnd =
            idleStart +
            UnityEngine.Random.Range(
                idleTime.x,
                idleTime.y);

        idleYaw = transform.eulerAngles.y;
    }

    private void PickWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            PickRandomPatrolPoint();
            return;
        }

        int pick = -1;

        float hunt = Mathf.Clamp01(
            huntChance +
            huntChancePerPage * pages);

        if (!pc.IsHidden &&
            Mathf.Abs(
                player.position.y - transform.position.y)
            <= hearHeight &&
            UnityEngine.Random.value < hunt)
        {
            pick = NearestWaypoint(player.position);
        }

        if (pick < 0 || pick == wpIndex)
        {
            for (int i = 0; i < 12; i++)
            {
                int candidate =
                    UnityEngine.Random.Range(
                        0,
                        waypoints.Length);

                if (waypoints.Length > 2 &&
                    (candidate == wpIndex ||
                     candidate == prevWpIndex))
                {
                    continue;
                }

                pick = candidate;
                break;
            }
        }

        if (pick < 0)
            pick = UnityEngine.Random.Range(
                0,
                waypoints.Length);

        prevWpIndex = wpIndex;
        wpIndex = pick;

        Transform waypoint = waypoints[pick];

        if (waypoint != null)
        {
            TrySetDestination(waypoint.position);
        }
        else
        {
            PickRandomPatrolPoint();
        }
    }

    private void PickRandomPatrolPoint()
    {
        for (int i = 0; i < 12; i++)
        {
            Vector3 point =
                transform.position +
                UnityEngine.Random.insideUnitSphere *
                fallbackPatrolRadius;

            point.y = transform.position.y;

            NavMeshHit hit;

            if (NavMesh.SamplePosition(
                point,
                out hit,
                3f,
                NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                return;
            }
        }
    }

    private int NearestWaypoint(Vector3 position)
    {
        int best = -1;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform waypoint = waypoints[i];

            if (waypoint == null)
                continue;

            float distance =
                (waypoint.position - position).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private bool Arrived(float threshold)
    {
        return agent.isOnNavMesh &&
               !agent.pathPending &&
               agent.hasPath &&
               agent.remainingDistance <= threshold;
    }

    private void CheckStuck()
    {
        if (agent.velocity.sqrMagnitude < 0.05f)
        {
            stuckTimer += Time.deltaTime;

            if (stuckTimer >= stuckTime)
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

    // =====================================================
    // CHASE / SEARCH / INVESTIGATE
    // =====================================================

    private void StartChase()
    {
        bool enteringChase = state != State.Chase;

        state = State.Chase;

        agent.isStopped = false;
        agent.speed = chaseSpeed;

        lostTimer = 0f;
        chaseStartedAt = Time.time;
        lastSeenAt = Time.time;

        lastKnownPos = player.position;
        nextRepath = 0f;

        TrySetDestination(lastKnownPos);

        if (enteringChase)
        {
            if (debugLogs)
            {
                Debug.Log(
                    "[ENEMY AI] ELEANOR SPOTTED PLAYER! " +
                    "Chase started.",
                    this);
            }

            ChaseStarted?.Invoke();
        }
    }

    private void StartSearch()
    {
        state = State.Search;
        agent.speed = patrolSpeed;

        stateTimer = searchTime;

        if (debugLogs)
        {
            Debug.Log(
                "[ENEMY AI] Lost player. Searching...",
                this);
        }

        TrySetDestination(lastKnownPos);
    }

    private void PickSearchPoint()
    {
        Vector3 point =
            lastKnownPos +
            UnityEngine.Random.insideUnitSphere *
            searchRadius;

        point.y = lastKnownPos.y;

        TrySetDestination(point);
    }

    private void Investigate(Vector3 position)
    {
        if (!agent.isOnNavMesh)
            return;

        state = State.Investigate;

        agent.isStopped = false;
        agent.speed = patrolSpeed * 1.4f;

        lastKnownPos = position;

        TrySetDestination(position);
    }

    // Used by bottle / trap noise systems.
    public void HearNoise(Vector3 position)
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh ||
            !enabled)
        {
            return;
        }

        if (waiting || state == State.Chase)
            return;

        Investigate(position);
    }

    // Used by GameManager page progression.
    public void Enrage(int pagesCollected)
    {
        pages = Mathf.Max(0, pagesCollected);

        chaseSpeed =
            baseChase + speedPerPage * pages;

        patrolSpeed =
            basePatrol + speedPerPage * 0.5f * pages;

        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        if (state == State.Chase)
            agent.speed = chaseSpeed;
        else if (state == State.Patrol)
            agent.speed = patrolSpeed;
    }

    // =====================================================
    // DEBUG GIZMOS
    // =====================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            catchDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            sightRange);

        Gizmos.color = Color.cyan;

        Vector3 left =
            Quaternion.Euler(
                0f,
                -sightAngle * 0.5f,
                0f) * transform.forward;

        Vector3 right =
            Quaternion.Euler(
                0f,
                sightAngle * 0.5f,
                0f) * transform.forward;

        Gizmos.DrawRay(
            transform.position + Vector3.up,
            left * sightRange);

        Gizmos.DrawRay(
            transform.position + Vector3.up,
            right * sightRange);
    }
}
