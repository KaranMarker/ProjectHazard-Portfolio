/*
 * Project Hazard — Selected Portfolio Source
 * Copyright (c) 2026 Karan Marker. All rights reserved.
 *
 * Provided for portfolio review only.
 * The complete Unity project and game assets remain private.
 */
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHp = 5;

    [Header("Health Icons")]
    [Tooltip("Optional. Drag the PlayerHealthIcons script here. If empty, this auto-finds it in the scene.")]
    [SerializeField] private PlayerHealthIcons playerHealthIcons;

    [Header("Damage Timing")]
    [SerializeField] private float inputLockTimeAfterDamage = 0.3f;
    [SerializeField] private float damageInvulnTime = 1f;

    [Header("Death Scene")]
    [SerializeField] private string deathSceneName = "LevelD";
    [SerializeField] private bool loadDeathSceneOnDeath = true;

    [Header("Death Transition Animation")]
    [Tooltip("If true, death uses SceneTransitionAnimator before loading the death scene.")]
    [SerializeField] private bool useTransitionAnimationOnDeath = true;

    [Header("Animator")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string damageTakenTrigger = "damagetaken";

    [Header("Damage Stun Visual")]
    [Tooltip("Optional. Drag the SpriteRenderer from the camera/damage overlay object here.")]
    [SerializeField] private SpriteRenderer damageStunSpriteRenderer;

    [Tooltip("If true, the renderer is hidden on Awake/OnEnable and only shown while damage input lock is active.")]
    [SerializeField] private bool hideDamageStunSpriteOnStart = true;

    [Header("Weapon Attack Damage Interaction")]
    [Tooltip("Optional. If empty, this is auto-found on the same GameObject.")]
    [SerializeField] private WeaponAttackController weaponAttackController;

    [Tooltip("If true, getting hit immediately cancels the weapon attack and turns off the weapon hitbox.")]
    [SerializeField] private bool cancelWeaponAttackOnDamage = true;

    [Header("Dial Charge Damage Interaction")]
    [Tooltip("Optional. If empty, this is auto-found on the same GameObject.")]
    [SerializeField] private DialCharge dialCharge;

    [Tooltip("If true, damage only cancels DialCharge startup/charge/full-charge loops. Released dash/slam states continue.")]
    [SerializeField] private bool cancelOnlyChargeLoopsOnDamage = true;

    [Tooltip("If true, committed DialCharge dash/slam/crash states ignore incoming damage entirely unless released-flow damage deferral handles it first.")]
    [SerializeField] private bool ignoreDamageDuringCommittedDialAction = true;

    [Tooltip("If true, damage received after DialCharge has been released is stored and applied after the released DialCharge flow finishes.")]
    [SerializeField] private bool deferDamageDuringReleasedDialFlow = true;

    [Tooltip("If true, damage will not zero velocity once DialCharge has committed into a dash/slam/crash.")]
    [SerializeField] private bool doNotZeroVelocityDuringDialDash = true;

    [Header("Scripts Disabled Briefly After Damage")]
    [Tooltip("Put movement scripts, scene transition scripts, ability scripts, etc. here. Do NOT put Counter here.")]
    [SerializeField] private MonoBehaviour[] scriptsToDisableAfterDamage;

    [Header("Velocity")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private bool zeroVelocityOnDamage = true;

    [Header("External Knockback")]
    [Tooltip("If true, external damage sources like ghost explosions can push the player.")]
    [SerializeField] private bool allowExternalKnockback = true;

    [Tooltip("If true, knockback keeps controlling velocity for the full knockback duration.")]
    [SerializeField] private bool forceVelocityDuringExternalKnockback = true;

    [Tooltip("If true, external knockback is skipped during committed DialCharge states.")]
    [SerializeField] private bool ignoreExternalKnockbackDuringCommittedDialAction = true;

    private int currentHp;
    private float invulnUntil;
    private Coroutine damageLockCo;
    private Coroutine externalKnockbackCo;
    private Coroutine pendingReleasedDialDamageCo;
    private int pendingReleasedDialDamage;
    private bool[] previousScriptEnabledStates;
    private bool dead;

    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public bool IsInvulnerable => Time.time < invulnUntil;
    public bool IsDamageInputLocked => damageLockCo != null;
    public bool IsDead => dead;

    public SpriteRenderer DamageStunSpriteRendererPublic => damageStunSpriteRenderer;

    private void Awake()
    {
        currentHp = maxHp;
        CacheReferences();
        UpdateHealthIcons();

        if (hideDamageStunSpriteOnStart)
            SetDamageStunSprite(false);
    }

    private void OnEnable()
    {
        CacheReferences();
        UpdateHealthIcons();

        if (hideDamageStunSpriteOnStart)
            SetDamageStunSprite(false);
    }

    private void OnDisable()
    {
        RestoreDisabledScripts();
        SetDamageStunSprite(false);

        if (externalKnockbackCo != null)
        {
            StopCoroutine(externalKnockbackCo);
            externalKnockbackCo = null;
        }

        ClearPendingReleasedDialDamage();
    }

    public void TakeDamage(int amount)
    {
        TryTakeDamage(amount);
    }

    public bool TryTakeDamage(int amount)
    {
        if (dead)
            return false;

        if (amount <= 0)
            return false;

        CacheReferences();

        if (Time.time < invulnUntil)
            return false;

        if (TryQueueDamageUntilReleasedDialFlowEnds(amount))
            return true;

        if (ShouldIgnoreDamageBecauseDialCommitted())
            return false;

        if (cancelOnlyChargeLoopsOnDamage && dialCharge != null)
            dialCharge.TryCancelChargeHoldStateFromDamage();

        if (cancelWeaponAttackOnDamage && weaponAttackController != null)
            weaponAttackController.InterruptAttackFromDamage();

        currentHp -= amount;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        UpdateHealthIcons();

        if (FirebaseGameProgressSync.Instance != null)
            FirebaseGameProgressSync.Instance.SaveNow();

        invulnUntil = Time.time + damageInvulnTime;

        PlayDamageAnimation();

        bool dialActionAlreadyCommitted =
            dialCharge != null &&
            dialCharge.IsCommittedDialActionPublic;

        bool shouldZeroVelocity =
            zeroVelocityOnDamage &&
            rb != null &&
            (!doNotZeroVelocityDuringDialDash || !dialActionAlreadyCommitted);

        if (shouldZeroVelocity)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        StartDamageInputLock();

        if (currentHp <= 0)
        {
            Die();
        }

        return true;
    }

    public bool TakeDamageWithKnockback(
        int amount,
        Transform damageSource,
        float horizontalSpeed,
        float upwardSpeed,
        float duration
    )
    {
        bool damageApplied = TryTakeDamage(amount);

        if (!damageApplied)
            return false;

        ApplyExternalKnockbackFromSource(
            damageSource,
            horizontalSpeed,
            upwardSpeed,
            duration
        );

        return true;
    }

    public void ApplyExternalKnockbackFromSource(
        Transform damageSource,
        float horizontalSpeed,
        float upwardSpeed,
        float duration
    )
    {
        if (!allowExternalKnockback)
            return;

        if (dead)
            return;

        CacheReferences();

        if (rb == null)
            return;

        if (ignoreExternalKnockbackDuringCommittedDialAction &&
            dialCharge != null &&
            dialCharge.IsCommittedDialActionPublic)
        {
            return;
        }

        float xDir = GetExternalKnockbackDirection(damageSource);

        Vector2 knockbackVelocity = new Vector2(
            xDir * Mathf.Abs(horizontalSpeed),
            Mathf.Abs(upwardSpeed)
        );

        if (externalKnockbackCo != null)
        {
            StopCoroutine(externalKnockbackCo);
            externalKnockbackCo = null;
        }

        externalKnockbackCo = StartCoroutine(ExternalKnockbackRoutine(
            knockbackVelocity,
            duration
        ));
    }

    private IEnumerator ExternalKnockbackRoutine(Vector2 knockbackVelocity, float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);

        if (rb != null)
        {
            rb.linearVelocity = knockbackVelocity;
            rb.angularVelocity = 0f;
        }

        if (forceVelocityDuringExternalKnockback)
        {
            float timer = 0f;

            while (timer < safeDuration)
            {
                timer += Time.deltaTime;

                if (dead)
                    yield break;

                if (rb != null)
                {
                    float t = Mathf.Clamp01(timer / safeDuration);
                    float eased = 1f - (t * t * (3f - 2f * t));

                    Vector2 nextVelocity = knockbackVelocity * eased;
                    rb.linearVelocity = nextVelocity;
                    rb.angularVelocity = 0f;
                }

                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(safeDuration);
        }

        externalKnockbackCo = null;
    }

    private float GetExternalKnockbackDirection(Transform damageSource)
    {
        if (damageSource == null)
        {
            return transform.localScale.x >= 0f ? -1f : 1f;
        }

        float xDir = transform.position.x - damageSource.position.x;

        if (Mathf.Abs(xDir) < 0.01f)
            xDir = transform.localScale.x >= 0f ? -1f : 1f;

        if (Mathf.Abs(xDir) < 0.01f)
            xDir = 1f;

        return Mathf.Sign(xDir);
    }

    private bool TryQueueDamageUntilReleasedDialFlowEnds(int amount)
    {
        if (!deferDamageDuringReleasedDialFlow)
            return false;

        if (dialCharge == null)
            return false;

        if (!dialCharge.IsReleasedDialFlowPublic)
            return false;

        if (pendingReleasedDialDamage <= 0)
            pendingReleasedDialDamage = amount;

        if (pendingReleasedDialDamageCo == null)
            pendingReleasedDialDamageCo = StartCoroutine(ApplyReleasedDialDamageWhenFlowEnds());

        return true;
    }

    private IEnumerator ApplyReleasedDialDamageWhenFlowEnds()
    {
        while (!dead && dialCharge != null && dialCharge.IsReleasedDialFlowPublic)
            yield return null;

        pendingReleasedDialDamageCo = null;

        if (dead)
        {
            pendingReleasedDialDamage = 0;
            yield break;
        }

        int damage = pendingReleasedDialDamage;
        pendingReleasedDialDamage = 0;

        if (damage > 0)
            TryTakeDamage(damage);
    }

    private void ClearPendingReleasedDialDamage()
    {
        pendingReleasedDialDamage = 0;

        if (pendingReleasedDialDamageCo != null)
        {
            StopCoroutine(pendingReleasedDialDamageCo);
            pendingReleasedDialDamageCo = null;
        }
    }

    private bool ShouldIgnoreDamageBecauseDialCommitted()
    {
        if (!ignoreDamageDuringCommittedDialAction)
            return false;

        if (dialCharge == null)
            return false;

        return dialCharge.IsDamageImmuneDuringCommittedDialActionPublic;
    }



    public void SetMaxHpFromUpgrade(int newMaxHp, bool healToFull)
    {
        if (dead)
            dead = false;

        int safeNewMaxHp = Mathf.Max(1, newMaxHp);

        if (safeNewMaxHp <= maxHp)
        {
            if (healToFull)
            {
                currentHp = maxHp;
                UpdateHealthIcons();
            }

            return;
        }

        int oldMaxHp = maxHp;
        int oldCurrentHp = currentHp;
        int addedMaxHp = safeNewMaxHp - oldMaxHp;

        maxHp = safeNewMaxHp;

        if (healToFull)
        {
            currentHp = maxHp;
        }
        else
        {
            // Health-upgrade behavior:
            // If the player was 4/5 and gains +1 max HP, they become 5/6.
            // This revives the old 5th heart and leaves the new 6th heart dead.
            currentHp = Mathf.Clamp(oldCurrentHp + addedMaxHp, 0, maxHp);
        }

        UpdateHealthIcons();

        if (FirebaseGameProgressSync.Instance != null)
            FirebaseGameProgressSync.Instance.SaveNow();
    }

    public void ApplyLoadedHealthState(int loadedCurrentHp, int loadedMaxHp)
    {
        int safeMaxHp = Mathf.Max(1, loadedMaxHp);

        maxHp = safeMaxHp;
        currentHp = Mathf.Clamp(loadedCurrentHp, 1, maxHp);
        dead = false;
        invulnUntil = 0f;

        ClearPendingReleasedDialDamage();

        if (damageLockCo != null)
        {
            StopCoroutine(damageLockCo);
            damageLockCo = null;
        }

        if (externalKnockbackCo != null)
        {
            StopCoroutine(externalKnockbackCo);
            externalKnockbackCo = null;
        }

        RestoreDisabledScripts();
        SetDamageStunSprite(false);
        CacheReferences();
        UpdateHealthIcons();
    }

    public void Heal(int amount)
    {
        if (dead)
            return;

        if (amount <= 0)
            return;

        currentHp = Mathf.Min(currentHp + amount, maxHp);
        UpdateHealthIcons();

        if (FirebaseGameProgressSync.Instance != null)
            FirebaseGameProgressSync.Instance.SaveNow();
    }

    public void RestoreFullHealth()
    {
        dead = false;
        currentHp = maxHp;
        invulnUntil = 0f;
        ClearPendingReleasedDialDamage();

        UpdateHealthIcons();
        SetDamageStunSprite(false);

        if (FirebaseGameProgressSync.Instance != null)
            FirebaseGameProgressSync.Instance.SaveNow();
    }

    private void PlayDamageAnimation()
    {
        if (playerAnimator == null)
            return;

        if (string.IsNullOrWhiteSpace(damageTakenTrigger))
            return;

        playerAnimator.ResetTrigger(damageTakenTrigger);
        playerAnimator.SetTrigger(damageTakenTrigger);
    }

    private void StartDamageInputLock()
    {
        if (damageLockCo != null)
            StopCoroutine(damageLockCo);

        RestoreDisabledScripts();
        SetDamageStunSprite(false);

        damageLockCo = StartCoroutine(DamageInputLockRoutine());
    }

    private IEnumerator DamageInputLockRoutine()
    {
        DisableListedScripts();
        SetDamageStunSprite(true);

        yield return new WaitForSeconds(inputLockTimeAfterDamage);

        SetDamageStunSprite(false);
        RestoreDisabledScripts();
        damageLockCo = null;
    }

    private void SetDamageStunSprite(bool state)
    {
        if (damageStunSpriteRenderer != null)
            damageStunSpriteRenderer.enabled = state;
    }

    private void DisableListedScripts()
    {
        if (scriptsToDisableAfterDamage == null || scriptsToDisableAfterDamage.Length == 0)
            return;

        previousScriptEnabledStates = new bool[scriptsToDisableAfterDamage.Length];

        for (int i = 0; i < scriptsToDisableAfterDamage.Length; i++)
        {
            MonoBehaviour script = scriptsToDisableAfterDamage[i];

            if (script == null)
                continue;

            if (script == this)
                continue;

            if (script is Counter)
                continue;

            if (script is WeaponAttackController)
                continue;

            previousScriptEnabledStates[i] = script.enabled;

            DialCharge listedDialCharge = script as DialCharge;

            if (listedDialCharge != null)
            {
                bool dialChargeIsActive =
                    listedDialCharge.IsChargingPublic ||
                    listedDialCharge.IsFullChargePublic ||
                    listedDialCharge.IsDashingPublic ||
                    listedDialCharge.IsSlamHorizontalMovementLockedPublic ||
                    listedDialCharge.IsDialReleaseADLockedPublic ||
                    listedDialCharge.IsWaitingForDialReleaseAfterDamageCancelPublic;

                if (dialChargeIsActive)
                {
                    if (Debug.isDebugBuild)
                        Debug.Log("PlayerHealth skipped disabling DialCharge because DialCharge was active or waiting for damaged charge release.");

                    continue;
                }
            }

            script.enabled = false;
        }
    }

    private void RestoreDisabledScripts()
    {
        if (scriptsToDisableAfterDamage == null || previousScriptEnabledStates == null)
            return;

        int count = Mathf.Min(scriptsToDisableAfterDamage.Length, previousScriptEnabledStates.Length);

        for (int i = 0; i < count; i++)
        {
            MonoBehaviour script = scriptsToDisableAfterDamage[i];

            if (script == null)
                continue;

            if (script == this)
                continue;

            if (script is Counter)
                continue;

            if (script is WeaponAttackController)
                continue;

            script.enabled = previousScriptEnabledStates[i];
        }

        previousScriptEnabledStates = null;
    }

    private void CacheReferences()
    {
        if (playerAnimator == null)
            playerAnimator = GetComponent<Animator>();

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (dialCharge == null)
            dialCharge = GetComponent<DialCharge>();

        if (weaponAttackController == null)
            weaponAttackController = GetComponent<WeaponAttackController>();

        if (playerHealthIcons == null)
            playerHealthIcons = GetComponent<PlayerHealthIcons>();

        if (playerHealthIcons == null)
            playerHealthIcons = GetComponentInChildren<PlayerHealthIcons>();

        if (playerHealthIcons == null)
            playerHealthIcons = FindObjectOfType<PlayerHealthIcons>();
    }

    private void UpdateHealthIcons()
    {
        if (playerHealthIcons != null)
        {
            playerHealthIcons.UpdateHealthIcons(currentHp, maxHp);
        }
    }

    private void Die()
    {
        if (dead)
            return;

        dead = true;

        Debug.Log("PLAYER DIED. Current scene = " + SceneManager.GetActiveScene().name);
        Debug.Log("Trying to load death scene: " + deathSceneName);

        if (weaponAttackController != null)
            weaponAttackController.InterruptAttackFromDamage();

        RestoreDisabledScripts();
        SetDamageStunSprite(false);

        if (damageLockCo != null)
        {
            StopCoroutine(damageLockCo);
            damageLockCo = null;
        }

        if (externalKnockbackCo != null)
        {
            StopCoroutine(externalKnockbackCo);
            externalKnockbackCo = null;
        }

        ClearPendingReleasedDialDamage();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (!loadDeathSceneOnDeath)
        {
            Debug.LogWarning("Death scene loading is disabled in the PlayerHealth inspector.");
            return;
        }

        if (string.IsNullOrWhiteSpace(deathSceneName))
        {
            Debug.LogWarning("Death scene name is empty.");
            return;
        }

        if (useTransitionAnimationOnDeath && SceneTransitionAnimator.Instance != null)
        {
            SceneTransitionAnimator.Instance.PlayTransitionAndLoadScene(deathSceneName);
        }
        else
        {
            SceneManager.LoadScene(deathSceneName);
        }
    }
}