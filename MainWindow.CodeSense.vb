' MainWindow.CodeSense.vb - CodeSense engine ownership and trigger wiring
' Suggestion rendering/interaction now lives entirely in CustomDrawingEditor
' (Editors/CustomDrawingEditor.CodeSensePopup.vb), drawn on the editor's own surface
' instead of a separate GTK popup window
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities
Imports SimpleIDE.Editors
Imports SimpleIDE.Syntax

Partial Public Class MainWindow

    ' Initialize CodeSense system
    Private Sub InitializeCodeSense()
        Try
            Static bolAlreadyRun As Boolean
            If Not bolAlreadyRun Then
                bolAlreadyRun = True
            Else
																RemoveHandler pProjectManager.ProjectChanged, AddressOf OnProjectChangedForCodeSense
            End If
            ' Create CodeSense engine
            pCodeSenseEngine = New CodeSenseEngine()

            ' Update references when project changes
            AddHandler pProjectManager.ProjectChanged, AddressOf OnProjectChangedForCodeSense

        Catch ex As Exception
            Console.WriteLine($"InitializeCodeSense error: {ex.Message}")
        End Try
    End Sub

    ' Handle project change for CodeSense
    Private Sub OnProjectChangedForCodeSense(vProjectFile As String)
        Try
            HideBottomPanel()
            UpdateCodeSenseReferences()
        Catch ex As Exception
            Console.WriteLine($"OnProjectChangedForCodeSense error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Update CodeSense references from project using ProjectManager's parser
    ''' </summary>
    ''' <remarks>
    ''' This method now works with ProjectManager's centralized ProjectParser
    ''' instead of creating its own parser instance
    ''' </remarks>
    Private Sub UpdateCodeSenseReferences()
        Try
            If pCodeSenseEngine Is Nothing Then Return

            ' Clear existing references - ClearReferences() re-seeds the core framework
            ' assemblies (System/System.Core/Microsoft.VisualBasic) itself via
            ' LoadCoreAssemblies(), so no need to (and no reliable way to, since AddReference
            ' expects a file path, not a bare assembly name) re-add them here
            pCodeSenseEngine.ClearReferences()

            ' Add project references from ProjectManager
            If pProjectManager IsNot Nothing AndAlso pProjectManager.IsProjectOpen Then
                ' Get project info from ProjectManager - using the actual property
                If pProjectManager.CurrentProjectInfo IsNot Nothing Then
                    Dim lProjectInfo = pProjectManager.CurrentProjectInfo

                    ' Add assembly references - HintPath (from <Reference><HintPath>) is
                    ' the real resolvable path; .Name alone is just the reference's short
                    ' name and isn't a loadable path. Fall back to searching the build
                    ' output directory if HintPath is missing or stale.
                    for each lRef in lProjectInfo.References
                        Try
                            Dim lPath As String = lRef.HintPath
                            If String.IsNullOrEmpty(lPath) OrElse Not System.IO.File.Exists(lPath) Then
                                lPath = ResolveReferenceAssemblyPath(lProjectInfo.ProjectDirectory, lRef.Name)
                            End If
                            If Not String.IsNullOrEmpty(lPath) Then
                                pCodeSenseEngine.AddReference(lPath)
                            End If
                        Catch ex As Exception
                            Console.WriteLine($"Failed to add Reference {lRef.Name}: {ex.Message}")
                        End Try
                    Next

                    ' Add package references - a <PackageReference> only carries a NuGet
                    ' package Name/Version, never a file path (resolving that requires
                    ' reading NuGet's restore output, not just the raw .vbproj XML) - so
                    ' passing lRef.Name straight to AddReference (which calls
                    ' Assembly.LoadFrom) was trying to load "<ProjectDir>/<PackageName>"
                    ' as a file path on every project load, which never exists. Resolve
                    ' against the project's own build output directory instead, which
                    ' already has every package's DLL copied alongside the project's own
                    ' compiled output after a successful build.
                    for each lRef in lProjectInfo.PackageReferences
                        Try
                            Dim lPath As String = ResolveReferenceAssemblyPath(lProjectInfo.ProjectDirectory, lRef.Name)
                            If Not String.IsNullOrEmpty(lPath) Then
                                pCodeSenseEngine.AddReference(lPath)
                            End If
                        Catch ex As Exception
                            Console.WriteLine($"Failed to add PackageReference {lRef.Name}: {ex.Message}")
                        End Try
                    Next
                End If

                ' Update CodeSenseEngine with project structure from ProjectParser
                Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                If lProjectTree IsNot Nothing Then
                    pCodeSenseEngine.UpdateFromSyntaxTree(lProjectTree, True)
                    Console.WriteLine("CodeSense updated with ProjectParser structure")
                End If
            End If

            Console.WriteLine($"CodeSense references updated from ProjectManager")

        Catch ex As Exception
            Console.WriteLine($"UpdateCodeSenseReferences error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Resolves a bare reference/package name to an actual DLL path by searching the
    ''' project's own build output directory - after a successful build this already
    ''' contains every referenced assembly (project references AND NuGet package
    ''' references) copied alongside the project's own compiled output, so this avoids
    ''' needing to parse NuGet's global package cache or restore metadata
    ''' </summary>
    ''' <param name="vProjectDir">Project's root directory</param>
    ''' <param name="vAssemblyName">Bare assembly/package name - no path, no extension</param>
    ''' <returns>Full path to the resolved DLL, or Nothing if it can't be found (e.g. the project hasn't been built yet)</returns>
    Private Function ResolveReferenceAssemblyPath(vProjectDir As String, vAssemblyName As String) As String
        Try
            If String.IsNullOrEmpty(vProjectDir) OrElse String.IsNullOrEmpty(vAssemblyName) Then Return Nothing

            Dim lConfigurations As String() = {"Debug", "Release"}
            Dim lFrameworks As String() = {"net9.0", "net8.0", "net7.0", "net6.0"}

            for each lConfig in lConfigurations
                for each lFramework in lFrameworks
                    Dim lCandidate As String = System.IO.Path.Combine(vProjectDir, "bin", lConfig, lFramework, $"{vAssemblyName}.dll")
                    If System.IO.File.Exists(lCandidate) Then Return lCandidate
                Next
            Next

            Return Nothing

        Catch ex As Exception
            Console.WriteLine($"ResolveReferenceAssemblyPath error: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Handle CodeSense request from editor using ProjectParser data
    ''' </summary>
    ''' <remarks>
    ''' Gets suggestions from CodeSenseEngine using the latest parse results from
    ''' ProjectManager's centralized ProjectParser, then hands them to the requesting
    ''' editor to draw and manage on its own surface
    ''' </remarks>
    Private Sub OnCodeSenseRequested(vSender As Object, vContext As CodeSenseContext)
        Try
            If pCodeSenseEngine Is Nothing OrElse vContext Is Nothing Then Return

            Dim lEditor As IEditor = TryCast(vSender, IEditor)
            If lEditor Is Nothing Then Return

            ' Find the TabInfo for this editor
            Dim lTabInfo As TabInfo = Nothing
            for each lTabEntry in pOpenTabs
                If lTabEntry.Value.Editor Is lEditor Then
                    lTabInfo = lTabEntry.Value
                    Exit for
                End If
            Next

            If lTabInfo IsNot Nothing Then
                ' Get SourceFileInfo from ProjectManager
                Dim lSourceFileInfo As SourceFileInfo = Nothing
                If pProjectManager IsNot Nothing Then
                    lSourceFileInfo = pProjectManager.GetSourceFileInfo(lTabInfo.FilePath)
                End If

                ' Ensure we have the latest parse from ProjectParser
                If lSourceFileInfo IsNot Nothing Then
                    Dim lSyntaxTree As SyntaxNode = lSourceFileInfo.SyntaxTree
                    If lSyntaxTree Is Nothing AndAlso pProjectManager IsNot Nothing Then
                        ' Request parse through ProjectManager if needed
                        pProjectManager.ParseFile(lSourceFileInfo)
                        lSyntaxTree = lSourceFileInfo.SyntaxTree
                    End If

                    ' Update CodeSense with the parsed structure
                    If lSyntaxTree IsNot Nothing Then
                        pCodeSenseEngine.UpdateDocumentNodes(lSyntaxTree)
                    End If
                End If
            End If

            ' Get suggestions from CodeSenseEngine and hand them to the editor to display
            Dim lSuggestions As List(Of CodeSenseSuggestion) = pCodeSenseEngine.GetSuggestions(vContext)

            If TypeOf lEditor Is CustomDrawingEditor Then
                DirectCast(lEditor, CustomDrawingEditor).ShowCodeSenseSuggestions(lSuggestions, vContext)
            End If

        Catch ex As Exception
            Console.WriteLine($"OnCodeSenseRequested error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handle parse completion from ProjectManager for CodeSense
    ''' </summary>
    Private Sub OnProjectParseCompletedForCodeSense(vFile As SourceFileInfo, vResult As SyntaxNode)
        Try
            ' Update CodeSense with the latest parse results
            If pCodeSenseEngine IsNot Nothing AndAlso vResult IsNot Nothing Then
                ' If this is the current file, update CodeSense immediately
                Dim lCurrentTab As TabInfo = GetCurrentTabInfo()

                ' Check if this file matches current tab
                If lCurrentTab IsNot Nothing AndAlso lCurrentTab.FilePath = vFile.FilePath Then
                    pCodeSenseEngine.UpdateDocumentNodes(vResult)
                    Console.WriteLine($"CodeSense updated with parse results for {vFile.FileName}")
                End If
            End If

        Catch ex As Exception
            Console.WriteLine($"OnProjectParseCompletedForCodeSense error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Handle project structure load from ProjectManager for CodeSense
    ''' </summary>
    Private Sub OnProjectStructureLoadedForCodeSense(vRootNode As SyntaxNode)
        Try
            If pCodeSenseEngine IsNot Nothing AndAlso vRootNode IsNot Nothing Then
                ' Update CodeSense with the complete project structure
                pCodeSenseEngine.UpdateFromSyntaxTree(vRootNode, True)
                Console.WriteLine($"CodeSense updated with project structure from ProjectParser")

                ' Update references as well
                UpdateCodeSenseReferences()
            End If

        Catch ex As Exception
            Console.WriteLine($"OnProjectStructureLoadedForCodeSense error: {ex.Message}")
        End Try
    End Sub


    ''' <summary>
    ''' Initialize CodeSense to work with ProjectManager's centralized parser
    ''' </summary>
    ''' <remarks>
    ''' Sets up CodeSense to consume parse results from ProjectManager.Parser
    ''' instead of performing its own parsing
    ''' </remarks>
    Private Sub InitializeCodeSenseWithProjectManager()
        Try
            If pCodeSenseEngine Is Nothing Then
                pCodeSenseEngine = New CodeSenseEngine()
            End If

            If pProjectManager IsNot Nothing Then
                ' Subscribe to ProjectManager parse events
                RemoveHandler pProjectManager.ParseCompleted, AddressOf OnProjectParseCompletedForCodeSense
                AddHandler pProjectManager.ParseCompleted, AddressOf OnProjectParseCompletedForCodeSense

                RemoveHandler pProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoadedForCodeSense
                AddHandler pProjectManager.ProjectStructureLoaded, AddressOf OnProjectStructureLoadedForCodeSense

                Console.WriteLine("CodeSense subscribed to ProjectManager parse events")
            End If

        Catch ex As Exception
            Console.WriteLine($"InitializeCodeSenseWithProjectManager error: {ex.Message}")
        End Try
    End Sub

    ' Cleanup CodeSense resources
    Private Sub CleanupCodeSense()
        Try
            pCodeSenseEngine?.Dispose()

        Catch ex As Exception
            Console.WriteLine($"CleanupCodeSense error: {ex.Message}")
        End Try
    End Sub

End Class
