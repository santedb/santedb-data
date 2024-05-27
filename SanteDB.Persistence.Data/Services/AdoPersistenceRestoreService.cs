using SanteDB.Core.Data.Backup;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Service for backing up and restoring 
    /// </summary>
    public class AdoPersistenceRestoreService : OrmBackupRestoreServiceBase<AdoPersistenceConfigurationSection>
    {
        // Primary database asset

        /// <inheritdoc/>
        public AdoPersistenceRestoreService(IConfigurationManager configurationManager) : base(configurationManager, DataConstants.PRIMARY_DATABASE_ASSET_ID)
        {
        }


    }
}
