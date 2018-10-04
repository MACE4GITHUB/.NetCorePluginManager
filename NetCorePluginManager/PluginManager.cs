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
 *  Copyright (c) 2018 Simon Carter.  All Rights Reserved.
 *
 *  Product:  AspNetCore.PluginManager
 *  
 *  File: PluginManager.cs
 *
 *  Purpose:  
 *
 *  Date        Name                Reason
 *  22/09/2018  Simon Carter        Initially Created
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0034

namespace AspNetCore.PluginManager
{
    public sealed class PluginManager : IDisposable
    {
        #region Private Members

        private const ushort MaxPluginVersion = 1;

        private readonly ILogger _logger;
        private readonly Dictionary<string, IPluginModule> _plugins;
        private readonly PluginSettings _pluginSettings;

        #endregion Private Members

        #region Constructors

        private PluginManager ()
        {
            _plugins = new Dictionary<string, IPluginModule>();
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainAssemblyResolve;
        }

        internal PluginManager(in ILogger logger, in PluginSettings pluginSettings)
            : this()
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pluginSettings = pluginSettings ?? throw new ArgumentNullException(nameof(pluginSettings));
        }

        #endregion Constructors

        #region Internal Methods

        /// <summary>
        /// Loads and configures an individual plugin
        /// </summary>
        /// <param name="pluginName"></param>
        internal void LoadPlugin(in string pluginName)
        {
            try
            {
                PluginSetting pluginSetting = GetPluginSetting(pluginName);

                if (pluginSetting.Disabled)
                    return;

                Assembly pluginAssembly = LoadAssembly(pluginName);

                foreach (Type type in pluginAssembly.GetTypes())
                {
                    try
                    {
                        if (type.GetInterface("IPlugin") != null)
                        {
                            IPlugin pluginService = (IPlugin)Activator.CreateInstance(type);

                            if (!pluginSetting.PreventExtractResources)
                            {
                                ExtractResources(pluginAssembly, pluginSetting);
                            }

                            IPluginModule pluginModule = new IPluginModule()
                            {
                                Assembly = pluginAssembly,
                                Module = pluginName,
                                Plugin = pluginService
                            };

                            IPluginVersion version = GetPluginClass<IPluginVersion>(pluginModule);

                            pluginModule.Version = version == null ? (ushort)1 : 
                                GetMinMaxValue(version.GetVersion(), 1, MaxPluginVersion);

                            pluginModule.Plugin.Initialise(_logger);

                            _plugins.Add(pluginName, pluginModule);
                        }
                    }
                    catch (Exception typeLoader)
                    {
                        _logger.AddToLog(typeLoader, $"{pluginName}{MethodBase.GetCurrentMethod().Name}");
                    }
                }
            }
            catch (Exception error)
            {
                _logger.AddToLog(error, $"{pluginName}{MethodBase.GetCurrentMethod().Name}");
            }
        }

        /// <summary>
        /// Allows plugins to configure with the current 
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        internal void Configure(in IApplicationBuilder app, in IHostingEnvironment env)
        {
            foreach (KeyValuePair<string, IPluginModule> plugin in _plugins)
            {
                try
                {
                    plugin.Value.Plugin.Configure(app, env);
                }
                catch (Exception error)
                {
                    _logger.AddToLog(error, $"{plugin.Key}{MethodBase.GetCurrentMethod().Name}");
                }
            }
        }

        /// <summary>
        /// Allows plugins to configure the services for all plugins
        /// </summary>
        /// <param name="services"></param>
        internal void ConfigureServices(IServiceCollection services)
        {
            foreach (KeyValuePair<string, IPluginModule> plugin in _plugins)
            {
                try
                {
                    plugin.Value.Plugin.ConfigureServices(services);
                    services.AddMvc().AddApplicationPart(plugin.Value.Assembly);
                }
                catch (Exception error)
                {
                    _logger.AddToLog(error, $"{plugin.Key}{MethodBase.GetCurrentMethod().Name}");
                }
            }
        }

        /// <summary>
        /// Retreives a specific type of class which inherits from a specific class 
        /// or interface from within the plugin modules
        /// </summary>
        /// <typeparam name="T">Type of interface/class</typeparam>
        /// <returns></returns>
        internal List<T> GetPluginClasses<T>()
        {
            List<T> Result = new List<T>(); 

            foreach (KeyValuePair<string, IPluginModule> plugin in _plugins)
            {
                try
                {
                    foreach (Type type in plugin.Value.Assembly.GetTypes())
                    {
                        try
                        {
                            if ((type.GetInterface(typeof(T).Name) != null) || (type.IsSubclassOf(typeof(T))))
                            {
                                Result.Add((T)Activator.CreateInstance(type));
                            }
                        }
                        catch (Exception typeLoader)
                        {
                            _logger.AddToLog(typeLoader, $"{plugin.Value.Module}{MethodBase.GetCurrentMethod().Name}");
                        }
                    }
                }
                catch (Exception error)
                {
                    _logger.AddToLog(error, $"{plugin.Value.Module}{MethodBase.GetCurrentMethod().Name}");
                }
            }

            return (Result);
        }

        #endregion Internal Methods

        #region IDisposable Methods

        /// <summary>
        /// Disposable method, notify all plugins to finalise
        /// </summary>
        public void Dispose()
        {
            foreach (KeyValuePair<string, IPluginModule> plugin in _plugins)
            {
                try
                {
                    plugin.Value.Plugin.Finalise();
                }
                catch (Exception error)
                {
                    _logger.AddToLog(error, $"{plugin.Key}{MethodBase.GetCurrentMethod().Name}");
                }
            }
        }

        #endregion IDisposable Methods

        #region Private Methods

        /// <summary>
        /// Checks a value, to ensure it is between min/max Value
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <param name="minValue">Min value allowed</param>
        /// <param name="maxValue">Max value allowed</param>
        /// <returns></returns>
        private ushort GetMinMaxValue(in ushort value, in ushort minValue, in ushort maxValue)
        {
            if (value < minValue)
                return (minValue);
            else if (value > maxValue)
                return (maxValue);

            return (value);
        }

        /// <summary>
        /// Returns the first class/interface of type T within the assembly
        /// </summary>
        /// <typeparam name="T">Type to return</typeparam>
        /// <param name="pluginModule">plugin module</param>
        /// <returns>instantiated instance of Type T if found, otherwise null</returns>
        private T GetPluginClass<T>(in IPluginModule pluginModule)
        {
            try
            {
                foreach (Type type in pluginModule.Assembly.GetTypes())
                {
                    try
                    {
                        if ((type.GetInterface(typeof(T).Name) != null) || (type.IsSubclassOf(typeof(T))))
                        {
                            return ((T)Activator.CreateInstance(type));
                        }
                    }
                    catch (Exception typeLoader)
                    {
                        _logger.AddToLog(typeLoader, $"{pluginModule.Module}{MethodBase.GetCurrentMethod().Name}");
                    }
                }
            }
            catch (Exception error)
            {
                _logger.AddToLog(error, $"{pluginModule.Module}{MethodBase.GetCurrentMethod().Name}");
            }

            return (default(T));
        }

        /// <summary>
        /// Retrieves the file path of the host website
        /// </summary>
        /// <param name="resourceName"></param>
        /// <param name="pluginSetting"></param>
        /// <returns></returns>
        private string GetLiveFilePath(in string resourceName, in PluginSetting pluginSetting)
        {
            // remove the first part of the name which is the library
            string Result = resourceName.Substring(resourceName.IndexOf(".") + 1);

            int lastIndex = Result.LastIndexOf('.');

            if (lastIndex > 0)
            {
                Result = Result.Substring(0, lastIndex).Replace(".", "\\") + Result.Substring(lastIndex);
            }

            return (Path.Combine(Directory.GetCurrentDirectory(), Result));
        }

        /// <summary>
        /// Extract Views/CSS/JS files from resources
        /// </summary>
        /// <param name="pluginAssembly"></param>
        /// <param name="pluginSetting"></param>
        private void ExtractResources(in Assembly pluginAssembly, in PluginSetting pluginSetting)
        {
            foreach (string resource in pluginAssembly.GetManifestResourceNames())
            {
                if (String.IsNullOrEmpty(resource))
                    continue;

                using (Stream stream = pluginAssembly.GetManifestResourceStream(resource))
                {
                    string resourceFileName = GetLiveFilePath(resource, pluginSetting);

                    if (File.Exists(resourceFileName) && !pluginSetting.ReplaceExistingResources)
                        continue;

                    string directory = Path.GetDirectoryName(resourceFileName);

                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    using (Stream fileStream = File.OpenWrite(resourceFileName))
                    {
                        byte[] buffer = new byte[stream.Length];

                        stream.Read(buffer, 0, buffer.Length);
                        fileStream.Write(buffer, 0, buffer.Length);
                    }
                }
            }
        }

        /// <summary>
        /// Dynamically loads an assembly
        /// </summary>
        /// <param name="assemblyName">name of assembly</param>
        /// <returns>Assembly instance</returns>
        private Assembly LoadAssembly(in string assemblyName)
        {
            if (String.IsNullOrEmpty(assemblyName))
                throw new ArgumentException(nameof(assemblyName));

            if (!File.Exists(assemblyName))
                throw new ArgumentException(nameof(assemblyName));

            return (Assembly.Load(File.ReadAllBytes(assemblyName)));
        }

        /// <summary>
        /// If associated/required dll's are not found, and settings are configured, 
        /// attempt to load them from the configured path
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Assembly CurrentDomainAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (String.IsNullOrWhiteSpace(_pluginSettings.SystemFiles) || 
                !Directory.Exists(_pluginSettings.SystemFiles))
            {
                return (null);
            }

            AssemblyName assyName = new AssemblyName(args.Name);
            string filename = args.Name.ToLower().Split(',')[0];
            string assembly = Path.Combine(_pluginSettings.SystemFiles, filename);

            if (!assembly.EndsWith(".dll"))
                assembly += ".dll";

            try
            {
                if (File.Exists(assembly))
                    return (Assembly.LoadFrom(assembly));
            }
            catch (Exception error)
            {
                _logger.AddToLog(error, $"{MethodBase.GetCurrentMethod().Name}");
            }

            return (null);
        }

        /// <summary>
        /// Retrieve plugin settings for an individual plugin module
        /// </summary>
        /// <param name="pluginName">Name of plugin</param>
        /// <returns></returns>
        private PluginSetting GetPluginSetting(in string pluginName)
        {
            foreach (PluginSetting setting in _pluginSettings.Plugins)
            {
                if (pluginName.EndsWith(setting.Name))
                    return (setting);
            }

            return (new PluginSetting(pluginName));
        }

        #endregion Private Methods
    }
}