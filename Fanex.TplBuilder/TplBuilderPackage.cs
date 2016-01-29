//------------------------------------------------------------------------------
// <copyright file="TplBuilderPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using EnvDTE;
using TplBuilder;
using EnvDTE80;

namespace Fanex.TplBuilder
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(TplBuilderPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string)]
    public sealed class TplBuilderPackage : Package
    {
        private static DocumentEvents _documentEvents;
        private static ProjectItemsEvents _websiteItemsEvents;
        private static ProjectItemsEvents _projectItemsEvents;


        /// <summary>
        /// TplBuilderPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "53801a01-0a7b-4452-8894-72c35b89ffb6";

        /// <summary>
        /// Initializes a new instance of the <see cref="TplBuilderPackage"/> class.
        /// </summary>
        public TplBuilderPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            VisualStudioRef.ApplicationObject = (DTE2)GetService(typeof(DTE));

            RegisterEvents();
        }

        #region Register Events

        private void RegisterEvents()
        {
            _documentEvents = VisualStudioRef.ApplicationObject.Events.DocumentEvents;
            _documentEvents.DocumentSaved += _documentEvents_DocumentSaved;
            InfoLog.Log("DocumentSaved event is registered succesfully");

            // register for website project
            _websiteItemsEvents = VisualStudioRef.ApplicationObject.Events.GetObject("WebSiteItemsEvents") as ProjectItemsEvents;
            if (_websiteItemsEvents != null)
            {
                _websiteItemsEvents.ItemRemoved += _projectItemsEvents_ItemRemoved;
                _websiteItemsEvents.ItemRenamed += _projectItemsEvents_ItemRenamed;
                InfoLog.Log("WebSiteItemsEvents' events are registered succesfully");
            }

            // register for other projects
            Events2 events2 = VisualStudioRef.ApplicationObject.Events as Events2;
            if (events2 != null)
            {
                _projectItemsEvents = events2.ProjectItemsEvents;
                if (_projectItemsEvents != null)
                {
                    _projectItemsEvents.ItemRemoved += _projectItemsEvents_ItemRemoved;
                    _projectItemsEvents.ItemRenamed += _projectItemsEvents_ItemRenamed;
                    InfoLog.Log("ProjectItemsEvents' events are registered succesfully");
                }
            }
        }

        private void _projectItemsEvents_ItemRenamed(ProjectItem ProjectItem, string OldName)
        {
            try
            {
                InfoLog.Log("ItemRenamed is fired");

                // item can a file or a folder
                string itemName = ProjectItem.Name;
                if (itemName == OldName)
                    return;

                var execute = new Execute(ProjectItem.ContainingProject);
                string itemPath = ProjectItem.get_FileNames(0);

                if (itemPath[itemPath.Length - 1] == '\\') // Rename a folder
                {
                    string relativePath = itemPath.Remove(0, execute.ProjectPath.Length);
                    relativePath = relativePath.Remove(relativePath.Length - 1);
                    relativePath = relativePath.Substring(0, relativePath.LastIndexOf('\\') + 1);
                    string tplDir = execute.ProjectPath + execute.OutputPath + relativePath + OldName + '\\';
                    execute.DeleteProjectFolder(tplDir);
                    execute.BuildTemplateFolder(itemPath);
                }
                else
                {
                    if (!OldName.EndsWith(Config.TplExt) || !itemPath.EndsWith(Config.TplExt))
                        return;

                    // Rename a file
                    string dirPath = Utils.GetDirPath(itemPath);
                    string relativePath = dirPath.Remove(0, execute.ProjectPath.Length);
                    string tplDir = execute.ProjectPath + execute.OutputPath + relativePath;

                    // Delete old template class
                    string oldFilePath = tplDir + OldName + Config.CsExt;
                    execute.DeleteProjectFile(oldFilePath);

                    // Build new template class
                    execute.BuildTemplate(itemPath);
                }
            }
            catch (Exception ex)
            {
                InfoLog.Log(ex);
            }
        }

        private void _projectItemsEvents_ItemRemoved(ProjectItem ProjectItem)
        {
            try
            {
                InfoLog.Log("ItemRemoved is fired");

                var execute = new Execute(ProjectItem.ContainingProject);
                string filePath = ProjectItem.get_FileNames(0);

                string relativePath;
                string tplDir;
                if (filePath[filePath.Length - 1] == '\\') // Del a folder
                {
                    relativePath = filePath.Remove(0, execute.ProjectPath.Length);
                    if (relativePath.StartsWith(execute.OutputPath))
                        return; // Del child folder in App_Code\\TplCore
                    tplDir = execute.ProjectPath + execute.OutputPath + relativePath;
                    execute.DeleteProjectFolder(tplDir);
                    return;
                }

                if (!filePath.EndsWith(Config.TplExt))
                    return;

                // Del old template class
                string fileName = Utils.GetFileName(filePath);
                string dirPath = Utils.GetDirPath(filePath);

                relativePath = dirPath.Remove(0, execute.ProjectPath.Length);
                tplDir = execute.ProjectPath + execute.OutputPath + relativePath;

                // Check folder is empty
                if (Utils.IsEmptyFolder(dirPath, Config.CsExt))
                {
                    execute.DeleteProjectFolder(tplDir);
                    return;
                }

                // Delete old template class
                string oldFileName = tplDir + fileName + Config.CsExt;
                execute.DeleteProjectFile(oldFileName);
            }
            catch (Exception ex)
            {
                InfoLog.Log(ex);
            }
        }

        private void _documentEvents_DocumentSaved(Document Document)
        {
            try
            {
                InfoLog.Log("DocumentSaved is fired");

                string filePath = Document.FullName;
                if (!filePath.EndsWith(Config.TplExt))
                    return;

                var execute = new Execute(Document.ProjectItem.ContainingProject);
                execute.BuildTemplate(filePath);
            }
            catch (Exception ex)
            {
                InfoLog.Log(ex);
            }
        }

        #endregion
    }
}
