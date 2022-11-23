using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Persistence.Data.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

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
