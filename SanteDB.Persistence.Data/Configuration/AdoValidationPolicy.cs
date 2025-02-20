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
 */
using SanteDB.Core.Configuration;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace SanteDB.Persistence.Data.Configuration
{
    /// <summary>
    /// The governance action to take
    /// </summary>
    [XmlType(nameof(AdoValidationEnforcement), Namespace = "http://santedb.org/configuration")]
    public enum AdoValidationEnforcement
    {
        /// <summary>
        /// Do no enforce th policy
        /// </summary>
        [XmlEnum("off")]
        Off = 0,

        
        /// <summary>
        /// Violations raise warnings
        /// </summary>
        [XmlEnum("loose")]
        Loose = 1,

        /// <summary>
        /// Violations raise errors
        /// </summary>
        [XmlEnum("strict")]
        Strict = 2,

    }

    /// <summary>
    /// Data caching policy
    /// </summary>
    [XmlType(nameof(AdoValidationPolicy), Namespace = "http://santedb.org/configuration")]
    [ExcludeFromCodeCoverage]
    public class AdoValidationPolicy
    {
        /// <summary>
        /// Gets or sets the targets
        /// </summary>
        [XmlElement("target")]
        [DisplayName("Resource Type"), Description("The resource types that this validation applies to")]
        [Editor("SanteDB.Configuration.Editors.ResourceCollectionEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0")]
        public ResourceTypeReferenceConfiguration Target { get; set; }

        /// <summary>
        /// Enforce uniqueness
        /// </summary>
        [XmlAttribute("unique")]
        [DisplayName("Uniqueness"), Description("Controls the validation of the IdentityDomain.Unique setting")]
        public AdoValidationEnforcement Uniqueness { get; set; }

        /// <summary>
        /// Enforce scope
        /// </summary>
        [XmlAttribute("scope")]
        [DisplayName("Scope"), Description("Controls the validation of the IdentityDomain.Scope setting")]
        public AdoValidationEnforcement Scope { get; set; }

        /// <summary>
        /// Enforce authority
        /// </summary>
        [XmlAttribute("authority")]
        [DisplayName("Authority"), Description("Controls the validation of the IdentityDomain.AssigningAuthority setting")]
        public AdoValidationEnforcement Authority { get; set; }

        /// <summary>
        /// Ensure format of identifier
        /// </summary>
        [XmlAttribute("format")]
        [DisplayName("Format"), Description("Controls the validation of the IdentityDomain.ValidationRegex")]
        public AdoValidationEnforcement Format { get; set; }

        /// <summary>
        /// Ensure check-digit of identifier
        /// </summary>
        [XmlAttribute("checkDigit")]
        [DisplayName("Check Digit"), Description("Controls the validation of the IdentityDomain.CheckDigitAlgorithm setting")]
        public AdoValidationEnforcement CheckDigit { get; set; }
    }
}