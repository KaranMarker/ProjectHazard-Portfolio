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
using UnityEngine.SceneManagement;

public class BossAttackController : MonoBehaviour
{
    [Header("Main Boss Animator")]
    [SerializeField] private Animator bossAnimator;

    [Header("Cutscene Gate")]
    [SerializeField] private bool waitForMagnusIntroCutscene = true;
    [SerializeField] private bool debugCutsceneGate = false;

    [Header("Magnus Arena Entry Delays")]
    [Tooltip("If true, this controller reads the delay values set by MagnusBossCutscene before the scene loaded. This keeps timing working across different scenes.")]
    [SerializeField] private bool usePortalRequestedDelays = false;

    [Tooltip("Fallback minimum delay after the intro cutscene gate opens before boss attacks begin.")]
    [SerializeField] private float bossAttackStartDelayMinAfterCutsceneGate = 2f;

    [Tooltip("Fallback maximum delay after the intro cutscene gate opens before boss attacks begin.")]
    [SerializeField] private float bossAttackStartDelayMaxAfterCutsceneGate = 3f;

    [Tooltip("Fallback delay after the intro cutscene gate opens before the health bar appears.")]
    [SerializeField] private float healthBarShowDelayAfterCutsceneGate = 1f;

    [Tooltip("Usually true. Health bar can appear during the boss start delay instead of waiting until attacks begin.")]
    [SerializeField] private bool showHealthBarDuringBossStartDelay = true;

    [Header("Boss Health Bar / Temporary Attack 2 Receiver Reference")]
    [Tooltip("Optional reference to the temporary Attack 2 damage receiver. This is NOT the shared HP owner.")]
    [SerializeField] private MagnusAttack2DamageReciever magnusDamageReceiver;
    [SerializeField] private MagnusHealthBarRigController magnusHealthBar;
    [SerializeField] private bool showHealthBarAfterCutsceneGate = true;

    [Header("Shared Magnus HP - OWNED ONLY BY THIS CONTROLLER")]
    [Tooltip("This is the real Magnus HP. Weapon, ghost explosion, and Dial Charge receivers all report here.")]
    [SerializeField] private int sharedMagnusMaxHp = 8;

    [Tooltip("Usually true. Initializes shared HP to full when the boss controller wakes up.")]
    [SerializeField] private bool resetSharedHpOnAwake = true;

    [Tooltip("If true, damage immediately reveals/syncs the CameraRig MagnusHealthBarRigController.")]
    [SerializeField] private bool revealHealthBarWhenDamageIsReceived = true;

    [Tooltip("If true, the boss damaged interrupt plays after non-lethal damage.")]
    [SerializeField] private bool interruptBossOnNonLethalSharedDamage = true;

    [SerializeField] private bool debugSharedMagnusHp = true;

    [Header("Shared Magnus Damage Queue")]
    [Tooltip("Every accepted Magnus damage report is processed here one HP at a time. 2 damage becomes -1, wait, then another -1.")]
    [SerializeField] private float sharedDamageTickDelay = 0.2f;

    [Header("Damage Interrupt")]
    [SerializeField] private string damagedBoolName = "damaged";

    [Tooltip("When Magnus dies, keep damaged=true while attack5setup starts, so the animator goes Damaged -> Attack5 Setup -> Dead instead of returning to default.")]
    [SerializeField] private bool keepDamagedBoolTrueDuringBossDeath = true;

    [Tooltip("Exact animator state name for the final-hit damaged pose. This should be your damagedfrom state from the Animator graph.")]
    [SerializeField] private string finalDeathDamagedStateName = "damagedfrom";

    [Tooltip("If true, the script forces the Animator into finalDeathDamagedStateName before setting attack5setup. This guarantees final hit goes Damaged -> Attack 5 instead of continuing an old attack.")]
    [SerializeField] private bool forceFinalDeathDamagedStateBeforeAttack5 = true;

    [Tooltip("Lets the Animator enter damagedfrom for one frame before attack5setup becomes true. Needed for damagedfrom -> deathanim transitions.")]
    [SerializeField] private bool waitOneFrameBeforeAttack5Setup = true;

    [SerializeField] private float damageInterruptPauseBeforeRestart = 0.75f;
    [SerializeField] private bool blowUpGhostsWhenMagnusDamaged = true;

    [Header("Platform Recharge State")]
    [Tooltip("Bool on the main Magnus animator that plays the recharge/platform reset animation.")]
    [SerializeField] private string platformRechargeBoolName = "recharge";

    [Tooltip("Used if another script triggers recharge without passing a custom duration.")]
    [SerializeField] private float defaultPlatformRechargeDuration = 0.8f;

    [Tooltip("If true, all current attacks/portals/hitboxes are cleaned up when recharge starts.")]
    [SerializeField] private bool cleanupAttacksWhenPlatformRechargeStarts = true;

    [SerializeField] private bool debugPlatformRecharge = true;

    [Header("Attack 4 / Vault Restore Setup")]
    [Tooltip("Bool on the MAIN MAGNUS animator. BossAttackController owns this bool.")]
    [SerializeField] private string attack4SetupBoolName = "attack4setup";

    [Tooltip("Total time attack4setup stays true. This is longer because the timer variant first shakes/kills vaults, then revives them.")]
    [SerializeField] private float attack4SetupDuration = 2.5f;

    [Tooltip("How many seconds into attack4setup before vaults start moving/restoring.")]
    [SerializeField] private float attack4PlatformRestoreStartTime = 0.5f;

    [Tooltip("Delay between each vault starting its revive during Attack 4.")]
    [SerializeField] private float attack4PlatformRestoreInterval = 0.25f;

    [Header("Attack 4 Turn Timer Variant")]
    [Tooltip("If true, BossAttackController queues an Attack 4 variant after a random number of completed normal attacks.")]
    [SerializeField] private bool enableTurnTimerAttack4Variant = true;

    [Tooltip("Minimum completed normal attacks before the timer variant can queue Attack 4.")]
    [SerializeField] private int minNormalAttacksBeforeAttack4Variant = 6;

    [Tooltip("Maximum completed normal attacks before the timer variant can queue Attack 4.")]
    [SerializeField] private int maxNormalAttacksBeforeAttack4Variant = 8;

    [Tooltip("Maximum time Attack 4 waits for vaults to finish their normal shake/dead flow before revive begins.")]
    [SerializeField] private float attack4VariantVaultSelfKillWaitTime = 1f;

    [SerializeField] private bool debugAttack4TurnTimerVariant = true;

    [Header("Attack 5 / Boss Death")]
    [Tooltip("Bool on the MAIN MAGNUS animator. This plays the boss death setup / dying animation.")]
    [SerializeField] private string attack5SetupBoolName = "attack5setup";

    [Tooltip("How many seconds after attack5setup becomes true before dead1 becomes true. attack5setup stays true during this delay.")]
    [SerializeField] private float dead1StartDelayAfterAttack5Setup = 1.25f;

    [Tooltip("First boss-death bool on the MAIN MAGNUS animator. This turns on after attack5setup finishes.")]
    [SerializeField] private string bossDead1BoolName = "dead1";

    [Tooltip("How long dead1 stays true before dead2 takes over.")]
    [SerializeField] private float bossDead1Duration = 2f;

    [Tooltip("Final boss-death bool on the MAIN MAGNUS animator. This stays true until the scene unloads.")]
    [SerializeField] private string bossDead2BoolName = "dead2";

    [Tooltip("Optional final animator state name to play after attack5setup finishes. Usually leave this false and drive death with dead1/dead2.")]
    [SerializeField] private string bossDeadAnimationStateName = "deathanim";

    [Tooltip("If true, script directly plays bossDeadAnimationStateName after attack5setup. Usually leave this false and let dead1/dead2 drive the Animator.")]
    [SerializeField] private bool forceDeadAnimationStateAfterAttack5 = false;

    [Tooltip("Vault manager whose assigned vaults collapse when Magnus dies. If empty, script auto-finds one.")]
    [SerializeField] private VaultSmashRechargeManager vaultSmashRechargeManager;

    [Tooltip("If true, every assigned vault in the recharge manager is forced into dead state when Magnus dies.")]
    [SerializeField] private bool collapseVaultsOnBossDeath = true;

    [Header("Boss Death Exit")]
    [Tooltip("If true, the hidden exit object is turned on when Magnus reaches dead2.")]
    [SerializeField] private bool revealExitObjectOnBossDeath = true;

    [Tooltip("Optional direct reference to the hidden exit object. If empty, the script searches the loaded scene for an inactive object with bossDeathExitTag.")]
    [SerializeField] private GameObject bossDeathExitObject;

    [Tooltip("Tag used to find the hidden exit object if bossDeathExitObject is not assigned.")]
    [SerializeField] private string bossDeathExitTag = "exit";

    [SerializeField] private bool debugBossDeath = true;

    [Header("Vault Smash Recharge Queue")]
    [Tooltip("If true, VaultSmashRechargeManager can queue Attack 4 as a real boss attack.")]
    [SerializeField] private bool allowVaultSmashRechargeQueue = true;

    [Tooltip("If true, Attack 4 happens randomly somewhere from attack slot 1 to the requested max slot. If false, it happens exactly on the requested slot.")]
    [SerializeField] private bool randomizeVaultRechargeAttackSlotWithinWindow = true;

    [Tooltip("If true, normal cooldown runs after Attack 4 finishes.")]
    [SerializeField] private bool useCooldownAfterVaultRechargeAttack = true;

    [SerializeField] private bool debugVaultSmashRechargeQueue = true;

    [Header("Attack Chances")]
    [SerializeField] private float attack1ChanceWeight = 33f;
    [SerializeField] private float attack2ChanceWeight = 33f;
    [SerializeField] private float attack3ChanceWeight = 34f;

    [Header("Dynamic Attack Chance System")]
    [Tooltip("After an attack plays, that same attack's chance is multiplied by this on the next roll. 0.5 means half.")]
    [SerializeField] private float repeatAttackChanceMultiplier = 0.5f;

    [Tooltip("If true, the cut chance from the repeated attack is shared by the other two attacks.")]
    [SerializeField] private bool shareCutChanceWithOtherAttacks = true;

    [Tooltip("If true, Attack 3 gets boosted while a background ghost is alive.")]
    [SerializeField] private bool boostAttack3ChanceWhileGhostAlive = true;

    [Tooltip("Attack 3 multiplier while a background ghost is alive. 2 means double Attack 3 chance by cutting from Attack 1 and Attack 2.")]
    [SerializeField] private float attack3GhostAliveChanceMultiplier = 2f;

    [SerializeField] private bool debugAttackChanceRolls = false;

    [Header("Attack Cooldown After Attack Finishes")]
    [SerializeField] private float cooldownAfterAttackMin = 2f;
    [SerializeField] private float cooldownAfterAttackMax = 3f;

    [Header("Attack 1 Timing - Ultimate Flow")]
    [Tooltip("Main Magnus animator keeps attack1Intro true for this long. No Attack 1 damage can happen here.")]
    [SerializeField] private float attack1IntroDuration = 0.65f;

    [Tooltip("Total time the MAIN MAGNUS attack1Hold bool stays true after intro ends. If this is shorter than track time + beam true time, the script extends hold long enough so the whole beam flow still happens inside attack1Hold.")]
    [SerializeField] private float attack1HoldTotalDuration = 2.25f;

    [Tooltip("After attack1Hold becomes true, the child beam object tracks the player's Y for this long while attackbeam stays false.")]
    [SerializeField] private float attack1HoldTrackPlayerYBeforeBeamTime = 1f;

    [Tooltip("After tracking locks, attackbeam stays true for this long.")]
    [SerializeField] private float attack1BeamTrueDuration = 1f;

    [Tooltip("After attackbeam becomes true, wait this long before turning on the grandchild damage hitbox. The hitbox stays on until attackbeam turns false.")]
    [SerializeField] private float attack1HitboxDelayAfterBeamStarts = 0.1f;

    [Header("Attack 1 Beam Y Tracking")]
    [Tooltip("If true, the beam object's Y follows the player's Y while Attack 1 hold is active.")]
    [SerializeField] private bool attack1BeamFollowsPlayerY = true;

    [Tooltip("Optional exact transform to move on Y. If empty, attack1Object is moved.")]
    [SerializeField] private Transform attack1BeamYRoot;

    [Tooltip("Y offset added to player Y when placing the beam.")]
    [SerializeField] private float attack1BeamPlayerYOffset = 0f;

    [Tooltip("Higher = faster beam Y adjustment. Use 999 for nearly instant.")]
    [SerializeField] private float attack1BeamYFollowSpeed = 999f;

    [Header("Attack 1 Damage Hitbox")]
    [Tooltip("Grandchild damage hitbox object. BossAttackController is the only script that should turn this object on during Attack 1.")]
    [SerializeField] private GameObject attack1HitboxObject;

    [Header("Attack 2 Timing")]
    [SerializeField] private float attack2SetupDuration = 0.8f;
    [SerializeField] private float attack2IdleDuration = 2.5f;
    [SerializeField] private float attack2ShadowLockBeforeSmash = 0.3f;
    [SerializeField] private float attack2HolderDelayAfterShadowLock = 0f;
    [SerializeField] private float attack2LiftDelayAfterHolder = 0.15f;
    [SerializeField] private Vector3 attack2ObjectLiftOffsetAfterLock = new Vector3(0f, 4f, 0f);
    [SerializeField] private float attack2DelayAfterLiftBeforeArmSmashDown = 1f;
    [SerializeField] private float attack2ChildSmashDelayAfterMainSmash = 0.1f;
    [SerializeField] private float attack2ArmSmashDownDuration = 0.45f;

    [Header("Attack 2 Hitbox Animation Event")]
    [SerializeField] private GameObject attack2ArmHitboxObject;
    [SerializeField] private GameObject attack2VaultDetectorObject;
    [SerializeField] private float attack2HitboxActiveTime = 0.2f;

    [Header("Attack 2 Hitbox Placement")]
    [SerializeField] private bool placeHitboxesAtLockedShadowPosition = true;
    [SerializeField] private Vector3 attack2HitboxWorldOffset = Vector3.zero;

    [Header("Magnus Attack 2 Damage Receiver Window")]
    [Tooltip("Temporary vulnerability object for Attack 2 only. It should have MagnusAttack2DamageReciever and should normally start inactive.")]
    [SerializeField] private GameObject attack2DamageReceiverObject;

    [Tooltip("Delay after Attack 2 starts before the temporary damage receiver turns on. Use 0 if you want it active for nearly the whole Attack 2.")]
    [SerializeField] private float attack2DamageReceiverDelayAfterAttackStart = 0f;

    [Tooltip("After the arm-smash-down begins, keep the temporary receiver alive for this many more seconds, then turn it off. Your requested default is 0.2.")]
    [SerializeField] private float attack2DamageReceiverTurnOffDelayAfterArmComesDown = 0.2f;

    [Tooltip("If true, the receiver is placed at the locked shadow/smash position when the shadow locks and again when the arm comes down.")]
    [SerializeField] private bool placeAttack2DamageReceiverAtLockedShadowPosition = true;

    [Tooltip("Extra world offset for the temporary Attack 2 damage receiver, added to the locked shadow/smash position.")]
    [SerializeField] private Vector3 attack2DamageReceiverWorldOffset = Vector3.zero;

    [Header("Attack 2 Pull Up End")]
    [SerializeField] private bool waitForArmPullUpAnimationToFinish = true;
    [SerializeField] private string attack2ChildArmPullUpStateName = "retr";
    [SerializeField] private float attack2ArmPullUpFallbackDuration = 0.4f;

    [Header("Attack 2 Follow Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Vector3 attack2ShadowFollowOffset = Vector3.zero;
    [SerializeField] private float attack2FollowSpeed = 25f;
    [SerializeField] private bool attack2SnapToPlayerOnFollowStart = true;

    [Header("Attack 3 Timing")]
    [SerializeField] private float attack3SetupDuration = 0.8f;
    [SerializeField] private float attack3MainDuration = 3f;

    [Header("Attack 3 Hitbox")]
    [SerializeField] private GameObject attack3ExplosionDamageHitboxObject;
    [SerializeField] private MagnusAttack3ExplosionDamageReceiver attack3ExplosionDamageReceiver;

    [Header("Attack 3 Warp Portals")]
    [Tooltip("Assign your portal arrow objects here. Attack 3 main fires 1-3 of them.")]
    [SerializeField] private MagnusAttack3PortalArrow[] attack3Portals;

    [Tooltip("Guaranteed minimum. Keep this at 1.")]
    [SerializeField] private int attack3MinPortalsToFire = 1;

    [Tooltip("Maximum portals fired during one Attack 3. Keep this at 3.")]
    [SerializeField] private int attack3MaxPortalsToFire = 3;

    [Header("Attack Child Objects")]
    [SerializeField] private GameObject attack1Object;
    [SerializeField] private GameObject attack2Object;
    [SerializeField] private GameObject attack3Object;

    [Header("Attack 1 Child Beam Animator")]
    [SerializeField] private Animator attack1BeamAnimator;
    [SerializeField] private string attack1BeamBoolName = "attackbeam";

    [Header("Attack 2 Child Animator")]
    [SerializeField] private Animator attack2ChildAnimator;
    [SerializeField] private string attack2ChildHolderBoolName = "holder";
    [SerializeField] private string attack2ChildArmSmashDownBoolName = "armsmashdown";
    [SerializeField] private string attack2ChildArmPullUpBoolName = "armpullup";
    [SerializeField] private string attack2ChildHolderStateName = "holder";
    [SerializeField] private string attack2ChildArmSmashDownStateName = "armattack";

    [Header("Main Boss Animator Parameters")]
    [SerializeField] private string attack1IntroBoolName = "attack1Intro";
    [SerializeField] private string attack1HoldBoolName = "attack1Hold";
    [SerializeField] private string attack2SetupBoolName = "attack2Setup";
    [SerializeField] private string attack2IdleBoolName = "attack2Idle";
    [SerializeField] private string attack2SmashBoolName = "attack2Smash";
    [SerializeField] private string attack3SetupBoolName = "attack3Setup";
    [SerializeField] private string attack3MainBoolName = "attack3Main";

    [Header("Background Ghost Attack During Attack 1 / Attack 2")]
    [SerializeField] private bool allowBackgroundAttackDuringAttack1And2 = true;

    [Range(0f, 100f)]
    [SerializeField] private float backgroundDuringAttackChancePercent = 30f;

    [SerializeField] private bool blockNewBackgroundAttackWhileGhostAlive = true;

    [Header("Background Attack Special Effects")]
    [SerializeField] private GameObject backgroundDuringAttackSpecialEffectsObject;
    [SerializeField] private Animator backgroundDuringAttackSpecialEffectsAnimator;
    [SerializeField] private string backgroundDuringAttackSpecialEffectsBoolName = "backgroundattack";
    [SerializeField] private float backgroundDuringAttackSpecialEffectsDuration = 1.5f;

    [Header("Background Ghosts")]
    [Tooltip("If true, assigned background ghost GameObjects start off and are turned on only when that ghost attack begins.")]
    [SerializeField] private bool startBackgroundGhostObjectsInactive = true;

    [SerializeField] private BackgroundGhostSettings guaranteedBackgroundGhost = new BackgroundGhostSettings();
    [SerializeField] private BackgroundGhostSettings chanceBackgroundGhost = new BackgroundGhostSettings();

    [Range(0f, 100f)]
    [SerializeField] private float chanceBackgroundGhostSpawnPercent = 50f;

    [Header("Background Ghost Second Spawn Scaling")]
    [Tooltip("Base ghost always spawns whenever the background ghost attack starts. The chance ghost is controlled separately below.")]
    [SerializeField] private bool alwaysSpawnBaseBackgroundGhost = true;

    [Tooltip("If true, the special/chance ghost uses the scaling rule below instead of the flat chance above.")]
    [SerializeField] private bool useScalingChanceGhostAfterCompletedAttacks = true;

    [Tooltip("The second ghost starts becoming possible after this many completed normal boss attacks.")]
    [SerializeField] private int completedAttacksBeforeChanceGhostCanSpawn = 5;

    [Tooltip("Chance for the second ghost on the first attack after the threshold. Example: after 5 completed attacks, attack 6 has this chance.")]
    [Range(0f, 100f)]
    [SerializeField] private float chanceGhostPercentOnFirstAttackAfterThreshold = 10f;

    [Tooltip("How much the second ghost chance increases each attack after the threshold.")]
    [Range(0f, 100f)]
    [SerializeField] private float chanceGhostPercentIncreasePerAttackAfterThreshold = 10f;

    [Tooltip("Maximum chance for the second ghost.")]
    [Range(0f, 100f)]
    [SerializeField] private float chanceGhostMaxPercent = 100f;

    [Header("Debug")]
    [SerializeField] private bool debugBackgroundGhosts = false;
    [SerializeField] private bool debugAttack1Hitbox = false;
    [SerializeField] private bool debugAttack2Hitboxes = false;
    [SerializeField] private bool debugAttack2DamageReceiver = false;
    [SerializeField] private bool debugAttack3 = true;
    [SerializeField] private bool debugDamageInterrupt = true;

    [Header("Debug Instant Kill")]
    [Tooltip("If true, pressing the number 1 key instantly starts the boss death flow.")]
    [SerializeField] private bool enableNumber1InstantKill = true;

    [Tooltip("Keyboard key that starts the instant boss death flow.")]
    [SerializeField] private Key instantKillKey = Key.Digit1;

    [Tooltip("If true, the keypad 1 key also starts the instant boss death flow.")]
    [SerializeField] private bool instantKillAlsoAcceptsKeypad1 = true;

    private Coroutine attackLoopCoroutine;
    private Coroutine activeNormalAttackCoroutine;
    private Coroutine activeBackgroundDuringAttackCoroutine;
    private Coroutine attack2HitboxCoroutine;
    private Coroutine attack2DamageReceiverCoroutine;
    private Coroutine damageInterruptCoroutine;
    private Coroutine platformRechargeCoroutine;
    private Coroutine bossDeathCoroutine;
    private Coroutine delayedHealthBarCoroutine;
    private bool healthBarShowAlreadyRequestedThisFight;
    private bool initialMagnusArenaEntryDelayFinished;
    private VaultSmashRechargeManager queuedVaultRechargeManager;
    private bool vaultRechargeAttackRunning;
    private bool normalAttackInProgress;
    private bool queuedVaultRechargeWasQueuedDuringCurrentNormalAttack;
    private bool queuedVaultRechargeIsAttack4TurnTimerVariant;
    private int vaultRechargeChosenAttackSlot = -1;
    private int normalAttacksCompletedBeforeVaultRecharge;
    private int normalAttacksCompletedSinceAttack4Variant;
    private int normalAttacksNeededForNextAttack4Variant;

    private bool isRunning;
    private bool hasPassedCutsceneGate;
    private bool ignoreAnimationEvents;
    private bool damageInterruptActive;
    private bool platformRechargeActive;
    private bool bossDead;
    private bool deathFlowActive;

    private Vector3 attack2CurrentSmashImpactPosition;

    private int sharedMagnusCurrentHp;
    private bool sharedMagnusHpInitialized;
    private int sharedMagnusVisualCurrentHp;
    private Coroutine sharedMagnusDamageQueueCoroutine;
    private readonly Queue<SharedMagnusDamageReport> sharedMagnusDamageQueue = new Queue<SharedMagnusDamageReport>();
    private int lastSharedMagnusDamageAmount;
    private string lastSharedMagnusDamageSourceName = "";

    private const string MagnusBossDeadPlayerPrefsKey = "MagnusBossDead";

    private static bool reviveMagnusFromIntroCutsceneRequested;

    public static bool MagnusBossDeadGlobal { get; private set; }
    public static bool MagnusBossAliveInputGateActive { get; private set; }
    public static bool ShouldBlockKeybindConsoleForLivingMagnus => MagnusBossAliveInputGateActive && !IsMagnusBossSavedDead();
    public static bool ShouldBlockParasiteSceneForLivingMagnus => MagnusBossAliveInputGateActive && !IsMagnusBossSavedDead();

    public static bool IsMagnusBossSavedDead()
    {
        return MagnusBossDeadGlobal || PlayerPrefs.GetInt(MagnusBossDeadPlayerPrefsKey, 0) == 1;
    }

    public static void SetMagnusBossSavedDead(bool isDead)
    {
        MagnusBossDeadGlobal = isDead;

        if (isDead)
            MagnusBossAliveInputGateActive = false;

        PlayerPrefs.SetInt(MagnusBossDeadPlayerPrefsKey, isDead ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void RequestMagnusReviveFromIntroCutscene()
    {
        reviveMagnusFromIntroCutsceneRequested = true;
        MagnusBossAliveInputGateActive = false;
        SetMagnusBossSavedDead(false);

        if (FirebaseGameProgressSync.Instance != null)
            FirebaseGameProgressSync.Instance.SaveNow();
    }

    public int SharedMagnusCurrentHpPublic => sharedMagnusCurrentHp;
    public int SharedMagnusMaxHpPublic => sharedMagnusMaxHp;
    public int LastSharedMagnusDamageAmountPublic => lastSharedMagnusDamageAmount;
    public string LastSharedMagnusDamageSourceNamePublic => lastSharedMagnusDamageSourceName;
    public bool IsBossDeadPublic => bossDead || deathFlowActive || sharedMagnusCurrentHp <= 0 || MagnusBossDeadGlobal;

    private int lastCompletedAttack = 0;
    private int consecutiveSameAttackCount = 0;
    private int completedNormalAttacksForChanceGhost = 0;

    private class SharedMagnusDamageReport
    {
        public Transform attacker;
        public int damage;
        public string sourceName;
    }

    [System.Serializable]
    private class BackgroundGhostSettings
    {
        public GameObject ghostObject;
        public GhostPortalController ghostController;
    }

    private void Awake()
    {
        CacheReferences();
        CacheBackgroundGhostReferences();
        TurnOffBackgroundGhostObjectsAtStartupIfNeeded();

        if (reviveMagnusFromIntroCutsceneRequested)
        {
            reviveMagnusFromIntroCutsceneRequested = false;
            ResetSharedMagnusHpToFull();
        }
        else if (IsMagnusBossSavedDead())
        {
            ApplySavedMagnusDeadState();
        }
        else
        {
            InitializeSharedMagnusHp(resetSharedHpOnAwake || !sharedMagnusHpInitialized);
        }

        TurnOffAllAttackObjects();
        TurnOffAttack1HitboxObject();
        TurnOffAttack2HitboxObjects();
        TurnOffAttack2DamageReceiver();
        StopAttack3Objects();
        TurnOffBackgroundSpecialEffectsOnly();

        if (bossDead || deathFlowActive)
        {
            ApplySavedMagnusDeadVisuals();
        }
        else
        {
            ResetAllAnimatorParameters();
        }

        healthBarShowAlreadyRequestedThisFight = false;
        initialMagnusArenaEntryDelayFinished = false;
        completedNormalAttacksForChanceGhost = 0;
        ResetAttack4TurnTimerVariantCounter(true);
        RefreshMagnusBossAliveInputGate();
    }

    private void OnEnable()
    {
        RefreshMagnusBossAliveInputGate();
        StartAttackLoop();
    }

    private void OnDisable()
    {
        StopAttackLoop();
        MagnusBossAliveInputGateActive = false;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;

        if (kb == null)
        {
            return;
        }

        if (!enableNumber1InstantKill)
        {
            return;
        }

        bool pressedInstantKillKey = WasPressed(kb, instantKillKey);
        bool pressedKeypadInstantKillKey = instantKillAlsoAcceptsKeypad1 && WasPressed(kb, Key.Numpad1);

        if (pressedInstantKillKey || pressedKeypadInstantKillKey)
        {
            TriggerInstantBossDeathFromKeyboard();
        }
    }

    private void TriggerInstantBossDeathFromKeyboard()
    {
        if (bossDead || deathFlowActive)
        {
            return;
        }

        InitializeSharedMagnusHp(false);

        if (sharedMagnusDamageQueueCoroutine != null)
        {
            StopCoroutine(sharedMagnusDamageQueueCoroutine);
            sharedMagnusDamageQueueCoroutine = null;
        }

        sharedMagnusDamageQueue.Clear();

        lastSharedMagnusDamageAmount = sharedMagnusCurrentHp;
        lastSharedMagnusDamageSourceName = "number 1 instant kill";
        sharedMagnusCurrentHp = 0;
        sharedMagnusVisualCurrentHp = 0;

        ForceSyncMagnusHealthBarToSpecificHp(0, revealHealthBarWhenDamageIsReceived);
        BeginBossDeathFlow("number 1 instant kill");
    }

    private void CacheReferences()
    {
        if (bossAnimator == null)
        {
            bossAnimator = GetComponent<Animator>();
        }

        if (attack1BeamAnimator == null && attack1Object != null)
        {
            attack1BeamAnimator = attack1Object.GetComponent<Animator>();
        }

        if (attack1BeamYRoot == null && attack1Object != null)
        {
            attack1BeamYRoot = attack1Object.transform;
        }

        if (attack2ChildAnimator == null && attack2Object != null)
        {
            attack2ChildAnimator = attack2Object.GetComponent<Animator>();
        }

        if (backgroundDuringAttackSpecialEffectsAnimator == null && backgroundDuringAttackSpecialEffectsObject != null)
        {
            backgroundDuringAttackSpecialEffectsAnimator = backgroundDuringAttackSpecialEffectsObject.GetComponent<Animator>();
        }

        if (attack3ExplosionDamageReceiver == null && attack3ExplosionDamageHitboxObject != null)
        {
            attack3ExplosionDamageReceiver = attack3ExplosionDamageHitboxObject.GetComponent<MagnusAttack3ExplosionDamageReceiver>();
        }

        if (magnusDamageReceiver == null)
        {
            magnusDamageReceiver = GetComponentInChildren<MagnusAttack2DamageReciever>(true);
        }

        if (magnusHealthBar == null)
        {
            magnusHealthBar = Object.FindFirstObjectByType<MagnusHealthBarRigController>(FindObjectsInactive.Include);
        }

        if (magnusHealthBar != null)
        {
            magnusHealthBar.RegisterBossAttackController(this);
        }

        if (vaultSmashRechargeManager == null)
        {
            vaultSmashRechargeManager = Object.FindFirstObjectByType<VaultSmashRechargeManager>(FindObjectsInactive.Include);
        }
    }

    private void StartAttackLoop()
    {
        if (bossDead || deathFlowActive)
        {
            return;
        }

        if (isRunning)
        {
            return;
        }

        isRunning = true;
        attackLoopCoroutine = StartCoroutine(AttackLoop());
    }

    private void ApplySavedMagnusDeadState()
    {
        sharedMagnusMaxHp = Mathf.Max(1, sharedMagnusMaxHp);
        sharedMagnusCurrentHp = 0;
        sharedMagnusVisualCurrentHp = 0;
        sharedMagnusHpInitialized = true;
        lastSharedMagnusDamageAmount = 0;
        lastSharedMagnusDamageSourceName = "";

        bossDead = true;
        deathFlowActive = true;
        MagnusBossAliveInputGateActive = false;
        SetMagnusBossSavedDead(true);

        isRunning = false;
        ignoreAnimationEvents = true;
        damageInterruptActive = true;
        platformRechargeActive = false;
        vaultRechargeAttackRunning = false;
        normalAttackInProgress = false;
        queuedVaultRechargeWasQueuedDuringCurrentNormalAttack = false;
        queuedVaultRechargeIsAttack4TurnTimerVariant = false;
        queuedVaultRechargeManager = null;
        vaultRechargeChosenAttackSlot = -1;
        normalAttacksCompletedBeforeVaultRecharge = 0;
        normalAttacksCompletedSinceAttack4Variant = 0;

    }

    private void ApplySavedMagnusDeadVisuals()
    {
        HardCleanupAttacksForDeath();
        SetAttack5SetupBool(false);
        SetBossDead1Bool(false);
        SetBossDead2Bool(true);

        if (bossAnimator != null)
        {
            bossAnimator.Update(0f);
        }

        ForceSyncMagnusHealthBarToSpecificHp(0, false);
        ForceInstantVaultDeadStateForSavedBossDeathReturn();
        RevealBossDeathExitObject();
    }

    private void ForceInstantVaultDeadStateForSavedBossDeathReturn()
    {
        if (!collapseVaultsOnBossDeath)
        {
            return;
        }

        CacheReferences();

        if (vaultSmashRechargeManager != null)
        {
            vaultSmashRechargeManager.ForceInstantDeadForSavedBossDeathReturn();
        }
        else if (debugBossDeath)
        {
            Debug.LogWarning("BossAttackController: saved boss death was applied, but no VaultSmashRechargeManager was assigned/found, so vaults were not instantly forced dead.");
        }
    }

    public void ApplySavedMagnusDead2StateNow()
    {
        if (!IsMagnusBossSavedDead())
        {
            return;
        }

        CacheReferences();
        ApplySavedMagnusDeadState();
        StopAttackLoop();
        ApplySavedMagnusDeadVisuals();
        RefreshMagnusBossAliveInputGate();

        if (debugBossDeath)
        {
            Debug.Log("BossAttackController: applied saved dead2 state immediately after scene return.");
        }
    }

    private void RefreshMagnusBossAliveInputGate()
    {
        MagnusBossAliveInputGateActive =
            isActiveAndEnabled &&
            !bossDead &&
            !deathFlowActive &&
            sharedMagnusCurrentHp > 0 &&
            !IsMagnusBossSavedDead();
    }

    private void StopAttackLoop()
    {
        isRunning = false;

        if (attackLoopCoroutine != null)
        {
            StopCoroutine(attackLoopCoroutine);
            attackLoopCoroutine = null;
        }

        StopActiveNormalAttackCoroutineOnly();

        if (damageInterruptCoroutine != null)
        {
            StopCoroutine(damageInterruptCoroutine);
            damageInterruptCoroutine = null;
        }

        if (platformRechargeCoroutine != null)
        {
            StopCoroutine(platformRechargeCoroutine);
            platformRechargeCoroutine = null;
        }

        SetPlatformRechargeBool(false);
        SetAttack4SetupBool(false);
        if (!bossDead && !deathFlowActive)
        {
            SetAttack5SetupBool(false);
            SetBossDead1Bool(false);
            SetBossDead2Bool(false);
        }
        platformRechargeActive = false;
        vaultRechargeAttackRunning = false;
        normalAttackInProgress = false;
        queuedVaultRechargeWasQueuedDuringCurrentNormalAttack = false;
        queuedVaultRechargeIsAttack4TurnTimerVariant = false;

        StopActiveBackgroundCoroutineOnly();
        StopAttack1HitboxCoroutineOnly();
        StopAttack2HitboxCoroutineOnly();
        StopAttack2DamageReceiverCoroutineOnly();
        StopAttack3Objects();

        if (delayedHealthBarCoroutine != null)
        {
            StopCoroutine(delayedHealthBarCoroutine);
            delayedHealthBarCoroutine = null;
        }

        StopAllCoroutines();

        TurnOffAllAttackObjects();
        TurnOffAttack1HitboxObject();
        TurnOffAttack2HitboxObjects();
        TurnOffAttack2DamageReceiver();
        StopAttack3Objects();
        TurnOffBackgroundSpecialEffectsOnly();
        ResetAllAnimatorParameters();

        ignoreAnimationEvents = false;
        damageInterruptActive = false;
        platformRechargeActive = false;
    }

    private IEnumerator AttackLoop()
    {
        yield return StartCoroutine(WaitForCutsceneGateIfNeeded());

        while (isRunning && !bossDead)
        {
            if (damageInterruptActive || platformRechargeActive || vaultRechargeAttackRunning)
            {
                yield return null;
                continue;
            }

            if (ShouldRunQueuedVaultRechargeNow())
            {
                yield return StartCoroutine(RunQueuedVaultRechargeAttack());

                if (damageInterruptActive || platformRechargeActive || vaultRechargeAttackRunning)
                {
                    yield return null;
                    continue;
                }

                if (useCooldownAfterVaultRechargeAttack)
                {
                    float rechargeCooldown = Random.Range(cooldownAfterAttackMin, cooldownAfterAttackMax);
                    yield return new WaitForSeconds(rechargeCooldown);
                }

                continue;
            }

            int chosenAttack = ChooseNextAttack();

            normalAttackInProgress = true;

            if (chosenAttack == 1)
            {
                activeNormalAttackCoroutine = StartCoroutine(DoAttack1());
            }
            else if (chosenAttack == 2)
            {
                activeNormalAttackCoroutine = StartCoroutine(DoAttack2());
            }
            else
            {
                activeNormalAttackCoroutine = StartCoroutine(DoAttack3());
            }

            yield return activeNormalAttackCoroutine;
            activeNormalAttackCoroutine = null;

            normalAttackInProgress = false;

            if (bossDead)
            {
                yield break;
            }

            if (damageInterruptActive || platformRechargeActive || vaultRechargeAttackRunning)
            {
                yield return null;
                continue;
            }

            RegisterCompletedAttack(chosenAttack);
            CountCompletedNormalAttackForVaultRechargeQueue(chosenAttack);
            CountCompletedNormalAttackForAttack4TurnTimerVariant(chosenAttack);

            float cooldown = Random.Range(cooldownAfterAttackMin, cooldownAfterAttackMax);
            yield return new WaitForSeconds(cooldown);
        }
    }

    private IEnumerator WaitForCutsceneGateIfNeeded()
    {
        if (waitForMagnusIntroCutscene && !hasPassedCutsceneGate)
        {
            if (debugCutsceneGate)
            {
                Debug.Log("BossAttackController waiting for Magnus intro cutscene gate...");
            }

            while (isRunning && !MagnusCameraCutscenePlayer.CanBossAttacksStart)
            {
                yield return null;
            }

            hasPassedCutsceneGate = true;
        }
        else
        {
            hasPassedCutsceneGate = true;
        }

        // HARD RULE:
        // The first attack flow cannot start until this one-time initial entry delay finishes.
        // This uses ONLY the values on BossAttackController in MagnusArena.
        if (!initialMagnusArenaEntryDelayFinished)
        {
            yield return StartCoroutine(HandleInitialMagnusArenaEntryDelays());
            initialMagnusArenaEntryDelayFinished = true;
        }
    }

    private IEnumerator HandleInitialMagnusArenaEntryDelays()
    {
        float healthDelay = Mathf.Max(0f, healthBarShowDelayAfterCutsceneGate);
        float attackDelayMin = Mathf.Max(0f, bossAttackStartDelayMinAfterCutsceneGate);
        float attackDelayMax = Mathf.Max(attackDelayMin, bossAttackStartDelayMaxAfterCutsceneGate);
        float attackDelay = Random.Range(attackDelayMin, attackDelayMax);

        if (debugCutsceneGate)
        {
            Debug.Log(
                "BossAttackController INITIAL MAGNUS ARENA ENTRY DELAY" +
                " | healthBarDelay=" + healthDelay +
                " | bossAttackDelay=" + attackDelay +
                " | min=" + attackDelayMin +
                " | max=" + attackDelayMax +
                " | waitForIntro=" + waitForMagnusIntroCutscene
            );
        }

        if (showHealthBarDuringBossStartDelay)
        {
            StartDelayedHealthBarShow(healthDelay);
        }

        if (attackDelay > 0f)
        {
            yield return new WaitForSeconds(attackDelay);
        }

        if (!showHealthBarDuringBossStartDelay)
        {
            StartDelayedHealthBarShow(healthDelay);
        }

        if (debugCutsceneGate)
        {
            Debug.Log("BossAttackController initial entry delay finished. Attack flow is now allowed to start.");
        }
    }

    private void StartDelayedHealthBarShow(float delay)
    {
        if (!showHealthBarAfterCutsceneGate)
        {
            return;
        }

        if (healthBarShowAlreadyRequestedThisFight)
        {
            return;
        }

        healthBarShowAlreadyRequestedThisFight = true;

        if (delayedHealthBarCoroutine != null)
        {
            StopCoroutine(delayedHealthBarCoroutine);
            delayedHealthBarCoroutine = null;
        }

        delayedHealthBarCoroutine = StartCoroutine(DelayedHealthBarShowRoutine(delay));
    }

    private IEnumerator DelayedHealthBarShowRoutine(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        ShowMagnusHealthBarAfterCutscene();
        delayedHealthBarCoroutine = null;
    }

    private void ShowMagnusHealthBarAfterCutscene()
    {
        if (!showHealthBarAfterCutsceneGate)
        {
            return;
        }

        // No run-state gate here. The health bar lives on the persistent CameraRig,
        // while this controller lives in MagnusArena. If the boss exists and this delay
        // finished, the visual bar should bind and show.
        CacheReferences();
        InitializeSharedMagnusHp(false);
        ForceSyncMagnusHealthBar(true);

        MagnusBossFightRunState.ClearHealthBarRequest();
    }

    private void ForceSyncMagnusHealthBar(bool revealIfHidden)
    {
        CacheReferences();

        if (magnusHealthBar == null)
        {
            if (debugSharedMagnusHp)
            {
                Debug.LogWarning("BossAttackController: no MagnusHealthBarRigController found. Make sure CameraRig with MagnusHealthBarRigController persists into MagnusArena.");
            }

            return;
        }

        if (revealIfHidden && !magnusHealthBar.IsShowing)
        {
            magnusHealthBar.ShowForMagnus(sharedMagnusMaxHp, sharedMagnusCurrentHp);
        }
        else
        {
            magnusHealthBar.UpdateHealth(sharedMagnusCurrentHp, sharedMagnusMaxHp);
        }
    }

    private void ForceSyncMagnusHealthBarToSpecificHp(int hpToDisplay, bool revealIfHidden)
    {
        CacheReferences();

        if (magnusHealthBar == null)
        {
            if (debugSharedMagnusHp)
            {
                Debug.LogWarning("BossAttackController: no MagnusHealthBarRigController found. Make sure CameraRig with MagnusHealthBarRigController persists into MagnusArena.");
            }

            return;
        }

        int safeHp = Mathf.Clamp(hpToDisplay, 0, sharedMagnusMaxHp);

        if (revealIfHidden && !magnusHealthBar.IsShowing)
        {
            magnusHealthBar.ShowForMagnus(sharedMagnusMaxHp, safeHp);
        }
        else
        {
            magnusHealthBar.UpdateHealth(safeHp, sharedMagnusMaxHp);
        }
    }

    private void EnqueueSharedMagnusDamage(Transform attacker, int damage, string sourceName)
    {
        int safeDamage = Mathf.Max(0, damage);

        if (safeDamage <= 0)
        {
            return;
        }

        sharedMagnusDamageQueue.Enqueue(new SharedMagnusDamageReport
        {
            attacker = attacker,
            damage = safeDamage,
            sourceName = string.IsNullOrWhiteSpace(sourceName) ? "Unknown Source" : sourceName
        });

        if (sharedMagnusDamageQueueCoroutine == null)
        {
            sharedMagnusDamageQueueCoroutine = StartCoroutine(ProcessSharedMagnusDamageQueue());
        }
    }

    private IEnumerator ProcessSharedMagnusDamageQueue()
    {
        while (sharedMagnusDamageQueue.Count > 0)
        {
            SharedMagnusDamageReport report = sharedMagnusDamageQueue.Dequeue();
            int remainingDamage = Mathf.Max(0, report.damage);
            bool appliedAnyDamageFromReport = false;

            while (remainingDamage > 0)
            {
                if (!CanReceiveMagnusSharedDamage())
                {
                    remainingDamage = 0;
                    break;
                }

                bool appliedTick = ApplySharedMagnusDamageTick(report.attacker, report.sourceName);

                if (!appliedTick)
                {
                    remainingDamage = 0;
                    break;
                }

                appliedAnyDamageFromReport = true;
                remainingDamage--;

                if (sharedMagnusCurrentHp <= 0)
                {
                    sharedMagnusDamageQueue.Clear();
                    BeginBossDeathFlow("shared HP reached 0 from " + lastSharedMagnusDamageSourceName);
                    sharedMagnusDamageQueueCoroutine = null;
                    yield break;
                }

                if (remainingDamage > 0)
                {
                    float delay = Mathf.Max(0f, sharedDamageTickDelay);

                    if (delay > 0f)
                    {
                        yield return new WaitForSeconds(delay);
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }

            if (appliedAnyDamageFromReport && interruptBossOnNonLethalSharedDamage && sharedMagnusCurrentHp > 0)
            {
                NotifyMagnusDamaged();
            }
        }

        sharedMagnusDamageQueueCoroutine = null;
    }

    private bool ApplySharedMagnusDamageTick(Transform attacker, string sourceName)
    {
        int oldHp = sharedMagnusCurrentHp;
        sharedMagnusCurrentHp = Mathf.Clamp(sharedMagnusCurrentHp - 1, 0, sharedMagnusMaxHp);

        int appliedDamage = oldHp - sharedMagnusCurrentHp;

        if (appliedDamage <= 0)
        {
            return false;
        }

        lastSharedMagnusDamageAmount = appliedDamage;
        lastSharedMagnusDamageSourceName = string.IsNullOrWhiteSpace(sourceName) ? "Unknown Source" : sourceName;
        sharedMagnusVisualCurrentHp = sharedMagnusCurrentHp;

        if (debugSharedMagnusHp)
        {
            Debug.Log(
                "BossAttackController: SHARED MAGNUS DAMAGE TICK" +
                " | source=" + lastSharedMagnusDamageSourceName +
                " | appliedDamage=" + lastSharedMagnusDamageAmount +
                " | HP " + oldHp + " -> " + sharedMagnusCurrentHp +
                " / " + sharedMagnusMaxHp
            );
        }

        ForceSyncMagnusHealthBarToSpecificHp(sharedMagnusVisualCurrentHp, revealHealthBarWhenDamageIsReceived);
        return true;
    }

    private void InitializeSharedMagnusHp(bool forceReset)
    {
        sharedMagnusMaxHp = Mathf.Max(1, sharedMagnusMaxHp);

        if (!sharedMagnusHpInitialized || forceReset)
        {
            sharedMagnusCurrentHp = sharedMagnusMaxHp;
            sharedMagnusVisualCurrentHp = sharedMagnusCurrentHp;
            lastSharedMagnusDamageAmount = 0;
            lastSharedMagnusDamageSourceName = "";
        }
        else
        {
            sharedMagnusCurrentHp = Mathf.Clamp(sharedMagnusCurrentHp, 0, sharedMagnusMaxHp);
        }

        sharedMagnusHpInitialized = true;

        if (magnusHealthBar != null && magnusHealthBar.IsShowing)
        {
            magnusHealthBar.UpdateHealth(sharedMagnusCurrentHp, sharedMagnusMaxHp);
        }
    }

    public void ResetSharedMagnusHpToFull()
    {
        sharedMagnusMaxHp = Mathf.Max(1, sharedMagnusMaxHp);
        sharedMagnusCurrentHp = sharedMagnusMaxHp;
        sharedMagnusVisualCurrentHp = sharedMagnusCurrentHp;
        sharedMagnusHpInitialized = true;
        lastSharedMagnusDamageAmount = 0;
        lastSharedMagnusDamageSourceName = "";

        if (sharedMagnusDamageQueueCoroutine != null)
        {
            StopCoroutine(sharedMagnusDamageQueueCoroutine);
            sharedMagnusDamageQueueCoroutine = null;
        }

        sharedMagnusDamageQueue.Clear();

        bossDead = false;
        deathFlowActive = false;
        SetMagnusBossSavedDead(false);

        healthBarShowAlreadyRequestedThisFight = false;
        initialMagnusArenaEntryDelayFinished = false;

        CacheReferences();
        ResetAllAnimatorParameters();

        if (magnusHealthBar != null)
        {
            magnusHealthBar.ResetToFull(sharedMagnusMaxHp);

            if (magnusHealthBar.IsShowing)
            {
                magnusHealthBar.UpdateHealth(sharedMagnusCurrentHp, sharedMagnusMaxHp);
            }
        }

        if (debugSharedMagnusHp)
        {
            Debug.Log("BossAttackController: shared Magnus HP reset to full. HP = " + sharedMagnusCurrentHp + "/" + sharedMagnusMaxHp);
        }

        RefreshMagnusBossAliveInputGate();
    }

    public bool CanReceiveMagnusSharedDamage()
    {
        InitializeSharedMagnusHp(false);

        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (bossDead || deathFlowActive)
        {
            return false;
        }

        if (sharedMagnusCurrentHp <= 0)
        {
            return false;
        }

        return true;
    }

    public void ReceiveMagnusSharedDamage(Transform attacker, int damage)
    {
        ReceiveMagnusSharedDamage(attacker, damage, "Unknown Source");
    }

    public void ReceiveMagnusSharedDamage(Transform attacker, int damage, string sourceName)
    {
        int finalDamage = Mathf.Max(0, damage);

        if (finalDamage <= 0)
        {
            return;
        }

        CacheReferences();
        InitializeSharedMagnusHp(false);

        if (!CanReceiveMagnusSharedDamage())
        {
            if (debugSharedMagnusHp)
            {
                Debug.Log("BossAttackController: shared Magnus HP rejected damage from " + sourceName + ".");
            }

            return;
        }

        if (debugSharedMagnusHp)
        {
            Debug.Log(
                "BossAttackController: SHARED MAGNUS DAMAGE QUEUED" +
                " | source=" + (string.IsNullOrWhiteSpace(sourceName) ? "Unknown Source" : sourceName) +
                " | requestedDamage=" + finalDamage +
                " | current HP=" + sharedMagnusCurrentHp +
                " / " + sharedMagnusMaxHp
            );
        }

        EnqueueSharedMagnusDamage(attacker, finalDamage, sourceName);
    }

    public void ForceSyncMagnusSharedHealthBar()
    {
        InitializeSharedMagnusHp(false);
        sharedMagnusVisualCurrentHp = sharedMagnusCurrentHp;
        ForceSyncMagnusHealthBar(true);
    }

    public void NotifyMagnusDamaged()
    {
        if (bossDead || deathFlowActive)
        {
            return;
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (damageInterruptCoroutine != null)
        {
            StopCoroutine(damageInterruptCoroutine);
            damageInterruptCoroutine = null;
        }

        damageInterruptCoroutine = StartCoroutine(DamageInterruptRoutine());
    }

    public void TriggerPlatformRecharge()
    {
        TriggerPlatformRecharge(defaultPlatformRechargeDuration);
    }

    public void TriggerPlatformRecharge(float rechargeDuration)
    {
        if (bossDead)
        {
            return;
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (platformRechargeCoroutine != null)
        {
            StopCoroutine(platformRechargeCoroutine);
            platformRechargeCoroutine = null;
        }

        platformRechargeCoroutine = StartCoroutine(PlatformRechargeRoutine(rechargeDuration));
    }

    public void QueueVaultRecharge(VaultSmashRechargeManager rechargeManager, int maxAttackSlotsUntilRecharge)
    {
        if (bossDead)
        {
            return;
        }

        if (!allowVaultSmashRechargeQueue)
        {
            if (debugVaultSmashRechargeQueue)
            {
                Debug.LogWarning("BossAttackController: vault recharge queue request ignored because allowVaultSmashRechargeQueue is false.");
            }

            return;
        }

        if (rechargeManager == null)
        {
            return;
        }

        // Clean-flow rule: once Attack 4 is queued, do not let duplicate dead reports
        // reroll the slot, reset the counter, or delay the already queued recharge.
        if (queuedVaultRechargeManager != null || vaultRechargeAttackRunning)
        {
            if (debugVaultSmashRechargeQueue)
            {
                Debug.Log("BossAttackController: duplicate Attack 4 queue request ignored. Existing queued manager stays in control.");
            }

            return;
        }

        queuedVaultRechargeManager = rechargeManager;
        queuedVaultRechargeIsAttack4TurnTimerVariant = false;

        int maxSlot = Mathf.Max(1, maxAttackSlotsUntilRecharge);
        vaultRechargeChosenAttackSlot = randomizeVaultRechargeAttackSlotWithinWindow
            ? Random.Range(1, maxSlot + 1)
            : maxSlot;

        normalAttacksCompletedBeforeVaultRecharge = 0;
        queuedVaultRechargeWasQueuedDuringCurrentNormalAttack = normalAttackInProgress;

        if (debugVaultSmashRechargeQueue)
        {
            Debug.Log(
                "BossAttackController: Attack 4 queued from VaultSmashRechargeManager. Chosen slot = " +
                vaultRechargeChosenAttackSlot +
                " out of max window " +
                maxSlot +
                ". Queued during current attack = " +
                queuedVaultRechargeWasQueuedDuringCurrentNormalAttack
            );
        }
    }

    private bool ShouldRunQueuedVaultRechargeNow()
    {
        if (!allowVaultSmashRechargeQueue)
        {
            return false;
        }

        if (vaultRechargeAttackRunning)
        {
            return false;
        }

        if (damageInterruptActive || platformRechargeActive)
        {
            return false;
        }

        if (queuedVaultRechargeManager == null)
        {
            return false;
        }

        if (!queuedVaultRechargeManager.HasQueuedRechargeReadyForBoss)
        {
            return false;
        }

        int requiredNormalAttacksBeforeAttack4 = Mathf.Max(0, vaultRechargeChosenAttackSlot - 1);
        return normalAttacksCompletedBeforeVaultRecharge >= requiredNormalAttacksBeforeAttack4;
    }

    private void CountCompletedNormalAttackForVaultRechargeQueue(int attackNumber)
    {
        if (queuedVaultRechargeManager == null)
        {
            return;
        }

        if (!queuedVaultRechargeManager.HasQueuedRechargeReadyForBoss)
        {
            return;
        }

        if (queuedVaultRechargeWasQueuedDuringCurrentNormalAttack)
        {
            queuedVaultRechargeWasQueuedDuringCurrentNormalAttack = false;

            if (debugVaultSmashRechargeQueue)
            {
                Debug.Log("BossAttackController: Attack 4 was queued during this attack, so this current attack is not counted.");
            }

            return;
        }

        normalAttacksCompletedBeforeVaultRecharge++;

        if (debugVaultSmashRechargeQueue)
        {
            Debug.Log(
                "BossAttackController: Attack 4 queue count = " +
                normalAttacksCompletedBeforeVaultRecharge +
                " / " +
                Mathf.Max(0, vaultRechargeChosenAttackSlot - 1) +
                ". Last normal attack = " +
                attackNumber
            );
        }
    }

    private void CountCompletedNormalAttackForAttack4TurnTimerVariant(int attackNumber)
    {
        if (!enableTurnTimerAttack4Variant)
        {
            return;
        }

        if (bossDead || deathFlowActive)
        {
            return;
        }

        if (vaultRechargeAttackRunning || queuedVaultRechargeManager != null)
        {
            return;
        }

        if (vaultSmashRechargeManager == null)
        {
            CacheReferences();
        }

        if (vaultSmashRechargeManager == null)
        {
            if (debugAttack4TurnTimerVariant)
            {
                Debug.LogWarning("BossAttackController: Attack 4 timer variant cannot queue because no VaultSmashRechargeManager was found.");
            }

            return;
        }

        if (vaultSmashRechargeManager.BossDeathCollapsed)
        {
            return;
        }

        if (normalAttacksNeededForNextAttack4Variant <= 0)
        {
            RollNextAttack4TurnTimerVariantRequirement();
        }

        normalAttacksCompletedSinceAttack4Variant++;

        if (debugAttack4TurnTimerVariant)
        {
            Debug.Log(
                "BossAttackController: Attack 4 timer variant count = " +
                normalAttacksCompletedSinceAttack4Variant +
                " / " +
                normalAttacksNeededForNextAttack4Variant +
                ". Last normal attack = " +
                attackNumber
            );
        }

        if (normalAttacksCompletedSinceAttack4Variant < normalAttacksNeededForNextAttack4Variant)
        {
            return;
        }

        QueueAttack4TurnTimerVariant();
    }

    private void QueueAttack4TurnTimerVariant()
    {
        if (!allowVaultSmashRechargeQueue)
        {
            if (debugAttack4TurnTimerVariant)
            {
                Debug.LogWarning("BossAttackController: Attack 4 timer variant reached its count, but vault recharge queueing is disabled.");
            }

            return;
        }

        if (vaultSmashRechargeManager == null)
        {
            CacheReferences();
        }

        if (vaultSmashRechargeManager == null)
        {
            return;
        }

        if (!vaultSmashRechargeManager.QueueAttack4FromBossTurnTimer())
        {
            return;
        }

        QueueVaultRecharge(vaultSmashRechargeManager, 1);

        if (queuedVaultRechargeManager == vaultSmashRechargeManager)
        {
            queuedVaultRechargeIsAttack4TurnTimerVariant = true;

            if (debugAttack4TurnTimerVariant)
            {
                Debug.Log(
                    "BossAttackController: Attack 4 timer variant queued after " +
                    normalAttacksCompletedSinceAttack4Variant +
                    " completed normal attacks. It will run as the next boss attack."
                );
            }
        }
    }

    private void ResetAttack4TurnTimerVariantCounter(bool rollNewRequirement)
    {
        normalAttacksCompletedSinceAttack4Variant = 0;
        queuedVaultRechargeIsAttack4TurnTimerVariant = false;

        if (rollNewRequirement)
        {
            RollNextAttack4TurnTimerVariantRequirement();
        }
    }

    private void RollNextAttack4TurnTimerVariantRequirement()
    {
        int minCount = Mathf.Max(1, minNormalAttacksBeforeAttack4Variant);
        int maxCount = Mathf.Max(minCount, maxNormalAttacksBeforeAttack4Variant);

        normalAttacksNeededForNextAttack4Variant = Random.Range(minCount, maxCount + 1);

        if (debugAttack4TurnTimerVariant)
        {
            Debug.Log("BossAttackController: next Attack 4 timer variant will queue after " + normalAttacksNeededForNextAttack4Variant + " completed normal attacks.");
        }
    }

    private IEnumerator RunQueuedVaultRechargeAttack()
    {
        if (queuedVaultRechargeManager == null)
        {
            yield break;
        }

        if (!queuedVaultRechargeManager.HasQueuedRechargeReadyForBoss)
        {
            yield break;
        }

        VaultSmashRechargeManager managerToRun = queuedVaultRechargeManager;
        bool runTurnTimerVariant = queuedVaultRechargeIsAttack4TurnTimerVariant;

        queuedVaultRechargeManager = null;
        queuedVaultRechargeIsAttack4TurnTimerVariant = false;
        vaultRechargeChosenAttackSlot = -1;
        normalAttacksCompletedBeforeVaultRecharge = 0;
        queuedVaultRechargeWasQueuedDuringCurrentNormalAttack = false;

        vaultRechargeAttackRunning = true;
        platformRechargeActive = true;
        ignoreAnimationEvents = true;
        normalAttackInProgress = false;

        if (debugVaultSmashRechargeQueue)
        {
            Debug.Log(
                "BossAttackController: ATTACK 4 started. " +
                attack4SetupBoolName +
                " true for " +
                attack4SetupDuration +
                "s. Vault restore starts at " +
                attack4PlatformRestoreStartTime +
                "s, interval = " +
                attack4PlatformRestoreInterval +
                "s."
            );
        }

        if (cleanupAttacksWhenPlatformRechargeStarts)
        {
            StopActiveBackgroundCoroutineOnly();
            StopAttack1HitboxCoroutineOnly();
            StopAttack2HitboxCoroutineOnly();
            StopAttack2DamageReceiverCoroutineOnly();
            StopAttack3Objects();

            TurnOffAllAttackObjects();
            TurnOffAttack1HitboxObject();
            TurnOffAttack2HitboxObjects();
            TurnOffAttack2DamageReceiver();
            StopAttack3Objects();
            TurnOffBackgroundSpecialEffectsOnly();
            ResetAllAnimatorParameters();
        }

        float attack4StartTime = Time.time;

        SetAttack4SetupBool(true);

        if (runTurnTimerVariant)
        {
            if (debugAttack4TurnTimerVariant)
            {
                Debug.Log("BossAttackController: Attack 4 timer variant is shaking/killing all assigned vaults before restore.");
            }

            yield return StartCoroutine(
                managerToRun.SmashAllAssignedVaultsForAttack4TurnTimerVariant(attack4VariantVaultSelfKillWaitTime)
            );
        }

        float restoreStartDelay = Mathf.Clamp(
            attack4PlatformRestoreStartTime,
            0f,
            Mathf.Max(0f, attack4SetupDuration)
        );

        float elapsedBeforeRestoreDelay = Time.time - attack4StartTime;
        float remainingRestoreStartDelay = Mathf.Max(0f, restoreStartDelay - elapsedBeforeRestoreDelay);

        if (remainingRestoreStartDelay > 0f)
        {
            yield return new WaitForSeconds(remainingRestoreStartDelay);
        }

        // IMPORTANT:
        // This only starts each vault's independent guaranteed revive sequence.
        // The vaults keep finishing revive on their own even after Attack 4 ends.
        yield return StartCoroutine(
            managerToRun.RestoreVaultsDuringAttack4SetupOneByOne(attack4PlatformRestoreInterval)
        );

        float elapsed = Time.time - attack4StartTime;
        float remainingAttack4Time = Mathf.Max(0f, attack4SetupDuration - elapsed);

        if (remainingAttack4Time > 0f)
        {
            yield return new WaitForSeconds(remainingAttack4Time);
        }

        SetAttack4SetupBool(false);

        ignoreAnimationEvents = false;
        platformRechargeActive = false;
        vaultRechargeAttackRunning = false;
        ResetAttack4TurnTimerVariantCounter(true);

        if (debugVaultSmashRechargeQueue)
        {
            Debug.Log("BossAttackController: ATTACK 4 finished. Vaults may still be finishing their own revive, but boss attacks can resume.");
        }
    }

    private IEnumerator PlatformRechargeRoutine(float rechargeDuration)
    {
        platformRechargeActive = true;
        ignoreAnimationEvents = true;

        if (debugPlatformRecharge)
        {
            Debug.Log("BossAttackController: platform recharge started. Boss attacks paused.");
        }

        if (attackLoopCoroutine != null)
        {
            StopCoroutine(attackLoopCoroutine);
            attackLoopCoroutine = null;
        }

        if (cleanupAttacksWhenPlatformRechargeStarts)
        {
            StopActiveBackgroundCoroutineOnly();
            StopAttack1HitboxCoroutineOnly();
            StopAttack2HitboxCoroutineOnly();
            StopAttack2DamageReceiverCoroutineOnly();
            StopAttack3Objects();

            TurnOffAllAttackObjects();
            TurnOffAttack1HitboxObject();
            TurnOffAttack2HitboxObjects();
            TurnOffAttack2DamageReceiver();
            StopAttack3Objects();
            ResetAllAnimatorParameters();
        }

        SetPlatformRechargeBool(true);

        float safeDuration = rechargeDuration > 0f ? rechargeDuration : defaultPlatformRechargeDuration;

        if (safeDuration > 0f)
        {
            yield return new WaitForSeconds(safeDuration);
        }

        SetPlatformRechargeBool(false);

        ignoreAnimationEvents = false;
        platformRechargeActive = false;

        if (debugPlatformRecharge)
        {
            Debug.Log("BossAttackController: platform recharge ended. Boss attacks resumed.");
        }

        if (isActiveAndEnabled && !bossDead)
        {
            isRunning = false;
            StartAttackLoop();
        }

        platformRechargeCoroutine = null;
    }

    private IEnumerator DamageInterruptRoutine()
    {
        damageInterruptActive = true;
        ignoreAnimationEvents = true;
        normalAttackInProgress = false;

        if (platformRechargeCoroutine != null)
        {
            StopCoroutine(platformRechargeCoroutine);
            platformRechargeCoroutine = null;
        }

        platformRechargeActive = false;
        vaultRechargeAttackRunning = false;
        queuedVaultRechargeIsAttack4TurnTimerVariant = false;
        SetPlatformRechargeBool(false);
        SetAttack4SetupBool(false);

        if (debugDamageInterrupt)
        {
            Debug.Log("BossAttackController: Magnus damaged. HARD interrupting current attack before damaged bool.");
        }

        // IMPORTANT:
        // Stop the parent attack loop AND the currently running attack coroutine.
        // If DoAttack3 keeps running for even one more frame, it can keep attack3Main/portals alive
        // and visually fight the damaged state.
        StopAttackLoopCoroutineOnly();
        StopActiveNormalAttackCoroutineOnly();

        // Kill every active attack object/hitbox first, including Attack 3 portal arrows.
        StopActiveBackgroundCoroutineOnly();
        StopAttack1HitboxCoroutineOnly();
        StopAttack2HitboxCoroutineOnly();
        StopAttack2DamageReceiverCoroutineOnly();
        StopAttack3Objects();

        TurnOffAllAttackObjects();
        TurnOffAttack1HitboxObject();
        TurnOffAttack2HitboxObjects();
        TurnOffAttack2DamageReceiver();
        StopAttack3Objects();

        // Clear attack booleans BEFORE setting damaged. This makes damaged take priority instead
        // of being forced while attack3Main/attack2Smash/etc. are still true.
        ResetAllAnimatorParameters();

        if (bossAnimator != null && !string.IsNullOrWhiteSpace(damagedBoolName))
        {
            SetAnimatorBool(bossAnimator, damagedBoolName, true);
            bossAnimator.Update(0f);
        }

        if (blowUpGhostsWhenMagnusDamaged)
        {
            ForceAllBackgroundGhostsToBlowUp();
        }

        if (damageInterruptPauseBeforeRestart > 0f)
        {
            yield return new WaitForSeconds(damageInterruptPauseBeforeRestart);
        }

        if (bossAnimator != null && !string.IsNullOrWhiteSpace(damagedBoolName))
        {
            SetAnimatorBool(bossAnimator, damagedBoolName, false);
        }

        ignoreAnimationEvents = false;
        damageInterruptActive = false;

        if (isActiveAndEnabled && !bossDead)
        {
            isRunning = false;
            StartAttackLoop();
        }

        damageInterruptCoroutine = null;
    }

    public void NotifyMagnusDeathFromHealthBar()
    {
        BeginBossDeathFlow("health bar reached 0");
    }

    public void NotifyMagnusDeathFromDamageReceiver()
    {
        BeginBossDeathFlow("damage receiver reached 0");
    }

    private void BeginBossDeathFlow(string reason)
    {
        if (bossDead || deathFlowActive)
        {
            return;
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        deathFlowActive = true;
        bossDead = true;
        MagnusBossAliveInputGateActive = false;
        SetMagnusBossSavedDead(true);

        if (FirebaseGameProgressSync.Instance != null)
            FirebaseGameProgressSync.Instance.SaveNow();

        isRunning = false;
        ignoreAnimationEvents = true;
        damageInterruptActive = true;
        platformRechargeActive = false;
        vaultRechargeAttackRunning = false;
        normalAttackInProgress = false;
        queuedVaultRechargeWasQueuedDuringCurrentNormalAttack = false;
        queuedVaultRechargeIsAttack4TurnTimerVariant = false;
        queuedVaultRechargeManager = null;
        vaultRechargeChosenAttackSlot = -1;
        normalAttacksCompletedBeforeVaultRecharge = 0;
        normalAttacksCompletedSinceAttack4Variant = 0;

        if (debugBossDeath)
        {
            Debug.Log("BossAttackController: DEATH FLOW LOCKED from " + reason + ". Stopping every attack before Attack 5 setup.");
        }

        StopAttackLoopCoroutineOnly();
        StopActiveNormalAttackCoroutineOnly();
        StopDamageInterruptCoroutineOnly();
        StopPlatformRechargeCoroutineOnly();
        StopActiveBackgroundCoroutineOnly();
        StopAttack1HitboxCoroutineOnly();
        StopAttack2HitboxCoroutineOnly();
        StopAttack2DamageReceiverCoroutineOnly();
        StopAttack3Objects();

        HardCleanupAttacksForDeath();

        if (bossDeathCoroutine != null)
        {
            StopCoroutine(bossDeathCoroutine);
            bossDeathCoroutine = null;
        }

        bossDeathCoroutine = StartCoroutine(BossDeathAttack5Routine());
    }

    private IEnumerator BossDeathAttack5Routine()
    {
        // BeginBossDeathFlow already locked the boss and stopped all running attack coroutines.
        // Do not call ResetAllAnimatorParameters() here after attack5setup is set, because that
        // is exactly what was letting attack/default states fight the death transition.
        HardCleanupAttacksForDeath();

        if (debugBossDeath)
        {
            Debug.Log("BossAttackController: ATTACK 5 / boss death started. Normal attacks are permanently stopped.");
        }

        // Death flow rule:
        // Keep Magnus in the damaged animator branch and then turn on attack5setup.
        // This prevents the final hit from visually returning to the default/idle state first.
        if (bossAnimator != null && keepDamagedBoolTrueDuringBossDeath && !string.IsNullOrWhiteSpace(damagedBoolName))
        {
            SetAnimatorBool(bossAnimator, damagedBoolName, true);
        }

        if (forceFinalDeathDamagedStateBeforeAttack5 && bossAnimator != null && !string.IsNullOrWhiteSpace(finalDeathDamagedStateName))
        {
            ForceAnimatorStateIfPossible(bossAnimator, finalDeathDamagedStateName);
        }

        if (waitOneFrameBeforeAttack5Setup)
        {
            yield return null;
        }

        if (blowUpGhostsWhenMagnusDamaged)
        {
            ForceAllBackgroundGhostsToBlowUp();
        }

        if (collapseVaultsOnBossDeath)
        {
            CacheReferences();

            if (vaultSmashRechargeManager != null)
            {
                vaultSmashRechargeManager.ForceCollapseAllVaultsForBossDeath();
            }
            else if (debugBossDeath)
            {
                Debug.LogWarning("BossAttackController: boss died, but no VaultSmashRechargeManager was assigned/found, so vaults were not force-collapsed.");
            }
        }

        SetBossDead1Bool(false);
        SetBossDead2Bool(false);
        SetAttack5SetupBool(true);

        if (dead1StartDelayAfterAttack5Setup > 0f)
        {
            yield return new WaitForSeconds(dead1StartDelayAfterAttack5Setup);
        }

        SetBossDead1Bool(true);
        SetAttack5SetupBool(false);

        if (forceDeadAnimationStateAfterAttack5)
        {
            ForceAnimatorStateIfPossible(bossAnimator, bossDeadAnimationStateName);
        }

        if (bossDead1Duration > 0f)
        {
            yield return new WaitForSeconds(bossDead1Duration);
        }

        SetBossDead1Bool(false);
        SetBossDead2Bool(true);
        RevealBossDeathExitObject();

        ignoreAnimationEvents = true;
        damageInterruptActive = true;
        deathFlowActive = true;
        bossDeathCoroutine = null;

        if (debugBossDeath)
        {
            Debug.Log("BossAttackController: ATTACK 5 finished. dead2 is now true, and attacks will not restart.");
        }
    }

    private void HardCleanupAttacksForDeath()
    {
        // This method is intentionally death-specific. It kills attack visuals/hitboxes/booleans
        // but does NOT clear damaged or attack5setup after the death transition begins.
        TurnOffAttack1HitboxObject();
        TurnOffAttack2HitboxObjects();
        TurnOffAttack2DamageReceiver();
        TurnAttack3ExplosionHitboxOff();

        SetObjectActive(attack1Object, false);
        SetObjectActive(attack2Object, false);
        SetObjectActive(attack3Object, false);
        TurnOffBackgroundSpecialEffectsOnly();

        if (attack3Portals != null)
        {
            for (int i = 0; i < attack3Portals.Length; i++)
            {
                if (attack3Portals[i] != null)
                {
                    attack3Portals[i].ForceResetPortal();
                }
            }
        }

        SetAnimatorBool(bossAnimator, attack1IntroBoolName, false);
        SetAnimatorBool(bossAnimator, attack1HoldBoolName, false);
        SetAnimatorBool(bossAnimator, attack2SetupBoolName, false);
        SetAnimatorBool(bossAnimator, attack2IdleBoolName, false);
        SetAnimatorBool(bossAnimator, attack2SmashBoolName, false);
        SetAnimatorBool(bossAnimator, attack3SetupBoolName, false);
        SetAnimatorBool(bossAnimator, attack3MainBoolName, false);
        SetPlatformRechargeBool(false);
        SetAttack4SetupBool(false);

        SetAttack1BeamBool(false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildHolderBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmSmashDownBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmPullUpBoolName, false);
        SetAnimatorBool(
            backgroundDuringAttackSpecialEffectsAnimator,
            backgroundDuringAttackSpecialEffectsBoolName,
            false
        );
    }

    private void StopAttackLoopCoroutineOnly()
    {
        if (attackLoopCoroutine != null)
        {
            StopCoroutine(attackLoopCoroutine);
            attackLoopCoroutine = null;
        }
    }

    private void StopActiveNormalAttackCoroutineOnly()
    {
        if (activeNormalAttackCoroutine != null)
        {
            StopCoroutine(activeNormalAttackCoroutine);
            activeNormalAttackCoroutine = null;
        }

        normalAttackInProgress = false;
    }

    private void StopDamageInterruptCoroutineOnly()
    {
        if (damageInterruptCoroutine != null)
        {
            StopCoroutine(damageInterruptCoroutine);
            damageInterruptCoroutine = null;
        }
    }

    private void StopPlatformRechargeCoroutineOnly()
    {
        if (platformRechargeCoroutine != null)
        {
            StopCoroutine(platformRechargeCoroutine);
            platformRechargeCoroutine = null;
        }

        SetPlatformRechargeBool(false);
        SetAttack4SetupBool(false);
    }

    private int ChooseNextAttack()
    {
        float attack1Weight = Mathf.Max(0f, attack1ChanceWeight);
        float attack2Weight = Mathf.Max(0f, attack2ChanceWeight);
        float attack3Weight = Mathf.Max(0f, attack3ChanceWeight);

        ApplyRepeatChancePenalty(ref attack1Weight, ref attack2Weight, ref attack3Weight);

        if (boostAttack3ChanceWhileGhostAlive && IsAnyBackgroundGhostAlive())
        {
            BoostAttack3ByCuttingFromOtherAttacks(ref attack1Weight, ref attack2Weight, ref attack3Weight);
        }

        float totalWeight = attack1Weight + attack2Weight + attack3Weight;

        if (totalWeight <= 0f)
        {
            return 1;
        }

        float roll = Random.Range(0f, totalWeight);

        if (debugAttackChanceRolls)
        {
            Debug.Log(
                "ATTACK ROLL WEIGHTS => A1: " + attack1Weight +
                " | A2: " + attack2Weight +
                " | A3: " + attack3Weight +
                " | Roll: " + roll +
                " / " + totalWeight
            );
        }

        if (roll < attack1Weight)
        {
            return 1;
        }

        roll -= attack1Weight;

        if (roll < attack2Weight)
        {
            return 2;
        }

        return 3;
    }

    private void ApplyRepeatChancePenalty(ref float attack1Weight, ref float attack2Weight, ref float attack3Weight)
    {
        if (lastCompletedAttack <= 0)
        {
            return;
        }

        if (consecutiveSameAttackCount <= 0)
        {
            return;
        }

        float safeMultiplier = Mathf.Clamp(repeatAttackChanceMultiplier, 0f, 1f);
        float penaltyMultiplier = Mathf.Pow(safeMultiplier, consecutiveSameAttackCount);

        if (lastCompletedAttack == 1)
        {
            ApplySingleAttackPenalty(ref attack1Weight, ref attack2Weight, ref attack3Weight, penaltyMultiplier);
        }
        else if (lastCompletedAttack == 2)
        {
            ApplySingleAttackPenalty(ref attack2Weight, ref attack1Weight, ref attack3Weight, penaltyMultiplier);
        }
        else if (lastCompletedAttack == 3)
        {
            ApplySingleAttackPenalty(ref attack3Weight, ref attack1Weight, ref attack2Weight, penaltyMultiplier);
        }
    }

    private void ApplySingleAttackPenalty(
        ref float repeatedAttackWeight,
        ref float otherAttackAWeight,
        ref float otherAttackBWeight,
        float penaltyMultiplier
    )
    {
        float oldWeight = repeatedAttackWeight;
        float newWeight = oldWeight * penaltyMultiplier;
        float cutAmount = oldWeight - newWeight;

        repeatedAttackWeight = newWeight;

        if (shareCutChanceWithOtherAttacks && cutAmount > 0f)
        {
            otherAttackAWeight += cutAmount * 0.5f;
            otherAttackBWeight += cutAmount * 0.5f;
        }
    }

    private void BoostAttack3ByCuttingFromOtherAttacks(ref float attack1Weight, ref float attack2Weight, ref float attack3Weight)
    {
        float multiplier = Mathf.Max(1f, attack3GhostAliveChanceMultiplier);

        float oldAttack3Weight = attack3Weight;
        float wantedAttack3Weight = attack3Weight * multiplier;
        float wantedIncrease = wantedAttack3Weight - oldAttack3Weight;

        if (wantedIncrease <= 0f)
        {
            return;
        }

        float availableToCut = attack1Weight + attack2Weight;

        if (availableToCut <= 0f)
        {
            return;
        }

        float actualIncrease = Mathf.Min(wantedIncrease, availableToCut);

        float attack1CutPercent = attack1Weight / availableToCut;
        float attack2CutPercent = attack2Weight / availableToCut;

        attack1Weight -= actualIncrease * attack1CutPercent;
        attack2Weight -= actualIncrease * attack2CutPercent;
        attack3Weight += actualIncrease;

        if (attack1Weight < 0f)
        {
            attack1Weight = 0f;
        }

        if (attack2Weight < 0f)
        {
            attack2Weight = 0f;
        }
    }

    private void RegisterCompletedAttack(int attackNumber)
    {
        if (attackNumber <= 0)
        {
            return;
        }

        if (lastCompletedAttack == attackNumber)
        {
            consecutiveSameAttackCount++;
        }
        else
        {
            lastCompletedAttack = attackNumber;
            consecutiveSameAttackCount = 1;
        }

        completedNormalAttacksForChanceGhost++;

        if (debugAttackChanceRolls)
        {
            Debug.Log(
                "Completed attack = " + attackNumber +
                ". Consecutive count = " + consecutiveSameAttackCount +
                ". Completed attacks for chance ghost = " + completedNormalAttacksForChanceGhost
            );
        }
    }


    private IEnumerator DoAttack1()
    {
        if (damageInterruptActive || bossDead || deathFlowActive)
        {
            yield break;
        }

        ResetAllAnimatorParameters();
        StopAttack1HitboxCoroutineOnly();
        TurnOffAttack1HitboxObject();
        SetAttack1BeamBool(false);

        TryStartBackgroundDuringAttack();

        Transform playerTransform = FindPlayerTransform();

        SetObjectActive(attack1Object, true);

        // ULTIMATE ATTACK 1 FLOW:
        // 1) Main Magnus: attack1Intro = true for editable attack1IntroDuration.
        // 2) Main Magnus: attack1Hold = true for editable attack1HoldTotalDuration.
        // 3) Child attack1 object: attackbeam stays false while this object tracks player Y.
        // 4) Tracking locks, attackbeam becomes true for editable attack1BeamTrueDuration.
        // 5) During attackbeam=true, wait editable attack1HitboxDelayAfterBeamStarts, then grandchild hitbox turns on.
        // 6) Hitbox stays on until attackbeam turns false.
        // 7) If attack1HoldTotalDuration is longer than track+beam, hold remains true for the extra time.
        SetAnimatorBool(bossAnimator, attack1IntroBoolName, true);
        SetAnimatorBool(bossAnimator, attack1HoldBoolName, false);
        SetAttack1BeamBool(false);
        TurnOffAttack1HitboxObject();

        float safeIntroDuration = Mathf.Max(0f, attack1IntroDuration);

        if (safeIntroDuration > 0f)
        {
            yield return new WaitForSeconds(safeIntroDuration);
        }

        if (damageInterruptActive || bossDead || deathFlowActive)
        {
            CleanupAttack1Only();
            yield break;
        }

        SetAnimatorBool(bossAnimator, attack1IntroBoolName, false);
        SetAnimatorBool(bossAnimator, attack1HoldBoolName, true);
        SetAttack1BeamBool(false);
        TurnOffAttack1HitboxObject();

        float safeHoldTotalDuration = Mathf.Max(0f, attack1HoldTotalDuration);
        float safeTrackTime = Mathf.Max(0f, attack1HoldTrackPlayerYBeforeBeamTime);
        float safeBeamTrueDuration = Mathf.Max(0f, attack1BeamTrueDuration);
        float safeHitboxDelay = Mathf.Max(0f, attack1HitboxDelayAfterBeamStarts);

        // attack1Hold must stay true for the whole Attack 1 hold flow.
        // If the Inspector hold duration is too short, extend the actual hold duration enough to fit tracking + beam.
        float requiredHoldDuration = safeTrackTime + safeBeamTrueDuration;
        float actualHoldDuration = Mathf.Max(safeHoldTotalDuration, requiredHoldDuration);

        float holdTimer = 0f;

        while (holdTimer < actualHoldDuration)
        {
            if (damageInterruptActive || bossDead || deathFlowActive)
            {
                CleanupAttack1Only();
                yield break;
            }

            bool inTrackingWindow = holdTimer < safeTrackTime;
            bool inBeamWindow = holdTimer >= safeTrackTime && holdTimer < safeTrackTime + safeBeamTrueDuration;
            bool shouldHitboxBeOn = inBeamWindow && holdTimer >= safeTrackTime + safeHitboxDelay;

            if (inTrackingWindow)
            {
                if (playerTransform == null)
                {
                    playerTransform = FindPlayerTransform();
                }

                UpdateAttack1BeamY(playerTransform);
            }

            SetAttack1BeamBool(inBeamWindow);
            SetObjectActive(attack1HitboxObject, shouldHitboxBeOn);

            holdTimer += Time.deltaTime;
            yield return null;
        }

        CleanupAttack1Only();
    }

    private void CleanupAttack1Only()
    {
        TurnOffAttack1HitboxObject();
        SetAttack1BeamBool(false);
        SetAnimatorBool(bossAnimator, attack1IntroBoolName, false);
        SetAnimatorBool(bossAnimator, attack1HoldBoolName, false);
        SetObjectActive(attack1Object, false);
    }

    private void UpdateAttack1BeamY(Transform playerTransform)
    {
        if (!attack1BeamFollowsPlayerY)
        {
            return;
        }

        if (attack1BeamYRoot == null)
        {
            if (attack1Object != null)
            {
                attack1BeamYRoot = attack1Object.transform;
            }
            else
            {
                return;
            }
        }

        if (playerTransform == null)
        {
            return;
        }

        Vector3 currentPosition = attack1BeamYRoot.position;
        float targetY = playerTransform.position.y + attack1BeamPlayerYOffset;

        if (attack1BeamYFollowSpeed >= 999f)
        {
            currentPosition.y = targetY;
        }
        else
        {
            currentPosition.y = Mathf.MoveTowards(
                currentPosition.y,
                targetY,
                attack1BeamYFollowSpeed * Time.deltaTime
            );
        }

        attack1BeamYRoot.position = currentPosition;
    }


    private IEnumerator DoAttack2()
    {
        if (damageInterruptActive || bossDead || deathFlowActive)
        {
            yield break;
        }

        ResetAllAnimatorParameters();

        StopAttack2HitboxCoroutineOnly();
        TurnOffAttack2HitboxObjects();
        StopAttack2DamageReceiverCoroutineOnly();

        TryStartBackgroundDuringAttack();

        Transform playerTransform = FindPlayerTransform();

        if (attack2ChildAnimator == null && attack2Object != null)
        {
            attack2ChildAnimator = attack2Object.GetComponent<Animator>();
        }

        SetObjectActive(attack2Object, false);

        SetAnimatorBool(bossAnimator, attack2SetupBoolName, true);
        SetAnimatorBool(bossAnimator, attack2IdleBoolName, false);
        SetAnimatorBool(bossAnimator, attack2SmashBoolName, false);

        SetAnimatorBool(attack2ChildAnimator, attack2ChildHolderBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmSmashDownBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmPullUpBoolName, false);

        yield return new WaitForSeconds(attack2SetupDuration);

        if (damageInterruptActive)
        {
            yield break;
        }

        SetAnimatorBool(bossAnimator, attack2SetupBoolName, false);

        SetObjectActive(attack2Object, true);
        TurnOffAttack2HitboxObjects();

        if (attack2ChildAnimator == null && attack2Object != null)
        {
            attack2ChildAnimator = attack2Object.GetComponent<Animator>();
        }

        if (playerTransform == null)
        {
            playerTransform = FindPlayerTransform();
        }

        if (playerTransform != null && attack2Object != null && attack2SnapToPlayerOnFollowStart)
        {
            attack2Object.transform.position = playerTransform.position + attack2ShadowFollowOffset;
        }

        PlaceAttack2DamageReceiverAtAttack2ObjectPosition();
        StartAttack2DamageReceiverWindow();

        SetAnimatorBool(bossAnimator, attack2IdleBoolName, true);

        float followDuration = attack2IdleDuration - attack2ShadowLockBeforeSmash;

        if (followDuration < 0f)
        {
            followDuration = 0f;
        }

        float followTimer = 0f;

        while (followTimer < followDuration)
        {
            if (damageInterruptActive || bossDead)
            {
                yield break;
            }

            if (playerTransform == null)
            {
                playerTransform = FindPlayerTransform();
            }

            if (playerTransform != null && attack2Object != null)
            {
                Vector3 targetPosition = playerTransform.position + attack2ShadowFollowOffset;

                attack2Object.transform.position = Vector3.MoveTowards(
                    attack2Object.transform.position,
                    targetPosition,
                    attack2FollowSpeed * Time.deltaTime
                );

                PlaceAttack2DamageReceiverAtAttack2ObjectPosition();
            }

            followTimer += Time.deltaTime;
            yield return null;
        }

        Vector3 lockedShadowPosition = attack2Object != null ? attack2Object.transform.position : Vector3.zero;
        attack2CurrentSmashImpactPosition = lockedShadowPosition + attack2HitboxWorldOffset;
        PlaceAttack2DamageReceiverAtImpactPosition();

        if (attack2ShadowLockBeforeSmash > 0f)
        {
            yield return new WaitForSeconds(attack2ShadowLockBeforeSmash);
        }

        if (damageInterruptActive)
        {
            yield break;
        }

        if (attack2HolderDelayAfterShadowLock > 0f)
        {
            yield return new WaitForSeconds(attack2HolderDelayAfterShadowLock);
        }

        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmSmashDownBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmPullUpBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildHolderBoolName, true);

        ForceAnimatorState(attack2ChildAnimator, attack2ChildHolderStateName);

        if (attack2LiftDelayAfterHolder > 0f)
        {
            yield return new WaitForSeconds(attack2LiftDelayAfterHolder);
        }

        if (damageInterruptActive)
        {
            yield break;
        }

        if (attack2Object != null)
        {
            attack2Object.transform.position = lockedShadowPosition + attack2ObjectLiftOffsetAfterLock;
        }

        SetAnimatorBool(bossAnimator, attack2IdleBoolName, false);
        SetAnimatorBool(bossAnimator, attack2SmashBoolName, true);

        if (attack2DelayAfterLiftBeforeArmSmashDown > 0f)
        {
            yield return new WaitForSeconds(attack2DelayAfterLiftBeforeArmSmashDown);
        }

        if (damageInterruptActive)
        {
            yield break;
        }

        if (attack2ChildSmashDelayAfterMainSmash > 0f)
        {
            yield return new WaitForSeconds(attack2ChildSmashDelayAfterMainSmash);
        }

        SetAnimatorBool(attack2ChildAnimator, attack2ChildHolderBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmPullUpBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmSmashDownBoolName, true);

        ForceAnimatorState(attack2ChildAnimator, attack2ChildArmSmashDownStateName);
        PlaceAttack2DamageReceiverAtImpactPosition();
        StartAttack2DamageReceiverOffAfterArmComesDown();

        yield return new WaitForSeconds(attack2ArmSmashDownDuration);

        if (damageInterruptActive)
        {
            yield break;
        }

        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmSmashDownBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmPullUpBoolName, true);

        if (waitForArmPullUpAnimationToFinish)
        {
            yield return StartCoroutine(WaitForAnimatorStateToFinish(
                attack2ChildAnimator,
                attack2ChildArmPullUpStateName,
                attack2ArmPullUpFallbackDuration
            ));
        }
        else
        {
            yield return new WaitForSeconds(attack2ArmPullUpFallbackDuration);
        }

        StopAttack2HitboxCoroutineOnly();
        TurnOffAttack2HitboxObjects();

        StopAttack2DamageReceiverCoroutineOnly();

        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmSmashDownBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmPullUpBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildHolderBoolName, false);

        SetAnimatorBool(bossAnimator, attack2SmashBoolName, false);

        SetObjectActive(attack2Object, false);
    }

    private IEnumerator DoAttack3()
    {
        if (damageInterruptActive || bossDead || deathFlowActive)
        {
            yield break;
        }

        ResetAllAnimatorParameters();
        StopAttack3Objects();

        SetObjectActive(attack3Object, true);

        SetAnimatorBool(bossAnimator, attack3SetupBoolName, true);
        SetAnimatorBool(bossAnimator, attack3MainBoolName, false);

        if (debugAttack3)
        {
            Debug.Log("ATTACK 3: setup started.");
        }

        if (attack3SetupDuration > 0f)
        {
            yield return new WaitForSeconds(attack3SetupDuration);
        }

        if (damageInterruptActive)
        {
            StopAttack3Objects();
            yield break;
        }

        SetAnimatorBool(bossAnimator, attack3SetupBoolName, false);
        SetAnimatorBool(bossAnimator, attack3MainBoolName, true);

        TurnAttack3ExplosionHitboxOn();
        StartAttack3Portals();

        if (debugAttack3)
        {
            Debug.Log("ATTACK 3: main started.");
        }

        if (attack3MainDuration > 0f)
        {
            yield return new WaitForSeconds(attack3MainDuration);
        }

        TurnAttack3ExplosionHitboxOff();

        SetAnimatorBool(bossAnimator, attack3MainBoolName, false);
        SetObjectActive(attack3Object, false);

        if (debugAttack3)
        {
            Debug.Log("ATTACK 3: main ended.");
        }
    }

    private void TurnAttack3ExplosionHitboxOn()
    {
        if (attack3ExplosionDamageReceiver == null)
        {
            if (attack3ExplosionDamageHitboxObject != null)
            {
                attack3ExplosionDamageReceiver =
                    attack3ExplosionDamageHitboxObject.GetComponent<MagnusAttack3ExplosionDamageReceiver>();
            }

            if (attack3ExplosionDamageReceiver == null)
            {
                attack3ExplosionDamageReceiver =
                    GetComponentInChildren<MagnusAttack3ExplosionDamageReceiver>(true);
            }
        }

        if (attack3ExplosionDamageHitboxObject == null && attack3ExplosionDamageReceiver != null)
        {
            attack3ExplosionDamageHitboxObject = attack3ExplosionDamageReceiver.gameObject;
        }

        if (attack3ExplosionDamageHitboxObject != null)
        {
            attack3ExplosionDamageHitboxObject.SetActive(true);
        }

        if (attack3ExplosionDamageReceiver == null && attack3ExplosionDamageHitboxObject != null)
        {
            attack3ExplosionDamageReceiver =
                attack3ExplosionDamageHitboxObject.GetComponent<MagnusAttack3ExplosionDamageReceiver>();
        }

        if (attack3ExplosionDamageReceiver != null)
        {
            attack3ExplosionDamageReceiver.SetAttack3MainHitboxActive(true);
        }
        else if (debugAttack3)
        {
            Debug.LogWarning("ATTACK 3: BossHitbox2 has no MagnusAttack3ExplosionDamageReceiver script.");
        }

        Physics2D.SyncTransforms();

        if (debugAttack3)
        {
            Debug.Log(
                "ATTACK 3: BossHitbox2 ON. Object = " +
                (attack3ExplosionDamageHitboxObject != null ? attack3ExplosionDamageHitboxObject.name : "NULL") +
                " | Receiver = " +
                (attack3ExplosionDamageReceiver != null ? attack3ExplosionDamageReceiver.name : "NULL")
            );
        }
    }

    private void TurnAttack3ExplosionHitboxOff()
    {
        if (attack3ExplosionDamageReceiver == null)
        {
            if (attack3ExplosionDamageHitboxObject != null)
            {
                attack3ExplosionDamageReceiver =
                    attack3ExplosionDamageHitboxObject.GetComponent<MagnusAttack3ExplosionDamageReceiver>();
            }

            if (attack3ExplosionDamageReceiver == null)
            {
                attack3ExplosionDamageReceiver =
                    GetComponentInChildren<MagnusAttack3ExplosionDamageReceiver>(true);
            }
        }

        if (attack3ExplosionDamageReceiver != null)
        {
            attack3ExplosionDamageReceiver.SetAttack3MainHitboxActive(false);
        }

        if (attack3ExplosionDamageHitboxObject != null)
        {
            attack3ExplosionDamageHitboxObject.SetActive(false);
        }

        Physics2D.SyncTransforms();

        if (debugAttack3)
        {
            Debug.Log("ATTACK 3: BossHitbox2 OFF.");
        }
    }

    private void StartAttack3Portals()
    {
        if (attack3Portals == null || attack3Portals.Length == 0)
        {
            if (debugAttack3)
            {
                Debug.LogWarning("ATTACK 3: no portals assigned.");
            }

            return;
        }

        int validCount = 0;

        for (int i = 0; i < attack3Portals.Length; i++)
        {
            if (attack3Portals[i] != null)
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            if (debugAttack3)
            {
                Debug.LogWarning("ATTACK 3: portal array exists, but every slot is null.");
            }

            return;
        }

        int min = Mathf.Clamp(attack3MinPortalsToFire, 1, validCount);
        int max = Mathf.Clamp(attack3MaxPortalsToFire, min, validCount);
        int fireCount = Random.Range(min, max + 1);

        MagnusAttack3PortalArrow[] shuffled = new MagnusAttack3PortalArrow[attack3Portals.Length];

        for (int i = 0; i < attack3Portals.Length; i++)
        {
            shuffled[i] = attack3Portals[i];
        }

        for (int i = 0; i < shuffled.Length; i++)
        {
            int randomIndex = Random.Range(i, shuffled.Length);

            MagnusAttack3PortalArrow temp = shuffled[i];
            shuffled[i] = shuffled[randomIndex];
            shuffled[randomIndex] = temp;
        }

        int started = 0;

        for (int i = 0; i < shuffled.Length; i++)
        {
            MagnusAttack3PortalArrow portal = shuffled[i];

            if (portal == null)
            {
                continue;
            }

            portal.BeginPortalArrowAttack();

            started++;

            if (started >= fireCount)
            {
                break;
            }
        }

        if (debugAttack3)
        {
            Debug.Log("ATTACK 3: portals started = " + started);
        }
    }

    private void StopAttack3Objects()
    {
        TurnAttack3ExplosionHitboxOff();

        SetObjectActive(attack3Object, false);

        if (attack3Portals == null)
        {
            return;
        }

        for (int i = 0; i < attack3Portals.Length; i++)
        {
            if (attack3Portals[i] != null)
            {
                attack3Portals[i].ForceResetPortal();
            }
        }
    }

    public void AnimationEvent_TurnOnAttack2Hitboxes()
    {
        if (ignoreAnimationEvents || damageInterruptActive || bossDead || deathFlowActive)
        {
            return;
        }

        StartAttack2HitboxPulse();
    }

    private void StopAttack1HitboxCoroutineOnly()
    {
        // Attack 1 no longer uses animation events or a pulse coroutine.
        // This method name stays so the rest of the controller can keep using one cleanup call.
        TurnOffAttack1HitboxObject();
    }

    private void TurnOffAttack1HitboxObject()
    {
        SetObjectActive(attack1HitboxObject, false);
    }

    private void StartAttack2HitboxPulse()
    {
        StopAttack2HitboxCoroutineOnly();
        attack2HitboxCoroutine = StartCoroutine(Attack2HitboxPulse());
    }

    private IEnumerator Attack2HitboxPulse()
    {
        TurnOffAttack2HitboxObjects();

        PlaceAttack2HitboxObjectsAtImpactPosition();

        SetObjectActive(attack2ArmHitboxObject, true);
        SetObjectActive(attack2VaultDetectorObject, true);

        if (debugAttack2Hitboxes)
        {
            Debug.Log("Attack2 animation event turned hitboxes ON.");
        }

        if (attack2HitboxActiveTime > 0f)
        {
            yield return new WaitForSeconds(attack2HitboxActiveTime);
        }

        TurnOffAttack2HitboxObjects();

        attack2HitboxCoroutine = null;
    }

    private void PlaceAttack2HitboxObjectsAtImpactPosition()
    {
        if (!placeHitboxesAtLockedShadowPosition)
        {
            return;
        }

        if (attack2ArmHitboxObject != null)
        {
            attack2ArmHitboxObject.transform.position = attack2CurrentSmashImpactPosition;
        }

        if (attack2VaultDetectorObject != null)
        {
            attack2VaultDetectorObject.transform.position = attack2CurrentSmashImpactPosition;
        }
    }

    private void StopAttack2HitboxCoroutineOnly()
    {
        if (attack2HitboxCoroutine != null)
        {
            StopCoroutine(attack2HitboxCoroutine);
            attack2HitboxCoroutine = null;
        }

        TurnOffAttack2HitboxObjects();
    }

    private void TurnOffAttack2HitboxObjects()
    {
        SetObjectActive(attack2ArmHitboxObject, false);
        SetObjectActive(attack2VaultDetectorObject, false);
    }

    private void StartAttack2DamageReceiverWindow()
    {
        StopAttack2DamageReceiverCoroutineOnly();
        attack2DamageReceiverCoroutine = StartCoroutine(Attack2DamageReceiverWindowStartRoutine());
    }

    private IEnumerator Attack2DamageReceiverWindowStartRoutine()
    {
        TurnOffAttack2DamageReceiver();

        float delay = Mathf.Max(0f, attack2DamageReceiverDelayAfterAttackStart);

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (damageInterruptActive || bossDead || deathFlowActive)
        {
            attack2DamageReceiverCoroutine = null;
            yield break;
        }

        PlaceAttack2DamageReceiverAtImpactPosition();
        SetObjectActive(attack2DamageReceiverObject, true);
        SyncAttack2DamageReceiverCollider(true);

        if (debugAttack2DamageReceiver)
        {
            Debug.Log("Attack2 temporary damage receiver ON. It will stay active until arm-down + " + attack2DamageReceiverTurnOffDelayAfterArmComesDown + " seconds.");
        }

        attack2DamageReceiverCoroutine = null;
    }

    private void StartAttack2DamageReceiverOffAfterArmComesDown()
    {
        if (attack2DamageReceiverCoroutine != null)
        {
            StopCoroutine(attack2DamageReceiverCoroutine);
            attack2DamageReceiverCoroutine = null;
        }

        attack2DamageReceiverCoroutine = StartCoroutine(Attack2DamageReceiverOffAfterArmComesDownRoutine());
    }

    private IEnumerator Attack2DamageReceiverOffAfterArmComesDownRoutine()
    {
        float delay = Mathf.Max(0f, attack2DamageReceiverTurnOffDelayAfterArmComesDown);

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        else
        {
            yield return null;
        }

        TurnOffAttack2DamageReceiver();

        if (debugAttack2DamageReceiver)
        {
            Debug.Log("Attack2 temporary damage receiver OFF after arm-down delay: " + delay);
        }

        attack2DamageReceiverCoroutine = null;
    }

    private void PlaceAttack2DamageReceiverAtAttack2ObjectPosition()
    {
        if (!placeAttack2DamageReceiverAtLockedShadowPosition)
        {
            return;
        }

        if (attack2DamageReceiverObject == null || attack2Object == null)
        {
            return;
        }

        attack2DamageReceiverObject.transform.position = attack2Object.transform.position + attack2DamageReceiverWorldOffset;
        Physics2D.SyncTransforms();
    }

    private void PlaceAttack2DamageReceiverAtImpactPosition()
    {
        if (!placeAttack2DamageReceiverAtLockedShadowPosition)
        {
            return;
        }

        if (attack2DamageReceiverObject == null)
        {
            return;
        }

        attack2DamageReceiverObject.transform.position = attack2CurrentSmashImpactPosition + attack2DamageReceiverWorldOffset;
    }

    private void SyncAttack2DamageReceiverCollider(bool enabledState)
    {
        if (attack2DamageReceiverObject == null)
        {
            return;
        }

        MagnusAttack2DamageReciever receiver = attack2DamageReceiverObject.GetComponent<MagnusAttack2DamageReciever>();

        if (receiver == null)
        {
            receiver = attack2DamageReceiverObject.GetComponentInChildren<MagnusAttack2DamageReciever>(true);
        }

        Collider2D col = null;

        if (receiver != null)
        {
            col = receiver.GetMainColliderForWeaponHit();
        }

        if (col == null)
        {
            col = attack2DamageReceiverObject.GetComponent<Collider2D>();
        }

        if (col == null)
        {
            col = attack2DamageReceiverObject.GetComponentInChildren<Collider2D>(true);
        }

        if (col != null)
        {
            col.isTrigger = true;
            col.enabled = enabledState;
        }

        Physics2D.SyncTransforms();
    }

    private void StopAttack2DamageReceiverCoroutineOnly()
    {
        if (attack2DamageReceiverCoroutine != null)
        {
            StopCoroutine(attack2DamageReceiverCoroutine);
            attack2DamageReceiverCoroutine = null;
        }

        TurnOffAttack2DamageReceiver();
    }

    private void TurnOffAttack2DamageReceiver()
    {
        SyncAttack2DamageReceiverCollider(false);
        SetObjectActive(attack2DamageReceiverObject, false);
    }

    private void TryStartBackgroundDuringAttack()
    {
        if (!allowBackgroundAttackDuringAttack1And2)
        {
            return;
        }

        if (blockNewBackgroundAttackWhileGhostAlive && IsAnyBackgroundGhostAlive())
        {
            return;
        }

        if (activeBackgroundDuringAttackCoroutine != null)
        {
            return;
        }

        float roll = Random.Range(0f, 100f);

        if (roll > backgroundDuringAttackChancePercent)
        {
            return;
        }

        activeBackgroundDuringAttackCoroutine = StartCoroutine(DoBackgroundDuringAttack());
    }

    private IEnumerator DoBackgroundDuringAttack()
    {
        CacheBackgroundGhostReferences();
        Transform playerTransform = FindPlayerTransform();

        SetObjectActive(backgroundDuringAttackSpecialEffectsObject, true);
        SetAnimatorBool(
            backgroundDuringAttackSpecialEffectsAnimator,
            backgroundDuringAttackSpecialEffectsBoolName,
            true
        );

        GhostPortalController baseGhost = null;

        if (alwaysSpawnBaseBackgroundGhost)
        {
            baseGhost = StartBackgroundGhost(guaranteedBackgroundGhost, playerTransform, "BASE / guaranteed background ghost");
        }

        GhostPortalController specialChanceGhost = null;
        float specialChancePercent = GetCurrentSpecialChanceGhostSpawnPercent();
        float specialChanceRoll = Random.Range(0f, 100f);

        bool chanceSlotIsDifferentFromBase =
            chanceBackgroundGhost != null &&
            guaranteedBackgroundGhost != null &&
            chanceBackgroundGhost.ghostController != null &&
            guaranteedBackgroundGhost.ghostController != null &&
            chanceBackgroundGhost.ghostController != guaranteedBackgroundGhost.ghostController;

        if (specialChancePercent > 0f && specialChanceRoll < specialChancePercent && chanceSlotIsDifferentFromBase)
        {
            specialChanceGhost = StartBackgroundGhost(chanceBackgroundGhost, playerTransform, "SPECIAL / chance background ghost");
        }
        else if (debugBackgroundGhosts && specialChancePercent > 0f && !chanceSlotIsDifferentFromBase)
        {
            Debug.LogWarning("SPECIAL / chance background ghost is the same controller as the BASE ghost. Drag a DIFFERENT ghost into Chance Background Ghost.");
        }

        if (debugBackgroundGhosts)
        {
            Debug.Log(
                "Background ghost roll" +
                " | completedNormalAttacks=" + completedNormalAttacksForChanceGhost +
                " | specialChancePercent=" + specialChancePercent +
                " | roll=" + specialChanceRoll +
                " | baseSpawned=" + (baseGhost != null) +
                " | specialSpawned=" + (specialChanceGhost != null)
            );
        }

        float specialEffectsTimer = 0f;
        bool specialEffectsStillActive = true;

        while (true)
        {
            if (specialEffectsStillActive)
            {
                specialEffectsTimer += Time.deltaTime;

                if (specialEffectsTimer >= backgroundDuringAttackSpecialEffectsDuration)
                {
                    specialEffectsStillActive = false;
                    TurnOffBackgroundSpecialEffectsOnly();
                }
            }

            bool baseDone = baseGhost == null || !baseGhost.IsRunning;
            bool specialDone = specialChanceGhost == null || !specialChanceGhost.IsRunning;

            if (!specialEffectsStillActive && baseDone && specialDone)
            {
                break;
            }

            yield return null;
        }

        activeBackgroundDuringAttackCoroutine = null;
    }

    private float GetCurrentSpecialChanceGhostSpawnPercent()
    {
        if (!useScalingChanceGhostAfterCompletedAttacks)
        {
            return Mathf.Clamp(chanceBackgroundGhostSpawnPercent, 0f, 100f);
        }

        int threshold = Mathf.Max(0, completedAttacksBeforeChanceGhostCanSpawn);

        if (completedNormalAttacksForChanceGhost < threshold)
        {
            return 0f;
        }

        int attacksAfterThreshold = completedNormalAttacksForChanceGhost - threshold;

        float chance =
            chanceGhostPercentOnFirstAttackAfterThreshold +
            (attacksAfterThreshold * chanceGhostPercentIncreasePerAttackAfterThreshold);

        return Mathf.Clamp(chance, 0f, chanceGhostMaxPercent);
    }

    private GhostPortalController StartBackgroundGhost(BackgroundGhostSettings settings, Transform playerTransform, string roleName)
    {
        if (settings == null)
        {
            return null;
        }

        CacheSingleBackgroundGhostReference(settings);

        if (settings.ghostController == null)
        {
            if (debugBackgroundGhosts)
            {
                Debug.LogWarning("Background ghost role '" + roleName + "' has no GhostPortalController assigned.");
            }

            return null;
        }

        if (settings.ghostController.IsRunning)
        {
            SetBackgroundGhostObjectActive(settings, true);
            return settings.ghostController;
        }

        if (debugBackgroundGhosts)
        {
            Debug.Log("Starting " + roleName + ": " + settings.ghostController.name);
        }

        SetBackgroundGhostObjectActive(settings, true);
        settings.ghostController.BeginGhostAttack(playerTransform);
        return settings.ghostController;
    }

    private bool IsAnyBackgroundGhostAlive()
    {
        CacheBackgroundGhostReferences();

        bool guaranteedAlive =
            guaranteedBackgroundGhost != null &&
            guaranteedBackgroundGhost.ghostController != null &&
            guaranteedBackgroundGhost.ghostController.IsRunning;

        bool chanceAlive =
            chanceBackgroundGhost != null &&
            chanceBackgroundGhost.ghostController != null &&
            chanceBackgroundGhost.ghostController.IsRunning;

        return guaranteedAlive || chanceAlive;
    }

    private void ForceAllBackgroundGhostsToBlowUp()
    {
        CacheBackgroundGhostReferences();

        ForceGhostToBlowUp(guaranteedBackgroundGhost);
        ForceGhostToBlowUp(chanceBackgroundGhost);
    }

    private void ForceGhostToBlowUp(BackgroundGhostSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        CacheSingleBackgroundGhostReference(settings);

        if (settings.ghostController == null)
        {
            return;
        }

        if (!settings.ghostController.IsRunning)
        {
            return;
        }

        settings.ghostController.ForceBlowUpFromMagnusDamage();
    }

    private void TurnOffBackgroundGhostObjectsAtStartupIfNeeded()
    {
        if (!startBackgroundGhostObjectsInactive)
        {
            return;
        }

        SetBackgroundGhostObjectActive(guaranteedBackgroundGhost, false);
        SetBackgroundGhostObjectActive(chanceBackgroundGhost, false);
    }

    private void SetBackgroundGhostObjectActive(BackgroundGhostSettings settings, bool active)
    {
        if (settings == null)
        {
            return;
        }

        CacheSingleBackgroundGhostReference(settings);

        if (settings.ghostObject == null)
        {
            return;
        }

        SetObjectActive(settings.ghostObject, active);
    }

    private void StopActiveBackgroundCoroutineOnly()
    {
        if (activeBackgroundDuringAttackCoroutine != null)
        {
            StopCoroutine(activeBackgroundDuringAttackCoroutine);
            activeBackgroundDuringAttackCoroutine = null;
        }

        TurnOffBackgroundSpecialEffectsOnly();
    }

    private void CacheBackgroundGhostReferences()
    {
        CacheSingleBackgroundGhostReference(guaranteedBackgroundGhost);
        CacheSingleBackgroundGhostReference(chanceBackgroundGhost);
    }

    private void CacheSingleBackgroundGhostReference(BackgroundGhostSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        if (settings.ghostController == null && settings.ghostObject != null)
        {
            settings.ghostController = settings.ghostObject.GetComponent<GhostPortalController>();
        }

        if (settings.ghostObject == null && settings.ghostController != null)
        {
            settings.ghostObject = settings.ghostController.gameObject;
        }
    }

    private IEnumerator WaitForAnimatorStateToFinish(Animator targetAnimator, string stateName, float fallbackDuration)
    {
        if (targetAnimator == null || string.IsNullOrWhiteSpace(stateName))
        {
            if (fallbackDuration > 0f)
            {
                yield return new WaitForSeconds(fallbackDuration);
            }

            yield break;
        }

        float searchTimer = 0f;
        float maxSearchTime = 0.5f;

        while (searchTimer < maxSearchTime)
        {
            AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName(stateName))
            {
                break;
            }

            searchTimer += Time.deltaTime;
            yield return null;
        }

        if (!targetAnimator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
        {
            if (fallbackDuration > 0f)
            {
                yield return new WaitForSeconds(fallbackDuration);
            }

            yield break;
        }

        while (true)
        {
            AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);

            if (!stateInfo.IsName(stateName))
            {
                break;
            }

            if (stateInfo.normalizedTime >= 1f && !targetAnimator.IsInTransition(0))
            {
                break;
            }

            yield return null;
        }
    }

    private Transform FindPlayerTransform()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject == null)
        {
            return null;
        }

        return playerObject.transform;
    }

    private void SetAttack1BeamBool(bool value)
    {
        SetAnimatorBool(attack1BeamAnimator, attack1BeamBoolName, value);
    }

    private void ForceAnimatorState(Animator targetAnimator, string stateName)
    {
        if (targetAnimator == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        targetAnimator.Play(stateName, 0, 0f);
        targetAnimator.Update(0f);
    }

    private bool AnimatorHasBoolParameter(Animator targetAnimator, string boolName)
    {
        if (targetAnimator == null || string.IsNullOrWhiteSpace(boolName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == boolName)
            {
                return true;
            }
        }

        return false;
    }

    private void SetAnimatorBool(Animator targetAnimator, string boolName, bool value)
    {
        if (targetAnimator == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(boolName))
        {
            return;
        }

        if (!AnimatorHasBoolParameter(targetAnimator, boolName))
        {
            if (debugAttack3 || debugDamageInterrupt)
            {
                Debug.LogWarning(name + ": Animator is missing bool parameter '" + boolName + "' on " + targetAnimator.name + ". Check capitalization exactly.");
            }

            return;
        }

        targetAnimator.SetBool(boolName, value);
    }

    private void SetPlatformRechargeBool(bool value)
    {
        SetAnimatorBool(bossAnimator, platformRechargeBoolName, value);
    }

    private void SetAttack4SetupBool(bool value)
    {
        SetAnimatorBool(bossAnimator, attack4SetupBoolName, value);
    }

    private void SetAttack5SetupBool(bool value)
    {
        SetAnimatorBool(bossAnimator, attack5SetupBoolName, value);
    }

    private void SetBossDead1Bool(bool value)
    {
        SetAnimatorBool(bossAnimator, bossDead1BoolName, value);
    }

    private void SetBossDead2Bool(bool value)
    {
        SetAnimatorBool(bossAnimator, bossDead2BoolName, value);
    }

    private void RevealBossDeathExitObject()
    {
        if (!revealExitObjectOnBossDeath)
        {
            return;
        }

        if (bossDeathExitObject == null)
        {
            bossDeathExitObject = FindSceneObjectWithTagIncludingInactive(bossDeathExitTag);
        }

        if (bossDeathExitObject == null)
        {
            if (debugBossDeath)
            {
                Debug.LogWarning(name + ": boss death reached dead2, but no exit object was found with tag '" + bossDeathExitTag + "'.");
            }

            return;
        }

        bossDeathExitObject.SetActive(true);

        if (debugBossDeath)
        {
            Debug.Log(name + ": boss death exit object turned on: " + bossDeathExitObject.name);
        }
    }

    private GameObject FindSceneObjectWithTagIncludingInactive(string targetTag)
    {
        if (string.IsNullOrWhiteSpace(targetTag))
        {
            return null;
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];

            if (candidate == null)
            {
                continue;
            }

            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
            {
                continue;
            }

            try
            {
                if (candidate.CompareTag(targetTag))
                {
                    return candidate;
                }
            }
            catch (UnityException)
            {
                if (debugBossDeath)
                {
                    Debug.LogWarning(name + ": tag '" + targetTag + "' is not defined in Unity's Tags list.");
                }

                return null;
            }
        }

        return null;
    }

    private bool WasPressed(Keyboard keyboard, Key key)
    {
        if (keyboard == null)
        {
            return false;
        }

        if (key == Key.None)
        {
            return false;
        }

        return keyboard[key] != null && keyboard[key].wasPressedThisFrame;
    }

    private void ForceAnimatorStateIfPossible(Animator targetAnimator, string stateName)
    {
        if (targetAnimator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);

        if (!targetAnimator.HasState(0, stateHash))
        {
            if (debugBossDeath)
            {
                Debug.LogWarning(name + ": Animator state '" + stateName + "' was not found on layer 0. Not forcing it.");
            }

            return;
        }

        targetAnimator.Play(stateHash, 0, 0f);
        targetAnimator.Update(0f);
    }


    private void SetObjectActive(GameObject targetObject, bool active)
    {
        if (targetObject == null)
        {
            return;
        }

        targetObject.SetActive(active);
    }

    private void TurnOffAllAttackObjects()
    {
        SetObjectActive(attack1Object, false);
        SetObjectActive(attack2Object, false);
        SetObjectActive(attack3Object, false);

        TurnOffAttack1HitboxObject();
        TurnOffAttack2HitboxObjects();
        TurnOffAttack2DamageReceiver();
        StopAttack3Objects();
    }

    private void TurnOffBackgroundSpecialEffectsOnly()
    {
        SetAnimatorBool(
            backgroundDuringAttackSpecialEffectsAnimator,
            backgroundDuringAttackSpecialEffectsBoolName,
            false
        );

        SetObjectActive(backgroundDuringAttackSpecialEffectsObject, false);
    }

    private void ResetAllAnimatorParameters()
    {
        SetAnimatorBool(bossAnimator, attack1IntroBoolName, false);
        SetAnimatorBool(bossAnimator, attack1HoldBoolName, false);

        SetAnimatorBool(bossAnimator, attack2SetupBoolName, false);
        SetAnimatorBool(bossAnimator, attack2IdleBoolName, false);
        SetAnimatorBool(bossAnimator, attack2SmashBoolName, false);

        SetAnimatorBool(bossAnimator, attack3SetupBoolName, false);
        SetAnimatorBool(bossAnimator, attack3MainBoolName, false);
        SetAttack4SetupBool(false);

        if (!bossDead && !deathFlowActive)
        {
            SetAttack5SetupBool(false);
            SetBossDead1Bool(false);
            SetBossDead2Bool(false);
        }

        SetAttack1BeamBool(false);

        SetAnimatorBool(attack2ChildAnimator, attack2ChildHolderBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmSmashDownBoolName, false);
        SetAnimatorBool(attack2ChildAnimator, attack2ChildArmPullUpBoolName, false);

        SetAnimatorBool(
            backgroundDuringAttackSpecialEffectsAnimator,
            backgroundDuringAttackSpecialEffectsBoolName,
            false
        );
    }
}