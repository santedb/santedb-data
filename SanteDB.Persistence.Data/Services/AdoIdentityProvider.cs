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
 * Date: 2023-6-21
 */
using SanteDB.Core;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Security.Tfa;
using SanteDB.Core.Services;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Exceptions;
using SanteDB.Persistence.Data.Model.Entities;
using SanteDB.Persistence.Data.Model.Security;
using SanteDB.Persistence.Data.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Principal;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// An identity provider implemented for .NET
    /// </summary>
    public class AdoIdentityProvider : IIdentityProviderService, ILocalServiceProvider<IIdentityProviderService>
    {


        /// <inheritdoc/>
        public IIdentityProviderService LocalProvider => this;

        // Secret claims which should not be disclosed 
        private readonly String[] m_nonIdentityClaims =
        {
            SanteDBClaimTypes.SanteDBOTAuthCode
        };

        private readonly String[] m_allowedCallerClaims =
        {
            SanteDBClaimTypes.XspaFacilityClaim,
            SanteDBClaimTypes.PurposeOfUse,
            SanteDBClaimTypes.XspaOrganizationIdClaim,
            SanteDBClaimTypes.Language
        };

        // Tracer
        private readonly Tracer m_tracer = Tracer.GetTracer(typeof(AdoIdentityProvider));

        private Guid? m_localUserGroupKey = null;

        // Session configuration
        private readonly AdoPersistenceConfigurationSection m_configuration;

        // Hashing service
        private readonly IPasswordHashingService m_passwordHashingService;

        // Security configuration
        private readonly SecurityConfigurationSection m_securityConfiguration;

        // PEP
        private readonly IPolicyEnforcementService m_pepService;

        // TFA generator
        private readonly ITfaService m_tfaRelay;
        private readonly IDataCachingService m_dataCachingService;

        // The password validator
        private readonly IPasswordValidatorService m_passwordValidator;

        // Localization service
        private readonly ILocalizationService m_localizationService;

        /// <summary>
        /// Creates a new ADO session identity provider with injected configuration manager
        /// </summary>
        public AdoIdentityProvider(IConfigurationManager configuration,
            ILocalizationService localizationService,
            IPasswordHashingService passwordHashingService,
            IPolicyEnforcementService policyEnforcementService,
            IPasswordValidatorService passwordValidator,
            IDataCachingService dataCachingService = null,
            ITfaService twoFactorSecretGenerator = null)
        {
            this.m_configuration = configuration.GetSection<AdoPersistenceConfigurationSection>();
            this.m_securityConfiguration = configuration.GetSection<SecurityConfigurationSection>();
            this.m_passwordHashingService = passwordHashingService;
            this.m_pepService = policyEnforcementService;
            this.m_tfaRelay = twoFactorSecretGenerator;
            this.m_dataCachingService = dataCachingService;
            this.m_passwordValidator = passwordValidator;
            this.m_localizationService = localizationService;
        }

        /// <summary>
        /// Gets the service name of the identity provider
        /// </summary>
        public string ServiceName => "ADO.NET Identity Provider";

        /// <summary>
        /// Fired when the identity provider is authenticating a principal
        /// </summary>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;

        /// <summary>
        /// Fired when an identity provider has authenticated the principal
        /// </summary>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;

        /// <summary>
        /// Adds a claim to the specified user account
        /// </summary>
        /// <param name="userName">The user for which the claim is to be persisted</param>
        /// <param name="claim">The claim which is to be persisted</param>
        /// <param name="principal">The principal which is adding the claim (the authority under which the claim is being added)</param>
        /// <param name="expiry">The expiration time for the claim</param>
        public void AddClaim(string userName, IClaim claim, IPrincipal principal, TimeSpan? expiry = null)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            if (!userName.Equals(principal.Identity.Name, StringComparison.OrdinalIgnoreCase) || !principal.Identity.IsAuthenticated)
            {

                this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterIdentity, principal);
                if (ApplicationServiceContext.Current.HostType != SanteDBHostType.Server && !this.m_securityConfiguration.GetSecurityPolicy(SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts, false))
                {
                    throw new SecurityException(String.Format(ErrorMessages.POLICY_PREVENTS_ACTION, SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts));
                }

            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    var dbUser = context.Query<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null).FirstOrDefault();
                    if (dbUser == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.USR_INVALID, new { user = userName }));
                    }

                    dbUser.UpdatedByKey = context.EstablishProvenance(principal, null);
                    dbUser.UpdatedTime = DateTimeOffset.Now;

                    var dbClaim = context.FirstOrDefault<DbUserClaim>(o => o.SourceKey == dbUser.Key && o.ClaimType.ToLowerInvariant() == claim.Type.ToLowerInvariant());

                    // Current claim in DB? Update
                    if (dbClaim == null)
                    {
                        dbClaim = new DbUserClaim()
                        {
                            SourceKey = dbUser.Key
                        };
                    }
                    dbClaim.ClaimType = claim.Type;
                    dbClaim.ClaimValue = claim.Value;

                    if (expiry.HasValue)
                    {
                        dbClaim.ClaimExpiry = DateTimeOffset.Now.Add(expiry.Value).DateTime;
                    }

                    using (var tx = context.BeginTransaction())
                    {
                        dbClaim = context.InsertOrUpdate(dbClaim);
                        context.Update(dbUser);
                        tx.Commit();
                    }

                    this.m_dataCachingService?.Remove(dbUser.Key);
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error adding claim to {0} - {1}", userName, e);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USER_CLAIM_GEN_ERR), e);
                }
            }
        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(string userName, string password, IEnumerable<IClaim> clientClaimAssertions = null, IEnumerable<String> demandedScopes = null)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (String.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            return this.AuthenticateInternal(userName, password, null, clientClaimAssertions);
        }

        /// <inheritdoc/>
        public IPrincipal Authenticate(string userName, string password, string tfaSecret, IEnumerable<IClaim> clientClaimAssertions = null, IEnumerable<String> demandedScopes = null)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (String.IsNullOrEmpty(tfaSecret))
            {
                throw new ArgumentNullException(nameof(tfaSecret), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            return this.AuthenticateInternal(userName, password, tfaSecret, clientClaimAssertions);
        }

        /// <summary>
        /// Perform internal authentication routine
        /// </summary>
        /// <param name="userName">The user to authentcate</param>
        /// <param name="password">If provided, the password to authenticated</param>
        /// <param name="tfaSecret">If provided the TFA challenge response</param>
        /// <returns>The authenticated principal</returns>
        protected virtual IPrincipal AuthenticateInternal(String userName, String password, String tfaSecret, IEnumerable<IClaim> clientClaimAssertions = null)
        {
            // Allow cancellation
            var preEvtArgs = new AuthenticatingEventArgs(userName);
            this.Authenticating?.Invoke(this, preEvtArgs);
            if (preEvtArgs.Cancel)
            {
                this.m_tracer.TraceWarning("Pre-Authenticate trigger signals cancel");
                if (preEvtArgs.Success)
                {
                    return preEvtArgs.Principal;
                }
                else
                {
                    throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_CANCELLED));
                }
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    IClaimsPrincipal retVal = null;
                    using (var tx = context.BeginTransaction())
                    {
                        var dbUser = context.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null);

                        // Perform authentication
                        try
                        {
                            if (dbUser == null)
                            {
                                throw new InvalidIdentityAuthenticationException();
                            }
                            else if (dbUser.Lockout.GetValueOrDefault() > DateTimeOffset.Now)
                            {
                                throw new LockedIdentityAuthenticationException(dbUser.Lockout.Value);
                            }

                            // Claims to add to the principal
                            var claims = context.Query<DbUserClaim>(o => o.SourceKey == dbUser.Key && (o.ClaimExpiry == null || o.ClaimExpiry < DateTimeOffset.Now)).ToList();

                            if (!String.IsNullOrEmpty(password))
                            {
                                // Peppered authentication
                                var pepperSecret = this.m_configuration.GetPepperCombos(password).Select(o => this.m_passwordHashingService.ComputeHash(o));
                                // Pepper authentication
                                if (!context.Any<DbSecurityUser>(a => a.UserName.ToLowerInvariant() == userName.ToLower() && pepperSecret.Contains(a.Password)))
                                {
                                    throw new InvalidIdentityAuthenticationException();
                                }

                                if (dbUser.PasswordExpiration.HasValue && dbUser.PasswordExpiration.Value < DateTimeOffset.Now)
                                {
                                    claims.Add(new DbUserClaim() { ClaimType = SanteDBClaimTypes.PurposeOfUse, ClaimValue = PurposeOfUseKeys.SecurityAdmin.ToString() });
                                    claims.Add(new DbUserClaim() { ClaimType = SanteDBClaimTypes.SanteDBScopeClaim, ClaimValue = PermissionPolicyIdentifiers.LoginPasswordOnly });
                                    claims.Add(new DbUserClaim() { ClaimType = SanteDBClaimTypes.SanteDBScopeClaim, ClaimValue = PermissionPolicyIdentifiers.Login });
                                    claims.Add(new DbUserClaim() { ClaimType = SanteDBClaimTypes.SanteDBScopeClaim, ClaimValue = PermissionPolicyIdentifiers.ReadMetadata });
                                    claims.Add(new DbUserClaim() { ClaimType = SanteDBClaimTypes.SanteDBScopeClaim, ClaimValue = PermissionPolicyIdentifiers.LoginAnywhere });
                                    claims.Add(new DbUserClaim() { ClaimType = SanteDBClaimTypes.ForceResetPassword, ClaimValue = "true" });
                                }
                            }
                            else
                            {
                                throw new InvalidIdentityAuthenticationException();
                            }

                            // User requires TFA but the secret is empty
                            var useMfa = dbUser.TwoFactorEnabled || this.m_securityConfiguration.GetSecurityPolicy(SecurityPolicyIdentification.RequireMfa, false);
                            var mfaMechanism = dbUser.TwoFactorMechnaismKey ?? this.m_securityConfiguration.GetSecurityPolicy(SecurityPolicyIdentification.DefaultMfaMethod, (Guid?)null) ?? TfaEmailMechanism.MechanismId;
                            if (useMfa && String.IsNullOrEmpty(tfaSecret))
                            {
                                var secretString = this.m_tfaRelay.SendSecret(mfaMechanism, new AdoUserIdentity(dbUser));
                                throw new TfaRequiredAuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_TFA_REQ, new { message = this.m_localizationService.GetString(secretString) }));
                            }

                            // TFA supplied?
                            if (useMfa && !this.m_tfaRelay.ValidateSecret(mfaMechanism, new AdoUserIdentity(dbUser), tfaSecret))
                            {
                                throw new InvalidIdentityAuthenticationException();
                            }

                            // Reset invalid logins
                            dbUser.InvalidLoginAttempts = 0;
                            dbUser.LastLoginTime = DateTimeOffset.Now;
                            dbUser.Password = this.m_passwordHashingService.ComputeHash(this.m_configuration.AddPepper(password));

                            dbUser = context.Update(dbUser);

                            // Establish ID
                            var identity = new AdoUserIdentity(dbUser, "LOCAL");


                            // Add any client claims
                            if (clientClaimAssertions != null)
                            {
                                claims.AddRange(clientClaimAssertions.Where(o => this.m_allowedCallerClaims.Contains(o.Type)).Select(o => new DbUserClaim()
                                {
                                    ClaimType = o.Type,
                                    ClaimValue = o.Value
                                }));
                            }

                            // Establish role
                            var roleSql = context.CreateSqlStatementBuilder()
                                .SelectFrom(typeof(DbSecurityRole))
                                .InnerJoin<DbSecurityRole, DbSecurityUserRole>(o => o.Key, o => o.RoleKey)
                                .Where<DbSecurityUserRole>(o => o.UserKey == dbUser.Key)
                                .Statement;


                            identity.AddRoleClaims(context.Query<DbSecurityRole>(roleSql).Select(o => o.Name));

                            // Establish additional claims
                            identity.AddClaims(claims.Where(o => !this.m_nonIdentityClaims.Contains(o.ClaimType)).Select(o => new SanteDBClaim(o.ClaimType, o.ClaimValue)));
                            identity.AddXspaClaims(context);

                            // Add the default language 
                            var prefLangSql = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbPersonLanguageCommunication))
                                .InnerJoin<DbPersonLanguageCommunication, DbEntityVersion>(o => o.SourceKey, o => o.Key)
                                .InnerJoin<DbEntityVersion, DbUserEntity>(o => o.VersionKey, o => o.ParentKey)
                                .Where<DbUserEntity>(o => o.SecurityUserKey == dbUser.Key)
                                .And<DbPersonLanguageCommunication>(o => o.IsPreferred == true && o.ObsoleteVersionSequenceId == null)
                                .And<DbEntityVersion>(o => o.IsHeadVersion)
                                .Statement;
                            var preferredLanguage = context.Query<DbPersonLanguageCommunication>(prefLangSql).Select(o => o.LanguageCode).FirstOrDefault();
                            if (!String.IsNullOrEmpty(preferredLanguage))
                            {
                                identity.AddClaim(new SanteDBClaim(SanteDBClaimTypes.Language, preferredLanguage));
                            }

                            // Create principal
                            retVal = new AdoClaimsPrincipal(identity);

                        }
                        catch (LockedIdentityAuthenticationException e)
                        {
                            throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_LOCKED, new { time = e.TimeLockExpires }), e);
                        }
                        catch (InvalidIdentityAuthenticationException) when (dbUser != null)
                        {
                            dbUser.InvalidLoginAttempts = dbUser.InvalidLoginAttempts.GetValueOrDefault() + 1;
                            if (dbUser.InvalidLoginAttempts > this.m_securityConfiguration.GetSecurityPolicy<Int32>(SecurityPolicyIdentification.MaxInvalidLogins, 5))
                            {
                                var lockoutSlide = 30 * dbUser.InvalidLoginAttempts.Value;
                                if (DateTimeOffset.Now < DateTimeOffset.MaxValue.AddSeconds(-lockoutSlide))
                                {
                                    dbUser.Lockout = DateTimeOffset.Now.AddSeconds(lockoutSlide);
                                }
                            }
                            context.Update(dbUser);
                            throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_INVALID));
                        }
                        catch (InvalidIdentityAuthenticationException)
                        {
                            throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_INVALID));
                        }
                        finally
                        {
                            tx.Commit();
                        }

                    }

                    if (retVal.HasClaim(o => o.Type == SanteDBClaimTypes.ForceResetPassword))
                    {
                        throw new PasswordExpiredException(retVal);
                    }
                    else
                    {
                        this.m_pepService.Demand(PermissionPolicyIdentifiers.Login, retVal);
                    }

                    // Fire authentication
                    this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, retVal, true));
                    return retVal;
                }
                catch (AuthenticationException)
                {
                    this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, null, false));
                    throw;
                }
                catch (Exception e)
                {
                    this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, null, false));
                    this.m_tracer.TraceError("Could not authenticate user {0} - {1}", userName, e);
                    throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_GENERAL), e);
                }
            }
        }

        /// <summary>
        /// Change the specified user password
        /// </summary>
        /// <param name="userName">The user who's password is being changed</param>
        /// <param name="newPassword">The new password to set</param>
        /// <param name="principal">The principal which is setting the password</param>
        /// <param name="isSynchronizationOperation">True to bypass validation for the password change. False otherwise.</param>
        public void ChangePassword(string userName, string newPassword, IPrincipal principal, bool isSynchronizationOperation = false)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (String.IsNullOrEmpty(newPassword))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (!isSynchronizationOperation && !this.m_passwordValidator.Validate(newPassword))
            {
                throw new DetectedIssueException(Core.BusinessRules.DetectedIssuePriorityType.Error, "password.complexity", this.m_localizationService.GetString(ErrorMessageStrings.USR_PWD_COMPLEXITY), DetectedIssueKeys.SecurityIssue, null);
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            // The user changing the password must be the user or an administrator
            if (!principal.Identity.Name.Equals(userName, StringComparison.OrdinalIgnoreCase) || !principal.Identity.IsAuthenticated)
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.ChangePassword, principal);
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    using (var tx = context.BeginTransaction())
                    {
                        // Get the user
                        var dbUser = context.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null);
                        if (dbUser == null)
                        {
                            throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.USR_INVALID));
                        }

                        // Password reuse policy?
                        if (!isSynchronizationOperation && this.m_securityConfiguration.GetSecurityPolicy<bool>(SecurityPolicyIdentification.PasswordHistory) && this.m_configuration.GetPepperCombos(newPassword).Any(o => this.m_passwordHashingService.ComputeHash(o) == dbUser.Password))
                        {
                            throw new DetectedIssueException(Core.BusinessRules.DetectedIssuePriorityType.Error, "password.history", this.m_localizationService.GetString(ErrorMessageStrings.USR_PWD_HISTORY), DetectedIssueKeys.SecurityIssue, null);
                        }

                        if (isSynchronizationOperation) // the password is changing to synchronize 
                        {
                            dbUser.LastLoginTime = DateTimeOffset.Now;
                        }

                        dbUser.Password = this.m_passwordHashingService.ComputeHash(this.m_configuration.AddPepper(newPassword));
                        dbUser.UpdatedByKey = context.EstablishProvenance(principal, null);
                        dbUser.UpdatedTime = DateTimeOffset.Now;
                        dbUser.SecurityHash = this.m_passwordHashingService.ComputeHash(userName + newPassword);
                        // Password expire policy
                        var pwdExpire = this.m_securityConfiguration.GetSecurityPolicy<TimeSpan>(SecurityPolicyIdentification.MaxPasswordAge);
                        if (pwdExpire != default(TimeSpan) && AuthenticationContext.Current.Principal != AuthenticationContext.SystemPrincipal) // system principal setting password doesn't expire
                        {
                            dbUser.PasswordExpiration = DateTimeOffset.Now.Add(pwdExpire);
                        }
                        else
                        {
                            dbUser.PasswordExpiration = null;
                            dbUser.PasswordExpirationSpecified = true;
                        }

                        // Abandon all sessions for this user
                        if (this.m_securityConfiguration.GetSecurityPolicy(SecurityPolicyIdentification.AbandonSessionAfterPasswordReset, false))
                        {
                            foreach (var ses in context.Query<DbSession>(o => o.UserKey == dbUser.Key && o.NotAfter >= DateTimeOffset.Now).ToArray())
                            {
                                ses.NotAfter = DateTimeOffset.Now;
                                context.Update(ses);
                            }
                        }

                        // Save user
                        dbUser = context.Update(dbUser);

                        tx.Commit();
                        this.m_dataCachingService?.Remove(dbUser.Key);

                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error updating user password - {0}", e);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_PWD_GEN_ERR, new { userName = userName }), e);
                }
            }
        }

        /// <summary>
        /// Create a security identity for the specified
        /// </summary>
        /// <param name="userName">The name of the user identity which is to be created</param>
        /// <param name="password">The initial password to set for the principal</param>
        /// <param name="principal">The principal which is creating the identity</param>
        /// <returns>The created identity</returns>
        public IIdentity CreateIdentity(string userName, string password, IPrincipal principal, Guid? withSid = null)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (String.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (!this.m_passwordValidator.Validate(password) && principal != AuthenticationContext.SystemPrincipal)
            {
                throw new DetectedIssueException(Core.BusinessRules.DetectedIssuePriorityType.Error, "password.complexity", this.m_localizationService.GetString(ErrorMessageStrings.USR_PWD_COMPLEXITY), DetectedIssueKeys.SecurityIssue, null);
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            // Validate create permission
            if (principal != AuthenticationContext.SystemPrincipal)
            {
                    this.m_pepService.Demand(PermissionPolicyIdentifiers.CreateIdentity, principal);
                    if (ApplicationServiceContext.Current.HostType != SanteDBHostType.Server && !this.m_securityConfiguration.GetSecurityPolicy(SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts, false))
                    {
                        throw new SecurityException(String.Format(ErrorMessages.POLICY_PREVENTS_ACTION, SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts));
                    }
            }
            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    using (var tx = context.BeginTransaction())
                    {
                        // Construct the request
                        var newIdentity = new DbSecurityUser()
                        {
                            Key = withSid ?? Guid.NewGuid(),
                            UserName = userName,
                            Password = this.m_passwordHashingService.ComputeHash(this.m_configuration.AddPepper(password)),
                            SecurityHash = this.m_passwordHashingService.ComputeHash(userName + password),
                            UserClass = ActorTypeKeys.HumanUser,
                            InvalidLoginAttempts = 0,
                            CreatedByKey = context.EstablishProvenance(principal, null),
                            CreationTime = DateTimeOffset.Now
                        };

                        var expirePwd = this.m_securityConfiguration.GetSecurityPolicy<TimeSpan>(SecurityPolicyIdentification.MaxPasswordAge);
                        if (expirePwd != default(TimeSpan))
                        {
                            newIdentity.PasswordExpiration = DateTimeOffset.Now.Add(expirePwd);
                        }

                        newIdentity = context.Insert(newIdentity);

                        // Register the group
                        context.InsertAll(context.Query<DbSecurityRole>(context.CreateSqlStatementBuilder()
                            .SelectFrom(typeof(DbSecurityRole))
                            .Where<DbSecurityRole>(o => o.Name == SanteDBConstants.UserGroupName)
                            .Statement)
                            .ToArray()
                            .Select(o => new DbSecurityUserRole()
                            {
                                RoleKey = o.Key,
                                UserKey = newIdentity.Key
                            }));

                        tx.Commit();
                        return new AdoUserIdentity(newIdentity);
                    }
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error creating identity {0} - {1}", userName, e.Message);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_CREATE_GEN, new { userName = userName }), e);
                }
            }
        }

        /// <summary>
        /// Delete the specified identity
        /// </summary>
        public void DeleteIdentity(string userName, IPrincipal principal)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterIdentity, principal);

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    // Obsolete user
                    var dbUser = context.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null);
                    if (dbUser == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND));
                    }

                    dbUser.ObsoletionTime = DateTimeOffset.Now;
                    dbUser.ObsoletedByKey = context.EstablishProvenance(principal, null);
                    context.Update(dbUser);
                    this.m_dataCachingService?.Remove(dbUser.Key);

                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Could not delete identity {0} - {1}", userName, e.Message);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_DEL_ERR, new { userName = userName }), e);
                }
            }
        }

        /// <summary>
        /// Get an unauthenticated identity for the specified username
        /// </summary>
        /// <param name="userName">The user to fetch the identity for</param>
        /// <returns>The un-authenticated identity</returns>
        public IIdentity GetIdentity(string userName)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    var dbUser = context.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null);
                    if (dbUser == null)
                    {
                        return null;
                    }

                    var dbClaims = context.Query<DbUserClaim>(o => o.SourceKey == dbUser.Key &&
                        (o.ClaimExpiry == null || o.ClaimExpiry > DateTimeOffset.Now));
                    var retVal = new AdoUserIdentity(dbUser);
                    retVal.AddClaims(dbClaims.ToArray().Where(o => !this.m_nonIdentityClaims.Contains(o.ClaimType)).Select(o => new SanteDBClaim(o.ClaimType, o.ClaimValue)));

                    // Establish role
                    var roleSql = context.CreateSqlStatementBuilder()
                                .SelectFrom(typeof(DbSecurityRole))
                                .InnerJoin<DbSecurityRole, DbSecurityUserRole>(o => o.Key, o => o.RoleKey)
                                .Where<DbSecurityUserRole>(o => o.UserKey == dbUser.Key)
                                .Statement;
                    retVal.AddRoleClaims(context.Query<DbSecurityRole>(roleSql).Select(o => o.Name));

                    return retVal;
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error fetching user identity {0} - {1}", userName, e.Message);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_GEN_ERR, new { userName = userName }), e);
                }
            }
        }

        /// <summary>
        /// Get the user identity by security ID
        /// </summary>
        public IIdentity GetIdentity(Guid sid)
        {
            if (sid == Guid.Empty)
            {
                throw new ArgumentNullException(nameof(sid), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    var dbUser = context.FirstOrDefault<DbSecurityUser>(o => o.Key == sid && o.ObsoletionTime == null);
                    if (dbUser == null)
                    {
                        return null;
                    }

                    var retVal = new AdoUserIdentity(dbUser);
                    var claims = context.Query<DbUserClaim>(o => o.SourceKey == dbUser.Key && o.ClaimExpiry < DateTimeOffset.Now).ToList();
                    retVal.AddClaims(claims.Select(o => new SanteDBClaim(o.ClaimType, o.ClaimValue)));
                    return retVal;
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error fetching user identity {0} - {1}", sid, e.Message);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_GEN_ERR, new { sid = sid }), e);
                }
            }
        }

        /// <summary>
        /// Gets the user sid by user name
        /// </summary>
        public Guid GetSid(string userName)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    var dbUser = context.Query<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null)
                        .Select(o => o.Key).FirstOrDefault();
                    if (dbUser == null)
                    {
                        return Guid.Empty;
                    }

                    return dbUser;
                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error fetching user identity {0} - {1}", userName, e.Message);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_GEN_ERR, new { userName = userName }), e);
                }
            }
        }

        /// <summary>
        /// Remove a claim from the specified user profile
        /// </summary>
        public void RemoveClaim(string userName, string claimType, IPrincipal principal)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (String.IsNullOrEmpty(claimType))
            {
                throw new ArgumentNullException(nameof(claimType), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            // User can remove their own claims
            if (!userName.Equals(principal.Identity.Name, StringComparison.OrdinalIgnoreCase) || !principal.Identity.IsAuthenticated)
            {
                this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterIdentity, principal);
                if (ApplicationServiceContext.Current.HostType != SanteDBHostType.Server && !this.m_securityConfiguration.GetSecurityPolicy(SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts, false))
                {
                    throw new SecurityException(String.Format(ErrorMessages.POLICY_PREVENTS_ACTION, SecurityPolicyIdentification.AllowLocalDownstreamUserAccounts));
                }
            }

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    var dbUser = context.Query<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null).FirstOrDefault();
                    if (dbUser == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.USR_INVALID, new { user = userName }));
                    }

                    dbUser.UpdatedByKey = context.EstablishProvenance(principal);
                    dbUser.UpdatedTime = DateTimeOffset.Now;

                    using (var tx = context.BeginTransaction())
                    {
                        context.DeleteAll<DbUserClaim>(o => o.SourceKey == dbUser.Key && o.ClaimType.ToLowerInvariant() == claimType.ToLowerInvariant());
                        context.Update(dbUser);
                        tx.Commit();
                    }
                    this.m_dataCachingService?.Remove(dbUser.Key);

                }
                catch (Exception e)
                {
                    this.m_tracer.TraceError("Error removing claim to {0} - {1}", userName, e);
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USER_CLAIM_GEN_ERR), e);
                }
            }
        }

        /// <inheritdoc/>
        public void ExpirePassword(String userName, IPrincipal principal)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.ChangePassword, principal);
            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    var dbUser = context.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null);
                    if (dbUser == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND, new { id = userName }));
                    }

                    dbUser.UpdatedByKey = context.EstablishProvenance(principal, null);
                    dbUser.UpdatedTime = DateTimeOffset.Now;
                    dbUser.PasswordExpiration = DateTimeOffset.Now;

                    context.Update(dbUser);
                    this.m_dataCachingService?.Remove(dbUser.Key);

                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_GEN_ERR), e);
                }
            }
        }

        /// <summary>
        /// Set the lockout status of the user
        /// </summary>
        /// <param name="userName">The user to set the lockout status for</param>
        /// <param name="lockout">The lockout status</param>
        /// <param name="principal">The principal which is performing the lockout</param>
        public void SetLockout(string userName, bool lockout, IPrincipal principal)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), this.m_localizationService.GetString(ErrorMessageStrings.ARGUMENT_NULL));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.AlterIdentity, principal);

            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    var dbUser = context.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null);
                    if (dbUser == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND, new { id = userName }));
                    }

                    dbUser.UpdatedByKey = context.EstablishProvenance(principal, null);
                    dbUser.UpdatedTime = DateTimeOffset.Now;
                    dbUser.Lockout = lockout ? (DateTimeOffset?)DateTimeOffset.MaxValue.ToLocalTime() : null;
                    dbUser.LockoutSpecified = true;
                    
                    context.Update(dbUser);
                    this.m_dataCachingService?.Remove(dbUser.Key);

                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_GEN_ERR), e);
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IClaim> GetClaims(String userName)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName));
            }

            using (var context = this.m_configuration.Provider.GetReadonlyConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);

                    var dbUserKey = context.Query<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null).Select(o => o.Key).FirstOrDefault();
                    if (dbUserKey == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.NOT_FOUND, new { id = userName }));
                    }
                    return context.Query<DbUserClaim>(o => o.SourceKey == dbUserKey && o.ClaimExpiry == null || o.ClaimExpiry > DateTime.Now).ToArray().Select(o => new SanteDBClaim(o.ClaimType, o.ClaimValue));
                }
                catch (Exception e)
                {
                    throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USER_CLAIM_GEN_ERR), e);
                }
            }
        }

        /// <inheritdoc/>
        public IPrincipal ReAuthenticate(IPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), ErrorMessages.ARGUMENT_NULL);
            }
            else if (!principal.Identity.IsAuthenticated || !(principal is IClaimsPrincipal claimsPrincipal))
            {
                throw new SecurityException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_REAUTH_NOT_ALLOWED));
            }

            this.m_pepService.Demand(PermissionPolicyIdentifiers.Login, principal); // Re-validate

            // Create an ADO principal off this principal
            using (var context = this.m_configuration.Provider.GetWriteConnection())
            {
                try
                {
                    context.Open(initializeExtensions: false);
                    var dbUser = context.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == principal.Identity.Name.ToLowerInvariant() && o.ObsoletionTime == null);

                    if (dbUser == null)
                    {
                        throw new InvalidIdentityAuthenticationException();
                    }
                    else if (dbUser.Lockout > DateTimeOffset.Now)
                    {
                        throw new LockedIdentityAuthenticationException(dbUser.Lockout.Value);
                    }

                    dbUser.UpdatedTime = dbUser.LastLoginTime = DateTimeOffset.Now;
                    dbUser.UpdatedByKey = Guid.Parse(AuthenticationContext.SystemUserSid);

                    context.Update(dbUser);

                    var claims = context.Query<DbUserClaim>(o => o.SourceKey == dbUser.Key && o.ClaimExpiry < DateTimeOffset.Now).ToList();

                    // Establish ID
                    var identity = new AdoUserIdentity(dbUser, "LOCAL");

                    // Get roles
                    var roleSql = context.CreateSqlStatementBuilder()
                        .SelectFrom(typeof(DbSecurityRole))
                        .InnerJoin<DbSecurityRole, DbSecurityUserRole>(o => o.Key, o => o.RoleKey)
                        .Where<DbSecurityUserRole>(o => o.UserKey == dbUser.Key)
                        .Statement;
                    identity.AddRoleClaims(context.Query<DbSecurityRole>(roleSql).Select(o => o.Name));

                    // Establish additional claims
                    identity.AddClaims(claims.Where(o => !this.m_nonIdentityClaims.Contains(o.ClaimType)).Select(o => new SanteDBClaim(o.ClaimType, o.ClaimValue)));

                    // Create principal
                    var retVal = new AdoClaimsPrincipal(identity);
                    this.m_pepService.Demand(PermissionPolicyIdentifiers.Login, retVal);

                    // Fire authentication
                    this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(retVal.Identity.Name, retVal, true));
                    return retVal;
                }
                catch (AuthenticationException)
                {
                    throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_INVALID));
                }
            }
        }

        /// <inheritdoc/>
        public AuthenticationMethod GetAuthenticationMethods(string userName)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName));
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetReadonlyConnection())
                {
                    context.Open(initializeExtensions: false);
                    if (context.Any<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null))
                    {
                        return AuthenticationMethod.Local;
                    }
                    else
                    {
                        return (AuthenticationMethod)0;
                    }
                }
            }
            catch (Exception e)
            {
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_GEN_ERR), e);
            }
        }


    }
}