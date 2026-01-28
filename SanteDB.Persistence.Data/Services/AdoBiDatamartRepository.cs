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
using SanteDB.BI.Datamart;
using SanteDB.BI.Datamart.DataFlow;
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.MappedResultSets;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.BI;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Represents an implementation of the BI datamart manager which uses the primary ADO storage to store 
    /// metadata about the other datamarts which are available on the SanteDB server
    /// </summary>
    public class AdoBiDatamartRepository : IBiDatamartRepository, IMappedQueryProvider<IDatamart>
    {
        private readonly IConfigurationManager m_configurationManager;

        // Configuration
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly IDataStreamManager m_dataStreamManager;
        private readonly ILocalizationService m_localization;
        private readonly ModelMapper m_modelMapper;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoBiDatamartRepository(IConfigurationManager configurationManager, IDataStreamManager dataStreamManager, ILocalizationService localizationService, IPolicyEnforcementService pepService)
        {
            this.m_configurationManager = configurationManager;
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_pepService = pepService;
            this.m_dataStreamManager = dataStreamManager;
            this.m_localization = localizationService;
            this.m_modelMapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.MapResourceName), "AdoModelMap", this.GetType().Assembly);

        }


        /// <inheritdoc/>
        public string ServiceName => "ADO.NET Datamart Manager";

        /// <inheritdoc/>
        public IDbProvider Provider => this.m_configuration.Provider;

        /// <inheritdoc/>
        public IQueryPersistenceService QueryPersistence => throw new NotSupportedException();

        /// <inheritdoc/>
        public IOrmResultSet ExecuteQueryOrm(DataContext context, Expression<Func<IDatamart, bool>> query)
        {
            var expression = this.m_modelMapper.MapModelExpression<IDatamart, DbDatamartRegistration, bool>(query, false);
            if (!query.ToString().Contains(nameof(BaseEntityData.ObsoletionTime)))
            {
                var obsoletionReference = Expression.MakeBinary(ExpressionType.Equal, Expression.MakeMemberAccess(expression.Parameters[0], typeof(DbBaseData).GetProperty(nameof(DbBaseData.ObsoletionTime))), Expression.Constant(null));
                expression = Expression.Lambda<Func<DbDatamartRegistration, bool>>(Expression.MakeBinary(ExpressionType.AndAlso, obsoletionReference, expression.Body), expression.Parameters);
            }

            return context.Query<DbDatamartRegistration>(expression);
        }

        /// <inheritdoc/>
        public IQueryResultSet<IDatamart> Find(Expression<Func<IDatamart, bool>> query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            if (!this.m_pepService.SoftDemand(PermissionPolicyIdentifiers.QueryWarehouseData, AuthenticationContext.Current.Principal))
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.AdministerWarehouse);
            }
            return new MappedQueryResultSet<IDatamart>(this).Where(query);
        }

        /// <inheritdoc/>
        public IDatamart Get(DataContext context, Guid key) => new AdoBiDatamart(context.FirstOrDefault<DbDatamartRegistration>(o => o.Key == key), this.m_configuration.Provider);

        /// <inheritdoc/>
        public SqlStatement GetCurrentVersionFilter(string tableAlias)
        {
            var tableMap = TableMapping.Get(typeof(DbDatamartRegistration));
            var obsltCol = tableMap.GetColumn(nameof(DbDatamartRegistration.ObsoletionTime));
            return new SqlStatement($"{tableAlias ?? tableMap.TableName}.{obsltCol.Name} IS NULL");
        }

        /// <inheritdoc/>
        public IDataFlowExecutionContext GetExecutionContext(IDatamart datamart, DataFlowExecutionPurposeType purpose)
        {
            if (datamart == null)
            {
                throw new ArgumentNullException(nameof(datamart));
            }

            if (purpose.HasFlag(DataFlowExecutionPurposeType.Discovery))
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.ReadWarehouseData);
            }
            else if (purpose.HasFlag(DataFlowExecutionPurposeType.DatabaseManagement))
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.UnrestrictedAdministration);
            }
            else if (purpose.HasFlag(DataFlowExecutionPurposeType.SchemaManagement))
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.AdministerWarehouse);
            }
            else if (purpose.HasFlag(DataFlowExecutionPurposeType.Refresh))
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.WriteWarehouseData);
            }

            return new AdoDataFlowExecutionContext(this.m_configurationManager, this.m_dataStreamManager, datamart, purpose).LogStart();
        }

        /// <inheritdoc/>
        public LambdaExpression MapExpression<TReturn>(Expression<Func<IDatamart, TReturn>> sortExpression) => this.m_modelMapper.MapModelExpression<IDatamart, DbDatamartRegistration, TReturn>(sortExpression, false);

        /// <inheritdoc/>
        public IDatamart Register(BiDatamartDefinition dataMartDefinition)
        {
            if (dataMartDefinition == null)
            {
                throw new ArgumentNullException(nameof(dataMartDefinition));
            }

            // Ensure the definition is valid and checks out! (use SYSTEM since we're not accessing anything and we dont' care about permissions at this point
            using (AuthenticationContext.EnterSystemContext())
            {
                var validationResult = dataMartDefinition.Validate();
                if (validationResult.Any(d => d.Priority == Core.BusinessRules.DetectedIssuePriorityType.Error))
                {
                    throw new DetectedIssueException(validationResult);
                }
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AdministerWarehouse);
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);

                    using (var tx = context.BeginTransaction())
                    {
                        var existing = context.Query<DbDatamartRegistration>(o => o.Id == dataMartDefinition.Id && o.ObsoletionTime == null).FirstOrDefault();

                        byte[] defHash = null;
                        using (var ms = new MemoryStream())
                        {
                            dataMartDefinition.Save(ms);
                            defHash = SHA256.Create().ComputeHash(ms.ToArray());
                        }

                        // Register the datamart
                        if (existing == null)
                        {
                            existing = context.Insert(new DbDatamartRegistration()
                            {
                                CreatedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal),
                                CreationTime = DateTimeOffset.Now,
                                Description = dataMartDefinition.MetaData?.Annotation?.JsonBody,
                                Id = dataMartDefinition.Id,
                                DefinitionHash = defHash,
                                Name = dataMartDefinition.Name,
                                Version = dataMartDefinition.MetaData?.Version ?? "1.0"
                            });
                        }
                        else
                        {
                            existing.UpdatedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                            existing.UpdatedTime = DateTimeOffset.Now;
                            existing.Description = dataMartDefinition.MetaData?.Annotation?.JsonBody;
                            existing.Name = dataMartDefinition.Name;
                            existing.Version = dataMartDefinition.MetaData?.Version ?? "1.0";
                            existing.DefinitionHash = defHash;
                            existing = context.Update(existing);
                        }

                        var retVal = new AdoBiDatamart(existing, this.m_configuration.Provider);
                        tx.Commit();
                        return retVal;
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localization.GetString(ErrorMessageStrings.DATAMART_MANAGE_ERROR), e);
            }

        }

        /// <inheritdoc/>
        public IDatamart ToModelInstance(DataContext context, object result)
        {
            if (result is DbDatamartRegistration dbfds)
            {
                return new AdoBiDatamart(dbfds, this.m_configuration.Provider);
            }
            else if (result == null)
            {
                return null;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(result));
            }
        }

        /// <inheritdoc/>
        public void Unregister(IDatamart datamart)
        {
            if (datamart == null)
            {
                throw new ArgumentNullException(nameof(datamart));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AdministerWarehouse);
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);

                    using (var tx = context.BeginTransaction())
                    {
                        var existing = context.Query<DbDatamartRegistration>(o => o.Key == datamart.Key).FirstOrDefault();
                        if (existing == null)
                        {
                            throw new KeyNotFoundException(datamart.Key.ToString());
                        }

                        existing.ObsoletedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                        existing.ObsoletionTime = DateTimeOffset.Now;
                        context.Update(existing);

                        // Delete executions 
                        foreach (var itm in context.Query<DbDatamartExecutionEntry>(o => o.DatamartKey == existing.Key).ToArray())
                        {
                            if (itm.DiagnosticStreamKey.HasValue)
                            {
                                this.m_dataStreamManager.Remove(itm.DiagnosticStreamKey.Value);
                            }

                            // Delete all logs
                            context.DeleteAll<DbDatamartLogEntry>(o => o.ExecutionContextId == itm.Key);
                            context.Delete(itm);
                        }

                        tx.Commit();
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localization.GetString(ErrorMessageStrings.DATAMART_MANAGE_ERROR), e);
            }
        }
    }
}