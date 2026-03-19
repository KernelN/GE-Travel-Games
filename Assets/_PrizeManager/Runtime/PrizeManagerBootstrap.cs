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
        [ContextMenu("Vincular canvas de administración")]
        private void EditorLinkAdminCanvas()
        {
            var foundCanvas = GetComponentInChildren<Canvas>(includeInactive: true);
            if (foundCanvas == null)
            {
                Debug.LogWarning(
                    "[PrizeManagerBootstrap] No se encontró ningún Canvas hijo.\n" +
                    "Entrá al modo Play una vez para que PrizeAdminApp construya la jerarquía del canvas, " +
                    "salí del modo Play y volvé a ejecutar este menú.", this);
                return;
            }

            var foundApp = GetComponent<PrizeAdminApp>()
                        ?? GetComponentInChildren<PrizeAdminApp>(includeInactive: true);
            if (foundApp == null)
            {
                Debug.LogWarning("[PrizeManagerBootstrap] No se encontró ningún PrizeAdminApp.", this);
                return;
            }

            UnityEditor.Undo.RecordObject(this, "Vincular canvas de administración");
            adminCanvas = foundCanvas;
            adminApp    = foundApp;
            UnityEditor.EditorUtility.SetDirty(this);

            Debug.Log(
                $"[PrizeManagerBootstrap] Canvas '{adminCanvas.name}' y PrizeAdminApp vinculados.\n" +
                "Siguiente: clic derecho en PrizeAdminApp → 'Vincular referencias del canvas'.", this);
        }
#endif
    }
}
