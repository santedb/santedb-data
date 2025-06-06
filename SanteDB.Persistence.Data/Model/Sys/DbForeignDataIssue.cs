﻿/*
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
using SanteDB.Core.BusinessRules;
using SanteDB.OrmLite.Attributes;
using System;

namespace SanteDB.Persistence.Data.Model.Sys
{
    /// <summary>
    /// Foreign data detected issue
    /// </summary>
    [Table("FD_ISS_SYSTBL")]
    public class DbForeignDataIssue : DbAssociation
    {
        /// <summary>
        /// Gets the key of the foreign data issue
        /// </summary>
        [Column("FD_ISS_ID"), PrimaryKey, NotNull, AutoGenerated]
        public override Guid Key { get; set; }

        /// <summary>
        /// Gets the source key to which the status pplies
        /// </summary>
        [Column("FD_ID"), ForeignKey(typeof(DbForeignDataStage), nameof(DbForeignDataStage.Key)), NotNull]
        public override Guid SourceKey { get; set; }

        /// <summary>
        /// Gets or sets the priority
        /// </summary>
        [Column("ISS_PRI"), NotNull]
        public DetectedIssuePriorityType Priority { get; set; }

        /// <summary>
        /// Gets or sets the text
        /// </summary>
        [Column("ISS_TXT"), NotNull]
        public String Text { get; set; }

        /// <summary>
        /// Gets or sets the identifier given to the issue by the caller
        /// </summary>
        [Column("ISS_ID"), NotNull]
        public String LogicalId { get; set; }

        /// <summary>
        /// Gets or sets the type key
        /// </summary>
        [Column("ISS_TYP_CD"), NotNull]
        public Guid IssueTypeKey { get; set; }
    }
}
