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
using System.Linq;
using SanteDB.Core.Notifications;
using SanteDB.Core.Services;
using SanteDB.OrmLite;
using SanteDB.Persistence.Data.Model.DataType;
using SanteDB.Persistence.Data.Model.Notifications;

namespace SanteDB.Persistence.Data.Services.Persistence.Notifications
{
    /// <summary>
    /// Notification template persistence service
    /// </summary>
    public class NotificationTemplatePersistenceService : NonVersionedDataPersistenceService<NotificationTemplate, DbNotificationTemplate>
    {
        /// <summary>
        /// Creates a new notification template persistence service
        /// </summary>
        public NotificationTemplatePersistenceService(IConfigurationManager configurationManager, ILocalizationService localizationService, IAdhocCacheService adhocCacheService = null, IDataCachingService dataCachingService = null, IQueryPersistenceService queryPersistence = null) : base(configurationManager, localizationService, adhocCacheService, dataCachingService, queryPersistence)
        {
        }

        protected override NotificationTemplate DoInsertModel(DataContext context, NotificationTemplate data)
        {
            var retVal = base.DoInsertModel(context, data);

            if (data.Contents?.Any() == true)
            {
                retVal.Contents = base.UpdateModelAssociations(context, retVal, data.Contents).ToList();
            }

            if (data.Parameters?.Any() == true)
            {
                retVal.Parameters = base.UpdateModelAssociations(context, retVal, data.Parameters).ToList();
            }

            return retVal;
        }

        protected override NotificationTemplate DoConvertToInformationModel(DataContext context, DbNotificationTemplate dbModel,
            params object[] referenceObjects)
        {
            var retVal = base.DoConvertToInformationModel(context, dbModel, referenceObjects);

            retVal.Contents = retVal.Contents.GetRelatedPersistenceService().Query(context, o => o.SourceEntityKey == dbModel.Key).ToList();
            retVal.SetLoaded(o => o.Contents);

            retVal.Parameters = retVal.Parameters.GetRelatedPersistenceService().Query(context, o => o.SourceEntityKey == dbModel.Key).ToList();
            retVal.SetLoaded(o => o.Parameters);

            return retVal;
        }

        /// <summary>
        /// Perform the actual update of a model object
        /// </summary>
        protected override NotificationTemplate DoUpdateModel(DataContext context, NotificationTemplate data)
        {
            var retVal = base.DoUpdateModel(context, data);

            if (data.Contents?.Any() == true)
            {
                retVal.Contents = base.UpdateModelAssociations(context, retVal, data.Contents).ToList();
            }

            if (data.Parameters?.Any() == true)
            {
                retVal.Parameters = base.UpdateModelAssociations(context, retVal, data.Parameters).ToList();
            }

            return retVal;
        }
    }
}
