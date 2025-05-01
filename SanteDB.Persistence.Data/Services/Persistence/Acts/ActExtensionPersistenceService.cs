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

namespace SanteDB.Persistence.Data.Services.Persistence.Acts
{
    /// <summary>
    /// Entity extension persistence service
    /// </summary>
    public class ActExtensionPersistenceService : ActAssociationPersistenceService<ActExtension, DbActExtension>
    {
        /// <summary>
        /// Creates a DI injected service header
        /// </summary>
        public ActExtensionPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override ActExtension BeforePersisting(DataContext context, ActExtension data)
        {
            if (!data.ExtensionTypeKey.HasValue && data.ExtensionType != null && this.TryGetKeyResolver<ExtensionType>(out var resolver))
            {
                data.ExtensionType = data.ExtensionType.GetRelatedPersistenceService().Query(context, resolver.GetKeyExpression(data.ExtensionType)).First();
                data.ExtensionTypeKey = data.ExtensionType.Key;
            }
            return base.BeforePersisting(context, data);
        }
    }
}