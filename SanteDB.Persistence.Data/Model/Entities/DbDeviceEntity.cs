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
using SanteDB.OrmLite.Attributes;
using SanteDB.Persistence.Data.Model.Security;
using System;

namespace SanteDB.Persistence.Data.Model.Entities
{
    /// <summary>
    /// Represents the entity representation of an object
    /// </summary>
    [Table("dev_ent_tbl")]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class DbDeviceEntity : DbEntitySubTable
    {
        /// <summary>
        /// Gets or sets the security device identifier.
        /// </summary>
        /// <value>The security device identifier.</value>
        [Column("sec_dev_id"), ForeignKey(typeof(DbSecurityDevice), nameof(DbSecurityDevice.Key))]
        public Guid SecurityDeviceKey
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the manufacturer model.
        /// </summary>
        /// <value>The name of the manufacturer model.</value>
        [Column("mnf_name")]
        public string ManufacturerModelName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the operating system.
        /// </summary>
        /// <value>The name of the operating system.</value>
        [Column("os_name")]
        public String OperatingSystemName
        {
            get;
            set;
        }

    }
}