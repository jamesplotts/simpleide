' Widgets/ReferenceManagerTab.vb - Reference management UI, opened as a notebook tab
' rather than a dialog. Manages Assembly/NuGet/Project references for one project at a
' time - when a solution is loaded, a "Project:" picker (matching Object Explorer's own
' project picker) lets the user switch which project is being edited, rather than which
' project it's showing ever being ambiguous or requiring a separate tab per project
Imports Gtk
Imports System.IO
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports System.Linq
Imports SimpleIDE.Managers

Namespace Widgets

    ''' <summary>
    ''' Manages Assembly/NuGet Package/Project references for a single project at a time,
    ''' with a project picker to switch when a solution is loaded. Deliberately does NOT
    ''' implement IEditor, following the same pattern as PreferencesTab/
    ''' AssemblySettingsEditor/SolutionSettingsTab - a settings view, not a text editor
    ''' </summary>
    ''' <remarks>
    ''' Converted from the former Dialogs/ReferenceManagerDialog.vb (modal Dialog) at James'
    ''' request. Originally shipped as one tab per project, keyed by project name - James
    ''' found that confusing/limiting (easy to only ever reach the startup project's tab)
    ''' and asked for a single tab with a project-switcher dropdown instead, the same pattern
    ''' Object Explorer already uses (CustomDrawObjectExplorer.Toolbar.vb's project picker).
    ''' MainWindow now keeps exactly one of these tabs open and calls SwitchToProject on it
    ''' rather than opening a new tab per project.
    ''' </remarks>
    Public Class ReferenceManagerTab
        Inherits Box

        ' ===== Private Fields =====
        Private pNotebook As CustomDrawNotebook
        Private pThemeManager As ThemeManager
        Private pSolutionManager As SolutionManager
        Private pProjectFile As String
        Private pNuGetClientInstance As NuGetClient
        Private pSettingsManager As SettingsManager
        Private pProjectManager As ProjectManager
        Private pTitleLabel As Label
        Private pProjectCombo As CustomDrawComboBox

        ' Assembly tab components
        Private pAssemblyTreeView As TreeView
        Private pAssemblyListStore As ListStore
        Private pAssemblySearchEntry As CustomDrawTextBox
        Private pAssemblyAddButton As CustomDrawButton
        Private pAssemblyRemoveButton As CustomDrawButton
        Private pBrowseAssemblyButton As CustomDrawButton

        ' NuGet tab components - split into an "Installed" list (top) and a searchable
        ' "Available" list (bottom), so browsing/searching the full NuGet index never hides
        ' what's already referenced by the project.
        Private pNuGetInstalledTreeView As TreeView
        Private pNuGetInstalledListStore As ListStore
        Private pNuGetSearchTreeView As TreeView
        Private pNuGetSearchListStore As ListStore
        Private pNuGetSearchEntry As CustomDrawTextBox
        Private pNuGetSearchButton As CustomDrawButton
        Private pNuGetAddButton As CustomDrawButton
        Private pNuGetRemoveButton As CustomDrawButton
        Private pNuGetUpdateButton As CustomDrawButton
        Private pNuGetVersionCombo As CustomDrawComboBox
        Private pNuGetSpinner As Spinner
        Private pNuGetStatusLabel As Label
        Private pCurrentSearchTask As Task(Of NuGetClient.SearchResult)

        ' Tracks whichever of the two NuGet lists currently owns the "active" selection, so
        ' the single shared button bar (Install/Update/Uninstall) knows what to act on.
        Private pNuGetSelectedPackage As NuGetClient.PackageInfo
        Private pNuGetSelectedIter As TreeIter
        Private pNuGetSelectedFromInstalled As Boolean
        Private pNuGetSyncingSelection As Boolean = False

        ' Project tab components
        Private pProjectTreeView As TreeView
        Private pProjectListStore As ListStore
        Private pProjectAddButton As CustomDrawButton
        Private pProjectRemoveButton As CustomDrawButton
        Private pProjectBrowseButton As CustomDrawButton

        ' Current references
        ''' <summary>
        ''' Eagerly initialized (never Nothing) - LoadRuntimeAssemblies() (called from
        ''' BuildUI(), BEFORE the constructor's later LoadCurrentReferences() call actually
        ''' populates this with real data) reads this via LINQ .Any() to pre-check assembly
        ''' rows; a Nothing list there throws ArgumentNullException on the very first
        ''' iteration, silently aborting the whole loop and leaving the Assemblies tab
        ''' permanently empty (confirmed live: "error loading Runtime assemblies: Value
        ''' cannot be null. (Parameter 'source')", 0 rows for every project). The exception
        ''' was swallowed by LoadRuntimeAssemblies' own Try/Catch, so nothing visibly failed -
        ''' the Assembly tab just looked empty, with both its buttons correctly-but-uselessly
        ''' disabled since nothing was ever selectable.
        ''' </summary>
        Private pCurrentReferences As New List(Of ReferenceManager.ReferenceInfo)

        ' ===== Events =====
        Public Event ReferencesChanged()

        ' ===== Public Properties =====

        Public ReadOnly Property Notebook As CustomDrawNotebook
            Get
                Return pNotebook
            End Get
        End Property

        ''' <summary>
        ''' The project this tab manages references for
        ''' </summary>
        Public ReadOnly Property ProjectManager As ProjectManager
            Get
                Return pProjectManager
            End Get
        End Property

        Private ReadOnly Property pReferenceManager() As ReferenceManager
            Get
                Return pProjectManager.ReferenceManager
            End Get
        End Property

        ''' <summary>
        ''' Lazily created on first actual use (NuGet search, or selecting an already-
        ''' installed package to see its version list) rather than eagerly in the
        ''' constructor - NuGetClient's own constructor makes a blocking synchronous network
        ''' call to resolve NuGet's service index, which would otherwise stall opening this
        ''' tab even when the user never touches the NuGet sub-tab at all. Was previously
        ''' declared but never assigned anywhere - every NuGet operation would have thrown
        ''' NullReferenceException (confirmed live: pNuGetClient Is Nothing = True after
        ''' opening the tab), which is why Install/Update never worked
        ''' </summary>
        Private ReadOnly Property pNuGetClient() As NuGetClient
            Get
                If pNuGetClientInstance Is Nothing Then
                    pNuGetClientInstance = New NuGetClient()
                End If
                Return pNuGetClientInstance
            End Get
        End Property

        ' ===== Constructor =====

        ''' <summary>
        ''' Creates a new Reference Manager tab, initially showing one project
        ''' </summary>
        ''' <param name="vProjectManager">The project to manage references for initially</param>
        ''' <param name="vSolutionManager">
        ''' The loaded solution, if any - when it has more than one project, a "Project:"
        ''' picker is shown letting the user switch which project this tab edits; Nothing
        ''' (or a single-project solution) shows a plain, non-interactive title instead
        ''' </param>
        ''' <param name="vThemeManager">Optional ThemeManager for CustomDraw widget theming</param>
        Public Sub New(vProjectManager As ProjectManager, Optional vSolutionManager As SolutionManager = Nothing, Optional vThemeManager As ThemeManager = Nothing)
            MyBase.New(Orientation.Vertical, 5)
            Try
                pProjectManager = vProjectManager
                pProjectFile = vProjectManager.CurrentProjectPath
                pSolutionManager = vSolutionManager
                pThemeManager = vThemeManager

                BuildUI()
                LoadCurrentReferences()

            Catch ex As Exception
                Console.WriteLine($"ReferenceManagerTab constructor error: {ex.Message}")
            End Try
        End Sub

        Private Sub BuildUI()
            Try
                BorderWidth = 10

                If pSolutionManager IsNot Nothing AndAlso pSolutionManager.AllProjects.Count > 1 Then
                    Dim lHeaderBox As New Box(Orientation.Horizontal, 6)

                    Dim lProjectLabel As New Label("Project:")
                    lHeaderBox.PackStart(lProjectLabel, False, False, 0)

                    pProjectCombo = New CustomDrawComboBox()
                    pProjectCombo.ThemeManager = pThemeManager
                    pProjectCombo.WidthRequest = 200
                    for each lProj in pSolutionManager.AllProjects
                        pProjectCombo.AppendText(lProj.CurrentProjectName)
                    Next
                    Dim lActiveIndex As Integer = pProjectCombo.IndexOf(pProjectManager.CurrentProjectName)
                    pProjectCombo.Active = If(lActiveIndex >= 0, lActiveIndex, 0)
                    AddHandler pProjectCombo.Changed, AddressOf OnProjectComboChanged
                    lHeaderBox.PackStart(pProjectCombo, False, False, 0)

                    PackStart(lHeaderBox, False, False, 0)
                Else
                    pTitleLabel = New Label()
                    pTitleLabel.Markup = $"<b>References for: {GLib.Markup.EscapeText(pProjectManager.CurrentProjectName)}</b>"
                    pTitleLabel.Halign = Align.Start
                    PackStart(pTitleLabel, False, False, 0)
                End If

                pNotebook = New CustomDrawNotebook(pThemeManager)

                Dim lCustomNotebook As CustomDrawNotebook = DirectCast(pNotebook, CustomDrawNotebook)
                lCustomNotebook.ShowHidePanelButton = False
                lCustomNotebook.ShowDropdownButton = False
                lCustomNotebook.ShowScrollButtons = False
                lCustomNotebook.ShowTabCloseButtons = False
                lCustomNotebook.BorderWidth = 5

                pNotebook.AppendPage(CreateAssembliesTab(), "Assemblies")
                pNotebook.AppendPage(CreateNuGetTab(), "NuGet Packages")
                pNotebook.AppendPage(CreateProjectsTab(), "Projects")

                PackStart(pNotebook, True, True, 0)

            Catch ex As Exception
                Console.WriteLine($"ReferenceManagerTab.BuildUI error: {ex.Message}")
            End Try
        End Sub

        Private Function CreateAssembliesTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 5)
            lVBox.BorderWidth = 10

            ' Search box
            Dim lSearchBox As New Box(Orientation.Horizontal, 5)
            lSearchBox.PackStart(New Label("Filter:"), False, False, 0)

            pAssemblySearchEntry = New CustomDrawTextBox("Type to filter assemblies...")
            pAssemblySearchEntry.ThemeManager = pThemeManager
            AddHandler pAssemblySearchEntry.Changed, AddressOf OnAssemblySearchChanged
            lSearchBox.PackStart(pAssemblySearchEntry, True, True, 0)

            lVBox.PackStart(lSearchBox, False, False, 0)

            ' TreeView
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.ShadowType = ShadowType.in

            ' Create list store
            pAssemblyListStore = New ListStore(GetType(Boolean), GetType(String), GetType(String), GetType(String), GetType(String), GetType(Object))

            pAssemblyTreeView = New TreeView(pAssemblyListStore)
            pAssemblyTreeView.HeadersVisible = True
            pAssemblyTreeView.RubberBanding = True

            ' Columns
            ' Selected checkbox
            Dim lToggle As New CellRendererToggle()
            lToggle.Activatable = True
            AddHandler lToggle.Toggled, AddressOf OnAssemblyToggled
            Dim lSelectedCol As New TreeViewColumn("", lToggle, "active", 0)
            pAssemblyTreeView.AppendColumn(lSelectedCol)

            ' Name
            pAssemblyTreeView.AppendColumn("Name", New CellRendererText(), "text", 1)

            ' Version
            pAssemblyTreeView.AppendColumn("Version", New CellRendererText(), "text", 2)

            ' Runtime
            pAssemblyTreeView.AppendColumn("Runtime", New CellRendererText(), "text", 3)

            ' Path
            Dim lPathRenderer As New CellRendererText()
            lPathRenderer.Ellipsize = Pango.EllipsizeMode.Middle
            pAssemblyTreeView.AppendColumn("Path", lPathRenderer, "text", 4)

            lScrolled.Add(pAssemblyTreeView)
            lVBox.PackStart(lScrolled, True, True, 0)

            ' Button box
            Dim lButtonBox As New Box(Orientation.Horizontal, 5)

            pBrowseAssemblyButton = New CustomDrawButton("Browse...")
            pBrowseAssemblyButton.ThemeManager = pThemeManager
            AddHandler pBrowseAssemblyButton.Clicked, AddressOf OnBrowseAssembly
            lButtonBox.PackStart(pBrowseAssemblyButton, False, False, 0)

            lButtonBox.PackStart(New Label(""), True, True, 0) ' Spacer

            pAssemblyAddButton = New CustomDrawButton("Add Selected")
            pAssemblyAddButton.ThemeManager = pThemeManager
            pAssemblyAddButton.Sensitive = False
            AddHandler pAssemblyAddButton.Clicked, AddressOf OnAddAssemblies
            lButtonBox.PackStart(pAssemblyAddButton, False, False, 0)

            pAssemblyRemoveButton = New CustomDrawButton("Remove Selected")
            pAssemblyRemoveButton.ThemeManager = pThemeManager
            pAssemblyRemoveButton.Sensitive = False
            AddHandler pAssemblyRemoveButton.Clicked, AddressOf OnRemoveAssemblies
            lButtonBox.PackStart(pAssemblyRemoveButton, False, False, 0)

            lVBox.PackStart(lButtonBox, False, False, 0)

            ' Load runtime assemblies
            LoadRuntimeAssemblies()

            Return lVBox
        End Function

        Private Function CreateNuGetTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 5)
            lVBox.BorderWidth = 10

            ' Vertical paned splitter: installed packages on top, searchable available
            ' packages on the bottom. Resizable by the user via the paned's grip.
            Dim lPaned As New Paned(Orientation.Vertical)

            ' ----- Top half: installed packages -----
            Dim lInstalledBox As New Box(Orientation.Vertical, 5)

            Dim lInstalledLabel As New Label()
            lInstalledLabel.Markup = "<b>Installed Packages</b>"
            lInstalledLabel.Halign = Align.Start
            lInstalledBox.PackStart(lInstalledLabel, False, False, 0)

            Dim lInstalledScrolled As New ScrolledWindow()
            lInstalledScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lInstalledScrolled.ShadowType = ShadowType.in

            pNuGetInstalledListStore = New ListStore(GetType(String), GetType(String), GetType(String), GetType(Long), GetType(Boolean), GetType(String), GetType(Object))

            pNuGetInstalledTreeView = New TreeView(pNuGetInstalledListStore)
            pNuGetInstalledTreeView.HeadersVisible = True
            pNuGetInstalledTreeView.AppendColumn("Package", New CellRendererText(), "text", 0)
            pNuGetInstalledTreeView.AppendColumn("Installed Version", New CellRendererText(), "text", 5)
            AddHandler pNuGetInstalledTreeView.Selection.Changed, AddressOf OnNuGetInstalledSelectionChanged

            lInstalledScrolled.Add(pNuGetInstalledTreeView)
            lInstalledBox.PackStart(lInstalledScrolled, True, True, 0)

            lPaned.Pack1(lInstalledBox, True, False)

            ' ----- Bottom half: search box + available packages -----
            Dim lAvailableBox As New Box(Orientation.Vertical, 5)

            Dim lSearchBox As New Box(Orientation.Horizontal, 5)
            lSearchBox.PackStart(New Label("Search:"), False, False, 0)

            pNuGetSearchEntry = New CustomDrawTextBox("Search NuGet Packages...")
            pNuGetSearchEntry.ThemeManager = pThemeManager
            AddHandler pNuGetSearchEntry.Activated, AddressOf OnNuGetSearch
            lSearchBox.PackStart(pNuGetSearchEntry, True, True, 0)

            pNuGetSearchButton = New CustomDrawButton("Search")
            pNuGetSearchButton.ThemeManager = pThemeManager
            AddHandler pNuGetSearchButton.Clicked, AddressOf OnNuGetSearch
            lSearchBox.PackStart(pNuGetSearchButton, False, False, 0)

            lAvailableBox.PackStart(lSearchBox, False, False, 0)

            Dim lAvailableLabel As New Label()
            lAvailableLabel.Markup = "<b>Available Packages</b>"
            lAvailableLabel.Halign = Align.Start
            lAvailableBox.PackStart(lAvailableLabel, False, False, 0)

            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.ShadowType = ShadowType.in

            pNuGetSearchListStore = New ListStore(GetType(String), GetType(String), GetType(String), GetType(Long), GetType(Boolean), GetType(String), GetType(Object))

            pNuGetSearchTreeView = New TreeView(pNuGetSearchListStore)
            pNuGetSearchTreeView.HeadersVisible = True

            ' Columns
            pNuGetSearchTreeView.AppendColumn("Package", New CellRendererText(), "text", 0)
            pNuGetSearchTreeView.AppendColumn("Version", New CellRendererText(), "text", 1)

            ' Description with wrapping
            Dim lDescRenderer As New CellRendererText()
            lDescRenderer.WrapMode = Pango.WrapMode.Word
            lDescRenderer.WrapWidth = 300
            pNuGetSearchTreeView.AppendColumn("Description", lDescRenderer, "text", 2)

            pNuGetSearchTreeView.AppendColumn("Downloads", New CellRendererText(), "text", 3)

            ' Installed indicator
            Dim lInstalledRenderer As New CellRendererText()
            lInstalledRenderer.Weight = 700 ' Bold
            pNuGetSearchTreeView.AppendColumn("Installed", lInstalledRenderer, "Text", 5)

            ' Selection handler
            AddHandler pNuGetSearchTreeView.Selection.Changed, AddressOf OnNuGetSearchSelectionChanged

            lScrolled.Add(pNuGetSearchTreeView)
            lAvailableBox.PackStart(lScrolled, True, True, 0)

            lPaned.Pack2(lAvailableBox, True, False)

            lVBox.PackStart(lPaned, True, True, 0)

            ' Version selection box
            Dim lVersionBox As New Box(Orientation.Horizontal, 5)
            lVersionBox.PackStart(New Label("Version:"), False, False, 0)

            pNuGetVersionCombo = New CustomDrawComboBox()
            pNuGetVersionCombo.ThemeManager = pThemeManager
            pNuGetVersionCombo.Sensitive = False
            lVersionBox.PackStart(pNuGetVersionCombo, False, False, 0)

            lVBox.PackStart(lVersionBox, False, False, 0)

            ' Status box
            Dim lStatusBox As New Box(Orientation.Horizontal, 5)

            pNuGetSpinner = New Spinner()
            lStatusBox.PackStart(pNuGetSpinner, False, False, 0)

            pNuGetStatusLabel = New Label("Ready")
            lStatusBox.PackStart(pNuGetStatusLabel, False, False, 0)

            lVBox.PackStart(lStatusBox, False, False, 0)

            ' Button box
            Dim lButtonBox As New Box(Orientation.Horizontal, 5)

            lButtonBox.PackStart(New Label(""), True, True, 0) ' Spacer

            pNuGetAddButton = New CustomDrawButton("Install")
            pNuGetAddButton.ThemeManager = pThemeManager
            pNuGetAddButton.Sensitive = False
            AddHandler pNuGetAddButton.Clicked, AddressOf OnInstallPackage
            lButtonBox.PackStart(pNuGetAddButton, False, False, 0)

            pNuGetUpdateButton = New CustomDrawButton("Update")
            pNuGetUpdateButton.ThemeManager = pThemeManager
            pNuGetUpdateButton.Sensitive = False
            AddHandler pNuGetUpdateButton.Clicked, AddressOf OnUpdatePackage
            lButtonBox.PackStart(pNuGetUpdateButton, False, False, 0)

            pNuGetRemoveButton = New CustomDrawButton("Uninstall")
            pNuGetRemoveButton.ThemeManager = pThemeManager
            pNuGetRemoveButton.Sensitive = False
            AddHandler pNuGetRemoveButton.Clicked, AddressOf OnUninstallPackage
            lButtonBox.PackStart(pNuGetRemoveButton, False, False, 0)

            lVBox.PackStart(lButtonBox, False, False, 0)

            ' Load installed packages
            LoadInstalledPackages()

            Return lVBox
        End Function

        Private Function CreateProjectsTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 5)
            lVBox.BorderWidth = 10

            ' Info label
            Dim lInfoLabel As New Label("Add References to other projects in your solution")
            lInfoLabel.Halign = Align.Start
            lVBox.PackStart(lInfoLabel, False, False, 0)

            ' TreeView
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.ShadowType = ShadowType.in

            ' Create list store
            pProjectListStore = New ListStore(GetType(String), GetType(String), GetType(Boolean))

            pProjectTreeView = New TreeView(pProjectListStore)
            pProjectTreeView.HeadersVisible = True

            ' Columns
            pProjectTreeView.AppendColumn("Project", New CellRendererText(), "text", 0)
            pProjectTreeView.AppendColumn("Path", New CellRendererText(), "text", 1)

            ' Selection handler
            AddHandler pProjectTreeView.Selection.Changed, AddressOf OnProjectSelectionChanged

            lScrolled.Add(pProjectTreeView)
            lVBox.PackStart(lScrolled, True, True, 0)

            ' Button box
            Dim lButtonBox As New Box(Orientation.Horizontal, 5)

            pProjectBrowseButton = New CustomDrawButton("Browse...")
            pProjectBrowseButton.ThemeManager = pThemeManager
            AddHandler pProjectBrowseButton.Clicked, AddressOf OnBrowseProject
            lButtonBox.PackStart(pProjectBrowseButton, False, False, 0)

            lButtonBox.PackStart(New Label(""), True, True, 0) ' Spacer

            pProjectAddButton = New CustomDrawButton("Add Reference")
            pProjectAddButton.ThemeManager = pThemeManager
            pProjectAddButton.Sensitive = False
            AddHandler pProjectAddButton.Clicked, AddressOf OnAddProject
            lButtonBox.PackStart(pProjectAddButton, False, False, 0)

            pProjectRemoveButton = New CustomDrawButton("Remove")
            pProjectRemoveButton.ThemeManager = pThemeManager
            pProjectRemoveButton.Sensitive = False
            AddHandler pProjectRemoveButton.Clicked, AddressOf OnRemoveProject
            lButtonBox.PackStart(pProjectRemoveButton, False, False, 0)

            lVBox.PackStart(lButtonBox, False, False, 0)

            ' Load current project references
            LoadProjectReferences()

            Return lVBox
        End Function

        ''' <summary>
        ''' Handles the project picker's selection changing - switches every sub-tab
        ''' (Assemblies/NuGet/Projects) to show the newly-selected project's own references
        ''' </summary>
        Private Sub OnProjectComboChanged(vSender As Object, vArgs As EventArgs)
            Try
                If pSolutionManager Is Nothing OrElse pProjectCombo.Active < 0 Then Return
                Dim lProjects As IReadOnlyList(Of ProjectManager) = pSolutionManager.AllProjects
                If pProjectCombo.Active >= lProjects.Count Then Return

                SwitchToProject(lProjects(pProjectCombo.Active))

            Catch ex As Exception
                Console.WriteLine($"ReferenceManagerTab.OnProjectComboChanged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Switches this tab to show and edit a different project's references - updates the
        ''' picker's own selection (without re-triggering Changed) if called externally (e.g.
        ''' MainWindow re-requesting this tab for a different project's References node)
        ''' </summary>
        ''' <param name="vProjectManager">The project to switch to</param>
        Public Sub SwitchToProject(vProjectManager As ProjectManager)
            Try
                If vProjectManager Is Nothing Then Return

                pProjectManager = vProjectManager
                pProjectFile = vProjectManager.CurrentProjectPath

                If pProjectCombo IsNot Nothing Then
                    Dim lIndex As Integer = pProjectCombo.IndexOf(vProjectManager.CurrentProjectName)
                    If lIndex >= 0 AndAlso pProjectCombo.Active <> lIndex Then
                        RemoveHandler pProjectCombo.Changed, AddressOf OnProjectComboChanged
                        pProjectCombo.Active = lIndex
                        AddHandler pProjectCombo.Changed, AddressOf OnProjectComboChanged
                    End If
                ElseIf pTitleLabel IsNot Nothing Then
                    pTitleLabel.Markup = $"<b>References for: {GLib.Markup.EscapeText(vProjectManager.CurrentProjectName)}</b>"
                End If

                LoadCurrentReferences()

            Catch ex As Exception
                Console.WriteLine($"ReferenceManagerTab.SwitchToProject error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Load current references from project through ProjectManager
        ''' </summary>
        Private Sub LoadCurrentReferences()
            Try
                If pProjectManager IsNot Nothing Then
                    ' Get references through ProjectManager
                    pCurrentReferences = pProjectManager.ProjectReferences

                    ' If not loaded yet, load them
                    If pCurrentReferences Is Nothing OrElse pCurrentReferences.Count = 0 Then
                        pProjectManager.LoadProjectReferences()
                        pCurrentReferences = pProjectManager.ProjectReferences
                    End If
                Else
                    pCurrentReferences = New List(Of ReferenceManager.ReferenceInfo)()
                End If

                ' Update UI to show current references - LoadInstalledPackages belongs here
                ' too, not just in SwitchToProject: CreateNuGetTab() (called from BuildUI(),
                ' which the constructor runs BEFORE this method) already calls it ONCE, but
                ' at that point pCurrentReferences is still the placeholder empty list this
                ' method hasn't populated with real data yet - so the initially-opened
                ' project's NuGet tab was permanently stuck showing zero installed packages,
                ' never getting a second refresh once real data existed. Confirmed live: the
                ' project switched TO via the picker (which explicitly re-calls
                ' LoadInstalledPackages in SwitchToProject) showed its packages correctly,
                ' but the project the tab originally opened for never did.
                UpdateAssemblyList()
                UpdateProjectList()
                LoadInstalledPackages()

                ' Available-list search results and the active selection both belong to
                ' whichever project was previously showing - stale once the project changes.
                pNuGetSearchListStore.Clear()
                ClearNuGetSelectionState()

            Catch ex As Exception
                Console.WriteLine($"Error loading References: {ex.Message}")
            End Try
        End Sub

        ' Load runtime assemblies
        Private Sub LoadRuntimeAssemblies()
            Try
                pAssemblyListStore.Clear()

                ' Get runtime assemblies
                Dim lAssemblies As List(Of AssemblyBrowser.AssemblyInfo) = AssemblyBrowser.GetRuntimeAssemblies()

                for each lAssembly in lAssemblies
                    ' Check if already referenced
                    Dim lIsReferenced As Boolean = pCurrentReferences.any(Function(r) r.Type = ReferenceManager.ReferenceType.eAssembly AndAlso r.Name = lAssembly.Name)

                    Dim lIter As TreeIter = pAssemblyListStore.AppendValues(
                        lIsReferenced,
                        lAssembly.Name,
                        lAssembly.Version,
                        lAssembly.Runtime,
                        lAssembly.Location,
                        lAssembly
                    )
                Next

            Catch ex As Exception
                Console.WriteLine($"error loading Runtime assemblies: {ex.Message}")
            End Try
        End Sub

        ' Assembly search changed
        Private Sub OnAssemblySearchChanged(vSender As Object, vE As EventArgs)
            Try
                Dim lFilter As String = pAssemblySearchEntry.Text.ToLower()

                ' TODO: Implement filtering
                ' For now, just update button state
                UpdateAssemblyButtons()

            Catch ex As Exception
                Console.WriteLine($"error filtering assemblies: {ex.Message}")
            End Try
        End Sub

        ' Assembly toggled
        Private Sub OnAssemblyToggled(vSender As Object, vE As ToggledArgs)
            Try
                Dim lPath As New TreePath(vE.Path)
                Dim lIter As TreeIter

                If pAssemblyListStore.GetIter(lIter, lPath) Then
                    Dim lCurrentValue As Boolean = CBool(pAssemblyListStore.GetValue(lIter, 0))
                    pAssemblyListStore.SetValue(lIter, 0, Not lCurrentValue)

                    UpdateAssemblyButtons()
                End If

            Catch ex As Exception
                Console.WriteLine($"error toggling assembly: {ex.Message}")
            End Try
        End Sub

        ' Update assembly buttons
        Private Sub UpdateAssemblyButtons()
            Try
                Dim lHasSelected As Boolean = False
                Dim lHasUnselected As Boolean = False

                Dim lIter As TreeIter
                If pAssemblyListStore.GetIterFirst(lIter) Then
                    Do
                        Dim lSelected As Boolean = CBool(pAssemblyListStore.GetValue(lIter, 0))
                        If lSelected Then
                            lHasSelected = True
                        Else
                            lHasUnselected = True
                        End If
                    Loop While pAssemblyListStore.IterNext(lIter)
                End If

                pAssemblyAddButton.Sensitive = lHasUnselected
                pAssemblyRemoveButton.Sensitive = lHasSelected

            Catch ex As Exception
                Console.WriteLine($"error updating assembly buttons: {ex.Message}")
            End Try
        End Sub

        ' Browse for assembly
        Private Sub OnBrowseAssembly(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Select Assembly",
                    GetTopLevelWindow(),
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept
                )

                ' Add filters
                Dim lFilter As New FileFilter()
                lFilter.Name = "Assembly Files (*.dll)"
                lFilter.AddPattern("*.dll")
                lDialog.AddFilter(lFilter)

                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)

                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    ' Add the selected assembly
                    Dim lAssemblyPath As String = lDialog.FileName

                    ' TODO: Add to list and mark as selected

                    ' Add to recent
                    AssemblyBrowser.AddToRecentAssemblies(pSettingsManager, lAssemblyPath)
                End If

                lDialog.Destroy()

            Catch ex As Exception
                Console.WriteLine($"error browsing for assembly: {ex.Message}")
            End Try
        End Sub

        ' Add selected assemblies
        Private Sub OnAddAssemblies(vSender As Object, vE As EventArgs)
            Try
                If pProjectManager Is Nothing Then
                    ShowError("No project manager available")
                    Return
                End If

                Dim lAddedCount As Integer = 0
                Dim lIter As TreeIter

                If pAssemblyListStore.GetIterFirst(lIter) Then
                    Do
                        Dim lSelected As Boolean = CBool(pAssemblyListStore.GetValue(lIter, 0))
                        If lSelected Then
                            Dim lAssembly As AssemblyBrowser.AssemblyInfo = CType(pAssemblyListStore.GetValue(lIter, 5), AssemblyBrowser.AssemblyInfo)

                            ' Check if not already referenced
                            If Not pProjectManager.HasReference(lAssembly.Name, ReferenceManager.ReferenceType.eAssembly) Then
                                ' Add reference through ProjectManager
                                If pProjectManager.AddAssemblyReference(lAssembly.Name, lAssembly.Location) Then
                                    lAddedCount += 1
                                End If
                            End If
                        End If
                    Loop While pAssemblyListStore.IterNext(lIter)
                End If

                If lAddedCount > 0 Then
                    ShowInfo($"Added {lAddedCount} assembly Reference(s)")
                    RaiseEvent ReferencesChanged()
                    LoadCurrentReferences()
                End If

            Catch ex As Exception
                Console.WriteLine($"Error adding assemblies: {ex.Message}")
                ShowError($"Error adding assemblies: {ex.Message}")
            End Try
        End Sub

        ' Remove selected assemblies
        Private Sub OnRemoveAssemblies(vSender As Object, vE As EventArgs)
            Try
                If pProjectManager Is Nothing Then
                    ShowError("No project manager available")
                    Return
                End If

                Dim lIter As TreeIter
                Dim lRemovedCount As Integer = 0

                If pAssemblyListStore.GetIterFirst(lIter) Then
                    Do
                        Dim lSelected As Boolean = CBool(pAssemblyListStore.GetValue(lIter, 0))
                        If lSelected Then
                            Dim lAssembly As AssemblyBrowser.AssemblyInfo = CType(pAssemblyListStore.GetValue(lIter, 5), AssemblyBrowser.AssemblyInfo)

                            ' Remove reference through ProjectManager
                            If pProjectManager.RemoveReference(lAssembly.Name, ReferenceManager.ReferenceType.eAssembly) Then
                                lRemovedCount += 1
                            End If
                        End If
                    Loop While pAssemblyListStore.IterNext(lIter)
                End If

                If lRemovedCount > 0 Then
                    ShowInfo($"Removed {lRemovedCount} assembly Reference(s)")
                    RaiseEvent ReferencesChanged()
                    LoadCurrentReferences()
                End If

            Catch ex As Exception
                Console.WriteLine($"Error removing assemblies: {ex.Message}")
                ShowError($"Error removing assemblies: {ex.Message}")
            End Try
        End Sub

        ' NuGet search - narrows the bottom "Available Packages" list only; the top
        ' "Installed Packages" list is unaffected by search.
        Private Sub OnNuGetSearch(vSender As Object, vE As EventArgs)
            Try
                Dim lQuery As String = pNuGetSearchEntry.Text.Trim()
                If String.IsNullOrEmpty(lQuery) Then Return

                ' Cancel previous search if running
                If pCurrentSearchTask IsNot Nothing AndAlso Not pCurrentSearchTask.IsCompleted Then
                    ' TODO: Implement cancellation
                End If

                ' Start search
                pNuGetSpinner.Start()
                pNuGetStatusLabel.Text = "Searching..."
                pNuGetSearchButton.Sensitive = False

                ' Clear current results
                pNuGetSearchListStore.Clear()

                ' Start async search
                pCurrentSearchTask = Task.Run(Async Function() Await pNuGetClient.SearchPackagesAsync(lQuery, 0, 50))
                pCurrentSearchTask.ContinueWith(Sub(t) GLib.Idle.Add(Function() OnNuGetSearchComplete(t)))

            Catch ex As Exception
                Console.WriteLine($"error searching NuGet: {ex.Message}")
                ShowError($"error searching NuGet: {ex.Message}")
            End Try
        End Sub

        ' NuGet search complete
        Private Function OnNuGetSearchComplete(vTask As Task(Of NuGetClient.SearchResult)) As Boolean
            Try
                pNuGetSpinner.Stop()
                pNuGetSearchButton.Sensitive = True

                If vTask.IsFaulted Then
                    pNuGetStatusLabel.Text = "Search failed"
                    ShowError($"Search failed: {vTask.Exception.GetBaseException().Message}")
                    Return False
                End If

                Dim lResult As NuGetClient.SearchResult = vTask.Result
                pNuGetStatusLabel.Text = $"Found {lResult.TotalHits} Packages"

                ' Populate results
                for each lPackage in lResult.Packages
                    ' Check if installed
                    Dim lInstalledVersion As String = ""
                    lPackage.IsInstalled = pNuGetClient.IsPackageInstalled(pProjectFile, lPackage.Id, lInstalledVersion)
                    lPackage.InstalledVersion = lInstalledVersion

                    Dim lIter As TreeIter = pNuGetSearchListStore.AppendValues(
                        lPackage.Id,
                        lPackage.Version,
                        lPackage.Description,
                        lPackage.TotalDownloads,
                        lPackage.IsInstalled,
                        If(lPackage.IsInstalled, lInstalledVersion, ""),
                        lPackage
                    )
                Next

            Catch ex As Exception
                Console.WriteLine($"error completing NuGet search: {ex.Message}")
            End Try

            Return False
        End Function

        ''' <summary>
        ''' Clears whatever package is currently tracked as the "active" NuGet selection and
        ''' disables the shared Install/Update/Uninstall button bar.
        ''' </summary>
        Private Sub ClearNuGetSelectionState()
            pNuGetSelectedPackage = Nothing
            pNuGetAddButton.Sensitive = False
            pNuGetRemoveButton.Sensitive = False
            pNuGetUpdateButton.Sensitive = False
            pNuGetVersionCombo.RemoveAll()
            pNuGetVersionCombo.Sensitive = False
        End Sub

        ' Installed-list selection changed (top half). Selecting here is how a dev manages
        ' an already-referenced package: Uninstall is always available, Update once a newer
        ' version is found.
        Private Sub OnNuGetInstalledSelectionChanged(vSender As Object, vE As EventArgs)
            If pNuGetSyncingSelection Then Return
            Try
                Dim lSelection As TreeSelection = pNuGetInstalledTreeView.Selection
                Dim lIter As TreeIter

                If lSelection.GetSelected(lIter) Then
                    pNuGetSyncingSelection = True
                    pNuGetSearchTreeView.Selection.UnselectAll()
                    pNuGetSyncingSelection = False

                    Dim lPackage As NuGetClient.PackageInfo = CType(pNuGetInstalledListStore.GetValue(lIter, 6), NuGetClient.PackageInfo)
                    pNuGetSelectedPackage = lPackage
                    pNuGetSelectedIter = lIter
                    pNuGetSelectedFromInstalled = True

                    pNuGetAddButton.Sensitive = False
                    pNuGetRemoveButton.Sensitive = True
                    pNuGetUpdateButton.Sensitive = False ' Will check for updates

                    pNuGetVersionCombo.RemoveAll()
                    pNuGetVersionCombo.Sensitive = False

                    Task.Run(Async Function() Await pNuGetClient.GetPackageVersionsAsync(lPackage.Id)).ContinueWith(
                        Sub(t) GLib.Idle.Add(Function() OnPackageVersionsLoaded(t, lPackage))
                    )
                Else
                    ' No installed-list selection - only clear the shared buttons if the
                    ' search list doesn't have an active selection of its own.
                    Dim lSearchIter As TreeIter
                    If Not pNuGetSearchTreeView.Selection.GetSelected(lSearchIter) Then
                        ClearNuGetSelectionState()
                    End If
                End If

            Catch ex As Exception
                Console.WriteLine($"error handling installed Package selection: {ex.Message}")
            End Try
        End Sub

        ' Available/search-list selection changed (bottom half). Selecting here is how a
        ' dev installs something new; already-installed packages are managed from the top
        ' list instead, so Install is the only button this can enable.
        Private Sub OnNuGetSearchSelectionChanged(vSender As Object, vE As EventArgs)
            If pNuGetSyncingSelection Then Return
            Try
                Dim lSelection As TreeSelection = pNuGetSearchTreeView.Selection
                Dim lIter As TreeIter

                If lSelection.GetSelected(lIter) Then
                    pNuGetSyncingSelection = True
                    pNuGetInstalledTreeView.Selection.UnselectAll()
                    pNuGetSyncingSelection = False

                    Dim lPackage As NuGetClient.PackageInfo = CType(pNuGetSearchListStore.GetValue(lIter, 6), NuGetClient.PackageInfo)
                    pNuGetSelectedPackage = lPackage
                    pNuGetSelectedIter = lIter
                    pNuGetSelectedFromInstalled = False

                    pNuGetAddButton.Sensitive = Not lPackage.IsInstalled
                    pNuGetRemoveButton.Sensitive = False
                    pNuGetUpdateButton.Sensitive = False

                    pNuGetVersionCombo.RemoveAll()
                    pNuGetVersionCombo.Sensitive = False

                    Task.Run(Async Function() Await pNuGetClient.GetPackageVersionsAsync(lPackage.Id)).ContinueWith(
                        Sub(t) GLib.Idle.Add(Function() OnPackageVersionsLoaded(t, lPackage))
                    )
                Else
                    Dim lInstalledIter As TreeIter
                    If Not pNuGetInstalledTreeView.Selection.GetSelected(lInstalledIter) Then
                        ClearNuGetSelectionState()
                    End If
                End If

            Catch ex As Exception
                Console.WriteLine($"error handling NuGet search selection: {ex.Message}")
            End Try
        End Sub

        ' Package versions loaded
        Private Function OnPackageVersionsLoaded(vTask As Task(Of List(Of String)), vPackage As NuGetClient.PackageInfo) As Boolean
            Try
                If vTask.IsCompletedSuccessfully Then
                    Dim lVersions As List(Of String) = vTask.Result

                    ' Populate version combo
                    for each lVersion in lVersions
                        pNuGetVersionCombo.AppendText(lVersion)
                    Next

                    ' Select current version
                    If lVersions.Contains(vPackage.Version) Then
                        pNuGetVersionCombo.Active = lVersions.IndexOf(vPackage.Version)
                    ElseIf lVersions.Count > 0 Then
                        pNuGetVersionCombo.Active = 0 ' Latest
                    End If

                    pNuGetVersionCombo.Sensitive = True

                    ' Check if update available
                    If vPackage.IsInstalled AndAlso lVersions.Count > 0 Then
                        Dim lLatestVersion As String = lVersions(0)
                        If lLatestVersion <> vPackage.InstalledVersion Then
                            pNuGetUpdateButton.Sensitive = True
                        End If
                    End If
                End If

            Catch ex As Exception
                Console.WriteLine($"error loading Package Versions: {ex.Message}")
            End Try

            Return False
        End Function

        ''' <summary>
        ''' If the given package also appears as a row in the "Available Packages" search
        ''' list, refreshes that row's Installed columns to match. Keeps the two lists
        ''' consistent without re-running the search after an install/uninstall/update.
        ''' </summary>
        Private Sub RefreshSearchRowInstalledState(vPackageId As String, vInstalled As Boolean, vInstalledVersion As String)
            Dim lIter As TreeIter
            If Not pNuGetSearchListStore.GetIterFirst(lIter) Then Return

            Do
                Dim lRowPackage As NuGetClient.PackageInfo = CType(pNuGetSearchListStore.GetValue(lIter, 6), NuGetClient.PackageInfo)
                If lRowPackage IsNot Nothing AndAlso lRowPackage.Id.Equals(vPackageId, StringComparison.OrdinalIgnoreCase) Then
                    lRowPackage.IsInstalled = vInstalled
                    lRowPackage.InstalledVersion = vInstalledVersion
                    pNuGetSearchListStore.SetValue(lIter, 4, vInstalled)
                    pNuGetSearchListStore.SetValue(lIter, 5, If(vInstalled, vInstalledVersion, ""))
                    Return
                End If
            Loop While pNuGetSearchListStore.IterNext(lIter)
        End Sub

        ' Install package - only ever acted on from the "Available Packages" (search) list;
        ' already-installed packages are managed from the "Installed Packages" list instead.
        Private Sub OnInstallPackage(vSender As Object, vE As EventArgs)
            Try
                If pNuGetSelectedPackage Is Nothing OrElse pNuGetSelectedFromInstalled Then Return

                Dim lPackage As NuGetClient.PackageInfo = pNuGetSelectedPackage
                Dim lIter As TreeIter = pNuGetSelectedIter
                Dim lVersion As String = pNuGetVersionCombo.ActiveText

                If String.IsNullOrEmpty(lVersion) Then
                    lVersion = lPackage.Version
                End If

                ' Add package reference
                If pReferenceManager.AddPackageReference(pProjectFile, lPackage.Id, lVersion) Then
                    ShowInfo($"Installed {lPackage.Id} {lVersion}")
                    RaiseEvent ReferencesChanged()

                    ' Update UI
                    lPackage.IsInstalled = True
                    lPackage.InstalledVersion = lVersion
                    pNuGetSearchListStore.SetValue(lIter, 4, True)
                    pNuGetSearchListStore.SetValue(lIter, 5, lVersion)

                    ' Update buttons
                    pNuGetAddButton.Sensitive = False
                    pNuGetRemoveButton.Sensitive = False

                    ' Refresh the top list so the newly-installed package shows up there
                    LoadInstalledPackages()
                End If

            Catch ex As Exception
                Console.WriteLine($"error installing Package: {ex.Message}")
                ShowError($"error installing Package: {ex.Message}")
            End Try
        End Sub

        ' Update package - only ever acted on from the "Installed Packages" list.
        Private Sub OnUpdatePackage(vSender As Object, vE As EventArgs)
            Try
                If pNuGetSelectedPackage Is Nothing OrElse Not pNuGetSelectedFromInstalled Then Return

                Dim lPackage As NuGetClient.PackageInfo = pNuGetSelectedPackage
                Dim lIter As TreeIter = pNuGetSelectedIter
                Dim lVersion As String = pNuGetVersionCombo.ActiveText

                If String.IsNullOrEmpty(lVersion) Then Return

                ' Update package reference
                If pReferenceManager.UpdatePackageReference(pProjectFile, lPackage.Id, lVersion) Then
                    ShowInfo($"updated {lPackage.Id} to {lVersion}")
                    RaiseEvent ReferencesChanged()

                    ' Update UI
                    lPackage.InstalledVersion = lVersion
                    pNuGetInstalledListStore.SetValue(lIter, 1, lVersion)
                    pNuGetInstalledListStore.SetValue(lIter, 5, lVersion)
                    pNuGetUpdateButton.Sensitive = False

                    RefreshSearchRowInstalledState(lPackage.Id, True, lVersion)
                End If

            Catch ex As Exception
                Console.WriteLine($"error updating Package: {ex.Message}")
                ShowError($"error updating Package: {ex.Message}")
            End Try
        End Sub

        ' Uninstall package - only ever acted on from the "Installed Packages" list.
        Private Sub OnUninstallPackage(vSender As Object, vE As EventArgs)
            Try
                If pNuGetSelectedPackage Is Nothing OrElse Not pNuGetSelectedFromInstalled Then Return

                Dim lPackage As NuGetClient.PackageInfo = pNuGetSelectedPackage

                ' Remove package reference
                If pReferenceManager.RemoveReference(pProjectFile, lPackage.Id, ReferenceManager.ReferenceType.ePackage) Then
                    ShowInfo($"Uninstalled {lPackage.Id}")
                    RaiseEvent ReferencesChanged()

                    RefreshSearchRowInstalledState(lPackage.Id, False, "")

                    ' Refresh the top list (removes this row) and reset the button bar
                    LoadInstalledPackages()
                    ClearNuGetSelectionState()
                End If

            Catch ex As Exception
                Console.WriteLine($"error uninstalling Package: {ex.Message}")
                ShowError($"error uninstalling Package: {ex.Message}")
            End Try
        End Sub

        ' Load installed packages (top list)
        Private Sub LoadInstalledPackages()
            Try
                pNuGetInstalledListStore.Clear()

                ' Get package references
                Dim lPackageRefs As List(Of ReferenceManager.ReferenceInfo) = pCurrentReferences.Where(
                    Function(r) r.Type = ReferenceManager.ReferenceType.ePackage
                ).ToList()

                for each lRef in lPackageRefs
                    Dim lPackage As New NuGetClient.PackageInfo()
                    lPackage.Id = lRef.Name
                    lPackage.Version = lRef.Version
                    lPackage.InstalledVersion = lRef.Version
                    lPackage.IsInstalled = True
                    lPackage.Description = "Installed Package"

                    Dim lIter As TreeIter = pNuGetInstalledListStore.AppendValues(
                        lPackage.Id,
                        lPackage.Version,
                        lPackage.Description,
                        0L,
                        True,
                        lPackage.InstalledVersion,
                        lPackage
                    )
                Next

                pNuGetStatusLabel.Text = $"{lPackageRefs.Count} installed Packages"

            Catch ex As Exception
                Console.WriteLine($"error loading installed Packages: {ex.Message}")
            End Try
        End Sub

        ' Load project references
        Private Sub LoadProjectReferences()
            Try
                pProjectListStore.Clear()

                ' Get project references
                Dim lProjectRefs As List(Of ReferenceManager.ReferenceInfo) = pCurrentReferences.Where(
                    Function(r) r.Type = ReferenceManager.ReferenceType.eProject
                ).ToList()

                for each lRef in lProjectRefs
                    Dim lIter As TreeIter = pProjectListStore.AppendValues(
                        lRef.Name,
                        lRef.Path,
                        True ' Is Reference
                    )
                Next

            Catch ex As Exception
                Console.WriteLine($"error loading project References: {ex.Message}")
            End Try
        End Sub

        ' Update assembly list
        Private Sub UpdateAssemblyList()
            Try
                ' Update checkboxes for referenced assemblies
                Dim lIter As TreeIter
                If pAssemblyListStore.GetIterFirst(lIter) Then
                    Do
                        Dim lAssembly As AssemblyBrowser.AssemblyInfo = CType(pAssemblyListStore.GetValue(lIter, 5), AssemblyBrowser.AssemblyInfo)
                        Dim lIsReferenced As Boolean = pCurrentReferences.any(
                            Function(r) r.Type = ReferenceManager.ReferenceType.eAssembly AndAlso r.Name = lAssembly.Name
                        )
                        pAssemblyListStore.SetValue(lIter, 0, lIsReferenced)
                    Loop While pAssemblyListStore.IterNext(lIter)
                End If

                UpdateAssemblyButtons()

            Catch ex As Exception
                Console.WriteLine($"error updating assembly list: {ex.Message}")
            End Try
        End Sub

        ' Update project list
        Private Sub UpdateProjectList()
            LoadProjectReferences()
        End Sub

        ' Project selection changed
        Private Sub OnProjectSelectionChanged(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pProjectTreeView.Selection
                Dim lIter As TreeIter

                If lSelection.GetSelected(lIter) Then
                    Dim lIsReference As Boolean = CBool(pProjectListStore.GetValue(lIter, 2))

                    pProjectAddButton.Sensitive = Not lIsReference
                    pProjectRemoveButton.Sensitive = lIsReference
                Else
                    pProjectAddButton.Sensitive = False
                    pProjectRemoveButton.Sensitive = False
                End If

            Catch ex As Exception
                Console.WriteLine($"error handling project selection: {ex.Message}")
            End Try
        End Sub

        ' Browse for project
        Private Sub OnBrowseProject(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Select project",
                    GetTopLevelWindow(),
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept
                )

                ' Add filters
                Dim lFilter As New FileFilter()
                lFilter.Name = "project Files"
                lFilter.AddPattern("*.vbproj")
                lFilter.AddPattern("*.csproj")
                lFilter.AddPattern("*.fsproj")
                lDialog.AddFilter(lFilter)

                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)

                ' Set initial directory
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
                lDialog.SetCurrentFolder(lProjectDir)

                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    Dim lSelectedProject As String = lDialog.FileName

                    ' Validate
                    Dim lValidation As ReferenceManager.ValidationResult = pReferenceManager.ValidateProjectReference(pProjectFile, lSelectedProject)

                    If lValidation.IsValid Then
                        ' Add to list
                        Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(lSelectedProject)
                        Dim lRelativePath As String = GetRelativePath(lProjectDir, lSelectedProject)

                        Dim lIter As TreeIter = pProjectListStore.AppendValues(
                            lProjectName,
                            lRelativePath,
                            False ' Not yet a Reference
                        )

                        ' Select it
                        pProjectTreeView.Selection.SelectIter(lIter)
                    Else
                        ShowError(lValidation.ErrorMessage)
                    End If
                End If

                lDialog.Destroy()

            Catch ex As Exception
                Console.WriteLine($"error browsing for project: {ex.Message}")
                ShowError($"error browsing for project: {ex.Message}")
            End Try
        End Sub

        ' Add project reference
        Private Sub OnAddProject(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pProjectTreeView.Selection
                Dim lIter As TreeIter

                If lSelection.GetSelected(lIter) Then
                    Dim lProjectPath As String = CStr(pProjectListStore.GetValue(lIter, 1))

                    ' Make absolute path if relative
                    If Not System.IO.Path.IsPathRooted(lProjectPath) Then
                        Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
                        lProjectPath = System.IO.Path.Combine(lProjectDir, lProjectPath)
                    End If

                    ' Add reference
                    If pReferenceManager.AddProjectReference(pProjectFile, lProjectPath) Then
                        ShowInfo("Added project Reference")
                        RaiseEvent ReferencesChanged()
                        LoadCurrentReferences()
                    End If
                End If

            Catch ex As Exception
                Console.WriteLine($"error adding project Reference: {ex.Message}")
                ShowError($"error adding project Reference: {ex.Message}")
            End Try
        End Sub

        ' Remove project reference
        Private Sub OnRemoveProject(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pProjectTreeView.Selection
                Dim lIter As TreeIter

                If lSelection.GetSelected(lIter) Then
                    Dim lProjectName As String = CStr(pProjectListStore.GetValue(lIter, 0))

                    ' Remove reference
                    If pReferenceManager.RemoveReference(pProjectFile, lProjectName, ReferenceManager.ReferenceType.eProject) Then
                        ShowInfo("Removed project Reference")
                        RaiseEvent ReferencesChanged()
                        LoadCurrentReferences()
                    End If
                End If

            Catch ex As Exception
                Console.WriteLine($"error removing project Reference: {ex.Message}")
                ShowError($"error removing project Reference: {ex.Message}")
            End Try
        End Sub

        ' Get relative path
        Private Function GetRelativePath(vFrom As String, vTo As String) As String
            Try
                Dim lFromUri As New Uri(vFrom & System.IO.Path.DirectorySeparatorChar)
                Dim lToUri As New Uri(vTo)

                Dim lRelativeUri As Uri = lFromUri.MakeRelativeUri(lToUri)
                Dim lRelativePath As String = Uri.UnescapeDataString(lRelativeUri.ToString())

                Return lRelativePath.Replace("/"c, System.IO.Path.DirectorySeparatorChar)

            Catch ex As Exception
                Return vTo
            End Try
        End Function

        ''' <summary>
        ''' Walks up the widget hierarchy to find the top-level Window - needed since this
        ''' tab is a Box, not a Window, but FileChooserDialog/MessageDialog both need a
        ''' parent Window
        ''' </summary>
        Private Function GetTopLevelWindow() As Window
            Try
                Dim lParent As Widget = Me.Parent
                While lParent IsNot Nothing
                    If TypeOf lParent Is Window Then
                        Return DirectCast(lParent, Window)
                    End If
                    lParent = lParent.Parent
                End While
            Catch ex As Exception
                Console.WriteLine($"GetTopLevelWindow error: {ex.Message}")
            End Try
            Return Nothing
        End Function

        ' Show info message
        Private Sub ShowInfo(vMessage As String)
            Dim lDialog As New MessageDialog(
                GetTopLevelWindow(),
                DialogFlags.Modal,
                MessageType.Info,
                ButtonsType.Ok,
                vMessage
            )
            lDialog.Run()
            lDialog.Destroy()
        End Sub

        ' Show error message
        Private Sub ShowError(vMessage As String)
            Dim lDialog As New MessageDialog(
                GetTopLevelWindow(),
                DialogFlags.Modal,
                MessageType.error,
                ButtonsType.Ok,
                vMessage
            )
            lDialog.Run()
            lDialog.Destroy()
        End Sub

    End Class

End Namespace
