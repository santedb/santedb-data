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
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Entities;
using System;
using System.Data.Common;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence.Entities
{
    /// <summary>
    /// A persistence service which handles entity relationships
    /// </summary>
    public class EntityRelationshipPersistenceService : EntityAssociationPersistenceService<EntityRelationship, DbEntityRelationship>,
        IAdoKeyResolver<EntityRelationship>, IAdoKeyResolver<DbEntityRelationship>
    {
        /// <summary>
        /// Entity relationship persistence service
        /// </summary>
        public EntityRelationshipPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        public Expression<Func<EntityRelationship, bool>> GetKeyExpression(EntityRelationship model) => o => o.SourceEntityKey == model.SourceEntityKey && o.TargetEntityKey == model.TargetEntityKey && o.RelationshipTypeKey == model.RelationshipTypeKey && o.ObsoleteVersionSequenceId == null;

        /// <inheritdoc/>
        public Expression<Func<DbEntityRelationship, bool>> GetKeyExpression(DbEntityRelationship model) => o => o.SourceKey == model.SourceKey && o.TargetKey == model.TargetKey && o.RelationshipTypeKey == model.RelationshipTypeKey && o.ObsoleteVersionSequenceId == null;

        /// <summary>
        /// Prepare references
        /// </summary>
        protected override EntityRelationship BeforePersisting(DataContext context, EntityRelationship data)
        {
            data.ClassificationKey = this.EnsureExists(context, data.Classification)?.Key ?? data.ClassificationKey;
            data.RelationshipRoleKey = this.EnsureExists(context, data.RelationshipRole)?.Key ?? data.RelationshipRoleKey;
            data.RelationshipTypeKey = this.EnsureExists(context, data.RelationshipType)?.Key ?? data.RelationshipTypeKey;
            data.TargetEntityKey = this.EnsureExists(context, data.TargetEntity)?.Key ?? data.TargetEntityKey;
            data.HolderKey = this.EnsureExists(context, data.Holder)?.Key ?? data.HolderKey;

            return base.BeforePersisting(context, data);
        }

        /// <inheritdoc/>
        protected override DbEntityRelationship DoInsertInternal(DataContext context, DbEntityRelationship dbModel)
        {   
            try
            {
                if(this.m_configuration.AutoUpdateExisting)
                {
                    var existingKey = context.Query<DbEntityRelationship>(o => o.SourceKey == dbModel.SourceKey && o.RelationshipTypeKey == dbModel.RelationshipTypeKey && o.TargetKey == dbModel.TargetKey && o.ObsoleteVersionSequenceId == null).Select(o => o.Key).FirstOrDefault();
                    if (existingKey != Guid.Empty)
                    {
                        context.UpdateAll<DbEntityRelationship>(o => o.Key == existingKey, o => o.ObsoleteVersionSequenceId == dbModel.EffectiveVersionSequenceId);
                    }
                }
                return base.DoInsertInternal(context, dbModel);
            }
            catch(DbException e) when (e.Message.Contains("ENTITY RELATIONSHIP FAILED VALIDATION") || e.Message.Contains("Validation error: Relationship"))
            {
                throw new DetectedIssueException(Core.BusinessRules.DetectedIssuePriorityType.Error, "data.relationship.validation", $"Relationship of type {dbModel.RelationshipTypeKey} between {dbModel.SourceKey} and {dbModel.TargetKey} is invalid", DetectedIssueKeys.CodificationIssue, e);
            }
        }

        /// <inheritdoc/>
        protected override DbEntityRelationship DoUpdateInternal(DataContext context, DbEntityRelationship dbModel)
        {
            try
            {
                // Get the existing key of the object
                var existingKey = context.Query<DbEntityRelationship>(o => o.SourceKey == dbModel.SourceKey && o.RelationshipTypeKey == dbModel.RelationshipTypeKey && o.TargetKey == dbModel.TargetKey && o.ObsoleteVersionSequenceId == null).Select(o => o.Key).FirstOrDefault();
                if (existingKey != Guid.Empty && existingKey != dbModel.Key)
                {
                    dbModel.Key = existingKey;
                }
                return base.DoUpdateInternal(context, dbModel);
            }
            catch (DbException e) when (e.Message.Contains("ENTITY RELATIONSHIP FAILED VALIDATION") || e.Message.Contains("Validation error: Relationship"))
            {
                throw new DetectedIssueException(Core.BusinessRules.DetectedIssuePriorityType.Error, "data.relationship.validation", $"Relationship of type {dbModel.RelationshipTypeKey} between {dbModel.SourceKey} and {dbModel.TargetKey} is invalid", DetectedIssueKeys.CodificationIssue, e);
            }
        }

        /// <summary>
        /// Convert to information model
        /// </summary>
        protected override EntityRelationship DoConvertToInformationModel(DataContext context, DbEntityRelationship dbModel, params Object[] referenceObjects)
        {
            using (context.CreateInformationModelGuard(dbModel.Key))
            {
                var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);

                switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
                {
                    case LoadMode.FullLoad:
                        if (context.ValidateMaximumStackDepth())
                        {
                            if (!context.IsLoadingInformationModel(dbModel.TargetKey))
                            {
                                retVal.TargetEntity = retVal.TargetEntity.GetRelatedPersistenceService().Get(context, dbModel.TargetKey);
                                retVal.SetLoaded(nameof(EntityRelationship.TargetEntity));
                            }
                            retVal.Classification = retVal.Classification.GetRelatedPersistenceService().Get(context, dbModel.ClassificationKey.GetValueOrDefault());
                            retVal.SetLoaded(nameof(EntityRelationship.Classification));
                            retVal.RelationshipRole = retVal.RelationshipRole.GetRelatedPersistenceService().Get(context, dbModel.RelationshipRoleKey.GetValueOrDefault());
                            retVal.SetLoaded(o => o.RelationshipRole);
                            retVal.RelationshipType = retVal.RelationshipType.GetRelatedPersistenceService().Get(context, dbModel.RelationshipTypeKey);
                            retVal.SetLoaded(o => o.RelationshipType);
                        }

                        break;
                }

                return retVal;
            }
        }
    }
}