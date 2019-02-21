using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

namespace MadsKristensen.AddAnyFile
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", Vsix.Version, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.guidAddAnyFilePkgString)]
    public sealed class AddAnyFilePackage : AsyncPackage
    {

        public static DTE2 _dte;
        private string rootNamespace;
        public IEnumerable<ProjectItem> GetProjectItems(EnvDTE.ProjectItems projectItems)
        {
            foreach (EnvDTE.ProjectItem item in projectItems)
            {
                yield return item;

                if (item.SubProject != null)
                {
                    foreach (EnvDTE.ProjectItem childItem in GetProjectItems(item.SubProject.ProjectItems))
                        yield return childItem;
                }
                else
                {
                    foreach (EnvDTE.ProjectItem childItem in GetProjectItems(item.ProjectItems))
                        yield return childItem;
                }
            }

        }
        public IEnumerable<ProjectItem> GetProjects(EnvDTE.ProjectItems projectItems) {

            foreach (EnvDTE.ProjectItem item in projectItems)
            {
                yield return item;

                if (item.SubProject != null)
                {
                    foreach (EnvDTE.ProjectItem childItem in GetProjectItems(item.SubProject.ProjectItems))
                        if (childItem.Kind == EnvDTE.Constants.vsProjectItemKindSolutionItems)
                            yield return childItem;
                }
                else
                {
                    foreach (EnvDTE.ProjectItem childItem in GetProjectItems(item.ProjectItems))
                        if (childItem.Kind == EnvDTE.Constants.vsProjectItemKindSolutionItems)
                            yield return childItem;
                }
            }
        }
        public IEnumerable<EntityInfo> GetEntities(Solution solution)
        {
            var projects = solution.Projects.OfType<Project>();
            foreach (var project in projects)
            {
                var projectitems = GetProjects(project.ProjectItems);
                foreach (EnvDTE.ProjectItem item in projectitems)
                {
                    var projectname = item.Name;
                    var rootnamespace = item.Name;
                    var regex= Regex.Match(projectname, @"(.+)\.Domain");
                    if (regex.Success) {
                        if (item.SubProject != null)
                        {
                            var projectfiles = GetProjectItems(item.SubProject.ProjectItems);
                            foreach (EnvDTE.ProjectItem projectfile in projectfiles) {
                                if (projectfile.FileCodeModel != null) {
                                    foreach (CodeElement code in projectfile.FileCodeModel.CodeElements) {
                                        if (code is EnvDTE.CodeNamespace) {
                                            var ns = code as EnvDTE.CodeNamespace;
                                            foreach (var member in ns.Members) {
                                                var codeType = member as CodeType;
                                                if (codeType == null)
                                                    continue;
                                                var name = codeType.Name;
                                                var fullname = codeType.FullName;
                                                var comment = codeType.Comment;
                                                var doccomment = codeType.DocComment;
                                                var entity = new EntityInfo()
                                                {
                                                    Name = codeType.Name,
                                                    ProjectName = projectname,
                                                    FullName = codeType.FullName,
                                                    RootNameSpace = rootnamespace
                                                };
                                                foreach (var cls in codeType.Bases) {
                                                    var baseClass = cls as CodeClass;
                                                    if (baseClass == null)
                                                        continue;

                                                    entity.BaseClassName ="," + baseClass.Name ;

                                                }
                                                if (!string.IsNullOrEmpty(entity.BaseClassName)) {
                                                    entity.BaseClassName = entity.BaseClassName.Substring(1);
                                                }
                                                var fields = new List<FieldInfo>();
                                                foreach (CodeElement field in codeType.Members)
                                                {
                                                    if (field is CodeProperty)
                                                    {
                                                        var prop = field as CodeProperty;
                                                        var typename = prop.Type.AsString;
                                                        fields.Add(new FieldInfo() { Name = prop.Name, TypeName = typename });
                                                    }
                                                }
                                                if (fields.Count > 0)
                                                {
                                                    entity.Fields = fields.ToArray();
                                                    yield return entity;
                                                }
                                            }

                                        }

                                    }

                                }
                            }
                        }
                    }
                }
            }

        }
        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            _dte = await GetServiceAsync(typeof(DTE)) as DTE2;
            var solution = _dte.Solution;
            
            Logger.Initialize(this, Vsix.Name);
        
            if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService mcs)
            {
                var menuCommandID = new CommandID(PackageGuids.guidAddAnyFileCmdSet, PackageIds.cmdidMyCommand);
                var menuItem = new OleMenuCommand(ExecuteAsync, menuCommandID);
                mcs.AddCommand(menuItem);
            }
        }

        private async void ExecuteAsync(object sender, EventArgs e)
        {
            //var assmbly = System.Reflection.Assembly.LoadFile(@"D:\sample\abp-master\abp-master\samples\BookStore\src\Acme.BookStore.Domain\bin\Debug\netcoreapp2.2\Acme.BookStore.Domain.dll");
            //if (assmbly != null) {
            //    var fullname = assmbly.FullName;
            //    var types = assmbly.GetTypes();
            //    foreach (var ty in types) {
            //        var typename = ty.FullName;
            //    }
            //}

            var entities = GetEntities(AddAnyFilePackage._dte.Solution).ToArray();
            object item = ProjectHelpers.GetSelectedItem();
            
            string folder = FindFolder(item);

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return;

            var selectedItem = item as ProjectItem;
            var selectedProject = item as Project;
            Project project = selectedItem?.ContainingProject ?? selectedProject ?? ProjectHelpers.GetActiveProject();

            if (project == null)
                return;
            var dir = new DirectoryInfo(folder);
            this.rootNamespace = project.GetRootNamespace();
            var viewmodel = new AppServiceDialogViewModel()
            {
                Entities = new System.Windows.Data.CollectionView(entities),
                SelectFolder = dir.Name + "/",
                RootNamespace = this.rootNamespace

            };

            string input = PromptForFileName(folder, viewmodel).TrimStart('/', '\\').Replace("/", "\\");

            if (string.IsNullOrEmpty(input))
                return;

            //string[] parsedInputs = GetParsedInput(input);
            string[] parsedInputs = new string[] {
                viewmodel.DtoClass+".cs",
                viewmodel.CudtoClass+".cs",
                viewmodel.ServiceClass+".cs",
                viewmodel.IServiceClass+".cs"
            };
            foreach (string inputItem in parsedInputs)
            {
                input = inputItem;

                if (input.EndsWith("\\", StringComparison.Ordinal))
                {
                    input = input + "__dummy__";
                }

                var file = new FileInfo(Path.Combine(folder, viewmodel.SubFolder,input));
                string path = file.DirectoryName;

                PackageUtilities.EnsureOutputPath(path);

                if (!file.Exists)
                {
                    int position = await WriteFileAsync(project, file.FullName);

                    try
                    {
                        ProjectItem projectItem = null;
                        if (item is ProjectItem projItem)
                        {
                            if ("{6BB5F8F0-4483-11D3-8BCF-00C04F8EC28C}" == projItem.Kind) // Constants.vsProjectItemKindVirtualFolder
                            {
                                projectItem = projItem.ProjectItems.AddFromFile(file.FullName);
                            }
                        }
                        if (projectItem == null)
                        {
                            projectItem = project.AddFileToProject(file);
                        }

                        if (file.FullName.EndsWith("__dummy__"))
                        {
                            projectItem?.Delete();
                            continue;
                        }

                        VsShellUtilities.OpenDocument(this, file.FullName);

                        // Move cursor into position
                        if (position > 0)
                        {
                            Microsoft.VisualStudio.Text.Editor.IWpfTextView view = ProjectHelpers.GetCurentTextView();

                            if (view != null)
                                view.Caret.MoveTo(new SnapshotPoint(view.TextBuffer.CurrentSnapshot, position));
                        }

                        _dte.ExecuteCommand("SolutionExplorer.SyncWithActiveDocument");
                        _dte.ActiveDocument.Activate();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("The file '" + file + "' already exist.");
                }
            }
        }

        private static async Task<int> WriteFileAsync(Project project, string file)
        {
            string extension = Path.GetExtension(file);
            string template = await TemplateMap.GetTemplateFilePathAsync(project, file);

            if (!string.IsNullOrEmpty(template))
            {
                int index = template.IndexOf('$');

                if (index > -1)
                {
                    template = template.Remove(index, 1);
                }

                await WriteToDiskAsync(file, template);
                return index;
            }

            await WriteToDiskAsync(file, string.Empty);

            return 0;
        }

        private static async System.Threading.Tasks.Task WriteToDiskAsync(string file, string content)
        {
            using (var writer = new StreamWriter(file, false, GetFileEncoding(file)))
            {
                await writer.WriteAsync(content);
            }
        }

        private static Encoding GetFileEncoding(string file)
        {
            string[] noBom = { ".cmd", ".bat", ".json" };
            string ext = Path.GetExtension(file).ToLowerInvariant();

            if (noBom.Contains(ext))
                return new UTF8Encoding(false);

            return new UTF8Encoding(true);
        }

        static string[] GetParsedInput(string input)
        {
            // var tests = new string[] { "file1.txt", "file1.txt, file2.txt", ".ignore", ".ignore.(old,new)", "license", "folder/",
            //    "folder\\", "folder\\file.txt", "folder/.thing", "page.aspx.cs", "widget-1.(html,js)", "pages\\home.(aspx, aspx.cs)",
            //    "home.(html,js), about.(html,js,css)", "backup.2016.(old, new)", "file.(txt,txt,,)", "file_@#d+|%.3-2...3^&.txt" };
            var pattern = new Regex(@"[,]?([^(,]*)([\.\/\\]?)[(]?((?<=[^(])[^,]*|[^)]+)[)]?");
            var results = new List<string>();
            Match match = pattern.Match(input);

            while (match.Success)
            {
                // Always 4 matches w. Group[3] being the extension, extension list, folder terminator ("/" or "\"), or empty string
                string path = match.Groups[1].Value.Trim() + match.Groups[2].Value;
                string[] extensions = match.Groups[3].Value.Split(',');

                foreach (string ext in extensions)
                {
                    string value = path + ext.Trim();

                    // ensure "file.(txt,,txt)" or "file.txt,,file.txt,File.TXT" returns as just ["file.txt"]
                    if (value != "" && !value.EndsWith(".", StringComparison.Ordinal) && !results.Contains(value, StringComparer.OrdinalIgnoreCase))
                    {
                        results.Add(value);
                    }
                }
                match = match.NextMatch();
            }
            return results.ToArray();
        }

        private string PromptForFileName(string folder, AppServiceDialogViewModel viewmodel)
        {
           
            var dialog = new FileNameDialog(viewmodel);

            var hwnd = new IntPtr(_dte.MainWindow.HWnd);
            var window = (System.Windows.Window)HwndSource.FromHwnd(hwnd).RootVisual;
            dialog.Owner = window;

            bool? result = dialog.ShowDialog();
            return (result.HasValue && result.Value) ? dialog.Input : string.Empty;
        }

        private static string FindFolder(object item)
        {
            if (item == null)
                return null;


            if (_dte.ActiveWindow is Window2 window && window.Type == vsWindowType.vsWindowTypeDocument)
            {
                // if a document is active, use the document's containing directory
                Document doc = _dte.ActiveDocument;
                if (doc != null && !string.IsNullOrEmpty(doc.FullName))
                {
                    ProjectItem docItem = _dte.Solution.FindProjectItem(doc.FullName);

                    if (docItem != null && docItem.Properties != null)
                    {
                        string fileName = docItem.Properties.Item("FullPath").Value.ToString();
                        if (File.Exists(fileName))
                            return Path.GetDirectoryName(fileName);
                    }
                }
            }

            string folder = null;

            var projectItem = item as ProjectItem;
            if (projectItem != null && "{6BB5F8F0-4483-11D3-8BCF-00C04F8EC28C}" == projectItem.Kind) //Constants.vsProjectItemKindVirtualFolder
            {
                ProjectItems items = projectItem.ProjectItems;
                foreach (ProjectItem it in items)
                {
                    if (File.Exists(it.FileNames[1]))
                    {
                        folder = Path.GetDirectoryName(it.FileNames[1]);
                        break;
                    }
                }
            }
            else
            {
                var project = item as Project;
                if (projectItem != null)
                {
                    string fileName = projectItem.FileNames[1];

                    if (File.Exists(fileName))
                    {
                        folder = Path.GetDirectoryName(fileName);
                    }
                    else
                    {
                        folder = fileName;
                    }


                }
                else if (project != null)
                {
                    folder = project.GetRootFolder();
                }
            }
            return folder;
        }

        //获取domain 实体对象

    }
    public class EntityInfo{
        public string Name { get; set; }
        public string BaseClassName { get; set; }
        public string FullName { get; set; }
        public string RootNameSpace { get; set; }
        public string ProjectName { get; set; }
        public FieldInfo[] Fields { get; set; }
    }
    public class FieldInfo {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public string ShortTypeName { get {
                var array = this.TypeName.Split('.');
                return array[array.Length - 1];
            } }
    }




     
}