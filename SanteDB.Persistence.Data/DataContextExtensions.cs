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
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Newtonsoft.Json.Bson;
using SanteDB.Core;
using SanteDB.Core.BusinessRules;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model;
using SanteDB.Core.Model.Security;
using SanteDB.Core.Security;
using SanteDB.Core.Security.Claims;
using SanteDB.Core.Security.Principal;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.MappedResultSets;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Model;
using SanteDB.Persistence.Data.Model.Security;
using SanteDB.Persistence.Data.Security;
using SanteDB.Persistence.Data.Services.Persistence;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Principal;

namespace SanteDB.Persistence.Data
{
   
    /// <summary>
    /// Data context extensions
    /// </summary>
    public static class DataContextExtensions
    {
        private const int MAX_STACK_DEPTH = 30;

        /// <summary>
        /// Data context information model entry allows for a using
        /// </summary>
        public class DataContextInformationModelEntry : IDisposable
        {
            private readonly DataContext m_dataContext;
            private readonly Guid m_entryId;

            /// <summary>
            /// Create new data context
            /// </summary>
            public DataContextInformationModelEntry(DataContext dataContext, Guid entryId)
            {
                this.m_dataContext = dataContext;
                this.m_entryId = entryId;
                dataContext.PushData(DataConstants.DoConvertToInformationModelStack, entryId);
            }

           
            /// <summary>
            /// Dispose the context
            /// </summary>
            public void Dispose()
            {
                if(!this.m_dataContext.PopData(DataConstants.DoConvertToInformationModelStack, out var entryId) || 
                    !entryId.Equals(this.m_entryId))
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.WOULD_RESULT_INVALID_STATE, nameof(DataContextInformationModelEntry)));
                }
            }

        }

        /// <summary>
        /// Persistence validation flags
        /// </summary>
        [Flags]
        public enum DisablePersistenceValidationFlags : byte
        {
            None = 0x0,
            Exists = 0x1,
            BusinessContstraints = 0x2,
            Relationships = 0x4,
            All = Exists | BusinessContstraints | Relationships
        }


        /// <summary>
        /// Defer constraints annotation
        /// </summary>
        private struct DeferConstraints
        {
            public DeferConstraints(DisablePersistenceValidationFlags settings)
            {
                this.Settings = settings;
            }

            /// <summary>
            /// Disable validation settings
            /// </summary>
            public DisablePersistenceValidationFlags Settings { get; }
        }

        // Localization service
        private static readonly ILocalizationService s_localizationService = ApplicationServiceContext.Current.GetService<ILocalizationService>();

        // Tracer
        private static readonly Tracer s_tracer = Tracer.GetTracer(typeof(DataContextExtensions));

        // Adhoc cache
        private static readonly IAdhocCacheService s_adhocCache = ApplicationServiceContext.Current.GetService<IAdhocCacheService>();

        /// <summary>
        /// Providers
        /// </summary>
        private readonly static ConcurrentDictionary<Type, IAdoPersistenceProvider> s_providers = new ConcurrentDictionary<Type, IAdoPersistenceProvider>();

        // Configuration
        private readonly static AdoPersistenceConfigurationSection s_configuration = ApplicationServiceContext.Current.GetService<IConfigurationManager>().GetSection<AdoPersistenceConfigurationSection>();

        /// <summary>
        /// Providers for mapping
        /// </summary>
        private readonly static ConcurrentDictionary<Type, object> s_mapProviders = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// Translates a DB exception to an appropriate SanteDB exception
        /// </summary>
        public static Exception TranslateDbException(this DbException e)
        {
            s_tracer.TraceError("Will Translate DBException: {0} - {1}", e.Data["SqlState"] ?? e.ErrorCode, e.Message);
            if (e.Data["SqlState"] != null)
            {
                switch (e.Data["SqlState"].ToString())
                {
                    case "O9001": // SanteDB => Data Validation Error
                        return new DetectedIssueException(
                            new DetectedIssue(DetectedIssuePriorityType.Error, e.Data["SqlState"].ToString(), e.Message, DetectedIssueKeys.InvalidDataIssue), e);

                    case "O9002": // SanteDB => Codification error
                        return new DetectedIssueException(new List<DetectedIssue>() {
                                        new DetectedIssue(DetectedIssuePriorityType.Error, e.Data["SqlState"].ToString(),  e.Message, DetectedIssueKeys.CodificationIssue),
                                        new DetectedIssue(DetectedIssuePriorityType.Information, e.Data["SqlState"].ToString(), "HINT: Select a code that is from the correct concept set or add the selected code to the concept set", DetectedIssueKeys.CodificationIssue)
                                    }, e);

                    case "23502": // PGSQL - NOT NULL
                        return new DetectedIssueException(
                                        new DetectedIssue(DetectedIssuePriorityType.Error, e.Data["SqlState"].ToString(), e.Message, DetectedIssueKeys.InvalidDataIssue)
                                    );

                    case "23503": // PGSQL - FK VIOLATION
                        return new DetectedIssueException(
                                        new DetectedIssue(DetectedIssuePriorityType.Error, e.Data["SqlState"].ToString(), e.Message, DetectedIssueKeys.FormalConstraintIssue), e
                                    );

                    case "23505": // PGSQL - UQ VIOLATION
                        return new DetectedIssueException(
                                        new DetectedIssue(DetectedIssuePriorityType.Error, e.Data["SqlState"].ToString(), e.Message, DetectedIssueKeys.AlreadyDoneIssue), e
                                    );

                    case "23514": // PGSQL - CK VIOLATION
                        return new DetectedIssueException(new List<DetectedIssue>()
                        {
                            new DetectedIssue(DetectedIssuePriorityType.Error, e.Data["SqlState"].ToString(), e.Message, DetectedIssueKeys.FormalConstraintIssue),
                            new DetectedIssue(DetectedIssuePriorityType.Information, e.Data["SqlState"].ToString(), "HINT: The code you're using may be incorrect for the given context", DetectedIssueKeys.CodificationIssue)
                        }, e);

                    default:
                        return new DataPersistenceException(e.Message, e);
                }
            }
            else
            {
                return new DetectedIssueException(new DetectedIssue(DetectedIssuePriorityType.Error, "dbexception", e.Message, DetectedIssueKeys.OtherIssue), e);
            }
        }

        /// <summary>
        /// Get related mapping provider
        /// </summary>
        /// <typeparam name="TRelated">The model type for which the provider should be returned</typeparam>
        public static IMappedQueryProvider<TRelated> GetRelatedMappingProvider<TRelated>(this TRelated me)
        {
            if (!s_mapProviders.TryGetValue(typeof(TRelated), out object provider))
            {
                provider = ApplicationServiceContext.Current.GetService<IMappedQueryProvider<TRelated>>();
                if (provider != null)
                {
                    s_mapProviders.TryAdd(typeof(TRelated), provider);
                }
                else
                {
                    throw new InvalidOperationException(s_localizationService.GetString(ErrorMessageStrings.MISSING_SERVICE, new { service = typeof(IMappedQueryProvider<TRelated>) }));
                }
            }
            return provider as IMappedQueryProvider<TRelated>;
        }

        /// <summary>
        /// Get related persistence service from an enumerable
        /// </summary>
        public static IAdoPersistenceProvider<TRelated> GetRelatedPersistenceService<TRelated>(this IEnumerable<TRelated> me) where TRelated : IdentifiedData
            => GetRelatedPersistenceService(typeof(TRelated)) as IAdoPersistenceProvider<TRelated>;

        /// <summary>
        /// Get related persistence service
        /// </summary>
        public static IAdoPersistenceProvider<TRelated> GetRelatedPersistenceService<TRelated>(this TRelated me) where TRelated : IdentifiedData
            => GetRelatedPersistenceService(typeof(TRelated)) as IAdoPersistenceProvider<TRelated>;

        /// <summary>
        /// Get related persistence service
        /// </summary>
        public static IAdoPersistenceProvider<TRelated> GetRelatedPersistenceService<TRelated>(this Type me) where TRelated : IdentifiedData
            => GetRelatedPersistenceService(me) as IAdoPersistenceProvider<TRelated>;


        /// <summary>
        /// Get related persistence service that can store model objects of <paramref name="trelated"/>
        /// </summary>
        /// <param name="trelated">The related type of object</param>
        /// <returns>The persistence provider</returns>
        public static IAdoPersistenceProvider GetRelatedPersistenceService(this Type trelated)
        {
            if (!s_providers.TryGetValue(trelated, out IAdoPersistenceProvider provider))
            {
                var relType = typeof(IAdoPersistenceProvider<>).MakeGenericType(trelated);
                provider = ApplicationServiceContext.Current.GetService(relType) as IAdoPersistenceProvider;
                if (provider != null)
                {
                    s_providers.TryAdd(trelated, provider);
                }
                else
                {
                    // Try TRelated's parent
                    return trelated.BaseType.GetRelatedPersistenceService();
                }
            }
            return provider;
        }

        /// <summary>
        /// Convert to security policy instance
        /// </summary>
        internal static SecurityPolicyInstance ToSecurityPolicyInstance(this DbSecurityPolicyInstance me, DataContext context)
        {
            var policy = s_adhocCache?.Get<DbSecurityPolicy>($"pol.{me.PolicyKey}");
            if (policy == null)
            {
                policy = context.FirstOrDefault<DbSecurityPolicy>(o => o.Key == me.PolicyKey);
                s_adhocCache?.Add($"pol.{me.PolicyKey}", policy);
            }

            if (policy == null)
            {
                throw new InvalidOperationException(s_localizationService.GetString(ErrorMessageStrings.RELATED_OBJECT_NOT_FOUND));
            }

            return new SecurityPolicyInstance(new SecurityPolicy(policy.Name, policy.Oid, policy.IsPublic, policy.CanOverride) { Key = me.PolicyKey }, PolicyGrantType.Grant);
        }

        /// <summary>
        /// Convert validation enforcement to priority
        /// </summary>
        internal static DetectedIssuePriorityType ToPriority(this AdoValidationEnforcement me)
        {
            switch (me)
            {
                case AdoValidationEnforcement.Off:
                    return DetectedIssuePriorityType.Information;

                case AdoValidationEnforcement.Loose:
                    return DetectedIssuePriorityType.Warning;

                case AdoValidationEnforcement.Strict:
                    return DetectedIssuePriorityType.Error;

                default:
                    return DetectedIssuePriorityType.Error;
            }
        }


        /// <summary>
        /// Set data on the context in a safe manner
        /// </summary>
        public static void PushData(this DataContext me, String dataKey, Object data)
        {
            if(me.Data.TryGetValue(dataKey, out var existing) && existing is Stack<Object> stk)
            {
                stk.Push(data);
            }
            else
            {
                stk = new Stack<object>();
                stk.Push(data);
                me.Data.Add(dataKey, stk);
            }
        }

        /// <summary>
        /// Set data on the context in a safe manner
        /// </summary>
        public static bool PopData(this DataContext me, String dataKey, out Object data)
        {
            if (me.Data.TryGetValue(dataKey, out var existing) && existing is Stack<Object> stk)
            {
                if (stk.Any())
                {
                    data = stk.Pop();
                    return true;
                }
                else
                {
                    data = null;
                    return false;
                }
            }
            else
            {
                stk = new Stack<object>();
                me.Data.Add(dataKey, stk);
                data = null;
                return false;
            }
        }

        /// <summary>
        /// Set data on the context in a safe manner
        /// </summary>
        public static bool PeekData(this DataContext me, String dataKey, out Object data)
        {
            if (me.Data.TryGetValue(dataKey, out var existing) && existing is Stack<Object> stk && stk.Any())
            {
                data = stk.Peek();
                return true;
            }
            else
            {
                data = null;
                return false;
            }
        }

        /// <summary>
        /// Returns true if the data stack contains an entry
        /// </summary>
        public static bool ContainsStackEntry(this DataContext me, String dataKey, Object checkData)
        {
            if (me.Data.TryGetValue(dataKey, out var existing) && existing is Stack<Object> stk)
            {
                return stk.Contains(checkData);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Should disable object validation
        /// </summary>
        public static DisablePersistenceValidationFlags ShouldDisableObjectValidation(this DataContext me) => me.Data.TryGetValue(DataConstants.DisableObjectValidation, out var vRaw) ? (DisablePersistenceValidationFlags)vRaw : DisablePersistenceValidationFlags.None;


        /// <summary>
        /// Get provenance from the context
        /// </summary>
        public static DbSecurityProvenance GetProvenance(this DataContext me)
        {
            if (me.Data.TryGetValue("provenance", out object provenance))
            {
                return provenance as DbSecurityProvenance;
            }
            else
            {
                var retVal = me.FirstOrDefault<DbSecurityProvenance>(o => o.Key == me.ContextId);
                me.Data.Add("provenance", retVal);
                return retVal;
            }
        }

        /// <summary>
        /// Establish a provenance entry for the specified connection
        /// </summary>
        public static Guid EstablishProvenance(this DataContext me, IPrincipal principal, Guid? externalRef = null)
        {
            // First, we want to get the identities
            DbSecurityProvenance retVal = new DbSecurityProvenance()
            {
                Key = me.ContextId,
                ExternalSecurityObjectRefKey = externalRef,
                ExternalSecurityObjectRefType = externalRef != null ?
                    (me.Count<DbSecurityUser>(o => o.Key == externalRef) > 0 ? "U" : "P") : null
            };

            // Establish identities
            if (principal is IClaimsPrincipal cprincipal) // claims principal?
            {
                foreach (var ident in cprincipal.Identities)
                {
                    Guid sid = Guid.Empty;
                    switch (ident)
                    {
                        case AdoIdentity adoIdentity:
                            sid = adoIdentity.Sid;
                            break;
                        case IClaimsIdentity cIdentity:
                            sid = Guid.Parse(cIdentity.FindFirst(SanteDBClaimTypes.SecurityId)?.Value);
                            break;
                        default:
                            throw new SecurityException(s_localizationService.GetString(ErrorMessageStrings.SEC_PROVENANCE_UNK_ID));
                    }

                    // Set apporopriate property
                    if (ident is IDeviceIdentity)
                    {
                        retVal.DeviceKey = sid;
                    }
                    else if (ident is IApplicationIdentity)
                    {
                        retVal.ApplicationKey = sid;
                    }
                    else
                    {
                        retVal.UserKey = sid;
                    }
                }

                // Session identifier
                var sidClaim = cprincipal?.FindFirst(SanteDBClaimTypes.SanteDBSessionIdClaim)?.Value;
                if (!String.IsNullOrEmpty(sidClaim) && Guid.TryParse(sidClaim, out Guid sessionId))
                {
                    retVal.SessionKey = sessionId;
                }

                // Pure application credential
                if (!retVal.UserKey.HasValue && !retVal.DeviceKey.HasValue)
                {
                    retVal.UserKey = Guid.Parse(AuthenticationContext.SystemUserSid);
                }
                if (retVal.ApplicationKey == Guid.Empty)
                {
                    retVal.ApplicationKey = Guid.Parse(AuthenticationContext.SystemApplicationSid); // System application SID fallback
                }
            }
            else // Establish the slow way - using identity name
            {
                switch (principal.Identity)
                {
                    case IDeviceIdentity di:
                        retVal.DeviceKey = me.Query<DbSecurityDevice>(o => o.PublicId.ToLowerInvariant() == di.Name.ToLowerInvariant()).Select(o => o.Key).First();
                        retVal.ApplicationKey = Guid.Parse(AuthenticationContext.SystemApplicationSid);
                        break;
                    case IApplicationIdentity ai:
                        retVal.ApplicationKey = me.Query<DbSecurityApplication>(o => o.PublicId.ToLowerInvariant() == ai.Name.ToLowerInvariant()).Select(o => o.Key).First();
                        retVal.UserKey = Guid.Parse(AuthenticationContext.SystemUserSid);
                        break;
                    default:

                        if (principal.Identity.Name == AuthenticationContext.SystemPrincipal.Identity.Name)
                        {
                            retVal.UserKey = Guid.Parse(AuthenticationContext.SystemUserSid);

                        }
                        else if (principal.Identity.Name == AuthenticationContext.AnonymousPrincipal.Identity.Name)
                        {
                            retVal.UserKey = Guid.Parse(AuthenticationContext.AnonymousUserSid);
                        }
                        else
                        {
                            retVal.UserKey = me.FirstOrDefault<DbSecurityUser>(o => o.UserName.ToLowerInvariant() == principal.Identity.Name.ToLowerInvariant())?.Key;
                        }
                        retVal.ApplicationKey = Guid.Parse(AuthenticationContext.SystemApplicationSid);

                        if (!retVal.UserKey.HasValue)
                        {
                            throw new SecurityException(s_localizationService.GetString(ErrorMessageStrings.SEC_PROVENANCE_UNK_ID));
                        }
                        break;
                }

            }

            // insert the provenance object
            try
            {
                if ((retVal.UserKey.ToString() == AuthenticationContext.SystemUserSid ||
                    retVal.UserKey.ToString() == AuthenticationContext.AnonymousUserSid) &&
                    retVal.ApplicationKey.ToString() == AuthenticationContext.SystemApplicationSid)
                {
                    retVal.Key = me.ContextId = retVal.UserKey.Value;
                }
                else
                {
                    if (!me.IsReadonly)
                    {
                        retVal = me.Insert(retVal);
                        me.ContextId = retVal.Key;
                    }
                }

                me.Data.Add("provenance", retVal);
                return retVal.Key;
            }
            catch (Exception e)
            {
                throw new SecurityException(s_localizationService.GetString(ErrorMessageStrings.SEC_PROVENANCE_GEN_ERR), e);
            }
        }



        /// <summary>
        /// Defer check constraints on the object in the persistence layer
        /// </summary>
        public static void DisablePersistenceValidation(this IdentifiedData me, DisablePersistenceValidationFlags settings)
        {
            me.AddAnnotation(new DeferConstraints(settings));
        }

        /// <summary>
        /// Determine if the object has been flagged for constraint deferral
        /// </summary>
        public static DisablePersistenceValidationFlags ShouldDisablePersistenceValidation(this IdentifiedData me) => me.GetAnnotations<DeferConstraints>().FirstOrDefault().Settings;


        /// <summary>
        /// Adds or updates data on the context
        /// </summary>
        public static void AddOrUpdateData(this DataContext dataContext, String key, object value)
        {
            if(dataContext.Data.ContainsKey(key)) {
                dataContext.Data[key] = value;
            }
            else
            {
                dataContext.Data.Add(key, value);
            }
        }

        /// <summary>
        /// Should be called when converting to information model to prevent stack overflows
        /// </summary>
        public static DataContextInformationModelEntry CreateInformationModelGuard(this DataContext dataContext, Guid entryId) => new DataContextInformationModelEntry(dataContext, entryId);

        /// <summary>
        /// Determine if the information model is loading
        /// </summary>
        public static bool IsLoadingInformationModel(this DataContext dataContext, Guid entryId) => dataContext.ContainsStackEntry(DataConstants.DoConvertToInformationModelStack, entryId);

        /// <summary>
        /// Validate that the current context is under the maximum stack depth
        /// </summary>
        public static bool ValidateMaximumStackDepth(this DataContext dataContext) => (dataContext.Data[DataConstants.DoConvertToInformationModelStack] as IEnumerable<Object>).Distinct().Count() < MAX_STACK_DEPTH;

    }
}