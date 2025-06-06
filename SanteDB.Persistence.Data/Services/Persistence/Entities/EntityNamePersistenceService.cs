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
using System.Linq;

namespace SanteDB.Persistence.Data.Services.Persistence.Entities
{
    /// <summary>
    /// A persistence service which operates on <see cref="EntityAddress"/>
    /// </summary>
    public class EntityNamePersistenceService : EntityAssociationPersistenceService<EntityName, DbEntityName>
    {
        /// <summary>
        /// Dependency injection ctor
        /// </summary>
        public EntityNamePersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <summary>
        /// Prepare referenced objects
        /// </summary>
        protected override EntityName BeforePersisting(DataContext context, EntityName data)
        {
            data.NameUseKey = this.EnsureExists(context, data.NameUse)?.Key ?? data.NameUseKey;
            return base.BeforePersisting(context, data);
        }

        /// <summary>
        /// Perform an insert with the nested components
        /// </summary>
        protected override EntityName DoInsertModel(DataContext context, EntityName data)
        {
            var retVal = base.DoInsertModel(context, data);

            if (data.Component != null)
            {
                retVal.Component = this.UpdateModelAssociations(context, retVal, data.Component).ToList();
                retVal.SetLoaded(o => o.Component);
            }

            return retVal;
        }

        /// <summary>
        /// Update model
        /// </summary>
        protected override EntityName DoUpdateModel(DataContext context, EntityName data)
        {
            var retVal = base.DoUpdateModel(context, data);

            if (data.Component != null)
            {
                retVal.Component = this.UpdateModelAssociations(context, retVal, data.Component).ToList();
                retVal.SetLoaded(o => o.Component);
            }

            return retVal;
        }

        /// <summary>
        /// Convert back to information model
        /// </summary>
        protected override EntityName DoConvertToInformationModel(DataContext context, DbEntityName dbModel, params Object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);
            switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                    retVal.NameUse = retVal.NameUse.GetRelatedPersistenceService().Get(context, dbModel.UseConceptKey);
                    retVal.SetLoaded(nameof(EntityName.NameUse));
                    goto case LoadMode.SyncLoad;
                case LoadMode.SyncLoad:
                    retVal.Component = retVal.Component.GetRelatedPersistenceService().Query(context, o => o.SourceEntityKey == dbModel.Key).OrderBy(o => o.OrderSequence).ToList();
                    retVal.SetLoaded(nameof(EntityName.Component));
                    break;
            }
            return retVal;
        }
    }
}