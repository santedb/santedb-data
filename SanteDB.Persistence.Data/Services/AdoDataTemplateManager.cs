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
 * Date: 2024-12-12
 */
using ClosedXML;
using SanteDB.Core.Data.Import;
using SanteDB.Core.Data.Query;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;


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
using SanteDB.Core.Templates;
using SanteDB.Core.Templates.Definition;
using SanteDB.OrmLite;
using SanteDB.OrmLite.MappedResultSets;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.ForeignData;
using SanteDB.Persistence.Data.Model;
using SanteDB.Persistence.Data.Model.Extensibility;
using SanteDB.Persistence.Data.Model.Security;
using SanteDB.Persistence.Data.Model.Sys;
using SanteDB.Persistence.Data.Services.Persistence;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mime;
using System.Runtime.CompilerServices;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// ADO data template manager
    /// </summary>
    public class AdoDataTemplateManager : IDataTemplateManagementService, IMappedQueryProvider<DataTemplateDefinition>, IAdoTrimProvider
    {

        // Configuration
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IQueryPersistenceService m_queryPersistenceService;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly ModelMapper m_modelMapper;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoDataTemplateManager));

        /// <summary>
        /// DI constructor
        /// </summary>
        public AdoDataTemplateManager(IConfigurationManager configurationManager, IQueryPersistenceService queryPersistenceService, IPolicyEnforcementService pepService, IPasswordHashingService hashingService)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_queryPersistenceService = queryPersistenceService;
            this.m_pepService = pepService;
            this.m_modelMapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.CdssMapResourceName), "CdssModelMap");
        }

        /// <inheritdoc/>
        public string ServiceName => "ADO.NET Data Template Manager";

        /// <inheritdoc/>
        public IDbProvider Provider => this.m_configuration.Provider;

        /// <inheritdoc/>
        public IQueryPersistenceService QueryPersistence => this.m_queryPersistenceService;


        /// <inheritdoc/>
        public DataTemplateDefinition AddOrUpdate(DataTemplateDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (AuthenticationContext.Current.Principal != AuthenticationContext.SystemPrincipal)
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterDataTemplates);
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);
                    using (var tx = context.BeginTransaction())
                    {
                        context.EstablishProvenance(AuthenticationContext.Current.Principal);

                        if (definition.Key.GetValueOrDefault() == Guid.Empty)
                        {
                            definition.Key = Guid.NewGuid();
                        }

                        // Now update the view 
                        using (var ms = new MemoryStream())
                        {
                            definition.Save(ms);

                            // Create the template definition
                            var existingTpl = context.Query<DbTemplateDefinition>(o => o.Key == definition.Key).FirstOrDefault();
                            var existingView = context.Query<DbDataTemplateDefinition>(o => o.Key == definition.Key).FirstOrDefault();
                            
                            // Ensure version is newer
                            if(definition.Version != 0 && existingView?.Version > definition.Version)
                            {
                                this.m_tracer.TraceInfo("Skipping the insert or update of {0} since version in the database is newer than the provided version", definition.Mnemonic);
                                return definition;
                            }

                            if (existingTpl == null)
                            {
                                existingTpl = context.Insert(new DbTemplateDefinition()
                                {
                                    CreatedByKey = context.ContextId,
                                    CreationTime = DateTimeOffset.Now,
                                    Description = definition.Description,
                                    Mnemonic = definition.Mnemonic,
                                    Name = definition.Name,
                                    Oid = definition.Oid,
                                    Key = definition.Key.Value
                                });
                            }
                            else
                            {
                                existingTpl.Oid = definition.Oid;
                                existingTpl.Name = definition.Name;
                                existingTpl.Mnemonic = definition.Mnemonic;
                                existingTpl.UpdatedByKey = context.ContextId;
                                existingTpl.UpdatedTime = DateTimeOffset.Now;
                                existingTpl.Description = definition.Description;
                                existingTpl = context.Update(existingTpl);
                            }


                            if (existingView == null)
                            {
                                existingView = context.Insert(new DbDataTemplateDefinition()
                                {
                                    Key = definition.Key.Value,
                                    CreatedByKey = context.ContextId,
                                    CreationTime = DateTimeOffset.Now,
                                    UpdatedByKey = context.ContextId,
                                    UpdatedTime = DateTimeOffset.Now,
                                    Definition = ms.ToArray(),
                                    Description = definition.Description,
                                    Mnemonic = definition.Mnemonic,
                                    Name = definition.Name,
                                    Oid = definition.Oid,
                                    Public = definition.Public,
                                    IsActive = definition.IsActive,
                                    Version = definition.Version,
                                    Readonly = definition.Readonly
                                });
                            }
                            else
                            {
                                existingView.IsActive = definition.IsActive;
                                existingView.UpdatedByKey = context.ContextId;
                                existingView.UpdatedTime = DateTimeOffset.Now;
                                if (existingView.ObsoletionTime.HasValue)
                                {
                                    existingView.Version = definition.Version;
                                }
                                else
                                {
                                    existingView.Version++;
                                }

                                if (existingView.ObsoletionTime.HasValue && definition.ObsoletionTime == null) // undelete
                                {
                                    existingView.ObsoletionTime = null;
                                    existingView.ObsoletedByKey = null;
                                    existingView.ObsoletedByKeySpecified = existingView.ObsoletionTimeSpecified = true;
                                }
                                else if(!existingView.Readonly || AuthenticationContext.Current.Principal == AuthenticationContext.SystemPrincipal)
                                {
                                    existingView.ObsoletionTime = null;
                                    existingView.ObsoletedByKey = null;
                                    existingView.Definition = ms.ToArray();
                                    existingView.Mnemonic = definition.Mnemonic;
                                    existingView.Name = definition.Name;
                                    existingView.Oid = definition.Oid;
                                    existingView.Public = definition.Public;
                                    existingView.Description = definition.Description;
                                    existingView.Readonly = definition.Readonly;
                                }
                                

                                existingView = context.Update(existingView);
                            }
                           
                            var retVal = this.ToModelInstance(context, existingView);
                            tx.Commit();
                            return retVal;
                        }
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Error persisting data template", e);
            }
        }

        /// <inheritdoc/>
        public IOrmResultSet ExecuteQueryOrm(DataContext context, Expression<Func<DataTemplateDefinition, bool>> query)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            else if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var expression = this.m_modelMapper.MapModelExpression<DataTemplateDefinition, DbDataTemplateDefinition, bool>(query, true);
            if (!query.ToString().Contains(nameof(BaseEntityData.ObsoletionTime)))
            {
                var obsoletionReference = Expression.MakeBinary(ExpressionType.Equal, Expression.MakeMemberAccess(expression.Parameters[0], typeof(DbNonVersionedBaseData).GetProperty(nameof(DbNonVersionedBaseData.ObsoletionTime))), Expression.Constant(null));
                expression = Expression.Lambda<Func<DbDataTemplateDefinition, bool>>(Expression.MakeBinary(ExpressionType.AndAlso, obsoletionReference, expression.Body), expression.Parameters);
            }

            return context.Query<DbDataTemplateDefinition>(expression);
        }

        /// <inheritdoc/>
        public IQueryResultSet<DataTemplateDefinition> Find(Expression<Func<DataTemplateDefinition, bool>> query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            return new MappedQueryResultSet<DataTemplateDefinition>(this).Where(query);
        }

        /// <inheritdoc/>
        public DataTemplateDefinition Get(Guid key)
        {
            if (key == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(key));
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    context.Open(initializeExtensions: false);
                    return this.Get(context, key);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Error reading data template", e);
            }
        }

        /// <inheritdoc/>
        public DataTemplateDefinition Get(DataContext context, Guid key)
        {
            var existing = context.Query<DbDataTemplateDefinition>(o => o.Key == key).FirstOrDefault();
            if (existing == null)
            {
                throw new KeyNotFoundException(key.ToString());
            }
            return this.ToModelInstance(context, existing);
        }

        /// <inheritdoc/>
        public SqlStatement GetCurrentVersionFilter(string tableAlias)
        {
            var tableMap = TableMapping.Get(typeof(DbDataTemplateDefinition));
            var obsltCol = tableMap.GetColumn(nameof(DbDataTemplateDefinition.ObsoletionTime));
            return new SqlStatement($"{tableAlias ?? tableMap.TableName}.{obsltCol.Name} IS NULL");
        }

        /// <inheritdoc/>
        public LambdaExpression MapExpression<TReturn>(Expression<Func<DataTemplateDefinition, TReturn>> sortExpression)
        {
            return this.m_modelMapper.MapModelExpression<DataTemplateDefinition, DbDataTemplateDefinition, TReturn>(sortExpression, true);
        }

        /// <inheritdoc/>
        public DataTemplateDefinition Remove(Guid key)
        {
            if (key == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterDataTemplates);
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);
                    using (var tx = context.BeginTransaction())
                    {
                        context.EstablishProvenance(AuthenticationContext.Current.Principal);
                        var existing = context.Query<DbDataTemplateDefinition>(o => o.Key == key).FirstOrDefault();
                        if (existing == null)
                        {
                            throw new KeyNotFoundException(key.ToString());
                        }

                        var existingTplDef = context.Query<DbTemplateDefinition>(o => o.Key == key).FirstOrDefault();
                        if(existingTplDef != null)
                        {
                            existingTplDef.ObsoletedByKey = context.ContextId;
                            existingTplDef.ObsoletionTime = DateTimeOffset.Now;
                            context.Update(existingTplDef);
                        }

                        existing.ObsoletedByKey = context.ContextId;
                        existing.ObsoletionTime = DateTimeOffset.Now;
                        tx.Commit();

                        var retVal = context.Update(existing);
                        return this.ToModelInstance(context, retVal);
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException("Error deleting template definition", e);
            }
        }

        /// <inheritdoc/>
        public DataTemplateDefinition ToModelInstance(DataContext context, object result)
        {
            if (result is DbDataTemplateDefinition dte)
            {
                using (var ms = new MemoryStream(dte.Definition))
                {
                    var retVal = DataTemplateDefinition.Load(ms);
                    retVal.LastUpdated = (dte.UpdatedTime ?? dte.CreationTime).DateTime;
                    retVal.IsActive = dte.ObsoletionTime.HasValue ? true : dte.IsActive;
                    retVal.Oid = dte.Oid;
                    retVal.Name = dte.Name;
                    retVal.Description = dte.Description;
                    retVal.Public = dte.Public;
                    retVal.Readonly = dte.Readonly;
                    retVal.Version = dte.Version;
                    retVal.Mnemonic = dte.Mnemonic;
                    retVal.CreatedByKey = dte.CreatedByKey;
                    retVal.CreationTime = dte.CreationTime;
                    retVal.UpdatedByKey = dte.UpdatedByKey;
                    retVal.UpdatedTime = dte.UpdatedTime;
                    retVal.ObsoletionTime = dte.ObsoletionTime;
                    retVal.ObsoletedByKey = dte.ObsoletedByKey;

                    // Authorship
                    if (retVal.Author?.Any() != true)
                    {
                        var provKey = dte.UpdatedByKey ?? dte.CreatedByKey;
                        var provShip = context.Query<DbSecurityProvenance>(o => o.Key == provKey).FirstOrDefault();
                        retVal.Author = new List<string>();

                        // Get the appropriate prov
                        if (provShip.UserKey.HasValue)
                        {
                            retVal.Author.Add(context.Query<DbSecurityUser>(o => o.Key == provShip.UserKey).Select(o => o.UserName).First());
                        }
                        else if (provShip.DeviceKey.HasValue)
                        {
                            retVal.Author.Add(context.Query<DbSecurityDevice>(o => o.Key == provShip.DeviceKey).Select(o => o.PublicId).First());
                        }
                        else
                        {
                            retVal.Author.Add(context.Query<DbSecurityApplication>(o => o.Key == provShip.ApplicationKey).Select(o => o.PublicId).First());

                        }
                    }

                    return retVal;
                }
            }
            else
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(DbDataTemplateDefinition), result.GetType()));
            }
        }

        /// <inheritdoc/>
        public void Trim(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder)
        {
            context.DeleteAll<DbTemplateDefinition>(o => o.ObsoletionTime != null && o.ObsoletionTime > deletedCutoff);
            context.DeleteAll<DbDataTemplateDefinition>(o => o.ObsoletionTime != null && o.ObsoletionTime > deletedCutoff);
        }

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Delete(Guid key) => this.Remove(key);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Delete(Guid key) => this.Remove(key);

        /// <inheritdoc/>
        IQueryResultSet<DataTemplateDefinition> IRepositoryService<DataTemplateDefinition>.Find(Expression<Func<DataTemplateDefinition, bool>> query) => this.Find(query);

        /// <inheritdoc/>
        IQueryResultSet IRepositoryService.Find(Expression query)
        {
            if (query is Expression<Func<DataTemplateDefinition, bool>> qr)
            {
                return this.Find(qr);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(Expression<Func<DataTemplateDefinition, bool>>), query.GetType()));
            }
        }

        /// <inheritdoc/>
        IEnumerable<IdentifiedData> IRepositoryService.Find(Expression query, int offset, int? count, out int totalResults)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Get(Guid key) => this.Get(key);

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Get(Guid key, Guid versionKey) => this.Get(key);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Get(Guid key) => this.Get(key);

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Insert(DataTemplateDefinition data) => this.AddOrUpdate(data);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Insert(object data)
        {
            if (data is DataTemplateDefinition dd)
            {
                return this.AddOrUpdate(dd);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(DataTemplateDefinition), data.GetType()));
            }
        }

        /// <inheritdoc/>
        DataTemplateDefinition IRepositoryService<DataTemplateDefinition>.Save(DataTemplateDefinition data) => this.AddOrUpdate(data);

        /// <inheritdoc/>
        IdentifiedData IRepositoryService.Save(object data)
        {
            if (data is DataTemplateDefinition dd)
            {
                return this.AddOrUpdate(dd);
            }
            else
            {
                throw new ArgumentOutOfRangeException(String.Format(ErrorMessages.ARGUMENT_INCOMPATIBLE_TYPE, typeof(DataTemplateDefinition), data.GetType()));
            }
        }
    }
}