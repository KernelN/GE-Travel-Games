using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GETravelGames.TapGallery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// One-shot Editor utility that creates the TapGallery tutorial scene and wires it
/// into the Build Settings at index 1 (replacing the disabled Snake placeholder).
///
/// Usage: GE Travel Games ▸ Create Tutorial Scene
/// After running, open the scene, select TutorialManager, and assign:
///   • runnerPrefab / walkerPrefab  (Tap Runner Variant / Tap Walker Variant)
///   • tutorialSfx
/// Then save the scene.
/// </summary>
public static class TapGalleryTutorialSetup
{
    const string ScenePath   = "Assets/_TapGallery/_Scenes/TapGalleryTutorial.unity";
    const string SnakePath   = "Assets/_SnakeAirlines/Scenes/Snake.unity";
    const string MenuPath    = "GE Travel Games/Create Tutorial Scene";

    [MenuItem(MenuPath)]
    static void CreateTutorialScene()
    {
        // ── 1. Create a fresh scene ───────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── 2. Camera ─────────────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic    = true;
        cam.orthographicSize = 3f;
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.11f, 0.14f, 0.19f); // matches UIBuilderHelper.ColBg
        cam.nearClipPlane   = 0.3f;
        cam.farClipPlane    = 1000f;
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        // ── 3. EventSystem ────────────────────────────────────────────────────
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<InputSystemUIInputModule>();

        // ── 4. TutorialManager GameObject ─────────────────────────────────────
        var managerGo   = new GameObject("TutorialManager");
        var manager     = managerGo.AddComponent<TapGalleryTutorialManager>();
        managerGo.AddComponent<AudioSource>();

        // ── 5. Build UI via [ContextMenu] method ─────────────────────────────
        MethodInfo buildUi = typeof(TapGalleryTutorialManager)
            .GetMethod("BuildUi", BindingFlags.NonPublic | BindingFlags.Instance);

        if (buildUi != null)
            buildUi.Invoke(manager, null);
        else
            Debug.LogWarning("[TapGalleryTutorialSetup] BuildUi method not found — run 'Construir UI' manually.");

        // ── 6. Save scene ─────────────────────────────────────────────────────
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (!saved)
        {
            Debug.LogError($"[TapGalleryTutorialSetup] Failed to save scene to {ScenePath}.");
            return;
        }

        AssetDatabase.Refresh();

        // ── 7. Update Build Settings ──────────────────────────────────────────
        string tutorialGuid = AssetDatabase.AssetPathToGUID(ScenePath);
        if (string.IsNullOrEmpty(tutorialGuid))
        {
            Debug.LogError("[TapGalleryTutorialSetup] Could not retrieve GUID for the saved scene.");
            return;
        }

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // Replace the disabled Snake entry at index 1 if it exists; otherwise insert.
        int snakeIndex = scenes.FindIndex(s =>
            s.path == SnakePath || (!s.enabled && s.path.Contains("Snake")));

        var tutorialEntry = new EditorBuildSettingsScene(ScenePath, true);

        if (snakeIndex >= 0)
            scenes[snakeIndex] = tutorialEntry;
        else
            scenes.Insert(1, tutorialEntry); // insert after MainMenu if Snake slot is gone

        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log($"[TapGalleryTutorialSetup] Done! Tutorial scene is at build index " +
                  $"{scenes.IndexOf(tutorialEntry)}. Assign runnerPrefab, walkerPrefab, and tutorialSfx in the Inspector.");

        // Open the new scene for the developer
        EditorSceneManager.OpenScene(ScenePath);
    }
}
