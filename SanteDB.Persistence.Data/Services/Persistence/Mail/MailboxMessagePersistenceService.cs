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
using SanteDB.Core.Mail;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Mail;

namespace SanteDB.Persistence.Data.Services.Persistence.Mail
{
    /// <summary>
    /// Represents a persistence service which can persist the assocation between a mail message and mailbox
    /// </summary>
    public class MailboxMessagePersistenceService : IdentifiedDataPersistenceService<MailboxMailMessage, DbMailboxMessageAssociation>
    {
        /// <inheritdoc/>
        public MailboxMessagePersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override MailboxMailMessage BeforePersisting(DataContext context, MailboxMailMessage data)
        {
            data.TargetKey = this.EnsureExists(context, data.Target)?.Key ?? data.TargetKey;
            return base.BeforePersisting(context, data);
        }

        /// <inheritdoc/>
        protected override MailboxMailMessage DoConvertToInformationModel(DataContext context, DbMailboxMessageAssociation dbModel, params object[] referenceObjects)
        {
            using (context.CreateInformationModelGuard(dbModel.Key))
            {
                var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);
                if ((DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy) == LoadMode.FullLoad && !context.ValidateMaximumStackDepth())
                {
                    retVal.Target = retVal.Target.GetRelatedPersistenceService().Get(context, dbModel.TargetKey);
                    retVal.SetLoaded(o => o.Target);
                }
                return retVal;
            }
        }
    }
}
