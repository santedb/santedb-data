using SanteDB.Core.Configuration;
using SanteDB.OrmLite.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace SanteDB.Persistence.Synchronization.ADO.Configuration
{
    /// <summary>
    /// Configuration section handler
    /// </summary>
    [XmlType(nameof(AdoSynchronizationConfigurationSection), Namespace = "http://santedb.org/configuration")]
    public class AdoSynchronizationConfigurationSection : OrmConfigurationBase, IConfigurationSection
    {

    }
}
