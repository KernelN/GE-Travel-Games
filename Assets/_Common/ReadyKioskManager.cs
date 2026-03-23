using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    public sealed class ReadyKioskManager : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] Button playButton;
        [SerializeField] Button backButton;

        void Start()
        {
            Time.timeScale = 1f;

            // Ensure PrizeService is alive and initialized.
            if (PrizeService.Instance == null)
            {
                var go = new GameObject("PrizeService");
                go.AddComponent<PrizeService>();
            }

            if (!PrizeService.Instance.IsInitialized)
                PrizeService.Instance.Initialize();

            playButton?.onClick.AddListener(() => SceneManager.LoadScene(1));
            backButton?.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
        }

#if UNITY_EDITOR
        [ContextMenu("Construir UI")]
        void BuildUi()
        {
            var existing = GetComponentInChildren<Canvas>(true);
            if (existing != null)
                UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);

            UnityEditor.Undo.RecordObject(this, "Construir UI ReadyKiosk");

            UIBuilderHelper.EnsureEventSystem();

            var canvas = UIBuilderHelper.MakeCanvas(transform, "ReadyKioskCanvas");
            canvas.gameObject.AddComponent<UnityEngine.UI.Image>().color = UIBuilderHelper.ColBg;

            // Title
            var title = UIBuilderHelper.MakeText(canvas.transform, "Title",
                52, TMPro.FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            title.text = "GE Travel Games";
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.1f, 0.65f);
            titleRt.anchorMax = new Vector2(0.9f, 0.85f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            // Big play button (center)
            playButton = UIBuilderHelper.MakeButton(canvas.transform, "PlayButton",
                "JUGAR", UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary,
                48, TMPro.FontStyles.Bold);
            var playRt = playButton.GetComponent<RectTransform>();
            playRt.anchorMin = new Vector2(0.25f, 0.25f);
            playRt.anchorMax = new Vector2(0.75f, 0.55f);
            playRt.offsetMin = Vector2.zero;
            playRt.offsetMax = Vector2.zero;

            // Small back button (top-left corner)
            backButton = UIBuilderHelper.MakeButton(canvas.transform, "BackButton",
                "Volver", UIBuilderHelper.ColBtnSmall, UIBuilderHelper.ColTextSecondary,
                16, TMPro.FontStyles.Normal);
            var backRt = backButton.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0, 1);
            backRt.anchorMax = new Vector2(0, 1);
            backRt.pivot = new Vector2(0, 1);
            backRt.sizeDelta = new Vector2(100, 36);
            backRt.anchoredPosition = new Vector2(10, -10);

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ReadyKioskManager] UI construida. Guard\u00e1 la escena.");
        }
#endif
    }
}
