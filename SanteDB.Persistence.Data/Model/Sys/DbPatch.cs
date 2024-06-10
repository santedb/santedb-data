/*
 * Copyright (C) 2021 - 2024, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using System;

namespace SanteDB.Persistence.Data.Model.Sys
{
    /// <summary>
    /// A representation of the patches applied in the database
    /// </summary>
    [Table("patch_db_systbl")]
    public class DbPatch
    {

        /// <summary>
        /// The patch applied ID
        /// </summary>
        [Column("patch_id"), PrimaryKey]
        public String PatchId { get; set; }

        /// <summary>
        /// The time the patch was applied
        /// </summary>
        [Column("apply_date"), NotNull]
        public DateTimeOffset ApplyDate { get; set; }

        /// <summary>
        /// The name of the patch
        /// </summary>
        [Column("info_name")]
        public String Description { get; set; }
    }
}
