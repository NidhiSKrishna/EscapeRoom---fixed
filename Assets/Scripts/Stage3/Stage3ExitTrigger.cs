using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using EscapeRoom.Core;
using EscapeRoom.UI;
using EscapeRoom.Player;

namespace EscapeRoom.Stage3
{
    [RequireComponent(typeof(Collider))]
    public class Stage3ExitTrigger : MonoBehaviour
    {
        [Header("Exit Configuration")]
        [Tooltip("The name of the next scene to load. Leave empty to just show completion.")]
        [SerializeField] private string nextSceneName = "FinalControlRoom";

        [Tooltip("Exit must be explicitly unlocked by SecurityRoomManager.")]
        [SerializeField] private bool requireAllPuzzlesSolved = true;

        public bool HasTriggered { get; private set; }

        // IMPORTANT:
        // Exit starts LOCKED.
        private bool exitUnlocked = false;

        public event System.Action OnStageComplete;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();

            if (col != null)
            {
                col.isTrigger = true;
            }

            exitUnlocked = false;
            HasTriggered = false;
        }

        private void Reset()
        {
            Collider col = GetComponent<Collider>();

            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        /// <summary>
        /// Called by SecurityRoomManager ONLY after the final Stage 3 puzzle
        /// has been successfully completed.
        /// </summary>
        public void UnlockExit()
        {
            exitUnlocked = true;

            Debug.Log(
                "<color=#00ff88><b>[Stage3ExitTrigger]</b></color> " +
                "EXIT UNLOCKED - all Stage 3 objectives completed."
            );

            FeedbackHUD.ShowMessage(
                "All security systems cleared! Exit unlocked.",
                new Color(0.40f, 0.95f, 0.45f)
            );
        }

        private void OnTriggerEnter(Collider other)
        {
            if (HasTriggered)
                return;

            bool isPlayer =
                other.CompareTag("Player") ||
                other.GetComponent<CharacterController>() != null ||
                other.GetComponentInParent<PlayerController>() != null;

            if (!isPlayer)
                return;

            // =========================================================
            // IMPORTANT SAFETY CHECK
            // =========================================================

            if (requireAllPuzzlesSolved && !exitUnlocked)
            {
                Debug.Log(
                    "<color=#ffcc00><b>[Stage3ExitTrigger]</b></color> " +
                    "Exit touched, but Stage 3 is NOT complete yet. Exit remains locked."
                );

                FeedbackHUD.ShowMessage(
                    "EXIT LOCKED",
                    new Color(1f, 0.75f, 0.20f)
                );

                return;
            }

            // =========================================================
            // STAGE 3 REALLY IS COMPLETE
            // =========================================================

            HasTriggered = true;

            Debug.Log(
                "<color=#5cb85c><b>[Stage3ExitTrigger]</b></color> " +
                "Stage 3 exit triggered AFTER all objectives were completed."
            );

            OnStageComplete?.Invoke();

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.SetFinalEscaped("STAGE 3 COMPLETE — FACILITY ESCAPED!");
            }

            if (BannerUI.Instance != null)
            {
                BannerUI.Instance.ShowBanner(
                    "STAGE 3 COMPLETE — FACILITY ESCAPED!",
                    "Security Control Room cleared."
                );
            }

            FeedbackHUD.ShowMessage(
                "STAGE 3 COMPLETE — FACILITY ESCAPED!",
                new Color(0.40f, 0.95f, 0.45f)
            );

            // Unlock cursor and show complete UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var look = FindAnyObjectByType<PlayerLook>();
            if (look != null)
            {
                look.SetCursorLock(false);
                look.enabled = false;
            }

            var controller = FindAnyObjectByType<PlayerController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            StartCoroutine(LoadNextSceneRoutine());
        }

        public void ProceedToStage4()
        {
            Time.timeScale = 1f;
            GameProgress.SetStageUnlocked(4);

            string target = !string.IsNullOrEmpty(nextSceneName) ? nextSceneName : GameProgress.GetStageSceneName(4);
            if (Application.CanStreamedLevelBeLoaded(target))
            {
                SceneManager.LoadScene(target);
            }
            else if (Application.CanStreamedLevelBeLoaded("FinalControlRoom"))
            {
                SceneManager.LoadScene("FinalControlRoom");
            }
            else if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                SceneManager.LoadScene("MainMenu");
            }
        }

        private IEnumerator LoadNextSceneRoutine()
        {
            GameProgress.SetStageUnlocked(4);
            yield return new WaitForSeconds(3.5f);
            ProceedToStage4();
        }

        private void OnGUI()
        {
            if (!HasTriggered) return;

            GUI.depth = -100;

            float winW = Mathf.Min(560f, Screen.width * 0.90f);
            float winH = Mathf.Min(360f, Screen.height * 0.90f);
            float winX = (Screen.width - winW) * 0.5f;
            float winY = (Screen.height - winH) * 0.5f;

            Color oldColor = GUI.color;

            // Chassis
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
            GUI.Box(new Rect(winX, winY, winW, winH), GUIContent.none);

            // Cyan Border
            GUI.color = new Color(0.20f, 0.80f, 1.0f, 0.90f);
            GUI.Box(new Rect(winX + 4, winY + 4, winW - 8, winH - 8), GUIContent.none);

            // Banner
            GUI.color = new Color(0.08f, 0.18f, 0.28f, 1f);
            GUI.Box(new Rect(winX + 6, winY + 6, winW - 12, 54f), GUIContent.none);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 10, winY + 12, winW - 20, 26f), "✔ STAGE 3: SECURITY ROOM CLEARED", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.70f, 0.90f, 1.0f) }
            };
            GUI.Label(new Rect(winX + 10, winY + 38, winW - 20, 20f), "ALL LOCKS DISARMED // PROCEEDING TO FINAL CONTROL ROOM", subStyle);

            // Action Button
            float btnW = 260f;
            float btnH = 46f;
            float btnX = winX + (winW - btnW) * 0.5f;
            float btnY = winY + winH - 70f;
            Rect btnRect = new Rect(btnX, btnY, btnW, btnH);

            var actionBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.black }
            };

            GUI.backgroundColor = new Color(0.25f, 0.88f, 0.45f);
            if (GUI.Button(btnRect, "CONTINUE TO STAGE 4 ►", actionBtnStyle))
            {
                ProceedToStage4();
            }

            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0 && btnRect.Contains(e.mousePosition))
            {
                ProceedToStage4();
                e.Use();
            }

            GUI.backgroundColor = Color.white;
            GUI.color = oldColor;
        }

        private void OnDrawGizmos()
        {
            Collider col = GetComponent<Collider>();

            if (col != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
                Gizmos.matrix = transform.localToWorldMatrix;

                if (col is BoxCollider box)
                {
                    Gizmos.DrawWireCube(
                        box.center,
                        box.size
                    );
                }
                else
                {
                    Gizmos.DrawWireCube(
                        col.bounds.center - transform.position,
                        col.bounds.size
                    );
                }
            }
        }
    }
}