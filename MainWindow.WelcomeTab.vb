Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading.Tasks
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Dialogs
Imports SimpleIDE.Utilities
Imports SimpleIDE.Widgets
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.AI
Imports SimpleIDE.Managers

' MainWindow.WelcomeTab.vb
' Created: 2025-08-15 05:02:31

Partial Public Class MainWindow

    ''' <summary>
    ''' Shows the welcome tab using the custom WelcomeTabWidget with penguin and description
    ''' </summary>
    Private Sub ShowWelcomeTab()
        Try
            Console.WriteLine("ShowWelcomeTab: Starting")
            
            ' Check if Welcome tab already exists
            for i As Integer = 0 To pNotebook.NPages - 1
                If IsWelcomeTab(i) Then
                    ' Welcome tab already exists, just switch to it
                    Console.WriteLine($"  Welcome tab already exists at index {i}")
                    pNotebook.CurrentPage = i
                    Return
                End If
            Next
            
            ' Don't show welcome tab if there are already non-Welcome tabs open
            Dim lHasNonWelcomeTabs As Boolean = False
            for i As Integer = 0 To pNotebook.NPages - 1
                If Not IsWelcomeTab(i) Then
                    lHasNonWelcomeTabs = True
                    Exit for
                End If
            Next
            
            If lHasNonWelcomeTabs Then
                Console.WriteLine("  Non-welcome tabs exist, not showing welcome tab")
                Return
            End If
            
            Console.WriteLine("  Creating new Welcome tab")
            
            ' Create the custom welcome widget
            Dim lWelcomeWidget As New WelcomeTabWidget()
            
            ' Set version info using the actual assembly version
            Dim lVersion As String = GetAssemblyVersion()
            lWelcomeWidget.VersionInfo = $"v{ApplicationVersion.VersionString}"
            
            ' Set IDE description with proper text for wrapping
             
            Dim lDescription As New List(Of String)
            lDescription.Add( "Welcome to SimpleIDE!" )
            lDescription.Add( "Start by creating a new project (File -> New Project)," )
            lDescription.Add( "opening an existing project (File -> Open Project)," )
            lDescription.Add( "opening a folder (File -> Open Folder)," )
            lDescription.Add( "or use keyboard shortcuts (Ctrl+N for new file)." )
            lDescription.Add( " " )
            lDescription.Add( "Copyright © 2024-2025 James Duane Plotts" )
            lDescription.Add( "License granted under GNU GPLv3:" )
            lDescription.Add( "https://www.gnu.org/licenses/gpl-3.0.html" )
            lDescription.Add( " " )
            lDescription.Add( "Website: https://github.com/jamesplotts/simpleide" )
            
            lWelcomeWidget.SetIdeDescription(lDescription)
            
            ' Set recent files from settings
            If pSettingsManager IsNot Nothing Then
                Dim lRecentFiles As List(Of String) = pSettingsManager.GetRecentFiles()
                If lRecentFiles IsNot Nothing AndAlso lRecentFiles.Count > 0 Then
                    ' Filter to only existing files and limit to 10
                    Dim lValidRecentFiles As New List(Of String)()
                    for each lFile As String in lRecentFiles
                        If File.Exists(lFile) Then
                            lValidRecentFiles.Add(lFile)
                            If lValidRecentFiles.Count >= 10 Then
                                Exit for
                            End If
                        End If
                    Next
                    lWelcomeWidget.SetRecentFiles(lValidRecentFiles)
                End If
            End If
            
            ' Apply current theme colors
            ApplyWelcomeTheme(lWelcomeWidget)
            
            ' Wire up event handlers
            AddHandler lWelcomeWidget.NewProjectClicked, AddressOf OnNewProject
            AddHandler lWelcomeWidget.OpenProjectClicked, AddressOf OnOpenProject
            AddHandler lWelcomeWidget.OpenFileClicked, AddressOf OnOpenFile
            AddHandler lWelcomeWidget.RecentFileClicked, AddressOf OnWelcomeRecentFileClicked
            
            ' Add to notebook
            Dim lTabIndex As Integer
            If TypeOf pNotebook Is CustomDrawNotebook Then
                Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                lTabIndex = lCustomNotebook.AppendPage(lWelcomeWidget, "Welcome", Nothing)
                Console.WriteLine($"  Added Welcome tab at index {lTabIndex}")
                
                ' Ensure the notebook is visible
                lCustomNotebook.ShowAll()
            Else
                lTabIndex = pNotebook.AppendPage(lWelcomeWidget, "Welcome")
                Console.WriteLine($"  Added Welcome tab at index {lTabIndex}")
            End If
            
            ' Make sure it's the current page
            pNotebook.CurrentPage = lTabIndex
            
            ' Force scrollbar visibility check after adding to notebook
            ' Use idle handler to ensure widget is fully realized
            GLib.Idle.Add(Function()
                lWelcomeWidget.CheckScrollbarVisibility()
                ' ALSO ensure left panel width is correct
                EnsureLeftPanelWidth()
                Return False  ' Remove idle handler
            End Function)
                        
            Console.WriteLine($"  Set Welcome tab as current page")
            
        Catch ex As Exception
            Console.WriteLine($"ShowWelcomeTab error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Applies the current theme colors to the welcome tab widget
    ''' </summary>
    ''' <param name="vWelcomeWidget">The welcome widget to apply theme to</param>
    Private Sub ApplyWelcomeTheme(vWelcomeWidget As WelcomeTabWidget)
        Try
            ' Determine if using dark theme
            Dim lIsDarkTheme As Boolean = False
            If pSettingsManager IsNot Nothing Then
                ' Check if the current theme is dark based on theme name
                Dim lThemeName As String = pThemeManager.GetCurrentTheme
                lIsDarkTheme = (lThemeName.ToLower().Contains("dark") OrElse 
                               lThemeName.ToLower().Contains("monokai") OrElse
                               lThemeName.ToLower().Contains("dracula"))
            End If
            
            ' Apply the theme using the existing UpdateTheme method
            vWelcomeWidget.UpdateTheme(lIsDarkTheme)
            
        Catch ex As Exception
            Console.WriteLine($"ApplyWelcomeTheme error: {ex.Message}")
        End Try
    End Sub
    
    ' Add: SimpleIDE.MainWindow.OnWelcomeRecentFileClicked
    ' To: MainWindow.WelcomeTab.vb
    
    ''' <summary>
    ''' Handles recent file click from the welcome tab widget
    ''' </summary>
    ''' <param name="vSender">The welcome widget</param>
    ''' <param name="vFilePath">Path to the file to open</param>
    Private Sub OnWelcomeRecentFileClicked(vSender As Object, vFilePath As String)
        Try
            Console.WriteLine($"OnWelcomeRecentFileClicked: Opening {vFilePath}")
            
            If String.IsNullOrEmpty(vFilePath) Then Return
            
            If File.Exists(vFilePath) Then
                If vFilePath.EndsWith(".vbproj") Then
                    ' It's a project file
                    LoadProjectEnhanced(vFilePath)
                Else
                    ' It's a regular file
                    OpenSpecificFile(vFilePath, 1, 1)
                End If
            Else
                ' File no longer exists
                ShowError("File Not Found", $"The file '{vFilePath}' no longer exists.")
                
                ' Remove from recent files
                pSettingsManager?.RemoveRecentFile(vFilePath)
                
                ' Update the welcome tab if it's still showing
                UpdateWelcomeTabRecentFiles()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnWelcomeRecentFileClicked error: {ex.Message}")
            ShowError("Open File error", $"Failed To open file: {ex.Message}")
        End Try
    End Sub
    
    ' Add: SimpleIDE.MainWindow.UpdateWelcomeTabRecentFiles
    ' To: MainWindow.WelcomeTab.vb
    
    ''' <summary>
    ''' Updates the recent files list in the welcome tab if it's currently displayed
    ''' </summary>
    Private Sub UpdateWelcomeTabRecentFiles()
        Try
            ' Find the welcome tab if it exists
            For i As Integer = 0 To pNotebook.NPages - 1
                If IsWelcomeTab(i) Then
                    Dim lPage As Widget = pNotebook.GetNthPage(i)
                    If TypeOf lPage Is WelcomeTabWidget Then
                        Dim lWelcomeWidget As WelcomeTabWidget = DirectCast(lPage, WelcomeTabWidget)
                        
                        ' Get updated recent files list
                        If pSettingsManager IsNot Nothing Then
                            Dim lRecentFiles As List(Of String) = pSettingsManager.GetRecentFiles()
                            If lRecentFiles IsNot Nothing Then
                                ' Filter to only existing files
                                Dim lValidRecentFiles As New List(Of String)()
                                For Each lFile As String In lRecentFiles
                                    If File.Exists(lFile) Then
                                        lValidRecentFiles.Add(lFile)
                                        If lValidRecentFiles.Count >= 10 Then
                                            Exit For
                                        End If
                                    End If
                                Next
                                lWelcomeWidget.SetRecentFiles(lValidRecentFiles)
                            Else
                                lWelcomeWidget.SetRecentFiles(New List(Of String)())
                            End If
                        End If
                        
                        ' Force redraw
                        lWelcomeWidget.QueueDraw()
                    End If
                    Exit For
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"UpdateWelcomeTabRecentFiles error: {ex.Message}")
        End Try
    End Sub
    
    ' Add: SimpleIDE.MainWindow.RefreshWelcomeTab
    ' To: MainWindow.WelcomeTab.vb
    
    ''' <summary>
    ''' Refreshes the welcome tab with current theme and recent files
    ''' </summary>
    Public Sub RefreshWelcomeTab()
        Try
            ' Find the welcome tab if it exists
            For i As Integer = 0 To pNotebook.NPages - 1
                If IsWelcomeTab(i) Then
                    Dim lPage As Widget = pNotebook.GetNthPage(i)
                    If TypeOf lPage Is WelcomeTabWidget Then
                        Dim lWelcomeWidget As WelcomeTabWidget = DirectCast(lPage, WelcomeTabWidget)
                        
                        ' Update theme
                        ApplyWelcomeTheme(lWelcomeWidget)
                        
                        ' Update recent files
                        UpdateWelcomeTabRecentFiles()
                    End If
                    Exit For
                End If
            Next
            
        Catch ex As Exception
            Console.WriteLine($"RefreshWelcomeTab error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handles settings changes that affect the welcome tab
    ''' </summary>
    ''' <param name="vSettingName">Name of the setting that changed</param>
    ''' <param name="vOldValue">Previous value</param>
    ''' <param name="vNewValue">New value</param>
    Private Sub OnSettingsManagerSettingsChanged_WelcomeTab(vSettingName As String, vOldValue As Object, vNewValue As Object)
        Try
            ' Check if the changed setting affects the welcome tab
            Select Case vSettingName
                Case "EditorTheme", "EditorForeground", "EditorBackground"
                    ' Theme-related change - refresh welcome tab appearance
                    RefreshWelcomeTab()
                    
                Case Else
                    ' Other settings don't affect welcome tab
            End Select
            
        Catch ex As Exception
            Console.WriteLine($"OnSettingsManagerSettingsChanged_WelcomeTab error: {ex.Message}")
        End Try
    End Sub

End Class