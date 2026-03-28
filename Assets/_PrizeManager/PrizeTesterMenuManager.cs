using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    /// <summary>
    /// Hub menu for prize-system tooling.
    ///
    /// Buttons:
    ///   Config            → PrizeTesterConfig scene (ConfigManager with back=PrizeTesterMenu)
    ///   Prize Verificator → PrizeAdminScene
    ///   Prize Tester      → PrizeGivingTester
    ///   Exit              → quit (or stop play mode in the Editor)
    ///
    /// Scene setup:
    ///   1. Create Assets/_PrizeManager/Scenes/PrizeTesterMenu.unity
    ///   2. Add this component to a GameObject, run "Construir UI".
    ///   3. Create Assets/_PrizeManager/Scenes/PrizeTesterConfig.unity with a ConfigManager
    ///      component — set its "Back Scene Name" field to "PrizeTesterMenu".
    /// </summary>
    public sealed class PrizeTesterMenuManager : MonoBehaviour
    {
        [Header("Buttons (set by Construir UI)")]
        [SerializeField] Button configButton;
        [SerializeField] Button verificatorButton;
        [SerializeField] Button testerButton;
        [SerializeField] Button exitButton;

        void Start()
        {
            Time.timeScale = 1f;
            configButton?.onClick.AddListener(
                () => SceneManager.LoadScene("PrizeTesterConfig"));
            verificatorButton?.onClick.AddListener(
                () => SceneManager.LoadScene("PrizeAdminScene"));
            testerButton?.onClick.AddListener(
                () => SceneManager.LoadScene("PrizeGivingTester"));
            exitButton?.onClick.AddListener(Exit);
        }

        static void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("Construir UI")]
        void BuildUi()
        {
            var existing = GetComponentInChildren<Canvas>(true);
            if (existing != null)
                UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);

            UnityEditor.Undo.RecordObject(this, "Construir UI PrizeTesterMenu");
            UIBuilderHelper.EnsureEventSystem();

            var canvas = UIBuilderHelper.MakeCanvas(transform, "PrizeTesterMenuCanvas");
            canvas.gameObject.AddComponent<Image>().color = UIBuilderHelper.ColBg;

            var panel = UIBuilderHelper.MakeView(canvas.transform, "Panel");
            UIBuilderHelper.AddVerticalLayout(panel, spacing: 20f,
                padding: new RectOffset(120, 120, 80, 80));

            // Title
            var title = UIBuilderHelper.MakeText(panel.transform, "Title",
                40, FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            title.text = "Prize Tools";
            UIBuilderHelper.AddLayout(title.gameObject, 60);

            // Spacer
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(panel.transform, false);
            UIBuilderHelper.AddLayout(spacer, 20);

            // Config
            configButton = UIBuilderHelper.MakeButton(panel.transform, "ConfigButton",
                "Config", UIBuilderHelper.ColBtnSecondary, UIBuilderHelper.ColTextPrimary,
                24, FontStyles.Normal);
            UIBuilderHelper.AddLayout(configButton.gameObject, 52);

            // Prize Verificator
            verificatorButton = UIBuilderHelper.MakeButton(panel.transform, "VerificatorButton",
                "Prize Verificator", UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary,
                24, FontStyles.Normal);
            UIBuilderHelper.AddLayout(verificatorButton.gameObject, 52);

            // Prize Tester
            testerButton = UIBuilderHelper.MakeButton(panel.transform, "TesterButton",
                "Prize Tester", UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary,
                24, FontStyles.Normal);
            UIBuilderHelper.AddLayout(testerButton.gameObject, 52);

            // Exit
            exitButton = UIBuilderHelper.MakeButton(panel.transform, "ExitButton",
                "Exit", UIBuilderHelper.ColBtnSecondary, UIBuilderHelper.ColTextPrimary,
                24, FontStyles.Normal);
            UIBuilderHelper.AddLayout(exitButton.gameObject, 52);

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[PrizeTesterMenuManager] UI construida. Guard\u00e1 la escena.");
        }
#endif
    }
}
