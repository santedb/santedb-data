using SanteDB.Core.Diagnostics;
using SanteDB.Core.Model.Audit;
using SanteDB.Core.Security.Audit;
using SanteDB.OrmLite;
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
    public class AdoTrimService : IAdoTrimProvider
    {

        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoTrimService));

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
                purgeKeys = context.Query<DbEntityAddress>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim).Select(o => o.Key).Take(100).ToArray();
                context.DeleteAll<DbEntityAddressComponent>(o => purgeKeys.Contains(o.SourceKey));
                context.DeleteAll<DbEntityAddress>(o => purgeKeys.Contains(o.Key));
                nrec += purgeKeys.LongLength;
            } while (purgeKeys.Length > 0);
            this.m_tracer.TraceInfo("Trimmed {0} entity addresses", nrec);

            nrec = 0l;
            do
            {
                purgeKeys = context.Query<DbEntityName>(o => o.ObsoleteVersionSequenceId != null && o.ObsoleteVersionSequenceId < versionSequenceTrim).Select(o => o.Key).Take(100).ToArray();
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
            do
            {
                purgeKeys = context.Query<DbEntityVersion>(o => o.VersionSequenceId < versionSequenceTrim && o.ObsoletionTime != null && !o.IsHeadVersion).Select(o => o.VersionKey).Take(100).ToArray();
                // First we want to set the replaces version to NULL for any version in our key list
                context.UpdateAll<DbEntityVersion>(o => purgeKeys.Contains(o.ReplacesVersionKey.Value), o => o.ReplacesVersionKey == null);
                context.DeleteAll<DbUserEntity>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbProvider>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbPatient>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbPerson>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbPlace>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbOrganization>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbDeviceEntity>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbApplicationEntity>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbManufacturedMaterial>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbContainer>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbMaterial>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbEntityVersion>(o => purgeKeys.Contains(o.VersionKey));

                auditBuilder.WithAuditableObjects(new AuditableObject()
                {
                    IDTypeCode = AuditableObjectIdType.NotSpecified,
                    CustomIdTypeCode = new AuditCode("EntityVersion", "SanteDBResource"),
                    LifecycleType = AuditableObjectLifecycle.PermanentErasure,
                    QueryData = $"o.VersionSequenceId < {versionSequenceTrim} && o.ObsoletionTime != null && !o.IsHeadVersion",
                    Role = AuditableObjectRole.Table,
                    Type = AuditableObjectType.SystemObject,
                    ObjectData = purgeKeys.Select(o => new ObjectDataExtension("vid", o.ToString())).ToList()
                });
                nrec += purgeKeys.LongLength;
            } while (purgeKeys.Length > 0);

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


            Guid[] purgeKeys = null;
            nrec = 0;
            do
            {
                purgeKeys = context.Query<DbConceptVersion>(o => o.VersionSequenceId < versionSequenceTrim && o.ObsoletionTime != null && !o.IsHeadVersion).Select(o => o.VersionKey).Take(100).ToArray();
                // First we want to set the replaces version to NULL for any version in our key list
                context.UpdateAll<DbConceptVersion>(o => purgeKeys.Contains(o.ReplacesVersionKey.Value), o => o.ReplacesVersionKey == null);
                context.DeleteAll<DbConceptVersion>(o => purgeKeys.Contains(o.VersionKey));

                auditBuilder.WithAuditableObjects(new AuditableObject()
                {
                    IDTypeCode = AuditableObjectIdType.NotSpecified,
                    CustomIdTypeCode = new AuditCode("ConceptVersion", "SanteDBResource"),
                    LifecycleType = AuditableObjectLifecycle.PermanentErasure,
                    QueryData = $"o.VersionSequenceId < {versionSequenceTrim} && o.ObsoletionTime != null && !o.IsHeadVersion",
                    Role = AuditableObjectRole.Table,
                    Type = AuditableObjectType.SystemObject,
                    ObjectData = purgeKeys.Select(o => new ObjectDataExtension("vid", o.ToString())).ToList()
                });
                nrec += purgeKeys.LongLength;
            } while (purgeKeys.Length > 0);
            this.m_tracer.TraceInfo("Purged {0} old concept versions", nrec);

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
            Guid[] purgeKeys = null;
            do
            {
                purgeKeys = context.Query<DbActVersion>(o => o.VersionSequenceId < versionSequenceTrim && o.ObsoletionTime != null && !o.IsHeadVersion).Select(o => o.VersionKey).Take(100).ToArray();
                // First we want to set the replaces version to NULL for any version in our key list
                context.UpdateAll<DbActVersion>(o => purgeKeys.Contains(o.ReplacesVersionKey.Value), o => o.ReplacesVersionKey == null);
                context.DeleteAll<DbNarrative>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbQuantityObservation>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbTextObservation>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbCodedObservation>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbDateObservation>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbObservation>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbProcedure>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbSubstanceAdministration>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbCarePlan>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbControlAct>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbPatientEn>(o => purgeKeys.Contains(o.ParentKey));
                context.DeleteAll<DbActVersion>(o => purgeKeys.Contains(o.VersionKey));

                auditBuilder.WithAuditableObjects(new AuditableObject()
                {
                    IDTypeCode = AuditableObjectIdType.NotSpecified,
                    CustomIdTypeCode = new AuditCode("ActVersion", "SanteDBResource"),
                    LifecycleType = AuditableObjectLifecycle.PermanentErasure,
                    QueryData = $"o.VersionSequenceId < {versionSequenceTrim} && o.ObsoletionTime != null && !o.IsHeadVersion",
                    Role = AuditableObjectRole.Table,
                    Type = AuditableObjectType.SystemObject,
                    ObjectData = purgeKeys.Select(o => new ObjectDataExtension("vid", o.ToString())).ToList()
                });
                nRec += purgeKeys.LongLength;
            } while (purgeKeys.Length > 0);

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
