using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Jobs;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Security;
using SanteDB.Persistence.Data.Services.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Persistence.Data.Jobs
{
    /// <summary>
    /// A job which deletes old versions, trims data in the database, etc.
    /// </summary>
    public class TrimDatabaseJob : IJob
    {

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(TrimDatabaseJob));

        /// <summary>
        /// Job identifier 
        /// </summary>
        public static readonly Guid JOB_ID = Guid.Parse("CF61010D-8EDE-4574-9DEE-4983B52C781F");
        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IJobStateManagerService m_jobStateManager;
        private readonly ILocalizationService m_localizationService;
        private readonly IServiceManager m_serviceManager;
        private readonly IAuditService m_auditService;
        private bool m_cancelRequest = false;

        /// <summary>
        /// Configuration manager
        /// </summary>
        public TrimDatabaseJob(IConfigurationManager configurationManager,
            IJobStateManagerService jobStateManager,
            ILocalizationService localizationService,
            IServiceManager serviceManager,
            IAuditService auditService)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_jobStateManager = jobStateManager;
            this.m_localizationService = localizationService;
            this.m_serviceManager = serviceManager;
            this.m_auditService = auditService;
            // Apply defaults
            this.m_configuration.TrimSettings = this.m_configuration.TrimSettings ?? new AdoTrimSettings();
            this.m_configuration.TrimSettings.MaxDeletedDataRetention = this.m_configuration.TrimSettings.MaxDeletedDataRetention ?? new TimeSpan(30, 0, 0, 0);
            this.m_configuration.TrimSettings.MaxSessionRetention = this.m_configuration.TrimSettings.MaxSessionRetention ?? new TimeSpan(30, 0, 0, 0);
            this.m_configuration.TrimSettings.MaxOldVersionRetention = this.m_configuration.TrimSettings.MaxOldVersionRetention ?? new TimeSpan(30, 0, 0, 0);
        }

        /// <inheritdoc/>
        public Guid Id => JOB_ID;

        /// <inheritdoc/>
        public string Name => "Trim Database Job";

        /// <inheritdoc/>
        public string Description => "Trims old versions and obsolete data from the database";

        /// <inheritdoc/>
        public bool CanCancel => true;

        /// <inheritdoc/>
        public IDictionary<string, Type> Parameters => new Dictionary<String, Type>();

        /// <inheritdoc/>
        public void Cancel()
        {
            this.m_cancelRequest = true;
        }

        /// <inheritdoc/>
        public void Run(object sender, EventArgs e, object[] parameters)
        {

            var audit = this.m_auditService.Audit()
                .WithAction(Core.Model.Audit.ActionType.Execute)
                .WithEventIdentifier(Core.Model.Audit.EventIdentifierType.ApplicationActivity)
                .WithEventType(ExtendedAuditCodes.EventTypeDataManagement)
                .WithTimestamp(DateTimeOffset.Now)
                .WithLocalSource();

            try
            {

                this.m_cancelRequest = false;
                this.m_jobStateManager.SetState(this, JobStateType.Running);

                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open();
                    // First we want to trim old sessions
                    var cutoff = DateTimeOffset.Now.Subtract(this.m_configuration.TrimSettings.MaxSessionRetention.Value);
                    this.m_tracer.TraceInfo("Pruning sessions before {0}", cutoff);
                    using (var tx = context.BeginTransaction())
                    {
                        var delSessions = context.Query<DbSession>(o => o.NotAfter < cutoff).Select(o => o.Key).ToArray();
                        // HACK: The IN() statement can only have a certain number of elements so we want to chunk our delete 
                        var c = 0;
                        while (c < delSessions.Length)
                        {
                            var batch = delSessions.Skip(c).Take(50).ToArray();
                            context.DeleteAll<DbSessionClaim>(o => batch.Contains(o.SessionKey));
                            context.DeleteAll<DbSession>(o => batch.Contains(o.Key));

                            // Audit
                            audit.WithAuditableObjects(batch.Select(o => new AuditableObject()
                            {
                                CustomIdTypeCode = ExtendedAuditCodes.CustomIdTypeSession,
                                IDTypeCode = AuditableObjectIdType.Custom,
                                LifecycleType = AuditableObjectLifecycle.PermanentErasure,
                                ObjectId = o.ToString(),
                                Role = AuditableObjectRole.SecurityResource,
                                Type = AuditableObjectType.SystemObject
                            }));

                            c += batch.Length;
                            this.m_jobStateManager.SetProgress(this, this.m_localizationService.GetString(UserMessageStrings.DB_TRIM_SESSION), (float)c / delSessions.Length * 0.3f);
                        }
                        tx.Commit();
                    }

                    // Prune deleted objects
                    using (var tx = context.BeginTransaction())
                    {
                        var trimHelpers = this.m_serviceManager.GetServices().OfType<IAdoTrimProvider>().ToArray();

                        var c = 0;
                        foreach (var th in trimHelpers)
                        {
                            if (this.m_cancelRequest)
                            {
                                this.m_jobStateManager.SetState(this, JobStateType.Cancelled);
                                return;
                            }
                            this.m_jobStateManager.SetProgress(this, this.m_localizationService.GetString(UserMessageStrings.DB_TRIM_OBJECTS), c++ / (float)trimHelpers.Length * 0.7f + 0.3f);
                            th.Trim(context, DateTimeOffset.Now.Subtract(this.m_configuration.TrimSettings.MaxOldVersionRetention.Value), DateTimeOffset.Now.Subtract(this.m_configuration.TrimSettings.MaxDeletedDataRetention.Value), audit);
                        }

                        tx.Commit();
                    }
                }

                this.m_jobStateManager.SetState(this, JobStateType.Completed);
                audit.WithOutcome(OutcomeIndicator.Success);
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error running trim job: {0}", ex.ToHumanReadableString());
                this.m_jobStateManager.SetState(this, JobStateType.Aborted);
                audit.WithOutcome(OutcomeIndicator.SeriousFail);
            }
            finally
            {
                audit.Send();
            }
        }
    }
}
