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
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Acts;
using System;
using System.Data.Common;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence.Acts
{
    /// <summary>
    /// Persistence service between act and act relationship
    /// </summary>
    public class ActParticipationPersistenceService : ActAssociationPersistenceService<ActParticipation, DbActParticipation>,
        IAdoKeyResolver<ActParticipation>, IAdoKeyResolver<DbActParticipation>
    {
        /// <summary>
        /// DI constructor
        /// </summary>
        public ActParticipationPersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        /// <inheritdoc/>
        public Expression<Func<DbActParticipation, bool>> GetKeyExpression(DbActParticipation model) => o => o.SourceKey == model.SourceKey && o.ParticipationRoleKey == model.ParticipationRoleKey && o.TargetKey == model.TargetKey && o.ObsoleteVersionSequenceId == null;

        /// <inheritdoc/>
        public Expression<Func<ActParticipation, bool>> GetKeyExpression(ActParticipation model) => o => o.SourceEntityKey == model.SourceEntityKey && o.ParticipationRoleKey == model.ParticipationRoleKey && o.PlayerEntityKey == model.PlayerEntityKey && o.ObsoleteVersionSequenceId == null;


        /// <inheritdoc/>
        protected override ActParticipation BeforePersisting(DataContext context, ActParticipation data)
        {
            data.ClassificationKey = data.ClassificationKey ?? this.EnsureExists(context, data.Classification)?.Key;
            data.ParticipationRoleKey = data.ParticipationRoleKey ?? this.EnsureExists(context, data.ParticipationRole)?.Key;
            data.PlayerEntityKey = data.PlayerEntityKey ?? this.EnsureExists(context, data.PlayerEntity)?.Key;
            data.ActKey = data.ActKey ?? this.EnsureExists(context, data.Act)?.Key;
            return base.BeforePersisting(context, data);
        }

        /// <inheritdoc/>
        protected override ActParticipation DoConvertToInformationModel(DataContext context, DbActParticipation dbModel, params Object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);

            switch (DataPersistenceControlContext.Current?.LoadMode ?? this.m_configuration.LoadStrategy)
            {
                case LoadMode.FullLoad:
                    retVal.PlayerEntity = retVal.PlayerEntity.GetRelatedPersistenceService().Get(context, dbModel.TargetKey);
                    retVal.SetLoaded(o => o.PlayerEntity);
                    retVal.Classification = retVal.Classification.GetRelatedPersistenceService().Get(context, dbModel.ClassificationKey.GetValueOrDefault());
                    retVal.SetLoaded(o => o.Classification);
                    retVal.ParticipationRole = retVal.ParticipationRole.GetRelatedPersistenceService().Get(context, dbModel.ParticipationRoleKey);
                    retVal.SetLoaded(o => o.ParticipationRole);
                    break;
            }

            return retVal;
        }


        /// <inheritdoc/>
        protected override DbActParticipation DoInsertInternal(DataContext context, DbActParticipation dbModel)
        {
            try
            {
                return base.DoInsertInternal(context, dbModel);
            }
            catch (DbException e) when (e.Message.Contains("ACT PARTICIPATION FAILED VALIDATION") || e.Message.Contains("Validation error: Relationship"))
            {
                throw new DetectedIssueException(Core.BusinessRules.DetectedIssuePriorityType.Error, "data.relationship.validation", $"Participation of type {dbModel.ParticipationRoleKey} between {dbModel.SourceKey} and {dbModel.TargetKey} is invalid", DetectedIssueKeys.CodificationIssue, e);
            }
        }

        /// <inheritdoc/>
        protected override DbActParticipation DoUpdateInternal(DataContext context, DbActParticipation dbModel)
        {
            try
            {
                return base.DoUpdateInternal(context, dbModel);
            }
            catch (DbException e) when (e.Message.Contains("ACT PARTICIPATION FAILED VALIDATION") || e.Message.Contains("Validation error: Relationship"))
            {
                throw new DetectedIssueException(Core.BusinessRules.DetectedIssuePriorityType.Error, "data.relationship.validation", $"Participation of type {dbModel.ParticipationRoleKey} between {dbModel.SourceKey} and {dbModel.TargetKey} is invalid", DetectedIssueKeys.CodificationIssue, e);
            }
        }
    }
}
