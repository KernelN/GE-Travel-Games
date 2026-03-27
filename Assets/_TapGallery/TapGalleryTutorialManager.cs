using System.Collections;
using GETravelGames.Common;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GETravelGames.TapGallery
{
    /// <summary>
    /// Manages the TapGallery tutorial screen shown before the game.
    /// Displays a Runner (score) and a Walker (penalty) as visual examples,
    /// then auto-advances to TapGallery after a configurable countdown.
    /// </summary>
    public sealed class TapGalleryTutorialManager : MonoBehaviour
    {
        [Header("Tutorial Settings")]
        [SerializeField] float tutorialDuration = 3f;
        [SerializeField] AudioClip tutorialSfx;
        [SerializeField] AudioSource audioSource;

        [Header("Tappable Prefabs")]
        [SerializeField] Tappable runnerPrefab;
        [SerializeField] Tappable walkerPrefab;
        [SerializeField] string runnerSpritePath = "TapGallery/Tappables/Runner";
        [SerializeField] string walkerSpritePath = "TapGallery/Tappables/Walker";
        [SerializeField] Transform runnerSpawnPoint;
        [SerializeField] Transform walkerSpawnPoint;

        [Header("UI References")]
        [SerializeField] TMP_Text timerLabel;
        [SerializeField] TMP_Text runnerLabel;
        [SerializeField] TMP_Text walkerLabel;

        [Header("Text")]
        [SerializeField] string runnerText = "Atrapa a todos los brokers viajeros!";
        [SerializeField] string walkerText  = "No atrapes a Remax o vas a perder puntos!";
        [SerializeField] string timerFormat = "{0:0}";

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            Time.timeScale = 1f;

            SpawnCharacter(runnerPrefab, runnerSpritePath, runnerSpawnPoint);
            SpawnCharacter(walkerPrefab, walkerSpritePath, walkerSpawnPoint);

            if (runnerLabel != null) runnerLabel.text = runnerText;
            if (walkerLabel != null) walkerLabel.text  = walkerText;

            if (audioSource != null && tutorialSfx != null)
                audioSource.PlayOneShot(tutorialSfx);

            StartCoroutine(CountdownRoutine());
        }

        // ── Core logic ────────────────────────────────────────────────────────

        void SpawnCharacter(Tappable prefab, string spritePath, Transform spawnPoint)
        {
            if (prefab == null || spawnPoint == null) return;

            Tappable instance = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

            // Disable interaction — tutorial characters are display-only
            Collider2D col = instance.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            // Assign a random photo if sprites are available for this character type
            Sprite[] sprites = Resources.LoadAll<Sprite>(spritePath);
            if (sprites != null && sprites.Length > 0)
                instance.SetSprite(sprites[Random.Range(0, sprites.Length)]);
        }

        IEnumerator CountdownRoutine()
        {
            float timeRemaining = tutorialDuration;
            while (timeRemaining > 0f)
            {
                if (timerLabel != null)
                    timerLabel.text = string.Format(timerFormat, Mathf.Ceil(timeRemaining));
                timeRemaining -= Time.deltaTime;
                yield return null;
            }

            if (timerLabel != null)
                timerLabel.text = string.Format(timerFormat, 0f);

            SceneManager.LoadScene("TapGallery");
        }

        // ── Editor UI builder ─────────────────────────────────────────────────

#if UNITY_EDITOR
        [UnityEngine.ContextMenu("Construir UI")]
        void BuildUi()
        {
            var existing = GetComponentInChildren<UnityEngine.Canvas>(true);
            if (existing != null)
                UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);

            UnityEditor.Undo.RecordObject(this, "Construir UI TapGalleryTutorial");

            UIBuilderHelper.EnsureEventSystem();

            // ── Canvas ────────────────────────────────────────────────────────
            var canvas = UIBuilderHelper.MakeCanvas(transform, "TutorialCanvas", 100);
            canvas.gameObject.AddComponent<UnityEngine.UI.Image>().color = UIBuilderHelper.ColBg;

            // ── Title ─────────────────────────────────────────────────────────
            var title = UIBuilderHelper.MakeText(canvas.transform, "Title",
                28, TMPro.FontStyles.Bold, UIBuilderHelper.ColTextSecondary);
            title.text = "Como jugar";
            var titleRt = title.GetComponent<UnityEngine.RectTransform>();
            titleRt.anchorMin = new Vector2(0.05f, 0.82f);
            titleRt.anchorMax = new Vector2(0.95f, 0.97f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            // ── Runner row (upper half) ───────────────────────────────────────
            var runnerPanel = new UnityEngine.GameObject("RunnerPanel",
                typeof(UnityEngine.RectTransform));
            runnerPanel.transform.SetParent(canvas.transform, false);
            var runnerPanelRt = runnerPanel.GetComponent<UnityEngine.RectTransform>();
            runnerPanelRt.anchorMin = new Vector2(0.05f, 0.47f);
            runnerPanelRt.anchorMax = new Vector2(0.95f, 0.80f);
            runnerPanelRt.offsetMin = Vector2.zero;
            runnerPanelRt.offsetMax = Vector2.zero;

            runnerLabel = UIBuilderHelper.MakeText(runnerPanel.transform, "RunnerLabel",
                26, TMPro.FontStyles.Bold, UIBuilderHelper.ColTextPrimary,
                TMPro.TextAlignmentOptions.MidlineLeft);
            runnerLabel.text = runnerText;
            var runnerLabelRt = runnerLabel.GetComponent<UnityEngine.RectTransform>();
            runnerLabelRt.anchorMin = new Vector2(0.45f, 0f);
            runnerLabelRt.anchorMax = new Vector2(1f,    1f);
            runnerLabelRt.offsetMin = new Vector2(10f, 0f);
            runnerLabelRt.offsetMax = Vector2.zero;

            // ── Walker row (lower half) ───────────────────────────────────────
            var walkerPanel = new UnityEngine.GameObject("WalkerPanel",
                typeof(UnityEngine.RectTransform));
            walkerPanel.transform.SetParent(canvas.transform, false);
            var walkerPanelRt = walkerPanel.GetComponent<UnityEngine.RectTransform>();
            walkerPanelRt.anchorMin = new Vector2(0.05f, 0.12f);
            walkerPanelRt.anchorMax = new Vector2(0.95f, 0.45f);
            walkerPanelRt.offsetMin = Vector2.zero;
            walkerPanelRt.offsetMax = Vector2.zero;

            walkerLabel = UIBuilderHelper.MakeText(walkerPanel.transform, "WalkerLabel",
                26, TMPro.FontStyles.Bold, new UnityEngine.Color(0.85f, 0.25f, 0.25f),
                TMPro.TextAlignmentOptions.MidlineLeft);
            walkerLabel.text = walkerText;
            var walkerLabelRt = walkerLabel.GetComponent<UnityEngine.RectTransform>();
            walkerLabelRt.anchorMin = new Vector2(0.45f, 0f);
            walkerLabelRt.anchorMax = new Vector2(1f,    1f);
            walkerLabelRt.offsetMin = new Vector2(10f, 0f);
            walkerLabelRt.offsetMax = Vector2.zero;

            // ── Timer ─────────────────────────────────────────────────────────
            timerLabel = UIBuilderHelper.MakeText(canvas.transform, "TimerLabel",
                72, TMPro.FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            timerLabel.text = string.Format(timerFormat, tutorialDuration);
            var timerRt = timerLabel.GetComponent<UnityEngine.RectTransform>();
            timerRt.anchorMin = new Vector2(0.35f, 0.00f);
            timerRt.anchorMax = new Vector2(0.65f, 0.12f);
            timerRt.offsetMin = Vector2.zero;
            timerRt.offsetMax = Vector2.zero;

            // ── World-space spawn points ──────────────────────────────────────
            // These are plain GameObjects at fixed positions in world space.
            // Position the camera and these anchors to taste in the scene.
            if (runnerSpawnPoint == null)
            {
                var rsp = new UnityEngine.GameObject("RunnerSpawnPoint");
                UnityEditor.Undo.RegisterCreatedObjectUndo(rsp, "Create RunnerSpawnPoint");
                rsp.transform.position = new Vector3(-1.5f, 1.2f, 0f);
                runnerSpawnPoint = rsp.transform;
            }

            if (walkerSpawnPoint == null)
            {
                var wsp = new UnityEngine.GameObject("WalkerSpawnPoint");
                UnityEditor.Undo.RegisterCreatedObjectUndo(wsp, "Create WalkerSpawnPoint");
                wsp.transform.position = new Vector3(-1.5f, -1.0f, 0f);
                walkerSpawnPoint = wsp.transform;
            }

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEngine.Debug.Log("[TapGalleryTutorialManager] UI construida. Guarda la escena.");
        }
#endif
    }
}
