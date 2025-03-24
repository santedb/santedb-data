using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Extensibility;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Services.Persistence.DataTypes
{
    /// <summary>
    /// Persistence service that stores concept extensions
    /// </summary>
    public class ConceptExtensionPersistenceService : ConceptReferencePersistenceBase<ConceptExtension, DbConceptExtension>
    {
        /// <inheritdoc/>
        public ConceptExtensionPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }
        /// <inheritdoc/>
        protected override ConceptExtension BeforePersisting(DataContext context, ConceptExtension data)
        {
            if (!data.ExtensionTypeKey.HasValue && data.ExtensionType != null && this.TryGetKeyResolver<ExtensionType>(out var resolver))
            {
                data.ExtensionType = data.ExtensionType.GetRelatedPersistenceService().Query(context, resolver.GetKeyExpression(data.ExtensionType)).First();
                data.ExtensionTypeKey = data.ExtensionType.Key;
            }
            return base.BeforePersisting(context, data);
        }
    }
}
