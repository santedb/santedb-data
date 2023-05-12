using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Concepts;

namespace SanteDB.Persistence.Data.Services.Persistence.DataTypes
{
    /// <summary>
    /// Concept set composition persistence
    /// </summary>
    public class ConceptSetCompositionPersistenceService : IdentifiedDataPersistenceService<ConceptSetComposition, DbConceptSetComposition>
    {
        /// <summary>
        /// DI constructor
        /// </summary>
        public ConceptSetCompositionPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }


        /// <inheritdoc/>
        protected override ConceptSetComposition BeforePersisting(DataContext context, ConceptSetComposition data)
        {
            if (data.Operation == 0) { data.Operation = ConceptSetCompositionOperation.Include; }
            data.TargetKey = this.EnsureExists(context, data.Target)?.Key ?? data.TargetKey;
            return base.BeforePersisting(context, data);
        }


        /// <inheritdoc/>
        protected override ConceptSetComposition DoConvertToInformationModel(DataContext context, DbConceptSetComposition dbModel, params object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);
            switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                    retVal.Target = retVal.Target.GetRelatedPersistenceService().Get(context, retVal.TargetKey.GetValueOrDefault());
                    break;
            }
            return retVal;
        }
    }
}
