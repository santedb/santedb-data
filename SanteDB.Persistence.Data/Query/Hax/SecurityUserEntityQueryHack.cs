/*
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
 * Date: 2024-6-21
 */
using SanteDB;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Security;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Entities;
using SanteDB.Persistence.Data.Model.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SanteDB.Persistence.Data.Query.Hax
{
    /// <summary>
    /// Query builder hack 
    /// </summary>
    public class SecurityUserEntityQueryHack : IQueryBuilderHack
    {
        // The mapper
        private ModelMapper m_mapper;

        /// <summary>
        /// CTOR taking mapper as parm
        /// </summary>
        public SecurityUserEntityQueryHack(ModelMapper map)
        {
            m_mapper = map;
        }
        /// <summary>
        /// Hack the query
        /// </summary>
        public bool HackQuery(QueryBuilder builder, SqlStatementBuilder sqlStatement, SqlStatementBuilder whereClause, Type tmodel, PropertyInfo property, string queryPrefix, QueryPredicate predicate, string[] values, IEnumerable<TableMapping> scopedTables, IDictionary<string, string[]> queryFilter)
        {
            if (typeof(SecurityUser) == tmodel && property.Name == nameof(SecurityUser.UserEntity))
            {
                var userkey = TableMapping.Get(typeof(DbUserEntity)).GetColumn(nameof(DbUserEntity.SecurityUserKey), false);
                var personSubSelect = builder.CreateQuery(typeof(UserEntity), queryFilter.ToDictionary(p => p.Key.Replace("userEntity.", ""), p => p.Value), null, userkey);
                var userIdKey = TableMapping.Get(typeof(DbSecurityUser)).PrimaryKey.FirstOrDefault();
                whereClause.And($"{userIdKey.Name} IN (").Append(personSubSelect).Append(")");
                return true;
            }
            return false;
        }
    }
}
