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
 * Date: 2023-3-10
 */
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using SanteDB.BI.Datamart;
using SanteDB.BI.Datamart.DataFlow;
using SanteDB.BI.Model;
using SanteDB.BI.Services;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Data.Import;
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
using SanteDB.OrmLite.Migration;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.BI;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.ForeignData;
using SanteDB.Persistence.Data.Model;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Represents an implementation of the BI datamart manager which uses the primary ADO storage to store 
    /// metadata about the other datamarts which are available on the SanteDB server
    /// </summary>
    public class AdoBiDatamartRepository : IBiDatamartRepository, IMappedQueryProvider<IBiDatamart>
    {
        private readonly IConfigurationManager m_configurationManager;

        // Configuration
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly ILocalizationService m_localization;
        private readonly ModelMapper m_modelMapper;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoBiDatamartRepository(IConfigurationManager configurationManager, ILocalizationService localizationService, IPolicyEnforcementService pepService)
        {
            this.m_configurationManager = configurationManager;
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_pepService = pepService;
            this.m_localization = localizationService;
            this.m_modelMapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.MapResourceName), "AdoModelMap");
        }

        /// <inheritdoc/>
        public string ServiceName => "ADO.NET Datamart Manager";

        /// <inheritdoc/>
        public IDbProvider Provider => this.m_configuration.Provider;

        /// <inheritdoc/>
        public IQueryPersistenceService QueryPersistence => throw new NotSupportedException();

        /// <inheritdoc/>
        public IOrmResultSet ExecuteQueryOrm(DataContext context, Expression<Func<IBiDatamart, bool>> query)
        {
            var expression = this.m_modelMapper.MapModelExpression<IBiDatamart, DbDatamartRegistration, bool>(query, false);
            if (!query.ToString().Contains(nameof(BaseEntityData.ObsoletionTime)))
            {
                var obsoletionReference = Expression.MakeBinary(ExpressionType.Equal, Expression.MakeMemberAccess(expression.Parameters[0], typeof(DbBaseData).GetProperty(nameof(DbBaseData.ObsoletionTime))), Expression.Constant(null));
                expression = Expression.Lambda<Func<DbDatamartRegistration, bool>>(Expression.MakeBinary(ExpressionType.AndAlso, obsoletionReference, expression.Body), expression.Parameters);
            }

            return context.Query<DbDatamartRegistration>(expression);
        }

        /// <inheritdoc/>
        public IQueryResultSet<IBiDatamart> Find(Expression<Func<IBiDatamart, bool>> query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            return new MappedQueryResultSet<IBiDatamart>(this).Where(query);
        }

        /// <inheritdoc/>
        public IBiDatamart Get(DataContext context, Guid key) => new AdoBiDatamart(context.FirstOrDefault<DbDatamartRegistration>(o => o.Key == key), this.m_configuration.Provider);

        /// <inheritdoc/>
        public SqlStatement GetCurrentVersionFilter(string tableAlias)
        {
            var tableMap = TableMapping.Get(typeof(DbDatamartRegistration));
            var obsltCol = tableMap.GetColumn(nameof(DbDatamartRegistration.ObsoletionTime));
            return new SqlStatement($"{tableAlias ?? tableMap.TableName}.{obsltCol.Name} IS NULL");
        }

        /// <inheritdoc/>
        public IBiDataFlowExecutionContext GetExecutionContext(IBiDatamart datamart, BiExecutionPurposeType purpose)
        {
            if(datamart == null)
            {
                throw new ArgumentNullException(nameof(datamart));
            }

            if (purpose.HasFlag(BiExecutionPurposeType.Discovery))
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.ReadWarehouseData);
            }
            else if(purpose.HasFlag(BiExecutionPurposeType.DatabaseManagement))
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.UnrestrictedAdministration);
            }
            else if(purpose.HasFlag(BiExecutionPurposeType.SchemaManagement))
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.AdministerWarehouse);
            }
            else if(purpose.HasFlag(BiExecutionPurposeType.Refresh))
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.WriteWarehouseData);
            }

            return new AdoDataFlowExecutionContext(this.m_configurationManager, datamart, purpose).LogStart();
        }

        /// <inheritdoc/>
        public LambdaExpression MapExpression<TReturn>(Expression<Func<IBiDatamart, TReturn>> sortExpression) => this.m_modelMapper.MapModelExpression<IBiDatamart, DbDatamartRegistration, TReturn>(sortExpression, false);

        /// <inheritdoc/>
        public IBiDatamart Register(BiDatamartDefinition dataMartDefinition)
        {
            if (dataMartDefinition == null)
            {
                throw new ArgumentNullException(nameof(dataMartDefinition));
            }

            // Ensure the definition is valid and checks out!
            var validationResult = dataMartDefinition.Validate();
            if (validationResult.Any(d => d.Priority == Core.BusinessRules.DetectedIssuePriorityType.Error))
            {
                throw new DetectedIssueException(validationResult);
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AdministerWarehouse);
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();

                    using (var tx = context.BeginTransaction())
                    {
                        var existing = context.Query<DbDatamartRegistration>(o => o.Id == dataMartDefinition.Id && o.ObsoletionTime == null).FirstOrDefault();
                        
                        // Register the datamart
                        if (existing == null)
                        {
                            existing = context.Insert(new DbDatamartRegistration()
                            {
                                CreatedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal),
                                CreationTime = DateTimeOffset.Now,
                                Description = dataMartDefinition.MetaData?.Annotation?.JsonBody,
                                Id = dataMartDefinition.Id,
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
        public IBiDatamart ToModelInstance(DataContext context, object result)
        {
            if (result is DbDatamartRegistration dbfds)
            {
                return new AdoBiDatamart(dbfds, this.m_configuration.Provider);
            }
            else if(result == null)
            {
                return null;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(result));
            }
        }

        /// <inheritdoc/>
        public void Unregister(IBiDatamart datamart)
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
                    context.Open();

                    var existing = context.Query<DbDatamartRegistration>(o => o.Key == datamart.Key).FirstOrDefault();
                    if(existing == null)
                    {
                        throw new KeyNotFoundException(datamart.Key.ToString());
                    }

                    existing.ObsoletedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                    existing.ObsoletionTime = DateTimeOffset.Now;
                    context.Update(existing);

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