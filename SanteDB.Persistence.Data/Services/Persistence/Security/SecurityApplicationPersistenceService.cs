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
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Entities;
using SanteDB.Persistence.Data.Model.Security;
using System;

namespace SanteDB.Persistence.Data.Services.Persistence.Security
{
    /// <summary>
    /// A persistence service that handles security applications
    /// </summary>
    public class SecurityApplicationPersistenceService : NonVersionedDataPersistenceService<SecurityApplication, DbSecurityApplication>
    {
        /// <summary>
        /// Security application persistence DI constructor
        /// </summary>
        public SecurityApplicationPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override void DoDeleteReferencesInternal(DataContext context, Guid key)
        {
            // is there a CDR user entity which points to this? 
            foreach (var cdrUe in context.Query<DbApplicationEntity>(o => o.SecurityApplicationKey == key))
            {
                cdrUe.SecurityApplicationKey = Guid.Empty;
                context.Update(cdrUe);
            }

            context.DeleteAll<DbApplicationClaim>(o => o.SourceKey == key);
            base.DoDeleteReferencesInternal(context, key);
        }

        /// <summary>
        /// Before persisting the object
        /// </summary>
        protected override SecurityApplication BeforePersisting(DataContext context, SecurityApplication data)
        {
            if (!String.IsNullOrEmpty(data.ApplicationSecret) && context.ContextId.ToString() != AuthenticationContext.SystemUserSid)
            {
                this.m_tracer.TraceWarning("Caller has set ApplicationSecret on the SecurityApplication instance - this will be ignored");
                data.ApplicationSecret = null;
            }
            return base.BeforePersisting(context, data);
        }

        /// <summary>
        /// After being persisted
        /// </summary>
        protected override SecurityApplication AfterPersisted(DataContext context, SecurityApplication data)
        {
            data.ApplicationSecret = null;
            return base.AfterPersisted(context, data);
        }
    }
}