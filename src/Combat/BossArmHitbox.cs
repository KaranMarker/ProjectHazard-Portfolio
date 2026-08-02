/*
 * Project Hazard — Selected Portfolio Source
 * Copyright (c) 2026 Karan Marker. All rights reserved.
 *
 * Provided for portfolio review only.
 * The complete Unity project and game assets remain private.
 */
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class BossArmHitboxRelay : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 1;

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Knockback")]
    [SerializeField] private bool applyKnockback = true;
    [SerializeField] private float knockbackHorizontalSpeed = 6f;
    [SerializeField] private float knockbackUpwardSpeed = 1.5f;
    [SerializeField] private float knockbackDuration = 0.15f;

    [Header("Hit Rules")]
    [SerializeField] private bool onlyHitOncePerActivation = true;

    [Header("Instant Overlap Check")]
    [SerializeField] private bool checkOverlapsImmediatelyOnEnable = true;
    [SerializeField] private bool keepCheckingWhileActive = true;
    [SerializeField] private LayerMask overlapMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Collider2D hitboxCollider;
    private bool hasHitPlayer;
    private readonly Collider2D[] overlapResults = new Collider2D[32];

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        hitboxCollider.isTrigger = true;
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        hasHitPlayer = false;

        if (checkOverlapsImmediatelyOnEnable)
        {
            CheckCurrentOverlaps();
        }
    }

    private void FixedUpdate()
    {
        if (!gameObject.activeInHierarchy || !keepCheckingWhileActive)
        {
            return;
        }

        CheckCurrentOverlaps();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void CheckCurrentOverlaps()
    {
        if (hitboxCollider == null)
        {
            hitboxCollider = GetComponent<Collider2D>();
        }

        if (hitboxCollider == null)
        {
            return;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.SetLayerMask(overlapMask);

        int count = hitboxCollider.Overlap(filter, overlapResults);

        for (int i = 0; i < count; i++)
        {
            TryDamage(overlapResults[i]);

            if (onlyHitOncePerActivation && hasHitPlayer)
            {
                break;
            }
        }
    }

    private void TryDamage(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (onlyHitOncePerActivation && hasHitPlayer)
        {
            return;
        }

        Transform playerRoot = FindTaggedRoot(other.transform, playerTag);

        if (playerRoot == null)
        {
            return;
        }

        PlayerHealth playerHealth = playerRoot.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
        {
            playerHealth = other.GetComponentInParent<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            if (debugLogs)
            {
                Debug.LogWarning(name + ": touched player collider, but no PlayerHealth was found.");
            }

            return;
        }

        bool damageApplied;

        if (applyKnockback)
        {
            damageApplied = playerHealth.TakeDamageWithKnockback(
                damage,
                transform,
                knockbackHorizontalSpeed,
                knockbackUpwardSpeed,
                knockbackDuration
            );
        }
        else
        {
            damageApplied = playerHealth.TryTakeDamage(damage);
        }

        if (!damageApplied)
        {
            if (debugLogs)
            {
                Debug.Log(name + ": touched player, but damage was blocked by invuln/dead/dial immunity.");
            }

            return;
        }

        hasHitPlayer = true;

        if (debugLogs)
        {
            Debug.Log(name + ": arm hitbox damaged player.");
        }
    }

    private Transform FindTaggedRoot(Transform start, string wantedTag)
    {
        Transform current = start;

        while (current != null)
        {
            if (current.CompareTag(wantedTag))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }
}