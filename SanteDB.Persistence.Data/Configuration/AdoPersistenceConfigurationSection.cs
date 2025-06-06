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
using SanteDB.Core.Configuration;
using SanteDB.Core.Exceptions;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Serialization;

namespace SanteDB.Persistence.Data.Configuration
{
    /// <summary>
    /// Versioning policy types
    /// </summary>
    [XmlType(nameof(AdoVersioningPolicyFlags), Namespace = "http://santedb.org/configuration"), Flags]
    public enum AdoVersioningPolicyFlags
    {
        /// <summary>
        /// When no-versioning is enabled, indicates that new versions should not be created per object
        /// </summary>
        [XmlEnum("none")]
        None = 0,

        /// <summary>
        /// When full-versioning is enabled, then each update results in a new version of an object
        /// </summary>
        [XmlEnum("core-props")]
        FullVersioning = 1,

        /// <summary>
        /// When core-versioning is enabled, any versioned associations will be removed meaning
        /// that only core properties are versioned the associative properties are not.
        /// </summary>
        [XmlEnum("associated")]
        AssociationVersioning = 2,

        /// <summary>
        /// When a resource is touched even if no data is changed, a new version should be created, this behavior will result in multiple creation/obsoletion 
        /// entries for a resource. 
        /// </summary>
        [XmlEnum("version-touch")]
        VersionOnTouch = 4,

        /// <summary>
        /// Version non CDR assets
        /// </summary>
        [XmlEnum("non-cdr")]
        VersionNonCdrAssets = 8,

        /// <summary>
        /// Default flags
        /// </summary>
        [XmlEnum("default")]
        Default = FullVersioning | AssociationVersioning,

    }

    /// <summary>
    /// Correation behavior
    /// </summary>
    [XmlType(nameof(BundleCorrelationBehaviorType), Namespace = "http://santedb.org/configuration")]
    public enum BundleCorrelationBehaviorType
    {
        /// <summary>
        /// Inform the caller that the content was not modified via throwing a <see cref="PreconditionFailedException"/>
        /// </summary>
        [XmlEnum("not-modified")]
        NotModified,
        /// <summary>
        /// Throw an error that the data is invalid
        /// </summary>
        [XmlEnum("error")]
        ThrowError,
        /// <summary>
        /// Ignore the condition and don't commit any data
        /// </summary>
        [XmlEnum("ignore")]
        Ignore,
        /// <summary>
        /// Process the data anyways
        /// </summary>
        [XmlEnum("process")]
        ProcessAnyways
    }

    /// <summary>
    /// Configuration section handler
    /// </summary>
    [XmlType(nameof(AdoPersistenceConfigurationSection), Namespace = "http://santedb.org/configuration")]
    [ExcludeFromCodeCoverage]
    public class AdoPersistenceConfigurationSection : OrmConfigurationBase, IConfigurationSection
    {
        // 
        private bool m_legacyPepper = false;

        // Random
        private Random m_random = new Random();

        // PEPPER CHARS DEFAULT
        private const string PEPPER_CHARS = "0ABcDEfgILqZ~k";

        /// <summary>
        /// ADO configuration
        /// </summary>
        public AdoPersistenceConfigurationSection()
        {
            this.Validation = new List<AdoValidationPolicy>();
            this.AutoInsertChildren = true;
            this.VersioningPolicy = AdoVersioningPolicyFlags.Default;
            this.CachingPolicy = new AdoPersistenceCachingPolicy()
            {
                DataObjectExpiry = new TimeSpan(0, 1, 0),
                Targets = AdoDataCachingPolicyTarget.ModelObjects
            };
            this.TrimSettings = new AdoTrimSettings();
        }

        /// <summary>
        /// The persistence layer is not versioned
        /// </summary>
        [XmlAttribute("versioning")]
        [Category("Behavior")]
        [DisplayName("Control Versioning")]
        [Description("When enabled, changes the versioning behavior of the persistence layer")]
        public AdoVersioningPolicyFlags VersioningPolicy { get; set; }

        /// <summary>
        /// The peppering characters for authentication hashes
        /// </summary>
        [XmlAttribute("pepper")]
        [Category("Security")]
        [DisplayName("Password peppering characters")]
        [Description("When set, identifies the pepper characters to use for authentication")]
        public String Pepper { get; set; }

        /// <summary>
        /// Maximum requests
        /// </summary>
        [XmlAttribute("maxRequests")]
        [Category("Performance")]
        [DisplayName("Request Throttling")]
        [Description("When set, instructs the ADO.NET data provider to limit queries")]
        public int MaxRequests { get; set; }

        /// <summary>
        /// Use legacy peppering algorithm
        /// </summary>
        [XmlAttribute("legacyPepper")]
        [Category("Security")]
        [DisplayName("Use Legacy Peppering")]
        [Description("When set will include legacy pepper combinations when authenticating (slower, but compatible)")]
        public bool LegacyPepper
        {
            get => this.m_legacyPepper;
            set
            {
                this.m_legacyPepper = value;
                this.LegacyPepperSpecified = true;
            }
        }

        /// <summary>
        /// True if pepper is specified
        /// </summary>
        [XmlIgnore, Browsable(false)]
        public bool LegacyPepperSpecified { get; set; }

        /// <summary>
        /// When true, indicates that inserts can allow keyed inserts
        /// </summary>
        [XmlAttribute("autoUpdateExisting")]
        [Category("Behavior")]
        [DisplayName("Auto-Update Existing Resource")]
        [Description("When set, instructs the provider to automatically update existing records when Insert() is called")]
        public bool AutoUpdateExisting { get; set; }

        /// <summary>
        /// Gets or sets strict key enforcement behavior
        /// </summary>
        [XmlAttribute("keyValidation")]
        [Category("Data Quality")]
        [DisplayName("Key / Data Agreement")]
        [Description("When a key property and a data property disagree (i.e. identifier.AuthorityKey <> identifier.Authority.Key) - the persistence layer should refuse to persist (when false, the key is taken over the property value)")]
        public bool StrictKeyAgreement { get; set; }

        /// <summary>
        /// When true, indicates that inserts can allow auto inserts of child properties
        /// </summary>
        [XmlAttribute("autoInsertChildren")]
        [Category("Behavior")]
        [DisplayName("Auto-Insert Child Objects")]
        [Description("When set, instructs the provider to automatically insert any child objects to ensure integrity of the object")]
        public bool AutoInsertChildren { get; set; }

        ///// <summary>
        ///// True if statements should be prepared
        ///// </summary>
        //[XmlAttribute("prepareStatements")]
        //[Category("Performance")]
        //[DisplayName("Prepare SQL Queries")]
        //[Description("When true, instructs the provider to prepare statements and reuse them during a transaction")]
        //public bool PrepareStatements { get; set; }

        /// <summary>
        /// Validation flags
        /// </summary>
        [XmlElement("validation"), Category("Data Quality"), DisplayName("Identifier Validation"), Description("When set, enables data validation parameters")]
        public List<AdoValidationPolicy> Validation { get; set; }

        /// <summary>
        /// Max page size
        /// </summary>
        [XmlElement("maxPageSize"), Category("Performance"), DisplayName("Maximum Results per Page")]
        public int? MaxPageSize { get; set; }

        /// <summary>
        /// Identiifes the caching policy
        /// </summary>
        [XmlElement("caching"), Category("Performance"), DisplayName("Caching Policy"), Description("Identifies the data caching policy for the database layer")]
        public AdoPersistenceCachingPolicy CachingPolicy { get; set; }

        /// <summary>
        /// Gets or sets the loading strategy
        /// </summary>
        [XmlAttribute("loadStrategy"), Category("Performance"), DisplayName("Load Strategy"), Description("Sets the loading strategy - Quick = No extended loading of properties , Sync = Only synchornization/serialization properties are deep loaded, Full = All properties are loaded")]
        public LoadMode LoadStrategy { get; set; }

        /// <summary>
        /// Gets or sets the deletion strategy
        /// </summary>
        [XmlAttribute("deleteStrategy"), Category("Behavior"), DisplayName("Delete Strategy"), Description("Sets the default deletion strategy for the persistence layer. LogicalDelete = Set state to Inactive and obsolete time, ObsoleteDelete = Set state to Obsolete and obsolete time, NullifyDelete = Set state to nullify and obsolete time, Versioned Delete = Create a new version with an obsolete time (won't be returned by any calls), Permanent Delete = Purge the data and related data (CASCADES)")]
        public DeleteMode DeleteStrategy { get; set; }

        /// <summary>
        /// Fast deletion mode
        /// </summary>
        [XmlAttribute("fastDelete"), Category("Behavior"), DisplayName("Fast Delete"), Description("When true, the DELETE verb will not return the full object rather a summary object")]
        public bool FastDelete { get; set; }

        /// <summary>
        /// Gets or sets the name of the skeleton connection string for the warehouse
        /// </summary>
        [XmlAttribute("biSkel"), Category("Business Intelligence"), DisplayName("Warehouse"), Description("The connection string to the data warehouse server")]
        [Editor("SanteDB.Configuration.Editors.ConnectionStringEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0")]
        public string WarehouseConnectionStringSkel { get; set; }

        /// <summary>
        /// Trim settings
        /// </summary>
        [XmlElement("trim"), Category("Maintenance"), DisplayName("Database Trimming"), Description("Configures how the database is maintained including retention time for old versions, sessions, etc.")]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public AdoTrimSettings TrimSettings { get; set; }

        /// <summary>
        /// Gets or sets the behavior when a bundle is recieved out of sequence
        /// </summary>
        [XmlElement("correlationBehavior"), Category("Behavior"), DisplayName("Ordered Bundle Process"), Description("Configures how the database should handle scenarios where bundles are processed out of order")]
        public BundleCorrelationBehaviorType BundleCorrelationBehavior { get; set; }

        /// <summary>
        /// Get all peppered combinations of the specified secret
        /// </summary>
        public IEnumerable<String> GetPepperCombos(String secret)
        {
            var pepperSource = !String.IsNullOrEmpty(this.Pepper) ? this.Pepper : PEPPER_CHARS;
            IEnumerable<String> pepperCombos = new String[] { };
            if (!this.LegacyPepperSpecified || this.LegacyPepper)
            {
                var pepper = Enumerable.Range(0, secret.Length / 2).Select(o => secret.Substring(o, 2) + $"{pepperSource}{pepperSource.Reverse()}".Substring(o % pepperSource.Length, (o + 1) % 5)).ToArray();
                pepperCombos = pepperCombos.Union(pepperSource.Select(p => $"{secret}{p}")).Union(pepper.Select(o => $"{secret}{o}"));
            }
            return pepperCombos.Union(Enumerable.Range(0, pepperSource.Length).Select(o => secret.Insert(o % secret.Length, pepperSource[o].ToString()))).ToArray();
        }

        /// <summary>
        /// Adds pepper to the secret
        /// </summary>
        public String AddPepper(String secret)
        {
            var pepperSource = !String.IsNullOrEmpty(this.Pepper) ? this.Pepper : PEPPER_CHARS;
            var nextPepper = this.m_random.Next(pepperSource.Length);
            return secret.Insert(nextPepper % secret.Length, pepperSource[nextPepper].ToString());
        }
    }
}