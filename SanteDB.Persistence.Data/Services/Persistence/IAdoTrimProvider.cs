using SanteDB.Core.Security.Audit;
using SanteDB.OrmLite;
using System;
using System.Collections.Generic;

namespace SanteDB.Persistence.Data.Services.Persistence
{
    /// <summary>
    /// Implementers of this class can trim old data from the database
    /// </summary>
    internal interface IAdoTrimProvider
    {

        /// <summary>
        /// Trim the specified object from the database 
        /// </summary>
        IEnumerable<KeyValuePair<Type, Guid>> Trim(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder);

    }
}
