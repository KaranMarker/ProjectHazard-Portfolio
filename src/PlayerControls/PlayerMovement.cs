/*
 * Project Hazard — Selected Portfolio Source
 * Copyright (c) 2026 Karan Marker. All rights reserved.
 *
 * Provided for portfolio review only.
 * The complete Unity project and game assets remain private.
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerMoveChildHitboxKind
{
    GroundLift,
    VaultLeft,
    VaultRight,
    VaultBlocker
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMoveController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravityScale = 4f;

    [Header("Jump")]
    [SerializeField] private Key jumpKey = Key.G;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private string jumpTrigger = "MoveJumpTrigger";

    [Header("Saved Keybind System")]
    [SerializeField] private FirebaseKeybindSettingsSync keybindSync;
    [SerializeField] private string jumpActionId = "jump";
    [SerializeField] private string fallbackJumpKey = "g";

    [Header("Forced Airborne")]
    [SerializeField] private float forcedAirborneAfterJumpTime = 2f;

    [Header("Jump Buffer / State Settle")]
    [SerializeField] private float jumpStateReadyTime = 0.25f;

    [Header("Ground Check")]
    [SerializeField] private Collider2D groundCheckCollider;
    [SerializeField] private float groundedCastDistance = 0.08f;
    [SerializeField] private float groundedNormalMinY = 0.35f;

    [Header("Ground Lift / Tiny Step Climb")]
    [SerializeField] private bool enableGroundLift = true;

    [Tooltip("Small trigger child in front of the player's feet/body.")]
    [SerializeField] private Collider2D groundLiftCollider;

    [Tooltip("Optional extra layer filter for ground lift. Tags are not checked.")]
    [SerializeField] private LayerMask groundLiftMask = ~0;

    [Tooltip("If true, ground lift also checks groundLiftMask.")]
    [SerializeField] private bool useGroundLiftLayerMask = false;

    [Tooltip("Smallest obstacle top above the player's feet that ground lift accepts.")]
    [SerializeField] private float groundLiftMinStepHeight = 0.20f;

    [Tooltip("Tallest obstacle top above the player's feet that ground lift accepts.")]
    [SerializeField] private float groundLiftMaxStepHeight = 0.30f;

    [Tooltip("How long the smooth lift onto the obstacle top takes. Set 0 for instant movement.")]
    [SerializeField] private float groundLiftMoveTime = 0.12f;

    [Tooltip("Small extra clearance so the player does not stay clipped into the tiny step.")]
    [SerializeField] private float groundLiftSkin = 0.005f;

    [Tooltip("Prevents OnTriggerStay from lifting the player repeatedly every physics frame.")]
    [SerializeField] private float groundLiftCooldown = 0.08f;

    [Tooltip("Require A/D input before ground lift runs.")]
    [SerializeField] private bool requireMoveInputForGroundLift = true;

    [Header("Vault")]
    [SerializeField] private bool enableVault = true;

    [Tooltip("Only objects with this tag can trigger vault.")]
    [SerializeField] private string vaultTag = "Vault";

    [SerializeField] private Collider2D vaultLeftCollider;
    [SerializeField] private Collider2D vaultRightCollider;
    [SerializeField] private LayerMask vaultMask = ~0;

    [Tooltip("New child trigger detector above/near the player's head. If this touches a solid collider, vault is blocked.")]
    [SerializeField] private Collider2D vaultBlockerCollider;

    [Tooltip("Layers that block vault when touched by vaultBlockerCollider.")]
    [SerializeField] private LayerMask vaultBlockerMask = ~0;

    [Tooltip("If true, vault only works after the player has actually jumped.")]
    [SerializeField] private bool requireJumpBeforeVault = true;

    [Tooltip("One shared Animator bool used for both left and right vaults.")]
    [SerializeField] private string vaultBool = "Vault";

    [SerializeField] private bool onlyVaultWhileAirborne = true;

    [Tooltip("Cooldown after vault so OnTriggerStay does not instantly restart the vault.")]
    [SerializeField] private float vaultCooldown = 0.35f;

    [Header("Vault Target Detection")]
    [Tooltip("If true, searches for the closest solid Vault-tagged collider instead of blindly using the trigger hit.")]
    [SerializeField] private bool findNearestVaultTaggedCollider = true;

    [Header("Ledge Pull-Up Vault Path")]
    [Tooltip("Small pause when the vault hitbox first grabs the vault object. Set 0 for no latch hold.")]
    [SerializeField] private float vaultLatchTime = 0.04f;

    [Tooltip("How barely above the top the player's collider bottom gets before moving horizontally. Good values: 0.02 - 0.06.")]
    [SerializeField] private float vaultTopClearance = 0.04f;

    [Tooltip("How long the upward pull-up movement takes.")]
    [SerializeField] private float vaultRiseTime = 0.32f;

    [Tooltip("How long the horizontal pull onto the ledge takes after reaching top height.")]
    [SerializeField] private float vaultOverTime = 0.42f;

    [Tooltip("How long the tiny settle down/stand-up movement takes.")]
    [SerializeField] private float vaultSettleDownTime = 0.10f;

    [Tooltip("How far inward from the edge the player lands. Bigger = deeper onto the block.")]
    [SerializeField] private float vaultLandInward = 0.22f;

    [Tooltip("Tiny final height above the platform top. Use 0.005-0.03. Too low can overlap.")]
    [SerializeField] private float vaultFinalYFromVaultTop = 0.008f;

    [Tooltip("Hold the final vault position briefly while the animation finishes.")]
    [SerializeField] private float vaultFinalHoldTime = 0.04f;

    [Tooltip("Final X edit after vault calculation. Negative = left. Positive = right.")]
    [SerializeField] private float finalVaultXEdit = 0f;

    [Tooltip("Final Y edit after vault calculation. Negative = lower final vault position. Positive = raise it.")]
    [SerializeField] private float finalVaultYEdit = 0f;

    [Header("Vault Player Collider Bypass")]
    [Tooltip("Turns off the player's main body collider during vault so the pull-up can stay close to the block.")]
    [SerializeField] private bool disablePlayerBodyColliderDuringVault = true;

    [Tooltip("Optional delay before restoring the player collider. Usually keep very low.")]
    [SerializeField] private float vaultColliderRestoreDelay = 0.02f;

    [Tooltip("After turning the player collider back on, lock exact X/Y briefly before the Y-only pin starts.")]
    [SerializeField] private float vaultColliderRestorePositionLockTime = 0.08f;

    [Header("Post Vault Y Pin")]
    [Tooltip("After vault, hold only the Y level so Unity does not pop/drop the player, but still allow A/D movement and jumping.")]
    [SerializeField] private bool pinYAfterVault = true;

    [Tooltip("How long to keep the player on the vault landing Y after collider restore.")]
    [SerializeField] private float postVaultYPinTime = 0.35f;

    [Tooltip("If true, pressing jump cancels the Y pin immediately.")]
    [SerializeField] private bool jumpCancelsPostVaultYPin = true;

    [Header("Vault Camera Drop")]
    [Tooltip("Queues camera movement when jump is pressed, then only activates it if a real Vault-tagged object confirms a vault.")]
    [SerializeField] private bool requestCameraDropOnConfirmedVault = true;

    [Tooltip("Optional. Leave empty if CameraRig is in the scene. The player will auto-find CameraRigFollowController.Instance.")]
    [SerializeField] private CameraRigFollowController cameraRigFollowController;

    [Tooltip("If true, auto-finds CameraRigFollowController because the CameraRig may be created from another scene.")]
    [SerializeField] private bool autoFindCameraRigFollowController = true;

    [Tooltip("How long after pressing jump a vault camera request is allowed to activate.")]
    [SerializeField] private float vaultCameraJumpQueueTime = 0.75f;

    [Tooltip("Positive number. 5 means the CameraRig target goes DOWN 5 world units when the vault is confirmed. Edit this to change the vault camera drop amount.")]
    [SerializeField] private float vaultCameraDownAmountPerVault = 5f;

    [Tooltip("After stable vault Y is saved, reset camera when player goes this far below it. 0.5 = reset at savedY - 0.5.")]
    [SerializeField] private float vaultCameraClearBelowLandingY = 0.5f;

    [Header("Vault Sensor Lock")]
    [SerializeField] private bool disableVaultSensorsWhileVaulting = true;

    [Header("Weapon Facing Sync")]
    [SerializeField] private WeaponHitbox weaponHitbox;
    [SerializeField] private bool syncWeaponHitboxFacing = true;

    [Header("Pixel Art")]
    [SerializeField] private float pixelsPerUnit = 16f;
    [SerializeField] private bool snapXPosition = false;

    [Header("Animator Params")]
    [SerializeField] private string speedParam = "SpeedX";

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;
    private Collider2D ownCollider;

    private Vector2 input;

    [Header("State")]
    [SerializeField] private bool canJump = true;
    [SerializeField] private bool isAirborne = false;
    [SerializeField] private bool isGrounded = true;

    private float forcedAirborneTimer;
    private bool externalAirborneLock;
    private bool externalMovementLock;

    private bool isVaulting;
    private float vaultCooldownTimer;
    private Coroutine vaultRoutine;

    private bool isGroundLifting;
    private Coroutine groundLiftRoutine;

    private bool hasJumpedSinceLastGrounded;

    private float groundLiftCooldownTimer;

    private bool postVaultYPinActive;
    private float postVaultPinnedY;
    private float postVaultYPinTimer;

    private bool vaultCameraQueuedFromJump;
    private float vaultCameraQueueTimer;

    private readonly HashSet<Collider2D> vaultBlockers = new HashSet<Collider2D>();

    private enum Facing { Left, Right }
    [SerializeField] private Facing facing = Facing.Right;

    private enum LocomotionState { Idle, WalkLeft, WalkRight }
    private LocomotionState locomotionState = LocomotionState.Idle;
    private float locomotionStateSince;

    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    private ContactFilter2D groundFilter;

    public bool CanJumpPublic => canJump;
    public bool IsAirbornePublic => isAirborne;
    public bool IsGroundedPublic => isGrounded;
    public bool ExternalMovementLockPublic => externalMovementLock;
    public bool IsVaultingPublic => isVaulting;
    public bool HasJumpedSinceLastGroundedPublic => hasJumpedSinceLastGrounded;
    public bool VaultBlockedPublic => IsVaultBlockedByBlockerDetector();

    public int FacingXPublic
    {
        get
        {
            if (facing == Facing.Left)
                return -1;

            return 1;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        ownCollider = GetComponent<Collider2D>();

        if (keybindSync == null)
            keybindSync = GetComponent<FirebaseKeybindSettingsSync>();

        if (keybindSync == null)
            keybindSync = FirebaseKeybindSettingsSync.Instance;

        CacheWeaponHitbox();
        CacheCameraRigFollowController();

        rb.constraints = RigidbodyConstraints2D.None;
        rb.freezeRotation = false;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.Sleep();
        rb.WakeUp();
        rb.freezeRotation = true;

        rb.gravityScale = gravityScale;
        rb.linearDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        groundFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = false
        };

        locomotionState = LocomotionState.Idle;
        locomotionStateSince = Time.time;

        SetupChildHitbox(groundLiftCollider, PlayerMoveChildHitboxKind.GroundLift);
        SetupChildHitbox(vaultLeftCollider, PlayerMoveChildHitboxKind.VaultLeft);
        SetupChildHitbox(vaultRightCollider, PlayerMoveChildHitboxKind.VaultRight);
        SetupChildHitbox(vaultBlockerCollider, PlayerMoveChildHitboxKind.VaultBlocker);

        RefreshGroundedState();
        SyncFacingToWeaponHitbox();
    }

    private void Start()
    {
        if (keybindSync == null)
            keybindSync = FirebaseKeybindSettingsSync.Instance;

        rb.constraints = RigidbodyConstraints2D.None;
        rb.freezeRotation = true;

        CacheCameraRigFollowController();
        SyncFacingToWeaponHitbox();
    }

    private void OnDisable()
    {
        if (groundLiftRoutine != null)
        {
            StopCoroutine(groundLiftRoutine);
            groundLiftRoutine = null;
        }

        isGroundLifting = false;

        if (rb != null)
            rb.gravityScale = gravityScale;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;

        if (kb == null)
            return;

        if (keybindSync == null)
            keybindSync = FirebaseKeybindSettingsSync.Instance;

        if (forcedAirborneTimer > 0f)
            forcedAirborneTimer -= Time.deltaTime;

        if (vaultCooldownTimer > 0f)
            vaultCooldownTimer -= Time.deltaTime;

        if (groundLiftCooldownTimer > 0f)
            groundLiftCooldownTimer -= Time.deltaTime;

        if (vaultCameraQueueTimer > 0f)
        {
            vaultCameraQueueTimer -= Time.deltaTime;

            if (vaultCameraQueueTimer <= 0f)
            {
                vaultCameraQueuedFromJump = false;
            }
        }

        CleanVaultBlockerSet();

        if (PlayerInputLockManager.BlocksPlayerMovement)
        {
            input = Vector2.zero;

            if (anim != null)
                anim.SetFloat(speedParam, 0f);

            if (rb != null)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            SyncFacingToWeaponHitbox();
            return;
        }

        RefreshGroundedState();

        if (isVaulting)
        {
            input = Vector2.zero;

            if (anim != null)
                anim.SetFloat(speedParam, 0f);

            SyncFacingToWeaponHitbox();
            return;
        }

        if (externalMovementLock)
        {
            input = Vector2.zero;

            if (anim != null)
                anim.SetFloat(speedParam, 0f);

            SyncFacingToWeaponHitbox();
            return;
        }

        float x = 0f;

        if (kb.aKey.isPressed)
            x -= 1f;

        if (kb.dKey.isPressed)
            x += 1f;

        LocomotionState newState = LocomotionState.Idle;

        if (x > 0.01f)
            newState = LocomotionState.WalkRight;
        else if (x < -0.01f)
            newState = LocomotionState.WalkLeft;

        if (newState != locomotionState)
        {
            locomotionState = newState;
            locomotionStateSince = Time.time;
        }

        bool jumpReady = (Time.time - locomotionStateSince) >= jumpStateReadyTime;
        bool jumpPressed = IsSavedJumpPressed(kb);

        if (jumpPressed && canJump && jumpReady)
        {
            DoJumpOverride();
            return;
        }

        input = new Vector2(x, 0f);

        if (input.x > 0.01f)
        {
            sr.flipX = false;
            facing = Facing.Right;
            SyncFacingToWeaponHitbox();
        }
        else if (input.x < -0.01f)
        {
            sr.flipX = true;
            facing = Facing.Left;
            SyncFacingToWeaponHitbox();
        }

        if (anim != null)
            anim.SetFloat(speedParam, Mathf.Abs(input.x) > 0.001f ? 1f : 0f);
    }

    private void FixedUpdate()
    {
        if (postVaultYPinActive)
        {
            postVaultYPinTimer -= Time.fixedDeltaTime;

            Vector2 p = rb.position;
            p.y = postVaultPinnedY;
            rb.MovePosition(p);

            Vector2 pinnedVelocity = rb.linearVelocity;

            pinnedVelocity.x = input.x * moveSpeed;
            pinnedVelocity.y = 0f;

            rb.linearVelocity = pinnedVelocity;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;

            if (postVaultYPinTimer <= 0f)
            {
                postVaultYPinActive = false;
                rb.gravityScale = gravityScale;

                Vector2 releaseVelocity = rb.linearVelocity;
                releaseVelocity.y = 0f;
                rb.linearVelocity = releaseVelocity;
            }

            return;
        }

        if (isGroundLifting)
        {
            Vector2 liftVelocity = rb.linearVelocity;
            liftVelocity.x = input.x * moveSpeed;
            liftVelocity.y = 0f;

            rb.linearVelocity = liftVelocity;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
            return;
        }

        if (isVaulting)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (externalMovementLock)
        {
            Vector2 lockedVelocity = rb.linearVelocity;
            lockedVelocity.x = 0f;
            rb.linearVelocity = lockedVelocity;
            return;
        }

        Vector2 normalVelocity = rb.linearVelocity;
        normalVelocity.x = input.x * moveSpeed;
        rb.linearVelocity = normalVelocity;
    }

    private void LateUpdate()
    {
        if (!snapXPosition)
            return;

        if (pixelsPerUnit <= 0.0001f)
            return;

        if (isVaulting)
            return;

        if (postVaultYPinActive)
            return;

        float unit = 1f / pixelsPerUnit;
        Vector3 p = transform.position;
        p.x = Mathf.Round(p.x / unit) * unit;
        transform.position = p;
    }

    private void SetupChildHitbox(Collider2D hitbox, PlayerMoveChildHitboxKind kind)
    {
        if (hitbox == null)
            return;

        hitbox.isTrigger = true;

        PlayerMoveChildHitboxRelay relay = hitbox.GetComponent<PlayerMoveChildHitboxRelay>();

        if (relay == null)
            relay = hitbox.gameObject.AddComponent<PlayerMoveChildHitboxRelay>();

        relay.Configure(this, kind);
    }

    public void ReceiveChildHitboxTrigger(PlayerMoveChildHitboxKind kind, Collider2D other)
    {
        if (other == null)
            return;

        if (other.transform == transform || other.transform.IsChildOf(transform))
            return;

        if (kind == PlayerMoveChildHitboxKind.GroundLift)
        {
            TryGroundLift(other);
            return;
        }

        if (other.isTrigger)
            return;

        if (kind == PlayerMoveChildHitboxKind.VaultBlocker)
        {
            TryAddVaultBlocker(other);
            return;
        }

        if (kind == PlayerMoveChildHitboxKind.VaultLeft)
        {
            TryStartVault(other, -1);
            return;
        }

        if (kind == PlayerMoveChildHitboxKind.VaultRight)
        {
            TryStartVault(other, 1);
            return;
        }
    }

    public void ReceiveChildHitboxExit(PlayerMoveChildHitboxKind kind, Collider2D other)
    {
        if (other == null)
            return;

        if (kind == PlayerMoveChildHitboxKind.VaultBlocker)
        {
            if (vaultBlockers.Contains(other))
                vaultBlockers.Remove(other);
        }
    }

    private void TryAddVaultBlocker(Collider2D other)
    {
        if (other == null)
            return;

        if (other.isTrigger)
            return;

        if (other.transform == transform || other.transform.IsChildOf(transform))
            return;

        if (!LayerAllowed(other.gameObject.layer, vaultBlockerMask))
            return;

        vaultBlockers.Add(other);
    }

    private bool IsVaultBlockedByBlockerDetector()
    {
        CleanVaultBlockerSet();
        return vaultBlockers.Count > 0;
    }

    private void CleanVaultBlockerSet()
    {
        if (vaultBlockers.Count <= 0)
            return;

        vaultBlockers.RemoveWhere(collider =>
            collider == null ||
            !collider.enabled ||
            collider.isTrigger ||
            collider.transform == transform ||
            collider.transform.IsChildOf(transform)
        );
    }

    private void TryGroundLift(Collider2D other)
    {
        if (!enableGroundLift)
            return;

        if (groundLiftCooldownTimer > 0f)
            return;

        if (isGroundLifting)
            return;

        if (isVaulting)
            return;

        if (externalMovementLock)
            return;

        if (!isGrounded)
            return;

        if (requireMoveInputForGroundLift && Mathf.Abs(input.x) < 0.01f)
            return;

        if (other == null)
            return;

        if (ownCollider == null || rb == null)
            return;

        // small step only, no tag needed here
        Collider2D solidObstacleCollider = GetGroundLiftSolidCollider(other);

        if (solidObstacleCollider == null)
            return;

        if (!TryGetGroundLiftStepHeight(solidObstacleCollider, out float stepHeight))
            return;

        float targetY = rb.position.y + stepHeight + groundLiftSkin;

        if (groundLiftRoutine != null)
            StopCoroutine(groundLiftRoutine);

        groundLiftRoutine = StartCoroutine(GroundLiftToYRoutine(targetY));
    }

    private IEnumerator GroundLiftToYRoutine(float targetY)
    {
        isGroundLifting = true;
        groundLiftCooldownTimer = groundLiftCooldown;

        float startY = rb.position.y;
        float duration = Mathf.Max(0f, groundLiftMoveTime);

        if (duration <= 0f)
        {
            Vector2 instantPosition = rb.position;
            instantPosition.y = targetY;
            rb.MovePosition(instantPosition);
        }
        else
        {
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.fixedDeltaTime;

                float t = SmoothVaultT(timer / duration);
                Vector2 nextPosition = rb.position;
                nextPosition.y = Mathf.Lerp(startY, targetY, t);

                rb.gravityScale = 0f;
                rb.MovePosition(nextPosition);

                Vector2 v = rb.linearVelocity;
                v.x = input.x * moveSpeed;
                v.y = 0f;
                rb.linearVelocity = v;
                rb.angularVelocity = 0f;

                yield return new WaitForFixedUpdate();
            }
        }

        Vector2 finalPosition = rb.position;
        finalPosition.y = targetY;
        rb.MovePosition(finalPosition);

        Vector2 finalVelocity = rb.linearVelocity;
        finalVelocity.y = 0f;
        rb.linearVelocity = finalVelocity;
        rb.angularVelocity = 0f;
        rb.gravityScale = gravityScale;

        RefreshGroundedState();

        groundLiftCooldownTimer = groundLiftCooldown;
        isGroundLifting = false;
        groundLiftRoutine = null;
    }

    private Collider2D GetGroundLiftSolidCollider(Collider2D hitCollider)
    {
        if (hitCollider == null || ownCollider == null)
            return null;

        Collider2D bestCollider = null;
        float bestStepHeight = float.MaxValue;

        // grab the closest small solid, trigger can hit any tag now
        TryChooseGroundLiftCollider(hitCollider, ref bestCollider, ref bestStepHeight);

        if (bestCollider != null)
            return bestCollider;

        Transform searchRoot = GetGroundLiftSearchRoot(hitCollider);

        if (searchRoot == null)
            return bestCollider;

        Collider2D[] colliders = searchRoot.GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];

            TryChooseGroundLiftCollider(candidate, ref bestCollider, ref bestStepHeight);
        }

        return bestCollider;
    }

    private Transform GetGroundLiftSearchRoot(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return null;

        if (hitCollider.attachedRigidbody != null && hitCollider.attachedRigidbody.transform != transform)
            return hitCollider.attachedRigidbody.transform;

        if (hitCollider.transform.parent != null && !hitCollider.transform.parent.IsChildOf(transform))
            return hitCollider.transform.parent;

        return hitCollider.transform;
    }

    private void TryChooseGroundLiftCollider(
        Collider2D candidate,
        ref Collider2D bestCollider,
        ref float bestStepHeight
    )
    {
        if (!IsValidGroundLiftSolidCollider(candidate))
            return;

        if (!TryGetGroundLiftStepHeight(candidate, out float stepHeight))
            return;

        if (stepHeight >= bestStepHeight)
            return;

        bestStepHeight = stepHeight;
        bestCollider = candidate;
    }

    private bool IsValidGroundLiftSolidCollider(Collider2D candidate)
    {
        if (candidate == null)
            return false;

        if (!candidate.enabled)
            return false;

        if (candidate.isTrigger)
            return false;

        if (candidate.transform == transform || candidate.transform.IsChildOf(transform))
            return false;

        if (useGroundLiftLayerMask && !LayerAllowed(candidate.gameObject.layer, groundLiftMask))
            return false;

        return true;
    }

    private bool TryGetGroundLiftStepHeight(Collider2D candidate, out float stepHeight)
    {
        stepHeight = 0f;

        if (candidate == null || ownCollider == null)
            return false;

        Bounds playerBounds = ownCollider.bounds;
        Bounds obstacleBounds = candidate.bounds;

        stepHeight = obstacleBounds.max.y - playerBounds.min.y;

        if (stepHeight < groundLiftMinStepHeight - groundLiftSkin)
            return false;

        if (stepHeight > groundLiftMaxStepHeight + groundLiftSkin)
            return false;

        return true;
    }

    private void TryStartVault(Collider2D other, int direction)
    {
        if (!enableVault)
            return;

        if (isVaulting)
            return;

        if (vaultCooldownTimer > 0f)
            return;

        if (externalMovementLock)
            return;

        if (onlyVaultWhileAirborne && !isAirborne)
            return;

        if (requireJumpBeforeVault && !hasJumpedSinceLastGrounded)
            return;

        if (IsVaultBlockedByBlockerDetector())
            return;

        if (!IsVaultTaggedObject(other))
            return;

        if (!LayerAllowed(other.gameObject.layer, vaultMask))
            return;

        if (ownCollider == null || rb == null)
            return;

        Collider2D vaultCollider = GetBestVaultCollider(other);

        if (vaultCollider == null)
            return;

        if (vaultRoutine != null)
            StopCoroutine(vaultRoutine);

        TryActivateQueuedVaultCameraDrop();

        hasJumpedSinceLastGrounded = false;
        vaultRoutine = StartCoroutine(VaultRoutine(vaultCollider, direction));
    }

    private Collider2D GetBestVaultCollider(Collider2D triggerHitCollider)
    {
        if (!findNearestVaultTaggedCollider)
            return triggerHitCollider;

        Collider2D bestCollider = null;
        float bestDistance = float.MaxValue;

        Collider2D[] allColliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);

        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider2D candidate = allColliders[i];

            if (candidate == null)
                continue;

            if (!candidate.enabled)
                continue;

            if (candidate.isTrigger)
                continue;

            if (!IsVaultTaggedObject(candidate))
                continue;

            if (!LayerAllowed(candidate.gameObject.layer, vaultMask))
                continue;

            float distance = Vector2.Distance(rb.position, candidate.bounds.center);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCollider = candidate;
            }
        }

        if (bestCollider != null)
            return bestCollider;

        return triggerHitCollider;
    }

    private bool IsVaultTaggedObject(Collider2D other)
    {
        if (other == null)
            return false;

        if (string.IsNullOrEmpty(vaultTag))
            return false;

        if (other.CompareTag(vaultTag))
            return true;

        if (other.transform.parent != null && other.transform.parent.CompareTag(vaultTag))
            return true;

        return false;
    }

    private IEnumerator VaultRoutine(Collider2D targetCollider, int direction)
    {
        isVaulting = true;
        postVaultYPinActive = false;
        input = Vector2.zero;

        SetVaultSensorsEnabled(false);

        canJump = false;
        isGrounded = false;
        isAirborne = true;

        if (direction < 0)
            ForceFacingLeft();
        else
            ForceFacingRight();

        if (anim != null)
        {
            anim.SetFloat(speedParam, 0f);

            if (!string.IsNullOrEmpty(vaultBool))
                anim.SetBool(vaultBool, true);
        }

        float oldGravity = rb.gravityScale;
        bool oldOwnColliderEnabled = false;

        if (ownCollider != null)
            oldOwnColliderEnabled = ownCollider.enabled;

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        Vector2 startPosition = rb.position;

        CalculateVaultPath(
            targetCollider,
            direction,
            startPosition,
            out Vector2 risePoint,
            out Vector2 overPoint,
            out Vector2 landPoint
        );

        NotifyVaultCameraLandingY(landPoint.y);

        if (disablePlayerBodyColliderDuringVault && ownCollider != null)
            ownCollider.enabled = false;

        if (vaultLatchTime > 0f)
            yield return LockExactVaultPosition(startPosition, vaultLatchTime);

        if (vaultRiseTime > 0f)
            yield return MoveVaultPosition(startPosition, risePoint, vaultRiseTime);

        if (vaultOverTime > 0f)
            yield return MoveVaultPosition(risePoint, overPoint, vaultOverTime);

        if (vaultSettleDownTime > 0f)
            yield return MoveVaultPosition(overPoint, landPoint, vaultSettleDownTime);

        if (vaultFinalHoldTime > 0f)
            yield return LockExactVaultPosition(landPoint, vaultFinalHoldTime);

        Vector2 lockedRestorePosition = landPoint;

        rb.MovePosition(lockedRestorePosition);
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (vaultColliderRestoreDelay > 0f)
            yield return LockExactVaultPosition(lockedRestorePosition, vaultColliderRestoreDelay);

        if (disablePlayerBodyColliderDuringVault && ownCollider != null)
            ownCollider.enabled = oldOwnColliderEnabled;

        if (vaultColliderRestorePositionLockTime > 0f)
            yield return LockExactVaultPosition(lockedRestorePosition, vaultColliderRestorePositionLockTime);

        rb.MovePosition(lockedRestorePosition);
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (pinYAfterVault)
        {
            postVaultPinnedY = lockedRestorePosition.y;
            postVaultYPinTimer = postVaultYPinTime;
            postVaultYPinActive = true;
            rb.gravityScale = 0f;
        }
        else
        {
            rb.gravityScale = oldGravity;
        }

        if (anim != null)
        {
            if (!string.IsNullOrEmpty(vaultBool))
                anim.SetBool(vaultBool, false);
        }

        isVaulting = false;
        vaultRoutine = null;
        vaultCooldownTimer = vaultCooldown;

        RefreshGroundedState();

        yield return new WaitForSeconds(vaultCooldown);

        SetVaultSensorsEnabled(true);
    }

    private void CalculateVaultPath(
        Collider2D targetCollider,
        int direction,
        Vector2 startPosition,
        out Vector2 risePoint,
        out Vector2 overPoint,
        out Vector2 landPoint
    )
    {
        Bounds targetBounds = targetCollider.bounds;
        Bounds playerBounds = ownCollider.bounds;

        float pivotToColliderBottom = rb.position.y - playerBounds.min.y;

        float finalY =
            targetBounds.max.y
            + pivotToColliderBottom
            + vaultFinalYFromVaultTop
            + finalVaultYEdit;

        float clearY =
            targetBounds.max.y
            + pivotToColliderBottom
            + vaultTopClearance
            + finalVaultYEdit;

        if (clearY < finalY)
            clearY = finalY;

        float finalX;

        if (direction > 0)
            finalX = targetBounds.min.x + vaultLandInward;
        else
            finalX = targetBounds.max.x - vaultLandInward;

        finalX += finalVaultXEdit;

        risePoint = new Vector2(startPosition.x, clearY);
        overPoint = new Vector2(finalX, clearY);
        landPoint = new Vector2(finalX, finalY);
    }

    private IEnumerator MoveVaultPosition(Vector2 startPosition, Vector2 endPosition, float duration)
    {
        float timer = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (timer < duration)
        {
            timer += Time.fixedDeltaTime;

            float rawT = Mathf.Clamp01(timer / duration);
            float smoothT = SmoothVaultT(rawT);

            Vector2 nextPosition = Vector2.Lerp(startPosition, endPosition, smoothT);

            rb.gravityScale = 0f;
            rb.MovePosition(nextPosition);
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(endPosition);
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private IEnumerator LockExactVaultPosition(Vector2 lockedPosition, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.fixedDeltaTime;

            rb.gravityScale = 0f;
            rb.MovePosition(lockedPosition);
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(lockedPosition);
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void SetVaultSensorsEnabled(bool state)
    {
        if (!disableVaultSensorsWhileVaulting)
            return;

        if (vaultLeftCollider != null)
            vaultLeftCollider.enabled = state;

        if (vaultRightCollider != null)
            vaultRightCollider.enabled = state;
    }

    private float SmoothVaultT(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (6f * t - 15f) + 10f);
    }

    private bool LayerAllowed(int objectLayer, LayerMask mask)
    {
        return (mask.value & (1 << objectLayer)) != 0;
    }

    private bool IsSavedJumpPressed(Keyboard kb)
    {
        if (keybindSync != null)
            return keybindSync.WasActionPressedThisFrame(jumpActionId, fallbackJumpKey);

        return WasPressed(kb, jumpKey);
    }

    private void DoJumpOverride()
    {
        if (externalMovementLock)
            return;

        if (isGroundLifting)
            return;

        if (isVaulting)
            return;

        if (postVaultYPinActive && jumpCancelsPostVaultYPin)
        {
            postVaultYPinActive = false;
            rb.gravityScale = gravityScale;
        }

        rb.gravityScale = gravityScale;

        Vector2 v = rb.linearVelocity;
        v.y = jumpForce;
        rb.linearVelocity = v;

        canJump = false;
        isGrounded = false;
        isAirborne = true;
        forcedAirborneTimer = forcedAirborneAfterJumpTime;
        hasJumpedSinceLastGrounded = true;
        QueueVaultCameraRequestFromJump();

        if (anim != null && !string.IsNullOrEmpty(jumpTrigger))
        {
            anim.ResetTrigger(jumpTrigger);
            anim.SetTrigger(jumpTrigger);
        }
    }

    public void SetExternalMovementLock(bool state)
    {
        externalMovementLock = state;

        if (state)
        {
            input = Vector2.zero;

            if (rb != null)
            {
                Vector2 v = rb.linearVelocity;
                v.x = 0f;
                rb.linearVelocity = v;
                rb.angularVelocity = 0f;
            }

            if (anim != null)
                anim.SetFloat(speedParam, 0f);
        }
    }

    public void SetExternalAirborneLock(bool state)
    {
        externalAirborneLock = state;

        if (state)
        {
            isAirborne = true;
            isGrounded = false;
            canJump = false;
        }
    }

    public void ForceAirborneForSeconds(float seconds)
    {
        forcedAirborneTimer = Mathf.Max(forcedAirborneTimer, seconds);
        isAirborne = true;
        isGrounded = false;
        canJump = false;
    }

    public void ForceFacingRight()
    {
        facing = Facing.Right;

        if (sr != null)
            sr.flipX = false;

        SyncFacingToWeaponHitbox();
    }

    public void ForceFacingLeft()
    {
        facing = Facing.Left;

        if (sr != null)
            sr.flipX = true;

        SyncFacingToWeaponHitbox();
    }

    private void QueueVaultCameraRequestFromJump()
    {
        if (!requestCameraDropOnConfirmedVault)
            return;

        vaultCameraQueuedFromJump = true;
        vaultCameraQueueTimer = Mathf.Max(0.01f, vaultCameraJumpQueueTime);
    }

    private void TryActivateQueuedVaultCameraDrop()
    {
        if (!requestCameraDropOnConfirmedVault)
            return;

        if (!vaultCameraQueuedFromJump)
            return;

        CacheCameraRigFollowController();

        if (cameraRigFollowController == null)
            return;

        cameraRigFollowController.AddVaultCameraDropFromConfirmedVault(
            vaultCameraDownAmountPerVault,
            vaultCameraClearBelowLandingY
        );

        vaultCameraQueuedFromJump = false;
        vaultCameraQueueTimer = 0f;
    }

    private void NotifyVaultCameraLandingY(float landingY)
    {
        if (!requestCameraDropOnConfirmedVault)
            return;

        CacheCameraRigFollowController();

        if (cameraRigFollowController != null)
            cameraRigFollowController.NotifyVaultCameraLandingY(landingY);
    }

    private void CacheCameraRigFollowController()
    {
        if (cameraRigFollowController != null)
            return;

        if (!autoFindCameraRigFollowController)
            return;

        if (CameraRigFollowController.Instance != null)
        {
            cameraRigFollowController = CameraRigFollowController.Instance;
            return;
        }

        cameraRigFollowController = Object.FindFirstObjectByType<CameraRigFollowController>();
    }

    private void SyncFacingToWeaponHitbox()
    {
        if (!syncWeaponHitboxFacing)
            return;

        CacheWeaponHitbox();

        if (weaponHitbox != null)
            weaponHitbox.SetFacing(FacingXPublic);
    }

    private void CacheWeaponHitbox()
    {
        if (weaponHitbox == null)
            weaponHitbox = GetComponentInChildren<WeaponHitbox>(true);
    }

    private bool WasPressed(Keyboard keyboard, Key key)
    {
        if (keyboard == null)
            return false;

        if (key == Key.None)
            return false;

        return keyboard[key] != null && keyboard[key].wasPressedThisFrame;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckLanding(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckLanding(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        RefreshGroundedState();
    }

    private void CheckLanding(Collision2D collision)
    {
        if (isVaulting)
        {
            isAirborne = true;
            isGrounded = false;
            canJump = false;
            return;
        }

        if (externalAirborneLock || forcedAirborneTimer > 0f)
        {
            isAirborne = true;
            isGrounded = false;
            canJump = false;
            return;
        }

        bool groundedFromContacts = false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);

            if (contact.normal.y > groundedNormalMinY)
            {
                groundedFromContacts = true;
                break;
            }
        }

        if (groundedFromContacts)
        {
            canJump = true;
            isGrounded = true;
            isAirborne = false;
            hasJumpedSinceLastGrounded = false;
        }
        else
        {
            RefreshGroundedState();
        }
    }

    private void RefreshGroundedState()
    {
        if (isVaulting)
        {
            canJump = false;
            isGrounded = false;
            isAirborne = true;
            return;
        }

        if (externalAirborneLock || forcedAirborneTimer > 0f)
        {
            canJump = false;
            isGrounded = false;
            isAirborne = true;
            return;
        }

        if (postVaultYPinActive)
        {
            canJump = true;
            isGrounded = true;
            isAirborne = false;
            return;
        }

        Collider2D castCollider = groundCheckCollider != null ? groundCheckCollider : ownCollider;

        if (castCollider == null || !castCollider.enabled)
            return;

        int hitCount = castCollider.Cast(
            Vector2.down,
            groundFilter,
            groundHits,
            groundedCastDistance
        );

        bool grounded = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundHits[i];

            if (hit.collider == null)
                continue;

            if (hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.normal.y >= groundedNormalMinY)
            {
                grounded = true;
                break;
            }
        }

        canJump = grounded;
        isGrounded = grounded;
        isAirborne = !grounded;

        if (grounded)
            hasJumpedSinceLastGrounded = false;
    }
}

public class PlayerMoveChildHitboxRelay : MonoBehaviour
{
    private PlayerMoveController owner;
    private PlayerMoveChildHitboxKind kind;

    public void Configure(PlayerMoveController newOwner, PlayerMoveChildHitboxKind newKind)
    {
        owner = newOwner;
        kind = newKind;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // pass hit up, controller decides what kind of move this is
        if (owner != null)
            owner.ReceiveChildHitboxTrigger(kind, other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // same check again so slow pushes still lift
        if (owner != null)
            owner.ReceiveChildHitboxTrigger(kind, other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (owner != null)
            owner.ReceiveChildHitboxExit(kind, other);
    }
}