using UnityEngine;
using EscapeRoom.Interaction;
using EscapeRoom.UI;

namespace EscapeRoom.Stage2
{
    /// <summary>
    /// Interactive search spot in Stage 2 (Storage Room).
    /// Represents one of the 5 potential search points (Desk Drawer, Filing Cabinet, Shelf, Crate, Work Table).
    /// Exactly one spot contains the Terminal Access Chip chosen randomly by Stage2PuzzleGenerator per run.
    /// Other spots provide descriptive inspection text so the player knows it has been searched.
    /// </summary>
    [DisallowMultipleComponent]
    public class Stage2SearchSpot : MonoBehaviour, IInteractable
    {
        [Header("Spot Configuration")]
        [Tooltip("Index corresponding to Stage2PuzzleGenerator.allSearchSpots (0 to 4).")]
        [SerializeField] private int spotIndex = 0;

        [SerializeField] private string spotName = "Search Spot";
        [SerializeField] private string promptText = "Press E to search";
        [SerializeField] private bool canInteract = true;

        [Header("Visual Feedback")]
        [Tooltip("Optional transform to slightly open (e.g. drawer pull or crate lid) on search.")]
        [SerializeField] private Transform animatedPart;
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 0f, 0.35f);

        private bool isSearched = false;
        private bool hasItem = false;
        private static bool itemCollected = false;

        public static bool HasCollectedTerminalChip => 
            itemCollected || (Stage2PuzzleGenerator.Instance != null && Stage2PuzzleGenerator.Instance.IsChipCollected);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetChipState()
        {
            itemCollected = false;
        }

        public static void SetChipCollected(bool val)
        {
            itemCollected = val;
        }

        public string InteractionPrompt => PromptText;
        public string PromptText => isSearched 
            ? (hasItem ? $"{spotName} (Item Taken)" : $"{spotName} (Empty) - Press E to inspect") 
            : $"Press E to search {spotName}";

        public bool CanInteract => canInteract && !HasCollectedTerminalChip;
        public int SpotIndex => spotIndex;

        private void Awake()
        {
            CheckAndApplyToolboxVisuals();
        }

        private void Start()
        {
            var gen = Stage2PuzzleGenerator.Instance;
            if (gen != null)
            {
                hasItem = (gen.ValidSearchSpotIndex == spotIndex);
            }
            itemCollected = false;
            CheckAndApplyToolboxVisuals();
        }

        private void CheckAndApplyToolboxVisuals()
        {
            if ((spotName != null && spotName.ToLower().Contains("tool case")) || 
                (gameObject.name != null && gameObject.name.ToLower().Contains("toolcase")))
            {
                var rend = GetComponent<Renderer>();
                if (rend != null)
                {
                    Color redColor = new Color(0.85f, 0.12f, 0.10f, 1f);
                    Material mat = rend.material;
                    if (mat != null)
                    {
                        mat.color = redColor;
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", redColor);
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", redColor);
                        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
                        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.25f);
                    }
                }
            }
        }


        public void ConfigureSpot(int index, string name)
        {
            spotIndex = index;
            spotName = name;
        }

        public void ConfigureSpot(string name, int index)
        {
            spotIndex = index;
            spotName = name;
        }

        public void Interact()
        {
            var gen = Stage2PuzzleGenerator.Instance;
            if (isSearched && !hasItem)
            {
                string targetHint = (gen != null && gen.ActiveSearchSpot != null)
                    ? $" (Shift Log points to: {gen.ActiveSearchSpot.spotName})"
                    : "";
                FeedbackHUD.ShowMessage($"{spotName} is empty!{targetHint}", new Color(1.0f, 0.82f, 0.35f));
                return;
            }

            isSearched = true;
            Stage2Audio.Instance?.PlayButtonPress();

            // Animate opening if applicable
            if (animatedPart != null)
            {
                animatedPart.localPosition += openOffset;
            }

            if (gen != null && gen.ValidSearchSpotIndex == spotIndex && !HasCollectedTerminalChip)
            {
                itemCollected = true;
                hasItem = true;
                gen.RegisterChipFound();
                Stage2Audio.Instance?.PlayItemPickup();
                FeedbackHUD.ShowMessage("FOUND: [Terminal Security Chip]! Insert into the Pattern Terminal on the work table.", new Color(0.35f, 0.95f, 0.40f));
                EscapeRoom.Core.ObjectiveManager.Instance?.CompleteFlag("ITEM_FOUND");
                Debug.Log($"[Stage2SearchSpot] Player found Terminal Security Chip at '{spotName}'.");
            }
            else
            {
                hasItem = false;
                string targetHint = (gen != null && gen.ActiveSearchSpot != null)
                    ? $" (Shift Log target: {gen.ActiveSearchSpot.spotName})"
                    : "";
                FeedbackHUD.ShowMessage($"{spotName} is empty! Nothing useful here.{targetHint}", new Color(1.0f, 0.82f, 0.35f));
                Debug.Log($"[Stage2SearchSpot] Player searched empty spot: '{spotName}'. Active spot is '{gen?.ActiveSearchSpot?.spotName}'.");
            }
        }
    }
}
