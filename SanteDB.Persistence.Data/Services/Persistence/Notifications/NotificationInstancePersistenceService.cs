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
using SanteDB.Core.Notifications;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.Notifications;
using System.Linq;

namespace SanteDB.Persistence.Data.Services.Persistence.Notifications
{
    /// <summary>
    /// Persistence service that works with NotificationInstance objects
    /// </summary>
    public class NotificationInstancePersistenceService : NonVersionedDataPersistenceService<NotificationInstance, DbNotificationInstance>
    {
        /// <summary>
        /// Dependency injected constructor
        /// </summary>
        public NotificationInstancePersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        protected override NotificationInstance DoInsertModel(DataContext context, NotificationInstance data)
        {
            var retVal = base.DoInsertModel(context, data);
            
            if (data.InstanceParameters?.Any() == true)
            {
                retVal.InstanceParameters = base.UpdateModelAssociations(context, retVal, data.InstanceParameters).ToList();
            }

            return retVal;
        }

        protected override NotificationInstance DoConvertToInformationModel(DataContext context, DbNotificationInstance dbModel, params object[] referenceObjects)
        {
            using (context.CreateInformationModelGuard(dbModel.Key))
            {
                var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);

                retVal.InstanceParameters = retVal.InstanceParameters.GetRelatedPersistenceService().Query(context, o => o.SourceEntityKey == dbModel.Key).ToList();
                retVal.SetLoaded(o => o.InstanceParameters);

                return retVal;
            }
        }

        protected override NotificationInstance DoUpdateModel(DataContext context, NotificationInstance data)
        {
            var retVal = base.DoUpdateModel(context, data);

            if (data.InstanceParameters?.Any() == true)
            {
                retVal.InstanceParameters = base.UpdateModelAssociations(context, retVal, data.InstanceParameters).ToList();
            }

            return retVal;
        }
    }
}
