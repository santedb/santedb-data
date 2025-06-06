﻿/*
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
using SanteDB.OrmLite.Attributes;
using System;

namespace SanteDB.Persistence.PubSub.ADO.Data.Model
{
    /// <summary>
    /// Represents a database representation of a channel
    /// </summary>
    [Table("sub_chnl_tbl")]
    public class DbChannel : DbBaseObject
    {
        /// <summary>
        /// Gets or sets the key
        /// </summary>
        [Column("chnl_id"), NotNull, AutoGenerated, PrimaryKey]
        public override Guid? Key { get; set; }

        /// <summary>
        /// Gets or sets the name
        /// </summary>
        [Column("name"), NotNull]
        public String Name { get; set; }

        /// <summary>
        /// Gets or sets the endpoint
        /// </summary>
        [Column("uri"), NotNull]
        public String Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the dispatcher
        /// </summary>
        [Column("dsptchr_cls"), NotNull]
        public String DispatchFactoryType { get; set; }
    }
}
