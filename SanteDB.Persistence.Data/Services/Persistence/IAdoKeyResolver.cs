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
using SanteDB.Core.Model.Interfaces;
using SanteDB.Persistence.Data.Model;
using System;
using System.Linq.Expressions;

namespace SanteDB.Persistence.Data.Services.Persistence
{

    /// <summary>
    /// Marker interface
    /// </summary>
    public interface IAdoKeyResolver { }

    /// <summary>
    /// Key resolver service
    /// </summary>
    /// <remarks>
    /// The key resolver service is used when related entities are persisted in a context where only one of a particular relationship can exist at the same time.
    /// The <see cref="GetKeyExpression(TModel)"/> is intended to return the LINQ expression to filter the existing dataset to determine if the specified object already exists.
    /// The <typeparamref name="TModel"/> should always be an <see cref="IDbIdentified"/> instance which has an actual primary key (surrogate key) and should not be used on 
    /// objects which have composite keys. If using this resolver implementation to resolve a <see cref="IAnnotatedResource"/> instance then the same applies.
    /// </remarks>
    public interface IAdoKeyResolver<TModel> : IAdoKeyResolver
    {

        /// <summary>
        /// Get the expression that can be used to fetch the key for the model
        /// </summary>
        /// <param name="model">The model to check for an existing key</param>
        /// <returns>The Expression for the model</returns>
        Expression<Func<TModel, bool>> GetKeyExpression(TModel model);

    }
}
