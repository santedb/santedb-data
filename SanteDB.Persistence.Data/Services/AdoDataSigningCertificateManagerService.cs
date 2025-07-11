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
using SanteDB.Core.Data.Quality;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Audit;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model.Security;
using SanteDB.Persistence.Data.Security;
using SanteDB.Persistence.Data.Services.Persistence;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// ADO data signing certificate 
    /// </summary>
    public class AdoDataSigningCertificateManagerService : IDataSigningCertificateManagerServiceEx, IAdoTrimProvider, ILocalServiceProvider<IDataSigningCertificateManagerService>
    {
        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoDataSigningCertificateManagerService));

        private readonly AdoPersistenceConfigurationSection m_configuration;
        private readonly IPolicyEnforcementService m_pepService;
        //private readonly ITfaRelayService m_tfaRelay;
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// Creates a new ADO session identity provider with injected configuration manager
        /// </summary>
        public AdoDataSigningCertificateManagerService(IConfigurationManager configuration,
            ILocalizationService localizationService,
            IPolicyEnforcementService policyEnforcementService
            )
        {
            this.m_configuration = configuration.GetSection<AdoPersistenceConfigurationSection>();
            this.m_pepService = policyEnforcementService;
            this.m_localizationService = localizationService;
        }

        /// <inheritdoc/>
        public string ServiceName => "ADO.NET Data Signing Service";

        /// <inheritdoc/>
        public IDataSigningCertificateManagerService LocalProvider => this;

        /// <inheritdoc/>
        public void AddSigningCertificate(IIdentity identity, X509Certificate2 x509Certificate, IPrincipal principal)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            else if (x509Certificate == null)
            {
                throw new ArgumentNullException(nameof(x509Certificate));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            this.AddSigningCertificates(identity, new X509Certificate2[] { x509Certificate }, principal);
        }

        /// <inheritdoc/>
        public void AddSigningCertificates(IIdentity identity, IEnumerable<X509Certificate2> x509Certificates, IPrincipal principal) {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            else if (x509Certificates == null)
            {
                throw new ArgumentNullException(nameof(x509Certificates));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            // Doing this to some other identity requires special permission
            if (!identity.Name.Equals(principal.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                this.m_pepService?.Demand(PermissionPolicyIdentifiers.AssignCertificateToIdentity, principal);
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);

                    using (var transaction = context.BeginTransaction())
                    {

                        foreach (var cert in x509Certificates)
                        {
                            // get the identity
                            var certificateRegistration = new DbCertificateMapping()
                            {
                                CreatedByKey = context.EstablishProvenance(principal, null),
                                CreationTime = DateTimeOffset.Now,
                                Expiration = cert.NotAfter,
                                X509Thumbprint = cert.Thumbprint,
                                X509PublicKeyData = cert.GetRawCertData(),
                                Use = CertificateMappingUse.Signature
                            };

                            switch (identity)
                            {
                                case IDeviceIdentity did:
                                    certificateRegistration.SecurityDeviceKey = context.Query<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == did.Name.ToLowerInvariant()).Select(o => o.Key).FirstOrDefault();
                                    break;
                                case IApplicationIdentity aid:
                                    certificateRegistration.SecurityApplicationKey = context.Query<DbSecurityApplication>(o => o.PublicId.ToLowerInvariant() == aid.Name.ToLowerInvariant()).Select(o => o.Key).FirstOrDefault();
                                    break;
                                default:
                                    certificateRegistration.SecurityUserKey = context.Query<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == identity.Name.ToLowerInvariant()).Select(o => o.Key).FirstOrDefault();
                                    break;
                            }

                            // Enusre there is no active mapping
                            var existingMap = context.FirstOrDefault<DbCertificateMapping>(o =>
                                o.X509Thumbprint == cert.Thumbprint &&
                                o.Use == CertificateMappingUse.Signature &&
                                o.ObsoletionTime == null &&
                                o.SecurityDeviceKey == certificateRegistration.SecurityDeviceKey &&
                                o.SecurityUserKey == certificateRegistration.SecurityUserKey &&
                                o.SecurityApplicationKey == certificateRegistration.SecurityApplicationKey);

                            if (existingMap != null &&
                                (existingMap.SecurityApplicationKey.GetValueOrDefault() == certificateRegistration.SecurityApplicationKey || existingMap.SecurityDeviceKey.GetValueOrDefault() == certificateRegistration.SecurityDeviceKey || existingMap.SecurityUserKey.GetValueOrDefault() == certificateRegistration.SecurityUserKey))
                            {
                                certificateRegistration.Key = existingMap.Key;
                                certificateRegistration.UpdatedByKey = context.ContextId;
                                certificateRegistration.UpdatedTime = DateTimeOffset.Now;
                                context.Update(certificateRegistration);
                            }
                            else if (existingMap != null)
                            {
                                throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.SIG_CERT_ALREADY_ASSIGNED));
                            }
                            else
                            {
                                // attempt storage
                                context.Insert(certificateRegistration);
                            }
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error registering signing certificate to identity {0} with {1} - {2}", identity.Name, String.Join(";", x509Certificates.Select(o=>o.Subject)), e.Message);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.SIG_CERT_CREATE_GEN, new
                {
                    identity = identity.Name,
                    subject = String.Join(";", x509Certificates.Select(o => o.Subject))
                }), e);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IIdentity> GetCertificateIdentities(X509Certificate2 signingCertificate)
        {
            if (signingCertificate == null)
            {
                throw new ArgumentNullException(nameof(signingCertificate), ErrorMessages.ARGUMENT_NULL);
            }

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                context.Open(initializeExtensions: false);

                var dsigSql = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCertificateMapping), typeof(DbSecurityUser), typeof(DbSecurityApplication), typeof(DbSecurityDevice))
                    .Join<DbCertificateMapping, DbSecurityUser>("LEFT", o => o.SecurityUserKey, o => o.Key)
                    .Join<DbCertificateMapping, DbSecurityApplication>("LEFT", o => o.SecurityApplicationKey, o => o.Key)
                    .Join<DbCertificateMapping, DbSecurityDevice>("LEFT", o => o.SecurityDeviceKey, o => o.Key)
                    .Where<DbCertificateMapping>(o => o.X509Thumbprint == signingCertificate.Thumbprint && o.Use == CertificateMappingUse.Signature && o.ObsoletionTime == null && o.Expiration > DateTime.Now)
                    .And<DbSecurityDevice>(o => o.ObsoletionTime == null)
                    .And<DbSecurityApplication>(o => o.ObsoletionTime == null)
                    .And<DbSecurityUser>(o => o.ObsoletionTime == null)
                    .Statement;

                // Return the appropriate type of data 
                foreach (var map in context.Query<CompositeResult<DbCertificateMapping, DbSecurityUser, DbSecurityApplication, DbSecurityDevice>>(dsigSql))
                {
                    if (map.Object1.SecurityUserKey.HasValue)
                    {
                        yield return new AdoUserIdentity(map.Object2);
                    }
                    else if (map.Object1.SecurityApplicationKey.HasValue)
                    {
                        yield return new AdoApplicationIdentity(map.Object3);
                    }
                    else if (map.Object1.SecurityDeviceKey.HasValue)
                    {
                        yield return new AdoDeviceIdentity(map.Object4);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<X509Certificate2> GetSigningCertificates(IIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity), ErrorMessages.ARGUMENT_NULL);
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    context.Open(initializeExtensions: false);

                    OrmResultSet<DbCertificateMapping> retVal = null;
                    if (identity is IDeviceIdentity did)
                    {
                        retVal = context.Query<DbCertificateMapping>(context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCertificateMapping))
                            .InnerJoin<DbCertificateMapping, DbSecurityDevice>(o => o.SecurityDeviceKey, o => o.Key)
                            .Where<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == did.Name.ToLowerInvariant() && o.ObsoletionTime == null)
                            .And<DbCertificateMapping>(o => o.ObsoletionTime == null && o.Use == CertificateMappingUse.Signature)
                            .Statement);
                    }
                    else if (identity is IApplicationIdentity aid)
                    {
                        retVal = context.Query<DbCertificateMapping>(context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCertificateMapping))
                           .InnerJoin<DbCertificateMapping, DbSecurityApplication>(o => o.SecurityApplicationKey, o => o.Key)
                           .Where<DbSecurityApplication>(o => o.PublicId.ToLowerInvariant() == aid.Name.ToLowerInvariant() && o.ObsoletionTime == null)
                           .And<DbCertificateMapping>(o => o.ObsoletionTime == null && o.Use == CertificateMappingUse.Signature)
                           .Statement);
                    }
                    else
                    {
                        retVal = context.Query<DbCertificateMapping>(context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCertificateMapping))
                           .InnerJoin<DbCertificateMapping, DbSecurityUser>(o => o.SecurityUserKey, o => o.Key)
                           .Where<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == identity.Name.ToLowerInvariant() && o.ObsoletionTime == null)
                           .And<DbCertificateMapping>(o => o.ObsoletionTime == null && o.Use == CertificateMappingUse.Signature)
                           .Statement);
                    }

                    return retVal.ToList().Select(o => new X509Certificate2(o.X509PublicKeyData));
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Could not find mapped identity using identity {0}- {1}", identity.Name, e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.SIG_CERT_GENERAL), e);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<X509Certificate2> GetSigningCertificates(Type classOfIdentity, NameValueCollection filter)
        {
            if (classOfIdentity == null)
            {
                throw new ArgumentNullException(nameof(classOfIdentity), ErrorMessages.ARGUMENT_NULL);
            }
            filter = filter ?? new NameValueCollection();

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                context.Open(initializeExtensions: false);

                SqlStatementBuilder sqlStatementBuilder = null;
                if (classOfIdentity == typeof(IDeviceIdentity) || classOfIdentity == typeof(SecurityDevice))
                {
                    sqlStatementBuilder = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCertificateMapping))
                        .InnerJoin<DbCertificateMapping, DbSecurityDevice>(o => o.SecurityDeviceKey, o => o.Key)
                        .Where<DbSecurityDevice>(o => o.ObsoletionTime == null);
                }
                else if (classOfIdentity == typeof(IApplicationIdentity) || classOfIdentity == typeof(SecurityApplication))
                {
                    sqlStatementBuilder = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCertificateMapping))
                        .InnerJoin<DbCertificateMapping, DbSecurityApplication>(o => o.SecurityApplicationKey, o => o.Key)
                        .Where<DbSecurityApplication>(o => o.ObsoletionTime == null);
                }
                else
                {
                    sqlStatementBuilder = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCertificateMapping))
                        .InnerJoin<DbCertificateMapping, DbSecurityUser>(o => o.SecurityUserKey, o => o.Key)
                        .Where<DbSecurityUser>(o => o.ObsoletionTime == null);
                }

                sqlStatementBuilder.And<DbCertificateMapping>(o => o.Use == CertificateMappingUse.Signature);
                if (!String.IsNullOrEmpty(filter["thumbprint"]))
                {
                    var thb = filter["thumbprint"];
                    sqlStatementBuilder.And<DbCertificateMapping>(o => o.X509Thumbprint == thb);
                }
                if (DateTime.TryParse(filter["modifiedSince"], out var modifiedSince))
                {
                    sqlStatementBuilder.And<DbCertificateMapping>(o => (o.UpdatedTime ?? o.CreationTime) > modifiedSince);
                }
                if (DateTime.TryParse(filter["obsoleteSince"], out var obsSince))
                {
                    sqlStatementBuilder.And<DbCertificateMapping>(o => o.ObsoletionTime != null && o.ObsoletionTime > obsSince);
                }
                else
                {
                    sqlStatementBuilder.And<DbCertificateMapping>(o => o.ObsoletionTime == null);
                }

                foreach (var itm in context.Query<DbCertificateMapping>(sqlStatementBuilder.Statement).OfType<DbCertificateMapping>())
                {
                    yield return new X509Certificate2(itm.X509PublicKeyData);
                }
            }
        }

        /// <inheritdoc/>
        public void RemoveSigningCertificate(IIdentity identity, X509Certificate2 x509Certificate, IPrincipal principal)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            else if (x509Certificate == null)
            {
                throw new ArgumentNullException(nameof(x509Certificate));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }

            // Doing this to some other identity requires special permission
            if (!identity.Name.Equals(principal.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                this.m_pepService?.Demand(PermissionPolicyIdentifiers.AssignCertificateToIdentity, principal);
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);
                    // Lookup the certificate
                    DbCertificateMapping dbCertMapping = null;

                    if (identity is IDeviceIdentity did)
                    {
                        dbCertMapping = context.Query<DbCertificateMapping>(context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCertificateMapping))
                            .InnerJoin<DbCertificateMapping, DbSecurityDevice>(o => o.SecurityDeviceKey, o => o.Key)
                            .Where<DbSecurityDevice>(o => o.ObsoletionTime == null && o.PublicId.ToLowerInvariant() == did.Name.ToLowerInvariant())
                            .And<DbCertificateMapping>(o => o.ObsoletionTime == null && o.X509Thumbprint == x509Certificate.Thumbprint && o.Use == CertificateMappingUse.Signature)
                            .Statement)
                            .FirstOrDefault();
                    }
                    else if (identity is IApplicationIdentity aid)
                    {
                        dbCertMapping = context.Query<DbCertificateMapping>(context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCertificateMapping))
                            .InnerJoin<DbCertificateMapping, DbSecurityApplication>(o => o.SecurityApplicationKey, o => o.Key)
                            .Where<DbSecurityApplication>(o => o.ObsoletionTime == null && o.PublicId.ToLowerInvariant() == aid.Name.ToLowerInvariant())
                            .And<DbCertificateMapping>(o => o.ObsoletionTime == null && o.X509Thumbprint == x509Certificate.Thumbprint && o.Use == CertificateMappingUse.Signature)
                            .Statement)
                            .FirstOrDefault();
                    }
                    else
                    {
                        dbCertMapping = context.Query<DbCertificateMapping>(context.CreateSqlStatementBuilder().SelectFrom(typeof(DbCertificateMapping))
                            .InnerJoin<DbCertificateMapping, DbSecurityUser>(o => o.SecurityUserKey, o => o.Key)
                            .Where<DbSecurityUser>(o => o.ObsoletionTime == null && o.UserName.ToLowerInvariant() == identity.Name.ToLowerInvariant())
                            .And<DbCertificateMapping>(o => o.ObsoletionTime == null && o.X509Thumbprint == x509Certificate.Thumbprint && o.Use == CertificateMappingUse.Signature)
                            .Statement)
                            .FirstOrDefault();
                    }

                    if (dbCertMapping == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_NO_CERT_MAP, new { identity = identity.Name, cert = x509Certificate.Subject }));
                    }

                    dbCertMapping.ObsoletedByKey = context.EstablishProvenance(principal, null);
                    dbCertMapping.ObsoletionTime = DateTimeOffset.Now;
                    context.Update(dbCertMapping);
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Error registering signing certificate to identity {0} with {1} - {2}", identity.Name, x509Certificate.Subject, e.Message);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.SIG_CERT_REMOVE_GEN, new
                {
                    identity = identity.Name,
                    subject = x509Certificate.Subject
                }), e);
            }
        }

        /// <inheritdoc/>
        public void Trim(DataContext context, DateTimeOffset oldVersionCutoff, DateTimeOffset deletedCutoff, IAuditBuilder auditBuilder)
        {
            context.DeleteAll<DbCertificateMapping>(o => o.Use == CertificateMappingUse.Signature && o.ObsoletionTime < deletedCutoff && o.ObsoletionTime != null);
        }

        /// <inheritdoc/>
        public bool TryGetSigningCertificateByHash(byte[] certHash, out X509Certificate2 certificate)
            => this.TryGetSigningCertificateByThumbprint(certHash.HexEncode(), out certificate);

        /// <inheritdoc/>
        public bool TryGetSigningCertificateByThumbprint(string x509Thumbprint, out X509Certificate2 certificate)
        {
            if (String.IsNullOrEmpty(x509Thumbprint))
            {
                throw new ArgumentNullException(nameof(x509Thumbprint));
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    context.Open(initializeExtensions: false);

                    var candidateCertificate = context.Query<DbCertificateMapping>(o => o.ObsoletionTime == null && o.Use == CertificateMappingUse.Signature && o.X509Thumbprint == x509Thumbprint).Select(o => o.X509PublicKeyData).FirstOrDefault();
                    if (candidateCertificate == null)
                    {
                        certificate = null;
                        return false;
                    }
                    else
                    {
                        certificate = new X509Certificate2(candidateCertificate);
                        return true;
                    }
                }
            }
            catch (DbException e)
            {
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Could not lookup certificate information - {0}", e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.SIG_CERT_GENERAL), e);
            }

        }
    }
}
