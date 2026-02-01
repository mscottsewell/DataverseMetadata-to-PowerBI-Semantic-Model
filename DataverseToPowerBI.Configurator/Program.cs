// =============================================================================
// Program.cs - Application Entry Point
// =============================================================================
// Purpose: Entry point for the DataverseToPowerBI Configurator standalone application.
//
// This Windows Forms application provides a graphical user interface for:
//   - Connecting to Microsoft Dataverse environments
//   - Selecting solutions, tables, forms, and views
//   - Configuring star-schema relationships
//   - Generating Power BI semantic models in TMDL format
//
// The application uses MSAL (Microsoft Authentication Library) for authentication
// and communicates with Dataverse via the OData Web API.
// =============================================================================

using System;
using System.Windows.Forms;
using DataverseToPowerBI.Configurator.Forms;

namespace DataverseToPowerBI.Configurator
{
    /// <summary>
    /// Application entry point class.
    /// Contains the Main method that starts the Windows Forms application.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The [STAThread] attribute is required for Windows Forms applications
        /// to properly interact with COM components and the Windows clipboard.
        /// </para>
        /// <para>
        /// This method:
        /// </para>
        /// <list type="number">
        ///   <item>Enables visual styles for modern Windows theming</item>
        ///   <item>Sets compatible text rendering for consistent font display</item>
        ///   <item>Launches the MainForm as the primary application window</item>
        /// </list>
        /// </remarks>
        [STAThread]
        static void Main()
        {
            // Enable XP/Vista visual styles for modern look and feel
            Application.EnableVisualStyles();
            
            // Use GDI+ for text rendering (compatible with older controls)
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Start the application with the main form
            // The application runs until MainForm is closed
            Application.Run(new MainForm());
        }
    }
}
