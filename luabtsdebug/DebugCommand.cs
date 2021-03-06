﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace luabtsdebug
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class DebugCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int AttachCommandId = 0x0100;
        public const int LaunchCommandId = 0x0101;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("c3667d62-5a02-4ab0-a82d-31b4702e50a0");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private readonly DTE2 dte;

        /// <summary>
        /// Initializes a new instance of the <see cref="DebugCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private DebugCommand(AsyncPackage package, OleMenuCommandService commandService, DTE2 _dte)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            dte = _dte;

            var menuItem = new MenuCommand(this.ExecuteAttach, new CommandID(CommandSet, AttachCommandId));
            commandService.AddCommand(menuItem);
            menuItem = new MenuCommand(this.ExecuteLaunch, new CommandID(CommandSet, LaunchCommandId));
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static DebugCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in DebugCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            DTE2 dte = await package.GetServiceAsync(typeof(SDTE)) as DTE2;
            Instance = new DebugCommand(package, commandService, dte);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void ExecuteAttach(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CallDebuger("attach");
        }

        private void ExecuteLaunch(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            CallDebuger("launch");
        }

        private void CallDebuger(string request)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var data = new Dictionary<string, object>();
            data["request"] = request;
            data["stopOnEntry"] = false;
            data["program"] = "remote debug";

            string path = dte.Solution.FileName;
            while (true)
            {
                int index = path.LastIndexOf('\\');
                if (index < 0)
                    break;
                path = path.Substring(0, index);
                if (Directory.Exists(path + @"\res\lua"))
                {
                    data["resDir"] = path + @"\res";
                    break;
                }
            }

            string tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, CJsonHelper.Save(data));
            string parameters = string.Format(@"/LaunchJson:""{0}"" /EngineGuid:""94eb6cd0-3439-4af2-971c-0327a9daea68""", tempFile);
            dte.Commands.Raise("0ddba113-7ac1-4c6e-a2ef-dcac3f9e731e", 0x0101, parameters, IntPtr.Zero);
            File.Delete(tempFile);
        }
    }
}
