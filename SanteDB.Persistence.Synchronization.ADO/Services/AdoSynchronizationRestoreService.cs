using SanteDB.Core.Services;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Services;
using SanteDB.Persistence.PubSub.ADO.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Synchronization.ADO.Services
{
    /// <summary>
    /// Backup provider for backup service
    /// </summary>
    public class AdoSynchronizationRestoreService : OrmBackupRestoreServiceBase<AdoPubSubConfigurationSection>
    {


        public AdoSynchronizationRestoreService(IConfigurationManager configurationManager) : base(configurationManager, Constants.SYNC_DATABASE_ASSET_ID)
        {
        }
    }
}
