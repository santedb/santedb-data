using SanteDB.Core.Jobs;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Configuration;
using SanteDB.Persistence.Data.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        public string Description => "Optimizes primary database";

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
                for(int i = 0; i < dataConnections.Length; i++)
                {
                    var configuration = dataConnections[i];
                    if(optimizedConnections.Contains(configuration.ReadWriteConnectionString))
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
            catch(Exception ex)
            {
                this.m_jobState.SetState(this, JobStateType.Aborted);
                this.m_jobState.SetProgress(this, ex.Message, 1.0f);
            }
        }
    }
}
