using SanteDB.Core.Services;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.PubSub.ADO.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.PubSub.ADO
{
    /// <summary>
    /// Pubsub backup service
    /// </summary>
    public class PubSubBackupService : OrmBackupRestoreServiceBase<AdoPubSubConfigurationSection>
    {
        /// <summary>
        /// Backup asset
        /// </summary>
        public static readonly Guid PUB_SUB_BACKUP_ASSET_ID = Guid.Parse("FA934227-659A-43B0-80C1-4FAB5FA92240");

        /// <summary>
        /// DI Ctor
        /// </summary>
        public PubSubBackupService(IConfigurationManager configurationManager) : base(configurationManager, PUB_SUB_BACKUP_ASSET_ID)
        {
        }
    }
}
