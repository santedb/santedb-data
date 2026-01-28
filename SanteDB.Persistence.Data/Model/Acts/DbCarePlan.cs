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
 * Date: 2024-6-21
 */
using SanteDB.Core.Model.Constants;
using SanteDB.OrmLite.Attributes;
using System;

namespace SanteDB.Persistence.Data.Model.Acts
{
    /// <summary>
    /// Represents a persistence class for storing extra metadata about a care plan
    /// </summary>
    [Table("cp_tbl")]
    public class DbCarePlan : DbActSubTable
    {
        /// <summary>
        /// Parent key
        /// </summary>
        [JoinFilter(PropertyName = nameof(DbActVersion.ClassConceptKey), Value = ActClassKeyStrings.CarePlan)]
        public override Guid ParentKey
        {
            get
            {
                return base.ParentKey;
            }
            set
            {
                base.ParentKey = value;
            }
        }

        /// <summary>
        /// Gets or sets the title of the column
        /// </summary>
        [Column("title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the program identifier for the care plan
        /// </summary>
        [Column("pth_id"), ForeignKey(typeof(DbCarePathwayDefinition), nameof(DbCarePathwayDefinition.Key))]
        public Guid? CarePathwayKey { get; set; }
    }
}
