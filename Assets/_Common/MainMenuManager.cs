using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    public sealed class MainMenuManager : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] Button configButton;
        [SerializeField] Button readyButton;

        void Start()
        {
            Time.timeScale = 1f;
            configButton?.onClick.AddListener(() => SceneManager.LoadScene("Config"));
            readyButton?.onClick.AddListener(() => SceneManager.LoadScene("ReadyKiosk"));
        }

#if UNITY_EDITOR
        [ContextMenu("Construir UI")]
        void BuildUi()
        {
            // Clean previous canvas.
            var existing = GetComponentInChildren<Canvas>(true);
            if (existing != null)
                UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);

            UnityEditor.Undo.RecordObject(this, "Construir UI MainMenu");

            UIBuilderHelper.EnsureEventSystem();

            var canvas = UIBuilderHelper.MakeCanvas(transform, "MainMenuCanvas");
            canvas.gameObject.AddComponent<UnityEngine.UI.Image>().color = UIBuilderHelper.ColBg;

            // Center panel
            var panel = UIBuilderHelper.MakeView(canvas.transform, "Panel");
            UIBuilderHelper.AddVerticalLayout(panel, spacing: 20f,
                padding: new RectOffset(120, 120, 80, 80));

            // Title
            var title = UIBuilderHelper.MakeText(panel.transform, "Title",
                48, TMPro.FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            title.text = "GE Travel Games";
            UIBuilderHelper.AddLayout(title.gameObject, 70);

            // Spacer
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(panel.transform, false);
            UIBuilderHelper.AddLayout(spacer, 30);

            // Config button
            configButton = UIBuilderHelper.MakeButton(panel.transform, "ConfigButton",
                "Configuraci\u00f3n", UIBuilderHelper.ColBtnSecondary, UIBuilderHelper.ColTextPrimary);
            UIBuilderHelper.AddLayout(configButton.gameObject, 52);

            // Ready button
            readyButton = UIBuilderHelper.MakeButton(panel.transform, "ReadyButton",
                "Preparar Juego", UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary,
                28, TMPro.FontStyles.Bold);
            UIBuilderHelper.AddLayout(readyButton.gameObject, 60);

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[MainMenuManager] UI construida. Guard\u00e1 la escena.");
        }
#endif
    }
}
