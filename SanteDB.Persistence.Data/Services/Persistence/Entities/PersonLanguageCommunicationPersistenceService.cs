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
using SanteDB.Persistence.Data.Model.Entities;
using System;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence.Entities
{
    /// <summary>
    /// Persistence service for language of communication
    /// </summary>
    public class PersonLanguageCommunicationPersistenceService : EntityAssociationPersistenceService<PersonLanguageCommunication, DbPersonLanguageCommunication>,
        IAdoKeyResolver<PersonLanguageCommunication>, IAdoKeyResolver<DbPersonLanguageCommunication>
    {
        /// <inheritdoc/>
        public PersonLanguageCommunicationPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        public Expression<Func<PersonLanguageCommunication, bool>> GetKeyExpression(PersonLanguageCommunication model) => o => o.LanguageCode == model.LanguageCode && o.SourceEntityKey == model.SourceEntityKey && o.ObsoleteVersionSequenceId == null;

        /// <inheritdoc/>
        public Expression<Func<DbPersonLanguageCommunication, bool>> GetKeyExpression(DbPersonLanguageCommunication model) => o => o.LanguageCode == model.LanguageCode && o.SourceKey == model.SourceKey && o.ObsoleteVersionSequenceId == null;
    }
}
