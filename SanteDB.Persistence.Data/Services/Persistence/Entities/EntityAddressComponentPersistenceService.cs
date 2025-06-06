﻿/*
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
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Entities;
using System;

namespace SanteDB.Persistence.Data.Services.Persistence.Entities
{
    /// <summary>
    /// Entity address component persistence
    /// </summary>
    public class EntityAddressComponentPersistenceService : IdentifiedDataPersistenceService<EntityAddressComponent, DbEntityAddressComponent>
    {
        /// <summary>
        /// Entity address component DI injection
        /// </summary>
        public EntityAddressComponentPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <summary>
        /// Prepare references
        /// </summary>
        protected override EntityAddressComponent BeforePersisting(DataContext context, EntityAddressComponent data)
        {
            data.ComponentTypeKey = this.EnsureExists(context, data.ComponentType)?.Key ?? data.ComponentTypeKey;
            return base.BeforePersisting(context, data);
        }

        /// <summary>
        /// Convert to information model
        /// </summary>
        protected override EntityAddressComponent DoConvertToInformationModel(DataContext context, DbEntityAddressComponent dbModel, params Object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);

            switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                    retVal.ComponentType = retVal.ComponentType.GetRelatedPersistenceService().Get(context, dbModel.ComponentTypeKey.GetValueOrDefault());
                    retVal.SetLoaded(nameof(EntityAddressComponent.ComponentType));
                    break;
            }

            return retVal;
        }
    }
}