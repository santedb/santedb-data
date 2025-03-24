using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SanteDB.Persistence.Data.Services.Persistence.DataTypes
{
    /// <summary>
    /// Concept tag persistence
    /// </summary>
    public class ConceptTagPersistenceService : BaseEntityDataPersistenceService<ConceptTag, DbConceptTag>, IAdoKeyResolver<ConceptTag>, IAdoKeyResolver<DbConceptTag>
    {
        /// <inheritdoc/>
        public ConceptTagPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }


        /// <inheritdoc/>
        public Expression<Func<DbConceptTag, bool>> GetKeyExpression(DbConceptTag model) => o => o.SourceKey == model.SourceKey && o.TagKey == model.TagKey && o.ObsoletionTime == null;

        /// <inheritdoc/>
        public Expression<Func<ConceptTag, bool>> GetKeyExpression(ConceptTag model) => o => o.SourceEntityKey == model.SourceEntityKey && o.TagKey == model.TagKey && o.ObsoletionTime == null;

        /// <inheritdoc/>
        protected override DbConceptTag DoInsertInternal(DataContext context, DbConceptTag dbModel)
        {
            if (dbModel.TagKey.StartsWith("$"))
            {
                return dbModel;
            }
            else
            {
                return base.DoInsertInternal(context, dbModel);
            }
        }

    }
}
