﻿/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2022-9-7
 */
using SanteDB.OrmLite.Attributes;
using SanteDB.Persistence.Data.Model.Concepts;
using SanteDB.Persistence.Data.Model.DataType;
using SanteDB.Persistence.Data.Model.Extensibility;
using System;
using System.Diagnostics.CodeAnalysis;

namespace SanteDB.Persistence.Data.Model.Acts
{
    /// <summary>
    /// Represents a table which can store act data
    /// </summary>
    [Table("act_vrsn_tbl")]
    [ExcludeFromCodeCoverage]
    public class DbActVersion : DbVersionedData, IDbHasStatus
    {
        /// <summary>
        /// Gets or sets the template
        /// </summary>
        [Column("tpl_id"), ForeignKey(typeof(DbTemplateDefinition), nameof(DbTemplateDefinition.Key))]
        public Guid? TemplateKey { get; set; }

        /// <summary>
        /// Identifies the class concept
        /// </summary>
        [Column("cls_cd_id"), ForeignKey(typeof(DbConcept), nameof(DbConcept.Key))]
        public Guid ClassConceptKey { get; set; }

        /// <summary>
        /// Gets or sets the mood of the act
        /// </summary>
        [Column("mod_cd_id"), ForeignKey(typeof(DbConcept), nameof(DbConcept.Key))]
        public Guid MoodConceptKey { get; set; }

        /// <summary>
        /// True if negated
        /// </summary>
        [Column("neg_ind")]
        public bool IsNegated { get; set; }

        /// <summary>
        /// Identifies the time that the act occurred
        /// </summary>
        [Column("act_utc")]
        public DateTimeOffset? ActTime { get; set; }

        /// <summary>
        /// Identifies the start time of the act
        /// </summary>
        [Column("act_start_utc")]
        public DateTimeOffset? StartTime { get; set; }

        /// <summary>
        /// Identifies the stop time of the act
        /// </summary>
        [Column("act_stop_utc")]
        public DateTimeOffset? StopTime { get; set; }

        /// <summary>
        /// Gets or sets the reason concept
        /// </summary>
        [Column("rsn_cd_id"), ForeignKey(typeof(DbConcept), nameof(DbConcept.Key))]
        public Guid? ReasonConceptKey { get; set; }

        /// <summary>
        /// Gets or sets the status concept
        /// </summary>
        [Column("sts_cd_id"), ForeignKey(typeof(DbConcept), nameof(DbConcept.Key))]
        public Guid StatusConceptKey { get; set; }

        /// <summary>
        /// Gets or sets the type concept
        /// </summary>
        [Column("typ_cd_id"), ForeignKey(typeof(DbConcept), nameof(DbConcept.Key))]
        public Guid TypeConceptKey { get; set; }

        /// <summary>
        /// Version identifier
        /// </summary>
        [Column("act_vrsn_id"), PrimaryKey, AutoGenerated]
        public override Guid VersionKey { get; set; }

        /// <summary>
        /// Gets or sets the act identifier
        /// </summary>
        [Column("act_id"), ForeignKey(typeof(DbAct), nameof(DbAct.Key)), AlwaysJoin]
        public override Guid Key { get; set; }

        /// <summary>
        /// Reference to the geo graphic location of the entity
        /// </summary>
        [Column("geo_id"), ForeignKey(typeof(DbGeoTag), nameof(DbGeoTag.Key))]
        public Guid? GeoTagKey { get; set; }
    }
}