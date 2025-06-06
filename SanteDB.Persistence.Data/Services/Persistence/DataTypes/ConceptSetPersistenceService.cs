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
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Concepts;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence.DataTypes
{
    /// <summary>
    /// ConceptSet persistence services for ADO
    /// </summary>
    public class ConceptSetPersistenceService : NonVersionedDataPersistenceService<ConceptSet, DbConceptSet>, IAdoKeyResolver<DbConceptSetConceptAssociation>
    {
        /// <summary>
        /// Creates a new instance of the concept set
        /// </summary>
        public ConceptSetPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        protected override void DoDeleteReferencesInternal(DataContext context, Guid key)
        {
            base.DoDeleteReferencesInternal(context, key);
            context.DeleteAll<DbConceptSetConceptAssociation>(o => o.SourceKey == key);
        }

        /// <inheritdoc/>
        protected override ConceptSet DoInsertModel(DataContext context, ConceptSet data)
        {
            var retVal = base.DoInsertModel(context, data);

            if (data.ConceptsXml != null)
            {
                retVal.ConceptsXml = base.UpdateInternalAssociations(context, retVal.Key.Value, data.ConceptsXml.Select(o => new DbConceptSetConceptAssociation()
                {
                    ConceptKey = o,
                    SourceKey = retVal.Key.Value
                })).Select(o => o.ConceptKey).ToList();
            }

            if (data.Composition != null)
            {
                retVal.Composition = base.UpdateModelAssociations(context, retVal, data.Composition).ToList();
            }
            return retVal;
        }

        /// <inheritdoc/>
        protected override ConceptSet DoUpdateModel(DataContext context, ConceptSet data)
        {
            var retVal = base.DoUpdateModel(context, data);
            if (data.ConceptsXml != null)
            {
                retVal.ConceptsXml = base.UpdateInternalAssociations(context, retVal.Key.Value, data.ConceptsXml.Select(o => new DbConceptSetConceptAssociation()
                {
                    ConceptKey = o,
                    SourceKey = retVal.Key.Value
                }), o => o.SourceKey == data.Key).Select(o => o.ConceptKey).ToList();
            }
            if (data.Composition != null)
            {
                retVal.Composition = base.UpdateModelAssociations(context, retVal, data.Composition).ToList();
            }
            return retVal;
        }

        /// <summary>
        /// Perform the conversion of this concept set to a relationship model
        /// </summary>
        protected override ConceptSet DoConvertToInformationModel(DataContext context, DbConceptSet dbModel, params Object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);
            retVal.ConceptsXml = context.Query<DbConceptSetConceptAssociation>(o => o.SourceKey == dbModel.Key).Select(o => o.ConceptKey).ToList();

            switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                case LoadMode.SyncLoad:
                    retVal.Composition = retVal.Composition.GetRelatedPersistenceService().Query(context, o => o.SourceEntityKey == dbModel.Key).ToList();
                    retVal.SetLoaded(o=>o.Composition);
                    break;
            }
            return retVal;
        }

        /// <inheritdoc/>
        public Expression<Func<DbConceptSetConceptAssociation, bool>> GetKeyExpression(DbConceptSetConceptAssociation model) => o => o.SourceKey == model.SourceKey && o.ConceptKey == model.ConceptKey;
    }
}