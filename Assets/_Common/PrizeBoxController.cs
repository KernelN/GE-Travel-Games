using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    /// <summary>
    /// Manages a single prize-box in the PrizeGiving scene.
    /// States: Closed → Revealed (after player clicks) or FadedOut (not chosen).
    /// </summary>
    public sealed class PrizeBoxController : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image boxImage;
        [SerializeField] Image lockOverlay;     // visible when box is locked (stage 0)
        [SerializeField] TMP_Text label;        // prize name, hidden until revealed
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] AnimationCurve popCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // Colours for the closed / revealed box states
        [SerializeField] Color closedColor  = new Color(0.25f, 0.55f, 1f);
        [SerializeField] Color revealedColor = new Color(1f, 0.82f, 0.18f);
        [SerializeField] Sprite questionMarkSprite;
        [SerializeField] Sprite openBoxSprite;

        public event Action<PrizeBoxController> OnBoxClicked;

        // ── Public API ────────────────────────────────────────────────────────

        /// <param name="isLocked">True for stage-0 boxes (false prize forced, no click).</param>
        public void Initialize(bool isLocked)
        {
            if (label != null) label.gameObject.SetActive(false);
            if (lockOverlay != null) lockOverlay.gameObject.SetActive(isLocked);
            if (boxImage != null)
            {
                boxImage.color = isLocked ? Color.gray : closedColor;
                if (questionMarkSprite != null) boxImage.sprite = questionMarkSprite;
            }

            if (button != null)
            {
                button.interactable = !isLocked;
                button.onClick.AddListener(() => OnBoxClicked?.Invoke(this));
            }
        }

        /// <summary>
        /// Flips the box open and pop-scales the prize label into view.
        /// Call this on the chosen box after all non-chosen boxes start fading.
        /// </summary>
        public IEnumerator Reveal(string text)
        {
            if (button != null) button.interactable = false;

            // Swap sprite / colour to "open" state.
            if (boxImage != null)
            {
                boxImage.color = revealedColor;
                if (openBoxSprite != null) boxImage.sprite = openBoxSprite;
            }

            if (label != null)
            {
                label.text = text;
                label.gameObject.SetActive(true);
                label.transform.localScale = Vector3.zero;

                float elapsed = 0f;
                const float duration = 0.35f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float s = popCurve.Evaluate(elapsed / duration);
                    label.transform.localScale = Vector3.one * s;
                    yield return null;
                }
                label.transform.localScale = Vector3.one;
            }
        }

        /// <summary>Fade this box out (used for unchosen boxes after a box is clicked).</summary>
        public IEnumerator FadeOut(float duration = 0.3f)
        {
            if (canvasGroup == null) { gameObject.SetActive(false); yield break; }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = 1f - elapsed / duration;
                yield return null;
            }
            gameObject.SetActive(false);
        }

        // ── Builder helper (called from PrizeGivingManager.SpawnBoxes at runtime) ──

        /// <summary>
        /// Programmatically constructs the box child hierarchy under this transform.
        /// Call after AddComponent on a fresh GameObject.
        /// </summary>
        public void BuildChildren(float size = 140f)
        {
            // CanvasGroup for fade
            canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            // Box background image + Button
            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(Button));
            boxGo.transform.SetParent(transform, false);
            var boxRect = boxGo.GetComponent<RectTransform>();
            boxRect.sizeDelta = new Vector2(size, size);
            boxImage = boxGo.GetComponent<Image>();
            boxImage.color = closedColor;
            button = boxGo.GetComponent<Button>();
            button.targetGraphic = boxImage;

            // Lock overlay (child of box)
            var lockGo = new GameObject("LockOverlay", typeof(RectTransform), typeof(Image));
            lockGo.transform.SetParent(boxGo.transform, false);
            var lockRect = lockGo.GetComponent<RectTransform>();
            lockRect.anchorMin = Vector2.zero;
            lockRect.anchorMax = Vector2.one;
            lockRect.offsetMin = lockRect.offsetMax = Vector2.zero;
            lockOverlay = lockGo.GetComponent<Image>();
            lockOverlay.color = new Color(0, 0, 0, 0.45f);
            lockGo.SetActive(false);

            // Label
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, -0.5f);
            labelRect.anchorMax = new Vector2(1f,  0f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize  = 20;
            label.color     = Color.white;
            labelGo.SetActive(false);

            // Default pop curve: 0→1.2→1
            popCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.7f, 1.2f),
                new Keyframe(1f,  1f));
        }
    }
}
