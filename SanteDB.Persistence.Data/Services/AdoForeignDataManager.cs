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
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Data.Import;
using SanteDB.Core.Data.Import.Definition;
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
using SanteDB.OrmLite;
using SanteDB.OrmLite.MappedResultSets;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.ForeignData;
using SanteDB.Persistence.Data.Model;
using SanteDB.Persistence.Data.Model.Sys;
using SanteDB.Persistence.Data.Services.Persistence;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Foreign data manager which stores data about staged foreign data into the database
    /// </summary>
    public class AdoForeignDataManager : IForeignDataManagerService, IReportProgressChanged, IMappedQueryProvider<IForeignDataSubmission>, IAdoTrimProvider
    {
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly ILocalizationService m_localizationService;
        private readonly IPolicyEnforcementService m_pepService;
        private readonly IDataStreamManager m_streamManager;
        private readonly IForeignDataImporter m_importService;
        private readonly IRepositoryService<ForeignDataMap> m_foreignDataMapRepository;
        private readonly ModelMapper m_modelMapper;
        private readonly IQueryPersistenceService m_queryPersistenceService;
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoForeignDataManager));

        /// <summary>
        /// Creates a new ADO session identity provider with injected configuration manager
        /// </summary>
        public AdoForeignDataManager(IConfigurationManager configuration,
            ILocalizationService localizationService,
            IPolicyEnforcementService pepService,
            IRepositoryService<ForeignDataMap> foreignDataMapRepository,
            IForeignDataImporter importService,
            IDataStreamManager foreignDataStreamManager,
            IQueryPersistenceService queryPersistence = null
            )
        {
            this.m_configuration = configuration.GetSection<AdoPersistenceConfigurationSection>();
            this.m_localizationService = localizationService;
            this.m_pepService = pepService;
            this.m_queryPersistenceService = queryPersistence;
            this.m_streamManager = foreignDataStreamManager;
            this.m_importService = importService;

            this.m_foreignDataMapRepository = foreignDataMapRepository;
            this.m_modelMapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.MapResourceName), "AdoModelMap");

            // Reset the state of running jobs
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();
                    foreach (var rj in context.Query<DbForeignDataStage>(o => o.Status == ForeignDataStatus.Running).ToArray())
                    {
                        rj.Status = ForeignDataStatus.Staged;
                        context.Update(rj);
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error resetting job status: {0}", e);
            }
        }

        /// <inheritdoc/>
        public string ServiceName => "ADO Foreign Data Manager";

        /// <inheritdoc/>
        public IDbProvider Provider => this.m_configuration.Provider;

        /// <inheritdoc/>
        public IQueryPersistenceService QueryPersistence => this.m_queryPersistenceService;

        /// <inheritdoc/>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <inheritdoc/>
        public IForeignDataSubmission Delete(Guid foreignDataId)
        {
            if (foreignDataId == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(foreignDataId));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.ManageForeignData);
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();

                    using (var tx = context.BeginTransaction())
                    {
                        var existing = context.Query<DbForeignDataStage>(o => o.Key == foreignDataId && o.ObsoletionTime == null).FirstOrDefault();
                        if (existing == null)
                        {
                            throw new KeyNotFoundException(foreignDataId.ToString());
                        }

                        existing.ObsoletionTime = DateTimeOffset.Now;
                        existing.ObsoletedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal);

                        if (existing.RejectStreamKey.HasValue)
                        {
                            this.m_streamManager.Remove(existing.RejectStreamKey.Value);
                        }

                        try
                        {
                            this.m_streamManager.Remove(existing.SourceStreamKey);
                        }
                        catch (FileNotFoundException) { }

                        existing.RejectStreamKey = null;
                        existing.SourceStreamKey = Guid.Empty;
                        context.Update(existing);
                        var issues = context.Query<DbForeignDataIssue>(o => o.SourceKey == foreignDataId).ToArray();
                        context.DeleteAll<DbForeignDataIssue>(o => o.SourceKey == foreignDataId);
                        var parameters = context.Query<DbForeignDataStageParameter>(o => o.SourceKey == foreignDataId).ToArray();
                        context.DeleteAll<DbForeignDataStageParameter>(o => o.SourceKey == foreignDataId);

                        tx.Commit();

                        return new AdoForeignDataSubmission(existing, issues, parameters, this.m_streamManager);
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_MANAGE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public IForeignDataSubmission Execute(Guid foreignDataId)
        {
            if (foreignDataId == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(foreignDataId));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.ManageForeignData);
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();

                    var existing = context.Query<DbForeignDataStage>(o => o.Key == foreignDataId && o.ObsoletionTime == null).FirstOrDefault();
                    if (existing == null)
                    {
                        throw new KeyNotFoundException(foreignDataId.ToString());
                    }
                    else if (existing.Status != ForeignDataStatus.Scheduled)
                    {
                        throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_INVALID_STATE));
                    }

                    existing.Status = ForeignDataStatus.Running;
                    existing.UpdatedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal);
                    existing.UpdatedTime = DateTimeOffset.Now;
                    context.Update(existing);

                    var parameters = context.Query<DbForeignDataStageParameter>(o => o.SourceKey == existing.Key).ToDictionary(o => o.Name, o => o.Value);

                    var foreignDataMap = this.m_foreignDataMapRepository.Get(existing.ForeignDataMapKey);
                    if (foreignDataMap == null)
                    {
                        throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_MAP_NOT_FOUND, new { map = existing.ForeignDataMapKey }));
                    }

                    // Run
                    if (ForeignDataImportUtil.Current.TryGetDataFormat(Path.GetExtension(existing.Name), out var dataFormat))
                    {
                        existing.Status = ForeignDataStatus.CompletedSuccessfully;
                        try
                        {
                            using (var rejectStream = new MemoryStream())
                            {
                                using (var sourceFile = dataFormat.Open(this.m_streamManager.Get(existing.SourceStreamKey)))
                                using (var rejectFile = dataFormat.Open(rejectStream))
                                {
                                    var subsetNames = sourceFile.GetSubsetNames().ToArray();

                                    // Callback function for status of this import
                                    var progressPerSubset = 1 / (float)subsetNames.Length;
                                    var currentSubsetOffset = 0;
                                    EventHandler<ProgressChangedEventArgs> progressRelay = (o, e) => this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(nameof(AdoForeignDataManager), currentSubsetOffset * progressPerSubset + e.Progress * progressPerSubset, $"{String.Format(UserMessages.IMPORTING_NAME, existing.Name)} ({e.State})"));
                                    if (this.m_importService is IReportProgressChanged irpc)
                                    {
                                        irpc.ProgressChanged += progressRelay;
                                    }

                                    for (currentSubsetOffset = 0; currentSubsetOffset < subsetNames.Length; currentSubsetOffset++)
                                    {
                                        var sourceName = subsetNames[currentSubsetOffset];
                                        var map = foreignDataMap.Maps.FirstOrDefault(o => (o.Source ?? String.Empty) == sourceName);
                                        if (map != null)
                                        {
                                            using (var reader = sourceFile.CreateReader(sourceName))
                                            using (var rejectWriter = rejectFile.CreateWriter(sourceName))
                                            {
                                                var issues = this.m_importService.Import(map, parameters, reader, rejectWriter, TransactionMode.Commit).ToArray();

                                                foreach (var itm in issues)
                                                {
                                                    if (itm != null)
                                                    {
                                                        context.Insert(new DbForeignDataIssue()
                                                        {
                                                            IssueTypeKey = itm.TypeKey,
                                                            Key = Guid.NewGuid(),
                                                            SourceKey = foreignDataId,
                                                            LogicalId = itm.Id,
                                                            Priority = itm.Priority,
                                                            Text = itm.Text
                                                        });
                                                    }

                                                    if (itm.Priority != DetectedIssuePriorityType.Information)
                                                    {
                                                        existing.Status = ForeignDataStatus.CompletedWithErrors;
                                                    }
                                                }

                                            }
                                        }
                                    } // end processing the 

                                    if (this.m_importService is IReportProgressChanged irpc2)
                                    {
                                        irpc2.ProgressChanged -= progressRelay;
                                    }

                                    // Reject stream
                                    if (rejectStream.Position > 0)
                                    {
                                        rejectStream.Seek(0, SeekOrigin.Begin);
                                        existing.RejectStreamKey = this.m_streamManager.Add(rejectStream);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            var exceptionString = String.Empty;
                            var ce = e;
                            while (ce != null)
                            {
                                exceptionString += ce.Message;
                                ce = ce.InnerException;
                                if (ce != null)
                                {
                                    exceptionString += ". CAUSE: ";
                                }
                            }
                            existing.Status = ForeignDataStatus.CompletedWithErrors;
                            context.Insert(new DbForeignDataIssue()
                            {
                                IssueTypeKey = DetectedIssueKeys.OtherIssue,
                                Key = Guid.NewGuid(),
                                SourceKey = foreignDataId,
                                LogicalId = "fatal",
                                Priority = DetectedIssuePriorityType.Error,
                                Text = exceptionString
                            });
                            this.m_tracer.TraceError("Could not execute import for {0} - {1}", foreignDataId, e);
                        }
                        finally
                        {
                            existing.UpdatedByKey = context.ContextId;
                            existing.UpdatedTime = DateTimeOffset.Now;
                            existing = context.Update(existing);
                        }
                    }
                    else
                    {
                        existing.Status = ForeignDataStatus.Rejected;
                        existing.UpdatedByKey = context.ContextId;
                        existing.UpdatedTime = DateTimeOffset.Now;
                        context.Update(existing);
                    }

                    return new AdoForeignDataSubmission(existing, context.Query<DbForeignDataIssue>(o => o.SourceKey == foreignDataId).ToArray(), context.Query<DbForeignDataStageParameter>(o => o.SourceKey == foreignDataId).ToArray(), this.m_streamManager);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_MANAGE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public IOrmResultSet ExecuteQueryOrm(DataContext context, Expression<Func<IForeignDataSubmission, bool>> query)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (query == null)
            {
                throw new ArgumentNullException(nameof(query), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            var expression = this.m_modelMapper.MapModelExpression<IForeignDataSubmission, DbForeignDataStage, bool>(query, false);
            if (!query.ToString().Contains(nameof(BaseEntityData.ObsoletionTime)))
            {
                var obsoletionReference = Expression.MakeBinary(ExpressionType.Equal, Expression.MakeMemberAccess(expression.Parameters[0], typeof(DbNonVersionedBaseData).GetProperty(nameof(DbNonVersionedBaseData.ObsoletionTime))), Expression.Constant(null));
                expression = Expression.Lambda<Func<DbForeignDataStage, bool>>(Expression.MakeBinary(ExpressionType.AndAlso, obsoletionReference, expression.Body), expression.Parameters);
            }

            return context.Query<DbForeignDataStage>(expression);
        }

        /// <inheritdoc/>
        public IQueryResultSet<IForeignDataSubmission> Find(Expression<Func<IForeignDataSubmission, bool>> query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            return new MappedQueryResultSet<IForeignDataSubmission>(this).Where(query);
        }

        /// <inheritdoc/>
        public IForeignDataSubmission Get(Guid foreignDataId)
        {
            if (foreignDataId == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(foreignDataId));
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    context.Open();
                    var existing = context.Query<DbForeignDataStage>(o => o.Key == foreignDataId && o.ObsoletionTime == null).FirstOrDefault();
                    if (existing == null)
                    {
                        throw new KeyNotFoundException(foreignDataId.ToString());
                    }
                    return new AdoForeignDataSubmission(existing, context.Query<DbForeignDataIssue>(o => o.SourceKey == foreignDataId).ToArray(), context.Query<DbForeignDataStageParameter>(o => o.SourceKey == foreignDataId), this.m_streamManager);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_MANAGE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public IForeignDataSubmission Get(DataContext context, Guid key) => new AdoForeignDataSubmission(context.FirstOrDefault<DbForeignDataStage>(o => o.Key == key && o.ObsoletionTime == null), context.Query<DbForeignDataIssue>(o => o.SourceKey == key).ToArray(), context.Query<DbForeignDataStageParameter>(o => o.SourceKey == key).ToArray(), this.m_streamManager);

        /// <inheritdoc/>
        public LambdaExpression MapExpression<TReturn>(Expression<Func<IForeignDataSubmission, TReturn>> sortExpression)
        {
            return this.m_modelMapper.MapModelExpression<IForeignDataSubmission, DbForeignDataStage, TReturn>(sortExpression, false);

        }

        /// <summary>
        /// Schedule the foreign data identifier
        /// </summary>
        /// <param name="foreignDataId">The foreign data to be scheduled</param>
        /// <returns>The foreidn data identifier</returns>
        public IForeignDataSubmission Schedule(Guid foreignDataId)
        {
            if (foreignDataId == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(foreignDataId));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.ManageForeignData);
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();

                    var existing = context.Query<DbForeignDataStage>(o => o.Key == foreignDataId && o.ObsoletionTime == null).FirstOrDefault();
                    if (existing == null)
                    {
                        throw new KeyNotFoundException(foreignDataId.ToString());
                    }

                    switch (existing.Status)
                    {
                        case ForeignDataStatus.Rejected:
                        case ForeignDataStatus.CompletedSuccessfully:
                        case ForeignDataStatus.Running:
                            throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_INVALID_STATE));
                        case ForeignDataStatus.CompletedWithErrors:
                        case ForeignDataStatus.Staged:
                            using (var tx = context.BeginTransaction())
                            {
                                context.EstablishProvenance(AuthenticationContext.Current.Principal);
                                context.DeleteAll<DbForeignDataIssue>(o => o.SourceKey == foreignDataId);
                                var issues = this.ValidateInternal(context, existing).ToArray();
                                var parameters = context.Query<DbForeignDataStageParameter>(o => o.SourceKey == foreignDataId).ToArray();
                                tx.Commit();
                                return new AdoForeignDataSubmission(existing, issues.ToArray(), parameters.ToArray(), this.m_streamManager);
                            }
                    }

                    return new AdoForeignDataSubmission(existing, context.Query<DbForeignDataIssue>(o => o.SourceKey == foreignDataId).ToArray(), context.Query<DbForeignDataStageParameter>(o => o.SourceKey == foreignDataId).ToArray(), this.m_streamManager);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_MANAGE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public IForeignDataSubmission Stage(Stream inputStream, string name, string description, Guid foreignDataMapKey, IDictionary<String, String> parameterValues)
        {
            if (inputStream == null)
            {
                throw new ArgumentNullException(nameof(inputStream));
            }
            else if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            else if (foreignDataMapKey == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(foreignDataMapKey));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.ManageForeignData);
            var sourceFileKey = this.m_streamManager.Add(inputStream);
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();

                    using (var tx = context.BeginTransaction())
                    {

                        var stageDataRecord = new DbForeignDataStage()
                        {
                            CreatedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal),
                            CreationTime = DateTimeOffset.Now,
                            Name = name,
                            Description = description,
                            Status = ForeignDataStatus.Staged,
                            SourceStreamKey = sourceFileKey,
                            ForeignDataMapKey = foreignDataMapKey
                        };


                        // Ensure that we understand the format
                        stageDataRecord = context.Insert(stageDataRecord);
                        var dbParmValues = parameterValues.Select(kv => context.Insert(new DbForeignDataStageParameter()
                        {
                            SourceKey = stageDataRecord.Key,
                            Name = kv.Key,
                            Value = kv.Value
                        })).ToArray();
                        var issues = this.ValidateInternal(context, stageDataRecord).ToArray();

                        tx.Commit();

                        return new AdoForeignDataSubmission(stageDataRecord, issues, dbParmValues, this.m_streamManager);
                    }
                }
            }
            catch (DbException e)
            {
                this.m_streamManager.Remove(sourceFileKey);
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_streamManager.Remove(sourceFileKey);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_MANAGE_ERROR), e);
            }
        }

        /// <inheritdoc/>
        public IForeignDataSubmission ToModelInstance(DataContext context, object result)
        {
            if (result is DbForeignDataStage dbfds)
            {
                return new AdoForeignDataSubmission(dbfds, context.Query<DbForeignDataIssue>(o => o.SourceKey == dbfds.Key).ToArray(), context.Query<DbForeignDataStageParameter>(o => o.SourceKey == dbfds.Key).ToArray(), this.m_streamManager);
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

        /// <summary>
        /// Validate the record 
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="stageDataRecord">The staged data record</param>
        private IEnumerable<DbForeignDataIssue> ValidateInternal(DataContext context, DbForeignDataStage stageDataRecord)
        {
            if (stageDataRecord.Status == ForeignDataStatus.Rejected)
            {
                throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_INVALID_STATE));
            }
            else if (stageDataRecord.Status != ForeignDataStatus.Staged &&
                stageDataRecord.Status != ForeignDataStatus.CompletedWithErrors)
            {
                yield break;
            }

            // Clear out existing issues
            context.DeleteAll<DbForeignDataIssue>(o => o.SourceKey == stageDataRecord.Key);

            var parameters = context.Query<DbForeignDataStageParameter>(o => o.SourceKey == stageDataRecord.Key).ToDictionary(o => o.Name, o => o.Value);

            // Attempt to get the foreign data map 
            var foreignDataMap = this.m_foreignDataMapRepository.Get(stageDataRecord.ForeignDataMapKey);
            if (foreignDataMap == null)
            {
                stageDataRecord.Status = ForeignDataStatus.Rejected;
                yield return context.Insert(new DbForeignDataIssue()
                {
                    Priority = DetectedIssuePriorityType.Error,
                    IssueTypeKey = DetectedIssueKeys.InvalidDataIssue,
                    LogicalId = "nomap",
                    Key = Guid.NewGuid(),
                    SourceKey = stageDataRecord.Key,
                    Text = this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_MAP_NOT_FOUND, new { map = stageDataRecord.ForeignDataMapKey })
                });
            }
            else if (!ForeignDataImportUtil.Current.TryGetDataFormat(Path.GetExtension(stageDataRecord.Name), out var dataFormat))
            {
                stageDataRecord.Status = ForeignDataStatus.Rejected;
                yield return context.Insert(new DbForeignDataIssue()
                {
                    IssueTypeKey = DetectedIssueKeys.InvalidDataIssue,
                    Key = Guid.NewGuid(),
                    LogicalId = "noformat",
                    Priority = DetectedIssuePriorityType.Error,
                    SourceKey = stageDataRecord.Key,
                    Text = this.m_localizationService.GetString(ErrorMessageStrings.FOREIGN_DATA_UNSUPPORTED_FORMAT, new { format = Path.GetExtension(stageDataRecord.Name) })
                });
            }
            else
            {
                var highestPriority = DetectedIssuePriorityType.Information;
                using (var foreignFile = dataFormat.Open(this.m_streamManager.Get(stageDataRecord.SourceStreamKey)))
                {
                    foreach (var subsetName in foreignFile.GetSubsetNames())
                    {
                        var map = foreignDataMap.Maps.FirstOrDefault(o => (o.Source ?? String.Empty) == subsetName);
                        if (map != null)
                        {
                            using (var reader = foreignFile.CreateReader(subsetName))
                            {
                                foreach (var dte in this.m_importService.Validate(map, parameters, reader))
                                {
                                    if (dte.Priority < highestPriority)
                                    {
                                        highestPriority = dte.Priority;
                                    }

                                    yield return context.Insert(new DbForeignDataIssue()
                                    {
                                        Priority = dte.Priority,
                                        IssueTypeKey = dte.TypeKey,
                                        LogicalId = dte.Id,
                                        Text = dte.Text,
                                        Key = Guid.NewGuid(),
                                        SourceKey = stageDataRecord.Key
                                    });
                                }
                            }
                        }
                    }
                }

                switch (highestPriority)
                {
                    case DetectedIssuePriorityType.Error:
                        stageDataRecord.Status = ForeignDataStatus.Rejected;
                        break;
                    case DetectedIssuePriorityType.Warning:
                        stageDataRecord.Status = ForeignDataStatus.Staged;
                        break;
                    case DetectedIssuePriorityType.Information:
                        stageDataRecord.Status = ForeignDataStatus.Scheduled;
                        break;
                }
            }

            stageDataRecord.UpdatedTime = DateTimeOffset.Now;
            stageDataRecord.UpdatedByKey = context.ContextId;
            context.Update(stageDataRecord);

        }


        /// <summary>
        /// Return sql statement for version filter
        /// </summary>
        public SqlStatement GetCurrentVersionFilter(string tableAlias)
        {
            var tableMap = TableMapping.Get(typeof(DbForeignDataStage));
            var obsltCol = tableMap.GetColumn(nameof(DbForeignDataStage.ObsoletionTime));
            return new SqlStatement($"{tableAlias ?? tableMap.TableName}.{obsltCol.Name} IS NULL");
        }

        /// <inheritdoc/>
        public void Trim(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder)
        {
            foreach (var itm in context.Query<DbForeignDataStage>(o => o.ObsoletionTime != null && o.ObsoletionTime < deletedCutoff).ToArray())
            {
                context.DeleteAll<DbForeignDataStageParameter>(o => o.SourceKey == itm.Key);
                context.DeleteAll<DbForeignDataIssue>(o => o.SourceKey == itm.Key);
                context.Delete(itm);
            }

            // Now mark the old (already imported) versions for deletion
            context.UpdateAll<DbForeignDataStage>(o => o.ObsoletionTime == null && o.Status != ForeignDataStatus.Staged && o.CreationTime < oldVersionCutoff, o => o.ObsoletionTime == DateTimeOffset.Now, o => o.ObsoletedByKey == context.ContextId);
        }
    }
}
