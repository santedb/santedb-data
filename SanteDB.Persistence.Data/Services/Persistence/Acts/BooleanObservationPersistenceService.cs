using SanteDB.Core.Model.Acts;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Acts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Persistence.Data.Services.Persistence.Acts
{
    /// <summary>
    /// Persistence service which stores boolean valued observations 
    /// </summary>
    public class BooleanObservationPersistenceService : ObservationDerivedPersistenceService<BooleanObservation, DbBooleanObservation>
    {
        public BooleanObservationPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override BooleanObservation DoConvertToInformationModelEx(DataContext context, DbActVersion dbModel, params object[] referenceObjects)
        {
            using (context.CreateInformationModelGuard(dbModel.Key))
            {

                var retVal = base.DoConvertToInformationModelEx(context, dbModel, referenceObjects);
                var obsData = referenceObjects?.OfType<DbBooleanObservation>().FirstOrDefault();
                if (obsData == null)
                {
                    this.m_tracer.TraceWarning("Using slow loading of observation data");
                    obsData = context.FirstOrDefault<DbBooleanObservation>(o => o.ParentKey == dbModel.VersionKey);
                }

                retVal.CopyObjectData(this.m_modelMapper.MapDomainInstance<DbBooleanObservation, BooleanObservation>(obsData), false, declaredOnly: true);
                return retVal;
            }
        }
    }
}
