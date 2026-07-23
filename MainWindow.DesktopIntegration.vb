' MainWindow.DesktopIntegration.vb - Linux .desktop file / icon integration
'
' On Wayland (notably KDE), GTK windows only get their correct taskbar/title-bar/Alt-Tab
' icon if the compositor can map the running window to an installed .desktop file via a
' matching application ID (WM_CLASS). Without that mapping, Wayland shows a generic
' fallback icon instead of the app's own. See DEVELOPER_SETUP.md for the manual steps this
' automates.
Imports Gtk
Imports System
Imports System.IO
Imports System.Runtime.InteropServices

Partial Public Class MainWindow

    ''' <summary>
    ''' Application ID used for the .desktop file, the installed icon's file name, and
    ''' GLib.Global.ProgramName (set in Program.vb) - all three must match for Wayland to
    ''' resolve the icon correctly
    ''' </summary>
    Private Const DesktopIntegrationAppId As String = "simpleide"

    ''' <summary>
    ''' On Linux, offers to install a .desktop file and icon so the desktop environment can
    ''' show the correct taskbar/Alt-Tab icon and an application-menu entry. Only asks once
    ''' per user (tracked via a setting) and skips entirely if already installed.
    ''' </summary>
    ''' <remarks>
    ''' Called once at startup, deferred via GLib.Idle.Add so the main window is fully shown
    ''' before any dialog can appear
    ''' </remarks>
    Private Sub CheckAndOfferDesktopIntegration()
        Try
            If Not RuntimeInformation.IsOSPlatform(OSPlatform.Linux) Then Return
            If IsDesktopIntegrationInstalled() Then Return
            If pSettingsManager IsNot Nothing AndAlso pSettingsManager.GetBoolean("DesktopIntegrationPrompted", False) Then Return

            Dim lInstall As Boolean = ShowQuestion(
                "Desktop Integration",
                "Install desktop integration for SimpleIDE?" & Environment.NewLine & Environment.NewLine &
                "This adds SimpleIDE to your application menu and fixes the taskbar/Alt-Tab icon " &
                "showing a generic icon instead of SimpleIDE's own (a common issue on Wayland/KDE). " &
                "It only writes to your personal application-data folder - no admin rights needed." &
                Environment.NewLine & Environment.NewLine &
                "You can do this later from Help > Install Desktop Integration.")

            pSettingsManager?.SetBoolean("DesktopIntegrationPrompted", True)

            If lInstall Then
                If InstallDesktopIntegration() Then
                    ShowInfo("Desktop Integration", "Desktop integration installed. It will take effect the next time you launch SimpleIDE.")
                Else
                    ShowError("Desktop Integration", "Could not install desktop integration automatically. See the console output for details, or follow the manual steps in DEVELOPER_SETUP.md.")
                End If
            End If

        Catch ex As Exception
            Console.WriteLine($"CheckAndOfferDesktopIntegration error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles Help > Install Desktop Integration - installs or repairs it on demand,
    ''' regardless of whether it's already installed or was previously declined
    ''' </summary>
    Public Sub OnInstallDesktopIntegration(vSender As Object, vArgs As EventArgs)
        Try
            If Not RuntimeInformation.IsOSPlatform(OSPlatform.Linux) Then
                ShowInfo("Desktop Integration", "Desktop integration is only applicable on Linux.")
                Return
            End If

            If InstallDesktopIntegration() Then
                pSettingsManager?.SetBoolean("DesktopIntegrationPrompted", True)
                ShowInfo("Desktop Integration", "Desktop integration installed/updated. Fully quit and relaunch SimpleIDE for any icon change to take effect.")
            Else
                ShowError("Desktop Integration", "Could not install desktop integration. See the console output for details.")
            End If

        Catch ex As Exception
            Console.WriteLine($"OnInstallDesktopIntegration error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Path to the user-level .desktop file this app installs
    ''' </summary>
    Private Function GetDesktopFilePath() As String
        Return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications", $"{DesktopIntegrationAppId}.desktop")
    End Function

    ''' <summary>
    ''' Whether the .desktop file has already been installed for this user
    ''' </summary>
    Private Function IsDesktopIntegrationInstalled() As Boolean
        Return File.Exists(GetDesktopFilePath())
    End Function

    ''' <summary>
    ''' Writes a .desktop file and a copy of the app icon to the user's local
    ''' application-data directories, and refreshes the desktop database if available
    ''' </summary>
    ''' <returns>True if the .desktop file was written successfully</returns>
    Private Function InstallDesktopIntegration() As Boolean
        Try
            Dim lLocalAppData As String = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)

            ' Read the embedded icon into memory first so we can inspect its actual pixel
            ' dimensions - the icons/hicolor theme spec requires the file to live in a
            ' folder matching its real size (e.g. "128x128/apps"), and hardcoding a size
            ' here previously caused gtk-update-icon-cache to reject the whole theme when
            ' icon.png didn't match the hardcoded "256x256" folder it was written to
            Dim lIconBytes As Byte()
            Using lStream = GetType(MainWindow).Assembly.GetManifestResourceStream("SimpleIDE.icon.png")
                If lStream Is Nothing Then
                    Console.WriteLine("InstallDesktopIntegration: embedded icon resource not found")
                    Return False
                End If
                Using lMemoryStream As New MemoryStream()
                    lStream.CopyTo(lMemoryStream)
                    lIconBytes = lMemoryStream.ToArray()
                End Using
            End Using

            Dim lIconSizeFolder As String
            Using lPixbuf As New Gdk.Pixbuf(lIconBytes)
                lIconSizeFolder = $"{lPixbuf.Width}x{lPixbuf.Height}"
            End Using

            Dim lIconsDir As String = System.IO.Path.Combine(lLocalAppData, "icons", "hicolor", lIconSizeFolder, "apps")
            Directory.CreateDirectory(lIconsDir)
            Dim lIconDestPath As String = System.IO.Path.Combine(lIconsDir, $"{DesktopIntegrationAppId}.png")

            File.WriteAllBytes(lIconDestPath, lIconBytes)

            ' Prefer the actual running executable's path (works for a published/built copy);
            ' fall back to the assembly location if the host process path isn't available
            ' (e.g. some "dotnet run" invocations) - see DEVELOPER_SETUP.md for the caveat
            ' this implies for pure source checkouts
            Dim lExecPath As String = Environment.ProcessPath
            If String.IsNullOrEmpty(lExecPath) Then
                lExecPath = Reflection.Assembly.GetExecutingAssembly().Location
            End If

            Dim lAppsDir As String = System.IO.Path.Combine(lLocalAppData, "applications")
            Directory.CreateDirectory(lAppsDir)

            Dim lDesktopContent As String =
                "[Desktop Entry]" & Environment.NewLine &
                "Type=Application" & Environment.NewLine &
                "Name=SimpleIDE" & Environment.NewLine &
                "Comment=Lightweight VB.NET IDE" & Environment.NewLine &
                $"Exec=""{lExecPath}""" & Environment.NewLine &
                $"Icon={DesktopIntegrationAppId}" & Environment.NewLine &
                "Terminal=false" & Environment.NewLine &
                $"StartupWMClass={DesktopIntegrationAppId}" & Environment.NewLine &
                "Categories=Development;IDE;" & Environment.NewLine

            File.WriteAllText(GetDesktopFilePath(), lDesktopContent)

            ' Best-effort desktop database refresh - not fatal if the tool isn't installed,
            ' most desktop environments will still pick up the file on their own periodic scan
            Try
                Dim lPsi As New Diagnostics.ProcessStartInfo("update-desktop-database")
                lPsi.ArgumentList.Add(lAppsDir)
                lPsi.UseShellExecute = False
                lPsi.RedirectStandardOutput = True
                lPsi.RedirectStandardError = True
                Using lProcess = Diagnostics.Process.Start(lPsi)
                    lProcess?.WaitForExit(5000)
                End Using
            Catch
                ' update-desktop-database not installed - not fatal
            End Try

            Return True

        Catch ex As Exception
            Console.WriteLine($"InstallDesktopIntegration error: {ex.Message}")
            Return False
        End Try
    End Function

End Class
