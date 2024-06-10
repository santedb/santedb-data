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
 * User: fyfej
 * Date: 2023-6-21
 */
using SanteDB.Core;
using SanteDB.Core.Diagnostics;
using SanteDB.Core.Exceptions;
using SanteDB.Core.i18n;
using SanteDB.Core.Model.Acts;
using SanteDB.Core.Model.DataTypes;
using SanteDB.Core.Model.Entities;
using SanteDB.Core.Model.Interfaces;
using SanteDB.Core.Model.Map;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Model.Subscription;
using SanteDB.Core.Security;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.OrmLite.Configuration;
using SanteDB.OrmLite.Providers;
using SanteDB.Persistence.Data.Configuration;
using SanteDB.Persistence.Data.Services.Persistence;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;

namespace SanteDB.Persistence.Data.Services
{
    /// <summary>
    /// An implementation of the <see cref="ISubscriptionExecutor"/> which uses an ADO persistence layer
    /// </summary>
    [ServiceProvider("ADO.NET Subscription Executor", Configuration = typeof(AdoPersistenceConfigurationSection))]
    public class AdoSubscriptionExecutor : ISubscriptionExecutor
    {
        // Parameter regex
        private static readonly Regex m_parmRegex = new Regex(@"\$\{([\w_][\-\d\w\._]*?)(?:\#([\w_\.]+?))?\}", RegexOptions.Multiline | RegexOptions.Compiled);

        // Allowed target types
        private readonly Type[] m_allowedTypes = new Type[]
        {
            typeof(Entity),
            typeof(Act),
            typeof(Concept)
        };

        /// <summary>
        /// Gets the service name
        /// </summary>
        public string ServiceName => "ADO.NET Subscription Executor";

        // Ref to mapper
        private readonly ModelMapper m_modelMapper;

        // Gets the configuration for this object
        private readonly AdoPersistenceConfigurationSection m_configuration;

        // Subscription definition
        private readonly ISubscriptionRepository m_subscriptionRepository;
        private readonly ILocalizationService m_localizationService;
        private readonly IServiceManager m_serviceManager;

        // Tracer
        private Tracer m_tracer = Tracer.GetTracer(typeof(AdoSubscriptionExecutor));

        /// <summary>
        /// Create the default subscription executor
        /// </summary>
        public AdoSubscriptionExecutor(IConfigurationManager configurationManager, ILocalizationService localizationService, ISubscriptionRepository subscriptionDefinition, IServiceManager serviceManager)
        {
            this.m_configuration = configurationManager.GetSection<AdoPersistenceConfigurationSection>();
            this.m_subscriptionRepository = subscriptionDefinition;
            this.m_localizationService = localizationService;
            this.m_serviceManager = serviceManager;
            this.m_modelMapper = new ModelMapper(typeof(AdoPersistenceService).Assembly.GetManifestResourceStream(DataConstants.MapResourceName), "AdoModelMap");
        }

        /// <summary>
        /// Fired when the query is executed
        /// </summary>
        public event EventHandler<SubscriptionExecutedEventArgs> Executed;

        /// <summary>
        /// Fired when the query is about to execute
        /// </summary>
        public event EventHandler<SubscriptionExecutingEventArgs> Executing;

        /// <summary>
        /// Exectue the specified subscription
        /// </summary>
        public IQueryResultSet Execute(Guid subscriptionKey, NameValueCollection parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters), ErrorMessages.ARGUMENT_NULL);
            }
            else if (subscriptionKey == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(subscriptionKey));
            }

            var subscription = ApplicationServiceContext.Current.GetService<IRepositoryService<SubscriptionDefinition>>()?.Get(subscriptionKey);
            if (subscription == null)
            {
                throw new KeyNotFoundException(subscriptionKey.ToString());
            }
            else
            {
                return this.Execute(subscription, parameters);
            }
        }

        /// <summary>
        /// Execute the current operation
        /// </summary>
        public IQueryResultSet Execute(SubscriptionDefinition subscription, NameValueCollection parameters)
        {
            if (subscription == null || subscription.LoadProperty(o => o.ServerDefinitions).Count == 0)
            {
                throw new InvalidOperationException(ErrorMessages.SUBSCRIPTION_MISSING_DEFINITION);
            }
            else if (!this.m_allowedTypes.Contains(subscription.ResourceType))
            {
                throw new InvalidOperationException(String.Format(ErrorMessages.SUBSCRIPTION_NOT_SUPPORTED_RESOURCE, String.Join(" or ", this.m_allowedTypes.Select(o => o.Name))));
            }
            try
            {

                var preArgs = new SubscriptionExecutingEventArgs(subscription, parameters, AuthenticationContext.Current.Principal);
                this.Executing?.Invoke(this, preArgs);
                if (preArgs.Cancel)
                {
                    this.m_tracer.TraceWarning("Pre-Event for executor indicates cancel");
                    return preArgs.Results;
                }

                // Subscriptions can execute against any type of data in SanteDB - so we want to get the appropriate persistence service
                var persistenceType = typeof(IDataPersistenceService<>).MakeGenericType(subscription.ResourceType);
                var persistenceInstance = ApplicationServiceContext.Current.GetService(persistenceType) as IQuerySetProvider;
                if (persistenceInstance == null)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.SUBSCRIPTION_RESOURCE_NOSTORE, subscription.Resource));
                }
                var encryptionProvider = (persistenceInstance.Provider as IEncryptedDbProvider)?.GetEncryptionProvider();

                // Get the definition
                var definition = subscription.ServerDefinitions.FirstOrDefault(o => o.InvariantName == m_configuration.Provider.Invariant);
                if (definition == null)
                {
                    throw new InvalidOperationException(String.Format(ErrorMessages.SUBSCRIPTION_NO_DEFINITION_FOR_PROVIDER, this.m_configuration.Provider.Invariant));
                }

                // No obsoletion time?
                if (typeof(IBaseData).IsAssignableFrom(subscription.ResourceType) && !parameters.TryGetValue("obsoletionTime", out _))
                {
                    parameters.Add("obsoletionTime", "null");
                }

                // Build the filter expression which is placed on the result set
                var queryExpression = QueryExpressionParser.BuildLinqExpression(subscription.ResourceType, parameters);

                var tableMapping = TableMapping.Get(this.m_modelMapper.MapModelType(subscription.ResourceType));

                // We want to build a query that is appropriate for the resource type so the definition will become
                // SELECT [columns for type] FROM (definition from subscription logic here) AS [tablename] WHERE [filter provided by caller];
                var query = new QueryBuilder(this.m_modelMapper, persistenceInstance.Provider.StatementFactory).CreateQuery(subscription.ResourceType, queryExpression).Statement;

                // Now we want to remove the portions of the built query statement after FROM and before WHERE as the definition in the subscription will be the source of our selection
                SqlStatementBuilder domainQuery = new SqlStatementBuilder(m_configuration.Provider.StatementFactory, query.ToString().Substring(0, query.ToString().IndexOf(" FROM ")));

                // Append our query
                var definitionQuery = definition.Definition;
                var arguments = new List<Object>();
                definitionQuery = m_parmRegex.Replace(definitionQuery, (o) =>
                {
                    if (parameters.TryGetValue($"_{o.Groups[1].Value}", out var qValue))
                    {
                        if (Guid.TryParse(qValue.First(), out var uuid))
                        {
                            arguments.AddRange(qValue.Select(v => Guid.Parse(v)).OfType<Object>());
                        }
                        else if (DateTime.TryParse(qValue.First(), out var dt))
                        {
                            arguments.AddRange(qValue.Select(v => DateTime.Parse(v)).OfType<Object>());
                        }
                        else
                        {
                            OrmAleMode ormMode = OrmAleMode.Off;
                            if (!String.IsNullOrEmpty(o.Groups[2].Value) && encryptionProvider?.TryGetEncryptionMode(o.Groups[2].Value, out ormMode) == true) // Encrypted field 
                            {
                                arguments.AddRange(qValue.Select(q => encryptionProvider.CreateQueryValue(ormMode, q)));
                            }
                            else
                            {
                                arguments.AddRange(qValue);
                            }
                        }
                        return String.Join(",", qValue.Select(v => "?"));
                    }
                    return "NULL";
                });

                // Now we want to append the new definitional query (with parameter substitutions) to our main select statement
                query = query.Prepare();
                domainQuery.Append(" FROM (").Append(definitionQuery, arguments.ToArray()).Append($") AS {tableMapping.TableName} ");
                domainQuery.Append(query.ToString().Substring(query.ToString().IndexOf("WHERE ")), query.Arguments.ToArray()); // Then we add the filters supplied by the caller 

                var retVal = persistenceInstance.Query(domainQuery.Statement);
                var postEvt = new SubscriptionExecutedEventArgs(subscription, parameters, retVal, AuthenticationContext.Current.Principal);
                this.Executed?.Invoke(this, postEvt);

                return postEvt.Results;

            }
            catch (DbException e)
            {
                this.m_tracer.TraceData(System.Diagnostics.Tracing.EventLevel.Error, "Data error executing subscription execution operation", subscription, e);
                throw e.TranslateDbException();
            }
            catch (Exception e)
            {
                this.m_tracer.TraceData(System.Diagnostics.Tracing.EventLevel.Error, "General error executing subscription execution operation", subscription, e);
                throw new DataPersistenceException(this.m_localizationService.GetString(ErrorMessageStrings.DATA_GENERAL), e);
            }
        }
    }
}
