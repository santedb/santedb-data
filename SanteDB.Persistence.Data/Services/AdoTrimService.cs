using DocumentFormat.OpenXml.Wordprocessing;
using SanteDB.Core.Data;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.Attributes;
using SanteDB.Persistence.Data.Model;
using SanteDB.Persistence.Data.Model.Acts;
using SanteDB.Persistence.Data.Model.Concepts;
using SanteDB.Persistence.Data.Model.DataType;
using SanteDB.Persistence.Data.Model.Entities;
using SanteDB.Persistence.Data.Model.Extensibility;
using SanteDB.Persistence.Data.Model.Roles;
using SanteDB.Persistence.Data.Model.Security;
using SanteDB.Persistence.Data.Services.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Implementation of a database trimming service
    /// </summary>
    public class AdoTrimService : IAdoTrimProvider, IReportProgressChanged
    {

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoTrimService));

        /// <summary>
        /// Progress has changed
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// Trim all entities
        /// </summary>
        private void TrimEntities(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder)
        {

            // Grab the highest version sequence of an object that was created at the old version cutoff date - this is the old relationship sequence we're deleting
            var versionSequenceTrim = context.Query<DbEntityVersion>(o => o.CreationTime <= oldVersionCutoff && !o.IsHeadVersion).OrderByDescending(o => o.VersionSequenceId).Select(o => o.VersionSequenceId).FirstOrDefault();

            long nrec = 0l;
            Guid[] purgeKeys = null;
            do
            {
                purgeKeys = context.Query<DbEntityAddress>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim).Select(o => o.Key).Take(1000).ToArray();
                context.DeleteAll<DbEntityAddressComponent>(o => purgeKeys.Contains(o.SourceKey));
                context.DeleteAll<DbEntityAddress>(o => purgeKeys.Contains(o.Key));
                nrec += purgeKeys.LongLength;
            } while (purgeKeys.Length > 0);
            this.m_tracer.TraceInfo("Trimmed {0} entity addresses", nrec);

            nrec = 0l;
            do
            {
                purgeKeys = context.Query<DbEntityName>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim).Select(o => o.Key).Take(1000).ToArray();
                context.DeleteAll<DbEntityNameComponent>(o => purgeKeys.Contains(o.SourceKey));
                context.DeleteAll<DbEntityName>(o => purgeKeys.Contains(o.Key));
            } while (purgeKeys.Length > 0);
            this.m_tracer.TraceInfo("Trimmed {0} entity names", nrec);

            nrec = context.DeleteAll<DbTelecomAddress>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} telecoms", nrec);
            nrec = context.DeleteAll<DbEntityIdentifier>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} identifiers", nrec);
            nrec = context.DeleteAll<DbEntityNote>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} entity notes", nrec);
            nrec = context.DeleteAll<DbEntityExtension>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} entity extensions", nrec);
            nrec = context.DeleteAll<DbEntityRelationship>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} entity relationships", nrec);
            nrec = context.DeleteAll<DbEntitySecurityPolicy>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} entity policies", nrec);
            nrec = context.DeleteAll<DbPlaceService>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} entity services", nrec);
            nrec = context.DeleteAll<DbPersonLanguageCommunication>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} person communications", nrec);

            nrec = 0;

            // Prepare 
            var purgeKeySet = context.Query<DbEntityVersion>(o => o.VersionSequenceId < versionSequenceTrim && o.ObsoletionTime != null && !o.IsHeadVersion).OrderBy(o=>o.VersionKey).Select(o => o.VersionKey);
            var totalRec = purgeKeySet.Count();
            purgeKeySet = purgeKeySet.Take(10_000); // 10,000 recs at a time
            this.m_tracer.TraceInfo("Will purge {0} EntityVersion records", totalRec);

            do
            {
                
                // First we want to set the replaces version to NULL for any version in our key list
                var stepRec = context.UpdateAll<DbEntityVersion>(o => purgeKeySet.Contains(o.ReplacesVersionKey.Value), o => o.ReplacesVersionKey == null);
                this.m_tracer.TraceInfo("Redirected {0} version meta", stepRec);
                stepRec = context.DeleteAll<DbUserEntity>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} user entity versions", stepRec);
                stepRec = context.DeleteAll<DbProvider>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} provider versions", stepRec);
                stepRec = context.DeleteAll<DbPatient>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} patient versions", stepRec);
                stepRec = context.DeleteAll<DbPerson>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} person versions", stepRec);
                stepRec = context.DeleteAll<DbPlace>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} place versions", stepRec);
                stepRec = context.DeleteAll<DbOrganization>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} organization versions", stepRec);
                stepRec = context.DeleteAll<DbDeviceEntity>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} device entity versions", stepRec);
                stepRec = context.DeleteAll<DbApplicationEntity>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} application entity versions", stepRec);
                stepRec = context.DeleteAll<DbManufacturedMaterial>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} manufactured material versions", stepRec);
                stepRec = context.DeleteAll<DbContainer>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} container versions", stepRec);
                stepRec = context.DeleteAll<DbMaterial>(o => purgeKeySet.Contains(o.ParentKey));
                this.m_tracer.TraceInfo("Purged {0} material versions", stepRec);
                stepRec = context.DeleteAll<DbEntityVersion>(o => purgeKeySet.Contains(o.VersionKey));
                this.m_tracer.TraceInfo("Purged {0} entity versions", stepRec);
                nrec += stepRec;

                this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(nameof(AdoTrimService), ((float)nrec / (float)totalRec) * 0.3f, $"Purging Entity Versions ({nrec} or {totalRec})"));
            } while (purgeKeys.Any());

            auditBuilder.WithAuditableObjects(new AuditableObject()
            {
                IDTypeCode = AuditableObjectIdType.NotSpecified,
                CustomIdTypeCode = new AuditCode("EntityVersion", "SanteDBResource"),
                LifecycleType = AuditableObjectLifecycle.PermanentErasure,
                QueryData = $"o.VersionSequenceId < {versionSequenceTrim} && o.ObsoletionTime != null && !o.IsHeadVersion",
                Role = AuditableObjectRole.Table,
                Type = AuditableObjectType.SystemObject
            });

            this.m_tracer.TraceInfo("Purged {0} old entity versions", nrec);

        }


        /// <summary>
        /// Trim concepts
        /// </summary>
        private void TrimConcepts(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder)
        {
            // Grab the last version that was created at the cutoff date
            var versionSequenceTrim = context.Query<DbConceptVersion>(o => o.CreationTime <= oldVersionCutoff && o.IsHeadVersion).OrderByDescending(o => o.VersionSequenceId).Select(o => o.VersionSequenceId).FirstOrDefault();

            long nrec = 0l;
            nrec = context.DeleteAll<DbConceptName>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} concept names", nrec);
            nrec = context.DeleteAll<DbConceptExtension>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} concept extensions", nrec);
            nrec = context.DeleteAll<DbConceptReferenceTerm>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} concept reference terms", nrec);
            nrec = context.DeleteAll<DbConceptRelationship>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} concept relationships", nrec);


            nrec = 0;

            // Prepare 
            var purgeKeySet = context.Query<DbConceptVersion>(o => o.VersionSequenceId < versionSequenceTrim && o.ObsoletionTime != null && !o.IsHeadVersion).Select(o => o.VersionKey);
            var totalRec = purgeKeySet.Count();
            purgeKeySet = purgeKeySet.Take(10_000); // 10,000 recs at a time
            this.m_tracer.TraceInfo("Will purge {0} Concept Version records", totalRec);

            do
            {
                // First we want to set the replaces version to NULL for any version in our key list
                context.UpdateAll<DbConceptVersion>(o => purgeKeySet.Contains(o.ReplacesVersionKey.Value), o => o.ReplacesVersionKey == null);
                nrec += context.DeleteAll<DbConceptVersion>(o => purgeKeySet.Contains(o.VersionKey));
                this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(nameof(AdoTrimService), ((float)nrec / (float)totalRec) * 0.3f + 0.6f, $"Purging Concept Versions ({nrec} or {totalRec})"));

            } while (purgeKeySet.Any());
            this.m_tracer.TraceInfo("Purged {0} old concept versions", nrec);

            auditBuilder.WithAuditableObjects(new AuditableObject()
            {
                IDTypeCode = AuditableObjectIdType.NotSpecified,
                CustomIdTypeCode = new AuditCode("ConceptVersion", "SanteDBResource"),
                LifecycleType = AuditableObjectLifecycle.PermanentErasure,
                QueryData = $"o.VersionSequenceId < {versionSequenceTrim} && o.ObsoletionTime != null && !o.IsHeadVersion",
                Role = AuditableObjectRole.Table,
                Type = AuditableObjectType.SystemObject
            });
        }

        /// <summary>
        /// Trim acts
        /// </summary>
        private void TrimActs(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder)
        {
            var versionSequenceTrim = context.Query<DbActVersion>(o => o.CreationTime <= oldVersionCutoff && o.IsHeadVersion).OrderByDescending(o => o.VersionSequenceId).Select(o => o.VersionSequenceId).FirstOrDefault();

            long nRec = 0l;
            nRec = context.DeleteAll<DbActIdentifier>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} act identifiers", nRec);
            nRec = context.DeleteAll<DbActNote>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} act notes", nRec);
            nRec = context.DeleteAll<DbActExtension>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} act extensions", nRec);
            nRec = context.DeleteAll<DbActRelationship>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} act relationships", nRec);
            nRec = context.DeleteAll<DbActParticipation>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} act participations", nRec);
            nRec = context.DeleteAll<DbActSecurityPolicy>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} act policies", nRec);
            nRec = context.DeleteAll<DbPatientEncounterArrangement>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim);
            this.m_tracer.TraceInfo("Trimmed {0} act encounter arrangements", nRec);


            nRec = 0;

            // Prepare 
            var purgeKeySet = context.Query<DbActVersion>(o => o.VersionSequenceId < versionSequenceTrim && o.ObsoletionTime != null && !o.IsHeadVersion).Select(o => o.VersionKey);
            var totalRec = purgeKeySet.Count();
            purgeKeySet = purgeKeySet.Take(10_000); // 10,000 recs at a time
            this.m_tracer.TraceInfo("Will purge {0} ActVersion records", totalRec);
            do
            {
                // First we want to set the replaces version to NULL for any version in our key list
                context.UpdateAll<DbActVersion>(o => purgeKeySet.Contains(o.ReplacesVersionKey.Value), o => o.ReplacesVersionKey == null);
                context.DeleteAll<DbNarrative>(o => purgeKeySet.Contains(o.ParentKey));
                context.DeleteAll<DbQuantityObservation>(o => purgeKeySet.Contains(o.ParentKey));
                context.DeleteAll<DbTextObservation>(o => purgeKeySet.Contains(o.ParentKey));
                context.DeleteAll<DbCodedObservation>(o => purgeKeySet.Contains(o.ParentKey));
                context.DeleteAll<DbDateObservation>(o => purgeKeySet.Contains(o.ParentKey));
                context.DeleteAll<DbObservation>(o => purgeKeySet.Contains(o.ParentKey));
                context.DeleteAll<DbProcedure>(o => purgeKeySet.Contains(o.ParentKey));
                context.DeleteAll<DbSubstanceAdministration>(o => purgeKeySet.Contains(o.ParentKey));
                context.DeleteAll<DbCarePlan>(o => purgeKeySet.Contains(o.ParentKey));
                context.DeleteAll<DbControlAct>(o => purgeKeySet.Contains(o.ParentKey));
                context.DeleteAll<DbPatientEncounter>(o => purgeKeySet.Contains(o.ParentKey));
                nRec += context.DeleteAll<DbActVersion>(o => purgeKeySet.Contains(o.VersionKey));
                this.ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(nameof(AdoTrimService), ((float)nRec / (float)totalRec) * 0.3f + 0.3f, $"Purging Act Versions ({nRec} or {totalRec})"));
            } while (purgeKeySet.Any());


            auditBuilder.WithAuditableObjects(new AuditableObject()
            {
                IDTypeCode = AuditableObjectIdType.NotSpecified,
                CustomIdTypeCode = new AuditCode("ActVersion", "SanteDBResource"),
                LifecycleType = AuditableObjectLifecycle.PermanentErasure,
                QueryData = $"o.VersionSequenceId < {versionSequenceTrim} && o.ObsoletionTime != null && !o.IsHeadVersion",
                Role = AuditableObjectRole.Table,
                Type = AuditableObjectType.SystemObject
            });

            this.m_tracer.TraceInfo("Purged {0} old act versions", nRec);
        }

        /// <inheritdoc/>
        public void Trim(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder)
        {
            this.TrimEntities(context, oldVersionCutoff, deletedCutoff, auditBuilder);
            this.TrimActs(context, oldVersionCutoff, deletedCutoff, auditBuilder);
            this.TrimConcepts(context, oldVersionCutoff, deletedCutoff, auditBuilder);
        }
    }
}
