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
 * Date: 2024-6-21
 */
using SanteDB.OrmLite.Attributes;
using System;

namespace SanteDB.Persistence.Data.Model.Sys
{
    /// <summary>
    /// Represents a data storage of a CDSS library 
    /// </summary>
    [Table("cdss_lib_tbl")]
    public class DbCdssLibrary : DbIdentified
    {
        /// <inheritdoc/>
        [Column("lib_id"), PrimaryKey, NotNull, AutoGenerated]
        public override Guid Key { get; set; }

        /// <summary>
        /// Identifies the CDSS library format
        /// </summary>
        [Column("cls"), NotNull]
        public string CdssLibraryFormat { get; set; }

        /// <summary>
        /// True if the protocol was defined as a system protocol (cannot be updated by the user)
        /// </summary>
        [Column("is_sys")]
        public bool IsSystem { get; set; }
    }

    /// <summary>
    /// Represents an individual version of the CDSS logic
    /// </summary>
    [Table("cdss_lib_vrsn_tbl")]
    public class DbCdssLibraryVersion : DbVersionedData
    {

        /// <inheritdoc/>
        [Column("lib_id"), ForeignKey(typeof(DbCdssLibrary), nameof(DbCdssLibrary.Key))]
        public override Guid Key { get; set; }

        /// <inheritdoc/>
        [Column("lib_vrsn_id"), PrimaryKey, AutoGenerated]
        public override Guid VersionKey { get; set; }

        /// <summary>
        /// Gets or sets the named identifier of the library
        /// </summary>
        [Column("id"), NotNull]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the long form name
        /// </summary>
        [Column("name"), NotNull]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the informational version
        /// </summary>
        [Column("ver")]
        public string VersionName { get; set; }

        /// <summary>
        /// Gets or sets the OID of the library
        /// </summary>
        [Column("oid")]
        public string Oid { get; set; }

        /// <summary>
        /// Gets or sets the informational documentation
        /// </summary>
        [Column("doc")]
        public string Documentation { get; set; }

        /// <summary>
        /// Gets or sets the source data
        /// </summary>
        [Column("def"), NotNull]
        public byte[] Definition { get; set; }

    }
}
