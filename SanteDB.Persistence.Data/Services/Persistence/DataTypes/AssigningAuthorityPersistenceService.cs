/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-5-19
 */
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.DataType;
using System;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence.DataTypes
{
    /// <summary>
    /// Assigning authority persistence
    /// </summary>
    public class AssigningAuthorityPersistenceService : BaseEntityDataPersistenceService<AssigningAuthority, DbAssigningAuthority>,
        IAdoKeyResolver<AssigningAuthority>, IAdoKeyResolver<DbAssigningAuthority>
    {
        /// <inheritdoc/>
        public AssigningAuthorityPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        public Expression<Func<DbAssigningAuthority, bool>> GetKeyExpression(DbAssigningAuthority model) => o => o.SourceKey == model.SourceKey && o.AssigningApplicationKey == model.AssigningApplicationKey && o.ObsoletionTime == null;

        /// <inheritdoc/>
        public Expression<Func<AssigningAuthority, bool>> GetKeyExpression(AssigningAuthority model) => o => o.SourceEntityKey == model.SourceEntityKey && o.AssigningApplicationKey == model.AssigningApplicationKey && o.ObsoletionTime == null;

        /// <inheritdoc/>
        protected override AssigningAuthority BeforePersisting(DataContext context, AssigningAuthority data)
        {
            data.SourceEntityKey = this.EnsureExists(context, data.SourceEntity)?.Key ?? data.SourceEntityKey;
            return base.BeforePersisting(context, data);
        }

        /// <inheritdoc/>
        protected override AssigningAuthority DoConvertToInformationModel(DataContext context, DbAssigningAuthority dbModel, params object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);

            if ((DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy) == LoadMode.FullLoad)
            {
                retVal.AssigningApplication = retVal.AssigningApplication.GetRelatedPersistenceService().Get(context, dbModel.AssigningApplicationKey);
                retVal.SetLoaded(o => o.AssigningApplication);
            }

            return retVal;
        }
    }
}
