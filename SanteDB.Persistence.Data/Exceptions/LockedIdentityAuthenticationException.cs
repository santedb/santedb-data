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
 * Date: 2023-5-19
 */
using System;

namespace SanteDB.Persistence.Data.Exceptions
{
    /// <summary>
    /// Indicates an identity is locked
    /// </summary>
    internal sealed class LockedIdentityAuthenticationException : System.Security.Authentication.AuthenticationException
    {

        /// <summary>
        /// Gets the time until lockout is cleared
        /// </summary>
        public DateTimeOffset TimeLockExpires { get; }

        /// <summary>
        /// Create with time until lockout
        /// </summary>
        public LockedIdentityAuthenticationException(DateTimeOffset timeToUnlock) : base($"Account is locked for {timeToUnlock}")
        {
            this.TimeLockExpires = timeToUnlock;
        }

        /// <summary>
        /// Create with absolute lockout time
        /// </summary>
        public LockedIdentityAuthenticationException(TimeSpan lockoutTime) : this(DateTimeOffset.Now.Add(lockoutTime))
        { }

    }
}