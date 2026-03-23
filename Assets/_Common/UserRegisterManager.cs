using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    public sealed class UserRegisterManager : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] GameObject registrationView;
        [SerializeField] GameObject consolationView;
        [SerializeField] GameObject successView;

        [Header("Registration Form")]
        [SerializeField] TMP_Text prizeHeaderLabel;
        [SerializeField] TMP_Text prizeDescriptionLabel;
        [SerializeField] TMP_InputField nameInput;
        [SerializeField] TMP_InputField phoneInput;
        [SerializeField] TMP_InputField officeInput;
        [SerializeField] Button submitButton;
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
        [SerializeField] string prizeHeaderFormat = "\u00a1Ganaste: {0}!";
        [SerializeField] string consolationText = "\u00a1Segu\u00ed intentando!";
        [SerializeField] string successConfirmText = "Retiralo en el stand";
        [SerializeField] string errorEmptyName = "Ingres\u00e1 tu nombre";
        [SerializeField] string errorEmptyPhone = "Ingres\u00e1 tu celular";
        [SerializeField] string claimFailedText = "Hubo un error, intent\u00e1 de nuevo";
        [SerializeField] string namePlaceholder = "Tu nombre";
        [SerializeField] string phonePlaceholder = "Tu celular";
        [SerializeField] string officePlaceholder = "Sucursal (opcional)";
        [SerializeField] string submitButtonText = "RECLAMAR PREMIO";
        [SerializeField] string playAgainText = "JUGAR DE NUEVO";

        PrizePullResult currentPull;
        float returnTimer = -1f;
        float returnStartTime;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            Time.timeScale = 1f;

            submitButton?.onClick.AddListener(OnSubmit);
            playAgainConsolation?.onClick.AddListener(PlayAgain);
            playAgainSuccess?.onClick.AddListener(PlayAgain);

            HideAllViews();

            if (PrizeService.Instance == null)
            {
                Debug.LogWarning("[UserRegister] PrizeService no encontrado.");
                ShowConsolation();
                return;
            }

            currentPull = PrizeService.Instance.TryPullPrize();

            if (currentPull.IsRealPrize)
                ShowRegistrationForm();
            else
                ShowConsolation();
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
            registrationView?.SetActive(false);
            consolationView?.SetActive(false);
            successView?.SetActive(false);
            errorLabel?.gameObject.SetActive(false);
            playAgainConsolation?.gameObject.SetActive(false);
            playAgainSuccess?.gameObject.SetActive(false);
        }

        void ShowRegistrationForm()
        {
            registrationView?.SetActive(true);

            if (prizeHeaderLabel != null)
                prizeHeaderLabel.text = string.Format(prizeHeaderFormat, currentPull.PrizeName);

            if (prizeDescriptionLabel != null)
            {
                var desc = currentPull.PrizeDescription;
                prizeDescriptionLabel.text = desc;
                prizeDescriptionLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(desc));
            }

            if (nameInput != null) nameInput.text = "";
            if (phoneInput != null) phoneInput.text = "";
            if (officeInput != null) officeInput.text = "";
        }

        void ShowConsolation()
        {
            consolationView?.SetActive(true);
            if (consolationLabel != null)
                consolationLabel.text = consolationText;

            // Immediately show play again button and start timer.
            playAgainConsolation?.gameObject.SetActive(true);
            StartReturnTimer();
        }

        void ShowSuccess()
        {
            registrationView?.SetActive(false);
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

            // Show play again button and start timer after successful registration.
            playAgainSuccess?.gameObject.SetActive(true);
            StartReturnTimer();
        }

        // ── Timer ──────────────────────────────────────────────────────────────

        void StartReturnTimer()
        {
            returnTimer = returnDelay;
            returnStartTime = Time.unscaledTime;
        }

        static void PlayAgain()
        {
            SceneManager.LoadScene(1);
        }

        // ── Form logic ─────────────────────────────────────────────────────────

        void OnSubmit()
        {
            if (nameInput == null || phoneInput == null) return;

            if (string.IsNullOrWhiteSpace(nameInput.text))
            {
                ShowError(errorEmptyName);
                return;
            }

            if (string.IsNullOrWhiteSpace(phoneInput.text))
            {
                ShowError(errorEmptyPhone);
                return;
            }

            var office = officeInput != null ? officeInput.text.Trim() : "";

            var claimed = PrizeService.Instance.ClaimPrize(
                nameInput.text.Trim(),
                phoneInput.text.Trim(),
                office);

            if (!claimed)
            {
                ShowError(claimFailedText);
                return;
            }

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

            UnityEditor.Undo.RecordObject(this, "Construir UI UserRegister");

            UIBuilderHelper.EnsureEventSystem();

            var canvas = UIBuilderHelper.MakeCanvas(transform, "UserRegisterCanvas", 100);
            canvas.gameObject.AddComponent<UnityEngine.UI.Image>().color = UIBuilderHelper.ColBg;

            // ── Registration view ──────────────────────────────────────────
            registrationView = UIBuilderHelper.MakeView(canvas.transform, "RegistrationView");
            UIBuilderHelper.AddVerticalLayout(registrationView, spacing: 12f);

            prizeHeaderLabel = UIBuilderHelper.MakeText(registrationView.transform, "PrizeHeader",
                36, FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.AddLayout(prizeHeaderLabel.gameObject, 50);

            prizeDescriptionLabel = UIBuilderHelper.MakeText(registrationView.transform,
                "PrizeDescription", 22, FontStyles.Normal, UIBuilderHelper.ColTextSecondary);
            UIBuilderHelper.AddLayout(prizeDescriptionLabel.gameObject, 36);

            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(registrationView.transform, false);
            UIBuilderHelper.AddLayout(spacer, 12);

            nameInput = UIBuilderHelper.MakeInputField(registrationView.transform,
                "NameInput", namePlaceholder);
            UIBuilderHelper.AddLayout(nameInput.gameObject, 48);

            phoneInput = UIBuilderHelper.MakeInputField(registrationView.transform,
                "PhoneInput", phonePlaceholder);
            phoneInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            UIBuilderHelper.AddLayout(phoneInput.gameObject, 48);

            officeInput = UIBuilderHelper.MakeInputField(registrationView.transform,
                "OfficeInput", officePlaceholder);
            UIBuilderHelper.AddLayout(officeInput.gameObject, 48);

            errorLabel = UIBuilderHelper.MakeText(registrationView.transform, "ErrorLabel",
                18, FontStyles.Normal, UIBuilderHelper.ColError);
            UIBuilderHelper.AddLayout(errorLabel.gameObject, 28);
            errorLabel.gameObject.SetActive(false);

            submitButton = UIBuilderHelper.MakeButton(registrationView.transform, "SubmitButton",
                submitButtonText, UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.AddLayout(submitButton.gameObject, 52);

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

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[UserRegisterManager] UI construida. Guard\u00e1 la escena.");
        }
#endif
    }
}
