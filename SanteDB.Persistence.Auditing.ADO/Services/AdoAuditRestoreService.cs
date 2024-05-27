using SanteDB.Core.Data.Backup;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Migration;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Auditing.ADO.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Auditing.ADO.Services
{
    /// <summary>
    /// Restore and provide backup assets
    /// </summary>
    public class AdoAuditRestoreService : OrmBackupRestoreServiceBase<AdoAuditConfigurationSection>
    {


        /// <inheritdoc/>
        public AdoAuditRestoreService(IConfigurationManager configurationManager) : base(configurationManager, AuditConstants.AUDIT_ASSET_ID)
        {
            
        }
    }
}
