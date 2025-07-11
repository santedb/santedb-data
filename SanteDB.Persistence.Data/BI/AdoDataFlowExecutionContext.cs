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
using SanteDB.BI.Datamart;
using SanteDB.BI.Datamart.DataFlow;
using SanteDB.BI.Diagnostics;
using SanteDB.BI.Model;
using SanteDB.Core.Configuration.Data;
using SanteDB.Core.Data;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.Migration;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Sys;
using System;
using System.Data.Common;
using System.Diagnostics.Tracing;

namespace SanteDB.Persistence.Data.BI
{
    /// <summary>
    /// An ADO.NET data flow execution context
    /// </summary>
    internal class AdoDataFlowExecutionContext : IDataFlowExecutionContext
    {
        private readonly IConfigurationManager m_configurationManager;
        private readonly AdoPersistenceConfigurationSection m_configuration;

        /// <inheritdoc/>
        public DataFlowExecutionPurposeType Purpose { get; }

        /// <inheritdoc/>
        public Guid Key { get; }

        /// <inheritdoc/>
        public IDatamart Datamart { get; }

        private readonly IDataStreamManager m_datastreamManager;

        /// <inheritdoc/>
        public IDataFlowDiagnosticSession DiagnosticSession { get; }

        /// <summary>
        /// Creates a new data flow execution context
        /// </summary>
        public AdoDataFlowExecutionContext(IConfigurationManager configurationManager, IDataStreamManager dataStreamManager, IDatamart datamartForExecution, DataFlowExecutionPurposeType biExecutionPurpose)
        {
            this.m_configurationManager = configurationManager;
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.Purpose = biExecutionPurpose;
            this.Datamart = datamartForExecution;
            this.m_datastreamManager = dataStreamManager;
            this.Key = Guid.NewGuid();
            if (biExecutionPurpose.HasFlag(DataFlowExecutionPurposeType.Diagnostics))
            {
                this.DiagnosticSession = new DataFlowDiagnosticSession(this);
            }
        }

        /// <inheritdoc/>
        internal IDataFlowExecutionContext LogStart()
        {
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);
                    if (context.Query<DbDatamartExecutionEntry>(o => o.Key == this.Key).Any())
                    {
                        throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(LogStart)));
                    }

                    context.Insert(new DbDatamartExecutionEntry()
                    {
                        CreatedByKey = context.EstablishProvenance(AuthenticationContext.Current.Principal),
                        DatamartKey = this.Datamart.Key.Value,
                        Key = this.Key,
                        Outcome = DataFlowExecutionOutcomeType.Unknown,
                        StartTime = DateTimeOffset.Now,
                        Purpose = this.Purpose
                    });
                    return this;
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(ErrorMessages.GENERAL_QUERY_ERROR, e);
            }
        }


        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);
                    var existing = context.Query<DbDatamartExecutionEntry>(o => o.Key == this.Key).FirstOrDefault();
                    if (existing == null)
                    {
                        return; // ignore the logging of the stop
                    }

                    existing.EndTime = DateTimeOffset.Now;
                    if (this.DiagnosticSession != null)
                    {
                        using (var tfs = new TemporaryFileStream())
                        {
                            this.DiagnosticSession.GetSessionData().Save(tfs);
                            tfs.Seek(0, System.IO.SeekOrigin.Begin);
                            existing.DiagnosticStreamKey = this.m_datastreamManager.Add(tfs);
                        }
                    }
                    new AdoBiDatamartExecutionEntry(context.Update(existing), this.m_configuration.Provider);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(ErrorMessages.GENERAL_QUERY_ERROR, e);
            }
        }


        /// <inheritdoc/>
        public void SetOutcome(DataFlowExecutionOutcomeType outcome)
        {
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);
                    var existing = context.Query<DbDatamartExecutionEntry>(o => o.Key == this.Key).FirstOrDefault();
                    if (existing == null)
                    {
                        throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(SetOutcome)));
                    }

                    existing.Outcome = outcome;

                    context.Update(existing);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(ErrorMessages.GENERAL_QUERY_ERROR, e);
            }
        }

        /// <inheritdoc/>
        public IDataIntegrator GetIntegrator(BiDataSourceDefinition dataSource)
        {

            ConnectionString connectionString = null;
            if (dataSource == null)
            {
                throw new ArgumentNullException(nameof(dataSource));
            }
            else if (String.IsNullOrEmpty(dataSource.ConnectionString))
            {
                // TODO: Move this to a configuration variable in the Ado
                connectionString = this.m_configurationManager.GetConnectionString(this.m_configuration.WarehouseConnectionStringSkel ?? "bi.marts") ??
                     new ConnectionString(this.m_configuration.Provider.Invariant, this.m_configuration.Provider.ConnectionString); // Allow the administrator to change or manually set this data to another server in config

                // We have to create a connection string?
                var dataConfigurationProvider = this.m_configuration.Provider.GetDataConfigurationProvider();
                // Does the database exist?
                connectionString.SetComponent(dataConfigurationProvider.Capabilities.NameSetting, dataSource.Name);
                connectionString = dataConfigurationProvider.CreateConnectionString(connectionString.ToDictionary());
                this.m_configurationManager.SetTransientConnectionString(dataSource.Id, connectionString);
            }
            else
            {
                connectionString = this.m_configurationManager.GetConnectionString(dataSource.ConnectionString);
                if (connectionString == null)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.MISSING_VALUE, dataSource.ConnectionString));
                }
            }

            // Does this datasource have a provider?
            return new OrmBiDataIntegrator(this, connectionString, dataSource);
        }

        /// <inheritdoc/>
        public IDataFlowLogEntry Log(EventLevel priority, string logText)
        {
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);

                    var logEntry = new AdoDatamartLogEntry(context.Insert(new DbDatamartLogEntry()
                    {
                        ExecutionContextId = this.Key,
                        Priority = priority,
                        Text = logText
                    }));
                    return logEntry;
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(ErrorMessages.GENERAL_QUERY_ERROR, e);
            }
        }

    }
}
