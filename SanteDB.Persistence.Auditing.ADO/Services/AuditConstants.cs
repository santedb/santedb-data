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
using System;

namespace SanteDB.Persistence.Auditing.ADO.Services
{
    /// <summary>
    /// Audit constants
    /// </summary>
    internal class AuditConstants
    {

        /// <summary>
        /// Audit database asset
        /// </summary>
        internal static readonly Guid AUDIT_ASSET_ID = Guid.Parse("EFF684F2-7641-4697-A4A0-CA0F5171BAA7");

        /// <summary>
        /// Configuration section name
        /// </summary>
        public const string ConfigurationSectionName = "santedb.persistence.auditing.ado";

        /// <summary>
        /// Trace source name
        /// </summary>
        public const string TraceSourceName = "SanteDB.Auditing.ADO";

        /// <summary>
        /// The audit is new and has not been reviewed
        /// </summary>
        public readonly Guid StatusNew = Guid.Parse("21B03AD0-70FB-4BEE-A6BC-BDDFAC6BF4A4");

        /// <summary>
        /// The audit has been reviewed by a person
        /// </summary>
        public readonly Guid StatusReviewed = Guid.Parse("31B03AD0-70FB-4BEE-A6BC-BDDFAC6BF4A4");

        /// <summary>
        /// The audit has been obsoleted
        /// </summary>
        public readonly Guid StatusObsolete = Guid.Parse("41B03AD0-70FB-4BEE-A6BC-BDDFAC6BF4A4");

        /// <summary>
        /// Model map name of audit
        /// </summary>
        public const string ModelMapName = "AuditModelMap";
    }
}