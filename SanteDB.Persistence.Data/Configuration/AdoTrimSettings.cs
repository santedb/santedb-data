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
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace SanteDB.Persistence.Data.Configuration
{
    /// <summary>
    /// ADO trim settings
    /// </summary>
    [XmlType(nameof(AdoTrimSettings), Namespace = "http://santedb.org/configuration")]
    public class AdoTrimSettings
    {
        /// <summary>
        /// Gets or sets the maximum session retention policy
        /// </summary>
        [XmlElement("maxSession"), DisplayName("Session Retention"), Description("Sets the maximum amount of time that old sessions should be retained (default: 30 days)")]
        [Editor("SanteDB.Configuration.Editors.TimespanPickerEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing")]
        public TimeSpan? MaxSessionRetention { get; set; }

        /// <summary>
        /// Gets or sets the maximum old version retention
        /// </summary>
        [XmlElement("maxVersion"), DisplayName("Version Retention"), Description("Sets the maximum amount of time that old versions should be retained (default: 30 days)")]
        [Editor("SanteDB.Configuration.Editors.TimespanPickerEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing")]
        public TimeSpan? MaxOldVersionRetention { get; set; }

        /// <summary>
        /// Gets or sets the maximum deleted data restoration availability.
        /// </summary>
        [XmlElement("maxRestore"), DisplayName("Restore Time"), Description("Sets the maximum amount of time that old data can be un-deleted (default: 30 days)")]
        [Editor("SanteDB.Configuration.Editors.TimespanPickerEditor, SanteDB.Configuration", "System.Drawing.Design.UITypeEditor, System.Drawing")]
        public TimeSpan? MaxDeletedDataRetention { get; set; }

    }
}