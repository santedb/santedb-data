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
using SanteDB.Core.Security.Audit;
using SanteDB.OrmLite;
using System;

namespace SanteDB.Persistence.Data.Services.Persistence
{
    /// <summary>
    /// Implementers of this class can trim old data from the database
    /// </summary>
    public interface IAdoTrimProvider
    {

        /// <summary>
        /// Trim the specified object from the database 
        /// </summary>
        /// <param name="auditBuilder">The audit build so the trimming process can audit the logical or perminent removal</param>
        /// <param name="context">The data context on which the trim process is running</param>
        /// <param name="deletedCutoff">The date/time whereby a logically deleted resource needs to be purged</param>
        /// <param name="oldVersionCutoff">The date/time of a historical version where the version should be removed</param>
        void Trim(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder);

    }
}
