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
 * Date: 2024-6-21
 */
using SanteDB;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Map;
using SanteDB.OrmLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SanteDB.Persistence.Data.Query.Hax
{
    /// <summary>
    /// Represents a query hack for participations / relationships where the guard is being queried 
    /// </summary>
    public class RelationshipGuardQueryHack : IQueryBuilderHack
    {
        // The mapper
        private ModelMapper m_mapper;

        /// <summary>
        /// CTOR taking mapper as parm
        /// </summary>
        public RelationshipGuardQueryHack(ModelMapper map)
        {
            m_mapper = map;
        }
        /// <summary>
        /// Hack query builder based on clause
        /// </summary>
        public bool HackQuery(QueryBuilder builder, SqlStatementBuilder sqlStatement, SqlStatementBuilder whereClause, Type tmodel, PropertyInfo property, string queryPrefix, QueryPredicate predicate, string[] values, IEnumerable<TableMapping> scopedTables, IDictionary<string, string[]> queryFilter)
        {
            string columnName = string.Empty;
            Type scanType = null;

            // Filter values
            if (typeof(Concept).IsAssignableFrom(property.PropertyType) && predicate.SubPath == "mnemonic")
            {
                Regex removeRegex = null;
                if (predicate.Path == "participationRole" && property.DeclaringType == typeof(ActParticipation))
                {
                    columnName = "rol_cd_id";
                    scanType = typeof(ActParticipationKeys);
                    // We want to remove the inner join for cd_tbl
                    removeRegex = new Regex(@"INNER\sJOIN\scd_tbl\s.*\(.*?rol_cd_id.*");
                }
                else if (predicate.Path == "relationshipType" && property.DeclaringType == typeof(EntityRelationship))
                {
                    columnName = "rel_typ_cd_id";
                    scanType = typeof(EntityRelationshipTypeKeys);
                    removeRegex = new Regex(@"INNER\sJOIN\scd_tbl\s.*\(.*?rel_typ_cd_id.*");
                }
                else if (predicate.Path == "relationshipType" && property.DeclaringType == typeof(ActRelationship))
                {
                    columnName = "rel_typ_cd_id";
                    scanType = typeof(ActRelationshipTypeKeys);
                    removeRegex = new Regex(@"INNER\sJOIN\scd_tbl\s.*\(.*?rel_typ_cd_id.*");
                }
                else
                {
                    return false;
                }

                // Now we scan
                List<object> qValues = new List<object>();
                if (values is IEnumerable)
                {
                    foreach (var i in values as IEnumerable)
                    {
                        var fieldInfo = scanType.GetRuntimeField(i.ToString());
                        if (fieldInfo == null)
                        {
                            return false;
                        }

                        qValues.Add(fieldInfo.GetValue(null));
                    }
                }
                else
                {
                    var fieldInfo = scanType.GetRuntimeField(values.ToString());
                    if (fieldInfo == null)
                    {
                        return false;
                    }

                    qValues.Add(fieldInfo.GetValue(null));
                }

                // Now add to query
                whereClause.And($"{columnName} IN ({string.Join(",", qValues.Select(o => $"'{o}'").ToArray())})");

                // Remove the inner join 
                var remStack = new Stack<SqlStatement>();
                while (sqlStatement.RemoveLast(out var last) != null && !last.IsEmpty())
                {
                    var m = removeRegex.Match(last.Sql);
                    if (m.Success)
                    {
                        // The last thing we added was the 
                        if (m.Index == 0 && m.Length == last.Sql.Length)
                        {
                            remStack.Pop();
                        }
                        else
                        {
                            sqlStatement.Append(last.Sql.Remove(m.Index, m.Length), last.Arguments.ToArray());
                        }

                        break;
                    }
                    else
                    {
                        remStack.Push(last);
                    }
                }
                while (remStack.Count > 0)
                {
                    sqlStatement.Append(remStack.Pop());
                }

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
