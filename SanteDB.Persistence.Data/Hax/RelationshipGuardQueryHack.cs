﻿/*
 * Copyright (C) 2021 - 2022, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2022-9-7
 */
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.OrmLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SanteDB.Persistence.Data.Hax
{
    /// <summary>
    /// Represents a query hack for participations / relationships where the guard is being queried 
    /// </summary>
    public class RelationshipGuardQueryHack : IQueryBuilderHack
    {

        /// <summary>
        /// Hack query builder based on clause
        /// </summary>
        public bool HackQuery(QueryBuilder builder, SqlStatement sqlStatement, SqlStatement whereClause, Type tmodel, PropertyInfo property, string queryPrefix, QueryPredicate predicate, String[] values, IEnumerable<TableMapping> scopedTables, IDictionary<String, string[]> queryFilter)
        {
            string columnName = String.Empty;
            Type scanType = null;

            // Filter values
            if (typeof(Concept).IsAssignableFrom(property.PropertyType) && predicate.SubPath == "mnemonic")
            {
                Regex removeRegex = null;
                if (predicate.Path == "participationRole"  && property.DeclaringType == typeof(ActParticipation))
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
                    return false;

                // Now we scan
                List<Object> qValues = new List<object>();
                if (values is IEnumerable)
                    foreach (var i in values as IEnumerable)
                    {
                        var fieldInfo = scanType.GetRuntimeField(i.ToString());
                        if (fieldInfo == null) return false;
                        qValues.Add(fieldInfo.GetValue(null));
                    }
                else
                {
                    var fieldInfo = scanType.GetRuntimeField(values.ToString());
                    if (fieldInfo == null) return false;
                    qValues.Add(fieldInfo.GetValue(null));
                }

                // Now add to query
                whereClause.And($"{columnName} IN ({String.Join(",", qValues.Select(o=>$"'{o}'").ToArray())})");

                // Remove the inner join 
                var remStack = new Stack<SqlStatement>();
                SqlStatement last;
                while(sqlStatement.RemoveLast(out last))
                {
                    var m = removeRegex.Match(last.SQL);
                    if (m.Success)
                    {
                        // The last thing we added was the 
                        if (m.Index == 0 && m.Length == last.SQL.Length)
                            remStack.Pop();
                        else
                            sqlStatement.Append(last.SQL.Remove(m.Index, m.Length), last.Arguments.ToArray());
                        break;
                    }
                    else
                        remStack.Push(last);
                }
                while (remStack.Count > 0)
                    sqlStatement.Append(remStack.Pop());
                return true;
            }
            else
                return false;
        }
    }
}
