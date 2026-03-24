using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    /// <summary>
    /// Manages the PrizeGiving scene (after the game).
    /// Reads PlayerSessionData, pulls a prize, records the play session,
    /// and exports both Jugadores.csv and WonPrizes.csv.
    /// </summary>
    public sealed class PrizeGivingManager : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] GameObject prizeView;
        [SerializeField] GameObject consolationView;
        [SerializeField] GameObject successView;
        [SerializeField] VirtualKeyboard keyboard;

        [Header("Prize view")]
        [SerializeField] TMP_Text prizeHeaderLabel;
        [SerializeField] TMP_Text prizeDescriptionLabel;
        [SerializeField] TMP_Text playerNameLabel;
        [SerializeField] Button claimButton;
        [SerializeField] TMP_Text errorLabel;

        [Header("Consolation")]
        [SerializeField] TMP_Text consolationLabel;
        [SerializeField] Button playAgainConsolation;

        [Header("Success")]
        [SerializeField] TMP_Text successHeaderLabel;
        [SerializeField] TMP_Text successDescriptionLabel;
        [SerializeField] TMP_Text successConfirmLabel;
        [SerializeField] Button playAgainSuccess;

        [Header("Timer")]
        [SerializeField] float returnDelay = 5f;

        [Header("Textos")]
        [SerializeField] string prizeHeaderFormat    = "\u00a1Ganaste: {0}!";
        [SerializeField] string consolationText      = "\u00a1Segu\u00ed intentando!";
        [SerializeField] string successConfirmText   = "Retiralo en el stand";
        [SerializeField] string claimButtonText      = "RECLAMAR PREMIO";
        [SerializeField] string playAgainText        = "JUGAR DE NUEVO";
        [SerializeField] string claimFailedText      = "Hubo un error, intent\u00e1 de nuevo";

        PrizePullResult currentPull;
        float returnTimer = -1f;
        float returnStartTime;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            Time.timeScale = 1f;

            claimButton?.onClick.AddListener(OnClaim);
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
                RecordAndShowConsolation(null);
                return;
            }

            currentPull = PrizeService.Instance.TryPullPrize();

            if (currentPull.IsRealPrize)
                ShowPrizeView();
            else
                RecordAndShowConsolation(currentPull);
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

        // ── View switching ─────────────────────────────────────────────────────

        void HideAllViews()
        {
            prizeView?.SetActive(false);
            consolationView?.SetActive(false);
            successView?.SetActive(false);
            errorLabel?.gameObject.SetActive(false);
            playAgainConsolation?.gameObject.SetActive(false);
            playAgainSuccess?.gameObject.SetActive(false);
        }

        void ShowPrizeView()
        {
            prizeView?.SetActive(true);

            if (prizeHeaderLabel != null)
                prizeHeaderLabel.text = string.Format(prizeHeaderFormat, currentPull.PrizeName);

            if (prizeDescriptionLabel != null)
            {
                var desc = currentPull.PrizeDescription;
                prizeDescriptionLabel.text = desc;
                prizeDescriptionLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(desc));
            }

            if (playerNameLabel != null)
            {
                var fullName = $"{PlayerSessionData.FirstName} {PlayerSessionData.LastName}".Trim();
                playerNameLabel.text = fullName;
            }

            keyboard?.Show();
        }

        void RecordAndShowConsolation(PrizePullResult pull)
        {
            PrizeService.Instance?.RecordPlay(
                PlayerSessionData.FirstName,
                PlayerSessionData.LastName,
                PlayerSessionData.Phone,
                PlayerSessionData.Office,
                pull);

            PlayerSessionData.Clear();

            keyboard?.Hide();
            consolationView?.SetActive(true);
            if (consolationLabel != null)
                consolationLabel.text = consolationText;

            playAgainConsolation?.gameObject.SetActive(true);
            StartReturnTimer();
        }

        void ShowSuccess()
        {
            keyboard?.Hide();
            prizeView?.SetActive(false);
            successView?.SetActive(true);

            if (successHeaderLabel != null)
                successHeaderLabel.text = string.Format(prizeHeaderFormat, currentPull.PrizeName);

            if (successDescriptionLabel != null)
            {
                var desc = currentPull.PrizeDescription;
                successDescriptionLabel.text = desc;
                successDescriptionLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(desc));
            }

            if (successConfirmLabel != null)
                successConfirmLabel.text = successConfirmText;

            playAgainSuccess?.gameObject.SetActive(true);
            StartReturnTimer();
        }

        // ── Timer ──────────────────────────────────────────────────────────────

        void StartReturnTimer()
        {
            returnTimer = returnDelay;
            returnStartTime = Time.unscaledTime;
        }

        static void PlayAgain() => SceneManager.LoadScene("RegisterUser");

        // ── Claim logic ────────────────────────────────────────────────────────

        void OnClaim()
        {
            if (PrizeService.Instance == null) return;

            var fullName = $"{PlayerSessionData.FirstName} {PlayerSessionData.LastName}".Trim();

            // Confirm prize reservation in the prize pool system.
            var claimed = PrizeService.Instance.ClaimPrize(
                fullName,
                PlayerSessionData.Phone,
                PlayerSessionData.Office);

            if (!claimed)
            {
                ShowError(claimFailedText);
                return;
            }

            // Record play + export both CSVs.
            PrizeService.Instance.RecordPlay(
                PlayerSessionData.FirstName,
                PlayerSessionData.LastName,
                PlayerSessionData.Phone,
                PlayerSessionData.Office,
                currentPull);

            PlayerSessionData.Clear();
            ShowSuccess();
        }

        void ShowError(string message)
        {
            if (errorLabel == null) return;
            errorLabel.text = message;
            errorLabel.gameObject.SetActive(true);
        }

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

            var canvas = UIBuilderHelper.MakeCanvas(transform, "PrizeGivingCanvas", 100);
            canvas.gameObject.AddComponent<UnityEngine.UI.Image>().color = UIBuilderHelper.ColBg;

            // ── Prize view ─────────────────────────────────────────────────
            prizeView = UIBuilderHelper.MakeView(canvas.transform, "PrizeView");
            UIBuilderHelper.SetAnchored(prizeView.GetComponent<RectTransform>(),
                new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, 220), new Vector2(0, 0));
            UIBuilderHelper.AddVerticalLayout(prizeView, spacing: 8f,
                padding: new RectOffset(20, 20, 10, 10));

            prizeHeaderLabel = UIBuilderHelper.MakeText(prizeView.transform, "PrizeHeader",
                36, FontStyles.Bold, UIBuilderHelper.ColSuccess);
            UIBuilderHelper.AddLayout(prizeHeaderLabel.gameObject, 50);

            prizeDescriptionLabel = UIBuilderHelper.MakeText(prizeView.transform,
                "PrizeDescription", 22, FontStyles.Normal, UIBuilderHelper.ColTextSecondary);
            UIBuilderHelper.AddLayout(prizeDescriptionLabel.gameObject, 36);
            prizeDescriptionLabel.gameObject.SetActive(false);

            playerNameLabel = UIBuilderHelper.MakeText(prizeView.transform, "PlayerName",
                24, FontStyles.Italic, UIBuilderHelper.ColTextMuted);
            UIBuilderHelper.AddLayout(playerNameLabel.gameObject, 32);

            errorLabel = UIBuilderHelper.MakeText(prizeView.transform, "ErrorLabel",
                18, FontStyles.Normal, UIBuilderHelper.ColError);
            UIBuilderHelper.AddLayout(errorLabel.gameObject, 28);
            errorLabel.gameObject.SetActive(false);

            claimButton = UIBuilderHelper.MakeButton(prizeView.transform, "ClaimButton",
                claimButtonText, UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary,
                32, FontStyles.Bold);
            UIBuilderHelper.AddLayout(claimButton.gameObject, 52);

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

            successHeaderLabel = UIBuilderHelper.MakeText(successView.transform, "SuccessHeader",
                36, FontStyles.Bold, UIBuilderHelper.ColSuccess);
            UIBuilderHelper.AddLayout(successHeaderLabel.gameObject, 50);

            successDescriptionLabel = UIBuilderHelper.MakeText(successView.transform,
                "SuccessDescription", 22, FontStyles.Normal, UIBuilderHelper.ColTextSecondary);
            UIBuilderHelper.AddLayout(successDescriptionLabel.gameObject, 36);

            successConfirmLabel = UIBuilderHelper.MakeText(successView.transform, "SuccessConfirm",
                20, FontStyles.Italic, UIBuilderHelper.ColTextMuted);
            UIBuilderHelper.AddLayout(successConfirmLabel.gameObject, 32);

            playAgainSuccess = UIBuilderHelper.MakeButton(successView.transform,
                "PlayAgainButton", playAgainText, UIBuilderHelper.ColBtn,
                UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.AddLayout(playAgainSuccess.gameObject, 52);

            // ── Virtual keyboard (shown on prize view only) ────────────────
            var kbGo = new GameObject("KeyboardPanel", typeof(RectTransform));
            kbGo.transform.SetParent(canvas.transform, false);
            keyboard = kbGo.AddComponent<VirtualKeyboard>();
            keyboard.Build();
            keyboard.Hide();

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[PrizeGivingManager] UI construida. Guardá la escena.");
        }
#endif
    }
}
