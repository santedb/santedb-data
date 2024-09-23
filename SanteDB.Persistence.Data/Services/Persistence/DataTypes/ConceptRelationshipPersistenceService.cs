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
 */
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Concepts;
using System;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence.DataTypes
{
    /// <summary>
    /// Concept relationship persistence service
    /// </summary>
    public class ConceptRelationshipPersistenceService : ConceptReferencePersistenceBase<ConceptRelationship, DbConceptRelationship>,
        IAdoKeyResolver<ConceptRelationship>, IAdoKeyResolver<DbConceptRelationship>
    {
        /// <summary>
        /// Concept relationship persistence service
        /// </summary>
        public ConceptRelationshipPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        public Expression<Func<DbConceptRelationship, bool>> GetKeyExpression(DbConceptRelationship model) => o => o.SourceKey == model.SourceKey && o.TargetKey == model.TargetKey && o.ObsoleteVersionSequenceId == null;

        /// <inheritdoc/>
        public Expression<Func<ConceptRelationship, bool>> GetKeyExpression(ConceptRelationship model) => o => o.SourceEntityKey == model.SourceEntityKey && o.TargetConceptKey == model.TargetConceptKey && o.ObsoleteVersionSequenceId == null;

        /// <summary>
        /// Concept relationship persistence service
        /// </summary>
        protected override ConceptRelationship BeforePersisting(DataContext context, ConceptRelationship data)
        {
            data.RelationshipTypeKey = data.RelationshipTypeKey ?? this.EnsureExists(context, data.RelationshipType)?.Key;
            data.TargetConceptKey = data.TargetConceptKey ?? this.EnsureExists(context, data.TargetConcept)?.Key;
            return data;
        }

        /// <summary>
        /// Information model conversion
        /// </summary>
        protected override ConceptRelationship DoConvertToInformationModel(DataContext context, DbConceptRelationship dbModel, params Object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);
            switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                    retVal.RelationshipType = retVal.RelationshipType.GetRelatedPersistenceService().Get(context, dbModel.RelationshipTypeKey);
                    retVal.SetLoaded(nameof(ConceptRelationship.RelationshipType));
                    retVal.TargetConcept = retVal.TargetConcept.GetRelatedPersistenceService().Get(context, dbModel.TargetKey);
                    retVal.SetLoaded(nameof(ConceptRelationship.TargetConcept));
                    break;
            }
            return retVal;
        }
    }
}