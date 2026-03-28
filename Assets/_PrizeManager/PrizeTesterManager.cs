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
    /// Config + log hub for prize-system testing.
    ///
    /// Two views toggled by the header button:
    ///
    ///   Single Pull — select a category from live-count buttons, set stage + save flag,
    ///                 click "Simular Pull". The full PrizeGivingManager scene (boxes,
    ///                 reveal, celebration FX) runs on top; this canvas hides until done.
    ///
    ///   Bulk Sim    — run N pulls with fake player data; optional save; result scroll log.
    ///
    /// Scene setup:
    ///   1. Run "Construir UI" on this component.
    ///   2. Add a PrizeGivingManager component to a separate GameObject in the same scene.
    ///      Do NOT run its "Construir UI" — StartTesterPull builds its canvas at runtime.
    ///   3. Wire the PrizeGivingManager reference in the Inspector.
    ///   4. Make sure PrizeService is also in the scene.
    /// </summary>
    public sealed class PrizeTesterManager : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Prize giving delegate (single pull)")]
        [SerializeField] PrizeGivingManager prizeGivingManager;

        [Header("UI Root (set by Construir UI)")]
        [SerializeField] Canvas canvas;

        [Header("Views")]
        [SerializeField] GameObject singlePullView;
        [SerializeField] GameObject bulkView;

        [Header("Header")]
        [SerializeField] Button   modeToggleButton;
        [SerializeField] TMP_Text modeToggleLabel;

        [Header("Category buttons")]
        [SerializeField] RectTransform categoryButtonsContent;

        [Header("Single pull controls")]
        [SerializeField] TMP_InputField singleStageInput;
        [SerializeField] Toggle         singleSaveToggle;
        [SerializeField] Button         singlePullButton;

        [Header("Bulk simulation")]
        [SerializeField] TMP_InputField bulkCountInput;
        [SerializeField] TMP_InputField bulkStageInput;
        [SerializeField] Toggle         bulkRandomStageToggle;
        [SerializeField] Toggle         bulkSaveToggle;
        [SerializeField] Button         bulkRunButton;
        [SerializeField] TMP_Text       bulkSummaryLabel;

        [Header("Results (bulk)")]
        [SerializeField] RectTransform  resultsContent;
        [SerializeField] TMP_Text       statusLabel;

        [Header("Navigation")]
        [SerializeField] Button backButton;

        // ── Runtime state ──────────────────────────────────────────────────────

        bool serviceReady;
        List<PrizeTemplate> loadedCategories = new();
        readonly List<Button>    categoryButtons = new();
        readonly List<TMP_Text>  resultRows      = new();
        int  selectedCategoryIndex = -1;
        bool bulkRunning;
        bool inBulkMode;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            singlePullButton?.onClick.AddListener(OnSinglePull);
            bulkRunButton?.onClick.AddListener(OnBulkRun);
            backButton?.onClick.AddListener(() => SceneManager.LoadScene("PrizeTesterMenu"));
            modeToggleButton?.onClick.AddListener(ToggleMode);

            if (PrizeService.Instance == null || !PrizeService.Instance.IsInitialized)
            {
                SetStatus("ERROR: PrizeService no inicializado.", isError: true);
                if (singlePullButton != null) singlePullButton.interactable = false;
                if (bulkRunButton    != null) bulkRunButton.interactable    = false;
                return;
            }

            serviceReady = true;
            BuildCategoryButtons();
            SetMode(false);
            SetStatus("Listo.");
        }

        // ── Mode switching ─────────────────────────────────────────────────────

        void ToggleMode() => SetMode(!inBulkMode);

        void SetMode(bool bulk)
        {
            inBulkMode = bulk;
            singlePullView?.SetActive(!bulk);
            bulkView?.SetActive(bulk);
            if (modeToggleLabel != null)
                modeToggleLabel.text = bulk ? "PULL INDIVIDUAL" : "SIMULACION BULK";
        }

        // ── Category buttons ───────────────────────────────────────────────────

        void BuildCategoryButtons()
        {
            if (categoryButtonsContent == null) return;

            foreach (var btn in categoryButtons)
                if (btn != null) Destroy(btn.gameObject);
            categoryButtons.Clear();
            selectedCategoryIndex = -1;

            if (!serviceReady) return;

            loadedCategories = PrizeService.Instance.GetCategories().ToList();
            var countByCat   = CountByCat();

            for (int i = 0; i < loadedCategories.Count; i++)
            {
                var cat = loadedCategories[i];
                countByCat.TryGetValue(cat.PrizeCategoryId, out int remaining);

                var btn = UIBuilderHelper.MakeButton(categoryButtonsContent, $"CatBtn_{i}",
                    $"{cat.PrizeName}  ({remaining} restantes)",
                    UIBuilderHelper.ColBtnSecondary, UIBuilderHelper.ColTextPrimary, 15);
                UIBuilderHelper.AddLayout(btn.gameObject, 40f);

                int idx = i;
                btn.onClick.AddListener(() => OnCategorySelected(idx));
                categoryButtons.Add(btn);
            }

            if (loadedCategories.Count > 0)
                OnCategorySelected(0);
        }

        void RefreshCategoryCountLabels()
        {
            if (!serviceReady) return;
            var countByCat = CountByCat();

            for (int i = 0; i < categoryButtons.Count && i < loadedCategories.Count; i++)
            {
                var cat = loadedCategories[i];
                countByCat.TryGetValue(cat.PrizeCategoryId, out int remaining);
                var lbl = categoryButtons[i].GetComponentInChildren<TMP_Text>();
                if (lbl != null)
                    lbl.text = $"{cat.PrizeName}  ({remaining} restantes)";
            }
        }

        Dictionary<ushort, int> CountByCat()
        {
            var instances = PrizeService.Instance.GetKioskInstances();
            return instances
                .GroupBy(i => i.PrizeCategoryId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        void OnCategorySelected(int index)
        {
            selectedCategoryIndex = index;
            for (int i = 0; i < categoryButtons.Count; i++)
            {
                var img = categoryButtons[i].GetComponent<Image>();
                if (img != null)
                    img.color = (i == index)
                        ? UIBuilderHelper.ColBtn
                        : UIBuilderHelper.ColBtnSecondary;
            }
        }

        // ── Single pull ────────────────────────────────────────────────────────

        void OnSinglePull()
        {
            if (!serviceReady)
            {
                SetStatus("ERROR: PrizeService no inicializado.", isError: true);
                return;
            }
            if (selectedCategoryIndex < 0 || selectedCategoryIndex >= loadedCategories.Count)
            {
                SetStatus("Seleccion\u00e1 una categor\u00eda primero.", isError: true);
                return;
            }
            if (prizeGivingManager == null)
            {
                SetStatus("ERROR: PrizeGivingManager no asignado.", isError: true);
                return;
            }

            var cat   = loadedCategories[selectedCategoryIndex];
            int stage = 1;
            if (singleStageInput != null &&
                int.TryParse(singleStageInput.text, out int parsedStage))
                stage = Mathf.Max(0, parsedStage);
            bool save = singleSaveToggle != null && singleSaveToggle.isOn;

            singlePullButton.interactable     = false;
            modeToggleButton.interactable     = false;
            canvas.gameObject.SetActive(false);

            prizeGivingManager.StartTesterPull(cat.PrizeCategoryId, stage, save,
                                               OnTesterPullComplete);
        }

        void OnTesterPullComplete(PrizePullResult result)
        {
            canvas.gameObject.SetActive(true);
            singlePullButton.interactable = true;
            modeToggleButton.interactable = true;

            if (result != null)
            {
                var catName = selectedCategoryIndex >= 0
                    ? loadedCategories[selectedCategoryIndex].PrizeName
                    : "?";
                bool saved = singleSaveToggle != null && singleSaveToggle.isOn;
                SetStatus(FormatSingleResult(result, catName, saved));
                if (saved) RefreshCategoryCountLabels();
            }
            else
            {
                SetStatus("Pull completado.");
            }
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

            int catIndex = selectedCategoryIndex >= 0 ? selectedCategoryIndex : 0;
            if (catIndex >= loadedCategories.Count)
            {
                SetStatus("Sin categor\u00edas cargadas.", isError: true);
                bulkRunning = false;
                if (bulkRunButton != null) bulkRunButton.interactable = true;
                yield break;
            }
            var category = loadedCategories[catIndex];

            ClearResultRows();
            if (bulkSummaryLabel != null) bulkSummaryLabel.text = "-";
            SetStatus($"Corriendo {n} pulls...");
            yield return null;

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

                if ((i + 1) % 50 == 0)
                {
                    SetStatus($"Progreso: {i + 1}/{n}...");
                    yield return null;
                }
            }

            UpdateBulkSummary(n, wins, category.PrizeName);
            if (saveAll) RefreshCategoryCountLabels();

            SetStatus(saveAll
                ? $"Bulk completado. {n} pulls guardados."
                : $"Bulk completado. {n} pulls (dry run).");

            bulkRunning = false;
            if (bulkRunButton != null) bulkRunButton.interactable = true;
        }

        void UpdateBulkSummary(int total, int wins, string categoryName)
        {
            if (bulkSummaryLabel == null) return;
            int losses = total - wins;
            float pct  = total > 0 ? 100f * wins / total : 0f;
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
            canvas.gameObject.AddComponent<Image>().color = UIBuilderHelper.ColBg;

            // ── Back button ───────────────────────────────────────────────────
            backButton = UIBuilderHelper.MakeButton(canvas.transform, "BackButton",
                "\u2190 Men\u00fa", UIBuilderHelper.ColBtnSmall,
                UIBuilderHelper.ColTextSecondary, 16, FontStyles.Normal);
            UIBuilderHelper.SetAnchored(backButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0.93f), new Vector2(0.14f, 1f),
                Vector2.zero, Vector2.zero);

            // ── Header title ──────────────────────────────────────────────────
            var header = UIBuilderHelper.MakeText(canvas.transform, "Header",
                26, FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.SetAnchored(header.GetComponent<RectTransform>(),
                new Vector2(0.14f, 0.93f), new Vector2(0.74f, 1f),
                Vector2.zero, Vector2.zero);
            header.text      = "PRIZE GIVING TESTER";
            header.alignment = TextAlignmentOptions.Center;

            // ── Mode toggle button ────────────────────────────────────────────
            modeToggleButton = UIBuilderHelper.MakeButton(canvas.transform, "ModeToggleButton",
                "SIMULACION BULK", UIBuilderHelper.ColBtnSecondary,
                UIBuilderHelper.ColTextPrimary, 15, FontStyles.Normal);
            UIBuilderHelper.SetAnchored(modeToggleButton.GetComponent<RectTransform>(),
                new Vector2(0.75f, 0.93f), new Vector2(0.99f, 1f),
                Vector2.zero, Vector2.zero);
            modeToggleLabel = modeToggleButton.GetComponentInChildren<TMP_Text>();

            // ── Status bar ────────────────────────────────────────────────────
            statusLabel = UIBuilderHelper.MakeText(canvas.transform, "StatusLabel",
                14, FontStyles.Italic, UIBuilderHelper.ColTextMuted);
            UIBuilderHelper.SetAnchored(statusLabel.GetComponent<RectTransform>(),
                new Vector2(0f, 0f), new Vector2(1f, 0.05f),
                Vector2.zero, Vector2.zero);
            statusLabel.text = "Esperando inicio...";

            // ═══════════════════════════════════════════════════════════════════
            // SINGLE PULL VIEW
            // ═══════════════════════════════════════════════════════════════════
            singlePullView = UIBuilderHelper.MakeView(canvas.transform, "SinglePullView",
                UIBuilderHelper.ColPanel);
            UIBuilderHelper.SetAnchored(singlePullView.GetComponent<RectTransform>(),
                new Vector2(0f, 0.05f), new Vector2(1f, 0.93f),
                Vector2.zero, Vector2.zero);

            // Category scroll view (top ~35% of singlePullView)
            var catScrollGo = new GameObject("CategoryScrollView", typeof(RectTransform));
            catScrollGo.transform.SetParent(singlePullView.transform, false);
            UIBuilderHelper.SetAnchored(catScrollGo.GetComponent<RectTransform>(),
                new Vector2(0.01f, 0.62f), new Vector2(0.99f, 0.99f),
                Vector2.zero, Vector2.zero);
            catScrollGo.AddComponent<Image>().color = UIBuilderHelper.ColInput;
            var catScrollRect = catScrollGo.AddComponent<ScrollRect>();
            catScrollRect.horizontal   = false;
            catScrollRect.vertical     = true;
            catScrollRect.movementType = ScrollRect.MovementType.Clamped;

            var catViewport = new GameObject("Viewport", typeof(RectTransform));
            catViewport.transform.SetParent(catScrollGo.transform, false);
            UIBuilderHelper.StretchFill(catViewport.GetComponent<RectTransform>());
            catViewport.AddComponent<RectMask2D>();
            catScrollRect.viewport = catViewport.GetComponent<RectTransform>();

            var catContent = new GameObject("Content", typeof(RectTransform));
            catContent.transform.SetParent(catViewport.transform, false);
            var catContentRT    = catContent.GetComponent<RectTransform>();
            catContentRT.anchorMin = new Vector2(0f, 1f);
            catContentRT.anchorMax = new Vector2(1f, 1f);
            catContentRT.pivot     = new Vector2(0f, 1f);
            catContentRT.offsetMin = catContentRT.offsetMax = Vector2.zero;
            var catCSF = catContent.AddComponent<ContentSizeFitter>();
            catCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIBuilderHelper.AddVerticalLayout(catContent, TextAnchor.UpperLeft, spacing: 4f,
                padding: new RectOffset(6, 6, 6, 6));
            catScrollRect.content = catContentRT;
            categoryButtonsContent = catContentRT;

            // Category label above scroll
            var catLabel = UIBuilderHelper.MakeText(singlePullView.transform, "CatLabel",
                13, FontStyles.Normal, UIBuilderHelper.ColTextMuted,
                TextAlignmentOptions.MidlineLeft);
            UIBuilderHelper.SetAnchored(catLabel.GetComponent<RectTransform>(),
                new Vector2(0.01f, 0.58f), new Vector2(0.99f, 0.62f),
                Vector2.zero, Vector2.zero);
            catLabel.text = "Categor\u00eda:";

            // Controls strip (bottom 15% of singlePullView): Stage label + input + save toggle + pull button
            var strip = new GameObject("ControlsStrip", typeof(RectTransform));
            strip.transform.SetParent(singlePullView.transform, false);
            UIBuilderHelper.SetAnchored(strip.GetComponent<RectTransform>(),
                new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.15f),
                Vector2.zero, Vector2.zero);
            var stripHlg = strip.AddComponent<HorizontalLayoutGroup>();
            stripHlg.childAlignment        = TextAnchor.MiddleLeft;
            stripHlg.spacing               = 10f;
            stripHlg.childControlWidth     = false;
            stripHlg.childForceExpandWidth = false;
            stripHlg.padding               = new RectOffset(8, 8, 4, 4);

            var stageLabel = UIBuilderHelper.MakeText(strip.transform, "StageLabel",
                14, FontStyles.Normal, UIBuilderHelper.ColTextSecondary,
                TextAlignmentOptions.MidlineLeft);
            stageLabel.text = "Stage:";
            UIBuilderHelper.AddLayout(stageLabel.gameObject, 36f, 46f);

            singleStageInput = UIBuilderHelper.MakeInputField(strip.transform, "StageInput", "1");
            singleStageInput.text        = "1";
            singleStageInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            UIBuilderHelper.AddLayout(singleStageInput.gameObject, 36f, 60f);

            // Save toggle
            var saveRow = new GameObject("SaveToggleRow", typeof(RectTransform));
            saveRow.transform.SetParent(strip.transform, false);
            UIBuilderHelper.AddLayout(saveRow, 36f, 130f);
            var saveHlg = saveRow.AddComponent<HorizontalLayoutGroup>();
            saveHlg.childAlignment        = TextAnchor.MiddleLeft;
            saveHlg.spacing               = 6f;
            saveHlg.childControlWidth     = false;
            saveHlg.childForceExpandWidth = false;

            var tGo = new GameObject("Toggle", typeof(RectTransform));
            tGo.transform.SetParent(saveRow.transform, false);
            var tRT = tGo.GetComponent<RectTransform>();
            tRT.sizeDelta = new Vector2(24f, 24f);
            var tBg = tGo.AddComponent<Image>();
            tBg.color     = UIBuilderHelper.ColInput;
            singleSaveToggle = tGo.AddComponent<Toggle>();
            singleSaveToggle.targetGraphic = tBg;
            var ck = new GameObject("Checkmark", typeof(RectTransform));
            ck.transform.SetParent(tGo.transform, false);
            UIBuilderHelper.StretchFill(ck.GetComponent<RectTransform>());
            var ckImg = ck.AddComponent<Image>();
            ckImg.color            = UIBuilderHelper.ColBtn;
            singleSaveToggle.graphic = ckImg;
            ck.SetActive(false);

            var saveLbl = UIBuilderHelper.MakeText(saveRow.transform, "SaveLabel",
                13, FontStyles.Normal, UIBuilderHelper.ColTextSecondary,
                TextAlignmentOptions.MidlineLeft);
            saveLbl.text = "Guardar pull";

            singlePullButton = UIBuilderHelper.MakeButton(strip.transform, "PullButton",
                "SIMULAR PULL", UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary, 17);
            UIBuilderHelper.AddLayout(singlePullButton.gameObject, 40f, 160f);

            // ═══════════════════════════════════════════════════════════════════
            // BULK VIEW
            // ═══════════════════════════════════════════════════════════════════
            bulkView = UIBuilderHelper.MakeView(canvas.transform, "BulkView",
                UIBuilderHelper.ColPanel);
            UIBuilderHelper.SetAnchored(bulkView.GetComponent<RectTransform>(),
                new Vector2(0f, 0.05f), new Vector2(1f, 0.93f),
                Vector2.zero, Vector2.zero);
            UIBuilderHelper.AddVerticalLayout(bulkView, TextAnchor.UpperLeft, spacing: 8f,
                padding: new RectOffset(16, 16, 12, 12));

            BuildText(bulkView.transform, "BulkTitle",
                "SIMULACION BULK", 18, FontStyles.Bold, 28f);

            BuildLabel(bulkView.transform, "CountLabel", "N\u00b0 de pulls:", 20f);
            bulkCountInput = UIBuilderHelper.MakeInputField(bulkView.transform, "BulkCountInput", "100");
            bulkCountInput.text        = "100";
            bulkCountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            UIBuilderHelper.AddLayout(bulkCountInput.gameObject, 36f);

            BuildLabel(bulkView.transform, "BulkStageLabel", "StageIndex fijo:", 20f);
            bulkStageInput = UIBuilderHelper.MakeInputField(bulkView.transform, "BulkStageInput", "1");
            bulkStageInput.text        = "1";
            bulkStageInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            UIBuilderHelper.AddLayout(bulkStageInput.gameObject, 36f);

            BuildToggleRow(bulkView.transform, "RandomStageRow",
                out bulkRandomStageToggle, "Aleatorizar stage por pull", 28f);

            BuildToggleRow(bulkView.transform, "SaveRow",
                out bulkSaveToggle, "Guardar resultados", 28f);

            bulkRunButton = UIBuilderHelper.MakeButton(bulkView.transform, "RunButton",
                "EJECUTAR BULK", UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary, 20);
            UIBuilderHelper.AddLayout(bulkRunButton.gameObject, 44f);

            bulkSummaryLabel = UIBuilderHelper.MakeText(bulkView.transform, "Summary",
                14, FontStyles.Normal, UIBuilderHelper.ColTextSecondary,
                TextAlignmentOptions.Left);
            bulkSummaryLabel.text             = "-";
            bulkSummaryLabel.enableWordWrapping = true;
            UIBuilderHelper.AddLayout(bulkSummaryLabel.gameObject, 44f);

            // Results scroll (bottom 42% of bulkView, positioned with SetAnchored)
            var scrollGo = new GameObject("ResultsScrollView", typeof(RectTransform));
            scrollGo.transform.SetParent(bulkView.transform, false);
            // Remove from VLG by using a LayoutElement with zero size and override:
            var scrollLE        = scrollGo.AddComponent<LayoutElement>();
            scrollLE.ignoreLayout = true;
            UIBuilderHelper.SetAnchored(scrollGo.GetComponent<RectTransform>(),
                new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.40f),
                Vector2.zero, Vector2.zero);
            scrollGo.AddComponent<Image>().color = UIBuilderHelper.ColInput;
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal   = false;
            scrollRect.vertical     = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

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

            // Start in single-pull view (bulkView inactive)
            bulkView.SetActive(false);

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[PrizeTesterManager] UI construida. Guard\u00e1 la escena.");
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

        static GameObject BuildToggleRow(Transform parent, string name,
            out Toggle toggle, string labelText, float height)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            UIBuilderHelper.AddLayout(row, height);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment        = TextAnchor.MiddleLeft;
            h.spacing               = 8f;
            h.childControlWidth     = false;
            h.childForceExpandWidth = false;

            var tGo = new GameObject("Toggle", typeof(RectTransform));
            tGo.transform.SetParent(row.transform, false);
            var tRT  = tGo.GetComponent<RectTransform>();
            tRT.sizeDelta = new Vector2(24f, 24f);
            var bg = tGo.AddComponent<Image>();
            bg.color  = UIBuilderHelper.ColInput;
            toggle    = tGo.AddComponent<Toggle>();
            toggle.isOn          = false;
            toggle.targetGraphic = bg;

            var ck    = new GameObject("Checkmark", typeof(RectTransform));
            ck.transform.SetParent(tGo.transform, false);
            UIBuilderHelper.StretchFill(ck.GetComponent<RectTransform>());
            var ckImg  = ck.AddComponent<Image>();
            ckImg.color    = UIBuilderHelper.ColBtn;
            toggle.graphic = ckImg;
            ck.SetActive(false);

            var lbl = UIBuilderHelper.MakeText(row.transform, "Label",
                14, FontStyles.Normal, UIBuilderHelper.ColTextSecondary,
                TextAlignmentOptions.Left);
            lbl.text = labelText;

            return row;
        }
#endif
    }
}
