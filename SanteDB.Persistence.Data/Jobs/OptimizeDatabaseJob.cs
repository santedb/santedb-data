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
 * Date: 2023-5-19
 */
using SanteDB.Core.Jobs;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Persistence.Data.Jobs
{
    /// <summary>
    /// Optimize databases
    /// </summary>
    public class OptimizeDatabaseJob : IJob
    {
        private readonly IConfigurationManager m_configurationManager;
        private readonly IJobStateManagerService m_jobState;
        internal static readonly Guid ID = Guid.Parse("08122590-3BC0-4D2F-BCF7-419DE363E2E7");

        /// <summary>
        /// DI constructor
        /// </summary>
        public OptimizeDatabaseJob(IConfigurationManager configurationManager, IJobStateManagerService jobStateManager)
        {
            this.m_configurationManager = configurationManager;
            this.m_jobState = jobStateManager;
        }

        /// <inheritdoc/>
        public Guid Id => ID;

        /// <inheritdoc/>
        public string Name => "Optimize Databases";

        /// <inheritdoc/>
        public string Description => "Vaccuum, Re-index, and Analyze Database";

        /// <inheritdoc/>
        public bool CanCancel => false;

        /// <inheritdoc/>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>();

        /// <inheritdoc/>
        public void Cancel()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void Run(object sender, EventArgs e, object[] parameters)
        {
            try
            {
                this.m_jobState.SetState(this, JobStateType.Running);

                var dataConnections = this.m_configurationManager.Configuration.Sections.OfType<OrmConfigurationBase>().ToArray();
                var optimizedConnections = new HashSet<String>();
                for (int i = 0; i < dataConnections.Length; i++)
                {
                    var configuration = dataConnections[i];
                    if (optimizedConnections.Contains(configuration.ReadWriteConnectionString))
                    {
                        continue;
                    }

                    optimizedConnections.Add(configuration.ReadWriteConnectionString);

                    this.m_jobState.SetProgress(this, $"Optimizing {configuration.ReadWriteConnectionString}", (float)i / (float)dataConnections.Length);
                    using (var context = configuration.Provider.GetWriteConnection())
                    {
                        context.Open();
                        context.ExecuteNonQuery(configuration.Provider.StatementFactory.CreateSqlKeyword(OrmLite.Providers.SqlKeyword.Vacuum));
                        context.ExecuteNonQuery(configuration.Provider.StatementFactory.CreateSqlKeyword(OrmLite.Providers.SqlKeyword.Reindex));
                        context.ExecuteNonQuery(configuration.Provider.StatementFactory.CreateSqlKeyword(OrmLite.Providers.SqlKeyword.Analyze));
                    }
                }


                this.m_jobState.SetState(this, JobStateType.Completed);
            }
            catch (Exception ex)
            {
                this.m_jobState.SetState(this, JobStateType.Aborted, ex.ToHumanReadableString());
                this.m_jobState.SetProgress(this, ex.Message, 1.0f);
            }
        }
    }
}
