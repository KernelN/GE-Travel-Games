using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    /// <summary>
    /// Drives the casino/Vampire-Survivors–style prize reveal animation.
    ///
    /// When a real prize is won the controller fires a series of particle burst
    /// explosions + screen flashes whose count and colours reflect how the prize
    /// "climbed" through levels during the multi-try roll. A false prize just
    /// gets a quiet text pop-in with no explosions.
    /// </summary>
    public sealed class PrizeCelebrationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ParticleSystem burstParticles;
        [SerializeField] Image screenFlashOverlay;  // full-screen Image, alpha=0 at start

        [Header("Audio")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip   burstSfx;     // played once per burst
        [SerializeField] AudioClip   fanfareSfx;   // played on final real-prize reveal

        [Header("Level colours (index 0 = escape/common, ascending = rarer)")]
        [SerializeField] Color[] levelColors = {
            new Color(0.40f, 0.70f, 1.00f),   // 0  blue   — escape / common
            new Color(0.30f, 0.90f, 0.45f),   // 1  green  — uncommon
            new Color(1.00f, 0.80f, 0.10f),   // 2  gold   — rare
            new Color(0.85f, 0.25f, 1.00f),   // 3  purple — epic
        };

        [Header("Timing")]
        [SerializeField] float delayBetweenBursts = 0.75f;

        [Header("Text pop curve  (0 → 1.2 → 1)")]
        [SerializeField] AnimationCurve textPopCurve = DefaultPopCurve();

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Moves the burst particle system to the given world position so bursts
        /// emanate from the selected box. Call before PlayCelebration.
        /// </summary>
        public void SetBurstOrigin(Vector3 worldPos)
        {
            if (burstParticles != null)
                burstParticles.transform.position = worldPos;
        }

        /// <summary>
        /// Plays the full celebration sequence for a won prize, then reveals the prize name.
        /// </summary>
        public IEnumerator PlayCelebration(
            PrizePullResult result,
            TMP_Text prizeLabel,
            TMP_Text titleLabel)
        {
            int burstCount = PrizePullResult.ComputeBurstCount(result.WinningLevel);
            var colors = BuildBurstColors(result.WinningLevel, burstCount);

            for (int i = 0; i < burstCount; i++)
            {
                bool isFinal = i == burstCount - 1;
                yield return PlayBurst(colors[i], isFinal);
                if (!isFinal)
                    yield return new WaitForSeconds(delayBetweenBursts);
            }

            string prizeName = result.PrizeName;
            if (string.IsNullOrWhiteSpace(prizeName)) prizeName = "Premio";

            if (audioSource != null && fanfareSfx != null)
                audioSource.PlayOneShot(fanfareSfx);

            if (titleLabel != null) titleLabel.text = $"\u00a1Ganaste {prizeName}!";
            yield return PopText(prizeLabel, prizeName);
        }

        /// <summary>
        /// Plays a minimal reveal for a false prize (quiet text pop, no explosions).
        /// </summary>
        public IEnumerator PlayFalsePrizeCelebration(
            TMP_Text prizeLabel,
            TMP_Text titleLabel,
            string message)
        {
            if (titleLabel != null) titleLabel.text = message;
            yield return PopText(prizeLabel, message);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        IEnumerator PlayBurst(Color color, bool isFinal)
        {
            if (burstParticles != null)
            {
                var main = burstParticles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    color,
                    new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 1f));
                burstParticles.transform.localScale = Vector3.one * (isFinal ? 2f : 1f);
                burstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                burstParticles.Play(true);
            }

            if (audioSource != null && burstSfx != null)
                audioSource.PlayOneShot(burstSfx);

            if (screenFlashOverlay != null)
            {
                float flashDur = isFinal ? 1.0f : 0.5f;
                screenFlashOverlay.color = new Color(color.r, color.g, color.b, 0.45f);
                screenFlashOverlay.CrossFadeAlpha(0f, flashDur, false);
                yield return new WaitForSeconds(flashDur);
            }
            else
            {
                yield return new WaitForSeconds(isFinal ? 1.0f : 0.5f);
            }
        }

        IEnumerator PopText(TMP_Text text, string value)
        {
            if (text == null) yield break;

            text.text = value;
            text.gameObject.SetActive(true);
            text.transform.localScale = Vector3.zero;

            float elapsed = 0f;
            const float duration = 0.45f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float s = textPopCurve.Evaluate(elapsed / duration);
                text.transform.localScale = Vector3.one * s;
                yield return null;
            }
            text.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Returns a list of colours for each burst — all the same colour for the
        /// winning prize level, matching the new level-based burst count.
        /// </summary>
        List<Color> BuildBurstColors(ushort winningLevel, int burstCount)
        {
            var result = new List<Color>(burstCount);
            var color = LevelColor(winningLevel);
            for (int i = 0; i < burstCount; i++)
                result.Add(color);
            return result;
        }

        Color LevelColor(ushort level)
        {
            if (levelColors == null || levelColors.Length == 0) return Color.white;
            return levelColors[Mathf.Clamp(level, 0, levelColors.Length - 1)];
        }

        static AnimationCurve DefaultPopCurve() =>
            new AnimationCurve(
                new Keyframe(0f,   0f),
                new Keyframe(0.65f, 1.2f),
                new Keyframe(1f,   1f));

        // ── Builder (called from PrizeGivingManager.BuildUi in editor) ──────────

        /// <summary>
        /// Creates child objects needed by this controller under the given parent transform.
        /// </summary>
        public void BuildChildren(Transform parent, Vector2 canvasSize)
        {
            // Full-screen flash overlay
            var flashGo = new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(parent, false);
            var flashRect = flashGo.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
            screenFlashOverlay = flashGo.GetComponent<Image>();
            screenFlashOverlay.color = new Color(1f, 1f, 1f, 0f);
            screenFlashOverlay.raycastTarget = false;

            // Burst particle system — created at scene root so it renders below the
            // Screen Space Overlay canvas but above the camera clear-colour background.
            var particleGo = new GameObject("BurstParticles");
            particleGo.transform.SetParent(null, false); // scene root, NOT canvas child
            particleGo.transform.position = Vector3.zero;
            burstParticles = particleGo.AddComponent<ParticleSystem>();

            var main = burstParticles.main;
            main.loop            = false;
            main.playOnAwake     = false;
            main.stopAction      = ParticleSystemStopAction.None;
            main.duration        = 1.0f;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(4f, 12f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.22f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.4f);
            main.maxParticles    = 120;
            main.startColor      = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.2f, 1f),
                new Color(1f, 0.40f, 0.1f, 1f));

            var emission = burstParticles.emission;
            emission.enabled      = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)80) });

            var shape = burstParticles.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.3f;

            var sol = burstParticles.sizeOverLifetime;
            sol.enabled = true;
            sol.size    = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var psr = particleGo.GetComponent<ParticleSystemRenderer>();
#if UNITY_EDITOR
            psr.material = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
#else
            psr.material = Resources.GetBuiltinResource<Material>("Default-Particle.mat");
#endif

            textPopCurve = DefaultPopCurve();

            // AudioSource for burst SFX and fanfare (assign clips in Inspector)
            if (audioSource == null)
            {
                audioSource = gameObject.GetComponent<AudioSource>()
                           ?? gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake  = false;
                audioSource.spatialBlend = 0f; // 2-D audio
            }
        }
    }
}
