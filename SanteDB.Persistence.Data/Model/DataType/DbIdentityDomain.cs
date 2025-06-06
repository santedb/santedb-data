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
using SanteDB.Core.Model.DataTypes;
using SanteDB.OrmLite.Attributes;
using SanteDB.Persistence.Data.Model.Concepts;
using SanteDB.Persistence.Data.Model.Security;
using System;

namespace SanteDB.Persistence.Data.Model.DataType
{
    /// <summary>
    /// Represents an assigning authority
    /// </summary>
    [Table("id_dmn_tbl"), AssociativeTable(typeof(DbSecurityApplication), typeof(DbAssigningAuthority)), AssociativeTable(typeof(DbConceptVersion), typeof(DbIdentityDomainScope))]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class DbIdentityDomain : DbNonVersionedBaseData
    {

        /// <summary>
        /// Gets or sets the name of the aa
        /// </summary>
        [Column("dmn_name")]
        public String Name { get; set; }

        /// <summary>
        /// Gets or sets the short HL7 code of the AA
        /// </summary>
        [Column("nsid"), NotNull]
        public String DomainName { get; set; }

        /// <summary>
        /// Gets or sets the OID of the AA
        /// </summary>
        [Column("oid"), NotNull]
        public String Oid { get; set; }

        /// <summary>
        /// Gets or sets the description of the AA
        /// </summary>
        [Column("descr")]
        public String Description { get; set; }

        /// <summary>
        /// Gets or sets the URL of AA
        /// </summary>
        [Column("url")]
        public String Url { get; set; }

        /// <summary>
        /// Gets or sets the assigning authority policy key (that policy which devices must have to be disclosed this identity)
        /// </summary>
        [Column("pol_id"), ForeignKey(typeof(DbSecurityPolicy), nameof(DbSecurityPolicy.Key))]
        public Guid? PolicyKey { get; set; }

        /// <summary>
        /// Validation regular expression
        /// </summary>
        [Column("val_rgx")]
        public String ValidationRegex { get; set; }

        /// <summary>
        /// Gets or sets the custom validator
        /// </summary>
        [Column("val_cls")]
        public String CustomValidator { get; set; }

        /// <summary>
        /// Gets or sets the check digit algorithm
        /// </summary>
        [Column("chk_dgt_alg")]
        public string CheckDigitAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the key
        /// </summary>
        [Column("dmn_id"), PrimaryKey, AutoGenerated]
        public override Guid Key { get; set; }

        /// <summary>
        /// True if the AA is unique
        /// </summary>
        [Column("is_unq")]
        public bool IsUnique { get; set; }

        /// <summary>
        /// Classification key 
        /// </summary>
        [Column("cls_cd_id"), ForeignKey(typeof(DbConceptVersion), nameof(DbConceptVersion.Key))]
        public Guid? IdentifierClassificationKey { get; set; }
    }

    /// <summary>
    /// Assigning authority for an identity domain
    /// </summary>
    [Table("asgn_aut_tbl")]
    public class DbAssigningAuthority : DbBaseData, IDbAssociation
    {

        /// <summary>
        /// The authority identifier
        /// </summary>
        [PrimaryKey, Column("aut_id"), AutoGenerated]
        public override Guid Key { get; set; }

        /// <summary>
        /// Gets or sets the scope of the auhority
        /// </summary>
        [Column("dmn_id"), PrimaryKey, ForeignKey(typeof(DbIdentityDomain), nameof(DbIdentityDomain.Key))]
        public Guid SourceKey { get; set; }

        /// <summary>
        /// Assigning device identifier
        /// </summary>
        [Column("app_id"), ForeignKey(typeof(DbSecurityApplication), nameof(DbSecurityApplication.Key))]
        public Guid AssigningApplicationKey { get; set; }

        /// <summary>
        /// Gets or sets the identifier reliability
        /// </summary>
        [Column("rel")]
        public IdentifierReliability Reliability { get; set; }
    }

    /// <summary>
    /// Identifier scope
    /// </summary>
    [Table("id_dmn_scp_tbl")]
#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    public class DbIdentityDomainScope : DbAssociation
    {

        /// <summary>
        /// Gets or sets the unique key
        /// </summary>
        public override Guid Key { get; set; }

        /// <summary>
        /// Gets or sets the scope of the auhority
        /// </summary>
        [Column("dmn_id"), PrimaryKey, ForeignKey(typeof(DbIdentityDomain), nameof(DbIdentityDomain.Key))]
        public override Guid SourceKey { get; set; }

        /// <summary>
        /// Gets or sets the scope of the auhority
        /// </summary>
        [Column("cd_id"), PrimaryKey, ForeignKey(typeof(DbConceptVersion), nameof(DbConceptVersion.Key))]
        public Guid ScopeConceptKey { get; set; }

        /// <summary>
        /// Determines value equality between <paramref name="obj"/> and this object
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is DbIdentityDomainScope dba)
            {
                return dba.ScopeConceptKey == this?.ScopeConceptKey &&
                    dba.SourceKey == this?.SourceKey;
            }
            else
            {
                return base.Equals(obj);
            }
        }

    }
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
}
