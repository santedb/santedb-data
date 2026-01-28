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
 * Date: 2024-12-12
 */
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
            using (context.CreateInformationModelGuard(dbModel.Key))
            {

                var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);

                switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
                {
                    case LoadMode.FullLoad:
                        if (context.ValidateMaximumStackDepth())
                        {
                            retVal.Template = retVal.Template.GetRelatedPersistenceService().Get(context, retVal.TemplateKey.GetValueOrDefault());
                            retVal.SetLoaded(o => o.Template);
                        }
                        break;
                }

                return retVal;
            }
           
        }

    }
}
