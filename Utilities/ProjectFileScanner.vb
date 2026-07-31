' Utilities/ProjectFileScanner.vb - Shared VB.NET source file enumeration that skips
' version-control/build/tooling directories, with a user-configurable, persisted
' extension list layered on top of a fixed set of sane defaults
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports SimpleIDE.Managers

Namespace Utilities

    ''' <summary>
    ''' Recursively enumerates *.vb files under a project root while skipping directories
    ''' that should never contribute source files to a scan
    ''' </summary>
    Public Class ProjectFileScanner

        ''' <summary>Setting key holding the user's additional excluded directory names, as
        ''' a comma-separated list, layered on top of DefaultExcludedDirectoryNames</summary>
        Private Const SETTINGS_KEY As String = "ProjectFileScanner.ExcludedDirectories"

        ''' <summary>Directory names always skipped - VCS internals, build output, IDE
        ''' caches, and (specific to this project) nested git worktree checkouts under
        ''' .claude, any of which would otherwise contribute duplicate or stale files to a
        ''' scan rooted at the project directory. Not user-editable; use
        ''' AddExcludedDirectory for project- or preference-specific additions</summary>
        Private Shared ReadOnly DefaultExcludedDirectoryNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".git", ".claude", ".vs", "bin", "obj", "node_modules", "packages"
        }

        ''' <summary>User-configured additional excluded directory names, loaded from
        ''' settings via Initialize()</summary>
        Private Shared pCustomExcludedDirectoryNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        ''' <summary>
        ''' Loads the user's additional excluded directory names from settings. Should be
        ''' called once during application startup, after SettingsManager exists; safe to
        ''' call again later (e.g. after a settings dialog changes the list) to refresh
        ''' </summary>
        Public Shared Sub Initialize(vSettingsManager As SettingsManager)
            Try
                pCustomExcludedDirectoryNames.Clear()
                If vSettingsManager Is Nothing Then Return

                Dim lStored As String = vSettingsManager.GetSetting(SETTINGS_KEY, "")
                For Each lName In lStored.Split(","c)
                    Dim lTrimmed As String = lName.Trim()
                    If Not String.IsNullOrEmpty(lTrimmed) Then
                        pCustomExcludedDirectoryNames.Add(lTrimmed)
                    End If
                Next

            Catch ex As Exception
                Console.WriteLine($"ProjectFileScanner.Initialize error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Adds a directory name to the user-configurable exclusion list and persists it
        ''' </summary>
        Public Shared Sub AddExcludedDirectory(vSettingsManager As SettingsManager, vDirectoryName As String)
            Try
                Dim lTrimmed As String = vDirectoryName?.Trim()
                If String.IsNullOrEmpty(lTrimmed) Then Return
                pCustomExcludedDirectoryNames.Add(lTrimmed)
                PersistCustomExcludedDirectories(vSettingsManager)
            Catch ex As Exception
                Console.WriteLine($"ProjectFileScanner.AddExcludedDirectory error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Removes a directory name from the user-configurable exclusion list and persists
        ''' the change - has no effect on DefaultExcludedDirectoryNames
        ''' </summary>
        Public Shared Sub RemoveExcludedDirectory(vSettingsManager As SettingsManager, vDirectoryName As String)
            Try
                If String.IsNullOrEmpty(vDirectoryName) Then Return
                pCustomExcludedDirectoryNames.Remove(vDirectoryName.Trim())
                PersistCustomExcludedDirectories(vSettingsManager)
            Catch ex As Exception
                Console.WriteLine($"ProjectFileScanner.RemoveExcludedDirectory error: {ex.Message}")
            End Try
        End Sub

        Private Shared Sub PersistCustomExcludedDirectories(vSettingsManager As SettingsManager)
            If vSettingsManager Is Nothing Then Return
            vSettingsManager.SetSetting(SETTINGS_KEY, String.Join(",", pCustomExcludedDirectoryNames))
        End Sub

        ''' <summary>Gets the fixed, non-editable default exclusion list</summary>
        Public Shared ReadOnly Property DefaultExcludedDirectories As IEnumerable(Of String)
            Get
                Return DefaultExcludedDirectoryNames.ToList()
            End Get
        End Property

        ''' <summary>Gets the user-configured additional exclusion list currently in effect</summary>
        Public Shared ReadOnly Property CustomExcludedDirectories As IEnumerable(Of String)
            Get
                Return pCustomExcludedDirectoryNames.ToList()
            End Get
        End Property

        ''' <summary>
        ''' Recursively finds all *.vb files under vRootPath, skipping VCS/build/tooling
        ''' directories (defaults plus any user-configured additions) so nested worktrees
        ''' or build output never contribute duplicate or stale files
        ''' </summary>
        ''' <param name="vRootPath">Directory to scan</param>
        ''' <returns>Full paths of every *.vb file found</returns>
        Public Shared Function GetVBFiles(vRootPath As String) As List(Of String)
            Dim lResults As New List(Of String)
            Try
                If String.IsNullOrEmpty(vRootPath) OrElse Not Directory.Exists(vRootPath) Then
                    Return lResults
                End If
                ScanDirectory(vRootPath, lResults)
            Catch ex As Exception
                Console.WriteLine($"ProjectFileScanner.GetVBFiles error: {ex.Message}")
            End Try
            Return lResults
        End Function

        Private Shared Function IsExcludedDirectory(vName As String) As Boolean
            Return DefaultExcludedDirectoryNames.Contains(vName) OrElse pCustomExcludedDirectoryNames.Contains(vName)
        End Function

        Private Shared Sub ScanDirectory(vDirectoryPath As String, vResults As List(Of String))
            Try
                vResults.AddRange(Directory.GetFiles(vDirectoryPath, "*.vb"))

                For Each lSubDirectory In Directory.GetDirectories(vDirectoryPath)
                    Dim lName As String = Path.GetFileName(lSubDirectory)
                    If Not IsExcludedDirectory(lName) Then
                        ScanDirectory(lSubDirectory, vResults)
                    End If
                Next
            Catch ex As Exception
                Console.WriteLine($"ProjectFileScanner.ScanDirectory error for '{vDirectoryPath}': {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
