using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GETravelGames.PrizeManager;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    /// <summary>
    /// Standalone tester for the prize-giving system.
    ///
    /// Place this component in the PrizeGivingTester scene alongside a PrizeService
    /// GameObject. Run "Construir UI" from the context menu to build the canvas, then
    /// enter Play Mode — PrizeService will auto-initialize from the CSV files.
    ///
    /// Two panels:
    ///   • Single pull  — pick a category, choose a stage index, optionally save.
    ///                    Plays the full PrizeCelebrationController FX sequence.
    ///   • Bulk sim     — run N pulls with fake player data, optionally save.
    ///
    /// When "save" is off the pull is a dry run: no reservation is made and no CSV is
    /// written. When "save" is on the pull goes through the full production path
    /// (reserve → claim → RecordPlay → export both CSVs).
    /// </summary>
    public sealed class PrizeTesterManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("UI Root (set by Construir UI)")]
        [SerializeField] Canvas canvas;

        [Header("Single pull")]
        [SerializeField] TMP_Dropdown   categoryDropdown;
        [SerializeField] TMP_InputField singleStageInput;
        [SerializeField] Toggle         singleSaveToggle;
        [SerializeField] Button         singlePullButton;
        [SerializeField] TMP_Text       singleResultLabel;

        [Header("Bulk simulation")]
        [SerializeField] TMP_InputField bulkCountInput;
        [SerializeField] TMP_InputField bulkStageInput;
        [SerializeField] Toggle         bulkRandomStageToggle;
        [SerializeField] Toggle         bulkSaveToggle;
        [SerializeField] Button         bulkRunButton;
        [SerializeField] TMP_Text       bulkSummaryLabel;

        [Header("Results")]
        [SerializeField] RectTransform  resultsContent;
        [SerializeField] TMP_Text       statusLabel;

        [Header("Celebration (set by Construir UI)")]
        [SerializeField] PrizeCelebrationController celebration;
        [SerializeField] TMP_Text       celebrationTitleLabel;

        [Header("Navigation")]
        [SerializeField] Button backButton;

        // ── Constants ─────────────────────────────────────────────────────────

        const string ConsolationText = "No gan\u00f3";

        // ── Runtime state ──────────────────────────────────────────────────────

        bool serviceReady;
        List<PrizeTemplate> loadedCategories = new();
        readonly List<TMP_Text> resultRows = new();
        readonly List<bool> resultIsWin = new();
        bool bulkRunning;
        bool singlePullRunning;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            singlePullButton?.onClick.AddListener(OnSinglePull);
            bulkRunButton?.onClick.AddListener(OnBulkRun);
            backButton?.onClick.AddListener(() => SceneManager.LoadScene("PrizeTesterMenu"));

            if (PrizeService.Instance == null || !PrizeService.Instance.IsInitialized)
            {
                SetStatus("ERROR: PrizeService no inicializado.", isError: true);
                if (singlePullButton != null) singlePullButton.interactable = false;
                if (bulkRunButton    != null) bulkRunButton.interactable    = false;
                return;
            }

            serviceReady = true;
            RefreshCategoryDropdown();
            SetStatus("Listo.");
        }

        // ── Category dropdown ──────────────────────────────────────────────────

        void RefreshCategoryDropdown()
        {
            if (!serviceReady || categoryDropdown == null) return;

            loadedCategories = PrizeService.Instance.GetCategories().ToList();
            var instances    = PrizeService.Instance.GetKioskInstances();

            var countByCat = instances
                .GroupBy(i => i.PrizeCategoryId)
                .ToDictionary(g => g.Key, g => g.Count());

            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var t in loadedCategories)
            {
                countByCat.TryGetValue(t.PrizeCategoryId, out int remaining);
                options.Add(new TMP_Dropdown.OptionData(
                    $"[{t.PrizeCategoryId}] {t.PrizeName}  ({remaining} restantes)"));
            }

            categoryDropdown.ClearOptions();
            categoryDropdown.AddOptions(options);
        }

        // ── Single pull ────────────────────────────────────────────────────────

        void OnSinglePull()
        {
            if (!serviceReady || loadedCategories.Count == 0 || singlePullRunning) return;
            StartCoroutine(SinglePullCoroutine());
        }

        IEnumerator SinglePullCoroutine()
        {
            singlePullRunning = true;
            if (singlePullButton != null) singlePullButton.interactable = false;

            int catIndex = categoryDropdown != null ? categoryDropdown.value : 0;
            if (catIndex >= loadedCategories.Count)
            {
                singlePullRunning = false;
                if (singlePullButton != null) singlePullButton.interactable = true;
                yield break;
            }
            var category = loadedCategories[catIndex];

            int stage = 1;
            if (singleStageInput != null &&
                int.TryParse(singleStageInput.text, out int parsedStage))
                stage = Mathf.Max(0, parsedStage);

            bool save = singleSaveToggle != null && singleSaveToggle.isOn;

            var result = PrizeService.Instance.TryPullFromCategory(
                category.PrizeCategoryId, stage, save,
                "Test", "Single", "000-SINGLE", "Test Office");

            // Clear celebration title before playing.
            if (celebrationTitleLabel != null) celebrationTitleLabel.text = "";

            // Play celebration FX — identical flow to PrizeGivingManager.RevealSequence.
            if (celebration != null)
            {
                // No box to click in the tester, so burst from the screen centre.
                if (Camera.main != null)
                {
                    float depth = Mathf.Abs(Camera.main.transform.position.z);
                    var worldCenter = Camera.main.ScreenToWorldPoint(
                        new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, depth));
                    worldCenter.z = 0f;
                    celebration.SetBurstOrigin(worldCenter);
                }

                if (result.IsRealPrize)
                    yield return celebration.PlayCelebration(result, singleResultLabel, celebrationTitleLabel);
                else
                    yield return celebration.PlayFalsePrizeCelebration(
                        singleResultLabel, celebrationTitleLabel, ConsolationText);
            }
            else
            {
                // Fallback if celebration controller wasn't set up.
                if (singleResultLabel != null)
                    singleResultLabel.text = FormatSingleResult(result, category.PrizeName, save);
            }

            // Log result to the scroll view (always, regardless of celebration).
            AddResultRow(FormatSingleResult(result, category.PrizeName, save), result.IsRealPrize);

            if (save) RefreshCategoryDropdown();
            SetStatus(save ? "Simulado y guardado." : "Simulado (dry run).");

            singlePullRunning = false;
            if (singlePullButton != null) singlePullButton.interactable = true;
        }

        static string FormatSingleResult(PrizePullResult r, string categoryName, bool saved)
        {
            string suffix = saved ? " [guardado]" : " [dry]";
            if (r.IsRealPrize)
                return $"GANO: {r.PrizeName}  [{categoryName}]  Level={r.WinningLevel}{suffix}";
            return $"No gano  [{categoryName}]{suffix}";
        }

        // ── Bulk simulation ────────────────────────────────────────────────────

        void OnBulkRun()
        {
            if (!serviceReady || bulkRunning) return;
            StartCoroutine(BulkRunCoroutine());
        }

        IEnumerator BulkRunCoroutine()
        {
            bulkRunning = true;
            if (bulkRunButton != null) bulkRunButton.interactable = false;

            int n = 100;
            if (bulkCountInput != null &&
                int.TryParse(bulkCountInput.text, out int parsedN))
                n = Mathf.Clamp(parsedN, 1, 10000);

            int fixedStage = 1;
            if (bulkStageInput != null &&
                int.TryParse(bulkStageInput.text, out int parsedStage))
                fixedStage = Mathf.Max(0, parsedStage);

            bool randomStage = bulkRandomStageToggle != null && bulkRandomStageToggle.isOn;
            bool saveAll     = bulkSaveToggle        != null && bulkSaveToggle.isOn;

            int catIndex = categoryDropdown != null ? categoryDropdown.value : 0;
            if (catIndex >= loadedCategories.Count)
            {
                SetStatus("Sin categorias cargadas.", isError: true);
                bulkRunning = false;
                if (bulkRunButton != null) bulkRunButton.interactable = true;
                yield break;
            }
            var category = loadedCategories[catIndex];

            ClearResultRows();
            if (bulkSummaryLabel != null) bulkSummaryLabel.text = "-";
            SetStatus($"Corriendo {n} pulls...");
            yield return null;  // let UI update before heavy work

            int wins = 0;

            for (int i = 0; i < n; i++)
            {
                int stage = randomStage ? UnityEngine.Random.Range(0, 7) : fixedStage;

                string firstName = "Test";
                string lastName  = $"Player{i + 1:D3}";
                string phone     = $"000-{i + 1:D4}";
                const string office = "Test Office";

                var result = PrizeService.Instance.TryPullFromCategory(
                    category.PrizeCategoryId, stage, saveAll,
                    firstName, lastName, phone, office);

                if (result.IsRealPrize) wins++;

                string saveTag = saveAll ? "  [guardado]" : "";
                string row = result.IsRealPrize
                    ? $"#{i + 1:D3}  GANO: {result.PrizeName}  Level={result.WinningLevel}  stage={stage}{saveTag}"
                    : $"#{i + 1:D3}  No gano  stage={stage}{saveTag}";
                AddResultRow(row, result.IsRealPrize);

                // Yield every 50 pulls to keep Unity responsive.
                if ((i + 1) % 50 == 0)
                {
                    SetStatus($"Progreso: {i + 1}/{n}...");
                    yield return null;
                }
            }

            UpdateBulkSummary(n, wins, category.PrizeName);
            if (saveAll) RefreshCategoryDropdown();

            SetStatus(saveAll
                ? $"Bulk completado. {n} pulls guardados."
                : $"Bulk completado. {n} pulls (dry run).");

            bulkRunning = false;
            if (bulkRunButton != null) bulkRunButton.interactable = true;
        }

        void UpdateBulkSummary(int total, int wins, string categoryName)
        {
            if (bulkSummaryLabel == null) return;
            int losses   = total - wins;
            float pct    = total > 0 ? 100f * wins / total : 0f;
            bulkSummaryLabel.text =
                $"RESUMEN - {categoryName}\n" +
                $"Total: {total}  |  Ganados: {wins} ({pct:F1}%)  |  No gano: {losses}";
        }

        // ── Results scroll view ────────────────────────────────────────────────

        void ClearResultRows()
        {
            foreach (var row in resultRows)
                if (row != null) Destroy(row.gameObject);
            resultRows.Clear();
            resultIsWin.Clear();
        }

        void AddResultRow(string text, bool isWin)
        {
            if (resultsContent == null) return;

            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(resultsContent, false);

            var rowText       = go.AddComponent<TextMeshProUGUI>();
            rowText.font      = TMP_Settings.defaultFontAsset;
            rowText.fontSize  = 16;
            rowText.color     = isWin
                ? UIBuilderHelper.ColSuccess
                : UIBuilderHelper.ColTextSecondary;
            rowText.enableWordWrapping = false;
            rowText.text = text;

            var le           = go.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;
            le.minHeight       = 22f;

            resultRows.Add(rowText);
            resultIsWin.Add(isWin);

            // Auto-scroll to bottom.
            Canvas.ForceUpdateCanvases();
            var scrollRect = resultsContent.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        void SetStatus(string message, bool isError = false)
        {
            if (statusLabel == null) return;
            statusLabel.text  = message;
            statusLabel.color = isError
                ? UIBuilderHelper.ColError
                : UIBuilderHelper.ColTextMuted;
        }

        // ── Editor UI builder ──────────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Construir UI")]
        void BuildUi()
        {
            var existing = GetComponentInChildren<Canvas>(true);
            if (existing != null)
                UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);

            UnityEditor.Undo.RecordObject(this, "Construir UI PrizeTester");
            UIBuilderHelper.EnsureEventSystem();

            canvas = UIBuilderHelper.MakeCanvas(transform, "TesterCanvas", 100);

            // Full-screen background.
            var bg   = canvas.gameObject.AddComponent<Image>();
            bg.color = UIBuilderHelper.ColBg;

            // ── Back button (top-left corner, anchored overlay) ───────────────
            backButton = UIBuilderHelper.MakeButton(canvas.transform, "BackButton",
                "\u2190 Men\u00fa", UIBuilderHelper.ColBtnSmall,
                UIBuilderHelper.ColTextSecondary, 16, FontStyles.Normal);
            UIBuilderHelper.SetAnchored(backButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0.92f), new Vector2(0.14f, 1f),
                Vector2.zero, Vector2.zero);

            // ── Header ────────────────────────────────────────────────────────
            var header = UIBuilderHelper.MakeText(canvas.transform, "Header",
                26, FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.SetAnchored(header.GetComponent<RectTransform>(),
                new Vector2(0f, 0.92f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);
            header.text      = "PRIZE GIVING TESTER";
            header.alignment = TextAlignmentOptions.Center;

            // ── Status bar ────────────────────────────────────────────────────
            statusLabel = UIBuilderHelper.MakeText(canvas.transform, "StatusLabel",
                14, FontStyles.Italic, UIBuilderHelper.ColTextMuted);
            UIBuilderHelper.SetAnchored(statusLabel.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 0.05f),
                Vector2.zero, Vector2.zero);
            statusLabel.text = "Esperando inicio...";

            // ── Celebration title label (full-width overlay, shown during FX) ─
            celebrationTitleLabel = UIBuilderHelper.MakeText(canvas.transform,
                "CelebrationTitleLabel", 32, FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.SetAnchored(celebrationTitleLabel.GetComponent<RectTransform>(),
                new Vector2(0f, 0.55f), new Vector2(1f, 0.75f),
                Vector2.zero, Vector2.zero);
            celebrationTitleLabel.text      = "";
            celebrationTitleLabel.alignment = TextAlignmentOptions.Center;

            // ── Celebration controller (particles + screen flash) ─────────────
            var celebGo = new GameObject("CelebrationController", typeof(RectTransform));
            celebGo.transform.SetParent(canvas.transform, false);
            UIBuilderHelper.StretchFill(celebGo.GetComponent<RectTransform>());
            celebration = celebGo.AddComponent<PrizeCelebrationController>();
            celebration.BuildChildren(canvas.transform, new Vector2(960, 540));

            // ── Single pull panel (left half) ─────────────────────────────────
            var leftPanel = UIBuilderHelper.MakeView(canvas.transform, "SinglePullPanel",
                UIBuilderHelper.ColPanel);
            UIBuilderHelper.SetAnchored(leftPanel.GetComponent<RectTransform>(),
                new Vector2(0.01f, 0.40f), new Vector2(0.49f, 0.91f),
                Vector2.zero, Vector2.zero);
            UIBuilderHelper.AddVerticalLayout(leftPanel, TextAnchor.UpperLeft, spacing: 8f,
                padding: new RectOffset(16, 16, 12, 12));

            BuildText(leftPanel.transform, "SingleTitle",
                "PULL INDIVIDUAL", 18, FontStyles.Bold, 28f);

            BuildLabel(leftPanel.transform, "CatLabel", "Categoria:", 20f);

            // Category dropdown.
            var dropGo  = new GameObject("CategoryDropdown", typeof(RectTransform));
            dropGo.transform.SetParent(leftPanel.transform, false);
            UIBuilderHelper.AddLayout(dropGo, 36f);
            var dropImg = dropGo.AddComponent<Image>();
            dropImg.color = UIBuilderHelper.ColInput;
            categoryDropdown = dropGo.AddComponent<TMP_Dropdown>();
            WireMinimalDropdown(categoryDropdown, dropGo);

            BuildLabel(leftPanel.transform, "StageLabel", "StageIndex:", 20f);

            singleStageInput = UIBuilderHelper.MakeInputField(
                leftPanel.transform, "SingleStageInput", "1");
            singleStageInput.text        = "1";
            singleStageInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            UIBuilderHelper.AddLayout(singleStageInput.gameObject, 36f);

            // Save toggle row.
            BuildToggleRow(leftPanel.transform, "SaveRow",
                out singleSaveToggle, "Guardar pull (savePull)", 28f);

            singlePullButton = UIBuilderHelper.MakeButton(leftPanel.transform, "PullButton",
                "SIMULAR PULL", UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary, 20);
            UIBuilderHelper.AddLayout(singlePullButton.gameObject, 44f);

            singleResultLabel = UIBuilderHelper.MakeText(leftPanel.transform, "SingleResult",
                15, FontStyles.Normal, UIBuilderHelper.ColTextPrimary,
                TextAlignmentOptions.Left);
            singleResultLabel.text           = "-";
            singleResultLabel.enableWordWrapping = false;
            UIBuilderHelper.AddLayout(singleResultLabel.gameObject, 40f);

            // ── Bulk panel (right half) ───────────────────────────────────────
            var rightPanel = UIBuilderHelper.MakeView(canvas.transform, "BulkPanel",
                UIBuilderHelper.ColPanel);
            UIBuilderHelper.SetAnchored(rightPanel.GetComponent<RectTransform>(),
                new Vector2(0.51f, 0.40f), new Vector2(0.99f, 0.91f),
                Vector2.zero, Vector2.zero);
            UIBuilderHelper.AddVerticalLayout(rightPanel, TextAnchor.UpperLeft, spacing: 8f,
                padding: new RectOffset(16, 16, 12, 12));

            BuildText(rightPanel.transform, "BulkTitle",
                "BULK SIMULATION", 18, FontStyles.Bold, 28f);

            BuildLabel(rightPanel.transform, "CountLabel", "N\u00b0 de pulls:", 20f);

            bulkCountInput = UIBuilderHelper.MakeInputField(
                rightPanel.transform, "BulkCountInput", "100");
            bulkCountInput.text        = "100";
            bulkCountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            UIBuilderHelper.AddLayout(bulkCountInput.gameObject, 36f);

            BuildLabel(rightPanel.transform, "BulkStageLabel", "StageIndex fijo:", 20f);

            bulkStageInput = UIBuilderHelper.MakeInputField(
                rightPanel.transform, "BulkStageInput", "1");
            bulkStageInput.text        = "1";
            bulkStageInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            UIBuilderHelper.AddLayout(bulkStageInput.gameObject, 36f);

            BuildToggleRow(rightPanel.transform, "RandomStageRow",
                out bulkRandomStageToggle, "Aleatorizar stage por pull", 28f);

            BuildToggleRow(rightPanel.transform, "SaveRow",
                out bulkSaveToggle, "Guardar resultados (subtracci\u00f3n + jugadores)", 28f);

            bulkRunButton = UIBuilderHelper.MakeButton(rightPanel.transform, "RunButton",
                "EJECUTAR BULK", UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary, 20);
            UIBuilderHelper.AddLayout(bulkRunButton.gameObject, 44f);

            bulkSummaryLabel = UIBuilderHelper.MakeText(rightPanel.transform, "Summary",
                14, FontStyles.Normal, UIBuilderHelper.ColTextSecondary,
                TextAlignmentOptions.Left);
            bulkSummaryLabel.text            = "-";
            bulkSummaryLabel.enableWordWrapping = true;
            UIBuilderHelper.AddLayout(bulkSummaryLabel.gameObject, 44f);

            // ── Results scroll view (bottom band, full width) ─────────────────
            var scrollGo = new GameObject("ResultsScrollView", typeof(RectTransform));
            scrollGo.transform.SetParent(canvas.transform, false);
            UIBuilderHelper.SetAnchored(scrollGo.GetComponent<RectTransform>(),
                new Vector2(0.01f, 0.06f), new Vector2(0.99f, 0.39f),
                Vector2.zero, Vector2.zero);
            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = UIBuilderHelper.ColInput;
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal    = false;
            scrollRect.vertical      = true;
            scrollRect.movementType  = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollGo.transform, false);
            UIBuilderHelper.StretchFill(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRT       = content.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot     = new Vector2(0f, 1f);
            contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIBuilderHelper.AddVerticalLayout(content, TextAnchor.UpperLeft, spacing: 2f,
                padding: new RectOffset(8, 8, 4, 4));
            scrollRect.content = contentRT;
            resultsContent     = contentRT;

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[PrizeTesterManager] UI construida. Guarda la escena.");
        }

        // ── BuildUi helpers ────────────────────────────────────────────────────

        static TMP_Text BuildText(Transform parent, string name, string text,
            float fontSize, FontStyles style, float height)
        {
            var t = UIBuilderHelper.MakeText(parent, name, fontSize, style,
                UIBuilderHelper.ColTextPrimary, TextAlignmentOptions.Left);
            t.text = text;
            UIBuilderHelper.AddLayout(t.gameObject, height);
            return t;
        }

        static void BuildLabel(Transform parent, string name, string text, float height)
        {
            var t = UIBuilderHelper.MakeText(parent, name, 14, FontStyles.Normal,
                UIBuilderHelper.ColTextSecondary, TextAlignmentOptions.Left);
            t.text = text;
            UIBuilderHelper.AddLayout(t.gameObject, height);
        }

        /// <summary>
        /// Creates a horizontal row with a Toggle on the left and a label on the right.
        /// Returns the row GameObject; the toggle is set via the <paramref name="toggle"/> out param.
        /// </summary>
        static GameObject BuildToggleRow(Transform parent, string name,
            out Toggle toggle, string labelText, float height)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            UIBuilderHelper.AddLayout(row, height);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment       = TextAnchor.MiddleLeft;
            h.spacing              = 8f;
            h.childControlWidth    = false;
            h.childForceExpandWidth = false;

            // Toggle background
            var tGo  = new GameObject("Toggle", typeof(RectTransform));
            tGo.transform.SetParent(row.transform, false);
            var tRT  = tGo.GetComponent<RectTransform>();
            tRT.sizeDelta = new Vector2(24f, 24f);
            var bg = tGo.AddComponent<Image>();
            bg.color = UIBuilderHelper.ColInput;
            toggle   = tGo.AddComponent<Toggle>();
            toggle.isOn         = false;
            toggle.targetGraphic = bg;

            // Checkmark
            var ck   = new GameObject("Checkmark", typeof(RectTransform));
            ck.transform.SetParent(tGo.transform, false);
            UIBuilderHelper.StretchFill(ck.GetComponent<RectTransform>());
            var ckImg  = ck.AddComponent<Image>();
            ckImg.color = UIBuilderHelper.ColBtn;
            toggle.graphic = ckImg;
            ck.SetActive(false);

            // Label
            var lbl = UIBuilderHelper.MakeText(row.transform, "Label",
                14, FontStyles.Normal, UIBuilderHelper.ColTextSecondary,
                TextAlignmentOptions.Left);
            lbl.text = labelText;

            return row;
        }

        /// <summary>
        /// Wires the minimal TMP_Dropdown internals (caption text + item template)
        /// needed for it to function at runtime.
        /// </summary>
        static void WireMinimalDropdown(TMP_Dropdown dropdown, GameObject root)
        {
            // Caption text
            var capGo   = new GameObject("Label", typeof(RectTransform));
            capGo.transform.SetParent(root.transform, false);
            UIBuilderHelper.StretchFill(capGo.GetComponent<RectTransform>());
            var capText = capGo.AddComponent<TextMeshProUGUI>();
            capText.font      = TMP_Settings.defaultFontAsset;
            capText.fontSize  = 16;
            capText.color     = UIBuilderHelper.ColTextPrimary;
            capText.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.captionText = capText;

            // Template (hidden, required by TMP_Dropdown)
            var tmplGo = new GameObject("Template", typeof(RectTransform));
            tmplGo.transform.SetParent(root.transform, false);
            tmplGo.SetActive(false);
            var tmplImg = tmplGo.AddComponent<Image>();
            tmplImg.color = UIBuilderHelper.ColPanel;
            var tmplRT  = tmplGo.GetComponent<RectTransform>();
            tmplRT.anchorMin = new Vector2(0f, 0f);
            tmplRT.anchorMax = new Vector2(1f, 0f);
            tmplRT.pivot     = new Vector2(0.5f, 1f);
            tmplRT.sizeDelta = new Vector2(0f, 150f);
            dropdown.template = tmplRT;

            // Viewport inside template
            var vpGo = new GameObject("Viewport", typeof(RectTransform));
            vpGo.transform.SetParent(tmplGo.transform, false);
            UIBuilderHelper.StretchFill(vpGo.GetComponent<RectTransform>());
            vpGo.AddComponent<RectMask2D>();

            // Content inside viewport
            var cntGo = new GameObject("Content", typeof(RectTransform));
            cntGo.transform.SetParent(vpGo.transform, false);
            var cntRT   = cntGo.GetComponent<RectTransform>();
            cntRT.anchorMin = new Vector2(0f, 1f);
            cntRT.anchorMax = new Vector2(1f, 1f);
            cntRT.pivot     = new Vector2(0.5f, 1f);
            cntRT.sizeDelta = new Vector2(0f, 28f);

            // Item inside content
            var itemGo = new GameObject("Item", typeof(RectTransform));
            itemGo.transform.SetParent(cntGo.transform, false);
            var itemRT    = itemGo.GetComponent<RectTransform>();
            itemRT.sizeDelta  = new Vector2(0f, 28f);
            itemRT.anchorMin  = new Vector2(0f, 0.5f);
            itemRT.anchorMax  = new Vector2(1f, 0.5f);
            var itemToggle = itemGo.AddComponent<Toggle>();

            var itemLblGo = new GameObject("Item Label", typeof(RectTransform));
            itemLblGo.transform.SetParent(itemGo.transform, false);
            UIBuilderHelper.StretchFill(itemLblGo.GetComponent<RectTransform>());
            var itemLbl      = itemLblGo.AddComponent<TextMeshProUGUI>();
            itemLbl.font     = TMP_Settings.defaultFontAsset;
            itemLbl.fontSize = 16;
            itemLbl.color    = UIBuilderHelper.ColTextPrimary;
            itemToggle.graphic  = itemLbl;
            dropdown.itemText   = itemLbl;

            // ScrollRect on template
            var sr            = tmplGo.AddComponent<ScrollRect>();
            sr.content        = cntRT;
            sr.viewport       = vpGo.GetComponent<RectTransform>();
            sr.horizontal     = false;
            sr.movementType   = ScrollRect.MovementType.Clamped;
        }
#endif
    }
}
