/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Acts;
using System;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence.Acts
{
    /// <summary>
    /// Persistence service between act and act relationship
    /// </summary>
    public class ActRelationshipPersistenceService : ActAssociationPersistenceService<ActRelationship, DbActRelationship>,
        IAdoKeyResolver<ActRelationship>, IAdoKeyResolver<DbActRelationship>
    {
        /// <inheritdoc/>
        public ActRelationshipPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        public Expression<Func<DbActRelationship, bool>> GetKeyExpression(DbActRelationship model) => o => o.SourceKey == model.SourceKey && o.RelationshipTypeKey == model.RelationshipTypeKey && o.TargetKey == model.TargetKey && o.ObsoleteVersionSequenceId == null;

        /// <inheritdoc/>
        public Expression<Func<ActRelationship, bool>> GetKeyExpression(ActRelationship model) => o => o.SourceEntityKey == model.SourceEntityKey && o.RelationshipTypeKey == model.RelationshipTypeKey && o.TargetActKey == model.TargetActKey && o.ObsoleteVersionSequenceId == null;


        /// <summary>
        /// Prepare references
        /// </summary>
        protected override ActRelationship BeforePersisting(DataContext context, ActRelationship data)
        {
            data.ClassificationKey = this.EnsureExists(context, data.Classification)?.Key ?? data.ClassificationKey;
            data.RelationshipTypeKey = this.EnsureExists(context, data.RelationshipType)?.Key ?? data.RelationshipTypeKey;
            data.TargetActKey = this.EnsureExists(context, data.TargetAct)?.Key ?? data.TargetActKey;
            data.SourceEntityKey = this.EnsureExists(context, data.SourceEntity)?.Key ?? data.SourceEntityKey;
            return base.BeforePersisting(context, data);
        }


        /// <summary>
        /// Convert to information model
        /// </summary>
        protected override ActRelationship DoConvertToInformationModel(DataContext context, DbActRelationship dbModel, params Object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);

            switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                    retVal.TargetAct = retVal.TargetAct.GetRelatedPersistenceService().Get(context, dbModel.TargetKey);
                    retVal.SetLoaded(o => o.TargetAct);
                    retVal.Classification = retVal.Classification.GetRelatedPersistenceService().Get(context, dbModel.ClassificationKey.GetValueOrDefault());
                    retVal.SetLoaded(o => o.Classification);
                    retVal.RelationshipType = retVal.RelationshipType.GetRelatedPersistenceService().Get(context, dbModel.RelationshipTypeKey);
                    retVal.SetLoaded(o => o.RelationshipType);
                    break;
            }

            return retVal;
        }
    }
}
