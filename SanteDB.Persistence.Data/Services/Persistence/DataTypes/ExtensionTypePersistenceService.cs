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
using SanteDB.Persistence.Data.Model.DataType;
using SanteDB.Persistence.Data.Model.Extensibility;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence.DataTypes
{
    /// <summary>
    /// Extension type persistence
    /// </summary>
    public class ExtensionTypePersistenceService : NonVersionedDataPersistenceService<ExtensionType, DbExtensionType>, IAdoKeyResolver<ExtensionType>, IAdoKeyResolver<DbExtensionType>
    {
        /// <summary>
        /// Creates a DI injected extension type
        /// </summary>
        public ExtensionTypePersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override void DoDeleteReferencesInternal(DataContext context, Guid key)
        {
            context.DeleteAll<DbExtensionTypeScope>(o => o.SourceKey == key);
            base.DoDeleteReferencesInternal(context, key);
        }

        /// <inheritdoc/>
        protected override ExtensionType DoInsertModel(DataContext context, ExtensionType data)
        {
            var retVal = base.DoInsertModel(context, data);

            if (data.ScopeXml?.Any() == true)
            {
                retVal.ScopeXml = base.UpdateInternalAssociations(context, retVal.Key.Value, data.ScopeXml.Select(o => new DbExtensionTypeScope()
                {
                    ClassCodeKey = o
                })).Select(o => o.ClassCodeKey).ToList();
            }
            return retVal;
        }

        /// <inheritdoc/>
        protected override ExtensionType DoUpdateModel(DataContext context, ExtensionType data)
        {
            var retVal = base.DoUpdateModel(context, data); // updates the core properties
            if (data.ScopeXml != null)
            {
                retVal.ScopeXml = base.UpdateInternalAssociations(context, retVal.Key.Value,
                    data.ScopeXml?.Select(o => new DbExtensionTypeScope()
                    {
                        ClassCodeKey = o
                    })).Select(o => o.ClassCodeKey).ToList();
            }

            return retVal;
        }

        /// <inheritdoc/>
        protected override ExtensionType DoConvertToInformationModel(DataContext context, DbExtensionType dbModel, params object[] referenceObjects)
        {
            using (context.CreateInformationModelGuard(dbModel.Key))
            {
                var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);
                retVal.ScopeXml = context.Query<DbExtensionTypeScope>(s => s.SourceKey == retVal.Key).Select(o => o.ClassCodeKey).ToList();
                return retVal;
            }
        }

        /// <inheritdoc/>
        public Expression<Func<ExtensionType, bool>> GetKeyExpression(ExtensionType model) => o => o.Uri == model.Uri && o.ObsoletionTime == null;

        /// <inheritdoc/>
        public Expression<Func<DbExtensionType, bool>> GetKeyExpression(DbExtensionType model) => o => o.Uri == model.Uri && o.ObsoletionTime == null;
    }
}