﻿/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 *  .Net Core Plugin Manager is distributed under the GNU General Public License version 3 and  
 *  is also available under alternative licenses negotiated directly with Simon Carter.  
 *  If you obtained Service Manager under the GPL, then the GPL applies to all loadable 
 *  Service Manager modules used on your system as well. The GPL (version 3) is 
 *  available at https://opensource.org/licenses/GPL-3.0
 *
 *  This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
 *  without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 *  See the GNU General Public License for more details.
 *
 *  The Original Code was created by Simon Carter (s1cart3r@gmail.com)
 *
 *  Copyright (c) 2018 - 2019 Simon Carter.  All Rights Reserved.
 *
 *  Product:  Documentation Plugin
 *  
 *  File: IndexViewModel.cs
 *
 *  Purpose:  View model for a index or document home
 *
 *  Date        Name                Reason
 *  18/05/2019  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Collections.Generic;

using DocumentationPlugin.Classes;

using SharedPluginFeatures;

namespace DocumentationPlugin.Models
{
    public sealed class IndexViewModel : BaseModel
    {
        #region Constructors

        public IndexViewModel(in List<BreadcrumbItem> breadcrumbs, in ShoppingCartSummary cartSummary)
            : base(breadcrumbs, cartSummary)
        {
            AssemblyNames = new Dictionary<string, DocumentationModule>();
        }

        #endregion Constructors

        #region Properties

        public Dictionary<string, DocumentationModule> AssemblyNames { get; private set; }

        public string Image { get; set; }

        #endregion Properties

        #region Methods

        public string ProcessImage(in string image)
        {
            if (String.IsNullOrEmpty(image))
                return "/images/docs/module.png";

            return image;
        }

        #endregion Methods        
    }
}