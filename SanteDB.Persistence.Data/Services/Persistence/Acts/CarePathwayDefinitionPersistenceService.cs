using SanteDB.Core.Model.Acts;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Acts;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Services.Persistence.Acts
{
    /// <summary>
    /// Persistence service for care pathway
    /// </summary>
    public class CarePathwayDefinitionPersistenceService : NonVersionedDataPersistenceService<CarePathwayDefinition, DbCarePathwayDefinition>
    {
        /// <inheritdoc/>
        public CarePathwayDefinitionPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override CarePathwayDefinition BeforePersisting(DataContext context, CarePathwayDefinition data)
        {
            data.TemplateKey = this.EnsureExists(context, data.Template)?.Key ?? data.TemplateKey;
            return base.BeforePersisting(context, data);
        }

        /// <inheritdoc/>
        protected override CarePathwayDefinition DoConvertToInformationModel(DataContext context, DbCarePathwayDefinition dbModel, params object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);

            switch(DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                    retVal.Template = retVal.Template.GetRelatedPersistenceService().Get(context, retVal.TemplateKey.GetValueOrDefault());
                    retVal.SetLoaded(o => o.Template);
                    break;
            }

            return retVal;
        }

    }
}
