using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stage 4 - TEMPORARY stand-in player, only for testing Stage 4 on its own.
/// WASD move, Shift run, mouse look, E interacts with Stage4Interactable objects.
/// Uses the NEW Input System (Project Settings > Player > Active Input Handling).
///
/// DO NOT ship this. The spec says to use the project's existing player.
/// When the real player exists: delete this player object from the Stage 4 scene and use theirs.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class Stage4TestPlayer : MonoBehaviour
{
    /// <summary>Set to true while a UI panel (keypad, terminal) is open. Frees the mouse, stops movement.</summary>
    public static bool InputLocked;

    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float sprintMultiplier = 1.6f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private bool showCrosshairAndPrompt = true;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;
    private Stage4Interactable current;
    private GUIStyle promptStyle;

    // =====================================================================
    // UNITY
    // =====================================================================

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null) playerCamera = Camera.main;
        InputLocked = false;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        InputLocked = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        InputLocked = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        UpdateCursor();

        if (InputLocked)
        {
            current = null;
            return;
        }

        Look();
        Move();
        UpdateInteraction();
    }

    // =====================================================================
    // CURSOR
    // =====================================================================

    private void UpdateCursor()
    {
        bool isPaused = EscapeRoom.Core.PauseManager.Instance != null && EscapeRoom.Core.PauseManager.Instance.IsPaused;
        bool isPauseUIOpen = EscapeRoom.UI.PauseUI.Instance != null && EscapeRoom.UI.PauseUI.Instance.IsOpen;

        if (InputLocked || isPaused || isPauseUIOpen)
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Mouse mouse = Mouse.current;
        if (Cursor.lockState != CursorLockMode.Locked && mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // =====================================================================
    // LOOK / MOVE
    // =====================================================================

    private void Look()
    {
        if (EscapeRoom.Core.PauseManager.Instance != null && EscapeRoom.Core.PauseManager.Instance.IsPaused) return;
        Mouse mouse = Mouse.current;
        if (mouse == null || playerCamera == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(0f, delta.x, 0f);

        pitch = Mathf.Clamp(pitch - delta.y, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        if (controller == null) return;
        if (EscapeRoom.Core.PauseManager.Instance != null && EscapeRoom.Core.PauseManager.Instance.IsPaused) return;

        Keyboard keyboard = Keyboard.current;
        float x = 0f;
        float z = 0f;

        if (keyboard != null)
        {
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) z += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) z -= 1f;
        }

        try
        {
            if (Mathf.Approximately(x, 0f) && Mathf.Approximately(z, 0f))
            {
                x = Input.GetAxisRaw("Horizontal");
                z = Input.GetAxisRaw("Vertical");
            }
        }
        catch {}

        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f) move.Normalize();

        bool isSprinting = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        float speed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);

        // Ground detection: combine controller.isGrounded with a sphere check at base
        bool isGrounded = controller.isGrounded;
        Vector3 feetPos = transform.position + Vector3.down * (controller.height * 0.5f - controller.radius);
        if (Physics.CheckSphere(feetPos, controller.radius + 0.15f, ~0, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
        }

        if (isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity = Mathf.Max(-25f, verticalVelocity + gravity * Time.deltaTime);
        }

        // Apply horizontal movement independently so downward gravity cannot cancel horizontal velocity
        Vector3 horizontalMovement = move * speed * Time.deltaTime;
        controller.Move(horizontalMovement);

        // Apply vertical displacement separately
        Vector3 verticalMovement = new Vector3(0f, verticalVelocity * Time.deltaTime, 0f);
        controller.Move(verticalMovement);
    }

    // =====================================================================
    // INTERACTION (key E)
    // =====================================================================

    private void UpdateInteraction()
    {
        current = null;
        if (EscapeRoom.Core.PauseManager.Instance != null && EscapeRoom.Core.PauseManager.Instance.IsPaused) return;
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, interactDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            Stage4Interactable target = hit.collider.GetComponentInParent<Stage4Interactable>();
            if (target != null && target.CanInteract) current = target;
        }

        Keyboard keyboard = Keyboard.current;
        if (current != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            current.Interact();
        }
    }

    // =====================================================================
    // SIMPLE ON-SCREEN CROSSHAIR AND PROMPT (temporary; the real UI comes later)
    // =====================================================================

    private void OnGUI()
    {
        if (!showCrosshairAndPrompt || InputLocked) return;
        if (EscapeRoom.UI.PauseUI.Instance != null && EscapeRoom.UI.PauseUI.Instance.IsOpen) return;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        GUI.Box(new Rect(cx - 2f, cy - 2f, 4f, 4f), GUIContent.none);

        if (current == null) return;

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.box);
            promptStyle.fontSize = 16;
            promptStyle.alignment = TextAnchor.MiddleCenter;
            promptStyle.normal.textColor = Color.white;
        }

        GUI.Box(new Rect(cx - 190f, cy + 40f, 380f, 34f), current.PromptText, promptStyle);
    }
}
