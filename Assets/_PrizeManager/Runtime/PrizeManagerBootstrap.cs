using UnityEngine;

namespace GETravelGames.PrizeManager
{
    [DisallowMultipleComponent]
    public sealed class PrizeManagerBootstrap : MonoBehaviour
    {
        [SerializeField] private PrizeManagerBootstrapState state = new();

        [Tooltip("Link via 'Link Admin Canvas' context menu. Must be a child Canvas.")]
        [SerializeField] private Canvas adminCanvas;

        [Tooltip("Linked automatically alongside adminCanvas.")]
        [SerializeField] private PrizeAdminApp adminApp;

        private PrizeAdminService adminService;
        private PrizeAdminStateStore stateStore;
        private PrizeCsvService csvService;

        public PrizeManagerBootstrapState State  => state;
        public PrizeAdminService AdminService    => adminService;

        private void Awake()
        {
            state.EnsureDefaults(Application.dataPath);

            csvService   = new PrizeCsvService();
            stateStore   = new PrizeAdminStateStore();
            adminService = new PrizeAdminService(csvService, stateStore);

            if (adminApp == null)
                adminApp = GetComponent<PrizeAdminApp>()
                        ?? gameObject.AddComponent<PrizeAdminApp>();

            adminApp.Initialize(adminService, state);
        }

#if UNITY_EDITOR
        [ContextMenu("Link Admin Canvas")]
        private void EditorLinkAdminCanvas()
        {
            var foundCanvas = GetComponentInChildren<Canvas>(includeInactive: true);
            if (foundCanvas == null)
            {
                Debug.LogWarning(
                    "[PrizeManagerBootstrap] No Canvas found in children.\n" +
                    "Enter Play mode once so PrizeAdminApp can build the canvas hierarchy, " +
                    "exit Play mode, then run this context menu again.", this);
                return;
            }

            var foundApp = GetComponent<PrizeAdminApp>()
                        ?? GetComponentInChildren<PrizeAdminApp>(includeInactive: true);
            if (foundApp == null)
            {
                Debug.LogWarning("[PrizeManagerBootstrap] No PrizeAdminApp found.", this);
                return;
            }

            UnityEditor.Undo.RecordObject(this, "Link Admin Canvas");
            adminCanvas = foundCanvas;
            adminApp    = foundApp;
            UnityEditor.EditorUtility.SetDirty(this);

            Debug.Log(
                $"[PrizeManagerBootstrap] Linked canvas '{adminCanvas.name}' and PrizeAdminApp.\n" +
                "Next: right-click PrizeAdminApp → 'Link Canvas References'.", this);
        }
#endif
    }
}
