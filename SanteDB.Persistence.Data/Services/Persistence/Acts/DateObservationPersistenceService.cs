/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Persistence.Data.Model.Entities;
using System;
using System.Linq;

namespace SanteDB.Persistence.Data.Services.Persistence.Acts
{
    /// <summary>
    /// An observation persistence service which can manage observations which are quantities (value + unit)
    /// </summary>
    public class DateObservationPersistenceService : ObservationDerivedPersistenceService<DateObservation, DbDateObservation>
    {
        /// <summary>
        /// DI constructor
        /// </summary>
        public DateObservationPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override DateObservation DoConvertToInformationModelEx(DataContext context, DbActVersion dbModel, params object[] referenceObjects)
        {
            using (context.CreateInformationModelGuard(dbModel.Key))
            {

                var retVal = base.DoConvertToInformationModelEx(context, dbModel, referenceObjects);
                var obsData = referenceObjects?.OfType<DbDateObservation>().FirstOrDefault();
                if (obsData == null)
                {
                    this.m_tracer.TraceWarning("Using slow loading of observation data");
                    obsData = context.FirstOrDefault<DbDateObservation>(o => o.ParentKey == dbModel.VersionKey);
                }

                retVal.CopyObjectData(this.m_modelMapper.MapDomainInstance<DbDateObservation, DateObservation>(obsData), false, declaredOnly: true);
                return retVal;
            }
        }
    }
}
