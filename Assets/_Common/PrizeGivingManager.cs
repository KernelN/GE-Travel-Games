using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    /// <summary>
    /// Manages the PrizeGiving scene (after the game).
    ///
    /// Flow:
    ///   1. Reads PlayerSessionData.StageIndex → determines box count (2*stage+1, max 13).
    ///   2. Calls PrizeService.TryPullPrize(stageIndex) to pre-roll the best prize.
    ///   3. Spawns boxes in a pleasing geometric layout.
    ///   4. Player clicks one box → unchosen boxes fade out → celebration animation plays.
    ///   5. If real prize: auto-claim → success screen with Play Again button.
    ///   6. If false prize: consolation message + auto-return timer.
    /// </summary>
    public sealed class PrizeGivingManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Shared UI (created by BuildUi or assigned manually)")]
        [SerializeField] Canvas canvas;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] RectTransform boxContainer;
        [SerializeField] PrizeCelebrationController celebration;

        [Header("Post-reveal views (shown after the box click)")]
        [SerializeField] GameObject consolationView;
        [SerializeField] GameObject successView;

        [Header("Consolation")]
        [SerializeField] TMP_Text consolationLabel;
        [SerializeField] Button playAgainConsolation;

        [Header("Success")]
        [SerializeField] TMP_Text successDescriptionLabel;
        [SerializeField] TMP_Text successConfirmLabel;
        [SerializeField] Button playAgainSuccess;

        [Header("Box layout")]
        [Tooltip("Optional prefab with a PrizeBoxController component. " +
                 "If left empty the box UI is built procedurally at runtime.")]
        [SerializeField] PrizeBoxController boxPrefab;
        [SerializeField] float boxSpreadPixels = 180f;

        [Header("Timer")]
        [Tooltip("Default auto-return delay (seconds) for consolation and any level not covered by levelReturnDelays.")]
        [SerializeField] float returnDelay = 5f;
        [Tooltip("Per-level auto-return delay (seconds), indexed by PrizeLevel (0 = common, 1 = uncommon, 2 = rare, 3 = epic, \u2026). " +
                 "Set -1 to require the player to press Play Again instead of auto-exiting. " +
                 "Falls back to returnDelay when the prize level is out of range.")]
        [SerializeField] float[] levelReturnDelays;

        [Header("Textos")]
        [SerializeField] string promptText        = "\u00a1Prob\u00e1 tu suerte y gan\u00e1 un premio!";
        [SerializeField] string consolationText   = "Siga Participando";
        [SerializeField] string successConfirmText = "Retiralo en el stand";
        [SerializeField] string playAgainText     = "JUGAR DE NUEVO";

        // ── Runtime state ──────────────────────────────────────────────────────

        PrizePullResult currentPull;
        readonly List<PrizeBoxController> boxes = new();
        bool boxClickHandled;
        float returnTimer  = -1f;
        float returnStartTime;

        // ── Box layout — predefined normalised positions (×boxSpreadPixels at runtime) ──

        static readonly Vector2[][] BoxLayouts = {
            /* 1  */ new[] { new Vector2( 0,  0) },
            /* 3  */ new[] { new Vector2(-1,  0), new Vector2( 0,  0), new Vector2( 1,  0) },
            /* 5  */ new[] { // cross / +
                new Vector2( 0,  1),
                new Vector2(-1,  0), new Vector2( 0,  0), new Vector2( 1,  0),
                new Vector2( 0, -1) },
            /* 7  */ new[] { // 2-3-2 honeycomb — no empty gaps, fully H+V symmetric
                new Vector2(-0.5f,  1f), new Vector2( 0.5f,  1f),
                new Vector2(-1f,    0f), new Vector2( 0f,    0f), new Vector2( 1f,    0f),
                new Vector2(-0.5f, -1f), new Vector2( 0.5f, -1f) },
            /* 9  */ new[] { // 3×3 grid
                new Vector2(-1,  1), new Vector2( 0,  1), new Vector2( 1,  1),
                new Vector2(-1,  0), new Vector2( 0,  0), new Vector2( 1,  0),
                new Vector2(-1, -1), new Vector2( 0, -1), new Vector2( 1, -1) },
            /* 11 */ new[] { // diamond  1+3+3+3+1
                new Vector2( 0,  2),
                new Vector2(-1,  1), new Vector2( 0,  1), new Vector2( 1,  1),
                new Vector2(-1,  0), new Vector2( 0,  0), new Vector2( 1,  0),
                new Vector2(-1, -1), new Vector2( 0, -1), new Vector2( 1, -1),
                new Vector2( 0, -2) },
            /* 13 */ new[] { // diamond  1+3+5+3+1
                new Vector2( 0,  2),
                new Vector2(-1,  1), new Vector2( 0,  1), new Vector2( 1,  1),
                new Vector2(-2,  0), new Vector2(-1,  0), new Vector2( 0,  0), new Vector2( 1,  0), new Vector2( 2,  0),
                new Vector2(-1, -1), new Vector2( 0, -1), new Vector2( 1, -1),
                new Vector2( 0, -2) },
        };

        // Map box count → layout array index (only odd counts are produced by the formula)
        static readonly Dictionary<int, int> LayoutIndexByCount = new()
        {
            { 1,  0 }, { 3,  1 }, { 5,  2 }, { 7,  3 },
            { 9,  4 }, { 11, 5 }, { 13, 6 },
        };

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            Time.timeScale = 1f;

            // Ensure the camera renders the dark background so world-space particles
            // are visible through the transparent (no background Image) canvas.
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = new Color(0.11f, 0.14f, 0.19f);
            }

            playAgainConsolation?.onClick.AddListener(PlayAgain);
            playAgainSuccess?.onClick.AddListener(PlayAgain);

            HideAllViews();

            if (!PlayerSessionData.HasData)
            {
                Debug.LogWarning("[PrizeGiving] No hay datos de jugador. Volviendo al inicio.");
                SceneManager.LoadScene("ReadyKiosk");
                return;
            }

            if (PrizeService.Instance == null)
            {
                Debug.LogWarning("[PrizeGiving] PrizeService no encontrado.");
                ShowConsolation(null);
                return;
            }

            // If the celebration controller wasn't wired up in the Inspector (or via BuildUi),
            // create it procedurally now so runtime always has one.
            if (celebration == null)
            {
                var celebGo = new GameObject("CelebrationController", typeof(RectTransform));
                celebGo.transform.SetParent(canvas != null ? canvas.transform : transform, false);
                var celebRect = celebGo.GetComponent<RectTransform>();
                celebRect.anchorMin = Vector2.zero;
                celebRect.anchorMax = Vector2.one;
                celebRect.offsetMin = celebRect.offsetMax = Vector2.zero;
                celebration = celebGo.AddComponent<PrizeCelebrationController>();
                celebration.BuildChildren(celebGo.transform, new Vector2(960, 540));
            }

            int stageIndex = PlayerSessionData.StageIndex;
            currentPull = PrizeService.Instance.TryPullPrize(stageIndex);

            // Title shows prompt until a box is chosen.
            if (titleLabel != null) titleLabel.text = promptText;

            int boxCount = Mathf.Min(2 * stageIndex + 1, 13);
            bool locked  = stageIndex == 0;
            SpawnBoxes(boxCount, locked);

            if (locked)
                StartCoroutine(HandleLockedBox());
        }

        void Update()
        {
            if (returnTimer < 0f) return;
            if (Time.unscaledTime - returnStartTime >= returnTimer)
            {
                returnTimer = -1f;
                SceneManager.LoadScene("ReadyKiosk");
            }
        }

        // ── Box spawning ──────────────────────────────────────────────────────

        void SpawnBoxes(int count, bool locked)
        {
            if (boxContainer == null) return;

            if (!LayoutIndexByCount.TryGetValue(count, out int layoutIdx))
            {
                int best = 1;
                foreach (var k in LayoutIndexByCount.Keys)
                    if (k <= count && k > best) best = k;
                layoutIdx = LayoutIndexByCount[best];
            }

            var positions = BoxLayouts[layoutIdx];
            boxes.Clear();

            // ── Compute constrained spread and box size ─────────────────────
            float maxExtentX = 0f, maxExtentY = 0f;
            foreach (var p in positions)
            {
                maxExtentX = Mathf.Max(maxExtentX, Mathf.Abs(p.x));
                maxExtentY = Mathf.Max(maxExtentY, Mathf.Abs(p.y));
            }

            // rect.size is valid once the canvas has done at least one layout pass;
            // fall back to reference-resolution-derived values if not yet computed.
            Vector2 containerSize = boxContainer.rect.size;
            if (containerSize.sqrMagnitude < 1f)
                containerSize = new Vector2(864f, 410f); // 90%×76% of 960×540

            const float minBoxSize = 44f;
            const float maxBoxSize = 140f;
            const float gap        = 10f;

            float halfBox    = maxBoxSize * 0.5f;
            float maxSpreadX = maxExtentX > 0f ? (containerSize.x * 0.5f - halfBox) / maxExtentX : boxSpreadPixels;
            float maxSpreadY = maxExtentY > 0f ? (containerSize.y * 0.5f - halfBox) / maxExtentY : boxSpreadPixels;
            float actualSpread = Mathf.Min(boxSpreadPixels, maxSpreadX, maxSpreadY);

            float actualBoxSize = Mathf.Clamp(actualSpread - gap, minBoxSize, maxBoxSize);

            // Re-verify with the real half-size so boxes never clip the container edge.
            float halfActual = actualBoxSize * 0.5f;
            if (maxExtentX > 0f) actualSpread = Mathf.Min(actualSpread, (containerSize.x * 0.5f - halfActual) / maxExtentX);
            if (maxExtentY > 0f) actualSpread = Mathf.Min(actualSpread, (containerSize.y * 0.5f - halfActual) / maxExtentY);
            // ────────────────────────────────────────────────────────────────

            foreach (var norm in positions)
            {
                PrizeBoxController controller;

                if (boxPrefab != null)
                {
                    controller = Instantiate(boxPrefab, boxContainer);
                }
                else
                {
                    var boxGo = new GameObject("PrizeBox", typeof(RectTransform), typeof(CanvasGroup));
                    boxGo.transform.SetParent(boxContainer, false);
                    controller = boxGo.AddComponent<PrizeBoxController>();
                    controller.BuildChildren(actualBoxSize);
                }

                var rt = controller.GetComponent<RectTransform>();
                rt.anchoredPosition = norm * actualSpread;
                rt.sizeDelta        = Vector2.one * actualBoxSize;

                controller.Initialize(locked);
                controller.OnBoxClicked += HandleBoxClicked;
                boxes.Add(controller);
            }
        }

        // ── Click handling ────────────────────────────────────────────────────

        void HandleBoxClicked(PrizeBoxController clickedBox)
        {
            if (boxClickHandled) return;
            boxClickHandled = true;

            // Remove all click listeners immediately.
            foreach (var b in boxes) b.OnBoxClicked -= HandleBoxClicked;

            StartCoroutine(RevealSequence(clickedBox));
        }

        IEnumerator RevealSequence(PrizeBoxController clickedBox)
        {
            // Fade out all non-clicked boxes concurrently.
            foreach (var b in boxes)
                if (b != clickedBox)
                    StartCoroutine(b.FadeOut(0.3f));

            // Small pause so fades begin.
            yield return new WaitForSeconds(0.1f);

            // Open the box (flips colour/sprite; keeps label hidden until celebration finishes).
            StartCoroutine(clickedBox.Reveal(currentPull.IsRealPrize
                ? currentPull.PrizeName
                : consolationText));

            yield return new WaitForSeconds(0.15f); // brief overlap so box flips open first

            if (currentPull.IsRealPrize)
            {
                if (celebration != null)
                {
                    celebration.SetBurstOrigin(BoxWorldPosition(clickedBox));
                    yield return celebration.PlayCelebration(currentPull, null, titleLabel);
                }
                else if (titleLabel != null)
                    titleLabel.text = currentPull.PrizeName;

                AutoClaim();
                ShowSuccess();
            }
            else
            {
                if (celebration != null)
                    yield return celebration.PlayFalsePrizeCelebration(null, titleLabel, consolationText);
                else if (titleLabel != null)
                    titleLabel.text = consolationText;

                ShowConsolation(currentPull);
            }
        }

        // ── Locked-box flow (stage 0 → forced false prize) ────────────────────

        IEnumerator HandleLockedBox()
        {
            yield return new WaitForSeconds(1.2f);

            var box = boxes.Count > 0 ? boxes[0] : null;
            TMP_Text boxLabel = box?.GetComponentInChildren<TMP_Text>(true);

            if (celebration != null)
                yield return celebration.PlayFalsePrizeCelebration(boxLabel, titleLabel, consolationText);
            else if (boxLabel != null)
            {
                boxLabel.text = consolationText;
                boxLabel.gameObject.SetActive(true);
                if (titleLabel != null) titleLabel.text = consolationText;
            }

            ShowConsolation(currentPull);
        }

        // ── View switching ─────────────────────────────────────────────────────

        void HideAllViews()
        {
            consolationView?.SetActive(false);
            successView?.SetActive(false);
            playAgainConsolation?.gameObject.SetActive(false);
            playAgainSuccess?.gameObject.SetActive(false);
        }

        void ShowConsolation(PrizePullResult pull)
        {
            PrizeService.Instance?.RecordPlay(
                PlayerSessionData.FirstName,
                PlayerSessionData.LastName,
                PlayerSessionData.Phone,
                PlayerSessionData.Office,
                pull);

            PlayerSessionData.Clear();

            consolationView?.SetActive(true);
            if (consolationLabel != null) consolationLabel.text = consolationText;

            playAgainConsolation?.gameObject.SetActive(true);
            StartReturnTimer(returnDelay);
        }

        void ShowSuccess()
        {
            successView?.SetActive(true);

            if (successDescriptionLabel != null)
            {
                var desc = currentPull.PrizeDescription;
                successDescriptionLabel.text = desc;
                successDescriptionLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(desc));
            }

            if (successConfirmLabel != null)
                successConfirmLabel.text = successConfirmText;

            playAgainSuccess?.gameObject.SetActive(true);
            float delay = GetReturnDelayForLevel(currentPull.WinningLevel);
            if (delay >= 0f)
                StartReturnTimer(delay);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts a Screen Space – Overlay box position to the world-space point
        /// on the z=0 plane, used to anchor burst particles to the selected box.
        /// </summary>
        static Vector3 BoxWorldPosition(PrizeBoxController box)
        {
            // For Screen Space Overlay, transform.position is already in screen pixels.
            Vector3 screen = box.transform.position;
            if (Camera.main == null) return Vector3.zero;
            float depth = Mathf.Abs(Camera.main.transform.position.z);
            Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            world.z = 0f;
            return world;
        }

        // ── Claim logic ────────────────────────────────────────────────────────

        void AutoClaim()
        {
            if (PrizeService.Instance == null) return;

            var fullName = $"{PlayerSessionData.FirstName} {PlayerSessionData.LastName}".Trim();
            bool ok = PrizeService.Instance.ClaimPrize(fullName, PlayerSessionData.Phone, PlayerSessionData.Office);
            if (!ok) Debug.LogWarning("[PrizeGiving] ClaimPrize fall\u00f3; se registra el juego de todas formas.");

            PrizeService.Instance.RecordPlay(
                PlayerSessionData.FirstName,
                PlayerSessionData.LastName,
                PlayerSessionData.Phone,
                PlayerSessionData.Office,
                currentPull);

            PlayerSessionData.Clear();
        }

        // ── Timer ──────────────────────────────────────────────────────────────

        float GetReturnDelayForLevel(ushort level) =>
            levelReturnDelays != null && level < levelReturnDelays.Length
                ? levelReturnDelays[level]
                : returnDelay;

        void StartReturnTimer(float delay)
        {
            returnTimer = delay;
            returnStartTime = Time.unscaledTime;
        }

        static void PlayAgain() => SceneManager.LoadScene("RegisterUser");

        // ── Editor UI builder ──────────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Construir UI")]
        void BuildUi()
        {
            var existing = GetComponentInChildren<Canvas>(true);
            if (existing != null)
                UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);

            UnityEditor.Undo.RecordObject(this, "Construir UI PrizeGiving");
            UIBuilderHelper.EnsureEventSystem();

            canvas = UIBuilderHelper.MakeCanvas(transform, "PrizeGivingCanvas", 100);
            // No full-screen background Image here — the camera clear colour provides the
            // dark background, keeping the canvas transparent where no UI element sits.
            // This lets world-space particle effects render through the canvas.
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = UIBuilderHelper.ColBg;
            }

            // ── Title label (top of screen, shows prompt → prize name) ─────
            titleLabel = UIBuilderHelper.MakeText(canvas.transform, "TitleLabel",
                34, FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.SetAnchored(titleLabel.GetComponent<RectTransform>(),
                new Vector2(0f, 0.82f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);
            titleLabel.alignment = TextAlignmentOptions.Center;
            titleLabel.text = promptText;

            // ── Box container (centred, slightly above midpoint) ───────────
            var boxContainerGo = new GameObject("BoxContainer", typeof(RectTransform));
            boxContainerGo.transform.SetParent(canvas.transform, false);
            boxContainer = boxContainerGo.GetComponent<RectTransform>();
            // Wide container: 90% width × 76% height gives boxes plenty of room
            // for all layouts up to 13 boxes while remaining within the canvas.
            UIBuilderHelper.SetAnchored(boxContainer,
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.88f),
                Vector2.zero, Vector2.zero);

            // ── Celebration controller (with particle child + flash overlay) ──
            var celebGo = new GameObject("CelebrationController", typeof(RectTransform));
            celebGo.transform.SetParent(canvas.transform, false);
            var celebRect = celebGo.GetComponent<RectTransform>();
            celebRect.anchorMin = Vector2.zero;
            celebRect.anchorMax = Vector2.one;
            celebRect.offsetMin = celebRect.offsetMax = Vector2.zero;
            celebration = celebGo.AddComponent<PrizeCelebrationController>();
            celebration.BuildChildren(canvas.transform, new Vector2(960, 540));

            // ── Consolation view ───────────────────────────────────────────
            consolationView = UIBuilderHelper.MakeView(canvas.transform, "ConsolationView");
            UIBuilderHelper.AddVerticalLayout(consolationView, spacing: 20f);

            consolationLabel = UIBuilderHelper.MakeText(consolationView.transform,
                "ConsolationLabel", 40, FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.AddLayout(consolationLabel.gameObject, 60);

            playAgainConsolation = UIBuilderHelper.MakeButton(consolationView.transform,
                "PlayAgainButton", playAgainText, UIBuilderHelper.ColBtn,
                UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.AddLayout(playAgainConsolation.gameObject, 52);

            // ── Success view ───────────────────────────────────────────────
            successView = UIBuilderHelper.MakeView(canvas.transform, "SuccessView");
            UIBuilderHelper.AddVerticalLayout(successView, spacing: 12f);

            successDescriptionLabel = UIBuilderHelper.MakeText(successView.transform,
                "SuccessDescription", 22, FontStyles.Normal, UIBuilderHelper.ColTextSecondary);
            UIBuilderHelper.AddLayout(successDescriptionLabel.gameObject, 36);
            successDescriptionLabel.gameObject.SetActive(false);

            successConfirmLabel = UIBuilderHelper.MakeText(successView.transform, "SuccessConfirm",
                20, FontStyles.Italic, UIBuilderHelper.ColTextMuted);
            UIBuilderHelper.AddLayout(successConfirmLabel.gameObject, 32);

            playAgainSuccess = UIBuilderHelper.MakeButton(successView.transform,
                "PlayAgainButton", playAgainText, UIBuilderHelper.ColBtn,
                UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.AddLayout(playAgainSuccess.gameObject, 52);

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[PrizeGivingManager] UI construida. Guard\u00e1 la escena.");
        }

        /// <summary>
        /// Creates a PrizeBox prefab asset in Assets/_Common/Prefabs/ and assigns it to
        /// <see cref="boxPrefab"/> so future scene builds use it instead of the procedural fallback.
        /// Run this once, then use the prefab to customise sprites/colours in the Inspector.
        /// </summary>
        [ContextMenu("Crear Prefab de Caja")]
        void CreateBoxPrefab()
        {
            const string folder = "Assets/_Common/Prefabs";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_Common", "Prefabs");

            var go = new GameObject("PrizeBox", typeof(RectTransform), typeof(CanvasGroup));
            var controller = go.AddComponent<PrizeBoxController>();
            controller.BuildChildren();

            var prefabPath = $"{folder}/PrizeBox.prefab";
            var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            DestroyImmediate(go);

            if (prefab != null)
            {
                boxPrefab = prefab.GetComponent<PrizeBoxController>();
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"[PrizeGivingManager] Prefab guardado en {prefabPath}. " +
                          "Personalizá sprites y colores en el Inspector.");
            }
        }
#endif
    }
}
