﻿using SanteDB.OrmLite.Attributes;
using SanteDB.Persistence.Data.Model.Extensibility;
using System;
using System.Collections.Generic;
using System.Text;

namespace SanteDB.Persistence.Data.Model.Sys
{
    /// <summary>
    /// Database stored data template definition
    /// </summary>
    [Table("tpl_vw_def_tbl")]
    public class DbDataTemplateDefinition : DbNonVersionedBaseData
    {
        /// <summary>
        /// Gets or sets the primary key of the object
        /// </summary>
        [Column("tpl_id"), PrimaryKey, AutoGenerated, ForeignKey(typeof(DbTemplateDefinition), nameof(DbTemplateDefinition.Key))]
        public override Guid Key { get; set; }

        /// <summary>
        /// Gets or sets the name of the template
        /// </summary>
        [Column("tpl_name"), NotNull]
        public String Name { get; set; }

        /// <summary>
        /// Gets or ses the description of the template definition
        /// </summary>
        [Column("descr")]
        public String Description { get; set; }

        /// <summary>
        /// Gets or sets the OID
        /// </summary>
        [Column("oid"), NotNull]
        public string Oid { get; set; }

        /// <summary>
        /// Gets or sets the dotted mnemonic
        /// </summary>
        [Column("mnemonic"), NotNull, Unique]
        public string Mnemonic { get; set; }

        /// <summary>
        /// Gets or sets the readonly flag
        /// </summary>
        [Column("ro"), NotNull, DefaultValue(false)]
        public bool Readonly { get; set; }

        /// <summary>
        /// Gets or sets the public 
        /// </summary>
        [Column("pub"), NotNull, DefaultValue(true)]
        public bool Public { get; set; }

        /// <summary>
        /// True if the definition is active
        /// </summary>
        [Column("ac"), NotNull, DefaultValue(true)]
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets the version of the template
        /// </summary>
        [Column("ver"), NotNull, DefaultValue(1)]
        public int Version { get; set; }

        /// <summary>
        /// Definition of the template
        /// </summary>
        [Column("def"), NotNull]
        public byte[] Definition { get; set; }

    }
}