/*
 * Project Hazard — Selected Portfolio Source
 * Copyright (c) 2026 Karan Marker. All rights reserved.
 *
 * Provided for portfolio review only.
 * The complete Unity project and game assets remain private.
 */
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class BatController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform player;

    [Header("Components")]
    [SerializeField] private Animator anim;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Collider2D mainBatCollider;
    [SerializeField] private Damageable damageable;

    [Header("Detection")]
    [SerializeField] private float chaseRadius = 10f;
    [SerializeField] private float attackRange = 2f;

    [Header("Hover Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float horizontalStandOff = 2f;
    [SerializeField] private float hoverBaseHeight = -11.5f;
    [SerializeField] private float hoverBobAmount = 0.25f;
    [SerializeField] private float hoverBobSpeed = 3f;

    [Header("Hover Reaction Delay")]
    [SerializeField] private float hoverReactionDelay = 0.8f;

    [Header("Attack")]
    [SerializeField] private float chargeDuration = 0.2f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private float dipAmount = 0.5f;
    [SerializeField] private float attackCooldown = 0.6f;

    [Header("Dash Floor Clamp")]
    [SerializeField] private float minDashY = -13f;

    [Header("Damage To Player")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private bool onlyDamageOncePerDash = true;

    [Header("Weapon Hit Reaction")]
    [SerializeField] private float hitStunTime = 0.5f;
    [SerializeField] private float knockbackDistance = 4f;
    [SerializeField] private float knockbackDuration = 0.2f;
    [SerializeField] private float knockbackYBobAmount = 0.2f;
    [SerializeField] private bool resetAttackCooldownWhenHit = true;

    [Tooltip("If true, damage is applied after knockback. This lets the bat visibly get knocked back before dying.")]
    [SerializeField] private bool applyDamageAfterKnockback = true;

    [Tooltip("If true, the bat can be hit while charging.")]
    [SerializeField] private bool canBeHitWhileCharging = true;

    [Tooltip("If true, the bat cannot be hit while attacking/dashing.")]
    [SerializeField] private bool invulnerableWhileAttacking = true;

    [Header("Animator Params")]
    [SerializeField] private string chargingBoolName = "isCharging";
    [SerializeField] private string attackingBoolName = "isAttacking";

    [Tooltip("Trigger fired when the bat is hit.")]
    [SerializeField] private string attackedTriggerName = "Attacked";

    [Tooltip("Bool held true while the bat is in knockback / damage stun.")]
    [SerializeField] private string damagedBoolName = "isDamaged";

    [Header("Instant Damage Animation")]
    [Tooltip("If true, forces the Animator into the damaged state immediately instead of waiting for transitions.")]
    [SerializeField] private bool forceDamagedStateInstantly = true;

    [Tooltip("The exact Animator state name for the bat damaged animation.")]
    [SerializeField] private string damagedStateName = "BatDamaged";

    [Tooltip("Small fade time into damaged animation. Use 0 for fully instant.")]
    [SerializeField] private float damagedCrossFadeTime = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    [Header("Respawn Reset")]
    [SerializeField] private bool resetToOriginalPositionOnSavedDeathRespawn = true;

    private bool aggroed;
    private bool isCharging;
    private bool isDashing;
    private bool isDashDamageWindow;
    private bool isHitStunned;
    private bool damagedThisDash;

    private float nextAttackTime;
    private float lockedZ;

    private Vector3 lockedDashStart;
    private Vector3 lockedDashEnd;
    private float lockedDashDirX;

    private Vector3 delayedHoverTarget;
    private float nextHoverTargetUpdateTime;

    private Coroutine attackCo;
    private Coroutine hitReactionCo;

    private bool hasOriginalSceneTransform;
    private Vector3 originalScenePosition;
    private Quaternion originalSceneRotation;
    private Vector3 originalSceneScale;

    public bool IsChargingPublic => isCharging;
    public bool IsDashingPublic => isDashing;
    public bool IsDashDamageWindowPublic => isDashDamageWindow;
    public bool IsHitStunnedPublic => isHitStunned;

    public bool IsAttackingPublic
    {
        get
        {
            return isDashing || isDashDamageWindow;
        }
    }

    private void Awake()
    {
        CacheReferences();
        CacheOriginalSceneTransform();

        lockedZ = transform.position.z;
        delayedHoverTarget = transform.position;

        if (mainBatCollider != null)
            mainBatCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        CacheReferences();

        aggroed = false;
        isCharging = false;
        isDashing = false;
        isDashDamageWindow = false;
        isHitStunned = false;
        damagedThisDash = false;

        delayedHoverTarget = transform.position;
        nextHoverTargetUpdateTime = 0f;

        ClearAttackAnimatorBools();
        SetAnimBool(damagedBoolName, false);

        LockZ();
    }

    public void ResetForSavedDeathRespawn()
    {
        CacheReferences();
        CacheOriginalSceneTransform();
        StopAllBatCoroutines();

        aggroed = false;
        isCharging = false;
        isDashing = false;
        isDashDamageWindow = false;
        isHitStunned = false;
        damagedThisDash = false;

        if (resetToOriginalPositionOnSavedDeathRespawn)
        {
            // command 3 means back to scene start spot
            transform.position = originalScenePosition;
            transform.rotation = originalSceneRotation;
            transform.localScale = originalSceneScale;
            lockedZ = originalScenePosition.z;
        }

        delayedHoverTarget = transform.position;
        nextHoverTargetUpdateTime = 0f;
        nextAttackTime = Time.time + attackCooldown;

        ClearAttackAnimatorBools();
        SetAnimBool(damagedBoolName, false);
        LockZ();
    }

    private void OnDisable()
    {
        StopAllBatCoroutines();

        isCharging = false;
        isDashing = false;
        isDashDamageWindow = false;
        isHitStunned = false;
        damagedThisDash = false;

        ClearAttackAnimatorBools();
        SetAnimBool(damagedBoolName, false);
    }

    private void Update()
    {
        FindPlayerIfNeeded();
        LockZ();

        if (player == null)
            return;

        if (damageable != null && damageable.IsDeadPublic)
            return;

        if (isHitStunned)
            return;

        if (isCharging || isDashing)
            return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (!aggroed && dist <= chaseRadius)
            aggroed = true;

        if (!aggroed)
            return;

        MoveToHoverPositionWithDelay();

        if (Time.time >= nextAttackTime && dist <= attackRange)
        {
            StartAttackRoutine();
            return;
        }

        FaceFromDirection(player.position.x - transform.position.x);
    }

    public bool CanReceiveWeaponHit()
    {
        if (!isActiveAndEnabled)
            return false;

        if (damageable != null && damageable.IsDeadPublic)
            return false;

        if (isHitStunned)
            return false;

        if (invulnerableWhileAttacking && IsAttackingPublic)
            return false;

        if (isCharging && !canBeHitWhileCharging)
            return false;

        return true;
    }

    public Collider2D GetMainColliderForWeaponHit()
    {
        if (mainBatCollider == null)
            mainBatCollider = GetComponent<Collider2D>();

        return mainBatCollider;
    }

    public void ReceiveWeaponHit(Transform attacker, int damage)
    {
        if (!CanReceiveWeaponHit())
        {
            if (debugLogs)
            {
                Debug.Log(
                    "BatController: rejected weapon hit. " +
                    "charging=" + isCharging +
                    " attacking=" + IsAttackingPublic +
                    " hitStunned=" + isHitStunned
                );
            }

            return;
        }

        if (debugLogs)
        {
            string attackerName = attacker != null ? attacker.name : "null attacker";

            Debug.Log(
                "BatController: HIT ACCEPTED from " + attackerName +
                " | charging=" + isCharging +
                " | attacking=" + IsAttackingPublic +
                " | dashDamageWindow=" + isDashDamageWindow +
                " | hitStunned=" + isHitStunned
            );
        }

        if (hitReactionCo != null)
        {
            StopCoroutine(hitReactionCo);
            hitReactionCo = null;
        }

        hitReactionCo = StartCoroutine(WeaponHitReactionRoutine(attacker, damage));
    }

    private IEnumerator WeaponHitReactionRoutine(Transform attacker, int damage)
    {
        isHitStunned = true;

        CancelChargeAndAttackState();

        if (resetAttackCooldownWhenHit)
            nextAttackTime = Time.time + attackCooldown;

        PlayDamagedAnimationInstant();

        if (!applyDamageAfterKnockback)
        {
            ApplyDamage(damage);

            if (damageable != null && damageable.IsDeadPublic)
                yield break;
        }

        Vector3 startPos = transform.position;
        startPos.z = lockedZ;

        float xDir = GetKnockbackXDirection(attacker);

        Vector3 targetPos = startPos;
        targetPos.x += xDir * knockbackDistance;
        targetPos.z = lockedZ;

        if (targetPos.y < minDashY)
            targetPos.y = minDashY;

        if (debugLogs)
        {
            Debug.Log(
                "BatController: knockback start=" + startPos +
                " target=" + targetPos +
                " xDir=" + xDir
            );
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, knockbackDuration);

        while (elapsed < duration)
        {
            if (damageable != null && damageable.IsDeadPublic)
                yield break;

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);

            Vector3 next = Vector3.Lerp(startPos, targetPos, easedT);

            float yBob = Mathf.Sin(t * Mathf.PI) * knockbackYBobAmount;
            next.y = startPos.y + yBob;

            if (next.y < minDashY)
                next.y = minDashY;

            next.z = lockedZ;

            transform.position = next;

            yield return null;
        }

        targetPos.y = startPos.y;

        if (targetPos.y < minDashY)
            targetPos.y = minDashY;

        targetPos.z = lockedZ;

        transform.position = targetPos;
        LockZ();

        if (applyDamageAfterKnockback)
        {
            ApplyDamage(damage);

            if (damageable != null && damageable.IsDeadPublic)
                yield break;
        }

        float remainingStun = hitStunTime - knockbackDuration;

        if (remainingStun > 0f)
            yield return new WaitForSeconds(remainingStun);

        isHitStunned = false;

        SetAnimBool(damagedBoolName, false);

        delayedHoverTarget = transform.position;
        nextHoverTargetUpdateTime = Time.time + hoverReactionDelay;

        hitReactionCo = null;
    }

    private void PlayDamagedAnimationInstant()
    {
        if (anim == null)
            return;

        ClearAttackAnimatorBools();

        SetAnimBool(damagedBoolName, true);

        if (!string.IsNullOrWhiteSpace(attackedTriggerName))
        {
            anim.ResetTrigger(attackedTriggerName);
            anim.SetTrigger(attackedTriggerName);
        }

        if (forceDamagedStateInstantly && !string.IsNullOrWhiteSpace(damagedStateName))
        {
            anim.CrossFadeInFixedTime(damagedStateName, damagedCrossFadeTime, 0, 0f);
        }
    }

    private void ApplyDamage(int damage)
    {
        if (damageable != null)
            damageable.TakeDamage(damage);
    }

    private void CancelChargeAndAttackState()
    {
        StopAttackRoutine();

        isCharging = false;
        isDashing = false;
        isDashDamageWindow = false;
        damagedThisDash = false;

        SetAnimBool(chargingBoolName, false);
        SetAnimBool(attackingBoolName, false);
    }

    private float GetKnockbackXDirection(Transform attacker)
    {
        float xDir;

        if (attacker != null)
        {
            xDir = transform.position.x - attacker.position.x;
        }
        else if (player != null)
        {
            xDir = transform.position.x - player.position.x;
        }
        else
        {
            xDir = sr != null && sr.flipX ? 1f : -1f;
        }

        if (Mathf.Abs(xDir) < 0.01f)
            xDir = sr != null && sr.flipX ? 1f : -1f;

        if (Mathf.Abs(xDir) < 0.01f)
            xDir = 1f;

        return Mathf.Sign(xDir);
    }

    private void MoveToHoverPositionWithDelay()
    {
        if (player == null)
            return;

        if (Time.time >= nextHoverTargetUpdateTime)
        {
            nextHoverTargetUpdateTime = Time.time + hoverReactionDelay;

            float side = transform.position.x >= player.position.x ? 1f : -1f;

            float targetX = player.position.x + side * horizontalStandOff;
            float targetY = hoverBaseHeight + Mathf.Sin(Time.time * hoverBobSpeed) * hoverBobAmount;

            delayedHoverTarget = new Vector3(targetX, targetY, lockedZ);

            if (debugLogs)
                Debug.Log("BatController: updated hover target = " + delayedHoverTarget);
        }

        Vector3 before = transform.position;
        Vector3 next = Vector3.MoveTowards(before, delayedHoverTarget, moveSpeed * Time.deltaTime);
        next.z = lockedZ;

        transform.position = next;

        float moveDirX = next.x - before.x;

        if (Mathf.Abs(moveDirX) > 0.001f)
            FaceFromDirection(moveDirX);
    }

    private void StartAttackRoutine()
    {
        StopAttackRoutine();
        attackCo = StartCoroutine(ChargeAndDash());
    }

    private IEnumerator ChargeAndDash()
    {
        if (player == null)
            yield break;

        isCharging = true;
        isDashing = false;
        isDashDamageWindow = false;
        damagedThisDash = false;

        SetAnimBool(damagedBoolName, false);
        SetAnimBool(chargingBoolName, true);
        SetAnimBool(attackingBoolName, false);

        lockedDashStart = transform.position;
        lockedDashStart.z = lockedZ;

        Vector3 lockedPlayerPos = player.position;
        lockedPlayerPos.z = lockedZ;

        Vector2 toPlayer = lockedPlayerPos - lockedDashStart;

        float startDistance = toPlayer.magnitude;

        if (startDistance < 0.01f)
            startDistance = horizontalStandOff;

        Vector2 dashDir = toPlayer.normalized;

        if (dashDir.sqrMagnitude < 0.001f)
            dashDir = Vector2.right;

        lockedDashDirX = dashDir.x;

        lockedDashEnd = lockedPlayerPos + (Vector3)(dashDir * startDistance);
        lockedDashEnd.z = lockedZ;

        FaceFromDirection(lockedDashDirX);

        float chargeElapsed = 0f;

        while (chargeElapsed < chargeDuration)
        {
            if (isHitStunned)
            {
                CancelChargeAndAttackState();
                yield break;
            }

            chargeElapsed += Time.deltaTime;
            yield return null;
        }

        if (isHitStunned)
        {
            CancelChargeAndAttackState();
            yield break;
        }

        isCharging = false;
        isDashing = true;
        isDashDamageWindow = true;
        damagedThisDash = false;

        SetAnimBool(chargingBoolName, false);
        SetAnimBool(attackingBoolName, true);

        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, dashDuration);

        while (elapsed < duration)
        {
            if (isHitStunned)
            {
                CancelChargeAndAttackState();
                yield break;
            }

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 pos = Vector3.Lerp(lockedDashStart, lockedDashEnd, t);

            float dip = 4f * dipAmount * t * (1f - t);
            pos.y -= dip;

            if (pos.y < minDashY)
                pos.y = minDashY;

            pos.z = lockedZ;

            transform.position = pos;
            FaceFromDirection(lockedDashDirX);

            yield return null;
        }

        Vector3 finalPos = lockedDashEnd;

        if (finalPos.y < minDashY)
            finalPos.y = minDashY;

        finalPos.z = lockedZ;
        transform.position = finalPos;

        FaceFromDirection(lockedDashDirX);
        LockZ();

        isDashing = false;
        isDashDamageWindow = false;
        damagedThisDash = false;

        SetAnimBool(chargingBoolName, false);
        SetAnimBool(attackingBoolName, false);

        nextAttackTime = Time.time + attackCooldown;
        attackCo = null;
    }

    private void StopAttackRoutine()
    {
        if (attackCo != null)
        {
            StopCoroutine(attackCo);
            attackCo = null;
        }

        isCharging = false;
        isDashing = false;
        isDashDamageWindow = false;
        damagedThisDash = false;

        ClearAttackAnimatorBools();
    }

    private void StopAllBatCoroutines()
    {
        StopAttackRoutine();

        if (hitReactionCo != null)
        {
            StopCoroutine(hitReactionCo);
            hitReactionCo = null;
        }
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null)
            return;

        GameObject found = GameObject.FindGameObjectWithTag(playerTag);

        if (found != null)
            player = found.transform;
    }

    private void FaceFromDirection(float xDir)
    {
        if (sr == null)
            return;

        if (xDir > 0.01f)
        {
            sr.flipX = true;
        }
        else if (xDir < -0.01f)
        {
            sr.flipX = false;
        }
    }

    private void SetAnimBool(string paramName, bool value)
    {
        if (anim != null && !string.IsNullOrEmpty(paramName))
            anim.SetBool(paramName, value);
    }

    private void ClearAttackAnimatorBools()
    {
        SetAnimBool(chargingBoolName, false);
        SetAnimBool(attackingBoolName, false);
    }

    private void LockZ()
    {
        Vector3 p = transform.position;

        if (!Mathf.Approximately(p.z, lockedZ))
        {
            p.z = lockedZ;
            transform.position = p;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (!isDashDamageWindow)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (onlyDamageOncePerDash && damagedThisDash)
            return;

        PlayerHealth health = other.GetComponent<PlayerHealth>();

        if (health != null)
        {
            health.TakeDamage(contactDamage);
            damagedThisDash = true;
        }
    }

    private void CacheReferences()
    {
        if (anim == null)
            anim = GetComponent<Animator>();

        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        if (mainBatCollider == null)
            mainBatCollider = GetComponent<Collider2D>();

        if (damageable == null)
            damageable = GetComponent<Damageable>();

        if (mainBatCollider != null)
            mainBatCollider.isTrigger = true;
    }

    private void CacheOriginalSceneTransform()
    {
        if (hasOriginalSceneTransform)
            return;

        originalScenePosition = transform.position;
        originalSceneRotation = transform.rotation;
        originalSceneScale = transform.localScale;
        hasOriginalSceneTransform = true;
    }
}