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
using SanteDB.Core.Data.Backup;
using SanteDB.Core.Services;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Service for backing up and restoring 
    /// </summary>
    public class AdoPersistenceRestoreService : OrmBackupRestoreServiceBase<AdoPersistenceConfigurationSection>
    {
        // Primary database asset

        /// <inheritdoc/>
        public AdoPersistenceRestoreService(IConfigurationManager configurationManager) : base(configurationManager, DataConstants.PRIMARY_DATABASE_ASSET_ID)
        {
        }


    }
}
