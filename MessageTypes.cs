#region Licence
/*
    Babbacombe SockLib
    https://github.com/trevorprinn/SockLib
    Copyright © 2015 Babbacombe Computers Ltd.

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301
    USA
 */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Babbacombe.SockLib {

    /// <summary>
    /// The types of messages that can be sent and received.
    /// </summary>
    public enum MessageTypes {
        /// <summary>
        /// A message containing UTF-8 text
        /// </summary>
        Text, 
        /// <summary>
        /// A message containing a status
        /// </summary>
        Status, 
        /// <summary>
        /// A text message containing Unicode text
        /// </summary>
        Unicode, 
        /// <summary>
        /// A message containing an XML document
        /// </summary>
        Xml, 
        /// <summary>
        /// A message containing binary data
        /// </summary>
        Binary, 
        /// <summary>
        /// A message containing a list of Filenames
        /// </summary>
        Filenames, 
        /// <summary>
        /// A message containing multiple files and data items
        /// </summary>
        Multipart
    }

}
