/*
 * Copyright (C) 2021 - 2026, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
using DocumentFormat.OpenXml.InkML;
using SanteDB.Core.Configuration;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Constants;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Configuration;
using SanteDB.Core.Security.Services;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Exceptions;
using SanteDB.Persistence.Data.Model.Security;
using SanteDB.Persistence.Data.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Principal;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// Represents a callenge service which uses the ADO.NET tables
    /// </summary>
    public class AdoSecurityChallengeProvider : ISecurityChallengeService, ISecurityChallengeIdentityService, ILocalServiceProvider<ISecurityChallengeIdentityService>, ILocalServiceProvider<ISecurityChallengeService>
    {
                        
        const string AUTHENTICATIONMETHOD_LOCALCHALLENGE = "LOCAL_CHALLENGE";


        /// <inheritdoc/>
        ISecurityChallengeIdentityService ILocalServiceProvider<ISecurityChallengeIdentityService>.LocalProvider => this;
        /// <inheritdoc/>
        ISecurityChallengeService ILocalServiceProvider<ISecurityChallengeService>.LocalProvider => this;

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AdoSecurityChallengeProvider));

        // Configuration section
        private AdoPersistenceConfigurationSection m_configuration;

        // Security Configuration section
        private SecurityConfigurationSection m_securityConfiguration;

        // Localization service
        private readonly ILocalizationService m_localizationService;
        private readonly IPasswordHashingService m_passwordHashingService;
        private readonly ITfaService m_tfaRelay;

        // Policy enforcement
        private IPolicyEnforcementService m_policyEnforcementService;

        // The randomizer
        private Random m_random = new Random();

        /// <summary>
        /// DI constructor for ADO CHallenge
        /// </summary>
        public AdoSecurityChallengeProvider(IConfigurationManager configurationManager, IPolicyEnforcementService pepService, ILocalizationService localizationService,
            IPasswordHashingService passwordHashingService, ITfaService tfaRelayService = null)
        {
            this.m_policyEnforcementService = pepService;
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_securityConfiguration = configurationManager.GetSection<SecurityConfigurationSection>();
            this.m_localizationService = localizationService;
            this.m_passwordHashingService = passwordHashingService;
            this.m_tfaRelay = tfaRelayService;
        }

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "ADO.NET Challenge Service";

        /// <summary>
        /// Service is authenticating
        /// </summary>
        public event EventHandler<AuthenticatingEventArgs> Authenticating;

        /// <summary>
        /// Authentication has succeeded
        /// </summary>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;



        /// <summary>
        /// Authenticate a user using their challenge response
        /// </summary>
        /// <param name="challengeKey">The challenge which was answered</param>
        /// <param name="response">The response to the challenge</param>
        /// <param name="tfaSecret">The TFA one time password to fulfill the authentication request</param>
        /// <param name="userName">The name of the user which is being authenticated</param>
        public IPrincipal Authenticate(string userName, Guid challengeKey, string response, string tfaSecret)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentNullException(nameof(userName), ErrorMessages.ARGUMENT_NULL);

            if (string.IsNullOrWhiteSpace(response))
                throw new ArgumentNullException(nameof(response), ErrorMessages.ARGUMENT_NULL);

            //Normalize the username for all operations.
            userName = userName.ToLowerInvariant();

            response = response.Trim();

            if (!string.IsNullOrEmpty(tfaSecret))
                tfaSecret = tfaSecret.Trim();

#if DEBUG
            m_tracer.TraceVerbose("{0}.{1} for userName {2}", nameof(AdoSecurityChallengeProvider), nameof(Authenticate), userName);
#endif

            m_tracer.TraceVerbose("Calling Pre-Authenticate trigger.");

            var preEvtArgs = new AuthenticatingEventArgs(userName);
            this.Authenticating?.Invoke(this, preEvtArgs);
            if (preEvtArgs.Cancel)
            {
                if (preEvtArgs.Success)
                {
                    this.m_tracer.TraceInfo("Pre-Authenticate trigger signals success with a principal.");
                    return preEvtArgs.Principal;
                }
                else
                {
                    throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_CANCELLED));
                }
            }

            try
            {
                var principal = AuthenticateInternal(userName, challengeKey, response, tfaSecret, DateTimeOffset.Now);
                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, principal, true));
                return principal;
            }
            catch (AuthenticationException)
            {
                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, null, false)); //TODO: Provide the exception to the event.
                throw;
            }
            catch (Exception ex) when (!(ex is StackOverflowException || ex is OutOfMemoryException))
            {
                this.Authenticated?.Invoke(this, new AuthenticatedEventArgs(userName, null, false));

                this.m_tracer.TraceError("Could not authenticate user {0}- {1]", userName, ex); //TODO: Determine if this is needed, a caught error should print out to the tracer.

                throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_GENERAL), ex);
            }
        }

        /// <summary>
        /// Handles the internal authentication process for this service.
        /// </summary>
        /// <param name="userName">The pre-normalized username being authenticated. Call <see cref="String.ToLowerInvariant" /> before calling <see cref="AuthenticateInternal(string, Guid, string, string)"/></param>
        /// <param name="challengeKey">When using a TFA challenge, set this to <see cref="ITfaMechanism.Id"/>. When using a security challenge, set this to the id of the security challenge. When using an expired password set, set this to <see cref="Guid.Empty"/>.</param>
        /// <param name="response">When <paramref name="challengeKey"/> is a TFA mechanism, this value is ignored. When <paramref name="challengeKey"/> is a security challenge, set this to the security response. When this is for an expired password, set this to the expired password.</param>
        /// <param name="tfaSecret">When <paramref name="challengeKey"/> is a TFA mechanism, set this to the response to the TFA mechanism challenge.</param>
        /// <param name="authenticationTime">The time the authentication is taking place.</param>
        /// <returns>An <see cref="IPrincipal"/> instance backed by a short lived session which can be used to reset the password of the user which can then be used to obtain a normal session. <em>If autentication is not successful, an exception is thrown.</em></returns>
        /// <remarks>
        /// This method does not perform any argument null checking.
        /// </remarks>
        protected virtual IPrincipal AuthenticateInternal(string userName, Guid challengeKey, string response, string tfaSecret, DateTimeOffset authenticationTime)
        {
            using (var ctx = m_configuration.Provider.GetWriteConnection())
            {
                ctx.Open(initializeExtensions: false);

                var dbuser = ctx.FirstOrDefault<DbSecurityUser>(usr => usr.UserName.ToLowerInvariant() == userName && usr.ObsoletionTime == null);

                try
                {
                    if (null == dbuser)
                        throw new InvalidIdentityAuthenticationException("SDBAUTH001: Invalid Username.");
                    else if (dbuser.Lockout.GetValueOrDefault() > authenticationTime)
                        throw new LockedIdentityAuthenticationException(dbuser.Lockout.Value);

                    var ischallengetfamechanism = m_tfaRelay?.Mechanisms?.Any(mech => mech.Id == challengeKey) == true;

                    //Set to true when m_tfaRelay.ValidateSecret() was called for tfaSecret and succeeded.
                    bool tfavalidatepassed = false;

                    //This temp identity is used when sending TFA messages or other auth related processing.
                    //It is not the final identity that will be returned.
                    var temporaryidentity = new AdoUserIdentity(dbuser);

                    if (ischallengetfamechanism && string.IsNullOrEmpty(tfaSecret))
                        tfaSecret = response; //TODO: This should be fixed in the UI; The TFA for the response could be different from the tfa for the user.

                    //Do we need to validate or send a tfa code?
                    if (dbuser.TwoFactorEnabled)
                    {
                        if (string.IsNullOrEmpty(tfaSecret)
                            && dbuser.TwoFactorMechnaismKey.HasValue && null != m_tfaRelay)
                        {
#if DEBUG
                            m_tracer.TraceVerbose("User has Two Factor Enabled and challenge has no TFA secret. Sending secret.");
#endif
                            var responsemessage = m_tfaRelay.SendSecret(dbuser.TwoFactorMechnaismKey.Value, temporaryidentity);
                            throw new TfaRequiredAuthenticationException(m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_TFA_REQ, new { message = m_localizationService.GetString(responsemessage) }));
                        }

                        if (!m_tfaRelay.ValidateSecret(dbuser.TwoFactorMechnaismKey.Value, temporaryidentity, tfaSecret))
                            throw new InvalidIdentityAuthenticationException("SDBAUTH002: TFA Validation failed.");

                        tfavalidatepassed = true;
                    }

                    if (challengeKey == Guid.Empty)
                    {
                        //Case 1 - Expired Password challenge
#if DEBUG
                        m_tracer.TraceVerbose("Using expired password challenge process.");
#endif

                        if (!dbuser.PasswordExpiration.HasValue)
                            throw new InvalidIdentityAuthenticationException("SDBAUTH004: User password is not expired.");
                        else if (authenticationTime < dbuser.PasswordExpiration.Value)
                            throw new InvalidIdentityAuthenticationException("SDBAUTH005: User password is not expired.");

                        var passwordpeppers = GetPepperCombos(response);

                        if (!ctx.Any<DbSecurityUser>(usr => 
                            usr.UserName == userName && 
                            usr.ObsoletionTime == null && 
                            passwordpeppers.Contains(usr.Password)))
                            throw new InvalidIdentityAuthenticationException("SDBAUTH006: User password does not match.");

                    }
                    else if (ischallengetfamechanism)
                    {
                        //Case 2 - MFA Challenge
#if DEBUG
                        m_tracer.TraceVerbose("Using TFA challenge process.");
#endif

                        if (!tfavalidatepassed && m_tfaRelay?.ValidateSecret(challengeKey, temporaryidentity, response) != true)
                            throw new InvalidIdentityAuthenticationException("SDBAUTH003: TFA Validation failed.");
                    }
                    else
                    {
                        //Case 3 - Security Question challenge.
#if DEBUG
                        m_tracer.TraceVerbose("Using security question challenge process.");
#endif
                        var challengeresponsepeppers = GetPepperCombos(response);

                        if (!ctx.Any<DbSecurityUserChallengeAssoc>(challenge =>
                            challenge.ChallengeKey == challengeKey &&
                            challenge.ExpiryTime > authenticationTime &&
                            challengeresponsepeppers.Contains(challenge.ChallengeResponse)))
                            throw new InvalidIdentityAuthenticationException("SDBAUTH007: Security challenge is invalid or expired.");
                        
                    }

                    //If an exception has not been raised already, we can issue the reset identity.
                    var identity = new AdoUserIdentity(dbuser, AUTHENTICATIONMETHOD_LOCALCHALLENGE);

                    //Create a temporary identity for the user for demanding policies.
                    m_policyEnforcementService.Demand(PermissionPolicyIdentifiers.Login, new AdoClaimsPrincipal(identity));

                    // Establish role
                    var roleSql = ctx.CreateSqlStatementBuilder()
                        .SelectFrom(typeof(DbSecurityRole))
                        .InnerJoin<DbSecurityRole, DbSecurityUserRole>(o => o.Key, o => o.RoleKey)
                        .Where<DbSecurityUserRole>(o => o.UserKey == dbuser.Key)
                        .And<DbSecurityRole>(o => o.ObsoletionTime == null)
                        .Statement;
                    identity.AddRoleClaims(ctx.Query<DbSecurityRole>(roleSql).Select(o => o.Name));

                    // Establish additional claims
                    identity.AddClaim(new SanteDBClaim(SanteDBClaimTypes.PurposeOfUse, PurposeOfUseKeys.SecurityAdmin.ToString()));
                    identity.AddClaim(new SanteDBClaim(SanteDBClaimTypes.SanteDBScopeClaim, PermissionPolicyIdentifiers.ReadMetadata));
                    identity.AddClaim(new SanteDBClaim(SanteDBClaimTypes.SanteDBScopeClaim, PermissionPolicyIdentifiers.LoginPasswordOnly));
                    identity.AddClaim(new SanteDBClaim(SanteDBClaimTypes.SanteDBScopeClaim, PermissionPolicyIdentifiers.Login));
                    identity.AddClaim(new SanteDBClaim(SanteDBClaimTypes.SanteDBScopeClaim, PermissionPolicyIdentifiers.LoginAnywhere));
                    identity.AddClaim(new SanteDBClaim(SanteDBClaimTypes.ForceResetPassword, "true"));

                    return new AdoClaimsPrincipal(identity);
                }
                catch (LockedIdentityAuthenticationException liaex)
                {
                    throw new AuthenticationException(m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_LOCKED), liaex);
                }
                catch (InvalidIdentityAuthenticationException iiaex)
                {

                    //Increment the invalid login attempts and lock out the user.
                    if (null != dbuser)
                    {
#if DEBUG
                        m_tracer.TraceVerbose("Incrementing invalid login attempt for {0}", userName);
#endif

                        dbuser.InvalidLoginAttempts = dbuser.InvalidLoginAttempts.GetValueOrDefault() + 1;
                        if (dbuser.InvalidLoginAttempts > this.m_securityConfiguration.GetSecurityPolicy<Int32>(SecurityPolicyIdentification.MaxInvalidLogins, 5))
                        {
                            var lockoutSlide = 30 * dbuser.InvalidLoginAttempts.Value;
                            if (authenticationTime < DateTimeOffset.MaxValue.AddSeconds(-lockoutSlide))
                            {
                                dbuser.Lockout = authenticationTime.AddSeconds(lockoutSlide);
                            }
                        }
                        dbuser.UpdatedByKey = Guid.Parse(AuthenticationContext.SystemUserSid);
                        dbuser.UpdatedTime = DateTimeOffset.Now;
                        ctx.Update(dbuser);
                    }

                    m_tracer.TraceWarning("Security challenge authentication failed: {0}", iiaex);

                    //SECURITY CRITICAL - DO NOT PROPAGATE INTERNAL ERROR IT WILL BE SERIALIZED IN AN API RESPONSE.
                    throw new AuthenticationException(this.m_localizationService.GetString(ErrorMessageStrings.AUTH_USR_INVALID));
                    //SECURITY CRITICAL
                }
            }
        }

        /// <summary>
        /// Gets an enumeration of all possible pepper combinations for a given input string.
        /// </summary>
        /// <param name="input">The input to be peppered.</param>
        /// <returns>An enumeration of all pepper combos for the input string.</returns>
        private IEnumerable<string> GetPepperCombos(string input)
            => m_configuration.GetPepperCombos(input).Select(pepper => m_passwordHashingService.ComputeHash(pepper));

        /// <summary>
        /// Get the security challenges for the specified user key
        /// </summary>
        /// <param name="principal">The principal that is requesting the security challenge information</param>
        /// <param name="userName">The user name which is requesting the challenge information</param>
        public IEnumerable<SecurityChallenge> Get(String userName, IPrincipal principal)
        {

            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), ErrorMessages.ARGUMENT_NULL);
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), ErrorMessages.ARGUMENT_NULL);
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {

                    context.Open(initializeExtensions: false);
                    var sqlQuery = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbSecurityChallenge), typeof(DbSecurityUserChallengeAssoc))
                            .InnerJoin<DbSecurityChallenge, DbSecurityUserChallengeAssoc>(o => o.Key, o => o.ChallengeKey)
                            .InnerJoin<DbSecurityUserChallengeAssoc, DbSecurityUser>(o => o.UserKey, o => o.Key)
                            .Where<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null)
                            .Statement;

                    var retVal = context.Query<CompositeResult<DbSecurityChallenge, DbSecurityUserChallengeAssoc>>(sqlQuery).ToArray().Select(o => new SecurityChallenge()
                    {
                        ChallengeText = o.Object1.ChallengeText,
                        Key = o.Object1.Key,
                        ObsoletionTime = o.Object2.ExpiryTime
                    }).ToList();

                    // Only the current user can fetch their own security challenge questions 
                    if (!userName.Equals(principal.Identity.Name, StringComparison.OrdinalIgnoreCase)
                        || !principal.Identity.IsAuthenticated)
                    {
                        return retVal.Skip(this.m_random.Next(0, retVal.Count)).Take(1);
                    }
                    else // Only a random option can be returned  
                    {
                        return retVal;
                    }

                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Failed to fetch security challenges for user {0}: {1}", userName, e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_CHL_GEN_ERR, new { userName = userName }), e);
            }
        }

        /// <summary>
        /// Get the specified challenges
        /// </summary>
        public IEnumerable<SecurityChallenge> Get(Guid userKey, IPrincipal principal)
        {
            if (userKey == default(Guid))
            {
                throw new ArgumentOutOfRangeException(nameof(userKey), ErrorMessages.ARGUMENT_OUT_OF_RANGE);
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), ErrorMessages.ARGUMENT_NULL);
            }

            try
            {

                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {

                    context.Open(initializeExtensions: false);
                    var sqlQuery = context.CreateSqlStatementBuilder().SelectFrom(typeof(DbSecurityChallenge), typeof(DbSecurityUserChallengeAssoc))
                            .InnerJoin<DbSecurityChallenge, DbSecurityUserChallengeAssoc>(o => o.Key, o => o.ChallengeKey)
                            .Where<DbSecurityUserChallengeAssoc>(o => o.UserKey == userKey)
                            .Statement;

                    var retVal = context.Query<CompositeResult<DbSecurityChallenge, DbSecurityUserChallengeAssoc>>(sqlQuery).ToArray().Select(o => new SecurityChallenge()
                    {
                        ChallengeText = o.Object1.ChallengeText,
                        Key = o.Object1.Key,
                        ObsoletionTime = o.Object2.ExpiryTime
                    }).ToList();

                    // Only the current user can fetch their own security challenge questions 
                    if (!context.Any<DbSecurityUser>(o => o.Key == userKey && principal.Identity.Name.ToLowerInvariant() == o.UserName.ToLowerInvariant())
                        || !principal.Identity.IsAuthenticated)
                    {
                        return retVal.Skip(this.m_random.Next(0, retVal.Count)).Take(1);
                    }
                    else // Only a random option can be returned  
                    {
                        return retVal;
                    }

                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Failed to fetch security challenges for user {0}: {1}", userKey, e);
                throw new Exception($"Failed to fetch security challenges for {userKey}", e);
            }
        }

        /// <summary>
        /// Remove the specified challenge for this particular key
        /// </summary>
        public void Remove(String userName, Guid challengeKey, IPrincipal principal)
        {
            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), ErrorMessages.ARGUMENT_NULL);
            }
            else if (challengeKey == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(challengeKey), ErrorMessages.ARGUMENT_OUT_OF_RANGE);
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), ErrorMessages.ARGUMENT_NULL);
            }

            if (!userName.Equals(principal.Identity.Name, StringComparison.OrdinalIgnoreCase)
                || !principal.Identity.IsAuthenticated)
            {
                this.m_policyEnforcementService.Demand(PermissionPolicyIdentifiers.AlterSecurityChallenge, principal);
            }

            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);
                    var dbUser = context.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null);
                    if (dbUser == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.USR_CHL_GEN_ERR, new { userName = userName }));
                    }
                    context.DeleteAll<DbSecurityUserChallengeAssoc>(o => o.ChallengeKey == challengeKey && o.UserKey == dbUser.Key);
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Failed to removing security challenge {0} for user {1}: {2}", challengeKey, userName, e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_CHL_GEN_ERR, new { userName = userName }), e);
            }
        }

        /// <summary>
        /// Set the specified challenge response
        /// </summary>
        public void Set(String userName, Guid challengeKey, string response, IPrincipal principal)
        {

            if (String.IsNullOrEmpty(userName))
            {
                throw new ArgumentNullException(nameof(userName), ErrorMessages.ARGUMENT_NULL);
            }
            else if (challengeKey == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(challengeKey), ErrorMessages.ARGUMENT_NULL);
            }
            else if (String.IsNullOrEmpty(response))
            {
                throw new ArgumentNullException(nameof(response), ErrorMessages.ARGUMENT_NULL);
            }
            else if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal), ErrorMessages.ARGUMENT_NULL);
            }
            else if (!userName.Equals(principal.Identity.Name, StringComparison.OrdinalIgnoreCase)
                || !principal.Identity.IsAuthenticated)
            {
                this.m_policyEnforcementService.Demand(PermissionPolicyIdentifiers.AlterSecurityChallenge, principal);
            }

            // Ensure that the user has been explicitly granted the special security policy
            try
            {
                using (var context = this.m_configuration.Provider.GetWriteConnection())
                {
                    context.Open(initializeExtensions: false);
                    var dbUser = context.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == userName.ToLowerInvariant() && o.ObsoletionTime == null);
                    if (dbUser == null)
                    {
                        throw new KeyNotFoundException(this.m_localizationService.GetString(ErrorMessageStrings.USR_CHL_GEN_ERR, new { userName = userName }));
                    }

                    var pepperResponses = this.m_configuration.GetPepperCombos(response);
                    // Existing?
                    var challengeRec = context.FirstOrDefault<DbSecurityUserChallengeAssoc>(o => o.UserKey == dbUser.Key && o.ChallengeKey == challengeKey);
                    if (challengeRec == null)
                    {
                        context.Insert(new DbSecurityUserChallengeAssoc()
                        {
                            ChallengeKey = challengeKey,
                            UserKey = dbUser.Key,
                            ChallengeResponse = this.m_passwordHashingService.ComputeHash(this.m_configuration.AddPepper(response)),
                            ExpiryTime = DateTimeOffset.Now.Add(this.m_securityConfiguration.GetSecurityPolicy<TimeSpan>(SecurityPolicyIdentification.MaxChallengeAge, new TimeSpan(3650, 0, 0, 0)))
                        });
                    }
                    else if (!this.m_securityConfiguration.GetSecurityPolicy<Boolean>(SecurityPolicyIdentification.ChallengeHistory, false) ||
                        !context.Any<DbSecurityUserChallengeAssoc>(o => o.ChallengeKey == challengeRec.ChallengeKey && o.UserKey == challengeRec.UserKey && pepperResponses.Contains(o.ChallengeResponse)))
                    {
                        challengeRec.ExpiryTime = DateTimeOffset.Now.Add(this.m_securityConfiguration.GetSecurityPolicy<TimeSpan>(SecurityPolicyIdentification.MaxChallengeAge, new TimeSpan(3650, 0, 0, 0)));
                        challengeRec.ChallengeResponse = this.m_passwordHashingService.ComputeHash(this.m_configuration.AddPepper(response));
                        context.Update(challengeRec);
                    }
                    else
                    {
                        throw new InvalidOperationException(this.m_localizationService.GetString(ErrorMessageStrings.USR_CHL_DUP_RSP));
                    }
                }
            }
            catch (Exception e)
            {
                this.m_tracer.TraceError("Failed to removing security challenge {0} for user {1}: {2}", challengeKey, userName, e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.USR_CHL_GEN_ERR, new { userName = userName }), e);
            }
        }
    }
}
