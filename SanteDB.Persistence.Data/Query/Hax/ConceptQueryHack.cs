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
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Map;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SanteDB.Persistence.Data.Query.Hax
{
    /// <summary>
    /// This particular hack will override queries where concepts are filtered by mnemonic to be more efficient
    /// </summary>
    public class ConceptQueryHack : IQueryBuilderHack
    {

        // The mapper to be used
        private ModelMapper m_mapper;

        /// <summary>
        /// Creates a new query hack
        /// </summary>
        public ConceptQueryHack(ModelMapper mapper)
        {
            m_mapper = mapper;
        }

        /// <summary>
        /// Hack the particular query
        /// </summary>
        public bool HackQuery(QueryBuilder builder, SqlStatementBuilder sqlStatement, SqlStatementBuilder whereClause, Type tmodel, PropertyInfo property, string queryPrefix, QueryPredicate predicate, string[] values, IEnumerable<TableMapping> scopedTables, IDictionary<string, string[]> queryFilter)
        {
            // Hack mnemonic queries
            if (typeof(Concept).IsAssignableFrom(property.PropertyType) && predicate.SubPath == "mnemonic")
            {
                // Has this already been joined?
                var mapType = property.DeclaringType;
                if (mapType.IsAbstract)
                {
                    mapType = tmodel;
                }

                var declType = TableMapping.Get(m_mapper.MapModelType(mapType));
                var keyProperty = property.PropertyType == typeof(Guid) ? property : mapType.GetRuntimeProperty(property.Name + "Key");
                var declProp = declType.GetColumn(m_mapper.MapModelProperty(mapType, declType.OrmType, keyProperty));
                if (declProp.ForeignKey == null)
                {
                    return false; // No FK link
                }

                var tblMap = TableMapping.Get(m_mapper.MapModelType(property.PropertyType));
                var fkTbl = TableMapping.Get(declProp.ForeignKey.Table);
                string directFkName = $"{queryPrefix}{fkTbl.TableName}";

                // We have to join to the FK table
                if (!declProp.IsAlwaysJoin)
                {
                    var fkColumn = fkTbl.GetColumn(declProp.ForeignKey.Column);
                    sqlStatement.Append($" INNER JOIN {fkTbl.TableName} AS {directFkName}_{declProp.Name} ON ({queryPrefix}{declType.TableName}.{declProp.Name} = {directFkName}_{declProp.Name}.{fkColumn.Name})");
                    directFkName += $"_{declProp.Name}";
                }

                // We aren't yet joined to our table, we need to join to our table though!!!!
                if (declProp.ForeignKey.Table != tblMap.OrmType)
                {
                    var fkKeyColumn = fkTbl.Columns.FirstOrDefault(o => o.ForeignKey?.Table == tblMap.OrmType && o.Name == tblMap.PrimaryKey.First().Name) ??
                        tblMap.Columns.FirstOrDefault(o => o.ForeignKey?.Table == fkTbl.OrmType && o.Name == fkTbl.PrimaryKey.First().Name);
                    if (fkKeyColumn == null)
                    {
                        return false; // couldn't find the FK link
                    }

                    // Now we want to filter our FK
                    var tblName = $"{queryPrefix}{declProp.Name}_{tblMap.TableName}";
                    sqlStatement.Append($" INNER JOIN {tblMap.TableName} AS {tblName} ON ({directFkName}.{fkKeyColumn.Name} = {tblName}.{fkKeyColumn.Name})");

                    // Append the where clause
                    whereClause.And(builder.CreateWhereCondition(property.PropertyType, predicate.SubPath, values, $"{queryPrefix}{declProp.Name}_", new List<TableMapping>() { tblMap }));

                    // Add obslt_utc version?
                    if (typeof(IDbBaseData).IsAssignableFrom(tblMap.OrmType))
                    {
                        whereClause.And($"{tblName}.{tblMap.GetColumn(nameof(IDbBaseData.ObsoletionTime)).Name} IS NULL");
                    }
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