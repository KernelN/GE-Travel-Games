using UnityEngine;

namespace GETravelGames.PrizeManager
{
    [DisallowMultipleComponent]
    public sealed class PrizeManagerBootstrap : MonoBehaviour
    {
        [SerializeField] private PrizeManagerBootstrapState state = new();

        private PrizeAdminService adminService;
        private PrizeAdminStateStore stateStore;
        private PrizeCsvService csvService;

        public PrizeManagerBootstrapState State => state;

        public PrizeAdminService AdminService => adminService;

        private void Awake()
        {
            state.EnsureDefaults(Application.dataPath);

            csvService = new PrizeCsvService();
            stateStore = new PrizeAdminStateStore();
            adminService = new PrizeAdminService(csvService, stateStore);

            var adminApp = GetComponent<PrizeAdminApp>();
            if (adminApp == null)
            {
                adminApp = gameObject.AddComponent<PrizeAdminApp>();
            }

            adminApp.Initialize(adminService, state);
        }
    }
}
