using SanteDB.Core.Security.Principal;
using SanteDB.Persistence.Data.Model.Security;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SanteDB.Persistence.Data.Security
{
    /// <summary>
    /// ADO user identity creatd with a certificate
    /// </summary>
    internal class AdoUserCertificateIdentity : AdoUserIdentity, ICertificateIdentity
    {
        /// <inheritdoc/>
        internal AdoUserCertificateIdentity(DbSecurityUser userData, X509Certificate2 authenticationCertificiate) : base(userData, "X.509")
        {
            this.AuthenticationCertificate = authenticationCertificiate;
        }

        /// <inheritdoc/>
        public X509Certificate2 AuthenticationCertificate { get; }
    }
}
