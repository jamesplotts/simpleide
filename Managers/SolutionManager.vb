' Managers/SolutionManager.vb - Orchestrates multiple ProjectManager instances for a .sln solution
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Managers

    ''' <summary>
    ''' Loads and coordinates every project in a .sln solution, one full ProjectManager
    ''' instance per project
    ''' </summary>
    ''' <remarks>
    ''' Deliberately does NOT fold ProjectManager's internals into multi-project collections -
    ''' ProjectManager has 65+ call sites across its own partial-class files, and 160+ more in
    ''' MainWindow*.vb, that all assume "the one current project"; rewriting those for zero
    ''' behavioral benefit (each ProjectManager instance already correctly resolves its own
    ''' project's symbols/namespace in isolation) would be pure risk. Instead this class owns a
    ''' List(Of ProjectManager), one unmodified instance per solution member, and coordinates
    ''' cross-project concerns (dependency order, cross-project lookups) at this layer.
    ''' Confirmed safe: ProjectManager has no Shared/static mutable state, its constructor does
    ''' no global registration, and its FileSystemWatcher is scoped per-instance to that
    ''' project's own directory - multiple live instances coexist without interference.
    ''' </remarks>
    Public Class SolutionManager

        Private pSolution As Solution
        Private ReadOnly pProjectManagers As New Dictionary(Of String, ProjectManager)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly pDependencyOrder As New List(Of String)
        Private pStartupProjectPath As String

        ''' <summary>
        ''' Gets the parsed solution this manager was loaded from
        ''' </summary>
        Public ReadOnly Property CurrentSolution As Solution
            Get
                Return pSolution
            End Get
        End Property

        ''' <summary>
        ''' Gets every project's ProjectManager instance, in dependency order (a project's own
        ''' references appear before it)
        ''' </summary>
        Public ReadOnly Property AllProjects As IReadOnlyList(Of ProjectManager)
            Get
                Dim lResult As New List(Of ProjectManager)
                for each lPath in pDependencyOrder
                    lResult.Add(pProjectManagers(lPath))
                Next
                Return lResult
            End Get
        End Property

        ''' <summary>
        ''' Gets the full project paths in dependency order (a project's own references appear
        ''' before it) - the order projects should be built or loaded in
        ''' </summary>
        Public ReadOnly Property DependencyOrder As IReadOnlyList(Of String)
            Get
                Return pDependencyOrder.AsReadOnly()
            End Get
        End Property

        ''' <summary>
        ''' Gets the startup project's ProjectManager - the first project listed in the .sln
        ''' </summary>
        ''' <remarks>
        ''' A .sln file has no real concept of "startup project" (Visual Studio stores that in
        ''' a separate .suo/user file, not the .sln itself) - using the first listed project is
        ''' a deliberate, simple heuristic for this phase, matching this exact repo's own
        ''' SimpleIDE.sln (SimpleIDE, the exe, is listed first). A later phase can add real
        ''' startup-project selection if needed.
        ''' </remarks>
        Public ReadOnly Property StartupProject As ProjectManager
            Get
                If String.IsNullOrEmpty(pStartupProjectPath) Then Return Nothing
                Dim lResult As ProjectManager = Nothing
                pProjectManagers.TryGetValue(pStartupProjectPath, lResult)
                Return lResult
            End Get
        End Property

        ''' <summary>
        ''' Gets the loaded ProjectManager for a specific project path, or Nothing if that path
        ''' isn't part of the loaded solution
        ''' </summary>
        ''' <param name="vProjectPath">Full path to the project file</param>
        Public Function GetProjectManager(vProjectPath As String) As ProjectManager
            If String.IsNullOrEmpty(vProjectPath) Then Return Nothing
            Dim lFullPath As String = Path.GetFullPath(vProjectPath)
            Dim lResult As ProjectManager = Nothing
            pProjectManagers.TryGetValue(lFullPath, lResult)
            Return lResult
        End Function

        ''' <summary>
        ''' Finds which loaded project owns a given source file, by checking each project's own
        ''' SourceFiles list
        ''' </summary>
        ''' <param name="vFilePath">Full path to the source file</param>
        ''' <returns>The owning project's ProjectManager, or Nothing if no loaded project owns it</returns>
        Public Function FindOwningProject(vFilePath As String) As ProjectManager
            Try
                If String.IsNullOrEmpty(vFilePath) Then Return Nothing
                Dim lFullPath As String = Path.GetFullPath(vFilePath)
                for each lProjectManager in AllProjects
                    If lProjectManager.CurrentProjectInfo?.ContainsFile(lFullPath) Then
                        Return lProjectManager
                    End If
                Next
                Return Nothing
            Catch ex As Exception
                Console.WriteLine($"SolutionManager.FindOwningProject error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Loads every project declared in a .sln file, one ProjectManager instance per project
        ''' </summary>
        ''' <param name="vSolutionPath">Full path to the .sln file</param>
        ''' <param name="vStartupProjectManager">
        ''' If provided and the solution's first-listed (startup) project matches this
        ''' instance's already-loaded project path, that instance is reused for the startup
        ''' project instead of constructing a new one - lets a caller that already has a
        ''' fully-wired ProjectManager (with UI event handlers already attached) keep using the
        ''' exact same object rather than being left pointed at a stale, disconnected instance
        ''' once a solution loads
        ''' </param>
        ''' <param name="vOnProjectLoading">
        ''' Optional callback invoked just before each project's own LoadProject() call - args
        ''' are (project display name, 1-based index, total project count). Invoked on
        ''' whatever thread LoadSolution itself is called from; callers driving this from a
        ''' background thread must marshal to the UI thread themselves before touching any
        ''' GTK state from inside the callback
        ''' </param>
        ''' <returns>True if the solution file was parsed and at least one project loaded successfully</returns>
        Public Function LoadSolution(vSolutionPath As String, Optional vStartupProjectManager As ProjectManager = Nothing, Optional vOnProjectLoading As Action(Of String, Integer, Integer) = Nothing) As Boolean
            Try
                Dim lSolution As Solution = SolutionFileParser.ParseSolutionFile(vSolutionPath)
                If lSolution Is Nothing OrElse lSolution.Projects.Count = 0 Then
                    #If DEBUG Then
                    Console.WriteLine($"SolutionManager.LoadSolution: No projects found in {vSolutionPath}")
                    #End If
                    Return False
                End If

                pSolution = lSolution
                pProjectManagers.Clear()
                pDependencyOrder.Clear()
                pStartupProjectPath = lSolution.Projects(0).ProjectPath

                Dim lLoadedCount As Integer = 0
                Dim lTotalCount As Integer = lSolution.Projects.Count
                Dim lIndex As Integer = 0
                for each lEntry in lSolution.Projects
                    lIndex += 1
                    If Not File.Exists(lEntry.ProjectPath) Then
                        #If DEBUG Then
                        Console.WriteLine($"SolutionManager.LoadSolution: Skipping missing project file {lEntry.ProjectPath}")
                        #End If
                        Continue for
                    End If

                    vOnProjectLoading?.Invoke(lEntry.Name, lIndex, lTotalCount)

                    Dim lIsStartup As Boolean = String.Equals(lEntry.ProjectPath, pStartupProjectPath, StringComparison.OrdinalIgnoreCase)
                    Dim lProjectManager As ProjectManager = If(lIsStartup AndAlso vStartupProjectManager IsNot Nothing, vStartupProjectManager, New ProjectManager())

                    ' Always (re)load - even when reusing a caller-supplied instance, so its
                    ' state reflects this specific project regardless of what it held before.
                    ' Callers driving their own UI for the startup project through a separate,
                    ' async path (e.g. MainWindow's existing LoadProjectEnhanced) should NOT
                    ' also pass that same live instance here - two concurrent loads of the same
                    ' ProjectManager would race. Passing vStartupProjectManager is only safe
                    ' when the caller isn't loading it through any other path at the same time.
                    If Not lProjectManager.LoadProject(lEntry.ProjectPath) Then
                        #If DEBUG Then
                        Console.WriteLine($"SolutionManager.LoadSolution: Failed to load {lEntry.ProjectPath}")
                        #End If
                        Continue for
                    End If

                    pProjectManagers(lEntry.ProjectPath) = lProjectManager
                    lLoadedCount += 1
                Next

                BuildDependencyOrder()

                #If DEBUG Then
                Console.WriteLine($"SolutionManager.LoadSolution: Loaded {lLoadedCount} of {lSolution.Projects.Count} project(s) from {vSolutionPath}")
                #End If
                Return lLoadedCount > 0

            Catch ex As Exception
                Console.WriteLine($"SolutionManager.LoadSolution error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Computes pDependencyOrder via a topological sort of each loaded project's own
        ''' &lt;ProjectReference&gt; elements (a project's references must appear before it)
        ''' </summary>
        ''' <remarks>
        ''' Each ProjectManager already populates its own ProjectReferences (Managers/
        ''' ProjectManager.ReferenceManager.vb) as part of LoadProject - reused here rather than
        ''' re-parsing the .vbproj XML a second time. A ReferenceInfo.Path for a project
        ''' reference is the raw, still-relative Include attribute value (MSBuild convention:
        ''' relative to the REFERENCING project's own directory), so it's resolved here against
        ''' that project's CurrentProjectDirectory before being matched against the loaded
        ''' project set.
        ''' Kahn's algorithm (in-degree/queue based) - naturally detects cycles: if any nodes
        ''' remain unprocessed once the queue empties, a cycle exists among them. Falls back to
        ''' simple .sln listing order for any such leftover nodes rather than failing outright,
        ''' since a cycle shouldn't prevent the solution from loading, just from having a
        ''' meaningful build/lookup order for the projects involved in it.
        ''' </remarks>
        Private Sub BuildDependencyOrder()
            Try
                ' dependents(X) = set of projects that reference X (X must come before them)
                Dim lDependents As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
                Dim lInDegree As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

                for each lPath in pProjectManagers.Keys
                    lDependents(lPath) = New List(Of String)
                    lInDegree(lPath) = 0
                Next

                for each lKvp in pProjectManagers
                    Dim lProjectPath As String = lKvp.Key
                    Dim lProjectManager As ProjectManager = lKvp.Value
                    Dim lProjectDir As String = lProjectManager.CurrentProjectDirectory

                    for each lRef in lProjectManager.ProjectReferences
                        If lRef.Type <> ReferenceManager.ReferenceType.eProject Then Continue for
                        If String.IsNullOrEmpty(lRef.Path) OrElse String.IsNullOrEmpty(lProjectDir) Then Continue for

                        Dim lReferencedPath As String
                        Try
                            ' <ProjectReference Include="..."> paths follow MSBuild convention
                            ' and commonly use Windows-style backslashes (this exact repo's own
                            ' SimpleIDE.vbproj does) - .NET's own MSBuild tooling normalizes
                            ' that automatically at build time, but a raw Path.Combine on Linux
                            ' treats a literal backslash as just another filename character, not
                            ' a separator, so it must be normalized here first
                            Dim lNormalizedRefPath As String = lRef.Path.Replace("\"c, Path.DirectorySeparatorChar)
                            lReferencedPath = Path.GetFullPath(Path.Combine(lProjectDir, lNormalizedRefPath))
                        Catch
                            Continue for
                        End Try

                        ' Only edges within the loaded solution matter for ordering - a
                        ' reference to a project outside this solution has nothing to order
                        ' against here
                        If Not lDependents.ContainsKey(lReferencedPath) Then Continue for

                        lDependents(lReferencedPath).Add(lProjectPath)
                        lInDegree(lProjectPath) += 1
                    Next
                Next

                Dim lQueue As New Queue(Of String)(
                    lInDegree.Where(Function(kvp) kvp.Value = 0).Select(Function(kvp) kvp.Key))
                Dim lOrdered As New List(Of String)

                While lQueue.Count > 0
                    Dim lNext As String = lQueue.Dequeue()
                    lOrdered.Add(lNext)
                    for each lDependent in lDependents(lNext)
                        lInDegree(lDependent) -= 1
                        If lInDegree(lDependent) = 0 Then lQueue.Enqueue(lDependent)
                    Next
                End While

                ' Any project not yet ordered is part of a reference cycle - append in original
                ' .sln order as a reasonable fallback rather than dropping them
                If lOrdered.Count < pProjectManagers.Count Then
                    #If DEBUG Then
                    Console.WriteLine("SolutionManager.BuildDependencyOrder: Circular project reference(s) detected; affected projects appended in .sln order")
                    #End If
                    for each lEntry in pSolution.Projects
                        If pProjectManagers.ContainsKey(lEntry.ProjectPath) AndAlso Not lOrdered.Contains(lEntry.ProjectPath) Then
                            lOrdered.Add(lEntry.ProjectPath)
                        End If
                    Next
                End If

                pDependencyOrder.Clear()
                pDependencyOrder.AddRange(lOrdered)

            Catch ex As Exception
                Console.WriteLine($"SolutionManager.BuildDependencyOrder error: {ex.Message}")
                ' Fall back to .sln listing order so AllProjects/DependencyOrder still return
                ' something usable even if graph construction itself failed
                pDependencyOrder.Clear()
                If pSolution IsNot Nothing Then
                    for each lEntry in pSolution.Projects
                        If pProjectManagers.ContainsKey(lEntry.ProjectPath) Then pDependencyOrder.Add(lEntry.ProjectPath)
                    Next
                End If
            End Try
        End Sub

    End Class

End Namespace
