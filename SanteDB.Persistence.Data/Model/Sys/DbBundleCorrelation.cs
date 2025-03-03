﻿using SanteDB.OrmLite.Attributes;
using SanteDB.Persistence.Data.Model.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Model.Sys
{
    /// <summary>
    /// Correlates the submission of bundles that have a <see cref="Bundle.CorrelationKey"/> 
    /// </summary>
    [Table("bdl_corr_systbl")]
    public class DbBundleCorrelation : DbIdentified
    {
        /// <summary>
        /// Gets or sets the key for the correlation
        /// </summary>
        [PrimaryKey, Column("corr_id"), NotNull]
        public override Guid Key { get; set; }

    }

    /// <summary>
    /// Bundle correlation submission
    /// </summary>
    [Table("bdl_corr_subm_systbl")]
    public class DbBundleCorrelationSubmission : DbAssociation
    {
        /// <summary>
        /// Gets or sets the unique submission identifier (the key of the bundle itself)
        /// </summary>
        [Column("subm_id"), NotNull, PrimaryKey]
        public override Guid Key { get; set; }

        /// <summary>
        /// Gets or sets the correlation key to which the submission belongs
        /// </summary>
        [Column("corr_id"), NotNull, ForeignKey(typeof(DbBundleCorrelation), nameof(DbBundleCorrelation.Key))]
        public override Guid SourceKey { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Column("seq"), NotNull]
        public long? CorrelationSequence { get; set; }

        /// <summary>
        /// Gets or sets the provenance where the bundle was 
        /// </summary>
        [Column("crt_prov_id"), NotNull, ForeignKey(typeof(DbSecurityProvenance), nameof(DbSecurityProvenance.Key))]
        public Guid CreatedByKey { get; set; }

        /// <summary>
        /// Creation time of the submission
        /// </summary>
        [Column("crt_utc"), NotNull, AutoGenerated]
        public DateTimeOffset CreationTime { get; set; }
    }

}
