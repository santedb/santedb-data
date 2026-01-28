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
using SanteDB.Core.Model.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Entities;
using SanteDB.Persistence.Data.Model.Security;
using System;

namespace SanteDB.Persistence.Data.Services.Persistence.Security
{
    /// <summary>
    /// Persistence service that works with SecurityApplication objects
    /// </summary>
    public class SecurityUserPersistenceService : NonVersionedDataPersistenceService<SecurityUser, DbSecurityUser>
    {
        /// <summary>
        /// DI injected constructor
        /// </summary>
        public SecurityUserPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override void DoDeleteReferencesInternal(DataContext context, Guid key)
        {
            // is there a CDR user entity which points to this? 
            foreach (var cdrUe in context.Query<DbUserEntity>(o => o.SecurityUserKey == key))
            {
                cdrUe.SecurityUserKey = Guid.Empty;
                context.Update(cdrUe);
            }

            context.DeleteAll<DbUserClaim>(o => o.SourceKey == key);
            base.DoDeleteReferencesInternal(context, key);
        }

        /// <inheritdoc/>
        protected override SecurityUser BeforePersisting(DataContext context, SecurityUser data)
        {
            if (!String.IsNullOrEmpty(data.Password))
            {
                this.m_tracer.TraceWarning("Caller has set the Password property on SecurityUser instance. Use the IIdentityProvider.ChangePassword() method - this property will be ignored here");
                data.Password = null;
            }
            if (!String.IsNullOrEmpty(data.SecurityHash))
            {
                this.m_tracer.TraceWarning("Caller has set the SecurityHash property on SecurityUser instance - this property is for internal use and the setting of this property will be ignored here");
                data.SecurityHash = null;
            }

            return base.BeforePersisting(context, data);
        }

        /// <inheritdoc/>
        protected override SecurityUser AfterPersisted(DataContext context, SecurityUser data)
        {
            data.Password = null;
            return base.AfterPersisted(context, data);
        }
    }
}