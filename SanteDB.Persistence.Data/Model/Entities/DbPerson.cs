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
using SanteDB.Persistence.Data.Model.Concepts;
using System;

namespace SanteDB.Persistence.Data.Model.Entities
{
    /// <summary>
    /// Represents a person
    /// </summary>
    [Table("psn_tbl")]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class DbPerson : DbEntitySubTable
    {
        /// <summary>
        /// Parent key
        /// </summary>
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
        /// Gets or sets the date of birth.
        /// </summary>
        /// <value>The date of birth.</value>
        [Column("dob")]
        public DateTime? DateOfBirth
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the date of birth precision.
        /// </summary>
        /// <value>The date of birth precision.</value>
        [Column("dob_prec")]
        public string DateOfBirthPrecision
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the deceased date.
        /// </summary>
        /// <value>The deceased date.</value>
        [Column("dcsd_utc")]
        public DateTime? DeceasedDate
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the deceased date precision.
        /// </summary>
        /// <value>The deceased date precision.</value>
        [Column("dcsd_prec")]
        public string DeceasedDatePrecision
        {
            get;
            set;
        }


        /// <summary>
        /// Gets or sets the ethnic group code id
        /// </summary>
        [Column("occ_cd_id"), ForeignKey(typeof(DbConcept), nameof(DbConcept.Key))]
        public Guid? OccupationKey { get; set; }

        /// <summary>
        /// Gets or sets the ethnic group code id
        /// </summary>
        [Column("vip_sts_cd_id"), ForeignKey(typeof(DbConcept), nameof(DbConcept.Key))]
        public Guid? VipStatusKey { get; set; }

        /// <summary>
        /// Nationality key
        /// </summary>
        [Column("nat_cd_id"), ForeignKey(typeof(DbConcept), nameof(DbConcept.Key))]
        public Guid? NationalityKey { get; set; }

        /// <summary>
        /// Gets or sets the gender concept
        /// </summary>
        /// <value>The gender concept.</value>
        [Column("gndr_cd_id"), ForeignKey(typeof(DbConcept), nameof(DbConcept.Key))]
        public Guid? GenderConceptKey
        {
            get;
            set;
        }
    }
}