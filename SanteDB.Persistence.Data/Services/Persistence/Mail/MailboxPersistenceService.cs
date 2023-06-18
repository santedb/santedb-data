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
using SanteDB.Core.Mail;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Mail;
using System.Linq;

namespace SanteDB.Persistence.Data.Services.Persistence.Mail
{
    /// <summary>
    /// Persistence service that can persist and handle mailboxes
    /// </summary>
    public class MailboxPersistenceService : BaseEntityDataPersistenceService<Mailbox, DbMailbox>
    {
        /// <inheritdoc/>
        public MailboxPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override Mailbox BeforePersisting(DataContext context, Mailbox data)
        {
            data.OwnerKey = this.EnsureExists(context, data.Owner)?.Key ?? data.OwnerKey;
            return base.BeforePersisting(context, data);
        }

        /// <inheritdoc/>
        protected override Mailbox DoConvertToInformationModel(DataContext context, DbMailbox dbModel, params object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);

            switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                    retVal.Owner = retVal.Owner.GetRelatedPersistenceService().Get(context, dbModel.OwnerKey);
                    retVal.SetLoaded(o => o.Owner);
                    goto case LoadMode.SyncLoad;
                case LoadMode.SyncLoad:
                    retVal.Messages = retVal.Messages.GetRelatedPersistenceService().Query(context, o => o.SourceEntityKey == dbModel.Key).ToList();
                    retVal.SetLoaded(o => o.Messages);
                    break;
            }
            return retVal;
        }
    }
}
