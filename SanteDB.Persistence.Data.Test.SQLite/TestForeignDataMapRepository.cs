/*
 * Copyright (C) 2021 - 2023, SanteSuite Inc. and the SanteSuite Contributors (See NOTICE.md for full copyright notices)
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
 * Date: 2023-3-10
 */
using SanteDB.Core.Data.Import.Definition;
using SanteDB.Core.Model.Query;
using SanteDB.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SanteDB.Persistence.Data.Test.SQLite
{
    public class TestForeignDataMapRepository : IRepositoryService<ForeignDataMap>
    {

        public string ServiceName => "Test Foreign Data Map Repo";

        public ForeignDataMap Delete(Guid key)
        {
            throw new NotSupportedException();
        }

        public IQueryResultSet<ForeignDataMap> Find(Expression<Func<ForeignDataMap, bool>> query)
        {
            return
                typeof(TestForeignDataMapRepository).Assembly.GetManifestResourceNames()
                .Where(t => t.EndsWith("Map.xml"))
                .Select(o =>
                {
                    using (var ms = typeof(TestForeignDataMapRepository).Assembly.GetManifestResourceStream(o))
                    {
                        return ForeignDataMap.Load(ms);
                    }
                })
                .Where(query.Compile())
                .AsResultSet();
        }

        public ForeignDataMap Get(Guid key) => this.Get(key, Guid.Empty);

        public ForeignDataMap Get(Guid key, Guid versionKey)
        {
            return this.Find(o => o.Key == key).FirstOrDefault();
        }

        public ForeignDataMap Insert(ForeignDataMap data)
        {
            throw new NotSupportedException();
        }

        public ForeignDataMap Save(ForeignDataMap data)
        {
            throw new NotSupportedException();
        }
    }
}
