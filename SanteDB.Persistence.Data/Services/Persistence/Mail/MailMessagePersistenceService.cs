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
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Mail;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Mail;
using System;
using System.Data;
using System.Linq;

namespace SanteDB.Persistence.Data.Services.Persistence.Mail
{
    /// <summary>
    /// Mail message persistence service which can handles the persistence of <see cref="MailMessage"/> with 
    /// <see cref="DbMailMessage"/>
    /// </summary>
    public class MailMessagePersistenceService : BaseEntityDataPersistenceService<MailMessage, DbMailMessage>
    {
        /// <inheritdoc/>
        public MailMessagePersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override MailMessage DoInsertModel(DataContext context, MailMessage data)
        {
            var retVal = base.DoInsertModel(context, data);

            // Persist the RCPT to
            if(data.RcptToXml?.Any() == true)
            {
                retVal.RcptToXml = context.InsertAll(data.RcptToXml.Select(o => new DbMailMessageRcptTo()
                {
                    SourceKey = retVal.Key.Value,
                    RecipientKey = o
                })).Select(o=>o.RecipientKey).ToList();
            }

            return retVal;
        }

        /// <inheritdoc/>
        /// <remarks>Not supported - once sent a mail message cannot be changed</remarks>
        protected override MailMessage DoUpdateModel(DataContext context, MailMessage data)
        {
            if (context.ShouldDisableObjectValidation().HasFlag(DataContextExtensions.DisablePersistenceValidationFlags.Exists))
            {
                this.m_tracer.TraceWarning("Mail message {0} cannot be updated since it already exists", data);
                return data;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        /// <inheritdoc/>
        protected override MailMessage DoConvertToInformationModel(DataContext context, DbMailMessage dbModel, params object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);
            retVal.RcptToXml = context.Query<DbMailMessageRcptTo>(o => o.SourceKey == dbModel.Key).Select(o => o.RecipientKey).ToList();
            retVal.SetLoaded(o => o.RcptToXml);
            return retVal;
        }
    }
}
