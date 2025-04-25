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
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Extensibility;
using System;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence.Entities
{
    /// <summary>
    /// Entity tag persistence
    /// </summary>
    public class EntityTagPersistenceService : BaseEntityDataPersistenceService<EntityTag, DbEntityTag>,
        IAdoKeyResolver<EntityTag>, IAdoKeyResolver<DbEntityTag>
    {
        /// <inheritdoc/>
        public EntityTagPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        public Expression<Func<EntityTag, bool>> GetKeyExpression(EntityTag model) => o => o.SourceEntityKey == model.SourceEntityKey && o.TagKey == model.TagKey && o.ObsoletionTime == null;

        /// <inheritdoc/>
        public Expression<Func<DbEntityTag, bool>> GetKeyExpression(DbEntityTag model) => o => o.SourceKey == model.SourceKey && o.TagKey == model.TagKey && o.ObsoletionTime == null;

        /// <inheritdoc/>
        protected override DbEntityTag DoInsertInternal(DataContext context, DbEntityTag dbModel)
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