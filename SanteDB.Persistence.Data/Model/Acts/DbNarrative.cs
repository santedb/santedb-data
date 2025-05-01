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
using SanteDB.Core.Model.Constants;
using SanteDB.OrmLite.Attributes;
using System;

namespace SanteDB.Persistence.Data.Model.Acts
{
    /// <summary>
    /// Database table for narrative structure
    /// </summary>
    [Table("nar_tbl")]
    public class DbNarrative : DbActSubTable
    {

        /// <summary>
        /// Parent key
        /// </summary>
        [JoinFilter(PropertyName = nameof(DbActVersion.ClassConceptKey), Value = ActClassKeyStrings.Document)]
        [JoinFilter(PropertyName = nameof(DbActVersion.ClassConceptKey), Value = ActClassKeyStrings.DocumentSection)]
        public override Guid ParentKey
        {
            get
            {
                return base.ParentKey;
            }
            set
            {
                base.ParentKey = value;
            }
        }

        /// <summary>
        /// The version of the document narrative section
        /// </summary>
        [Column("ver")]
        public string VersionNumber { get; set; }

        /// <summary>
        /// The language code of the document narrative
        /// </summary>
        [Column("lang_cs"), NotNull]
        public String LanguageCode { get; set; }

        /// <summary>
        /// The title of the document narrative
        /// </summary>
        [Column("title"), NotNull, ApplicationEncrypt("narrative.title")]
        public String Title { get; set; }

        /// <summary>
        /// The mime type of the content
        /// </summary>
        [Column("mime")]
        public String MimeType { get; set; }

        /// <summary>
        /// The text of the narrative section
        /// </summary>
        [Column("text"), NotNull, ApplicationEncrypt("narrative.text")]
        public byte[] Text { get; set; }
    }
}
