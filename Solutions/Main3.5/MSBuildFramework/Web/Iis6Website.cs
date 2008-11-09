//-----------------------------------------------------------------------
// <copyright file="Iis6Website.cs">(c) http://www.codeplex.com/MSBuildExtensionPack. This source is subject to the Microsoft Permissive License. See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx. All other rights reserved.</copyright>
//-----------------------------------------------------------------------
namespace MSBuild.ExtensionPack.Web
{
    using System;
    using System.DirectoryServices;
    using System.Globalization;
    using Microsoft.Build.Framework;

    /// <summary>
    /// <b>Valid TaskActions are:</b>
    /// <para><i>Create</i> (<b>Required: </b> Name <b>Optional:</b> Force, Properties)</para>
    /// <para><i>CheckExists</i> (<b>Required: </b> Name <b>Output: </b>Exists)</para>
    /// <para><i>Continue</i> (<b>Required: </b> Name)</para>
    /// <para><i>Delete</i> (<b>Required: </b> Name)</para>
    /// <para><i>Start</i> (<b>Required: </b> Name)</para>
    /// <para><i>Stop</i> (<b>Required: </b> Name)</para>
    /// <para><i>Pause</i> (<b>Required: </b> Name)</para>
    /// <para><b>Remote Execution Support:</b> Yes</para>
    /// </summary>
    /// <example>
    /// <code lang="xml"><![CDATA[
    /// <Project ToolsVersion="3.5" DefaultTargets="Default" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    ///     <PropertyGroup>
    ///         <TPath>$(MSBuildProjectDirectory)\..\MSBuild.ExtensionPack.tasks</TPath>
    ///         <TPath Condition="Exists('$(MSBuildProjectDirectory)\..\..\Common\MSBuild.ExtensionPack.tasks')">$(MSBuildProjectDirectory)\..\..\Common\MSBuild.ExtensionPack.tasks</TPath>
    ///     </PropertyGroup>
    ///     <Import Project="$(TPath)"/>
    ///     <Target Name="Default">
    ///         <!-- Create a website -->
    ///         <MSBuild.ExtensionPack.Web.Iis6Website TaskAction="Create"  Name="awebsite" Force="true" Properties="AspEnableApplicationRestart=False;AspScriptTimeout=1200;ContentIndexed=False;LogExtFileFlags=917455;ScriptMaps=;ServerBindings=:80:www.free2todev.com;SecureBindings=;ServerAutoStart=True;UseHostName=True"/>
    ///         <!-- Pause a website -->
    ///         <MSBuild.ExtensionPack.Web.Iis6Website TaskAction="Pause" Name="awebsite" />
    ///         <!-- Stop a website -->
    ///         <MSBuild.ExtensionPack.Web.Iis6Website TaskAction="Stop" Name="awebsite" />
    ///         <!-- Start a website -->
    ///         <MSBuild.ExtensionPack.Web.Iis6Website TaskAction="Start" Name="awebsite" />
    ///         <!-- Check whether a website exists -->
    ///         <MSBuild.ExtensionPack.Web.Iis6Website TaskAction="CheckExists" Name="awebsite">
    ///             <Output PropertyName="SiteExists" TaskParameter="Exists"/>
    ///         </MSBuild.ExtensionPack.Web.Iis6Website>
    ///         <Message Text="Website Exists: $(SiteExists)"/>
    ///         <!-- Check whether a website exists -->
    ///         <MSBuild.ExtensionPack.Web.Iis6Website TaskAction="CheckExists" Name="anonwebsite">
    ///             <Output PropertyName="SiteExists" TaskParameter="Exists"/>
    ///         </MSBuild.ExtensionPack.Web.Iis6Website>
    ///         <Message Text="Website Exists: $(SiteExists)"/>
    ///     </Target>
    /// </Project>
    /// ]]></code>    
    /// </example>
    public class Iis6Website : BaseTask
    {
        private DirectoryEntry websiteEntry;
        private string properties;
        private int sleep = 250;

        /// <summary>
        /// Gets or sets the app pool properties.
        /// </summary>
        /// <value>The app pool properties.</value>
        public string Properties
        {
            get { return System.Web.HttpUtility.HtmlDecode(this.properties); }
            set { this.properties = value; }
        }

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// Set force to true to delete an existing website when calling Create. Default is false.
        /// </summary>
        public bool Force { get; set; }

        /// <summary>
        /// Set the sleep time in ms for when calling Start, Stop, Pause or Continue. Default is 250ms.
        /// </summary>
        public int Sleep
        {
            get { return this.sleep; }
            set { this.sleep = value; }
        }

        /// <summary>
        /// Gets whether the website exists.
        /// </summary>
        [Output]
        public bool Exists { get; set; }

        /// <summary>
        /// Gets the IIS path.
        /// </summary>
        /// <value>The IIS path.</value>
        internal string IISPath
        {
            get { return "IIS://" + this.MachineName + "/W3SVC"; }
        }

        /// <summary>
        /// When overridden in a derived class, executes the task.
        /// </summary>
        protected override void InternalExecute()
        {
            switch (this.TaskAction)
            {
                case "Create":
                    this.Create();
                    break;
                case "Delete":
                    this.Delete();
                    break;
                case "Start":
                case "Stop":
                case "Pause":
                case "Continue":
                    this.ControlWebSite();
                    break;
                case "CheckExists":
                    this.CheckSiteExists();
                    break;
                default:
                    this.Log.LogError(string.Format(CultureInfo.CurrentCulture, "Invalid TaskAction passed: {0}", this.TaskAction));
                    return;
            }
        }

        private static void UpdateMetabaseProperty(DirectoryEntry entry, string metabasePropertyName, string metabaseProperty)
        {
            if (metabaseProperty.IndexOf('|') == -1)
            {
                entry.Invoke("Put", metabasePropertyName, metabaseProperty);
                entry.Invoke("SetInfo");
            }
            else
            {
                entry.Invoke("Put", metabasePropertyName, string.Empty);
                entry.Invoke("SetInfo");
                string[] metabaseProperties = metabaseProperty.Split('|');
                foreach (string metabasePropertySplit in metabaseProperties)
                {
                    entry.Properties[metabasePropertyName].Add(metabasePropertySplit);
                }

                entry.CommitChanges();
            }
        }

        private bool CheckSiteExists()
        {
            this.LoadWebsite();
            if (this.websiteEntry != null)
            {
                this.Exists = true;
            }

            return this.Exists;
        }

        private DirectoryEntry LoadWebService()
        {
            return new DirectoryEntry(this.IISPath);
        }

        private void LoadWebsite()
        {
            using (DirectoryEntry webService = this.LoadWebService())
            {
                DirectoryEntries webEntries = webService.Children;

                foreach (DirectoryEntry webEntry in webEntries)
                {
                    if (webEntry.SchemaClassName == "IIsWebServer")
                    {
                        if (string.Compare(this.Name, webEntry.Properties["ServerComment"][0].ToString(), StringComparison.CurrentCultureIgnoreCase) == 0)
                        {
                            this.websiteEntry = webEntry;
                            break;
                        }
                    }

                    webEntry.Dispose();
                }
            }
        }

        private void Create()
        {
            this.LogTaskMessage(string.Format(CultureInfo.CurrentUICulture, "Creating Website: {0}", this.Name));
            using (DirectoryEntry webserviceEntry = this.LoadWebService())
            {
                // We'll try and find the website first.
                this.LoadWebsite();
                if (this.websiteEntry != null)
                {
                    if (this.Force)
                    {
                        this.LogTaskMessage(string.Format(CultureInfo.CurrentUICulture, "Website exists. Deleting Website: {0}", this.Name));
                        this.Delete();
                    }
                    else
                    {
                        Log.LogError(string.Format(CultureInfo.CurrentUICulture, "The Website already exists: {0}", this.Name));
                        return;
                    }
                }

                bool foundSlot = false;
                int websiteIdentifier = 1;
                do
                {
                    try
                    {
                        this.websiteEntry = (DirectoryEntry)webserviceEntry.Invoke("Create", "IIsWebServer", websiteIdentifier);
                        this.websiteEntry.CommitChanges();
                        webserviceEntry.CommitChanges();
                        foundSlot = true;
                    }
                    catch
                    {
                        if (websiteIdentifier > 1000)
                        {
                            Log.LogError(string.Format(CultureInfo.CurrentUICulture, "websiteIdentifier > 1000. Aborting: {0}", this.Name));
                            return;
                        }

                        ++websiteIdentifier;
                    }
                }
                while (foundSlot == false);

                using (DirectoryEntry vdirEntry = (DirectoryEntry)this.websiteEntry.Invoke("Create", "IIsWebVirtualDir", "ROOT"))
                {
                    vdirEntry.CommitChanges();
                    this.websiteEntry.Invoke("Put", "AppFriendlyName", this.Name);
                    this.websiteEntry.Invoke("Put", "ServerComment", this.Name);
                    this.websiteEntry.Invoke("SetInfo");

                    // Now loop through all the metabase properties specified.
                    if (string.IsNullOrEmpty(this.Properties) == false)
                    {
                        string[] propList = this.Properties.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string s in propList)
                        {
                            string[] propPair = s.Split(new[] { '=' });
                            string propName = propPair[0];
                            string propValue = propPair.Length > 1 ? propPair[1] : string.Empty;
                            this.LogTaskMessage(string.Format(CultureInfo.CurrentUICulture, "Adding Property: {0}({1})", propName, propValue));
                            UpdateMetabaseProperty(this.websiteEntry, propName, propValue);
                        }
                    }

                    vdirEntry.CommitChanges();
                    this.websiteEntry.CommitChanges();
                    this.websiteEntry.Dispose();
                }
            }
        }

        private void Delete()
        {
            if (this.CheckSiteExists())
            {
                using (DirectoryEntry webService = this.LoadWebService())
                {
                    object[] args = { "IIsWebServer", Convert.ToInt32(this.websiteEntry.Name, CultureInfo.InvariantCulture) };
                    webService.Invoke("Delete", args);
                }
            }
            else
            {
                Log.LogError(string.Format(CultureInfo.CurrentUICulture, "Website not found: {0}", this.Name));
            }
        }

        private void ControlWebSite()
        {
            if (this.CheckSiteExists())
            {
                this.LogTaskMessage(string.Format(CultureInfo.CurrentUICulture, "{0} Website: {1}", this.TaskAction, this.Name));
                
                // need to insert a sleep as the code occasionaly fails to work without a wait.
                System.Threading.Thread.Sleep(this.Sleep);
                this.websiteEntry.Invoke(this.TaskAction, null);
            }
            else
            {
                Log.LogError(string.Format(CultureInfo.CurrentUICulture, "Website not found: {0}", this.Name));
            }
        }
    }
}