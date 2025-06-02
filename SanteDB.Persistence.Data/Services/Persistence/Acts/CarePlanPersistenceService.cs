/*
 * Copyright (C) 2021 - 2025, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
 * Copyright (C) 2019 - 2021, Fyfe Software Inc. and the SanteSuite Contributors
 * Portions Copyright (C) 2015-2018 Mohawk College of Applied Arts and Technology
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you 
 * may not use this file except in compliance with the License. You may 
 * obtain a copy of the License at 
 * 
 * http://www.apache.org/licenses/LICENSE-2.0 
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations under 
 * the License.
 * 
 * User: fyfej
 * Date: 2023-6-21
 */
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Acts;
using System.Linq;

namespace SanteDB.Persistence.Data.Services.Persistence.Acts
{
    /// <summary>
    /// Persistence service which stores care plans
    /// </summary>
    /// <remarks>
    /// The care plan storage has no specific storage requirements for care plans
    /// </remarks>
    public class CarePlanPersistenceService : ActDerivedPersistenceService<CarePlan, DbCarePlan>
    {
        /// <summary>
        /// DI constructor
        /// </summary>
        public CarePlanPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override CarePlan BeforePersisting(DataContext context, CarePlan data)
        {
            data.CarePathwayKey = this.EnsureExists(context, data.CarePathway)?.Key ?? data.CarePathwayKey;
            return base.BeforePersisting(context, data);
        }

        /// <inheritdoc/>
        protected override CarePlan DoConvertToInformationModelEx(DataContext context, DbActVersion dbModel, params object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModelEx(context, dbModel, referenceObjects);
            var dbCarePlan = referenceObjects?.OfType<DbCarePlan>().FirstOrDefault();
            if (dbCarePlan == null)
            {
                this.m_tracer.TraceWarning("Using slow loading of careplan data (hint: use the appropriate persistence API)");
                dbCarePlan = context.FirstOrDefault<DbCarePlan>(o => o.ParentKey == dbModel.VersionKey);
            }

            switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                    retVal.CarePathway = retVal.CarePathway.GetRelatedPersistenceService().Get(context, dbCarePlan.CarePathwayKey.GetValueOrDefault());
                    retVal.SetLoaded(o => o.CarePathway);
                    break;
            }
            retVal.CopyObjectData(this.m_modelMapper.MapDomainInstance<DbCarePlan, CarePlan>(dbCarePlan), declaredOnly: true);
            return retVal;
        }

    }
}
