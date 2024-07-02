using SanteDB.Core.i18n;
using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Text;

namespace SanteDB.Persistence.Data.Exceptions
{
    /// <summary>
    /// Claim provided does not match expected value
    /// </summary>
    public sealed class ClaimAssertionException : AuthenticationException
    {

        /// <summary>
        /// Claim mismatch exception
        /// </summary>
        public ClaimAssertionException(String claimType, String providedValue, String expectedValue) : base(String.Format(ErrorMessages.ASSERTION_MISMATCH, $"{claimType}={expectedValue}", $"{claimType}={providedValue}"))
        {
            
        }
    }
}
