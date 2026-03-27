using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    /// <summary>
    /// Manages the RegisterUser scene (before the game).
    /// Collects player data and stores it in PlayerSessionData before loading the game.
    /// </summary>
    public sealed class RegisterUserManager : MonoBehaviour
    {
        [Header("Form")]
        [SerializeField] TMP_InputField firstNameInput;
        [SerializeField] TMP_InputField lastNameInput;
        [SerializeField] TMP_InputField phoneInput;
        [SerializeField] TMP_InputField officeInput;
        [SerializeField] Button submitButton;
        [SerializeField] Button backButton;
        [SerializeField] TMP_Text errorLabel;
        [SerializeField] VirtualKeyboard keyboard;

        [Header("Textos")]
        [SerializeField] string firstNamePlaceholder = "Nombre";
        [SerializeField] string lastNamePlaceholder  = "Apellido";
        [SerializeField] string phonePlaceholder     = "Celular";
        [SerializeField] string officePlaceholder    = "Sucursal RE/MAX (opcional)";
        [SerializeField] string submitButtonText     = "JUGAR";
        [SerializeField] string errorEmptyFirstName  = "Ingresá tu nombre";
        [SerializeField] string errorEmptyPhone      = "Ingresá tu celular";

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            Time.timeScale = 1f;
            PlayerSessionData.Clear();

            submitButton?.onClick.AddListener(OnSubmit);
            backButton?.onClick.AddListener(() => SceneManager.LoadScene("ReadyKiosk"));

            if (keyboard != null)
            {
                firstNameInput?.onSelect.AddListener(_ => keyboard.SetActiveField(firstNameInput));
                lastNameInput?.onSelect.AddListener(_ => keyboard.SetActiveField(lastNameInput));
                phoneInput?.onSelect.AddListener(_ => keyboard.SetActiveField(phoneInput));
                officeInput?.onSelect.AddListener(_ => keyboard.SetActiveField(officeInput));
                keyboard.Show();
            }

            errorLabel?.gameObject.SetActive(false);
        }

        // ── Form logic ─────────────────────────────────────────────────────────

        void OnSubmit()
        {
            if (firstNameInput == null || phoneInput == null) return;

            if (string.IsNullOrWhiteSpace(firstNameInput.text))
            {
                ShowError(errorEmptyFirstName);
                return;
            }

            if (string.IsNullOrWhiteSpace(phoneInput.text))
            {
                ShowError(errorEmptyPhone);
                return;
            }

            PlayerSessionData.FirstName = firstNameInput.text.Trim();
            PlayerSessionData.LastName  = lastNameInput  != null ? lastNameInput.text.Trim()  : "";
            PlayerSessionData.Phone     = phoneInput.text.Trim();
            PlayerSessionData.Office    = officeInput    != null ? officeInput.text.Trim()    : "";

            SceneManager.LoadScene(1);
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

            UnityEditor.Undo.RecordObject(this, "Construir UI RegisterUser");

            UIBuilderHelper.EnsureEventSystem();

            var canvas = UIBuilderHelper.MakeCanvas(transform, "RegisterUserCanvas", 100);
            canvas.gameObject.AddComponent<UnityEngine.UI.Image>().color = UIBuilderHelper.ColBg;

            // ── Form view ──────────────────────────────────────────────────
            var formView = UIBuilderHelper.MakeView(canvas.transform, "FormView");
            // Reserve bottom 220px for the keyboard
            UIBuilderHelper.SetAnchored(formView.GetComponent<RectTransform>(),
                new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, 220), new Vector2(0, 0));
            UIBuilderHelper.AddVerticalLayout(formView, spacing: 10f,
                padding: new RectOffset(40, 40, 20, 20));

            var title = UIBuilderHelper.MakeText(formView.transform, "Title",
                34, FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            title.text = "Registrate para jugar";
            UIBuilderHelper.AddLayout(title.gameObject, 48);

            firstNameInput = UIBuilderHelper.MakeInputField(formView.transform,
                "FirstNameInput", firstNamePlaceholder);
            UIBuilderHelper.AddLayout(firstNameInput.gameObject, 44);

            lastNameInput = UIBuilderHelper.MakeInputField(formView.transform,
                "LastNameInput", lastNamePlaceholder);
            UIBuilderHelper.AddLayout(lastNameInput.gameObject, 44);

            phoneInput = UIBuilderHelper.MakeInputField(formView.transform,
                "PhoneInput", phonePlaceholder);
            phoneInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            UIBuilderHelper.AddLayout(phoneInput.gameObject, 44);

            officeInput = UIBuilderHelper.MakeInputField(formView.transform,
                "OfficeInput", officePlaceholder);
            UIBuilderHelper.AddLayout(officeInput.gameObject, 44);

            errorLabel = UIBuilderHelper.MakeText(formView.transform, "ErrorLabel",
                18, FontStyles.Normal, UIBuilderHelper.ColError);
            UIBuilderHelper.AddLayout(errorLabel.gameObject, 28);
            errorLabel.gameObject.SetActive(false);

            submitButton = UIBuilderHelper.MakeButton(formView.transform, "SubmitButton",
                submitButtonText, UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary,
                32, FontStyles.Bold);
            UIBuilderHelper.AddLayout(submitButton.gameObject, 52);

            // ── Back button (top-left, outside form layout) ────────────────
            backButton = UIBuilderHelper.MakeButton(canvas.transform, "BackButton",
                "Volver", UIBuilderHelper.ColBtnSmall, UIBuilderHelper.ColTextSecondary);
            var backRt = backButton.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0, 1);
            backRt.anchorMax = new Vector2(0, 1);
            backRt.pivot     = new Vector2(0, 1);
            backRt.sizeDelta = new Vector2(100, 36);
            backRt.anchoredPosition = new Vector2(10, -10);

            // ── Virtual keyboard ───────────────────────────────────────────
            var kbGo = new GameObject("KeyboardPanel", typeof(RectTransform));
            kbGo.transform.SetParent(canvas.transform, false);
            keyboard = kbGo.AddComponent<VirtualKeyboard>();
            keyboard.Build();
            keyboard.Hide();

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[RegisterUserManager] UI construida. Guardá la escena.");
        }
#endif
    }
}
