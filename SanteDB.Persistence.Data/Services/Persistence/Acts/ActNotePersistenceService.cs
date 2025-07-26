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
using SanteDB.Persistence.Data.Model.Entities;
using SanteDB.Persistence.Data.Model.Extensibility;
using SanteDB.Persistence.Data.Model.Security;
using System;

namespace SanteDB.Persistence.Data.Services.Persistence.Acts
{
    /// <summary>
    /// Persistence service for act notes
    /// </summary>
    public class ActNotePersistenceService : ActAssociationPersistenceService<ActNote, DbActNote>
    {
        /// <summary>
        /// Note persistence service DI constructor
        /// </summary>
        public ActNotePersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <summary>
        /// Prepare referneces on the object
        /// </summary>
        protected override ActNote BeforePersisting(DataContext context, ActNote data)
        {
            data.AuthorKey = this.EnsureExists(context, data.Author)?.Key ?? data.AuthorKey;

            if (!data.AuthorKey.HasValue && context.Data.TryGetValue("provenance", out var prov) && prov is DbSecurityProvenance dbProv)
            {
                var userEntityStmt = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbUserEntity), typeof(DbEntityVersion))
                        .InnerJoin<DbUserEntity, DbEntityVersion>(o => o.ParentKey, o => o.VersionKey)
                        .Where<DbUserEntity>(o => o.SecurityUserKey == dbProv.UserKey);
                var userEntityKey = context.Query<DbEntityVersion>(userEntityStmt.Statement).Select(o => o.Key).FirstOrDefault();
                data.AuthorKey = userEntityKey;
            }
            return base.BeforePersisting(context, data);
        }

        /// <summary>
        /// Perform conversion to information model
        /// </summary>
        protected override ActNote DoConvertToInformationModel(DataContext context, DbActNote dbModel, params Object[] referenceObjects)
        {
            using (context.CreateInformationModelGuard(dbModel.Key))
            {

                var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);
                switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
                {
                    case LoadMode.FullLoad:
                        if (context.ValidateMaximumStackDepth())
                        {
                            retVal.Author = retVal.Author.GetRelatedPersistenceService().Get(context, dbModel.AuthorKey);
                            retVal.SetLoaded(nameof(ActNote.Author));
                        }
                        break;
                }

                return retVal;
            }
            
        }
    }
}