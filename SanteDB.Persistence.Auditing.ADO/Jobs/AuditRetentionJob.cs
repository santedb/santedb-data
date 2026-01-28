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
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.i18n;
using SanteDB.Core.Jobs;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Services;
using SanteDB.Persistence.Auditing.ADO.Configuration;
using SanteDB.Persistence.Auditing.ADO.Data.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SanteDB.Persistence.Auditing.ADO.Jobs
{
    /// <summary>
    /// A job which performs audit retention activities
    /// </summary>
    public class AuditRetentionJob : IJob
    {
        /// <summary>
        /// JOB ID
        /// </summary>
        public static readonly Guid JOB_ID = new Guid("C0FECC02-FCA7-43C7-A54D-AE7A8FE809EC");
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AuditRetentionJob));
        private readonly IJobStateManagerService m_jobStateManager;
        private readonly AdoAuditConfigurationSection m_configuration;
        private readonly PolicyValueTimeSpan m_retentionPeriod;
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// DI constructor
        /// </summary>
        public AuditRetentionJob(IConfigurationManager configurationManager, ILocalizationService localizationService, IJobStateManagerService jobStateManagerService)
        {
            this.m_jobStateManager = jobStateManagerService;
            this.m_configuration = configurationManager.GetSection<AdoAuditConfigurationSection>();
            this.m_retentionPeriod = configurationManager.GetSection<SecurityConfigurationSection>().GetSecurityPolicy(Core.Configuration.SecurityPolicyIdentification.AuditRetentionTime, new PolicyValueTimeSpan(new TimeSpan(30, 0, 0, 0)));
            this.m_localizationService = localizationService;
        }

        /// <inheritdoc/>
        public Guid Id => JOB_ID;

        /// <inheritdoc/>
        public string Name => "Audit Log Retention";

        /// <inheritdoc/>
        public string Description => "Removes old audits from the database according to the configured retention policies on the server";

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
                this.m_jobStateManager.SetState(this, JobStateType.Running);

                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);
                    using (var tx = context.BeginTransaction())
                    {

                        // Delete all audits beyond the cutoff
                        var cutoff = DateTimeOffset.Now.Subtract(this.m_retentionPeriod.Value);
                        this.m_tracer.TraceInfo("Pruning audits older than {0}", cutoff);
                        var auditsToRetain = context.Query<DbAuditEventData>(o => o.CreationTime < cutoff).Select(o => o.Key).ToArray();

                        // Prune all actors and what-not for the audits
                        var batch = auditsToRetain.Take(50);
                        var ofs = 0;
                        while (batch.Any())
                        {
                            this.m_jobStateManager.SetProgress(this, this.m_localizationService.GetString(UserMessageStrings.DB_TRIM_OBJECTS, new { type = "AuditEvent" }), (float)ofs / (float)auditsToRetain.Length);
                            context.DeleteAll<DbAuditActorAssociation>(o => batch.Contains(o.SourceKey));
                            context.DeleteAll<DbAuditMetadata>(o => batch.Contains(o.AuditId));
                            var obsToDelete = context.Query<DbAuditObject>(o => batch.Contains(o.AuditId)).Select(o => o.Key).ToArray();
                            context.DeleteAll<DbAuditObjectData>(o => obsToDelete.Contains(o.ObjectId));
                            context.DeleteAll<DbAuditObject>(o => obsToDelete.Contains(o.Key));
                            context.DeleteAll<DbAuditEventData>(o => batch.Contains(o.Key));
                            ofs += batch.Count();
                            batch = auditsToRetain.Skip(ofs).Take(50);
                        }

                        tx.Commit();

                    }
                }

                this.m_jobStateManager.SetState(this, JobStateType.Completed);
            }
            catch (Exception ex)
            {
                this.m_tracer.TraceError("Error running Audit Retention Cleanup - {0}", ex.ToHumanReadableString());
                this.m_jobStateManager.SetState(this, JobStateType.Aborted, ex.ToHumanReadableString());
            }
        }
    }
}
