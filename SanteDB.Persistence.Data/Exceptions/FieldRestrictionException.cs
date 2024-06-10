using SanteDB.Core.i18n;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Exceptions
{
    /// <summary>
    /// Represents a field restriction exception
    /// </summary>
    internal class FieldRestrictionException : ArgumentException
    {
        public FieldRestrictionException(String fieldName) : base(String.Format(ErrorMessages.FORBIDDEN_FIELD, fieldName), fieldName)
        {
        }
    }
}
