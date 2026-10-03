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
    /// Represents a persistence service where value is a URI with supporting data
    /// </summary>
    public class UriObservationPersistenceService : ObservationDerivedPersistenceService<UriObservation, DbUriObservation>
    {
        public UriObservationPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        protected override UriObservation DoConvertToInformationModelEx(DataContext context, DbActVersion dbModel, params object[] referenceObjects)
        {
            using (context.CreateInformationModelGuard(dbModel.Key))
            {

                var retVal = base.DoConvertToInformationModelEx(context, dbModel, referenceObjects);
                var obsData = referenceObjects?.OfType<DbUriObservation>().FirstOrDefault();
                if (obsData == null)
                {
                    this.m_tracer.TraceWarning("Performing slow load of uri observation data from database");
                    obsData = context.FirstOrDefault<DbUriObservation>(o => o.ParentKey == dbModel.VersionKey);
                }

                if ((DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy) == LoadMode.FullLoad && context.ValidateMaximumStackDepth())
                {
                    retVal.ContentClass = retVal.ContentClass.GetRelatedMappingProvider().Get(context, obsData?.ContentClassKey ?? Guid.Empty);
                    retVal.SetLoaded(o => o.ContentClass);
                }

                retVal.Value = obsData?.Value;
                retVal.Hash = obsData?.Hash;
                retVal.AvailabilityStartTime = obsData?.AvailabilityStartTime;
                retVal.AvailabilityStopTime = obsData?.AvailabilityStopTime;
                retVal.MimeType = obsData?.MimeType;

                return retVal;
            }
        }
    }
}
